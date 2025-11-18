using System;
using StrideVector3 = Stride.Core.Mathematics.Vector3;

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

    /// <summary>
    /// Calculates the squared distance between this vector and another.
    /// Using squared distance avoids expensive square root operations.
    /// </summary>
    public double DistanceSquaredTo(Vector3 other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Calculates the distance between this vector and another.
    /// </summary>
    public double DistanceTo(Vector3 other)
    {
        return Math.Sqrt(DistanceSquaredTo(other));
    }

    /// <summary>
    /// Returns the length (magnitude) of this vector.
    /// </summary>
    public double Length()
    {
        return Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    /// <summary>
    /// Returns the squared length of this vector.
    /// </summary>
    public double LengthSquared()
    {
        return X * X + Y * Y + Z * Z;
    }

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

        // Stride interop

        /// <summary>
        /// Creates a Vector3 from a Stride Vector3.
        /// </summary>
        public Vector3(StrideVector3 v)
        {
            X = v.X;
            Y = v.Y;
            Z = v.Z;
        }

        /// <summary>
        /// Implicit conversion to Stride Vector3 (casts to float).
        /// </summary>
        public static implicit operator StrideVector3(Vector3 v)
        {
            return new StrideVector3((float)v.X, (float)v.Y, (float)v.Z);
        }

        /// <summary>
        /// Implicit conversion from Stride Vector3.
        /// </summary>
        public static implicit operator Vector3(StrideVector3 v)
        {
            return new Vector3(v);
        }
    }
}

