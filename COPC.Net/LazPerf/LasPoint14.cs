using System;
using System.Runtime.InteropServices;

namespace Copc.LazPerf
{
    /// <summary>
    /// LAS 1.4 Point Format (base format 6 - 30 bytes)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LasPoint14
    {
        public int X;
        public int Y;
        public int Z;
        public ushort Intensity;
        public byte Returns;           // Return number (4 bits) + Number of returns (4 bits)
        public byte Flags;             // Classification flags (4 bits) + Scanner channel (2 bits) + Scan direction (1 bit) + Edge of flight line (1 bit)
        public byte Classification;
        public byte UserData;
        public short ScanAngle;        // Note: 2 bytes in format 14 (vs 1 byte in format 10)
        public ushort PointSourceId;
        public double GpsTime;

        // Derived properties
        public byte ReturnNumber => (byte)(Returns & 0x0F);
        public byte NumberOfReturns => (byte)((Returns >> 4) & 0x0F);
        
        public byte ClassificationFlags => (byte)(Flags & 0x0F);
        public byte ScannerChannel => (byte)((Flags >> 4) & 0x03);
        public bool ScanDirectionFlag => ((Flags >> 6) & 0x01) != 0;
        public bool EdgeOfFlightLine => ((Flags >> 7) & 0x01) != 0;

        /// <summary>
        /// Unpacks a LAS 1.4 point from a byte array
        /// </summary>
        public static LasPoint14 Unpack(byte[] data, int offset)
        {
            var point = new LasPoint14();
            int pos = offset;

            point.X = BitConverter.ToInt32(data, pos);
            pos += 4;
            point.Y = BitConverter.ToInt32(data, pos);
            pos += 4;
            point.Z = BitConverter.ToInt32(data, pos);
            pos += 4;
            point.Intensity = BitConverter.ToUInt16(data, pos);
            pos += 2;
            point.Returns = data[pos++];
            point.Flags = data[pos++];
            point.Classification = data[pos++];
            point.UserData = data[pos++];
            point.ScanAngle = BitConverter.ToInt16(data, pos);
            pos += 2;
            point.PointSourceId = BitConverter.ToUInt16(data, pos);
            pos += 2;
            point.GpsTime = BitConverter.ToDouble(data, pos);

            return point;
        }

        /// <summary>
        /// Packs a LAS 1.4 point into a byte array
        /// </summary>
        public void Pack(byte[] data, int offset)
        {
            int pos = offset;

            BitConverter.GetBytes(X).CopyTo(data, pos);
            pos += 4;
            BitConverter.GetBytes(Y).CopyTo(data, pos);
            pos += 4;
            BitConverter.GetBytes(Z).CopyTo(data, pos);
            pos += 4;
            BitConverter.GetBytes(Intensity).CopyTo(data, pos);
            pos += 2;
            data[pos++] = Returns;
            data[pos++] = Flags;
            data[pos++] = Classification;
            data[pos++] = UserData;
            BitConverter.GetBytes(ScanAngle).CopyTo(data, pos);
            pos += 2;
            BitConverter.GetBytes(PointSourceId).CopyTo(data, pos);
            pos += 2;
            BitConverter.GetBytes(GpsTime).CopyTo(data, pos);
        }

        public override string ToString()
        {
            return $"Point14({X}, {Y}, {Z}) I={Intensity} C={Classification} Rtn={ReturnNumber}/{NumberOfReturns}";
        }
    }

    /// <summary>
    /// RGB component for LAS 1.4 formats 7 and 8
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Rgb14
    {
        public ushort Red;
        public ushort Green;
        public ushort Blue;

        public static Rgb14 Unpack(byte[] data, int offset)
        {
            var rgb = new Rgb14();
            rgb.Red = BitConverter.ToUInt16(data, offset);
            rgb.Green = BitConverter.ToUInt16(data, offset + 2);
            rgb.Blue = BitConverter.ToUInt16(data, offset + 4);
            return rgb;
        }

        public void Pack(byte[] data, int offset)
        {
            BitConverter.GetBytes(Red).CopyTo(data, offset);
            BitConverter.GetBytes(Green).CopyTo(data, offset + 2);
            BitConverter.GetBytes(Blue).CopyTo(data, offset + 4);
        }
    }

    /// <summary>
    /// NIR component for LAS 1.4 format 8
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Nir14
    {
        public ushort Value;

        public static Nir14 Unpack(byte[] data, int offset)
        {
            return new Nir14 { Value = BitConverter.ToUInt16(data, offset) };
        }

        public void Pack(byte[] data, int offset)
        {
            BitConverter.GetBytes(Value).CopyTo(data, offset);
        }
    }
}

