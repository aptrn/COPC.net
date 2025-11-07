using System;
using System.Collections.Generic;
using System.Linq;
using Copc.IO;
using Stride.Core.Mathematics;

namespace Copc.Cache
{
    /// <summary>
    /// Represents a point cloud point in Stride engine format.
    /// Optimized for rendering with position and color as Vector4.
    /// </summary>
    public struct StridePoint
    {
        /// <summary>
        /// Position (X, Y, Z, W) where W = 1
        /// </summary>
        public Vector4 Position;

        /// <summary>
        /// Color (R, G, B, A) where A = 1
        /// Values normalized to 0-1 range
        /// </summary>
        public Vector4 Color;

        /// <summary>
        /// Creates a StridePoint from a CopcPoint.
        /// </summary>
        public static StridePoint FromCopcPoint(CopcPoint point)
        {
            return new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f  // W component set to 1
                ),
                Color = new Vector4(
                    // Normalize RGB from 0-65535 to 0-1
                    (point.Red ?? 0) / 65535.0f,
                    (point.Green ?? 0) / 65535.0f,
                    (point.Blue ?? 0) / 65535.0f,
                    1.0f  // Alpha component set to 1
                )
            };
        }

        /// <summary>
        /// Creates a StridePoint from a CopcPoint with intensity-based color.
        /// Useful for point clouds without RGB data.
        /// </summary>
        public static StridePoint FromCopcPointWithIntensity(CopcPoint point)
        {
            // Convert intensity (0-65535) to grayscale color (0-1)
            float intensity = point.Intensity / 65535.0f;

            return new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f
                ),
                Color = new Vector4(intensity, intensity, intensity, 1.0f)
            };
        }

        /// <summary>
        /// Creates a StridePoint with custom color (RGB values 0-1).
        /// </summary>
        public static StridePoint FromCopcPointWithColor(CopcPoint point, float r, float g, float b)
        {
            return new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f
                ),
                Color = new Vector4(r, g, b, 1.0f)
            };
        }

        /// <summary>
        /// Creates a StridePoint with classification-based color.
        /// Uses standard LAS classification color scheme.
        /// </summary>
        public static StridePoint FromCopcPointWithClassificationColor(CopcPoint point)
        {
            var color = GetClassificationColor(point.Classification);
            return new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f
                ),
                Color = new Vector4(color.X, color.Y, color.Z, 1.0f)
            };
        }

        /// <summary>
        /// Returns a standard color for LAS classification codes.
        /// </summary>
        private static Vector3 GetClassificationColor(byte classification)
        {
            return classification switch
            {
                0 => new Vector3(0.5f, 0.5f, 0.5f),    // Never classified - Gray
                1 => new Vector3(0.7f, 0.7f, 0.7f),    // Unclassified - Light Gray
                2 => new Vector3(0.6f, 0.4f, 0.2f),    // Ground - Brown
                3 => new Vector3(0.0f, 0.8f, 0.0f),    // Low Vegetation - Green
                4 => new Vector3(0.0f, 0.6f, 0.0f),    // Medium Vegetation - Dark Green
                5 => new Vector3(0.0f, 0.4f, 0.0f),    // High Vegetation - Darker Green
                6 => new Vector3(0.8f, 0.0f, 0.0f),    // Building - Red
                7 => new Vector3(0.8f, 0.8f, 0.0f),    // Low Point (noise) - Yellow
                9 => new Vector3(0.0f, 0.0f, 1.0f),    // Water - Blue
                17 => new Vector3(1.0f, 0.5f, 0.0f),   // Bridge Deck - Orange
                _ => new Vector3(0.5f, 0.5f, 0.5f)     // Other - Gray
            };
        }

        public override string ToString()
        {
            return $"Pos({Position.X:F2}, {Position.Y:F2}, {Position.Z:F2}) " +
                   $"Color({Color.X:F3}, {Color.Y:F3}, {Color.Z:F3})";
        }
    }

    /// <summary>
    /// Contains all cached point data in Stride format, ready for rendering.
    /// </summary>
    public class StrideCacheData
    {
        /// <summary>
        /// All points in the cache in Stride format.
        /// </summary>
        public StridePoint[] Points { get; set; } = Array.Empty<StridePoint>();

        /// <summary>
        /// Number of points.
        /// </summary>
        public int Count => Points?.Length ?? 0;

        /// <summary>
        /// Total memory size estimate in bytes.
        /// </summary>
        public long MemorySize => Count * (sizeof(float) * 8 + 32); // 8 floats + overhead

        /// <summary>
        /// Separate arrays for direct GPU upload (optional optimization).
        /// </summary>
        public Vector4[]? Positions { get; set; }
        public Vector4[]? Colors { get; set; }

        /// <summary>
        /// Generates separate position and color arrays.
        /// Useful for uploading to GPU as separate vertex buffers.
        /// </summary>
        public void GenerateSeparateArrays()
        {
            if (Points == null || Points.Length == 0)
                return;

            Positions = new Vector4[Points.Length];
            Colors = new Vector4[Points.Length];

            for (int i = 0; i < Points.Length; i++)
            {
                Positions[i] = Points[i].Position;
                Colors[i] = Points[i].Color;
            }
        }

        public override string ToString()
        {
            return $"StrideCacheData: {Count:N0} points, {MemorySize / 1024.0 / 1024.0:F2} MB";
        }
    }

    /// <summary>
    /// Conversion options for Stride format.
    /// </summary>
    public enum StrideColorMode
    {
        /// <summary>Use RGB values from point cloud (default to white if missing)</summary>
        RGB,
        /// <summary>Use intensity as grayscale</summary>
        Intensity,
        /// <summary>Use classification-based colors</summary>
        Classification,
        /// <summary>Use elevation-based gradient</summary>
        Elevation
    }

    /// <summary>
    /// Extension methods for converting cached data to Stride format.
    /// </summary>
    public static class StrideCacheExtensions
    {
        /// <summary>
        /// Gets all cached data converted to Stride format.
        /// </summary>
        public static StrideCacheData GetCacheData(this PointCache cache, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var cachedNodes = cache.GetCachedNodes();
            var allPoints = new List<StridePoint>();

            foreach (var nodeInfo in cachedNodes)
            {
                // Get the points from cache
                if (cache.TryGetPoints(nodeInfo.Key, out var copcPoints) && copcPoints != null)
                {
                    var stridePoints = ConvertToStridePoints(copcPoints, colorMode);
                    allPoints.AddRange(stridePoints);
                }
            }

            return new StrideCacheData
            {
                Points = allPoints.ToArray()
            };
        }

        /// <summary>
        /// Gets all cached data with separate position and color arrays.
        /// Useful for direct GPU buffer upload.
        /// </summary>
        public static StrideCacheData GetCacheDataSeparated(this PointCache cache, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var data = GetCacheData(cache, colorMode);
            data.GenerateSeparateArrays();
            return data;
        }

        /// <summary>
        /// Converts an array of CopcPoints to StridePoints.
        /// </summary>
        public static StridePoint[] ConvertToStridePoints(CopcPoint[] copcPoints, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            var stridePoints = new StridePoint[copcPoints.Length];

            for (int i = 0; i < copcPoints.Length; i++)
            {
                stridePoints[i] = colorMode switch
                {
                    StrideColorMode.RGB => StridePoint.FromCopcPoint(copcPoints[i]),
                    StrideColorMode.Intensity => StridePoint.FromCopcPointWithIntensity(copcPoints[i]),
                    StrideColorMode.Classification => StridePoint.FromCopcPointWithClassificationColor(copcPoints[i]),
                    StrideColorMode.Elevation => FromCopcPointWithElevationColor(copcPoints[i], copcPoints),
                    _ => StridePoint.FromCopcPoint(copcPoints[i])
                };
            }

            return stridePoints;
        }

        /// <summary>
        /// Converts a single CopcPoint to StridePoint.
        /// </summary>
        public static StridePoint ToStridePoint(this CopcPoint point, StrideColorMode colorMode = StrideColorMode.RGB)
        {
            return colorMode switch
            {
                StrideColorMode.RGB => StridePoint.FromCopcPoint(point),
                StrideColorMode.Intensity => StridePoint.FromCopcPointWithIntensity(point),
                StrideColorMode.Classification => StridePoint.FromCopcPointWithClassificationColor(point),
                _ => StridePoint.FromCopcPoint(point)
            };
        }

        /// <summary>
        /// Creates a StridePoint with elevation-based color gradient.
        /// </summary>
        private static StridePoint FromCopcPointWithElevationColor(CopcPoint point, CopcPoint[] allPoints)
        {
            // Calculate Z range from all points
            double minZ = allPoints.Min(p => p.Z);
            double maxZ = allPoints.Max(p => p.Z);
            double range = maxZ - minZ;

            // Normalize Z to 0-1
            float normalizedZ = range > 0 ? (float)((point.Z - minZ) / range) : 0.5f;

            // Create color gradient: blue (low) -> green (mid) -> red (high)
            Vector3 color;
            if (normalizedZ < 0.5f)
            {
                // Blue to Green
                float t = normalizedZ * 2.0f;
                color = new Vector3(0, t, 1.0f - t);
            }
            else
            {
                // Green to Red
                float t = (normalizedZ - 0.5f) * 2.0f;
                color = new Vector3(t, 1.0f - t, 0);
            }

            return new StridePoint
            {
                Position = new Vector4(
                    (float)point.X,
                    (float)point.Y,
                    (float)point.Z,
                    1.0f
                ),
                Color = new Vector4(color.X, color.Y, color.Z, 1.0f)
            };
        }
    }
}

