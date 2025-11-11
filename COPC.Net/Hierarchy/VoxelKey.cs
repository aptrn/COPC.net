using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Represents a key for a voxel in the COPC hierarchy.
    /// The key consists of depth (d) and 3D coordinates (x, y, z).
    /// </summary>
    public struct VoxelKey : IEquatable<VoxelKey>
    {
        public int D { get; set; }  // Depth level
        public int X { get; set; }  // X coordinate
        public int Y { get; set; }  // Y coordinate
        public int Z { get; set; }  // Z coordinate

        public VoxelKey(int d, int x, int y, int z)
        {
            D = d;
            X = x;
            Y = y;
            Z = z;
        }

        public static VoxelKey InvalidKey() => new VoxelKey(-1, -1, -1, -1);
        public static VoxelKey RootKey() => new VoxelKey(0, 0, 0, 0);

        public bool IsValid() => D >= 0 && X >= 0 && Y >= 0 && Z >= 0;

        /// <summary>
        /// Returns the string representation "D-X-Y-Z" used as a key in dictionaries.
        /// </summary>
        public override string ToString()
        {
            return $"{D}-{X}-{Y}-{Z}";
        }

        /// <summary>
        /// Creates a VoxelKey from a string in format "D-X-Y-Z".
        /// </summary>
        public static VoxelKey FromString(string key)
        {
            var parts = key.Split('-');
            if (parts.Length != 4)
                throw new ArgumentException($"Invalid voxel key format: {key}");

            return new VoxelKey(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2]),
                int.Parse(parts[3])
            );
        }

        /// <summary>
        /// Returns the corresponding key depending on direction [0,7].
        /// Each direction represents one of the 8 octants.
        /// </summary>
        public VoxelKey Bisect(int direction)
        {
            if (direction < 0 || direction > 7)
                throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be between 0 and 7");

            int newD = D + 1;
            int newX = 2 * X + ((direction & 0x4) >> 2);
            int newY = 2 * Y + ((direction & 0x2) >> 1);
            int newZ = 2 * Z + (direction & 0x1);

            return new VoxelKey(newD, newX, newY, newZ);
        }

        /// <summary>
        /// Returns all 8 children of this voxel key.
        /// </summary>
        public List<VoxelKey> GetChildren()
        {
            var children = new List<VoxelKey>(8);
            for (int i = 0; i < 8; i++)
            {
                children.Add(Bisect(i));
            }
            return children;
        }

        /// <summary>
        /// Returns the hierarchical parent of this key.
        /// </summary>
        public VoxelKey GetParent()
        {
            if (D <= 0)
                return InvalidKey();

            return new VoxelKey(D - 1, X / 2, Y / 2, Z / 2);
        }

        /// <summary>
        /// Returns the hierarchical parent of this key at the requested depth.
        /// </summary>
        public VoxelKey GetParentAtDepth(int depth)
        {
            if (depth < 0 || depth > D)
                return InvalidKey();

            if (depth == D)
                return this;

            int levelDiff = D - depth;
            int divisor = 1 << levelDiff; // 2^levelDiff

            return new VoxelKey(depth, X / divisor, Y / divisor, Z / divisor);
        }

        /// <summary>
        /// Returns a list of the key's parents from the key to the root node.
        /// </summary>
        /// <param name="includeSelf">Whether to include the key itself in the list.</param>
        public List<VoxelKey> GetParents(bool includeSelf = false)
        {
            var parents = new List<VoxelKey>();
            
            if (includeSelf)
                parents.Add(this);

            var current = this;
            while (current.D > 0)
            {
                current = current.GetParent();
                if (current.IsValid())
                    parents.Add(current);
            }

            return parents;
        }

        /// <summary>
        /// Tests whether the current key is a child of a given parent key.
        /// </summary>
        public bool ChildOf(VoxelKey parentKey)
        {
            if (D <= parentKey.D)
                return false;

            var ancestor = GetParentAtDepth(parentKey.D);
            return ancestor == parentKey;
        }

        /// <summary>
        /// Calculates the bounding box for this voxel given header information.
        /// </summary>
        public BoundingBox GetBounds(LasHeader header, CopcInfo copcInfo)
        {
            double span = GetSpanAtDepth(D, header);
            
            // Calculate the voxel bounds based on the COPC cube
            var cubeCenter = new Vector3((float)copcInfo.CenterX, (float)copcInfo.CenterY, (float)copcInfo.CenterZ);
            var halfSize = (float)copcInfo.HalfSize;
            var cube = new BoundingBox(cubeCenter - new Vector3(halfSize), cubeCenter + new Vector3(halfSize));

            double minX = cube.Minimum.X + X * span;
            double minY = cube.Minimum.Y + Y * span;
            double minZ = cube.Minimum.Z + Z * span;

            var min = new Vector3((float)minX, (float)minY, (float)minZ);
            var max = new Vector3((float)(minX + span), (float)(minY + span), (float)(minZ + span));
            return new BoundingBox(min, max);
        }

        /// <summary>
        /// Returns the span (size) of a voxel at the given depth.
        /// </summary>
        public static double GetSpanAtDepth(int depth, LasHeader header)
        {
            double cubeSize = Math.Max(
                Math.Max(header.MaxX - header.MinX, header.MaxY - header.MinY),
                header.MaxZ - header.MinZ
            );
            
            return cubeSize / (1 << depth); // cubeSize / 2^depth
        }

        /// <summary>
        /// Returns the resolution (point spacing) at this depth level.
        /// </summary>
        public double GetResolution(LasHeader header, CopcInfo copcInfo)
        {
            return GetResolutionAtDepth(D, header, copcInfo);
        }

        /// <summary>
        /// Returns the resolution at a given depth level.
        /// </summary>
        public static double GetResolutionAtDepth(int depth, LasHeader header, CopcInfo copcInfo)
        {
            return copcInfo.Spacing / (1 << depth); // spacing / 2^depth
        }

        public bool Equals(VoxelKey other)
        {
            return D == other.D && X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object? obj)
        {
            return obj is VoxelKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Use the same hash approach as the C++ implementation
            ulong k1 = ((ulong)D << 32) | (uint)X;
            ulong k2 = ((ulong)Y << 32) | (uint)Z;
            return HashCode.Combine(k1, k2);
        }

        public static bool operator ==(VoxelKey left, VoxelKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VoxelKey left, VoxelKey right)
        {
            return !left.Equals(right);
        }
    }
}

