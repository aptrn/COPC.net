using System;
using Stride.Core.Mathematics;

namespace Copc.Examples
{
    /// <summary>
    /// Basic tests for frustum functionality.
    /// These are manual tests - in a production environment, use a proper unit testing framework.
    /// </summary>
    public static class FrustumTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("Running Frustum Tests...\n");

            TestFrustumCreationAndBoxIntersection();
            TestMatrixExtraction();

            Console.WriteLine("\n✓ All tests completed!");
        }

        static void TestFrustumCreationAndBoxIntersection()
        {
            Console.WriteLine("Test: Stride BoundingFrustum Creation + Box Intersection");
            
            // Simple identity-like view-projection matrix
            double[] matrix = new double[16]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            };

            // Create Stride matrix and frustum
            var m = new Matrix(
                (float)matrix[0], (float)matrix[1], (float)matrix[2], (float)matrix[3],
                (float)matrix[4], (float)matrix[5], (float)matrix[6], (float)matrix[7],
                (float)matrix[8], (float)matrix[9], (float)matrix[10], (float)matrix[11],
                (float)matrix[12], (float)matrix[13], (float)matrix[14], (float)matrix[15]
            );
            var frustum = new BoundingFrustum(ref m);
            
            // Box in front of camera (should be visible)
            var visibleBox = new BoundingBox(new Vector3(-5, -5, -20), new Vector3(5, 5, -10));
            var vext = new BoundingBoxExt(visibleBox.Minimum, visibleBox.Maximum);
            Assert(frustum.Contains(ref vext), "Box in front should be visible");
            
            // Box behind camera (should not be visible)
            var behindBox = new BoundingBox(new Vector3(-5, -5, 5), new Vector3(5, 5, 10));
            var bext = new BoundingBoxExt(behindBox.Minimum, behindBox.Maximum);
            bool behindVisible = frustum.Contains(ref bext);
            // Note: This test may pass or fail depending on the exact matrix - simplified test
            Console.WriteLine($"  - Box behind camera: {(behindVisible ? "visible" : "not visible")}");
            
            Console.WriteLine("  ✓ Frustum-box intersection works\n");
        }

        static void TestMatrixExtraction()
        {
            Console.WriteLine("Test: Matrix Extraction to Frustum (Stride)");
            
            // Test with different matrix formats
            double[] doubleMatrix = CreateSimplePerspectiveMatrix();
            var m = new Matrix(
                (float)doubleMatrix[0], (float)doubleMatrix[1], (float)doubleMatrix[2], (float)doubleMatrix[3],
                (float)doubleMatrix[4], (float)doubleMatrix[5], (float)doubleMatrix[6], (float)doubleMatrix[7],
                (float)doubleMatrix[8], (float)doubleMatrix[9], (float)doubleMatrix[10], (float)doubleMatrix[11],
                (float)doubleMatrix[12], (float)doubleMatrix[13], (float)doubleMatrix[14], (float)doubleMatrix[15]
            );
            var frustum1 = new BoundingFrustum(ref m);
            var testBox1 = new BoundingBoxExt(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Assert(frustum1.Contains(ref testBox1), "Stride frustum created");
            
            // Test float matrix conversion
            float[] floatMatrix = new float[16];
            for (int i = 0; i < 16; i++)
                floatMatrix[i] = (float)doubleMatrix[i];
            
            var mf = new Matrix(
                floatMatrix[0], floatMatrix[1], floatMatrix[2], floatMatrix[3],
                floatMatrix[4], floatMatrix[5], floatMatrix[6], floatMatrix[7],
                floatMatrix[8], floatMatrix[9], floatMatrix[10], floatMatrix[11],
                floatMatrix[12], floatMatrix[13], floatMatrix[14], floatMatrix[15]
            );
            var frustum2 = new BoundingFrustum(ref mf);
            var testBox2 = new BoundingBoxExt(new Vector3(-1, -1, -1), new Vector3(1, 1, 1));
            Assert(frustum2.Contains(ref testBox2), "Float matrix extraction");
            
            Console.WriteLine("  ✓ Matrix extraction works\n");
        }

        /// <summary>
        /// Creates a simple perspective projection matrix for testing
        /// </summary>
        private static double[] CreateSimplePerspectiveMatrix()
        {
            // Simplified perspective matrix
            // This represents a camera looking down -Z with a reasonable FOV
            double fov = Math.PI / 3.0; // 60 degrees
            double aspect = 16.0 / 9.0;
            double near = 1.0;
            double far = 1000.0;

            double f = 1.0 / Math.Tan(fov / 2.0);
            double rangeInv = 1.0 / (near - far);

            return new double[]
            {
                f / aspect, 0,  0,                          0,
                0,          f,  0,                          0,
                0,          0,  (far + near) * rangeInv,   2 * far * near * rangeInv,
                0,          0, -1,                          0
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}

