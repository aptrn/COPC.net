using System;
using System.Collections.Generic;
using Copc.Hierarchy;
using Copc.IO;

namespace Copc.Cache
{
    /// <summary>
    /// Represents a single cached node entry containing decompressed point data.
    /// </summary>
    internal class CachedNodeData
    {
        public VoxelKey Key { get; set; }
        public CopcPoint[] Points { get; set; }
        public long MemorySize { get; set; }
        public DateTime LastAccessTime { get; set; }
        public long AccessCount { get; set; }

        public CachedNodeData(VoxelKey key, CopcPoint[] points, long memorySize)
        {
            Key = key;
            Points = points;
            MemorySize = memorySize;
            LastAccessTime = DateTime.UtcNow;
            AccessCount = 0;
        }

        public void UpdateAccessTime()
        {
            LastAccessTime = DateTime.UtcNow;
            AccessCount++;
        }
    }

    /// <summary>
    /// Smart cache for COPC point cloud data with automatic memory management.
    /// Uses LRU (Least Recently Used) eviction policy when the cache is full.
    /// </summary>
    public class PointCache
    {
        // Configuration
        private readonly long maxMemoryBytes;
        private readonly int estimatedBytesPerPoint;

        // Cache storage
        private readonly Dictionary<VoxelKey, LinkedListNode<CachedNodeData>> cache;
        private readonly LinkedList<CachedNodeData> lruList; // Head = most recent, Tail = least recent
        
        // Statistics
        private long currentMemoryBytes;
        private long totalHits;
        private long totalMisses;
        private long totalEvictions;

		// Cached Stride-format aggregation of all cached points
		private StrideCacheData? cachedStrideData;
		private bool strideCacheDirty;

        /// <summary>
        /// Gets the current memory usage in bytes.
        /// </summary>
        public long CurrentMemoryBytes => currentMemoryBytes;

        /// <summary>
        /// Gets the maximum memory capacity in bytes.
        /// </summary>
        public long MaxMemoryBytes => maxMemoryBytes;

        /// <summary>
        /// Gets the current memory usage as a percentage (0-100).
        /// </summary>
        public double MemoryUsagePercent => (double)currentMemoryBytes / maxMemoryBytes * 100.0;

        /// <summary>
        /// Gets the number of cached nodes.
        /// </summary>
        public int Count => cache.Count;

        /// <summary>
        /// Gets the total number of cache hits.
        /// </summary>
        public long TotalHits => totalHits;

        /// <summary>
        /// Gets the total number of cache misses.
        /// </summary>
        public long TotalMisses => totalMisses;

        /// <summary>
        /// Gets the total number of evictions.
        /// </summary>
        public long TotalEvictions => totalEvictions;

        /// <summary>
        /// Gets the cache hit rate as a percentage (0-100).
        /// </summary>
        public double HitRate
        {
            get
            {
                long total = totalHits + totalMisses;
                return total > 0 ? (double)totalHits / total * 100.0 : 0.0;
            }
        }

        /// <summary>
        /// Creates a new point cache with the specified maximum memory size.
        /// </summary>
        /// <param name="maxMemoryBytes">Maximum memory to use for caching (in bytes)</param>
        /// <param name="estimatedBytesPerPoint">Estimated memory per point (default: 100 bytes)</param>
        public PointCache(long maxMemoryBytes, int estimatedBytesPerPoint = 100)
        {
            if (maxMemoryBytes <= 0)
                throw new ArgumentException("Max memory must be positive", nameof(maxMemoryBytes));
            if (estimatedBytesPerPoint <= 0)
                throw new ArgumentException("Bytes per point must be positive", nameof(estimatedBytesPerPoint));

            this.maxMemoryBytes = maxMemoryBytes;
            this.estimatedBytesPerPoint = estimatedBytesPerPoint;
            
            cache = new Dictionary<VoxelKey, LinkedListNode<CachedNodeData>>();
            lruList = new LinkedList<CachedNodeData>();
            currentMemoryBytes = 0;
            totalHits = 0;
            totalMisses = 0;
            totalEvictions = 0;

			cachedStrideData = null;
			strideCacheDirty = true;
        }

        /// <summary>
        /// Creates a new point cache with the specified maximum memory size in megabytes.
        /// </summary>
        /// <param name="maxMemoryMB">Maximum memory to use for caching (in megabytes)</param>
        /// <param name="estimatedBytesPerPoint">Estimated memory per point (default: 100 bytes)</param>
        public static PointCache CreateWithMB(int maxMemoryMB, int estimatedBytesPerPoint = 100)
        {
            return new PointCache((long)maxMemoryMB * 1024 * 1024, estimatedBytesPerPoint);
        }

        /// <summary>
        /// Attempts to get cached point data for a node.
        /// </summary>
        /// <param name="key">The voxel key to look up</param>
        /// <param name="points">Output parameter containing the cached points if found</param>
        /// <returns>True if the data was in cache, false otherwise</returns>
        public bool TryGetPoints(VoxelKey key, out CopcPoint[]? points)
        {
            if (cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                lruList.Remove(node);
                lruList.AddFirst(node);
                node.Value.UpdateAccessTime();
                
                points = node.Value.Points;
                totalHits++;
                return true;
            }

            points = null;
            totalMisses++;
            return false;
        }

        /// <summary>
        /// Adds or updates point data in the cache.
        /// </summary>
        /// <param name="key">The voxel key</param>
        /// <param name="points">The point data to cache</param>
        public void Put(VoxelKey key, CopcPoint[] points)
        {
            // Calculate memory size for this entry
            long memorySize = EstimateMemorySize(points);

            // If this single entry is too large for the cache, don't cache it
            if (memorySize > maxMemoryBytes)
            {
                return;
            }

            // Remove existing entry if present
            if (cache.TryGetValue(key, out var existingNode))
            {
                currentMemoryBytes -= existingNode.Value.MemorySize;
                lruList.Remove(existingNode);
                cache.Remove(key);
            }

            // Evict entries until we have enough space
            while (currentMemoryBytes + memorySize > maxMemoryBytes && lruList.Count > 0)
            {
                EvictLeastRecentlyUsed();
            }

            // Add new entry
            var cachedData = new CachedNodeData(key, points, memorySize);
            var newNode = lruList.AddFirst(cachedData);
            cache[key] = newNode;
            currentMemoryBytes += memorySize;

			// Mark stride cache dirty since content changed
			strideCacheDirty = true;
        }

        /// <summary>
        /// Gets points for a node, using cache if available or loading from reader if not.
        /// This is a convenience method that combines cache lookup and loading.
        /// </summary>
        /// <param name="node">The node to get points for</param>
        /// <param name="reader">The COPC reader to use if cache miss occurs</param>
        /// <returns>Array of points (from cache or freshly loaded)</returns>
        public CopcPoint[] GetOrLoadPoints(Node node, CopcReader reader)
        {
            // Try cache first
            if (TryGetPoints(node.Key, out var cachedPoints) && cachedPoints != null)
            {
                return cachedPoints;
            }

            // Cache miss - load from reader
            var points = reader.GetPointsFromNode(node);
            
            // Add to cache for future use
            Put(node.Key, points);
            
            return points;
        }

        /// <summary>
        /// Gets points for multiple nodes, using cache when possible and loading only uncached nodes.
        /// This is more efficient than loading nodes one at a time.
        /// </summary>
        /// <param name="nodes">The nodes to get points for</param>
        /// <param name="reader">The COPC reader to use for cache misses</param>
        /// <returns>Array of all points from the nodes</returns>
        public CopcPoint[] GetOrLoadPointsFromNodes(IEnumerable<Node> nodes, CopcReader reader)
        {
            var allPoints = new List<CopcPoint>();
            var nodesToLoad = new List<Node>();

            // First pass: check cache and collect nodes that need loading
            foreach (var node in nodes)
            {
                if (TryGetPoints(node.Key, out var cachedPoints) && cachedPoints != null)
                {
                    allPoints.AddRange(cachedPoints);
                }
                else
                {
                    nodesToLoad.Add(node);
                }
            }

            // Second pass: load uncached nodes
            foreach (var node in nodesToLoad)
            {
                try
                {
                    var points = reader.GetPointsFromNode(node);
                    Put(node.Key, points);
                    allPoints.AddRange(points);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load node {node.Key}: {ex.Message}");
                    // Continue with other nodes
                }
            }

            return allPoints.ToArray();
        }

        /// <summary>
        /// Checks if a node's data is in the cache.
        /// </summary>
        /// <param name="key">The voxel key to check</param>
        /// <returns>True if the node is cached, false otherwise</returns>
        public bool Contains(VoxelKey key)
        {
            return cache.ContainsKey(key);
        }

        /// <summary>
        /// Removes a specific entry from the cache.
        /// </summary>
        /// <param name="key">The voxel key to remove</param>
        /// <returns>True if the entry was removed, false if it wasn't in cache</returns>
        public bool Remove(VoxelKey key)
        {
            if (cache.TryGetValue(key, out var node))
            {
                currentMemoryBytes -= node.Value.MemorySize;
                lruList.Remove(node);
                cache.Remove(key);
				// Content changed
				strideCacheDirty = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            cache.Clear();
            lruList.Clear();
            currentMemoryBytes = 0;
			cachedStrideData = null;
			strideCacheDirty = true;
        }

        /// <summary>
        /// Gets statistics about cache performance.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                MaxMemoryBytes = maxMemoryBytes,
                CurrentMemoryBytes = currentMemoryBytes,
                MemoryUsagePercent = MemoryUsagePercent,
                CachedNodeCount = Count,
                TotalHits = totalHits,
                TotalMisses = totalMisses,
                TotalEvictions = totalEvictions,
                HitRate = HitRate
            };
        }

        /// <summary>
        /// Gets information about all cached nodes.
        /// Useful for debugging and monitoring.
        /// </summary>
        public List<CachedNodeInfo> GetCachedNodes()
        {
            var result = new List<CachedNodeInfo>();
            foreach (var entry in lruList)
            {
                result.Add(new CachedNodeInfo
                {
                    Key = entry.Key,
                    PointCount = entry.Points.Length,
                    MemorySize = entry.MemorySize,
                    LastAccessTime = entry.LastAccessTime,
                    AccessCount = entry.AccessCount
                });
            }
            return result;
        }

        /// <summary>
        /// Evicts the least recently used entry from the cache.
        /// </summary>
        private void EvictLeastRecentlyUsed()
        {
            if (lruList.Last == null)
                return;

            var lruNode = lruList.Last;
            var lruData = lruNode.Value;

            cache.Remove(lruData.Key);
            lruList.RemoveLast();
            currentMemoryBytes -= lruData.MemorySize;
            totalEvictions++;
			// Content changed
			strideCacheDirty = true;
        }

		/// <summary>
		/// Builds or returns cached Stride-format separated arrays for all cached points.
		/// Rebuilds only when cache content changes (on Put/Remove/Clear/Evict).
		/// </summary>
		public StrideCacheData GetOrBuildStrideCacheDataSeparated(List<ExtraDimension>? extraDimensions = null)
		{
			if (cachedStrideData != null && !strideCacheDirty)
			{
				return cachedStrideData;
			}

			// Convert all cached points to Stride and generate separated arrays
			var cachedNodes = GetCachedNodes();
			var allStridePoints = new List<StridePoint>();
			foreach (var nodeInfo in cachedNodes)
			{
				if (TryGetPoints(nodeInfo.Key, out var copcPoints) && copcPoints != null)
				{
					var stridePoints = StrideCacheExtensions.ConvertToStridePoints(copcPoints, extraDimensions);
					allStridePoints.AddRange(stridePoints);
				}
			}

			var data = new StrideCacheData { Points = allStridePoints.ToArray() };
			data.GenerateSeparateArrays();

			cachedStrideData = data;
			strideCacheDirty = false;
			return data;
		}

        /// <summary>
        /// Estimates the memory size of a point array.
        /// </summary>
        private long EstimateMemorySize(CopcPoint[] points)
        {
            // Estimated memory per CopcPoint object:
            // - Object header: ~16 bytes
            // - 3 doubles (X,Y,Z): 24 bytes
            // - Various ushort, byte, bool fields: ~20 bytes
            // - Nullable fields: ~20 bytes
            // - Array overhead: ~24 bytes
            // - Reference: 8 bytes
            // Total: ~100-120 bytes per point (configurable)
            
            long arrayOverhead = 24; // Array object overhead
            long pointMemory = points.Length * estimatedBytesPerPoint;
            return arrayOverhead + pointMemory;
        }
    }

    /// <summary>
    /// Statistics about cache performance.
    /// </summary>
    public class CacheStatistics
    {
        public long MaxMemoryBytes { get; set; }
        public long CurrentMemoryBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public int CachedNodeCount { get; set; }
        public long TotalHits { get; set; }
        public long TotalMisses { get; set; }
        public long TotalEvictions { get; set; }
        public double HitRate { get; set; }

        public override string ToString()
        {
            return $"Cache Stats: {CachedNodeCount} nodes, {CurrentMemoryBytes / 1024.0 / 1024.0:F2} MB / {MaxMemoryBytes / 1024.0 / 1024.0:F2} MB ({MemoryUsagePercent:F1}%), " +
                   $"Hit Rate: {HitRate:F1}% ({TotalHits} hits, {TotalMisses} misses), {TotalEvictions} evictions";
        }
    }

    /// <summary>
    /// Information about a cached node entry.
    /// </summary>
    public class CachedNodeInfo
    {
        public VoxelKey Key { get; set; }
        public int PointCount { get; set; }
        public long MemorySize { get; set; }
        public DateTime LastAccessTime { get; set; }
        public long AccessCount { get; set; }

        public override string ToString()
        {
            return $"Node {Key}: {PointCount} points, {MemorySize / 1024.0:F2} KB, accessed {AccessCount} times, last: {LastAccessTime:s}";
        }
    }
}

