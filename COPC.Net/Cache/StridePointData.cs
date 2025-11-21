using System;
using System.Collections.Generic;
using System.Linq;
using Copc.IO;
using Stride.Core.Mathematics;

namespace Copc.Cache
{
    /// <summary>
    /// Represents a point cloud point in Stride engine format.
    /// Only Position and Color are stored explicitly; Extra dimensions are optional.
    /// </summary>
    public struct StridePoint
    {
        /// <summary>
        /// Position (X, Y, Z, W) where W = 1
        /// </summary>
        public Vector4 Position;

        /// <summary>
        /// Color (R, G, B, A) where A = 1 - ONLY RGB, normalized to 0-1 range
        /// If point has no RGB data, defaults to white (1, 1, 1, 1)
        /// </summary>
        public Vector4 Color;

        /// <summary>
        /// Extra dimension values (from Extra Bytes VLR), stored as Dictionary[dimension_name, float32[]]
        /// Each extra dimension may have 1-3 components.
        /// For scalar_* fields, this will typically be a single float32 value.
        /// </summary>
        public Dictionary<string, float[]>? ExtraDimensions;

        /// <summary>
        /// Creates a StridePoint from a CopcPoint.
        /// </summary>
        /// <param name="point">The CopcPoint to convert</param>
        /// <param name="extraDimDefinitions">Optional extra dimension definitions for parsing ExtraBytes</param>
        public static StridePoint FromCopcPoint(CopcPoint point, List<ExtraDimension>? extraDimDefinitions = null)
        {
            var stridePoint = new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f  // W component set to 1
                ),
                Color = new Vector4(
					// CopcPoint stores RGB already normalized to 0-1; default to white if not present
					point.Red ?? 1.0f,
					point.Green ?? 1.0f,
					point.Blue ?? 1.0f,
                    1.0f  // Alpha component set to 1
                )
            };

            // Extract extra dimensions if present
            if (point.ExtraBytes != null && point.ExtraBytes.Length > 0 && 
                extraDimDefinitions != null && extraDimDefinitions.Count > 0)
            {
                stridePoint.ExtraDimensions = new Dictionary<string, float[]>();
                int offset = 0;

                foreach (var dim in extraDimDefinitions)
                {
                    try
                    {
                        int dimSize = dim.GetDataSize() * dim.GetComponentCount();
                        if (offset + dimSize <= point.ExtraBytes.Length)
                        {
                            float[] values = dim.ExtractAsFloat32(point.ExtraBytes, offset);
                            stridePoint.ExtraDimensions[dim.Name] = values;
                            offset += dimSize;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to extract extra dimension '{dim.Name}': {ex.Message}");
                    }
                }
            }

            return stridePoint;
        }

        public override string ToString()
        {
            return $"Pos({Position.X:F2}, {Position.Y:F2}, {Position.Z:F2}) " +
                   $"Color({Color.X:F3}, {Color.Y:F3}, {Color.Z:F3})";
        }
    }

    /// <summary>
    /// Contains all cached point data in Stride format, ready for rendering.
    /// Provides both combined and separate arrays for flexible GPU upload.
    /// </summary>
    public class StrideCacheData : IDisposable
    {
        private bool disposed;
        private long memoryPressure;
        /// <summary>
        /// All points in the cache in Stride format (interleaved).
        /// </summary>
        public StridePoint[] Points { get; set; } = Array.Empty<StridePoint>();

        /// <summary>
        /// Number of points.
        /// </summary>
		public int Count => (Points != null && Points.Length > 0)
			? Points.Length
			: (Positions?.Length ?? 0);

        /// <summary>
        /// Total memory size estimate in bytes.
        /// </summary>
		public long MemorySize => (long)Count * (sizeof(float) * 8 + 32); // 8 floats (2 Vector4) + overhead

        /// <summary>
        /// Separate arrays for direct GPU upload as vertex attributes.
        /// Call GenerateSeparateArrays() to populate these.
        /// </summary>
        public Vector4[]? Positions { get; set; }
        public Vector4[]? Colors { get; set; }

        /// <summary>
        /// Depth index array storing the octree depth level for each point.
        /// Each element corresponds to the depth (D value from VoxelKey) of the node that the point came from.
        /// This allows reconstruction of which depth level each point corresponds to.
        /// </summary>
        public int[]? Depth { get; set; }

        /// <summary>
        /// Extra dimension arrays [dimension_name -> float32[num_points * num_components]]
        /// For scalar_* fields, each array contains one float per point.
        /// For vector fields, each array contains N floats per point (where N is component count).
        /// </summary>
        public Dictionary<string, float[]>? ExtraDimensionArrays { get; set; }

        /// <summary>
        /// Generates separate arrays for each attribute.
        /// Useful for uploading to GPU as separate vertex attribute buffers.
        /// </summary>
        public void GenerateSeparateArrays()
        {
            if (Points == null || Points.Length == 0)
                return;

            int count = Points.Length;
            Positions = new Vector4[count];
            Colors = new Vector4[count];

            for (int i = 0; i < count; i++)
            {
                var p = Points[i];
                Positions[i] = p.Position;
                Colors[i] = p.Color;
            }

            // Generate extra dimension arrays
            GenerateExtraDimensionArrays();
        }

        /// <summary>
        /// Generates separate arrays for extra dimensions.
        /// Each extra dimension gets its own array: dimension_name -> float32[]
        /// For multi-component dimensions, the array is interleaved [x1,y1,z1, x2,y2,z2, ...]
        /// </summary>
        public void GenerateExtraDimensionArrays()
        {
            if (Points == null || Points.Length == 0)
                return;

            // Find all unique extra dimension names across all points
            var allExtraDimNames = new HashSet<string>();
            foreach (var point in Points)
            {
                if (point.ExtraDimensions != null)
                {
                    foreach (var dimName in point.ExtraDimensions.Keys)
                    {
                        allExtraDimNames.Add(dimName);
                    }
                }
            }

            if (allExtraDimNames.Count == 0)
                return;

            ExtraDimensionArrays = new Dictionary<string, float[]>();

            foreach (var dimName in allExtraDimNames)
            {
                // Determine component count from first point that has this dimension
                int componentCount = 1;
                foreach (var point in Points)
                {
                    if (point.ExtraDimensions?.TryGetValue(dimName, out var values) == true)
                    {
                        componentCount = values.Length;
                        break;
                    }
                }

                // Create array for this dimension
                var dimArray = new float[Points.Length * componentCount];

                // Populate array
                for (int i = 0; i < Points.Length; i++)
                {
                    if (Points[i].ExtraDimensions?.TryGetValue(dimName, out var values) == true)
                    {
                        for (int c = 0; c < componentCount; c++)
                        {
                            dimArray[i * componentCount + c] = c < values.Length ? values[c] : 0f;
                        }
                    }
                    else
                    {
                        // Fill with zeros if dimension not present for this point
                        for (int c = 0; c < componentCount; c++)
                        {
                            dimArray[i * componentCount + c] = 0f;
                        }
                    }
                }

                ExtraDimensionArrays[dimName] = dimArray;
            }
        }

        /// <summary>
        /// Adds GC memory pressure hint for the allocated arrays.
        /// Call this after allocating large arrays.
        /// </summary>
        internal void AddMemoryPressure()
        {
            if (memoryPressure > 0)
                return; // Already added

            long pressure = 0;
            if (Positions != null)
                pressure += (long)Positions.Length * sizeof(float) * 4;
            if (Colors != null)
                pressure += (long)Colors.Length * sizeof(float) * 4;
            if (Depth != null)
                pressure += (long)Depth.Length * sizeof(int);
            if (ExtraDimensionArrays != null)
            {
                foreach (var arr in ExtraDimensionArrays.Values)
                {
                    if (arr != null)
                        pressure += (long)arr.Length * sizeof(float);
                }
            }
            if (Points != null)
                pressure += (long)Points.Length * (sizeof(float) * 8 + 64); // Estimate for struct + dictionaries

            if (pressure > 0)
            {
                GC.AddMemoryPressure(pressure);
                memoryPressure = pressure;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (memoryPressure > 0)
            {
                GC.RemoveMemoryPressure(memoryPressure);
                memoryPressure = 0;
            }

            Points = Array.Empty<StridePoint>();
            Positions = null;
            Colors = null;
            Depth = null;
            ExtraDimensionArrays?.Clear();
            ExtraDimensionArrays = null;

            disposed = true;
        }

        public override string ToString()
        {
            return $"StrideCacheData: {Count:N0} points, {MemorySize / 1024.0 / 1024.0:F2} MB";
        }
    }

    /// <summary>
    /// Extension methods for converting cached data to Stride format.
    /// </summary>
    public static class StrideCacheExtensions
    {
		/// <summary>
		/// Builds separated arrays directly from CopcPoint[] without creating StridePoint[]
		/// and without per-point dictionary allocations. Optionally include only a subset
		/// of extra-dimension names (pass null to include all).
		/// </summary>
		/// <param name="copcPoints">Points to convert</param>
		/// <param name="extraDimDefinitions">Optional extra dimension definitions</param>
		/// <param name="includeExtraDimensionNames">Optional filter for extra dimensions to include</param>
		/// <param name="depth">Optional depth value to assign to all points (from Node.Key.D)</param>
		public static StrideCacheData BuildSeparatedFromCopcPoints(
			CopcPoint[] copcPoints,
			List<ExtraDimension>? extraDimDefinitions = null,
			HashSet<string>? includeExtraDimensionNames = null,
			int? depth = null)
		{
			int count = copcPoints?.Length ?? 0;
			var positions = new Vector4[count];
			var colors = new Vector4[count];
			var depths = depth.HasValue ? new int[count] : null;

			Dictionary<string, float[]>? extraArrays = null;
			List<ExtraDimension>? dimsOrdered = null;

			bool includeExtras = extraDimDefinitions != null && extraDimDefinitions.Count > 0;
			if (includeExtras)
			{
				// Prepare the ordered dimension list and allocate arrays per selected dimension
				dimsOrdered = new List<ExtraDimension>(extraDimDefinitions!);
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
					extraArrays[dim.Name] = new float[count * comp];
				}
			}

			for (int i = 0; i < count; i++)
			{
				var p = copcPoints[i];
				positions[i] = new Vector4((float)p.X, (float)p.Y, (float)p.Z, 1.0f);

				float r = p.Red ?? 1.0f;
				float g = p.Green ?? 1.0f;
				float b = p.Blue ?? 1.0f;
				colors[i] = new Vector4(r, g, b, 1.0f);

				if (depths != null && depth.HasValue)
				{
					depths[i] = depth.Value;
				}

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
							// Write directly into the destination array at the correct slot
							int destIndex = i * compCount;
							dim.ExtractAsFloat32Into(p.ExtraBytes, offset, arr, destIndex);
						}
						offset += totalSize;
					}
				}
			}

			var result = new StrideCacheData
			{
				Positions = positions,
				Colors = colors,
				Depth = depths,
				ExtraDimensionArrays = extraArrays
			};
			result.AddMemoryPressure();
			return result;
		}

        /// <summary>
        /// Gets all cached data converted to Stride format.
        /// </summary>
        /// <param name="cache">The point cache</param>
        /// <param name="extraDimDefinitions">Optional extra dimension definitions</param>
        public static StrideCacheData GetCacheData(this PointCache cache, List<ExtraDimension>? extraDimDefinitions = null)
        {
            var cachedNodes = cache.GetCachedNodes();
            var allPoints = new List<StridePoint>();

            foreach (var nodeInfo in cachedNodes)
            {
                // Get the points from cache
                if (cache.TryGetPoints(nodeInfo.Key, out var copcPoints) && copcPoints != null)
                {
                    var stridePoints = ConvertToStridePoints(copcPoints, extraDimDefinitions);
                    allPoints.AddRange(stridePoints);
                }
            }

            return new StrideCacheData
            {
                Points = allPoints.ToArray()
            };
        }

        /// <summary>
        /// Gets all cached data with separate arrays for each attribute.
        /// Useful for direct GPU buffer upload as vertex attributes.
        /// </summary>
        /// <param name="cache">The point cache</param>
        /// <param name="extraDimDefinitions">Optional extra dimension definitions</param>
        public static StrideCacheData GetCacheDataSeparated(this PointCache cache, List<ExtraDimension>? extraDimDefinitions = null)
        {
            var data = GetCacheData(cache, extraDimDefinitions);
            data.GenerateSeparateArrays();
            return data;
        }

        /// <summary>
        /// Converts an array of CopcPoints to StridePoints.
        /// </summary>
        /// <param name="copcPoints">Points to convert</param>
        /// <param name="extraDimDefinitions">Optional extra dimension definitions</param>
        public static StridePoint[] ConvertToStridePoints(CopcPoint[] copcPoints, List<ExtraDimension>? extraDimDefinitions = null)
        {
            var stridePoints = new StridePoint[copcPoints.Length];

            for (int i = 0; i < copcPoints.Length; i++)
            {
                stridePoints[i] = StridePoint.FromCopcPoint(copcPoints[i], extraDimDefinitions);
            }

            return stridePoints;
        }

        /// <summary>
        /// Converts a single CopcPoint to StridePoint.
        /// </summary>
        /// <param name="point">Point to convert</param>
        /// <param name="extraDimDefinitions">Optional extra dimension definitions</param>
        public static StridePoint ToStridePoint(this CopcPoint point, List<ExtraDimension>? extraDimDefinitions = null)
        {
            return StridePoint.FromCopcPoint(point, extraDimDefinitions);
        }
    }
}

