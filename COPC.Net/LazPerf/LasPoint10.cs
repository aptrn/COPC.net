using System;

namespace Copc.LazPerf
{
    /// <summary>
    /// LAS Point Format 0 structure
    /// </summary>
    public struct LasPoint10
    {
        public int X;
        public int Y;
        public int Z;
        public ushort Intensity;
        public byte ReturnNumber;               // 3 bits
        public byte NumberOfReturns;            // 3 bits
        public byte ScanDirectionFlag;          // 1 bit
        public byte EdgeOfFlightLine;           // 1 bit
        public byte Classification;
        public sbyte ScanAngleRank;
        public byte UserData;
        public ushort PointSourceId;

        public const int Size = 20; // 4+4+4+2+1+1+1+1+2 = 20 bytes

        public static LasPoint10 Unpack(byte[] data, int offset)
        {
            var point = new LasPoint10
            {
                X = LazUtils.UnpackInt32(data, offset),
                Y = LazUtils.UnpackInt32(data, offset + 4),
                Z = LazUtils.UnpackInt32(data, offset + 8),
                Intensity = LazUtils.UnpackUInt16(data, offset + 12)
            };

            // Unpack bit fields
            byte bitfields = data[offset + 14];
            point.ReturnNumber = (byte)(bitfields & 0x07);
            point.NumberOfReturns = (byte)((bitfields >> 3) & 0x07);
            point.ScanDirectionFlag = (byte)((bitfields >> 6) & 0x01);
            point.EdgeOfFlightLine = (byte)((bitfields >> 7) & 0x01);

            point.Classification = data[offset + 15];
            point.ScanAngleRank = (sbyte)data[offset + 16];
            point.UserData = data[offset + 17];
            point.PointSourceId = LazUtils.UnpackUInt16(data, offset + 18);

            return point;
        }

        public void Pack(byte[] data, int offset)
        {
            LazUtils.PackInt32(X, data, offset);
            LazUtils.PackInt32(Y, data, offset + 4);
            LazUtils.PackInt32(Z, data, offset + 8);
            LazUtils.PackUInt16(Intensity, data, offset + 12);

            // Pack bit fields
            byte bitfields = (byte)((EdgeOfFlightLine & 0x01) << 7 |
                                   (ScanDirectionFlag & 0x01) << 6 |
                                   (NumberOfReturns & 0x07) << 3 |
                                   (ReturnNumber & 0x07));
            data[offset + 14] = bitfields;

            data[offset + 15] = Classification;
            data[offset + 16] = (byte)ScanAngleRank;
            data[offset + 17] = UserData;
            LazUtils.PackUInt16(PointSourceId, data, offset + 18);
        }

        public byte GetBitfields()
        {
            return (byte)((EdgeOfFlightLine & 0x01) << 7 |
                         (ScanDirectionFlag & 0x01) << 6 |
                         (NumberOfReturns & 0x07) << 3 |
                         (ReturnNumber & 0x07));
        }

        public void SetBitfields(byte b)
        {
            ReturnNumber = (byte)(b & 0x07);
            NumberOfReturns = (byte)((b >> 3) & 0x07);
            ScanDirectionFlag = (byte)((b >> 6) & 0x01);
            EdgeOfFlightLine = (byte)((b >> 7) & 0x01);
        }
    }
}

