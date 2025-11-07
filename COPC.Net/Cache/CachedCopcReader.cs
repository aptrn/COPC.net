using System;
using System.Collections.Generic;
using Copc.Geometry;
using Copc.Hierarchy;
using Copc.IO;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideVector4 = Stride.Core.Mathematics.Vector4;
using System.Linq;
using System.Threading.Tasks;

namespace Copc.Cache
{
    /// <summary>
    /// A wrapper around CopcReader that provides automatic point data caching.
    /// This is a convenience class that makes it easy to use the point cache.
    /// </summary>
    public class CachedCopcReader : IDisposable
    {
        private readonly CopcReader reader;
        private readonly PointCache cache;
        private readonly bool ownReader;
        private bool disposed;

        /// <summary>
        /// Gets the underlying COPC reader.
        /// </summary>
        public CopcReader Reader => reader;

        /// <summary>
        /// Gets the point cache used by this reader.
        /// </summary>
        public PointCache Cache => cache;

        /// <summary>
        /// Gets the COPC configuration from the underlying reader.
        /// </summary>
        public CopcConfig Config => reader.Config;

        /// <summary>
        /// Creates a new cached COPC reader.
        /// </summary>
        /// <param name="reader">The underlying COPC reader</param>
        /// <param name="cache">The point cache to use</param>
        /// <param name="ownReader">If true, the reader will be disposed when this object is disposed</param>
        public CachedCopcReader(CopcReader reader, PointCache cache, bool ownReader = false)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.ownReader = ownReader;
        }

        /// <summary>
        /// Opens a COPC file with automatic caching.
        /// </summary>
        /// <param name="filePath">Path to the COPC file</param>
        /// <param name="cacheSizeMB">Cache size in megabytes (default: 512 MB)</param>
        /// <param name="estimatedBytesPerPoint">Estimated memory per point (default: 100 bytes)</param>
        /// <returns>A new cached COPC reader</returns>
        public static CachedCopcReader Open(string filePath, int cacheSizeMB = 512, int estimatedBytesPerPoint = 100)
        {
            var reader = CopcReader.Open(filePath);
            var cache = PointCache.CreateWithMB(cacheSizeMB, estimatedBytesPerPoint);
            return new CachedCopcReader(reader, cache, ownReader: true);
        }

        /// <summary>
        /// Gets points from a single node, using cache if available.
        /// </summary>
        /// <param name="node">The node to get points from</param>
        /// <returns>Array of points</returns>
        public CopcPoint[] GetPointsFromNode(Node node)
        {
            return cache.GetOrLoadPoints(node, reader);
        }

        /// <summary>
        /// Gets points from multiple nodes, using cache when possible.
        /// </summary>
        /// <param name="nodes">The nodes to get points from</param>
        /// <returns>Array of all points from the nodes</returns>
        public CopcPoint[] GetPointsFromNodes(IEnumerable<Node> nodes)
        {
            return cache.GetOrLoadPointsFromNodes(nodes, reader);
        }

        /// <summary>
        /// Updates the cache with the provided nodes by ensuring their point data
        /// is loaded into the cache. This does not return points, avoiding extra
        /// allocations and concatenations for performance.
        /// </summary>
        /// <param name="nodes">Nodes whose data should be cached</param>
        public void Update(IEnumerable<Node> nodes)
        {
            Update(nodes, 1);
        }

        /// <summary>
        /// Updates the cache with provided nodes. Decompression is currently single-threaded
        /// due to thread-safety concerns in the decompressor.
        /// </summary>
        /// <param name="nodes">Nodes to warm into cache</param>
        /// <param name="degreeOfParallelism">Ignored for now; decompression runs single-threaded</param>
        public void Update(IEnumerable<Node> nodes, int degreeOfParallelism)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            // Determine which nodes actually need loading
            var nodesToLoad = new List<Node>();
            foreach (var node in nodes)
            {
                if (!cache.Contains(node.Key))
                {
                    nodesToLoad.Add(node);
                }
            }

            if (nodesToLoad.Count == 0)
                return;

            // Step 1: Read compressed chunks sequentially (stream is not thread-safe)
            var compressed = new List<(Node node, byte[] data)>(nodesToLoad.Count);
            foreach (var node in nodesToLoad)
            {
                try
                {
                    if (node.PointCount == 0 || node.ByteSize == 0)
                        continue;

                    var data = reader.GetPointDataCompressed(node);
                    if (data != null && data.Length > 0)
                    {
                        compressed.Add((node, data));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to read compressed data for node {node.Key}: {ex.Message}");
                }
            }

            if (compressed.Count == 0)
                return;

            // Step 2: Decompress sequentially (thread-safety)
            var header = reader.Config.LasHeader;
            var extra = reader.Config.ExtraDimensions;
            var results = new CopcPoint[compressed.Count][];

            for (int i = 0; i < compressed.Count; i++)
            {
                try
                {
                    var item = compressed[i];
                    var points = LazDecompressor.DecompressChunk(
                        header.BasePointFormat,
                        header.PointDataRecordLength,
                        item.data,
                        item.node.PointCount,
                        header,
                        extra
                    );
                    results[i] = points;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to decompress node {compressed[i].node.Key}: {ex.Message}");
                    results[i] = Array.Empty<CopcPoint>();
                }
            }

            // Step 3: Put into cache sequentially to keep cache structure consistent
            for (int i = 0; i < compressed.Count; i++)
            {
                var node = compressed[i].node;
                var points = results[i] ?? Array.Empty<CopcPoint>();
                if (points.Length > 0)
                {
                    cache.Put(node.Key, points);
                }
            }
        }

        // Pass-through methods to underlying reader (no caching needed for hierarchy queries)

        /// <summary>
        /// Loads the root hierarchy page.
        /// </summary>
        public Page LoadRootHierarchyPage()
        {
            return reader.LoadRootHierarchyPage();
        }

        /// <summary>
        /// Gets a node by its key.
        /// </summary>
        public Node? GetNode(VoxelKey key)
        {
            return reader.GetNode(key);
        }

        /// <summary>
        /// Gets all nodes in the hierarchy.
        /// </summary>
        public List<Node> GetAllNodes()
        {
            return reader.GetAllNodes();
        }

        /// <summary>
        /// Gets all nodes at a specific depth/layer.
        /// </summary>
        public List<Node> GetNodesAtLayer(int layer)
        {
            return reader.GetNodesAtLayer(layer);
        }

        /// <summary>
        /// Gets bounding boxes for all nodes at a specific layer.
        /// </summary>
        public Dictionary<VoxelKey, Box> GetBoundingBoxesAtLayer(int layer)
        {
            return reader.GetBoundingBoxesAtLayer(layer);
        }

        /// <summary>
        /// Gets nodes within a bounding box.
        /// </summary>
        public List<Node> GetNodesWithinBox(Box box, double resolution = 0)
        {
            return reader.GetNodesWithinBox(box, resolution);
        }

        /// <summary>
        /// Gets nodes that intersect with a bounding box.
        /// </summary>
        public List<Node> GetNodesIntersectBox(Box box, double resolution = 0)
        {
            return reader.GetNodesIntersectBox(box, resolution);
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum.
        /// </summary>
        public List<Node> GetNodesIntersectFrustum(Frustum frustum, double resolution = 0)
        {
            return reader.GetNodesIntersectFrustum(frustum, resolution);
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum (from matrix).
        /// </summary>
        public List<Node> GetNodesIntersectFrustum(double[] viewProjectionMatrix, double resolution = 0)
        {
            return reader.GetNodesIntersectFrustum(viewProjectionMatrix, resolution);
        }

        /// <summary>
        /// Gets nodes that intersect with a view frustum (from float matrix).
        /// </summary>
        public List<Node> GetNodesIntersectFrustum(float[] viewProjectionMatrix, double resolution = 0)
        {
            return reader.GetNodesIntersectFrustum(viewProjectionMatrix, resolution);
        }

        /// <summary>
        /// Gets nodes within a spherical radius.
        /// </summary>
        public List<Node> GetNodesWithinRadius(Sphere sphere, double resolution = 0)
        {
            return reader.GetNodesWithinRadius(sphere, resolution);
        }

        /// <summary>
        /// Gets nodes within a spherical radius (from coordinates).
        /// </summary>
        public List<Node> GetNodesWithinRadius(double centerX, double centerY, double centerZ, double radius, double resolution = 0)
        {
            return reader.GetNodesWithinRadius(centerX, centerY, centerZ, radius, resolution);
        }

        /// <summary>
        /// Gets nodes within a spherical radius (from Vector3).
        /// </summary>
        public List<Node> GetNodesWithinRadius(Vector3 center, double radius, double resolution = 0)
        {
            return reader.GetNodesWithinRadius(center, radius, resolution);
        }

        /// <summary>
        /// Gets the depth level for a given resolution.
        /// </summary>
        public int GetDepthAtResolution(double resolution)
        {
            return reader.GetDepthAtResolution(resolution);
        }

        /// <summary>
        /// Gets all nodes at a specific resolution.
        /// </summary>
        public List<Node> GetNodesAtResolution(double resolution)
        {
            return reader.GetNodesAtResolution(resolution);
        }

        // Cached query methods - these combine node queries with point loading

        /// <summary>
        /// Gets all points within a bounding box, using cache when possible.
        /// </summary>
        /// <param name="box">The bounding box to query</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the box</returns>
        public CopcPoint[] GetPointsInBox(Box box, double resolution = 0)
        {
            var nodes = reader.GetNodesIntersectBox(box, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a view frustum, using cache when possible.
        /// </summary>
        /// <param name="frustum">The frustum to query</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the frustum</returns>
        public CopcPoint[] GetPointsInFrustum(Frustum frustum, double resolution = 0)
        {
            var nodes = reader.GetNodesIntersectFrustum(frustum, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a view frustum (from matrix), using cache when possible.
        /// </summary>
        /// <param name="viewProjectionMatrix">The view-projection matrix</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the frustum</returns>
        public CopcPoint[] GetPointsInFrustum(double[] viewProjectionMatrix, double resolution = 0)
        {
            var nodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a view frustum (from float matrix), using cache when possible.
        /// </summary>
        /// <param name="viewProjectionMatrix">The view-projection matrix</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the frustum</returns>
        public CopcPoint[] GetPointsInFrustum(float[] viewProjectionMatrix, double resolution = 0)
        {
            var nodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a spherical radius, using cache when possible.
        /// </summary>
        /// <param name="sphere">The sphere to query</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the sphere</returns>
        public CopcPoint[] GetPointsInRadius(Sphere sphere, double resolution = 0)
        {
            var nodes = reader.GetNodesWithinRadius(sphere, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a spherical radius (from coordinates), using cache when possible.
        /// </summary>
        /// <param name="centerX">X coordinate of sphere center</param>
        /// <param name="centerY">Y coordinate of sphere center</param>
        /// <param name="centerZ">Z coordinate of sphere center</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the sphere</returns>
        public CopcPoint[] GetPointsInRadius(double centerX, double centerY, double centerZ, double radius, double resolution = 0)
        {
            var nodes = reader.GetNodesWithinRadius(centerX, centerY, centerZ, radius, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets all points within a spherical radius (from Vector3), using cache when possible.
        /// </summary>
        /// <param name="center">Center point of the sphere</param>
        /// <param name="radius">Radius of the sphere</param>
        /// <param name="resolution">Optional minimum resolution filter</param>
        /// <returns>Array of points within the sphere</returns>
        public CopcPoint[] GetPointsInRadius(Vector3 center, double radius, double resolution = 0)
        {
            var nodes = reader.GetNodesWithinRadius(center, radius, resolution);
            return GetPointsFromNodes(nodes);
        }

        /// <summary>
        /// Gets metadata about all available point attributes in the point cloud.
        /// This returns information about what attributes are available (names, types, ranges)
        /// WITHOUT loading any actual point data. Useful for setting up rendering pipeline.
        /// </summary>
        /// <returns>Metadata describing all available attributes</returns>
        public PointCloudAttributeMetadata GetAttributeMetadata()
        {
            return reader.GetAttributeMetadata();
        }

        // Stride-specific methods for easy data export

        /// <summary>
        /// Gets all currently cached data in Stride engine format.
        /// Returns points with Position and Color as Vector4 (W=1 for both),
        /// and all other attributes (including extra dimensions) as separate float32 values.
        /// </summary>
        /// <returns>All cached points in Stride format with extra dimensions extracted</returns>
        public StrideCacheData GetCacheData()
        {
            return cache.GetCacheData(reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets all currently cached data in Stride format with separate arrays for each attribute.
        /// Useful for direct GPU buffer upload as vertex attributes.
        /// Extra dimensions are included in ExtraDimensionArrays dictionary.
        /// </summary>
        /// <returns>All cached points with separate arrays (Positions, Colors, Intensities, ExtraDimensionArrays, etc.)</returns>
        public StrideCacheData GetCacheDataSeparated()
        {
            return cache.GetOrBuildStrideCacheDataSeparated(reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets separated Stride-format data only for the specified nodes, using points from cache.
        /// Does not trigger loading; nodes must be pre-warmed via Update().
        /// </summary>
        /// <param name="nodes">Nodes whose cached points should be converted</param>
        public StrideCacheData GetCacheDataSeparatedFromNodes(IEnumerable<Node> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));

            var allCopcPoints = new List<CopcPoint>();
            foreach (var node in nodes)
            {
                if (cache.TryGetPoints(node.Key, out var pts) && pts != null && pts.Length > 0)
                {
                    allCopcPoints.AddRange(pts);
                }
            }

            var stridePoints = StrideCacheExtensions.ConvertToStridePoints(allCopcPoints.ToArray(), reader.Config.ExtraDimensions);
            var data = new StrideCacheData { Points = stridePoints };
            data.GenerateSeparateArrays();
            return data;
        }

        /// <summary>
        /// Gets points from nodes and immediately converts to Stride format.
        /// Does not cache the points.
        /// </summary>
        /// <param name="nodes">Nodes to get points from</param>
        /// <returns>Points in Stride format</returns>
        public StridePoint[] GetStridePointsFromNodes(IEnumerable<Node> nodes)
        {
            var copcPoints = GetPointsFromNodes(nodes);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets points in a box and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInBox(Box box, double resolution = 0)
        {
            var copcPoints = GetPointsInBox(box, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets points in a frustum and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInFrustum(Frustum frustum, double resolution = 0)
        {
            var copcPoints = GetPointsInFrustum(frustum, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets points in a frustum (from matrix) and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInFrustum(double[] viewProjectionMatrix, double resolution = 0)
        {
            var copcPoints = GetPointsInFrustum(viewProjectionMatrix, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets points in a radius and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInRadius(Sphere sphere, double resolution = 0)
        {
            var copcPoints = GetPointsInRadius(sphere, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        /// <summary>
        /// Gets points in a radius and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInRadius(double centerX, double centerY, double centerZ, double radius, double resolution = 0)
        {
            var copcPoints = GetPointsInRadius(centerX, centerY, centerZ, radius, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, reader.Config.ExtraDimensions);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (ownReader)
                {
                    reader?.Dispose();
                }
                disposed = true;
            }
        }
    }
}

