using System;

namespace Copc.Geometry
{
    /// <summary>
    /// Represents a 3D vector with X, Y, Z coordinates.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Default scale factor for LAS/COPC files (0.01 for cm precision).
        /// </summary>
        public static Vector3 DefaultScale() => new Vector3(0.01, 0.01, 0.01);

        /// <summary>
        /// Default offset for LAS/COPC files (0, 0, 0).
        /// </summary>
        public static Vector3 DefaultOffset() => new Vector3(0, 0, 0);

        public bool Equals(Vector3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object? obj)
        {
            return obj is Vector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(Vector3 left, Vector3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3 left, Vector3 right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }
}

