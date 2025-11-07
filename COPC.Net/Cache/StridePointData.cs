using System;
using System.Collections.Generic;
using System.Linq;
using Copc.IO;
using Stride.Core.Mathematics;

namespace Copc.Cache
{
    /// <summary>
    /// Represents a point cloud point in Stride engine format.
    /// Position and Color as Vector4, all other attributes as separate float32 values.
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
        /// Intensity value (normalized 0-1 from 0-65535)
        /// </summary>
        public float Intensity;

        /// <summary>
        /// Classification value (0-255)
        /// </summary>
        public float Classification;

        /// <summary>
        /// Return number (1-15)
        /// </summary>
        public float ReturnNumber;

        /// <summary>
        /// Number of returns (1-15)
        /// </summary>
        public float NumberOfReturns;

        /// <summary>
        /// Scan angle (degrees)
        /// </summary>
        public float ScanAngle;

        /// <summary>
        /// User data (0-255)
        /// </summary>
        public float UserData;

        /// <summary>
        /// Point source ID (0-65535)
        /// </summary>
        public float PointSourceId;

        /// <summary>
        /// GPS time (if available, otherwise 0)
        /// </summary>
        public float GpsTime;

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
                    // Normalize RGB from 0-65535 to 0-1, default to white if not present
                    (point.Red ?? 65535) / 65535.0f,
                    (point.Green ?? 65535) / 65535.0f,
                    (point.Blue ?? 65535) / 65535.0f,
                    1.0f  // Alpha component set to 1
                ),
                Intensity = point.Intensity / 65535.0f,
                Classification = point.Classification,
                ReturnNumber = point.ReturnNumber,
                NumberOfReturns = point.NumberOfReturns,
                ScanAngle = (float)point.ScanAngle,
                UserData = point.UserData,
                PointSourceId = point.PointSourceId,
                GpsTime = (float)(point.GpsTime ?? 0.0)
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
                   $"Color({Color.X:F3}, {Color.Y:F3}, {Color.Z:F3}) " +
                   $"Intensity={Intensity:F3} Class={Classification}";
        }
    }

    /// <summary>
    /// Contains all cached point data in Stride format, ready for rendering.
    /// Provides both combined and separate arrays for flexible GPU upload.
    /// </summary>
    public class StrideCacheData
    {
        /// <summary>
        /// All points in the cache in Stride format (interleaved).
        /// </summary>
        public StridePoint[] Points { get; set; } = Array.Empty<StridePoint>();

        /// <summary>
        /// Number of points.
        /// </summary>
        public int Count => Points?.Length ?? 0;

        /// <summary>
        /// Total memory size estimate in bytes.
        /// </summary>
        public long MemorySize => Count * (sizeof(float) * 18 + 32); // 18 floats (2 Vector4 + 10 floats) + overhead

        /// <summary>
        /// Separate arrays for direct GPU upload as vertex attributes.
        /// Call GenerateSeparateArrays() to populate these.
        /// </summary>
        public Vector4[]? Positions { get; set; }
        public Vector4[]? Colors { get; set; }
        public float[]? Intensities { get; set; }
        public float[]? Classifications { get; set; }
        public float[]? ReturnNumbers { get; set; }
        public float[]? NumberOfReturns { get; set; }
        public float[]? ScanAngles { get; set; }
        public float[]? UserData { get; set; }
        public float[]? PointSourceIds { get; set; }
        public float[]? GpsTimes { get; set; }

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
            Intensities = new float[count];
            Classifications = new float[count];
            ReturnNumbers = new float[count];
            NumberOfReturns = new float[count];
            ScanAngles = new float[count];
            UserData = new float[count];
            PointSourceIds = new float[count];
            GpsTimes = new float[count];

            for (int i = 0; i < count; i++)
            {
                var p = Points[i];
                Positions[i] = p.Position;
                Colors[i] = p.Color;
                Intensities[i] = p.Intensity;
                Classifications[i] = p.Classification;
                ReturnNumbers[i] = p.ReturnNumber;
                NumberOfReturns[i] = p.NumberOfReturns;
                ScanAngles[i] = p.ScanAngle;
                UserData[i] = p.UserData;
                PointSourceIds[i] = p.PointSourceId;
                GpsTimes[i] = p.GpsTime;
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

