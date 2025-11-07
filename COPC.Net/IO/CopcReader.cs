using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Copc.Geometry;
using Copc.Hierarchy;
using Copc.Utils;

namespace Copc.IO
{
    /// <summary>
    /// Reader for Cloud Optimized Point Cloud (COPC) files.
    /// </summary>
    public class CopcReader : IDisposable
    {
        private Stream? stream;
        private bool leaveOpen;
        private string? filePath;
        private bool disposed;

        /// <summary>
        /// The COPC configuration containing header and metadata.
        /// </summary>
        public CopcConfig Config { get; private set; }

        /// <summary>
        /// Cache of loaded hierarchy pages.
        /// </summary>
        private readonly Dictionary<string, Page> pageCache;

        /// <summary>
        /// Cache of loaded nodes.
        /// </summary>
        private readonly Dictionary<string, Node> nodeCache;

        private CopcReader(CopcConfig config)
        {
            Config = config;
            pageCache = new Dictionary<string, Page>();
            nodeCache = new Dictionary<string, Node>();
            leaveOpen = false;
        }

        /// <summary>
        /// Opens a COPC file from a file path.
        /// </summary>
        public static CopcReader Open(string filePath)
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                var reader = Open(fileStream, false);
                reader.filePath = filePath;
                return reader;
            }
            catch
            {
                fileStream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Opens a COPC file from a stream.
        /// </summary>
        public static CopcReader Open(Stream stream, bool leaveOpen = true)
        {
            // Read the LAS header
            var header = ReadLasHeader(stream);

            // Validate that this is a COPC file (LAS 1.4 required)
            if (header.VersionMajor != 1 || header.VersionMinor != 4)
            {
                throw new InvalidDataException(
                    $"COPC requires LAS 1.4, but file is LAS {header.VersionMajor}.{header.VersionMinor}");
            }

            // Read VLRs
            stream.Position = header.HeaderSize;
            var vlrs = ReadVlrs(stream, header);

            // Find and parse the COPC Info VLR
            var copcInfoVlr = FindVlr(vlrs, "copc", 1);
            if (copcInfoVlr == null)
            {
                throw new InvalidDataException("COPC Info VLR not found. This may not be a COPC file.");
            }

            var copcInfo = CopcInfo.Parse(copcInfoVlr.Data ?? throw new InvalidDataException("COPC Info VLR has no data"));

            // Find WKT VLR (optional)
            string? wkt = null;
            var wktVlr = FindVlr(vlrs, "LASF_Projection", 2112);
            if (wktVlr?.Data != null && wktVlr.Data.Length > 0)
            {
                wkt = wktVlr.Data.ReadNullTerminatedString();
                if (string.IsNullOrWhiteSpace(wkt))
                    wkt = null;
            }

            // Create config
            var config = new CopcConfig(header, copcInfo, null, wkt);

            var reader = new CopcReader(config)
            {
                stream = stream,
                leaveOpen = leaveOpen
            };

            return reader;
        }

        /// <summary>
        /// Reads the LAS header from the stream.
        /// </summary>
        private static LasHeader ReadLasHeader(Stream stream)
        {
            stream.Position = 0;
            
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);
            
            var header = new LasHeader();
            
            // Read LAS header (minimum 227 bytes for LAS 1.0-1.2, 375 for LAS 1.4)
            byte[] signature = reader.ReadBytes(4);
            if (Encoding.ASCII.GetString(signature) != "LASF")
                throw new InvalidDataException("Invalid LAS file signature");
                
            header.FileSourceID = reader.ReadUInt16();
            header.GlobalEncoding = reader.ReadUInt16();
            header.ProjectIDGuidData1 = reader.ReadUInt32();
            header.ProjectIDGuidData2 = reader.ReadUInt16();
            header.ProjectIDGuidData3 = reader.ReadUInt16();
            
            // Read into pre-allocated arrays
            byte[] guid4 = reader.ReadBytes(8);
            Array.Copy(guid4, header.ProjectIDGuidData4, 8);
            
            header.VersionMajor = reader.ReadByte();
            header.VersionMinor = reader.ReadByte();
            
            // Read into pre-allocated arrays
            byte[] sysId = reader.ReadBytes(32);
            Array.Copy(sysId, header.SystemIdentifier, 32);
            byte[] genSoft = reader.ReadBytes(32);
            Array.Copy(genSoft, header.GeneratingSoftware, 32);
            header.FileCreationDay = reader.ReadUInt16();
            header.FileCreationYear = reader.ReadUInt16();
            header.HeaderSize = reader.ReadUInt16();
            header.OffsetToPointData = reader.ReadUInt32();
            header.NumberOfVariableLengthRecords = reader.ReadUInt32();
            header.PointDataFormat = reader.ReadByte();
            header.PointDataRecordLength = reader.ReadUInt16();
            header.NumberOfPointRecords = reader.ReadUInt32();
            
            for (int i = 0; i < 5; i++)
                header.NumberOfPointsByReturn[i] = reader.ReadUInt32();
                
            header.XScaleFactor = reader.ReadDouble();
            header.YScaleFactor = reader.ReadDouble();
            header.ZScaleFactor = reader.ReadDouble();
            header.XOffset = reader.ReadDouble();
            header.YOffset = reader.ReadDouble();
            header.ZOffset = reader.ReadDouble();
            header.MaxX = reader.ReadDouble();
            header.MinX = reader.ReadDouble();
            header.MaxY = reader.ReadDouble();
            header.MinY = reader.ReadDouble();
            header.MaxZ = reader.ReadDouble();
            header.MinZ = reader.ReadDouble();
            
            // LAS 1.3+
            if (header.VersionMinor >= 3)
            {
                header.StartOfWaveformDataPacketRecord = reader.ReadUInt64();
            }
            
            // LAS 1.4+
            if (header.VersionMinor >= 4)
            {
                header.StartOfFirstExtendedVariableLengthRecord = reader.ReadUInt64();
                header.NumberOfExtendedVariableLengthRecords = reader.ReadUInt32();
                header.ExtendedNumberOfPointRecords = reader.ReadUInt64();
                
                for (int i = 0; i < 15; i++)
                    header.ExtendedNumberOfPointsByReturn[i] = reader.ReadUInt64();
            }

            return header;
        }

        /// <summary>
        /// Reads all VLRs from the stream.
        /// </summary>
        private static List<LasVariableLengthRecord> ReadVlrs(Stream stream, LasHeader header)
        {
            var vlrs = new List<LasVariableLengthRecord>();

            for (int i = 0; i < header.NumberOfVariableLengthRecords; i++)
            {
                var vlr = new LasVariableLengthRecord();

                using var reader = new BinaryReader(stream, Encoding.ASCII, true);

                vlr.Reserved = reader.ReadUInt16();
                vlr.UserID = reader.ReadBytes(16);
                vlr.RecordID = reader.ReadUInt16();
                vlr.RecordLengthAfterHeader = reader.ReadUInt16();
                vlr.Description = reader.ReadBytes(32);

                if (vlr.RecordLengthAfterHeader > 0)
                {
                    vlr.Data = reader.ReadBytes(vlr.RecordLengthAfterHeader);
                }

                vlrs.Add(vlr);
            }

            return vlrs;
        }

        /// <summary>
        /// Finds a VLR by user ID and record ID.
        /// </summary>
        private static LasVariableLengthRecord? FindVlr(List<LasVariableLengthRecord> vlrs, string userId, ushort recordId)
        {
            foreach (var vlr in vlrs)
            {
                var vlrUserId = vlr.UserID.ReadNullTerminatedString();
                if (vlrUserId == userId && vlr.RecordID == recordId)
                {
                    return vlr;
                }
            }
            return null;
        }

        /// <summary>
        /// Loads the root hierarchy page.
        /// </summary>
        public Page LoadRootHierarchyPage()
        {
            var rootKey = VoxelKey.RootKey();
            var rootPageKey = rootKey.ToString();

            if (pageCache.TryGetValue(rootPageKey, out var cachedPage))
            {
                return cachedPage;
            }

            var rootPage = new Page(
                rootKey,
                (long)Config.CopcInfo.RootHierarchyOffset,
                (int)Config.CopcInfo.RootHierarchySize
            );

            LoadHierarchyPage(rootPage);
            pageCache[rootPageKey] = rootPage;

            return rootPage;
        }

        /// <summary>
        /// Loads a hierarchy page from the file.
        /// </summary>
        public void LoadHierarchyPage(Page page)
        {
            if (page.Loaded || stream == null)
                return;

            stream.Position = page.Offset;
            byte[] data = stream.ReadExactly(page.ByteSize);

            var subtree = ParseHierarchyPage(data);

            // Add nodes and pages to the page's children
            foreach (var kvp in subtree.Nodes)
            {
                var node = kvp.Value;
                node.PageKey = page.Key;
                page.Children[kvp.Key] = node;
                nodeCache[kvp.Key] = node;
            }

            foreach (var kvp in subtree.Pages)
            {
                page.Children[kvp.Key] = kvp.Value;
                pageCache[kvp.Key] = kvp.Value;
            }

            page.Loaded = true;
        }

        /// <summary>
        /// Parses a hierarchy page from binary data.
        /// </summary>
        private static HierarchySubtree ParseHierarchyPage(byte[] data)
        {
            var subtree = new HierarchySubtree();

            if (data.Length % Entry.EntrySize != 0)
            {
                throw new InvalidDataException($"Invalid hierarchy page length: {data.Length}");
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            int entryCount = data.Length / Entry.EntrySize;

            for (int i = 0; i < entryCount; i++)
            {
                var entry = Entry.Unpack(reader);
                var keyString = entry.Key.ToString();

                if (entry.PointCount < -1)
                {
                    throw new InvalidDataException($"Invalid hierarchy point count at key: {keyString}");
                }
                else if (entry.PointCount == -1)
                {
                    // This is a page
                    subtree.Pages[keyString] = new Page(entry);
                }
                else
                {
                    // This is a node
                    subtree.Nodes[keyString] = new Node(entry);
                }
            }

            return subtree;
        }

        /// <summary>
        /// Gets a node by its key, loading hierarchy pages as needed.
        /// </summary>
        public Node? GetNode(VoxelKey key)
        {
            var keyString = key.ToString();

            // Check if already cached
            if (nodeCache.TryGetValue(keyString, out var cachedNode))
            {
                return cachedNode;
            }

            // Load hierarchy pages starting from root until we find the node
            var rootPage = LoadRootHierarchyPage();
            return FindNodeInHierarchy(key, rootPage);
        }

        /// <summary>
        /// Recursively searches for a node in the hierarchy.
        /// </summary>
        private Node? FindNodeInHierarchy(VoxelKey key, Page currentPage)
        {
            var keyString = key.ToString();

            if (currentPage.Children.TryGetValue(keyString, out var entry))
            {
                if (entry is Node node)
                    return node;
                else if (entry is Page childPage)
                {
                    if (!childPage.Loaded)
                        LoadHierarchyPage(childPage);
                    return FindNodeInHierarchy(key, childPage);
                }
            }

            // Check if we need to load a page that might contain this key
            foreach (var child in currentPage.Children.Values)
            {
                if (child is Page childPage && key.ChildOf(childPage.Key))
                {
                    if (!childPage.Loaded)
                        LoadHierarchyPage(childPage);
                    return FindNodeInHierarchy(key, childPage);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all nodes in the hierarchy.
        /// </summary>
        public List<Node> GetAllNodes()
        {
            var rootPage = LoadRootHierarchyPage();
            var allNodes = new List<Node>();
            CollectAllNodes(rootPage, allNodes);
            return allNodes;
        }

        /// <summary>
        /// Recursively collects all nodes from the hierarchy.
        /// </summary>
        private void CollectAllNodes(Page page, List<Node> nodes)
        {
            if (!page.Loaded)
                LoadHierarchyPage(page);

            foreach (var entry in page.Children.Values)
            {
                if (entry is Node node)
                {
                    nodes.Add(node);
                }
                else if (entry is Page childPage)
                {
                    CollectAllNodes(childPage, nodes);
                }
            }
        }

        /// <summary>
        /// Gets all nodes at a specific depth/layer in the hierarchy and returns their bounding boxes.
        /// </summary>
        /// <param name="layer">The depth/layer level (0 = root, 1 = first level children, etc.)</param>
        /// <returns>A dictionary mapping VoxelKey to Box (bounding box) for each node at the specified layer</returns>
        public Dictionary<VoxelKey, Box> GetBoundingBoxesAtLayer(int layer)
        {
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer), "Layer must be non-negative");

            var allNodes = GetAllNodes();
            var result = new Dictionary<VoxelKey, Box>();

            foreach (var node in allNodes)
            {
                if (node.Key.D == layer)
                {
                    var bounds = node.Key.GetBounds(Config.LasHeader, Config.CopcInfo);
                    result[node.Key] = bounds;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all nodes at a specific depth/layer in the hierarchy.
        /// </summary>
        /// <param name="layer">The depth/layer level (0 = root, 1 = first level children, etc.)</param>
        /// <returns>A list of nodes at the specified layer</returns>
        public List<Node> GetNodesAtLayer(int layer)
        {
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer), "Layer must be non-negative");

            var allNodes = GetAllNodes();
            return allNodes.Where(node => node.Key.D == layer).ToList();
        }

        /// <summary>
        /// Reads compressed point data for a node.
        /// </summary>
        public byte[] GetPointDataCompressed(Node node)
        {
            if (stream == null)
                throw new ObjectDisposedException(nameof(CopcReader));

            stream.Position = node.Offset;
            return stream.ReadExactly(node.ByteSize);
        }

        /// <summary>
        /// Decompresses points from a specific node using lazperf.
        /// This efficiently decompresses only the data for the requested node.
        /// </summary>
        /// <param name="node">The node to decompress</param>
        /// <returns>Array of decompressed points</returns>
        public CopcPoint[] GetPointsFromNode(Node node)
        {
            // Skip nodes with no points or no data
            if (node.PointCount == 0 || node.ByteSize == 0)
            {
                return new CopcPoint[0];
            }

            var compressedData = GetPointDataCompressed(node);
            
            // Additional check: compressed data must have some minimum size
            if (compressedData == null || compressedData.Length < 8)
            {
                Console.WriteLine($"Warning: Node {node.Key} has invalid compressed data size: {compressedData?.Length ?? 0} bytes");
                return new CopcPoint[0];
            }

            return LazDecompressor.DecompressChunk(
                Config.LasHeader.BasePointFormat,
                Config.LasHeader.PointDataRecordLength,
                compressedData,
                node.PointCount,
                Config.LasHeader
            );
        }

        /// <summary>
        /// Decompresses points from multiple nodes.
        /// This efficiently decompresses only the data for the requested nodes.
        /// </summary>
        /// <param name="nodes">The nodes to decompress</param>
        /// <returns>Array of decompressed points from all nodes</returns>
        public CopcPoint[] GetPointsFromNodes(IEnumerable<Node> nodes)
        {
            var allPoints = new List<CopcPoint>();
            int failedNodes = 0;
            foreach (var node in nodes)
            {
                try
                {
                    var points = GetPointsFromNode(node);
                    allPoints.AddRange(points);
                }
                catch (Exception ex)
                {
                    failedNodes++;
                    Console.WriteLine($"Warning: Failed to decompress node {node.Key}: {ex.Message}");
                    // Continue with other nodes
                }
            }
            if (failedNodes > 0)
            {
                Console.WriteLine($"Warning: {failedNodes} node(s) failed to decompress and were skipped");
            }
            return allPoints.ToArray();
        }

        /// <summary>
        /// Gets nodes within a bounding box with optional resolution limit.
        /// </summary>
        public List<Node> GetNodesWithinBox(Box box, double resolution = 0)
        {
            var allNodes = GetAllNodes();
            var result = new List<Node>();

            foreach (var node in allNodes)
            {
                var nodeBounds = node.Key.GetBounds(Config.LasHeader, Config.CopcInfo);
                
                if (nodeBounds.Within(box))
                {
                    // Check resolution if specified
                    if (resolution > 0)
                    {
                        double nodeResolution = node.Key.GetResolution(Config.LasHeader, Config.CopcInfo);
                        if (nodeResolution > resolution)
                            continue; // Node is not detailed enough
                    }

                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets nodes that intersect with a bounding box.
        /// </summary>
        public List<Node> GetNodesIntersectBox(Box box, double resolution = 0)
        {
            var allNodes = GetAllNodes();
            var result = new List<Node>();

            foreach (var node in allNodes)
            {
                var nodeBounds = node.Key.GetBounds(Config.LasHeader, Config.CopcInfo);
                
                if (nodeBounds.Intersects(box))
                {
                    // Check resolution if specified
                    if (resolution > 0)
                    {
                        double nodeResolution = node.Key.GetResolution(Config.LasHeader, Config.CopcInfo);
                        if (nodeResolution > resolution)
                            continue;
                    }

                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum.
        /// This is useful for querying points visible from a camera perspective.
        /// </summary>
        /// <param name="frustum">The view frustum to test against</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the frustum</returns>
        public List<Node> GetNodesIntersectFrustum(Frustum frustum, double resolution = 0)
        {
            var allNodes = GetAllNodes();
            var result = new List<Node>();

            foreach (var node in allNodes)
            {
                var nodeBounds = node.Key.GetBounds(Config.LasHeader, Config.CopcInfo);
                
                if (frustum.IntersectsBox(nodeBounds))
                {
                    // Check resolution if specified
                    if (resolution > 0)
                    {
                        double nodeResolution = node.Key.GetResolution(Config.LasHeader, Config.CopcInfo);
                        if (nodeResolution > resolution)
                            continue;
                    }

                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum, constructed from a view-projection matrix.
        /// This is a convenience method that creates the frustum from the matrix and queries nodes.
        /// </summary>
        /// <param name="viewProjectionMatrix">The combined view-projection matrix as a 16-element array in row-major order</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the frustum</returns>
        public List<Node> GetNodesIntersectFrustum(double[] viewProjectionMatrix, double resolution = 0)
        {
            var frustum = Frustum.FromViewProjectionMatrix(viewProjectionMatrix);
            return GetNodesIntersectFrustum(frustum, resolution);
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum, constructed from a view-projection matrix.
        /// This overload accepts float arrays for compatibility with graphics APIs.
        /// </summary>
        /// <param name="viewProjectionMatrix">The combined view-projection matrix as a 16-element float array in row-major order</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the frustum</returns>
        public List<Node> GetNodesIntersectFrustum(float[] viewProjectionMatrix, double resolution = 0)
        {
            var frustum = Frustum.FromViewProjectionMatrix(viewProjectionMatrix);
            return GetNodesIntersectFrustum(frustum, resolution);
        }

        /// <summary>
        /// Gets nodes within a spherical radius from a center point.
        /// This performs an omnidirectional spatial query to retrieve all nodes that intersect
        /// with a sphere defined by a center position and radius.
        /// </summary>
        /// <param name="sphere">The sphere to test against</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the sphere</returns>
        public List<Node> GetNodesWithinRadius(Sphere sphere, double resolution = 0)
        {
            var allNodes = GetAllNodes();
            var result = new List<Node>();

            foreach (var node in allNodes)
            {
                var nodeBounds = node.Key.GetBounds(Config.LasHeader, Config.CopcInfo);
                
                if (sphere.IntersectsBox(nodeBounds))
                {
                    // Check resolution if specified
                    if (resolution > 0)
                    {
                        double nodeResolution = node.Key.GetResolution(Config.LasHeader, Config.CopcInfo);
                        if (nodeResolution > resolution)
                            continue;
                    }

                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets nodes within a spherical radius from a center point.
        /// This is a convenience overload that constructs the sphere from center coordinates and radius.
        /// </summary>
        /// <param name="centerX">X coordinate of the sphere center</param>
        /// <param name="centerY">Y coordinate of the sphere center</param>
        /// <param name="centerZ">Z coordinate of the sphere center</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the sphere</returns>
        public List<Node> GetNodesWithinRadius(double centerX, double centerY, double centerZ, double radius, double resolution = 0)
        {
            var sphere = new Sphere(centerX, centerY, centerZ, radius);
            return GetNodesWithinRadius(sphere, resolution);
        }

        /// <summary>
        /// Gets nodes within a spherical radius from a center point.
        /// This is a convenience overload that constructs the sphere from a Vector3 center and radius.
        /// </summary>
        /// <param name="center">The center point of the sphere</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="resolution">Optional minimum resolution (point spacing). If > 0, only nodes with resolution less than or equal to this value are returned.</param>
        /// <returns>List of nodes that intersect the sphere</returns>
        public List<Node> GetNodesWithinRadius(Vector3 center, double radius, double resolution = 0)
        {
            var sphere = new Sphere(center, radius);
            return GetNodesWithinRadius(sphere, resolution);
        }

        /// <summary>
        /// Gets the depth level that provides at least the requested resolution.
        /// </summary>
        public int GetDepthAtResolution(double resolution)
        {
            int depth = 0;
            while (VoxelKey.GetResolutionAtDepth(depth, Config.LasHeader, Config.CopcInfo) > resolution)
            {
                depth++;
                if (depth > 32) // Safety limit
                    break;
            }
            return depth;
        }

        /// <summary>
        /// Gets all nodes at a specific resolution.
        /// </summary>
        public List<Node> GetNodesAtResolution(double resolution)
        {
            int depth = GetDepthAtResolution(resolution);
            var allNodes = GetAllNodes();
            
            return allNodes.Where(n => n.Key.D == depth).ToList();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (!leaveOpen && stream != null)
                {
                    stream.Dispose();
                }

                // LasZipDll doesn't implement IDisposable, no need to dispose

                disposed = true;
            }
        }
    }
}

