using System;
using StridePlane = Stride.Core.Mathematics.Plane;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace Copc.Geometry
{
    /// <summary>
    /// Represents a plane in 3D space defined by the equation: Ax + By + Cz + D = 0
    /// The normal vector is (A, B, C).
    /// </summary>
    public struct Plane : IEquatable<Plane>
    {
        /// <summary>
        /// The A coefficient (X component of normal).
        /// </summary>
        public double A { get; set; }

        /// <summary>
        /// The B coefficient (Y component of normal).
        /// </summary>
        public double B { get; set; }

        /// <summary>
        /// The C coefficient (Z component of normal).
        /// </summary>
        public double C { get; set; }

        /// <summary>
        /// The D coefficient (distance from origin along normal).
        /// </summary>
        public double D { get; set; }

        public Plane(double a, double b, double c, double d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        /// <summary>
        /// Creates a plane from a normal vector and a point on the plane.
        /// </summary>
        public static Plane FromNormalAndPoint(Vector3 normal, Vector3 point)
        {
            double d = -(normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z);
            return new Plane(normal.X, normal.Y, normal.Z, d);
        }

        /// <summary>
        /// Gets the normal vector of the plane.
        /// </summary>
        public Vector3 Normal => new Vector3(A, B, C);

        /// <summary>
        /// Normalizes the plane equation so that the normal has unit length.
        /// </summary>
        public Plane Normalize()
        {
            double magnitude = Math.Sqrt(A * A + B * B + C * C);
            if (magnitude < 1e-10)
                return this;

            return new Plane(A / magnitude, B / magnitude, C / magnitude, D / magnitude);
        }

        /// <summary>
        /// Calculates the signed distance from a point to the plane.
        /// Positive means the point is on the side the normal points to.
        /// </summary>
        public double DistanceToPoint(Vector3 point)
        {
            return A * point.X + B * point.Y + C * point.Z + D;
        }

        /// <summary>
        /// Tests if a point is on the positive side of the plane (in the direction of the normal).
        /// </summary>
        public bool IsPointOnPositiveSide(Vector3 point)
        {
            return DistanceToPoint(point) > 0;
        }

        /// <summary>
        /// Tests if a box intersects with or is on the positive side of the plane.
        /// This is used for frustum culling - returns false only if the box is completely
        /// on the negative side of the plane (outside the frustum).
        /// </summary>
        public bool IntersectsBox(Box box)
        {
            // Get the positive vertex (the corner furthest along the normal direction)
            Vector3 positiveVertex = new Vector3(
                A >= 0 ? box.MaxX : box.MinX,
                B >= 0 ? box.MaxY : box.MinY,
                C >= 0 ? box.MaxZ : box.MinZ
            );

            // If the positive vertex is on the negative side, the whole box is outside
            return DistanceToPoint(positiveVertex) >= 0;
        }

        public bool Equals(Plane other)
        {
            return A == other.A && B == other.B && C == other.C && D == other.D;
        }

        public override bool Equals(object? obj)
        {
            return obj is Plane other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(A, B, C, D);
        }

        public static bool operator ==(Plane left, Plane right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Plane left, Plane right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Plane({A:F3}x + {B:F3}y + {C:F3}z + {D:F3} = 0)";
        }

        // Stride interop

        /// <summary>
        /// Converts this plane to a Stride Plane.
        /// </summary>
        public StridePlane ToStride()
        {
            return new StridePlane(new StrideVector3((float)A, (float)B, (float)C), (float)D);
        }

        /// <summary>
        /// Creates a Plane from a Stride Plane.
        /// </summary>
        public static Plane FromStride(StridePlane p)
        {
            return new Plane(p.Normal.X, p.Normal.Y, p.Normal.Z, p.D);
        }
    }
}

