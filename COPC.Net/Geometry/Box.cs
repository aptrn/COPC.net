using System;
using StrideBoundingBox = Stride.Core.Mathematics.BoundingBox;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

namespace Copc.Geometry
{
    /// <summary>
    /// Represents an axis-aligned bounding box in 3D space.
    /// </summary>
    public struct Box : IEquatable<Box>
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }

        public Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        public Box(Vector3 min, Vector3 max)
        {
            MinX = min.X;
            MinY = min.Y;
            MinZ = min.Z;
            MaxX = max.X;
            MaxY = max.Y;
            MaxZ = max.Z;
        }

        /// <summary>
        /// Creates a box from a center point and half-size (radius).
        /// This is used for COPC cube definitions.
        /// </summary>
        public static Box FromCenterAndHalfSize(Vector3 center, double halfSize)
        {
            return new Box(
                center.X - halfSize, center.Y - halfSize, center.Z - halfSize,
                center.X + halfSize, center.Y + halfSize, center.Z + halfSize
            );
        }

        public Vector3 Min => new Vector3(MinX, MinY, MinZ);
        public Vector3 Max => new Vector3(MaxX, MaxY, MaxZ);
        public Vector3 Center => new Vector3((MinX + MaxX) / 2, (MinY + MaxY) / 2, (MinZ + MaxZ) / 2);

        /// <summary>
        /// Tests if this box contains a point.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            return point.X >= MinX && point.X <= MaxX &&
                   point.Y >= MinY && point.Y <= MaxY &&
                   point.Z >= MinZ && point.Z <= MaxZ;
        }

        /// <summary>
        /// Tests if this box contains another box entirely.
        /// </summary>
        public bool Contains(Box other)
        {
            return other.MinX >= MinX && other.MaxX <= MaxX &&
                   other.MinY >= MinY && other.MaxY <= MaxY &&
                   other.MinZ >= MinZ && other.MaxZ <= MaxZ;
        }

        /// <summary>
        /// Tests if this box is entirely within another box.
        /// </summary>
        public bool Within(Box other)
        {
            return MinX >= other.MinX && MaxX <= other.MaxX &&
                   MinY >= other.MinY && MaxY <= other.MaxY &&
                   MinZ >= other.MinZ && MaxZ <= other.MaxZ;
        }

        /// <summary>
        /// Tests if this box intersects with another box.
        /// </summary>
        public bool Intersects(Box other)
        {
            // Prefer Stride implementation for intersection testing
            var a = ToStride();
            var b = other.ToStride();
            return a.Intersects(b);
        }

    /// <summary>
    /// Tests if this box crosses another box (intersects but not contained).
    /// </summary>
    public bool Crosses(Box other)
    {
        return Intersects(other) && !Within(other) && !Contains(other);
    }

    /// <summary>
    /// Tests if this box intersects with a sphere.
    /// </summary>
    public bool IntersectsSphere(Sphere sphere)
    {
        return sphere.IntersectsBox(this);
    }

    /// <summary>
    /// Tests if this box is completely within a sphere.
    /// </summary>
    public bool WithinSphere(Sphere sphere)
    {
        return sphere.Contains(this);
    }

    public bool Equals(Box other)
        {
            return MinX == other.MinX && MinY == other.MinY && MinZ == other.MinZ &&
                   MaxX == other.MaxX && MaxY == other.MaxY && MaxZ == other.MaxZ;
        }

        public override bool Equals(object? obj)
        {
            return obj is Box other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);
        }

        public static bool operator ==(Box left, Box right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Box left, Box right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Box[({MinX}, {MinY}, {MinZ}) -> ({MaxX}, {MaxY}, {MaxZ})]";
        }

        // Stride interop

        /// <summary>
        /// Converts this box to a Stride BoundingBox.
        /// </summary>
        public StrideBoundingBox ToStride()
        {
            return new StrideBoundingBox(
                new StrideVector3((float)MinX, (float)MinY, (float)MinZ),
                new StrideVector3((float)MaxX, (float)MaxY, (float)MaxZ)
            );
        }

        /// <summary>
        /// Creates a Box from a Stride BoundingBox.
        /// </summary>
        public static Box FromStride(StrideBoundingBox bbox)
        {
            return new Box(
                bbox.Minimum.X, bbox.Minimum.Y, bbox.Minimum.Z,
                bbox.Maximum.X, bbox.Maximum.Y, bbox.Maximum.Z
            );
        }
    }
}

