using System;

namespace Copc.Geometry
{
    /// <summary>
    /// Represents a view frustum defined by 6 planes (left, right, bottom, top, near, far).
    /// Used for spatial culling of point cloud nodes based on camera view.
    /// </summary>
    public class Frustum
    {
        /// <summary>
        /// The six frustum planes in the order: Left, Right, Bottom, Top, Near, Far
        /// </summary>
        public Plane[] Planes { get; private set; }

        public Plane Left => Planes[0];
        public Plane Right => Planes[1];
        public Plane Bottom => Planes[2];
        public Plane Top => Planes[3];
        public Plane Near => Planes[4];
        public Plane Far => Planes[5];

        /// <summary>
        /// Creates a frustum with the specified planes.
        /// </summary>
        public Frustum(Plane left, Plane right, Plane bottom, Plane top, Plane near, Plane far)
        {
            Planes = new Plane[6] { left, right, bottom, top, near, far };
        }

        /// <summary>
        /// Creates a frustum from a 4x4 view-projection matrix.
        /// The matrix should be in row-major order: [m00, m01, m02, m03, m10, m11, ...]
        /// </summary>
        /// <param name="viewProjectionMatrix">The combined view-projection matrix as a 16-element array in row-major order</param>
        /// <param name="normalize">Whether to normalize the plane equations (recommended for accurate distance calculations)</param>
        public static Frustum FromViewProjectionMatrix(double[] viewProjectionMatrix, bool normalize = true)
        {
            if (viewProjectionMatrix.Length != 16)
                throw new ArgumentException("View-projection matrix must have 16 elements (4x4 matrix)", nameof(viewProjectionMatrix));

            // Extract matrix elements (row-major order)
            double m00 = viewProjectionMatrix[0], m01 = viewProjectionMatrix[1], m02 = viewProjectionMatrix[2], m03 = viewProjectionMatrix[3];
            double m10 = viewProjectionMatrix[4], m11 = viewProjectionMatrix[5], m12 = viewProjectionMatrix[6], m13 = viewProjectionMatrix[7];
            double m20 = viewProjectionMatrix[8], m21 = viewProjectionMatrix[9], m22 = viewProjectionMatrix[10], m23 = viewProjectionMatrix[11];
            double m30 = viewProjectionMatrix[12], m31 = viewProjectionMatrix[13], m32 = viewProjectionMatrix[14], m33 = viewProjectionMatrix[15];

            // Extract frustum planes using the Gribb-Hartmann method
            // Each plane is extracted by adding/subtracting rows of the VP matrix

            // Left plane: m3 + m0
            Plane left = new Plane(m30 + m00, m31 + m01, m32 + m02, m33 + m03);

            // Right plane: m3 - m0
            Plane right = new Plane(m30 - m00, m31 - m01, m32 - m02, m33 - m03);

            // Bottom plane: m3 + m1
            Plane bottom = new Plane(m30 + m10, m31 + m11, m32 + m12, m33 + m13);

            // Top plane: m3 - m1
            Plane top = new Plane(m30 - m10, m31 - m11, m32 - m12, m33 - m13);

            // Near plane: m3 + m2
            Plane near = new Plane(m30 + m20, m31 + m21, m32 + m22, m33 + m23);

            // Far plane: m3 - m2
            Plane far = new Plane(m30 - m20, m31 - m21, m32 - m22, m33 - m23);

            // Normalize planes for accurate distance calculations
            if (normalize)
            {
                left = left.Normalize();
                right = right.Normalize();
                bottom = bottom.Normalize();
                top = top.Normalize();
                near = near.Normalize();
                far = far.Normalize();
            }

            return new Frustum(left, right, bottom, top, near, far);
        }

        /// <summary>
        /// Creates a frustum from a 4x4 view-projection matrix.
        /// This overload accepts float arrays for compatibility with graphics APIs.
        /// </summary>
        public static Frustum FromViewProjectionMatrix(float[] viewProjectionMatrix, bool normalize = true)
        {
            if (viewProjectionMatrix.Length != 16)
                throw new ArgumentException("View-projection matrix must have 16 elements (4x4 matrix)", nameof(viewProjectionMatrix));

            // Convert float array to double array
            double[] doubleMatrix = new double[16];
            for (int i = 0; i < 16; i++)
                doubleMatrix[i] = viewProjectionMatrix[i];

            return FromViewProjectionMatrix(doubleMatrix, normalize);
        }

        /// <summary>
        /// Tests if a bounding box intersects with the frustum.
        /// Returns true if the box is partially or fully inside the frustum.
        /// </summary>
        public bool IntersectsBox(Box box)
        {
            // A box is inside the frustum if it's on the positive side of all 6 planes
            // (or at least partially intersecting each plane)
            for (int i = 0; i < 6; i++)
            {
                if (!Planes[i].IntersectsBox(box))
                {
                    // Box is completely outside this plane, so outside the frustum
                    return false;
                }
            }

            // Box passed all plane tests - it's at least partially inside
            return true;
        }

        /// <summary>
        /// Tests if a point is inside the frustum.
        /// </summary>
        public bool ContainsPoint(Vector3 point)
        {
            // Point is inside if it's on the positive side of all planes
            for (int i = 0; i < 6; i++)
            {
                if (Planes[i].DistanceToPoint(point) < 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Creates a frustum from separate view and projection matrices.
        /// </summary>
        public static Frustum FromViewAndProjection(double[] viewMatrix, double[] projectionMatrix, bool normalize = true)
        {
            if (viewMatrix.Length != 16)
                throw new ArgumentException("View matrix must have 16 elements", nameof(viewMatrix));
            if (projectionMatrix.Length != 16)
                throw new ArgumentException("Projection matrix must have 16 elements", nameof(projectionMatrix));

            // Multiply projection * view to get view-projection matrix
            double[] vpMatrix = MultiplyMatrices(projectionMatrix, viewMatrix);
            return FromViewProjectionMatrix(vpMatrix, normalize);
        }

        /// <summary>
        /// Multiplies two 4x4 matrices (A * B) in row-major order.
        /// </summary>
        private static double[] MultiplyMatrices(double[] a, double[] b)
        {
            double[] result = new double[16];

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    double sum = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += a[row * 4 + k] * b[k * 4 + col];
                    }
                    result[row * 4 + col] = sum;
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"Frustum[Left={Left}, Right={Right}, Bottom={Bottom}, Top={Top}, Near={Near}, Far={Far}]";
        }
    }
}

