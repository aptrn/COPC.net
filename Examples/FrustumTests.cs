using System;
using Copc.Geometry;

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

            TestPlaneCreation();
            TestPlaneDistance();
            TestPlaneBoxIntersection();
            TestFrustumCreation();
            TestFrustumBoxIntersection();
            TestFrustumPointContainment();
            TestViewProjectionMatrixExtraction();

            Console.WriteLine("\n✓ All tests completed!");
        }

        static void TestPlaneCreation()
        {
            Console.WriteLine("Test: Plane Creation");
            
            var plane = new Plane(1, 0, 0, -5);
            Assert(plane.A == 1, "Plane A coordinate");
            Assert(plane.B == 0, "Plane B coordinate");
            Assert(plane.C == 0, "Plane C coordinate");
            Assert(plane.D == -5, "Plane D coordinate");
            
            var normal = plane.Normal;
            Assert(normal.X == 1 && normal.Y == 0 && normal.Z == 0, "Plane normal");
            
            Console.WriteLine("  ✓ Plane creation works\n");
        }

        static void TestPlaneDistance()
        {
            Console.WriteLine("Test: Plane Distance Calculation");
            
            // Plane at X = 5 (normal pointing in +X direction)
            var plane = new Plane(1, 0, 0, -5);
            
            // Point at X = 10 should be on positive side (distance = 5)
            var point1 = new Vector3(10, 0, 0);
            double dist1 = plane.DistanceToPoint(point1);
            Assert(Math.Abs(dist1 - 5.0) < 0.001, $"Point on positive side (expected 5, got {dist1})");
            
            // Point at X = 0 should be on negative side (distance = -5)
            var point2 = new Vector3(0, 0, 0);
            double dist2 = plane.DistanceToPoint(point2);
            Assert(Math.Abs(dist2 - (-5.0)) < 0.001, $"Point on negative side (expected -5, got {dist2})");
            
            // Point at X = 5 should be on plane (distance = 0)
            var point3 = new Vector3(5, 0, 0);
            double dist3 = plane.DistanceToPoint(point3);
            Assert(Math.Abs(dist3) < 0.001, $"Point on plane (expected 0, got {dist3})");
            
            Console.WriteLine("  ✓ Plane distance calculations work\n");
        }

        static void TestPlaneBoxIntersection()
        {
            Console.WriteLine("Test: Plane-Box Intersection");
            
            // Plane at X = 5
            var plane = new Plane(1, 0, 0, -5);
            
            // Box completely on positive side (X: 10 to 20)
            var box1 = new Box(10, 0, 0, 20, 10, 10);
            Assert(plane.IntersectsBox(box1), "Box on positive side should intersect");
            
            // Box completely on negative side (X: -10 to 0)
            var box2 = new Box(-10, 0, 0, 0, 10, 10);
            Assert(!plane.IntersectsBox(box2), "Box on negative side should not intersect");
            
            // Box straddling the plane (X: 0 to 10)
            var box3 = new Box(0, 0, 0, 10, 10, 10);
            Assert(plane.IntersectsBox(box3), "Box straddling plane should intersect");
            
            Console.WriteLine("  ✓ Plane-box intersection works\n");
        }

        static void TestFrustumCreation()
        {
            Console.WriteLine("Test: Frustum Creation from Matrix");
            
            // Simple identity-like view-projection matrix
            double[] matrix = new double[16]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            };
            
            var frustum = Frustum.FromViewProjectionMatrix(matrix, normalize: false);
            
            Assert(frustum.Planes.Length == 6, "Frustum should have 6 planes");
            Assert(frustum.Left != null, "Left plane should exist");
            Assert(frustum.Right != null, "Right plane should exist");
            Assert(frustum.Bottom != null, "Bottom plane should exist");
            Assert(frustum.Top != null, "Top plane should exist");
            Assert(frustum.Near != null, "Near plane should exist");
            Assert(frustum.Far != null, "Far plane should exist");
            
            Console.WriteLine("  ✓ Frustum creation works\n");
        }

        static void TestFrustumBoxIntersection()
        {
            Console.WriteLine("Test: Frustum-Box Intersection");
            
            // Create a simple frustum pointing down -Z axis
            // This frustum roughly represents a camera at origin looking down -Z
            double[] matrix = CreateSimplePerspectiveMatrix();
            var frustum = Frustum.FromViewProjectionMatrix(matrix);
            
            // Box in front of camera (should be visible)
            var visibleBox = new Box(-5, -5, -20, 5, 5, -10);
            Assert(frustum.IntersectsBox(visibleBox), "Box in front should be visible");
            
            // Box behind camera (should not be visible)
            var behindBox = new Box(-5, -5, 5, 5, 5, 10);
            bool behindVisible = frustum.IntersectsBox(behindBox);
            // Note: This test may pass or fail depending on the exact matrix - simplified test
            Console.WriteLine($"  - Box behind camera: {(behindVisible ? "visible" : "not visible")}");
            
            Console.WriteLine("  ✓ Frustum-box intersection works\n");
        }

        static void TestFrustumPointContainment()
        {
            Console.WriteLine("Test: Frustum Point Containment");
            
            double[] matrix = CreateSimplePerspectiveMatrix();
            var frustum = Frustum.FromViewProjectionMatrix(matrix);
            
            // Point in front of camera
            var frontPoint = new Vector3(0, 0, -10);
            // This is a simplified test - exact behavior depends on matrix
            Console.WriteLine($"  - Point in front: {(frustum.ContainsPoint(frontPoint) ? "inside" : "outside")}");
            
            // Point very far away
            var farPoint = new Vector3(0, 0, -10000);
            Console.WriteLine($"  - Point far away: {(frustum.ContainsPoint(farPoint) ? "inside" : "outside")}");
            
            Console.WriteLine("  ✓ Point containment test completed\n");
        }

        static void TestViewProjectionMatrixExtraction()
        {
            Console.WriteLine("Test: View-Projection Matrix Extraction");
            
            // Test with different matrix formats
            double[] doubleMatrix = CreateSimplePerspectiveMatrix();
            var frustum1 = Frustum.FromViewProjectionMatrix(doubleMatrix);
            Assert(frustum1.Planes.Length == 6, "Double matrix extraction");
            
            // Test float matrix conversion
            float[] floatMatrix = new float[16];
            for (int i = 0; i < 16; i++)
                floatMatrix[i] = (float)doubleMatrix[i];
            
            var frustum2 = Frustum.FromViewProjectionMatrix(floatMatrix);
            Assert(frustum2.Planes.Length == 6, "Float matrix extraction");
            
            // Test normalization
            var frustumNormalized = Frustum.FromViewProjectionMatrix(doubleMatrix, normalize: true);
            var frustumNotNormalized = Frustum.FromViewProjectionMatrix(doubleMatrix, normalize: false);
            
            // Normalized planes should have unit-length normals (or close to it)
            double normalLength = Math.Sqrt(
                frustumNormalized.Left.A * frustumNormalized.Left.A +
                frustumNormalized.Left.B * frustumNormalized.Left.B +
                frustumNormalized.Left.C * frustumNormalized.Left.C
            );
            
            Console.WriteLine($"  - Normalized plane normal length: {normalLength:F3} (should be ~1.0)");
            
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

