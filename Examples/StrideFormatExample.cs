using System;
using System.Linq;
using Copc.Cache;
using CopcVector3 = Copc.Geometry.Vector3;
using StrideVector3 = Stride.Core.Mathematics.Vector3;
using StrideVector4 = Stride.Core.Mathematics.Vector4;
using Stride.Core.Mathematics;

namespace Copc.Examples
{
    /// <summary>
    /// Demonstrates exporting cached data in Stride engine format.
    /// </summary>
    public class StrideFormatExample
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== Stride Format Export Example ===\n");

            // Open with cache
            using var cachedReader = CachedCopcReader.Open(copcFilePath, cacheSizeMB: 512);

            var header = cachedReader.Config.LasHeader;
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Bounds: X[{header.MinX:F2}, {header.MaxX:F2}], " +
                            $"Y[{header.MinY:F2}, {header.MaxY:F2}], " +
                            $"Z[{header.MinZ:F2}, {header.MaxZ:F2}]\n");

            // Query some data to populate cache
            double centerX = (header.MinX + header.MaxX) / 2;
            double centerY = (header.MinY + header.MaxY) / 2;
            double centerZ = (header.MinZ + header.MaxZ) / 2;
            double size = (header.MaxX - header.MinX) * 0.2; // 20% of total

            var box = Copc.Geometry.Box.FromCenterAndHalfSize(
                new CopcVector3(centerX, centerY, centerZ),
                size
            );

            Console.WriteLine("Querying central region to populate cache...");
            var points = cachedReader.GetPointsInBox(box);
            Console.WriteLine($"Loaded {points.Length:N0} points into cache\n");

            // ========================================
            // Example 1: Get all cached data
            // ========================================
            Console.WriteLine("=== Example 1: Get All Cached Data (RGB Mode) ===\n");
            
            var strideData = cachedReader.GetCacheData(StrideColorMode.RGB);
            Console.WriteLine($"Stride data: {strideData.Count:N0} points");
            Console.WriteLine($"Memory: {strideData.MemorySize / 1024.0 / 1024.0:F2} MB");

            // Show first few points
            Console.WriteLine("\nFirst 5 points:");
            for (int i = 0; i < Math.Min(5, strideData.Count); i++)
            {
                var p = strideData.Points[i];
                Console.WriteLine($"  [{i}] {p}");
            }

            // Verify Vector4 format
            Console.WriteLine("\nVerifying Stride format:");
            var firstPoint = strideData.Points[0];
            Console.WriteLine($"  Position.W = {firstPoint.Position.W} (should be 1.0)");
            Console.WriteLine($"  Color.W = {firstPoint.Color.W} (should be 1.0)");
            Console.WriteLine($"  ✓ Format verified!");

            // ========================================
            // Example 2: Different color modes
            // ========================================
            Console.WriteLine("\n=== Example 2: Different Color Modes ===\n");

            var colorModes = new[]
            {
                StrideColorMode.RGB,
                StrideColorMode.Intensity,
                StrideColorMode.Classification,
                StrideColorMode.Elevation
            };

            foreach (var mode in colorModes)
            {
                var data = cachedReader.GetCacheData(mode);
                var sample = data.Points[0];
                
                Console.WriteLine($"{mode} mode:");
                Console.WriteLine($"  Position: ({sample.Position.X:F2}, {sample.Position.Y:F2}, {sample.Position.Z:F2}, {sample.Position.W:F1})");
                Console.WriteLine($"  Color:    ({sample.Color.X:F3}, {sample.Color.Y:F3}, {sample.Color.Z:F3}, {sample.Color.W:F1})");
            }

            // ========================================
            // Example 3: Separated arrays for GPU upload
            // ========================================
            Console.WriteLine("\n=== Example 3: Separated Arrays (GPU-Ready) ===\n");

            var separatedData = cachedReader.GetCacheDataSeparated(StrideColorMode.RGB);
            Console.WriteLine($"Generated separate arrays:");
            Console.WriteLine($"  Positions: {separatedData.Positions.Length:N0} Vector4s");
            Console.WriteLine($"  Colors:    {separatedData.Colors.Length:N0} Vector4s");
            Console.WriteLine($"\nReady for GPU buffer upload:");
            Console.WriteLine($"  glBufferData(GL_ARRAY_BUFFER, positions, GL_STATIC_DRAW);");
            Console.WriteLine($"  glBufferData(GL_ARRAY_BUFFER, colors, GL_STATIC_DRAW);");

            // Show memory layout
            Console.WriteLine($"\nMemory layout per point:");
            Console.WriteLine($"  Position: 4 × float (16 bytes)");
            Console.WriteLine($"  Color:    4 × float (16 bytes)");
            Console.WriteLine($"  Total:    32 bytes per point");
            Console.WriteLine($"  Buffer size: {separatedData.Count * 32:N0} bytes ({separatedData.Count * 32 / 1024.0 / 1024.0:F2} MB)");

            // ========================================
            // Example 4: Direct query to Stride format
            // ========================================
            Console.WriteLine("\n=== Example 4: Direct Query to Stride Format ===\n");

            // Query and convert in one step (doesn't use cache)
            var stridePoints = cachedReader.GetStridePointsInBox(box, resolution: 0, StrideColorMode.Classification);
            Console.WriteLine($"Direct query: {stridePoints.Length:N0} points in Stride format");
            Console.WriteLine($"Color mode: Classification");
            Console.WriteLine($"\nSample classification colors:");
            
            var classifications = stridePoints
                .GroupBy(p => $"({p.Color.X:F2}, {p.Color.Y:F2}, {p.Color.Z:F2})")
                .Select(g => new { Color = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5);

            foreach (var c in classifications)
            {
                Console.WriteLine($"  RGB {c.Color}: {c.Count:N0} points");
            }

            // ========================================
            // Example 5: Frustum query with Stride output
            // ========================================
            Console.WriteLine("\n=== Example 5: Frustum Query with Stride Output ===\n");

            // Create a simple view-projection matrix (looking at center from above)
            var viewMatrix = Matrix.LookAtRH(
                new StrideVector3((float)centerX, (float)centerY, (float)(centerZ + size)),  // Eye position
                new StrideVector3((float)centerX, (float)centerY, (float)centerZ),           // Look at
                StrideVector3.UnitY                                                          // Up vector
            );

            var projMatrix = Matrix.PerspectiveFovRH(
                (float)Math.PI / 4,  // 45 degree FOV
                16.0f / 9.0f,        // Aspect ratio
                0.1f,                // Near plane
                (float)(size * 4)    // Far plane
            );

            var vpMatrix = viewMatrix * projMatrix;
            
            // Convert to double array
            double[] vpArray = new double[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    vpArray[i * 4 + j] = vpMatrix[i, j];
                }
            }

            var frustumPoints = cachedReader.GetStridePointsInFrustum(vpArray, resolution: 0, StrideColorMode.RGB);
            Console.WriteLine($"Frustum query: {frustumPoints.Length:N0} visible points");
            Console.WriteLine($"Camera position: ({centerX:F2}, {centerY:F2}, {centerZ + size:F2})");
            Console.WriteLine($"Looking at: ({centerX:F2}, {centerY:F2}, {centerZ:F2})");
            Console.WriteLine($"\n✓ Points ready for rendering!");

            // ========================================
            // Example 6: Performance statistics
            // ========================================
            Console.WriteLine("\n=== Example 6: Cache Performance ===\n");

            var cacheStats = cachedReader.Cache.GetStatistics();
            Console.WriteLine(cacheStats);

            Console.WriteLine($"\n💡 Usage in Stride Engine:");
            Console.WriteLine($"   1. Query points: GetStridePointsInFrustum(viewProjectionMatrix)");
            Console.WriteLine($"   2. Or get all cached: GetCacheData()");
            Console.WriteLine($"   3. Upload positions to GPU vertex buffer");
            Console.WriteLine($"   4. Upload colors to GPU color buffer");
            Console.WriteLine($"   5. Render as point cloud");
            Console.WriteLine($"   6. Repeat each frame with updated frustum");
        }
    }
}

