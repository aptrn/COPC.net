using System;
using System.IO;
using Stride.Core.Mathematics;

namespace Copc
{
    /// <summary>
    /// Represents the COPC Info VLR data structure.
    /// This is stored in a Variable Length Record with User ID "copc" and Record ID 1.
    /// The VLR must be the first VLR after the LAS header block (at offset 375).
    /// </summary>
    public class CopcInfo
    {
        public const int VlrOffset = 375;      // COPC Info VLR must be at this offset
        public const int VlrSizeBytes = 160;   // COPC Info VLR payload is 160 bytes

        /// <summary>
        /// Center X coordinate of the COPC cube.
        /// </summary>
        public double CenterX { get; set; }

        /// <summary>
        /// Center Y coordinate of the COPC cube.
        /// </summary>
        public double CenterY { get; set; }

        /// <summary>
        /// Center Z coordinate of the COPC cube.
        /// </summary>
        public double CenterZ { get; set; }

        /// <summary>
        /// Half-size (radius) of the COPC cube.
        /// The cube extends from (center - halfsize) to (center + halfsize) in all dimensions.
        /// </summary>
        public double HalfSize { get; set; }

        /// <summary>
        /// The spacing at the root node level.
        /// Child nodes have spacing / 2.
        /// </summary>
        public double Spacing { get; set; }

        /// <summary>
        /// Offset to the root hierarchy page in the file.
        /// </summary>
        public ulong RootHierarchyOffset { get; set; }

        /// <summary>
        /// Size of the root hierarchy page in bytes.
        /// </summary>
        public ulong RootHierarchySize { get; set; }

        /// <summary>
        /// Minimum GPS time value in the dataset (optional).
        /// </summary>
        public double GpsTimeMinimum { get; set; }

        /// <summary>
        /// Maximum GPS time value in the dataset (optional).
        /// </summary>
        public double GpsTimeMaximum { get; set; }

        public CopcInfo()
        {
            CenterX = 0;
            CenterY = 0;
            CenterZ = 0;
            HalfSize = 0;
            Spacing = 0;
            RootHierarchyOffset = 0;
            RootHierarchySize = 0;
            GpsTimeMinimum = 0;
            GpsTimeMaximum = 0;
        }

        /// <summary>
        /// Gets the COPC cube as a Stride bounding box.
        /// </summary>
        public BoundingBox GetCube()
        {
            var center = new Vector3((float)CenterX, (float)CenterY, (float)CenterZ);
            var half = new Vector3((float)HalfSize);
            return new BoundingBox(center - half, center + half);
        }

        /// <summary>
        /// Parses a COPC Info structure from binary data.
        /// </summary>
        public static CopcInfo Parse(byte[] data)
        {
            if (data.Length != VlrSizeBytes)
            {
                throw new ArgumentException(
                    $"Invalid COPC info VLR length (should be {VlrSizeBytes}): {data.Length}",
                    nameof(data));
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            var info = new CopcInfo
            {
                CenterX = reader.ReadDouble(),
                CenterY = reader.ReadDouble(),
                CenterZ = reader.ReadDouble(),
                HalfSize = reader.ReadDouble(),
                Spacing = reader.ReadDouble(),
                RootHierarchyOffset = reader.ReadUInt64(),
                RootHierarchySize = reader.ReadUInt64(),
                GpsTimeMinimum = reader.ReadDouble(),
                GpsTimeMaximum = reader.ReadDouble()
            };

            // Read and ignore reserved bytes (160 - 72 = 88 bytes)
            reader.ReadBytes(88);

            return info;
        }

        /// <summary>
        /// Writes the COPC Info structure to binary format.
        /// </summary>
        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(CenterX);
            writer.Write(CenterY);
            writer.Write(CenterZ);
            writer.Write(HalfSize);
            writer.Write(Spacing);
            writer.Write(RootHierarchyOffset);
            writer.Write(RootHierarchySize);
            writer.Write(GpsTimeMinimum);
            writer.Write(GpsTimeMaximum);

            // Write reserved bytes (all zeros)
            writer.Write(new byte[88]);

            return stream.ToArray();
        }

        public override string ToString()
        {
            return $"CopcInfo: center=({CenterX}, {CenterY}, {CenterZ}), halfsize={HalfSize}, " +
                   $"spacing={Spacing}, root_offset={RootHierarchyOffset}, root_size={RootHierarchySize}";
        }
    }
}

