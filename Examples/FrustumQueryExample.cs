using System;
using System.Linq;
using Copc.Geometry;
using Copc.IO;

namespace Copc.Examples
{
    /// <summary>
    /// Example demonstrating how to query COPC nodes using a view frustum.
    /// This is useful for efficiently loading only the points visible from a camera perspective.
    /// </summary>
    public static class FrustumQueryExample
    {
        /// <summary>
        /// Basic example: Query nodes using a pre-computed view-projection matrix
        /// </summary>
        public static void BasicFrustumQuery(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Example view-projection matrix (row-major order)
            // In a real application, this would come from your camera/graphics system
            double[] viewProjectionMatrix = CreateExampleViewProjectionMatrix();

            // Query nodes that intersect with the frustum
            var nodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix);

            Console.WriteLine($"Found {nodes.Count} nodes intersecting the frustum");
            Console.WriteLine($"Total points: {nodes.Sum(n => (long)n.PointCount):N0}");

            // Decompress and process sample nodes
            var sample = nodes.Take(Math.Min(3, nodes.Count)).ToList();
            if (sample.Count > 0)
            {
                Console.WriteLine($"Decompressing {sample.Count} nodes...");
                var points = reader.GetPointsFromNodes(sample);
                Console.WriteLine($"Decompressed {points.Length:N0} points");
                int toShow = Math.Min(10, points.Length);
                for (int i = 0; i < toShow; i++)
                {
                    var p = points[i];
                    Console.WriteLine($"[{i,3}] X={p.X,12:F3} Y={p.Y,12:F3} Z={p.Z,12:F3} Intensity={p.Intensity,5} Class={p.Classification,3}");
                }
            }
        }

        /// <summary>
        /// Advanced example: Query with resolution filtering
        /// </summary>
        public static void FrustumQueryWithResolution(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Create view-projection matrix
            double[] viewProjectionMatrix = CreateExampleViewProjectionMatrix();

            // Query with a minimum resolution of 0.1 meters
            // This limits the level of detail to avoid loading too many points
            double targetResolution = 0.1;
            var nodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix, targetResolution);

            Console.WriteLine($"Found {nodes.Count} nodes at resolution <= {targetResolution}m");
            
            // Group by depth to see the hierarchy
            var byDepth = nodes.GroupBy(n => n.Key.D).OrderBy(g => g.Key);
            foreach (var group in byDepth)
            {
                double resolution = Copc.Hierarchy.VoxelKey.GetResolutionAtDepth(
                    group.Key, 
                    reader.Config.LasHeader, 
                    reader.Config.CopcInfo
                );
                Console.WriteLine($"  Depth {group.Key} (res: {resolution:F3}m): {group.Count()} nodes, {group.Sum(n => (long)n.PointCount):N0} points");
            }
        }

        /// <summary>
        /// Example using Frustum object directly for multiple queries
        /// </summary>
        public static void ReusableFrustumQuery(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Create frustum once and reuse it
            double[] viewProjectionMatrix = CreateExampleViewProjectionMatrix();
            var frustum = Frustum.FromViewProjectionMatrix(viewProjectionMatrix);

            Console.WriteLine("Frustum planes:");
            Console.WriteLine($"  Left:   {frustum.Left}");
            Console.WriteLine($"  Right:  {frustum.Right}");
            Console.WriteLine($"  Bottom: {frustum.Bottom}");
            Console.WriteLine($"  Top:    {frustum.Top}");
            Console.WriteLine($"  Near:   {frustum.Near}");
            Console.WriteLine($"  Far:    {frustum.Far}");

            // Query at different resolutions using the same frustum
            double[] resolutions = { 1.0, 0.5, 0.1 };
            
            foreach (double resolution in resolutions)
            {
                var nodes = reader.GetNodesIntersectFrustum(frustum, resolution);
                Console.WriteLine($"\nResolution {resolution}m: {nodes.Count} nodes, {nodes.Sum(n => (long)n.PointCount):N0} points");
            }
        }

        /// <summary>
        /// Example: Create frustum from separate view and projection matrices
        /// </summary>
        public static void SeparateViewProjectionMatrices(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // In real applications, these come from your camera system
            double[] viewMatrix = CreateExampleViewMatrix();
            double[] projectionMatrix = CreateExampleProjectionMatrix();

            // Combine them into a frustum
            var frustum = Frustum.FromViewAndProjection(viewMatrix, projectionMatrix);

            // Query nodes
            var nodes = reader.GetNodesIntersectFrustum(frustum);
            Console.WriteLine($"Found {nodes.Count} nodes visible from camera");
            var sample = nodes.Take(Math.Min(2, nodes.Count)).ToList();
            if (sample.Count > 0)
            {
                var points = reader.GetPointsFromNodes(sample);
                Console.WriteLine($"Decompressed {points.Length:N0} points from visible nodes");
            }
        }

        /// <summary>
        /// Example: Test if specific points/boxes are in frustum
        /// </summary>
        public static void TestFrustumIntersection()
        {
            double[] viewProjectionMatrix = CreateExampleViewProjectionMatrix();
            var frustum = Frustum.FromViewProjectionMatrix(viewProjectionMatrix);

            // Test a point
            var testPoint = new Vector3(100, 200, 50);
            bool pointInside = frustum.ContainsPoint(testPoint);
            Console.WriteLine($"Point {testPoint} is {(pointInside ? "inside" : "outside")} the frustum");

            // Test a bounding box
            var testBox = new Box(100, 200, 50, 110, 210, 60);
            bool boxIntersects = frustum.IntersectsBox(testBox);
            Console.WriteLine($"Box {testBox} {(boxIntersects ? "intersects" : "does not intersect")} the frustum");
        }

        /// <summary>
        /// Example: Working with float matrices (common in graphics APIs like Unity, OpenGL)
        /// </summary>
        public static void FloatMatrixExample(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Many graphics APIs use float matrices
            float[] viewProjectionMatrix = CreateExampleViewProjectionMatrixFloat();

            // The API supports float arrays directly
            var nodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix);
            Console.WriteLine($"Found {nodes.Count} nodes using float matrix");
        }

        #region Example Matrix Creation Methods

        /// <summary>
        /// Creates an example view-projection matrix for demonstration.
        /// In a real application, this would come from your camera/graphics system.
        /// 
        /// Matrix format: Row-major order
        /// [m00, m01, m02, m03]
        /// [m10, m11, m12, m13]
        /// [m20, m21, m22, m23]
        /// [m30, m31, m32, m33]
        /// </summary>
        private static double[] CreateExampleViewProjectionMatrix()
        {
            // This is a simplified example matrix
            // In reality, you would get this from your camera system
            return new double[]
            {
                // Example: Looking down negative Z axis with perspective projection
                1.0,  0.0,  0.0,  0.0,
                0.0,  1.0,  0.0,  0.0,
                0.0,  0.0, -1.0, -10.0,
                0.0,  0.0, -1.0,  0.0
            };
        }

        private static float[] CreateExampleViewProjectionMatrixFloat()
        {
            // Float version for graphics APIs
            return new float[]
            {
                1.0f,  0.0f,  0.0f,  0.0f,
                0.0f,  1.0f,  0.0f,  0.0f,
                0.0f,  0.0f, -1.0f, -10.0f,
                0.0f,  0.0f, -1.0f,  0.0f
            };
        }

        private static double[] CreateExampleViewMatrix()
        {
            // Example view matrix (camera transformation)
            return new double[]
            {
                1.0, 0.0, 0.0, 0.0,
                0.0, 1.0, 0.0, 0.0,
                0.0, 0.0, 1.0, -100.0,
                0.0, 0.0, 0.0, 1.0
            };
        }

        private static double[] CreateExampleProjectionMatrix()
        {
            // Example perspective projection matrix
            // FOV: 60 degrees, Aspect: 16:9, Near: 1.0, Far: 1000.0
            double fov = Math.PI / 3.0; // 60 degrees
            double aspect = 16.0 / 9.0;
            double near = 1.0;
            double far = 1000.0;

            double f = 1.0 / Math.Tan(fov / 2.0);
            
            return new double[]
            {
                f/aspect, 0.0,  0.0,                           0.0,
                0.0,      f,    0.0,                           0.0,
                0.0,      0.0,  (far+near)/(near-far),        (2*far*near)/(near-far),
                0.0,      0.0, -1.0,                           0.0
            };
        }

        #endregion

        #region Integration Examples with Graphics Systems

        /// <summary>
        /// Example showing how to integrate with a typical 3D graphics system
        /// </summary>
        public class GraphicsIntegrationExample
        {
            // This demonstrates the typical workflow:
            // 1. Get view-projection matrix from your camera/graphics system
            // 2. Query COPC nodes
            // 3. Load and render visible points

            public static void RenderVisiblePoints(string copcFilePath, double[] cameraViewProjectionMatrix)
            {
                using var reader = CopcReader.Open(copcFilePath);

                // Step 1: Query visible nodes based on camera frustum
                var visibleNodes = reader.GetNodesIntersectFrustum(cameraViewProjectionMatrix, resolution: 0.1);

                Console.WriteLine($"Rendering {visibleNodes.Count} visible node chunks");

                // Step 2: Load and process each visible node
                foreach (var node in visibleNodes)
                {
                    // Get the compressed point data
                    byte[] compressedData = reader.GetPointDataCompressed(node);
                    
                    // Get node bounds for additional culling/LOD decisions
                    var bounds = node.Key.GetBounds(reader.Config.LasHeader, reader.Config.CopcInfo);
                    
                    Console.WriteLine($"  Loading node {node.Key}: {node.PointCount} points, bounds: {bounds}");
                    
                    // Step 3: Decompress and render
                    // (In practice, you would decompress using PDAL or similar,
                    //  then upload to GPU for rendering)
                    // RenderPointCloud(compressedData, bounds);
                }
            }
        }

        #endregion
    }
}

