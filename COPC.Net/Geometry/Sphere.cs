using System;
using StrideBoundingBox = Stride.Core.Mathematics.BoundingBox;
using StrideBoundingSphere = Stride.Core.Mathematics.BoundingSphere;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace Copc.Geometry
{
    /// <summary>
    /// Represents a sphere in 3D space defined by a center point and radius.
    /// Used for radius-based spatial queries (omnidirectional point retrieval).
    /// </summary>
    public struct Sphere : IEquatable<Sphere>
    {
        /// <summary>
        /// The center point of the sphere.
        /// </summary>
        public Vector3 Center { get; set; }

        /// <summary>
        /// The radius of the sphere.
        /// </summary>
        public double Radius { get; set; }

        public Sphere(Vector3 center, double radius)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be non-negative");

            Center = center;
            Radius = radius;
        }

        public Sphere(double centerX, double centerY, double centerZ, double radius)
            : this(new Vector3(centerX, centerY, centerZ), radius)
        {
        }

        /// <summary>
        /// Tests if the sphere contains a point.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            return DistanceSquaredToPoint(point) <= Radius * Radius;
        }

        /// <summary>
        /// Calculates the squared distance from the sphere's center to a point.
        /// Using squared distance avoids expensive square root operations.
        /// </summary>
        public double DistanceSquaredToPoint(Vector3 point)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;
            double dz = point.Z - Center.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Calculates the distance from the sphere's center to a point.
        /// </summary>
        public double DistanceToPoint(Vector3 point)
        {
            return Math.Sqrt(DistanceSquaredToPoint(point));
        }

        /// <summary>
        /// Tests if the sphere intersects with an axis-aligned bounding box.
        /// Returns true if the sphere overlaps or is contained within the box.
        /// Uses Arvo's algorithm for efficient sphere-box intersection testing.
        /// </summary>
        public bool IntersectsBox(Box box)
        {
            // Prefer Stride implementation
            StrideBoundingSphere s = ToStride();
            StrideBoundingBox b = box.ToStride();
            return s.Intersects(b);
        }

        /// <summary>
        /// Tests if the sphere completely contains a bounding box.
        /// </summary>
        public bool Contains(Box box)
        {
            // For the sphere to contain the box, all 8 corners must be inside the sphere
            // We can optimize by checking if the farthest corner is within the sphere
            
            // Find the corner of the box that is farthest from the sphere center
            double farthestX = Math.Abs(box.MaxX - Center.X) > Math.Abs(box.MinX - Center.X) ? box.MaxX : box.MinX;
            double farthestY = Math.Abs(box.MaxY - Center.Y) > Math.Abs(box.MinY - Center.Y) ? box.MaxY : box.MinY;
            double farthestZ = Math.Abs(box.MaxZ - Center.Z) > Math.Abs(box.MinZ - Center.Z) ? box.MaxZ : box.MinZ;

            Vector3 farthestCorner = new Vector3(farthestX, farthestY, farthestZ);
            return Contains(farthestCorner);
        }

        /// <summary>
        /// Tests if the sphere is completely within a bounding box.
        /// </summary>
        public bool Within(Box box)
        {
            // The sphere is within the box if:
            // 1. The center is inside the box
            // 2. The sphere doesn't extend beyond any face of the box
            
            return Center.X - Radius >= box.MinX && Center.X + Radius <= box.MaxX &&
                   Center.Y - Radius >= box.MinY && Center.Y + Radius <= box.MaxY &&
                   Center.Z - Radius >= box.MinZ && Center.Z + Radius <= box.MaxZ;
        }

        /// <summary>
        /// Gets the bounding box that contains this sphere.
        /// </summary>
        public Box GetBoundingBox()
        {
            return new Box(
                Center.X - Radius, Center.Y - Radius, Center.Z - Radius,
                Center.X + Radius, Center.Y + Radius, Center.Z + Radius
            );
        }

        public bool Equals(Sphere other)
        {
            return Center.Equals(other.Center) && Radius == other.Radius;
        }

        public override bool Equals(object? obj)
        {
            return obj is Sphere other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Center, Radius);
        }

        public static bool operator ==(Sphere left, Sphere right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Sphere left, Sphere right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Sphere[Center={Center}, Radius={Radius}]";
        }

        // Stride interop

        /// <summary>
        /// Converts this sphere to a Stride BoundingSphere.
        /// </summary>
        public StrideBoundingSphere ToStride()
        {
            return new StrideBoundingSphere(new StrideVector3((float)Center.X, (float)Center.Y, (float)Center.Z), (float)Radius);
        }

        /// <summary>
        /// Creates a Sphere from a Stride BoundingSphere.
        /// </summary>
        public static Sphere FromStride(StrideBoundingSphere s)
        {
            return new Sphere(new Vector3(s.Center.X, s.Center.Y, s.Center.Z), s.Radius);
        }
    }
}

