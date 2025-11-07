using System;
using System.Collections.Generic;
using Copc.Geometry;
using Copc.Hierarchy;
using Copc.IO;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideVector4 = Stride.Core.Mathematics.Vector4;

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

        // Stride-specific methods for easy data export

        /// <summary>
        /// Gets all currently cached data in Stride engine format.
        /// Returns points with Position and Color as Vector4 (W=1 for both).
        /// </summary>
        /// <param name="colorMode">Color mode for conversion (RGB, Intensity, Classification, or Elevation)</param>
        /// <returns>All cached points in Stride format</returns>
        public StrideCacheData GetCacheData(StrideColorMode colorMode = StrideColorMode.RGB)
        {
            return cache.GetCacheData(colorMode);
        }

        /// <summary>
        /// Gets all currently cached data in Stride format with separate position and color arrays.
        /// Useful for direct GPU buffer upload.
        /// </summary>
        /// <param name="colorMode">Color mode for conversion (RGB, Intensity, Classification, or Elevation)</param>
        /// <returns>All cached points with separate Position[] and Color[] arrays</returns>
        public StrideCacheData GetCacheDataSeparated(StrideColorMode colorMode = StrideColorMode.RGB)
        {
            return cache.GetCacheDataSeparated(colorMode);
        }

        /// <summary>
        /// Gets points from nodes and immediately converts to Stride format.
        /// Does not cache the points.
        /// </summary>
        /// <param name="nodes">Nodes to get points from</param>
        /// <param name="colorMode">Color mode for conversion</param>
        /// <returns>Points in Stride format</returns>
        public StridePoint[] GetStridePointsFromNodes(IEnumerable<Node> nodes, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsFromNodes(nodes);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
        }

        /// <summary>
        /// Gets points in a box and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInBox(Box box, double resolution = 0, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsInBox(box, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
        }

        /// <summary>
        /// Gets points in a frustum and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInFrustum(Frustum frustum, double resolution = 0, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsInFrustum(frustum, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
        }

        /// <summary>
        /// Gets points in a frustum (from matrix) and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInFrustum(double[] viewProjectionMatrix, double resolution = 0, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsInFrustum(viewProjectionMatrix, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
        }

        /// <summary>
        /// Gets points in a radius and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInRadius(Sphere sphere, double resolution = 0, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsInRadius(sphere, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
        }

        /// <summary>
        /// Gets points in a radius and converts to Stride format.
        /// </summary>
        public StridePoint[] GetStridePointsInRadius(double centerX, double centerY, double centerZ, double radius, double resolution = 0, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var copcPoints = GetPointsInRadius(centerX, centerY, centerZ, radius, resolution);
            return StrideCacheExtensions.ConvertToStridePoints(copcPoints, colorMode);
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

