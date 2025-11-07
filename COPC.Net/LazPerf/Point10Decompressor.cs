using System;

namespace Copc.LazPerf
{
    /// <summary>
    /// Decompressor for LAS Point Format 0
    /// </summary>
    public class Point10Decompressor
    {
        // Lookup tables for return number mapping
        private static readonly byte[,] NumberReturnMap = new byte[8, 8]
        {
            { 15, 14, 13, 12, 11, 10,  9,  8 },
            { 14,  0,  1,  3,  6, 10, 10,  9 },
            { 13,  1,  2,  4,  7, 11, 11, 10 },
            { 12,  3,  4,  5,  8, 12, 12, 11 },
            { 11,  6,  7,  8,  9, 13, 13, 12 },
            { 10, 10, 11, 12, 13, 14, 14, 13 },
            {  9, 10, 11, 12, 13, 14, 15, 14 },
            {  8,  9, 10, 11, 12, 13, 14, 15 }
        };

        private static readonly byte[,] NumberReturnLevel = new byte[8, 8]
        {
            {  0,  1,  2,  3,  4,  5,  6,  7 },
            {  1,  0,  1,  2,  3,  4,  5,  6 },
            {  2,  1,  0,  1,  2,  3,  4,  5 },
            {  3,  2,  1,  0,  1,  2,  3,  4 },
            {  4,  3,  2,  1,  0,  1,  2,  3 },
            {  5,  4,  3,  2,  1,  0,  1,  2 },
            {  6,  5,  4,  3,  2,  1,  0,  1 },
            {  7,  6,  5,  4,  3,  2,  1,  0 }
        };

        private readonly ArithmeticDecoder _decoder;
        
        private LasPoint10 _last;
        private readonly ushort[] _lastIntensity;
        
        private readonly StreamingMedian<int>[] _lastXDiffMedian5;
        private readonly StreamingMedian<int>[] _lastYDiffMedian5;
        
        private readonly int[] _lastHeight;
        private readonly ArithmeticModel _mChangedValues;
        
        private readonly ArithmeticModel[] _mScanAngleRank;
        private readonly ArithmeticModel[] _mBitByte;
        private readonly ArithmeticModel[] _mClassification;
        private readonly ArithmeticModel[] _mUserData;
        
        private readonly IntegerDecompressor _icIntensity;
        private readonly IntegerDecompressor _icPointSourceId;
        private readonly IntegerDecompressor _icDx;
        private readonly IntegerDecompressor _icDy;
        private readonly IntegerDecompressor _icZ;
        
        private bool _haveLast;
        private bool _decompressorsInited;

        public Point10Decompressor(ArithmeticDecoder decoder)
        {
            _decoder = decoder;
            
            _lastIntensity = new ushort[16];
            _lastXDiffMedian5 = new StreamingMedian<int>[16];
            _lastYDiffMedian5 = new StreamingMedian<int>[16];
            _lastHeight = new int[8];
            
            for (int i = 0; i < 16; i++)
            {
                _lastXDiffMedian5[i] = new StreamingMedian<int>();
                _lastYDiffMedian5[i] = new StreamingMedian<int>();
            }
            
            _mChangedValues = new ArithmeticModel(64);
            
            _mScanAngleRank = new ArithmeticModel[2];
            _mScanAngleRank[0] = new ArithmeticModel(256);
            _mScanAngleRank[1] = new ArithmeticModel(256);
            
            _mBitByte = new ArithmeticModel[256];
            _mClassification = new ArithmeticModel[256];
            _mUserData = new ArithmeticModel[256];
            
            for (int i = 0; i < 256; i++)
            {
                _mBitByte[i] = new ArithmeticModel(256);
                _mClassification[i] = new ArithmeticModel(256);
                _mUserData[i] = new ArithmeticModel(256);
            }
            
            _icIntensity = new IntegerDecompressor(16, 4);
            _icPointSourceId = new IntegerDecompressor(16);
            _icDx = new IntegerDecompressor(32, 2);
            _icDy = new IntegerDecompressor(32, 22);
            _icZ = new IntegerDecompressor(32, 20);
            
            _haveLast = false;
            _decompressorsInited = false;
        }

        private void Init()
        {
            _icIntensity.Init();
            _icPointSourceId.Init();
            _icDx.Init();
            _icDy.Init();
            _icZ.Init();
        }

        public byte[] Decompress()
        {
            if (!_decompressorsInited)
            {
                Init();
                _decompressorsInited = true;
            }

            // First point is stored uncompressed
            if (!_haveLast)
            {
                _haveLast = true;
                byte[] buf = new byte[LasPoint10.Size];
                _decoder.GetInStream().GetBytes(buf, LasPoint10.Size);
                _last = LasPoint10.Unpack(buf, 0);
                _last.Intensity = 0;
                return buf;
            }

            uint r, n, m, l;
            int median, diff;

            // Decompress which values have changed
            int changedValues = (int)_decoder.DecodeSymbol(_mChangedValues);
            
            if (changedValues != 0)
            {
                // Decode bit fields if changed
                if ((changedValues & (1 << 5)) != 0)
                {
                    byte b = _last.GetBitfields();
                    b = (byte)_decoder.DecodeSymbol(_mBitByte[b]);
                    _last.SetBitfields(b);
                }

                r = _last.ReturnNumber;
                n = _last.NumberOfReturns;
                m = NumberReturnMap[n, r];
                l = NumberReturnLevel[n, r];

                // Skip decoding of non-required attributes: intensity, classification, scan angle, user data, point source ID
                // We keep bitfields (return info) updated above for correct XY/Z contexts.
            }
            else
            {
                r = _last.ReturnNumber;
                n = _last.NumberOfReturns;
                m = NumberReturnMap[n, r];
                l = NumberReturnLevel[n, r];
            }

            // Decompress X coordinate
            median = _lastXDiffMedian5[m].Get();
            diff = _icDx.Decompress(_decoder, median, n == 1 ? 1U : 0U);
            _last.X += diff;
            _lastXDiffMedian5[m].Add(diff);

            // Decompress Y coordinate
            median = _lastYDiffMedian5[m].Get();
            uint kBits = _icDx.GetK();
            uint context = (n == 1 ? 1U : 0U) + (kBits < 20 ? ClearBit0(kBits) : 20U);
            diff = _icDy.Decompress(_decoder, median, context);
            _last.Y += diff;
            _lastYDiffMedian5[m].Add(diff);

            // Decompress Z coordinate
            kBits = (_icDx.GetK() + _icDy.GetK()) / 2;
            context = (n == 1 ? 1U : 0U) + (kBits < 18 ? ClearBit0(kBits) : 18U);
            _last.Z = _icZ.Decompress(_decoder, _lastHeight[l], context);
            _lastHeight[l] = _last.Z;

            // Pack and return
            byte[] result = new byte[LasPoint10.Size];
            _last.Pack(result, 0);
            return result;
        }

        private static uint ClearBit0(uint value)
        {
            return value & ~1U;
        }
    }
}

