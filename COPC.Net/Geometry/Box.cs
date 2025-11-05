using System;

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
            return !(MaxX < other.MinX || MinX > other.MaxX ||
                     MaxY < other.MinY || MinY > other.MaxY ||
                     MaxZ < other.MinZ || MinZ > other.MaxZ);
        }

        /// <summary>
        /// Tests if this box crosses another box (intersects but not contained).
        /// </summary>
        public bool Crosses(Box other)
        {
            return Intersects(other) && !Within(other) && !Contains(other);
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
    }
}

