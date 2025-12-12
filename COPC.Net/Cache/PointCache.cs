using System;
using System.Collections.Generic;
using Copc.Hierarchy;
using Copc.IO;
using System.Threading.Tasks;
using StrideVector4 = Stride.Core.Mathematics.Vector4;

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
		public SeparatedNodeData? Separated { get; set; }
		public long SeparatedMemorySize { get; set; }

        public CachedNodeData(VoxelKey key, CopcPoint[] points, long memorySize)
        {
            Key = key;
            Points = points;
            MemorySize = memorySize;
            LastAccessTime = DateTime.UtcNow;
            AccessCount = 0;
			Separated = null;
			SeparatedMemorySize = 0;
        }

        public void UpdateAccessTime()
        {
            LastAccessTime = DateTime.UtcNow;
            AccessCount++;
        }
    }

	/// <summary>
	/// Per-node separated arrays ready for GPU upload.
	/// </summary>
	public class SeparatedNodeData : IDisposable
	{
		public StrideVector4[] Positions { get; set; } = Array.Empty<StrideVector4>();
		public StrideVector4[] Colors { get; set; } = Array.Empty<StrideVector4>();
		public StrideVector4[] Normals { get; set; } = Array.Empty<StrideVector4>();
		public int[]? Depth { get; set; }
		public Dictionary<string, float[]>? ExtraDimensionArrays { get; set; }
		public int Count => Positions?.Length ?? 0;
		internal long MemoryPressure { get; set; }
		private bool disposed;
		private bool memoryPressureAdded;

		/// <summary>
		/// Adds GC memory pressure for the allocated arrays. Call this once after construction.
		/// NOTE: For cached objects, DON'T add pressure - cache already tracks memory!
		/// Only add pressure for temporary/returned objects.
		/// </summary>
		internal void AddMemoryPressure()
		{
			// DISABLED for vvvv gamma stability
			// Cache already tracks memory via maxMemoryBytes
			// Adding GC pressure on top causes double accounting and crashes
			return;
			
			/*
			if (memoryPressureAdded || disposed)
				return;

			long pressure = 0;
			if (Positions != null && Positions.Length > 0)
				pressure += (long)Positions.Length * sizeof(float) * 4;
			if (Colors != null && Colors.Length > 0)
				pressure += (long)Colors.Length * sizeof(float) * 4;
			if (Normals != null && Normals.Length > 0)
				pressure += (long)Normals.Length * sizeof(float) * 4;
			if (Depth != null && Depth.Length > 0)
				pressure += (long)Depth.Length * sizeof(int);
			if (ExtraDimensionArrays != null)
			{
				foreach (var arr in ExtraDimensionArrays.Values)
				{
					if (arr != null)
						pressure += (long)arr.Length * sizeof(float);
				}
			}

			if (pressure > 0)
			{
				GC.AddMemoryPressure(pressure);
				MemoryPressure = pressure;
				memoryPressureAdded = true;
			}
			*/
		}

	// NO FINALIZER for short-lived objects - causes GC pressure and finalizer queue backup
	// Rely on deterministic disposal through cache eviction

	public void Dispose()
	{
		if (disposed)
			return;

		try
		{
			if (memoryPressureAdded && MemoryPressure > 0)
			{
				try
				{
					GC.RemoveMemoryPressure(MemoryPressure);
				}
				catch
				{
					// Ignore exceptions from GC memory pressure API
				}
				finally
				{
					MemoryPressure = 0;
					memoryPressureAdded = false;
				}
			}

			Positions = Array.Empty<StrideVector4>();
			Colors = Array.Empty<StrideVector4>();
			Normals = Array.Empty<StrideVector4>();
			Depth = null;
		ExtraDimensionArrays?.Clear();
		ExtraDimensionArrays = null;
	}
	catch
	{
		// Ensure Dispose never throws
	}
	finally
	{
		disposed = true;
	}
}
	}

    /// <summary>
    /// Smart cache for COPC point cloud data with automatic memory management.
    /// Uses LRU (Least Recently Used) eviction policy when the cache is full.
    /// </summary>
    public class PointCache : IDisposable
    {
        private bool disposed;
        private bool cacheMemoryPressureAdded;
        // Configuration
        private readonly long maxMemoryBytes;
        private readonly int estimatedBytesPerPoint;
		private List<ExtraDimension>? extraDimensionsForSeparated;

        // Cache storage
        private readonly Dictionary<VoxelKey, LinkedListNode<CachedNodeData>> cache;
        private readonly LinkedList<CachedNodeData> lruList; // Head = most recent, Tail = least recent
        
        // Thread safety for vvvv gamma - multiple patches can call cache methods simultaneously
        private readonly object cacheLock = new object();
        
        // Statistics
        private long currentMemoryBytes;
        private long totalHits;
        private long totalMisses;
        private long totalEvictions;

		// Versioning
		private int cacheVersion;

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
        /// Gets the current cache version. This integer increments each time the cache is updated.
        /// </summary>
        public int Version => cacheVersion;

        /// <summary>
        /// Resets the cache version counter to zero.
        /// </summary>
        public void ResetVersion()
        {
            cacheVersion = 0;
        }

        /// <summary>
        /// Increments the cache version by 1. Call this after batch update operations.
        /// </summary>
        public void IncrementVersion()
        {
            cacheVersion++;
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
			cacheVersion = 0;

		cachedStrideData = null;
		strideCacheDirty = true;
		extraDimensionsForSeparated = null;
		disposed = false;
		cacheMemoryPressureAdded = false;

		// Inform GC about the memory pressure from this cache
		GC.AddMemoryPressure(maxMemoryBytes);
		cacheMemoryPressureAdded = true;
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
		/// Sets the extra dimension definitions used to precompute separated arrays.
		/// If set, the cache will build per-node separated arrays on Put() for reuse.
		/// </summary>
		public void SetExtraDimensions(List<ExtraDimension>? extraDimensions)
		{
			extraDimensionsForSeparated = extraDimensions;
		}

        /// <summary>
        /// Attempts to get cached point data for a node.
        /// </summary>
        /// <param name="key">The voxel key to look up</param>
        /// <param name="points">Output parameter containing the cached points if found</param>
        /// <returns>True if the data was in cache, false otherwise</returns>
        public bool TryGetPoints(VoxelKey key, out CopcPoint[]? points)
        {
            lock (cacheLock)
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
        }

        /// <summary>
        /// Adds or updates point data in the cache.
        /// </summary>
        /// <param name="key">The voxel key</param>
        /// <param name="points">The point data to cache</param>
        public void Put(VoxelKey key, CopcPoint[] points)
        {
            lock (cacheLock)
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
                    currentMemoryBytes -= (existingNode.Value.MemorySize + existingNode.Value.SeparatedMemorySize);
                    existingNode.Value.Separated?.Dispose();
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

                // Optionally precompute separated arrays for this node (once), accounting for memory
                if (extraDimensionsForSeparated != null && points.Length > 0)
                {
                    // Build separated arrays with depth information
                    using (var sepData = StrideCacheExtensions.BuildSeparatedFromCopcPoints(points, extraDimensionsForSeparated, null, key.D))
                    {
                        var sepMem = EstimateSeparatedMemorySize(sepData);

                        // Ensure capacity for both entries (points + separated for this node)
                        while (currentMemoryBytes + memorySize + sepMem > maxMemoryBytes && lruList.Count > 0)
                        {
                            EvictLeastRecentlyUsed();
                        }

                        // Only attach if fits
                        if (currentMemoryBytes + memorySize + sepMem <= maxMemoryBytes)
                        {
                            cachedData.Separated = new SeparatedNodeData
                            {
                                Positions = sepData.Positions ?? Array.Empty<StrideVector4>(),
                                Colors = sepData.Colors ?? Array.Empty<StrideVector4>(),
                                Normals = sepData.Normals ?? Array.Empty<StrideVector4>(),
                                Depth = sepData.Depth,
                                ExtraDimensionArrays = sepData.ExtraDimensionArrays
                            };
                            // Add memory pressure for the newly created SeparatedNodeData
                            cachedData.Separated.AddMemoryPressure();
                            cachedData.SeparatedMemorySize = sepMem;
                        }
                        // sepData will be disposed here, removing its memory pressure
                    }
                }

                var newNode = lruList.AddFirst(cachedData);
                cache[key] = newNode;
                currentMemoryBytes += memorySize + cachedData.SeparatedMemorySize;

                // Mark stride cache dirty since content changed
                strideCacheDirty = true;
            }
        }

		/// <summary>
		/// Adds or updates separated-only data in the cache (no CopcPoint[] stored).
		/// Use this when the application never needs CopcPoint objects.
		/// </summary>
		public void PutSeparatedOnly(VoxelKey key, SeparatedNodeData separated)
		{
			if (separated == null) return;

			// Remove existing entry if present
			if (cache.TryGetValue(key, out var existingNode))
			{
				currentMemoryBytes -= (existingNode.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 Value.MemorySize + existingNode.Value.SeparatedMemorySize);
				existingNode.Value.Separated?.Dispose();
				lruList.Remove(existingNode);
				cache.Remove(key);
			}

			// Compute memory size for separated
			var tempStride = new StrideCacheData
			{
				Positions = separated.Positions,
				Colors = separated.Colors,
				Normals = separated.Normals,
				Depth = separated.Depth,
				ExtraDimensionArrays = separated.ExtraDimensionArrays
			};
			long sepMem = EstimateSeparatedMemorySize(tempStride);

			// Evict until fits
			while (currentMemoryBytes + sepMem > maxMemoryBytes && lruList.Count > 0)
			{
				EvictLeastRecentlyUsed();
			}

			if (sepMem > maxMemoryBytes)
			{
				// Too large to fit even in empty cache
				return;
			}

			var nodeData = new CachedNodeData(key, Array.Empty<CopcPoint>(), 0)
			{
				Separated = separated,
				SeparatedMemorySize = sepMem
			};

			var newNode = lruList.AddFirst(nodeData);
			cache[key] = newNode;
			currentMemoryBytes += sepMem;

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
            IncrementVersion();
            
            return points;
        }


        private List<CopcPoint> allPoints = new List<CopcPoint>();
        private List<Node> nodesToLoadScratch = new List<Node>();
        private List<StridePoint> allStridePointsScratch = new List<StridePoint>();
        /// <summary>
        /// Gets points for multiple nodes, using cache when possible and loading only uncached nodes.
        /// This is more efficient than loading nodes one at a time.
        /// </summary>
        /// <param name="nodes">The nodes to get points for</param>
        /// <param name="reader">The COPC reader to use for cache misses</param>
        /// <returns>Array of all points from the nodes</returns>
        public CopcPoint[] GetOrLoadPointsFromNodes(IEnumerable<Node> nodes, CopcReader reader)
        {
            allPoints.Clear();
            nodesToLoadScratch.Clear();

            // First pass: check cache and collect nodes that need loading
            foreach (var node in nodes)
            {
                if (TryGetPoints(node.Key, out var cachedPoints) && cachedPoints != null)
                {
                    allPoints.AddRange(cachedPoints);
                }
                else
                {
                    nodesToLoadScratch.Add(node);
                }
            }

            // Second pass: load uncached nodes
            bool anyLoaded = false;
            foreach (var node in nodesToLoadScratch)
            {
                try
                {
                    var points = reader.GetPointsFromNode(node);
                    Put(node.Key, points);
                    allPoints.AddRange(points);
                    anyLoaded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load node {node.Key}: {ex.Message}");
                    // Continue with other nodes
                }
            }

            // Increment version once for this batch load
            if (anyLoaded)
            {
                IncrementVersion();
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
            lock (cacheLock)
            {
                return cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// Removes a specific entry from the cache.
        /// </summary>
        /// <param name="key">The voxel key to remove</param>
        /// <returns>True if the entry was removed, false if it wasn't in cache</returns>
        public bool Remove(VoxelKey key)
        {
            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out var node))
                {
                    currentMemoryBytes -= (node.Value.MemorySize + node.Value.SeparatedMemorySize);
                    node.Value.Separated?.Dispose();
                    lruList.Remove(node);
                    cache.Remove(key);
                    // Content changed
                    strideCacheDirty = true;
                    cacheVersion++;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            lock (cacheLock)
            {
                // Clear references and dispose separated data to help GC
                foreach (var node in lruList)
                {
                    node.Points = Array.Empty<CopcPoint>();
                    node.Separated?.Dispose();
                    node.Separated = null;
                }

                cache.Clear();
                lruList.Clear();
                currentMemoryBytes = 0;
                cachedStrideData?.Dispose();
                cachedStrideData = null;
                strideCacheDirty = true;
                cacheVersion++;

                // Clear scratch lists
                allPoints.Clear();
                nodesToLoadScratch.Clear();
                allStridePointsScratch.Clear();
            }
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
            lock (cacheLock)
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
			currentMemoryBytes -= (lruData.MemorySize + lruData.SeparatedMemorySize);
			
			// Dispose separated data to release GC pressure
			lruData.Separated?.Dispose();
			
            totalEvictions++;
			// Content changed
			strideCacheDirty = true;
			cacheVersion++;
        }

		/// <summary>
		/// Builds or returns cached Stride-format separated arrays for all cached points.
		/// Rebuilds only when cache content changes (on Put/Remove/Clear/Evict).
		/// </summary>
		public StrideCacheData GetOrBuildStrideCacheDataSeparated(List<ExtraDimension>? extraDimensions = null)
		{
			return GetOrBuildStrideCacheDataSeparated(extraDimensions, null);
		}

	/// <summary>
	/// Builds or returns cached Stride-format separated arrays for all cached points, optionally including only a subset of extra dimensions.
	/// Rebuilds only when cache content changes (on Put/Remove/Clear/Evict).
	/// </summary>
	public StrideCacheData GetOrBuildStrideCacheDataSeparated(List<ExtraDimension>? extraDimensions, HashSet<string>? includeExtraDimensionNames)
	{
		// Check if we can return cached data (quick check with lock)
		lock (cacheLock)
		{
			if (cachedStrideData != null && !strideCacheDirty)
			{
				return cachedStrideData;
			}
		}

		// CRITICAL: Only hold lock while READING from cache, not during slow array building
		// This prevents blocking UpdateSeparated() which needs to Put() into cache
		var nodePointArrays = new List<CopcPoint[]>();
		var nodeSeparated = new List<SeparatedNodeData?>();
		var nodeOffsets = new List<int>();
		var nodeKeys = new List<VoxelKey>();
		int totalPoints = 0;

		// Quick lock just to snapshot cache contents
		lock (cacheLock)
		{
			// Dispose old cached stride data if it exists
			cachedStrideData?.Dispose();

			// Gather cached arrays and compute total size
			foreach (var entry in lruList)
			{
				if (entry.Points != null && entry.Points.Length > 0)
				{
					nodeOffsets.Add(totalPoints);
					nodePointArrays.Add(entry.Points);
					nodeSeparated.Add(entry.Separated);
					nodeKeys.Add(entry.Key);
					totalPoints += entry.Points.Length;
				}
			}
		} // Release lock here - now UpdateSeparated can proceed while we build arrays

			// Allocate destination arrays
			var positions = new Stride.Core.Mathematics.Vector4[totalPoints];
			var colors = new Stride.Core.Mathematics.Vector4[totalPoints];
			var normals = new Stride.Core.Mathematics.Vector4[totalPoints];
			var depths = new int[totalPoints];

			Dictionary<string, float[]>? extraArrays = null;
			List<ExtraDimension>? dimsOrdered = null;
			bool includeExtras = extraDimensions != null && extraDimensions.Count > 0;
			if (includeExtras)
			{
				dimsOrdered = new List<ExtraDimension>(extraDimensions!);
				extraArrays = new Dictionary<string, float[]>(dimsOrdered.Count);
				for (int d = 0; d < dimsOrdered.Count; d++)
				{
					var dim = dimsOrdered[d];
					if (includeExtraDimensionNames != null && includeExtraDimensionNames.Count > 0 &&
						!includeExtraDimensionNames.Contains(dim.Name))
					{
						continue;
					}
					int comp = dim.GetComponentCount();
					extraArrays[dim.Name] = new float[totalPoints * comp];
				}
			}

			// Fill arrays per node; prefer block copies when separated data available
			// Use sequential processing for vvvv gamma stability
			int nodeCount = nodePointArrays.Count;
			for (int i = 0; i < nodeCount; i++)
			{
				var pts = nodePointArrays[i];
				if (pts == null || pts.Length == 0) continue;
				int start = nodeOffsets[i];
				var sep = nodeSeparated[i];
				var nodeKey = nodeKeys[i];
				int nodeDepth = nodeKey.D;

				if (sep != null && sep.Positions != null && sep.Positions.Length == pts.Length)
				{
					// Fast path: block copy precomputed arrays
					Array.Copy(sep.Positions, 0, positions, start, pts.Length);
					Array.Copy(sep.Colors, 0, colors, start, pts.Length);
					if (sep.Normals != null && sep.Normals.Length == pts.Length)
					{
						Array.Copy(sep.Normals, 0, normals, start, pts.Length);
					}

					// Copy or fill depth array
					if (sep.Depth != null && sep.Depth.Length == pts.Length)
					{
						Array.Copy(sep.Depth, 0, depths, start, pts.Length);
					}
					else
					{
						// Fill with node depth if not precomputed
						for (int k = 0; k < pts.Length; k++)
						{
							depths[start + k] = nodeDepth;
						}
					}

					if (includeExtras && dimsOrdered != null && extraArrays != null && sep.ExtraDimensionArrays != null)
					{
						foreach (var dim in dimsOrdered)
						{
							if (includeExtraDimensionNames != null && includeExtraDimensionNames.Count > 0 &&
								!includeExtraDimensionNames.Contains(dim.Name))
							{
								continue;
							}
							if (!sep.ExtraDimensionArrays.TryGetValue(dim.Name, out var nodeArr))
								continue;
							if (!extraArrays.TryGetValue(dim.Name, out var dst)) continue;
							int comp = dim.GetComponentCount();
							Buffer.BlockCopy(nodeArr, 0, dst, start * comp * sizeof(float), nodeArr.Length * sizeof(float));
						}
					}

					continue;
				}

				for (int k = 0; k < pts.Length; k++)
				{
					int dstIndex = start + k;
					var p = pts[k];

					positions[dstIndex] = new Stride.Core.Mathematics.Vector4((float)p.X, (float)p.Y, (float)p.Z, 1.0f);

					float r = p.Red ?? 1.0f;
					float g = p.Green ?? 1.0f;
					float b = p.Blue ?? 1.0f;
					colors[dstIndex] = new Stride.Core.Mathematics.Vector4(r, g, b, 1.0f);

					depths[dstIndex] = nodeDepth;

					if (includeExtras && p.ExtraBytes != null && p.ExtraBytes.Length > 0 && dimsOrdered != null && extraArrays != null)
					{
						int offset = 0;
						for (int d = 0; d < dimsOrdered.Count; d++)
						{
							var dim = dimsOrdered[d];
							int compSize = dim.GetDataSize();
							int compCount = dim.GetComponentCount();
							int totalSize = compSize * compCount;

							if (extraArrays.TryGetValue(dim.Name, out var arr))
							{
								int writeIndex = dstIndex * compCount;
								dim.ExtractAsFloat32Into(p.ExtraBytes, offset, arr, writeIndex);
							}
							offset += totalSize;
						}
					}
				}
			}

		var data = new StrideCacheData
		{
			Positions = positions,
			Colors = colors,
			Normals = normals,
			Depth = depths,
			ExtraDimensionArrays = extraArrays
		};
		data.AddMemoryPressure();

		// Quick lock to update cached data
		lock (cacheLock)
		{
			cachedStrideData = data;
			strideCacheDirty = false;
		}

		return data;
	}

		/// <summary>
		/// Builds separated arrays only for the specified nodes using cached points.
		/// Nodes must be pre-warmed; this does not trigger loading.
		/// includeExtraDimensionNames can be used to restrict which extra dimensions are extracted.
		/// </summary>
	public StrideCacheData BuildSeparatedFromNodes(IEnumerable<Node> nodes, List<ExtraDimension>? extraDimensions, HashSet<string>? includeExtraDimensionNames = null)
	{
		if (nodes == null) throw new ArgumentNullException(nameof(nodes));

		// CRITICAL: Only hold lock briefly while reading from cache
		// Release before expensive array operations to avoid blocking UpdateSeparated
		var nodeList = new List<Node>(nodes);
		var nodePointArrays = new List<CopcPoint[]>(nodeList.Count);
		var nodeSeparated = new List<SeparatedNodeData?>(nodeList.Count);
		var nodeOffsets = new int[nodeList.Count];
		int totalPoints = 0;

		// Quick lock just to read from cache
		lock (cacheLock)
		{
			for (int i = 0; i < nodeList.Count; i++)
			{
				var node = nodeList[i];
				// Direct cache access within lock - don't call TryGetPoints as it locks again
				if (cache.TryGetValue(node.Key, out var cacheNode) && cacheNode != null)
				{
					var pts = cacheNode.Value.Points;
					if (pts != null && pts.Length > 0)
					{
						nodeOffsets[i] = totalPoints;
						nodePointArrays.Add(pts);
						nodeSeparated.Add(cacheNode.Value.Separated);
						totalPoints += pts.Length;
					}
					else
					{
						nodeOffsets[i] = totalPoints;
						nodePointArrays.Add(Array.Empty<CopcPoint>());
						nodeSeparated.Add(null);
					}
				}
				else
				{
					nodeOffsets[i] = totalPoints;
					nodePointArrays.Add(Array.Empty<CopcPoint>());
					nodeSeparated.Add(null);
				}
			}
		} // Release lock - now UpdateSeparated can proceed while we build arrays

			var positions = new Stride.Core.Mathematics.Vector4[totalPoints];
			var colors = new Stride.Core.Mathematics.Vector4[totalPoints];
			var normals = new Stride.Core.Mathematics.Vector4[totalPoints];
			var depths = new int[totalPoints];

			Dictionary<string, float[]>? extraArrays = null;
			List<ExtraDimension>? dimsOrdered = null;
			bool includeExtras = extraDimensions != null && extraDimensions.Count > 0;
			if (includeExtras)
			{
				dimsOrdered = new List<ExtraDimension>(extraDimensions!);
				extraArrays = new Dictionary<string, float[]>(dimsOrdered.Count);
				for (int d = 0; d < dimsOrdered.Count; d++)
				{
					var dim = dimsOrdered[d];
					if (includeExtraDimensionNames != null && includeExtraDimensionNames.Count > 0 &&
						!includeExtraDimensionNames.Contains(dim.Name))
					{
						continue;
					}
					int comp = dim.GetComponentCount();
					extraArrays[dim.Name] = new float[totalPoints * comp];
				}
			}

		int nodeCount = nodePointArrays.Count;
		for (int i = 0; i < nodeCount; i++)
		{
			var pts = nodePointArrays[i];
			if (pts == null || pts.Length == 0) continue;
				int start = nodeOffsets[i];
				var sep = nodeSeparated[i];
				var node = nodeList[i];
				int nodeDepth = node.Key.D;

				if (sep != null && sep.Positions != null && sep.Positions.Length == pts.Length)
				{
					// Fast path: block copy the precomputed arrays
					Array.Copy(sep.Positions, 0, positions, start, pts.Length);
					Array.Copy(sep.Colors, 0, colors, start, pts.Length);
					if (sep.Normals != null && sep.Normals.Length == pts.Length)
					{
						Array.Copy(sep.Normals, 0, normals, start, pts.Length);
					}

					// Copy or fill depth array
					if (sep.Depth != null && sep.Depth.Length == pts.Length)
					{
						Array.Copy(sep.Depth, 0, depths, start, pts.Length);
					}
					else
					{
						// Fill with node depth if not precomputed
						for (int k = 0; k < pts.Length; k++)
						{
							depths[start + k] = nodeDepth;
						}
					}

					if (includeExtras && dimsOrdered != null && extraArrays != null && sep.ExtraDimensionArrays != null)
					{
						foreach (var dim in dimsOrdered)
						{
							if (includeExtraDimensionNames != null && includeExtraDimensionNames.Count > 0 &&
								!includeExtraDimensionNames.Contains(dim.Name))
							{
								continue;
							}
							if (!sep.ExtraDimensionArrays.TryGetValue(dim.Name, out var nodeArr))
								continue;
							if (!extraArrays.TryGetValue(dim.Name, out var dst)) continue;
							int comp = dim.GetComponentCount();
							Buffer.BlockCopy(nodeArr, 0, dst, start * comp * sizeof(float), nodeArr.Length * sizeof(float));
						}
					}

					continue;
				}

				for (int k = 0; k < pts.Length; k++)
				{
					int dstIndex = start + k;
					var p = pts[k];

					positions[dstIndex] = new Stride.Core.Mathematics.Vector4((float)p.X, (float)p.Y, (float)p.Z, 1.0f);

					float r = p.Red ?? 1.0f;
					float g = p.Green ?? 1.0f;
					float b = p.Blue ?? 1.0f;
					colors[dstIndex] = new Stride.Core.Mathematics.Vector4(r, g, b, 1.0f);

					depths[dstIndex] = nodeDepth;

					if (includeExtras && p.ExtraBytes != null && p.ExtraBytes.Length > 0 && dimsOrdered != null && extraArrays != null)
					{
						int offset = 0;
						for (int d = 0; d < dimsOrdered.Count; d++)
						{
							var dim = dimsOrdered[d];
							int compSize = dim.GetDataSize();
							int compCount = dim.GetComponentCount();
							int totalSize = compSize * compCount;

							if (extraArrays.TryGetValue(dim.Name, out var arr))
							{
								int writeIndex = dstIndex * compCount;
								dim.ExtractAsFloat32Into(p.ExtraBytes, offset, arr, writeIndex);
							}
							offset += totalSize;
						}
					}
				}
			}

			var result = new StrideCacheData
			{
				Positions = positions,
				Colors = colors,
				Normals = normals,
				Depth = depths,
				ExtraDimensionArrays = extraArrays
			};
			result.AddMemoryPressure();
			return result;
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

		/// <summary>
		/// Estimates memory size of separated arrays payload.
		/// </summary>
		private long EstimateSeparatedMemorySize(StrideCacheData data)
		{
			long size = 0;
			if (data.Positions != null) size += 24 + (long)data.Positions.Length * sizeof(float) * 4;
			if (data.Colors != null) size += 24 + (long)data.Colors.Length * sizeof(float) * 4;
			if (data.Normals != null) size += 24 + (long)data.Normals.Length * sizeof(float) * 4;
			if (data.Depth != null) size += 24 + (long)data.Depth.Length * sizeof(int);
			if (data.ExtraDimensionArrays != null)
			{
				foreach (var kvp in data.ExtraDimensionArrays)
				{
					size += 24 + (long)kvp.Value.Length * sizeof(float);
				}
			}
			return size;
		}

	/// <summary>
	/// Finalizer to ensure memory pressure is released even if Dispose is not called.
	/// </summary>
	~PointCache()
	{
		// Finalizers must NEVER throw exceptions or they crash the process
		try
		{
			if (cacheMemoryPressureAdded)
			{
				try
				{
					GC.RemoveMemoryPressure(maxMemoryBytes);
				}
				catch
				{
					// Swallow all exceptions in finalizer
				}
			}
		}
		catch
		{
			// Swallow ALL exceptions in finalizer - CRITICAL for stability
		}
	}

	/// <summary>
	/// Disposes the cache and releases all memory.
	/// </summary>
	public void Dispose()
	{
		if (disposed)
			return;

		try
		{
			// Clear all cached data and dispose separated data
			foreach (var node in lruList)
			{
				try
				{
					node.Points = Array.Empty<CopcPoint>();
					node.Separated?.Dispose();
					node.Separated = null;
				}
				catch
				{
					// Continue with other nodes even if one fails
				}
			}

			cache.Clear();
			lruList.Clear();
			
			try
			{
				cachedStrideData?.Dispose();
			}
			catch
			{
				// Ignore disposal errors
			}
			cachedStrideData = null;

			// Clear scratch lists
			allPoints.Clear();
			nodesToLoadScratch.Clear();
			allStridePointsScratch.Clear();

			// Release GC memory pressure only if it was added
			if (cacheMemoryPressureAdded)
			{
				try
				{
					GC.RemoveMemoryPressure(maxMemoryBytes);
				}
				catch
				{
					// Ignore exceptions from GC memory pressure API
				}
				finally
				{
					cacheMemoryPressureAdded = false;
				}
			}
		}
		catch
		{
			// Ensure Dispose never throws
		}
		finally
		{
			disposed = true;
			GC.SuppressFinalize(this);
		}
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

