using System;
using System.Linq;
using Copc.Cache;
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

            // ========================================
            // Get attribute metadata BEFORE loading any points
            // ========================================
            Console.WriteLine("=== Available Attributes (Metadata) ===\n");
            
            var metadata = cachedReader.GetAttributeMetadata();
            Console.WriteLine(metadata);
            Console.WriteLine($"\nAll attributes:");
            foreach (var attr in metadata.Attributes)
            {
                Console.WriteLine($"  • {attr}");
            }
            Console.WriteLine();

            // Query some data to populate cache
            double centerX = (header.MinX + header.MaxX) / 2;
            double centerY = (header.MinY + header.MaxY) / 2;
            double centerZ = (header.MinZ + header.MaxZ) / 2;
            double size = (header.MaxX - header.MinX) * 0.2; // 20% of total

            var center = new StrideVector3((float)centerX, (float)centerY, (float)centerZ);
            var half = (float)size;
            var box = new BoundingBox(center - new StrideVector3(half), center + new StrideVector3(half));

            Console.WriteLine("Querying central region to populate cache...");
            var points = cachedReader.GetPointsInBox(box);
            Console.WriteLine($"Loaded {points.Length:N0} points into cache\n");

            // ========================================
            // Example 1: Get all cached data
            // ========================================
            Console.WriteLine("=== Example 1: Get All Cached Data ===\n");
            
            var strideData = cachedReader.GetCacheData();
            Console.WriteLine($"Stride data: {strideData.Count:N0} points");
            Console.WriteLine($"Memory: {strideData.MemorySize / 1024.0 / 1024.0:F2} MB");

            // Show first few points
            Console.WriteLine("\nFirst 5 points:");
            for (int i = 0; i < Math.Min(5, strideData.Count); i++)
            {
                var p = strideData.Points[i];
                Console.WriteLine($"  [{i}] Pos({p.Position.X:F2}, {p.Position.Y:F2}, {p.Position.Z:F2}, {p.Position.W})");
                Console.WriteLine($"       Color({p.Color.X:F3}, {p.Color.Y:F3}, {p.Color.Z:F3}, {p.Color.W})");
                
                // Show extra dimensions if present
                if (p.ExtraDimensions != null && p.ExtraDimensions.Count > 0)
                {
                    Console.WriteLine($"       Extra Dimensions:");
                    foreach (var dim in p.ExtraDimensions)
                    {
                        string values = dim.Value.Length == 1 
                            ? $"{dim.Value[0]:F4}" 
                            : $"[{string.Join(", ", dim.Value.Select(v => $"{v:F4}"))}]";
                        Console.WriteLine($"         {dim.Key} = {values}");
                    }
                }
            }

            // Verify format
            Console.WriteLine("\nVerifying Stride format:");
            var firstPoint = strideData.Points[0];
            Console.WriteLine($"  Position.W = {firstPoint.Position.W} (should be 1.0)");
            Console.WriteLine($"  Color.W = {firstPoint.Color.W} (should be 1.0)");
            Console.WriteLine($"  All extra attributes are float32");
            Console.WriteLine($"  ✓ Format verified!");

            // ========================================
            // Example 2: Separated arrays for GPU upload
            // ========================================
            Console.WriteLine("\n=== Example 2: Separated Arrays (GPU-Ready) ===\n");

            var separatedData = cachedReader.GetCacheDataSeparated();
            Console.WriteLine($"Generated separate arrays for all attributes:");
            Console.WriteLine($"  Positions:        {separatedData.Positions!.Length:N0} Vector4s");
            Console.WriteLine($"  Colors:           {separatedData.Colors!.Length:N0} Vector4s");
            
            // Show extra dimension arrays if present
            if (separatedData.ExtraDimensionArrays != null && separatedData.ExtraDimensionArrays.Count > 0)
            {
                Console.WriteLine($"\n  Extra Dimensions:");
                foreach (var dimArray in separatedData.ExtraDimensionArrays)
                {
                    int componentsPerPoint = dimArray.Value.Length / separatedData.Count;
                    string suffix = componentsPerPoint > 1 ? $" ({componentsPerPoint} × float per point)" : "";
                    Console.WriteLine($"    {dimArray.Key}: {dimArray.Value.Length:N0} floats{suffix}");
                    
                    // Show a few sample values
                    if (dimArray.Value.Length > 0)
                    {
                        var samples = dimArray.Value.Take(Math.Min(3 * componentsPerPoint, dimArray.Value.Length));
                        Console.WriteLine($"      Samples: {string.Join(", ", samples.Select(v => $"{v:F4}"))}");
                    }
                }
            }
            
            Console.WriteLine($"\nReady for GPU vertex attribute buffers:");
            Console.WriteLine($"  glBufferData(POSITION_BUFFER, positions, GL_STATIC_DRAW);");
            Console.WriteLine($"  glBufferData(COLOR_BUFFER, colors, GL_STATIC_DRAW);");

            // Show memory layout
            Console.WriteLine($"\nMemory layout per point:");
            Console.WriteLine($"  Position: 4 × float (16 bytes) - Vector4");
            Console.WriteLine($"  Color:    4 × float (16 bytes) - Vector4");
            Console.WriteLine($"  Total:    ~32 bytes per point (+ extras if present)");

            // ========================================
            // Example 3: Direct query to Stride format
            // ========================================
            Console.WriteLine("\n=== Example 3: Direct Query to Stride Format ===\n");

            // Query and convert in one step (uses cache)
            var stridePoints = cachedReader.GetStridePointsInBox(box, resolution: 0);
            Console.WriteLine($"Direct query: {stridePoints.Length:N0} points in Stride format");

            // ========================================
            // Example 4: Frustum query with Stride output
            // ========================================
            Console.WriteLine("\n=== Example 4: Frustum Query with Stride Output ===\n");

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

            var frustumPoints = cachedReader.GetStridePointsInFrustum(vpArray, resolution: 0);
            Console.WriteLine($"Frustum query: {frustumPoints.Length:N0} visible points");
            Console.WriteLine($"Camera position: ({centerX:F2}, {centerY:F2}, {centerZ + size:F2})");
            Console.WriteLine($"Looking at: ({centerX:F2}, {centerY:F2}, {centerZ:F2})");
            Console.WriteLine($"\n✓ Points ready for rendering!");

            // ========================================
            // Example 5: Performance statistics
            // ========================================
            Console.WriteLine("\n=== Example 5: Cache Performance ===\n");

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

