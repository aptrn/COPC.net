using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Copc
{
    /// <summary>
    /// Represents a single Extra Bytes dimension definition from the Extra Bytes VLR.
    /// This describes custom per-point attributes beyond the standard LAS fields.
    /// </summary>
    public class ExtraDimension
    {
        /// <summary>
        /// Reserved (2 bytes)
        /// </summary>
        public ushort Reserved { get; set; }

        /// <summary>
        /// Data type of this dimension (1-30)
        /// </summary>
        public byte DataType { get; set; }

        /// <summary>
        /// Options bitfield
        /// </summary>
        public byte Options { get; set; }

        /// <summary>
        /// Name of the dimension (32 bytes, null-terminated string)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Unused bytes (4 bytes)
        /// </summary>
        public uint Unused { get; set; }

        /// <summary>
        /// No data value (3x double, for min/max/no_data)
        /// </summary>
        public double[] NoData { get; set; } = new double[3];

        /// <summary>
        /// Min value (3x double)
        /// </summary>
        public double[] Min { get; set; } = new double[3];

        /// <summary>
        /// Max value (3x double)
        /// </summary>
        public double[] Max { get; set; } = new double[3];

        /// <summary>
        /// Scale (3x double)
        /// </summary>
        public double[] Scale { get; set; } = new double[3];

        /// <summary>
        /// Offset (3x double)
        /// </summary>
        public double[] Offset { get; set; } = new double[3];

        /// <summary>
        /// Description (32 bytes, null-terminated string)
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets the size in bytes of this dimension's data
        /// </summary>
        public int GetDataSize()
        {
            return DataType switch
            {
                1 => 1,  // unsigned char
                2 => 1,  // char
                3 => 2,  // unsigned short
                4 => 2,  // short
                5 => 4,  // unsigned long
                6 => 4,  // long
                7 => 8,  // unsigned long long
                8 => 8,  // long long
                9 => 4,  // float
                10 => 8, // double
                11 => 1, // unsigned char[2]
                12 => 1, // char[2]
                13 => 2, // unsigned short[2]
                14 => 2, // short[2]
                15 => 4, // unsigned long[2]
                16 => 4, // long[2]
                17 => 8, // unsigned long long[2]
                18 => 8, // long long[2]
                19 => 4, // float[2]
                20 => 8, // double[2]
                21 => 1, // unsigned char[3]
                22 => 1, // char[3]
                23 => 2, // unsigned short[3]
                24 => 2, // short[3]
                25 => 4, // unsigned long[3]
                26 => 4, // long[3]
                27 => 8, // unsigned long long[3]
                28 => 8, // long long[3]
                29 => 4, // float[3]
                30 => 8, // double[3]
                _ => 0
            };
        }

        /// <summary>
        /// Gets the number of components (1, 2, or 3)
        /// </summary>
        public int GetComponentCount()
        {
            if (DataType >= 1 && DataType <= 10) return 1;
            if (DataType >= 11 && DataType <= 20) return 2;
            if (DataType >= 21 && DataType <= 30) return 3;
            return 1;
        }

        /// <summary>
        /// Extracts the value(s) from raw bytes and returns as float32
        /// </summary>
        public float[] ExtractAsFloat32(byte[] data, int offset)
        {
            int componentCount = GetComponentCount();
            float[] result = new float[componentCount];

            for (int i = 0; i < componentCount; i++)
            {
                double value = ExtractComponent(data, offset, i);
                // Apply scale and offset if present
                if (Scale[i] != 0.0)
                {
                    value = value * Scale[i] + Offset[i];
                }
                result[i] = (float)value;
            }

            return result;
        }

        /// <summary>
        /// Extracts the value(s) from raw bytes and writes them as float32 directly into the destination array.
        /// This avoids per-point allocations when converting extra dimensions at scale.
        /// </summary>
        /// <param name="data">Source extra-bytes buffer for a single point</param>
        /// <param name="offset">Byte offset within extra-bytes where this dimension starts</param>
        /// <param name="destination">Destination float array to write into</param>
        /// <param name="destinationIndex">Starting index in destination to write the component(s)</param>
        public void ExtractAsFloat32Into(byte[] data, int offset, float[] destination, int destinationIndex)
        {
            int componentCount = GetComponentCount();
            for (int i = 0; i < componentCount; i++)
            {
                double value = ExtractComponent(data, offset, i);
                if (Scale[i] != 0.0)
                {
                    value = value * Scale[i] + Offset[i];
                }
                destination[destinationIndex + i] = (float)value;
            }
        }

        private double ExtractComponent(byte[] data, int offset, int componentIndex)
        {
            int baseType = ((DataType - 1) % 10) + 1;
            int compSize = GetDataSize();
            int pos = offset + (componentIndex * compSize);

            return baseType switch
            {
                1 => data[pos],                                              // unsigned char
                2 => (sbyte)data[pos],                                       // char
                3 => BitConverter.ToUInt16(data, pos),                       // unsigned short
                4 => BitConverter.ToInt16(data, pos),                        // short
                5 => BitConverter.ToUInt32(data, pos),                       // unsigned long
                6 => BitConverter.ToInt32(data, pos),                        // long
                7 => BitConverter.ToUInt64(data, pos),                       // unsigned long long
                8 => BitConverter.ToInt64(data, pos),                        // long long
                9 => BitConverter.ToSingle(data, pos),                       // float
                10 => BitConverter.ToDouble(data, pos),                      // double
                _ => 0.0
            };
        }

        /// <summary>
        /// Parses an ExtraDimension from a 192-byte record
        /// </summary>
        public static ExtraDimension Parse(byte[] data, int offset = 0)
        {
            using var ms = new MemoryStream(data, offset, 192);
            using var reader = new BinaryReader(ms);

            var dim = new ExtraDimension
            {
                Reserved = reader.ReadUInt16(),
                DataType = reader.ReadByte(),
                Options = reader.ReadByte()
            };

            // Read name (32 bytes)
            byte[] nameBytes = reader.ReadBytes(32);
            dim.Name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

            dim.Unused = reader.ReadUInt32();

            // Read arrays of 3 doubles each
            for (int i = 0; i < 3; i++) dim.NoData[i] = reader.ReadDouble();
            for (int i = 0; i < 3; i++) dim.Min[i] = reader.ReadDouble();
            for (int i = 0; i < 3; i++) dim.Max[i] = reader.ReadDouble();
            for (int i = 0; i < 3; i++) dim.Scale[i] = reader.ReadDouble();
            for (int i = 0; i < 3; i++) dim.Offset[i] = reader.ReadDouble();

            // Read description (32 bytes)
            byte[] descBytes = reader.ReadBytes(32);
            dim.Description = Encoding.UTF8.GetString(descBytes).TrimEnd('\0');

            return dim;
        }

        public override string ToString()
        {
            return $"{Name} (Type {DataType}, {GetComponentCount()} component(s), {GetDataSize()} bytes)";
        }
    }

    /// <summary>
    /// Parser for the Extra Bytes VLR (record ID 4, user ID "LASF_Spec")
    /// </summary>
    public static class ExtraBytesVlrParser
    {
        /// <summary>
        /// Parses Extra Bytes VLR data into a list of extra dimensions
        /// </summary>
        public static List<ExtraDimension> Parse(byte[] vlrData)
        {
            var dimensions = new List<ExtraDimension>();

            if (vlrData == null || vlrData.Length == 0)
                return dimensions;

            // Each extra dimension record is 192 bytes
            int recordCount = vlrData.Length / 192;

            for (int i = 0; i < recordCount; i++)
            {
                try
                {
                    var dim = ExtraDimension.Parse(vlrData, i * 192);
                    dimensions.Add(dim);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to parse extra dimension {i}: {ex.Message}");
                }
            }

            return dimensions;
        }
    }
}

