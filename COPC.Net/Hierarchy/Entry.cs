using System;
using System.IO;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Base class for Node and Page objects in the COPC hierarchy.
    /// An entry contains a key, offset, byte size, and point count.
    /// If point_count is -1, it's a Page; otherwise it's a Node.
    /// </summary>
    public class Entry
    {
        public const int EntrySize = 32; // Size in bytes

        public VoxelKey Key { get; set; }
        public long Offset { get; set; }
        public int ByteSize { get; set; }
        public int PointCount { get; set; }

        public Entry()
        {
            Key = VoxelKey.InvalidKey();
            Offset = -1;
            ByteSize = -1;
            PointCount = -1;
        }

        public Entry(VoxelKey key, long offset, int byteSize, int pointCount)
        {
            Key = key;
            Offset = offset;
            ByteSize = byteSize;
            PointCount = pointCount;
        }

        public virtual bool IsValid()
        {
            return Offset >= 0 && ByteSize >= 0 && Key.IsValid();
        }

        public virtual bool IsPage()
        {
            return IsValid() && PointCount == -1;
        }

        public override string ToString()
        {
            return $"Entry {Key}: off={Offset}, size={ByteSize}, count={PointCount}, valid={IsValid()}";
        }

        /// <summary>
        /// Packs the entry into binary format for writing to a COPC file.
        /// </summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(Key.D);
            writer.Write(Key.X);
            writer.Write(Key.Y);
            writer.Write(Key.Z);
            writer.Write((ulong)Offset);
            writer.Write(ByteSize);
            writer.Write(PointCount);
        }

        /// <summary>
        /// Unpacks an entry from binary format when reading from a COPC file.
        /// </summary>
        public static Entry Unpack(BinaryReader reader)
        {
            int d = reader.ReadInt32();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int z = reader.ReadInt32();
            long offset = (long)reader.ReadUInt64();
            int byteSize = reader.ReadInt32();
            int pointCount = reader.ReadInt32();

            var key = new VoxelKey(d, x, y, z);
            return new Entry(key, offset, byteSize, pointCount);
        }

        protected bool IsEqual(Entry other)
        {
            return Key == other.Key &&
                   Offset == other.Offset &&
                   ByteSize == other.ByteSize &&
                   PointCount == other.PointCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is Entry other && IsEqual(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Offset, ByteSize, PointCount);
        }
    }
}

