using System;
using System.Collections.Generic;

namespace Copc.LazPerf
{
    /// <summary>
    /// Decompressor for LAS 1.4 point formats (6, 7, 8)
    /// Based on the LAZ-perf implementation
    /// </summary>
    public class Point14Decompressor
    {
        private static void DebugLog(string message)
        {
            if (Copc.Utils.DebugConfig.LazPerfDebug)
                Console.WriteLine(message);
        }
        // The main input stream (for reading first point and stream sizes)
        private readonly IInStream _rawStream;
        private readonly int _pointFormat; // 6, 7, or 8
        private readonly int _extraByteCount; // Number of extra bytes beyond base format
        
        // Nine separate decoders for different field types (as per LAZ format 14 spec)
        // Null means the field doesn't vary (all points have same value as first point)
        private ArithmeticDecoder? _xyDecoder;
        private ArithmeticDecoder? _zDecoder;
        private ArithmeticDecoder? _classDecoder;
        private ArithmeticDecoder? _flagsDecoder;
        private ArithmeticDecoder? _intensityDecoder;
        private ArithmeticDecoder? _scanAngleDecoder;
        private ArithmeticDecoder? _userDataDecoder;
        private ArithmeticDecoder? _pointSourceIdDecoder;
        private ArithmeticDecoder? _gpsTimeDecoder;
        
        // RGB decoder (for format 7/8)
        private ArithmeticDecoder? _rgbDecoder;
        
        // NIR decoder (for format 8)
        private ArithmeticDecoder? _nirDecoder;
        
        // Extra bytes decoder
        private ArithmeticDecoder? _extraBytesDecoder;
        
        private bool _decodersInitialized = false;
        
        // RGB decompressor contexts (for format 7/8)
        private RgbContext[]? _rgbContexts;
        
        // Last RGB values for each channel
        private ushort[] _lastR = new ushort[4];
        private ushort[] _lastG = new ushort[4];
        private ushort[] _lastB = new ushort[4];
        
        // Lookup tables for return number/number of returns context mapping
        private static readonly byte[,] NumberReturnMap6Ctx = new byte[16,16]
        {
            {  0,  1,  2,  3,  4,  5,  3,  4,  4,  5,  5,  5,  5,  5,  5,  5 },
            {  1,  0,  1,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3,  3 },
            {  2,  1,  2,  4,  4,  4,  4,  4,  4,  4,  4,  3,  3,  3,  3,  3 },
            {  3,  3,  4,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4 },
            {  4,  3,  4,  4,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4 },
            {  5,  3,  4,  4,  4,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4 },
            {  3,  3,  4,  4,  4,  4,  5,  4,  4,  4,  4,  4,  4,  4,  4,  4 },
            {  4,  3,  4,  4,  4,  4,  4,  5,  4,  4,  4,  4,  4,  4,  4,  4 },
            {  4,  3,  4,  4,  4,  4,  4,  4,  5,  4,  4,  4,  4,  4,  4,  4 },
            {  5,  3,  4,  4,  4,  4,  4,  4,  4,  5,  4,  4,  4,  4,  4,  4 },
            {  5,  3,  4,  4,  4,  4,  4,  4,  4,  4,  5,  4,  4,  4,  4,  4 },
            {  5,  3,  3,  4,  4,  4,  4,  4,  4,  4,  4,  5,  5,  4,  4,  4 },
            {  5,  3,  3,  4,  4,  4,  4,  4,  4,  4,  4,  5,  5,  5,  4,  4 },
            {  5,  3,  3,  4,  4,  4,  4,  4,  4,  4,  4,  4,  5,  5,  5,  4 },
            {  5,  3,  3,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  5,  5,  5 },
            {  5,  3,  3,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  4,  5,  5 }
        };

        private static readonly byte[,] NumberReturnLevel8Ctx = new byte[16,16]
        {
            {  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7,  7,  7,  7,  7,  7 },
            {  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7,  7,  7,  7,  7 },
            {  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7,  7,  7,  7 },
            {  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7,  7,  7 },
            {  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7,  7 },
            {  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7,  7 },
            {  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7,  7 },
            {  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7,  7 },
            {  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6,  7 },
            {  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5,  6 },
            {  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4,  5 },
            {  7,  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3,  4 },
            {  7,  7,  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2,  3 },
            {  7,  7,  7,  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1,  2 },
            {  7,  7,  7,  7,  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0,  1 },
            {  7,  7,  7,  7,  7,  7,  7,  7,  7,  6,  5,  4,  3,  2,  1,  0 }
        };

        private class RgbContext
        {
            public ArithmeticModel UsedModel;
            public ArithmeticModel[] DiffModels; // 6 models for RGB differences
            
            public RgbContext()
            {
                UsedModel = new ArithmeticModel(128);
                DiffModels = new ArithmeticModel[6];
                for (int i = 0; i < 6; i++)
                    DiffModels[i] = new ArithmeticModel(256);
            }
        }
        
        private class ChannelContext
        {
            // Models for various fields
            public List<ArithmeticModel> ChangedValuesModel;
            public ArithmeticModel ScannerChannelModel;
            public List<ArithmeticModel> NumberOfReturnsModel;
            public List<ArithmeticModel> ReturnNumberModel;
            public ArithmeticModel RnGpsSameModel;
            public List<ArithmeticModel> ClassificationModel;
            public List<ArithmeticModel> FlagsModel;
            public List<ArithmeticModel> UserDataModel;
            public ArithmeticModel GpsTimeMultiModel;
            public ArithmeticModel GpsTime0DiffModel;

            // Decompressors for integer fields
            public IntegerDecompressor DxDecomp;
            public IntegerDecompressor DyDecomp;
            public IntegerDecompressor ZDecomp;
            public IntegerDecompressor IntensityDecomp;
            public IntegerDecompressor ScanAngleDecomp;
            public IntegerDecompressor PointSourceIdDecomp;
            public IntegerDecompressor GpsTimeDecomp;

            // Last point and state
            public bool HaveLast;
            public LasPoint14 Last;
            public ushort[] LastIntensity;
            public int[] LastZ;
            public double[] LastGpsTime;
            public int[] LastGpsTimeDiff;
            public int[] MultiExtremeCounter;
            
            // Streaming medians for X/Y prediction (12 contexts like C++ implementation)
            public StreamingMedian<int>[] LastXDiffMedian5;
            public StreamingMedian<int>[] LastYDiffMedian5;
            
            // GPS time change flag
            public bool GpsTimeChange;

            public ChannelContext()
            {
                ChangedValuesModel = new List<ArithmeticModel>();
                for (int i = 0; i < 8; i++)
                    ChangedValuesModel.Add(new ArithmeticModel(128));

                ScannerChannelModel = new ArithmeticModel(3);
                NumberOfReturnsModel = new List<ArithmeticModel>();
                ReturnNumberModel = new List<ArithmeticModel>();
                for (int i = 0; i < 16; i++)
                {
                    NumberOfReturnsModel.Add(new ArithmeticModel(16));
                    ReturnNumberModel.Add(new ArithmeticModel(16));
                }

                ClassificationModel = new List<ArithmeticModel>();
                FlagsModel = new List<ArithmeticModel>();
                UserDataModel = new List<ArithmeticModel>();
                for (int i = 0; i < 64; i++)
                {
                    ClassificationModel.Add(new ArithmeticModel(256));
                    FlagsModel.Add(new ArithmeticModel(64));
                    UserDataModel.Add(new ArithmeticModel(256));
                }

                GpsTimeMultiModel = new ArithmeticModel(515);
                GpsTime0DiffModel = new ArithmeticModel(5);
                RnGpsSameModel = new ArithmeticModel(32);

                // Initialize decompressors
                DxDecomp = new IntegerDecompressor(32, 2);
                DyDecomp = new IntegerDecompressor(32, 22);
                ZDecomp = new IntegerDecompressor(32, 20);
                IntensityDecomp = new IntegerDecompressor(16, 4);
                ScanAngleDecomp = new IntegerDecompressor(16, 2);
                PointSourceIdDecomp = new IntegerDecompressor(16);
                GpsTimeDecomp = new IntegerDecompressor(32, 9);

                DxDecomp.Init();
                DyDecomp.Init();
                ZDecomp.Init();
                IntensityDecomp.Init();
                ScanAngleDecomp.Init();
                PointSourceIdDecomp.Init();
                GpsTimeDecomp.Init();

                HaveLast = false;
                Last = new LasPoint14();
                LastIntensity = new ushort[8];
                LastZ = new int[8];
                LastGpsTime = new double[4];
                LastGpsTimeDiff = new int[4];
                MultiExtremeCounter = new int[4];
                GpsTimeChange = false;
                
                // Initialize streaming medians for X/Y (12 contexts)
                LastXDiffMedian5 = new StreamingMedian<int>[12];
                LastYDiffMedian5 = new StreamingMedian<int>[12];
                for (int i = 0; i < 12; i++)
                {
                    LastXDiffMedian5[i] = new StreamingMedian<int>();
                    LastYDiffMedian5[i] = new StreamingMedian<int>();
                }
            }
        }

        private readonly List<ChannelContext> _contexts;
        private int _lastChannel;

        public Point14Decompressor(IInStream rawStream, int pointFormat, int extraByteCount = 0)
        {
            _rawStream = rawStream;
            _pointFormat = pointFormat;
            _extraByteCount = extraByteCount;
            _contexts = new List<ChannelContext>();
            
            // Initialize contexts for up to 4 scanner channels
            for (int i = 0; i < 4; i++)
                _contexts.Add(new ChannelContext());
            
            // Initialize RGB contexts for format 7/8
            if (_pointFormat == 7 || _pointFormat == 8)
            {
                _rgbContexts = new RgbContext[4];
                for (int i = 0; i < 4; i++)
                    _rgbContexts[i] = new RgbContext();
            }
            
            _lastChannel = -1;  // -1 means no points read yet
            _decodersInitialized = false;
        }
        
        /// <summary>
        /// Initializes the arithmetic decoders after reading the first point.
        /// For COPC files, the chunk structure is:
        /// 1. First point (30 bytes uncompressed)
        /// 2. First RGB (6 bytes uncompressed) if format 7/8
        /// 3. First NIR (2 bytes uncompressed) if format 8
        /// 4. 9 stream sizes for base point (36 bytes) - NO chunk point count in COPC!
        /// 5. RGB stream size (4 bytes) if format 7/8
        /// 6. NIR stream size (4 bytes) if format 8
        /// 7. Extra bytes stream size (4 bytes) if extra bytes exist
        /// 8. Then all compressed stream data
        /// 
        /// NOTE: COPC does NOT include chunk point count (unlike regular LAZ files)
        /// because point count is stored in the COPC hierarchy.
        /// </summary>
        private void InitializeDecoders()
        {
            // Read and ignore the chunk point count (present in laz-perf streams)
            uint _ = _rawStream.ReadInt();

            // Read sizes for the 9 point14 streams
            uint xySizeBytes = _rawStream.ReadInt();
            uint zSizeBytes = _rawStream.ReadInt();
            uint classSizeBytes = _rawStream.ReadInt();
            uint flagsSizeBytes = _rawStream.ReadInt();
            uint intensitySizeBytes = _rawStream.ReadInt();
            uint scanAngleSizeBytes = _rawStream.ReadInt();
            uint userDataSizeBytes = _rawStream.ReadInt();
            uint pointSourceIdSizeBytes = _rawStream.ReadInt();
            uint gpsTimeSizeBytes = _rawStream.ReadInt();

            // RGB and NIR sizes if applicable
            uint rgbSizeBytes = 0;
            if (_pointFormat == 7 || _pointFormat == 8)
                rgbSizeBytes = _rawStream.ReadInt();

            uint nirSizeBytes = 0;
            if (_pointFormat == 8)
                nirSizeBytes = _rawStream.ReadInt();

            // Extra bytes sizes: one size per extra byte
            uint[]? extraSizes = null;
            if (_extraByteCount > 0)
            {
                extraSizes = new uint[_extraByteCount];
                for (int i = 0; i < _extraByteCount; i++)
                    extraSizes[i] = _rawStream.ReadInt();
            }

            // Initialize decoders by consuming stream data in the same order
            _xyDecoder = CreateDecoderForStream(_rawStream, (int)xySizeBytes);
            _zDecoder = CreateDecoderForStream(_rawStream, (int)zSizeBytes);
            _classDecoder = CreateDecoderForStream(_rawStream, (int)classSizeBytes);
            _flagsDecoder = CreateDecoderForStream(_rawStream, (int)flagsSizeBytes);
            _intensityDecoder = CreateDecoderForStream(_rawStream, (int)intensitySizeBytes);
            _scanAngleDecoder = CreateDecoderForStream(_rawStream, (int)scanAngleSizeBytes);
            _userDataDecoder = CreateDecoderForStream(_rawStream, (int)userDataSizeBytes);
            _pointSourceIdDecoder = CreateDecoderForStream(_rawStream, (int)pointSourceIdSizeBytes);
            _gpsTimeDecoder = CreateDecoderForStream(_rawStream, (int)gpsTimeSizeBytes);

            if (_pointFormat == 7 || _pointFormat == 8)
                _rgbDecoder = CreateDecoderForStream(_rawStream, (int)rgbSizeBytes);

            if (_pointFormat == 8)
                _nirDecoder = CreateDecoderForStream(_rawStream, (int)nirSizeBytes);

            // Consume extra byte streams to advance the input, even if we don't decode them yet
            if (extraSizes != null)
            {
                for (int i = 0; i < extraSizes.Length; i++)
                {
                    int sz = (int)extraSizes[i];
                    if (sz > 0)
                    {
                        byte[] skip = new byte[sz];
                        _rawStream.GetBytes(skip, sz);
                    }
                }
            }

            _decodersInitialized = true;
        }
        
        /// <summary>
        /// Creates an arithmetic decoder for a specific compressed stream
        /// </summary>
        private static ArithmeticDecoder? CreateDecoderForStream(IInStream parentStream, int streamSize)
        {
            if (streamSize == 0)
            {
                // Empty stream - return null to indicate no decoding needed
                // This means all points have the same value as the first point
                return null;
            }
            
            // Read the compressed data for this stream
            byte[] streamData = new byte[streamSize];
            parentStream.GetBytes(streamData, streamSize);
            
            // Create a new stream and decoder for this data
            var stream = new MemoryInStream(streamData);
            var decoder = new ArithmeticDecoder(stream);
            decoder.ReadInitBytes();
            
            return decoder;
        }

        public byte[] Decompress()
        {
            // First point is read from raw stream
            if (_lastChannel == -1)
            {
                DebugLog($"[DEBUG] Reading first uncompressed point (30 bytes)");
                // Read first uncompressed point (base 30 bytes)
                byte[] firstPointData = new byte[30]; 
                _rawStream.GetBytes(firstPointData, 30);
                DebugLog($"[DEBUG] First uncompressed point read successfully");
                
                // Debug: print raw bytes
                if (Copc.Utils.DebugConfig.LazPerfDebug)
                {
                    Console.Write($"[DEBUG] First point raw bytes: ");
                    for (int i = 0; i < 12; i++)
                        Console.Write($"{firstPointData[i]:X2} ");
                    Console.WriteLine();
                }
                
                var firstPoint = LasPoint14.Unpack(firstPointData, 0);
                DebugLog($"[DEBUG] First point unpacked: X={firstPoint.X}, Y={firstPoint.Y}, Z={firstPoint.Z}, Channel={firstPoint.ScannerChannel}");
                
                // For format 7 & 8, also read the first RGB value (6 bytes: R, G, B as uint16)
                // This happens BEFORE reading sizes, as per LAZ format spec
                byte[]? firstRGB = null;
                if (_pointFormat == 7 || _pointFormat == 8)
                {
                    firstRGB = new byte[6];
                    _rawStream.GetBytes(firstRGB, 6);
                    DebugLog($"[DEBUG] Read first RGB bytes: {BitConverter.ToString(firstRGB)}");
                    
                    // Unpack RGB
                    _lastR[0] = BitConverter.ToUInt16(firstRGB, 0);
                    _lastG[0] = BitConverter.ToUInt16(firstRGB, 2);
                    _lastB[0] = BitConverter.ToUInt16(firstRGB, 4);
                }
                
                // For format 8, also read the first NIR value (2 bytes: uint16)
                byte[]? firstNIR = null;
                if (_pointFormat == 8)
                {
                    firstNIR = new byte[2];
                    _rawStream.GetBytes(firstNIR, 2);
                    DebugLog($"[DEBUG] Read first NIR bytes");
                }
                
                // For extra bytes, the first 'extra' block is stored raw as well
                if (_extraByteCount > 0)
                {
                    var firstExtra = new byte[_extraByteCount];
                    _rawStream.GetBytes(firstExtra, _extraByteCount);
                    DebugLog($"[DEBUG] Read first ExtraBytes ({_extraByteCount} bytes)");
                }
                
                // Determine channel from first point
                int firstChannel = firstPoint.ScannerChannel;
                var firstCtx = _contexts[firstChannel];
                
                // Initialize context
                firstCtx.Last = firstPoint;
                firstCtx.HaveLast = true;
                firstCtx.LastGpsTime[0] = firstPoint.GpsTime;
                for (int i = 0; i < 8; i++)
                {
                    firstCtx.LastIntensity[i] = firstPoint.Intensity;
                    firstCtx.LastZ[i] = firstPoint.Z;
                }
                
                _lastChannel = firstChannel;
                
                // Initialize the decoders now that we've read the first point (and RGB/NIR if applicable)
                InitializeDecoders();
                
                // Pack and return first point - include RGB if format 7/8
                int totalSize = 30;
                if (_pointFormat == 7) totalSize = 36; // 30 + 6 RGB
                if (_pointFormat == 8) totalSize = 38; // 30 + 6 RGB + 2 NIR
                
                byte[] firstResult = new byte[totalSize];
                firstPoint.Pack(firstResult, 0);
                
                if (firstRGB != null)
                    Array.Copy(firstRGB, 0, firstResult, 30, 6);
                if (firstNIR != null)
                    Array.Copy(firstNIR, 0, firstResult, 36, 2);
                
                return firstResult;
            }
            
            if (_xyDecoder == null)
            {
                var same = _contexts[_lastChannel].Last;
                _lastChannel = same.ScannerChannel;
                var res = new byte[_pointFormat == 7 ? 36 : (_pointFormat == 8 ? 38 : 30)];
                same.Pack(res, 0);
                if (_pointFormat == 7 || _pointFormat == 8)
                    DecompressRgb(_lastChannel, res, 30);
                return res;
            }

            var prevCtx = _contexts[_lastChannel];
            int changeStreamPrev =
                (prevCtx.Last.ReturnNumber == 1 ? 1 : 0) |
                ((prevCtx.Last.ReturnNumber >= prevCtx.Last.NumberOfReturns ? 1 : 0) << 1) |
                (prevCtx.GpsTimeChange ? 1 << 2 : 0);

            uint changedValuesSym = _xyDecoder.DecodeSymbol(prevCtx.ChangedValuesModel[changeStreamPrev]);
            bool scannerChannelChanged = ((changedValuesSym >> 6) & 1) != 0;
            bool pointSourceChanged = ((changedValuesSym >> 5) & 1) != 0;
            bool gpsTimeChangedBit = ((changedValuesSym >> 4) & 1) != 0;
            bool scanAngleChanged = ((changedValuesSym >> 3) & 1) != 0;
            bool nrChanges = ((changedValuesSym >> 2) & 1) != 0;
            bool rnMinus = ((changedValuesSym >> 1) & 1) != 0;
            bool rnPlus = ((changedValuesSym >> 0) & 1) != 0;

            int sc = prevCtx.Last.ScannerChannel;
            if (scannerChannelChanged)
            {
                uint diff = _xyDecoder.DecodeSymbol(prevCtx.ScannerChannelModel);
                sc = (sc + (int)diff + 1) & 0x03;
                _lastChannel = sc;
            }

            var ctx = _contexts[sc];
            if (!ctx.HaveLast)
            {
                ctx.HaveLast = true;
                ctx.Last = prevCtx.Last;
                for (int i = 0; i < 8; i++)
                {
                    ctx.LastZ[i] = prevCtx.Last.Z;
                    ctx.LastIntensity[i] = prevCtx.Last.Intensity;
                }
                ctx.LastGpsTime[0] = prevCtx.Last.GpsTime;
            }
            // Update scanner channel bits in Flags (bits 4-5)
            ctx.Last.Flags = (byte)((ctx.Last.Flags & ~0x30) | ((sc & 0x03) << 4));

            uint n = ctx.Last.NumberOfReturns;
            uint r = ctx.Last.ReturnNumber;
            if (nrChanges)
                n = _xyDecoder.DecodeSymbol(ctx.NumberOfReturnsModel[(int)(ctx.Last.NumberOfReturns > 15 ? 15 : ctx.Last.NumberOfReturns)]);
            ctx.Last.Returns = (byte)((n << 4) | (ctx.Last.ReturnNumber & 0xF));

            bool rnIncrements = rnPlus && !rnMinus;
            bool rnDecrements = rnMinus && !rnPlus;
            bool rnMiscChange = rnPlus && rnMinus;
            if (rnIncrements)
                r = (r + 1) & 0x0F;
            else if (rnDecrements)
                r = (r + 15) & 0x0F;
            else if (rnMiscChange)
            {
                if (gpsTimeChangedBit)
                {
                    int rIdx = (int)(r > 15 ? 15 : r);
                    r = _xyDecoder.DecodeSymbol(ctx.ReturnNumberModel[rIdx]);
                }
                else
                    r = (uint)((r + _xyDecoder.DecodeSymbol(ctx.RnGpsSameModel) + 2) & 0x0F);
            }
            ctx.Last.Returns = (byte)((n << 4) | (r & 0xF));

            int m = NumberReturnMap6Ctx[Math.Min(15, n), Math.Min(15, r)];
            int xyContext = (m << 1) | (gpsTimeChangedBit ? 1 : 0);

            int medianX = ctx.LastXDiffMedian5[xyContext].Get();
            int diffX = ctx.DxDecomp.Decompress(_xyDecoder, medianX, (uint)(n == 1 ? 1 : 0));
            ctx.Last.X = ctx.Last.X + diffX;
            ctx.LastXDiffMedian5[xyContext].Add(diffX);

            uint kBitsY = Math.Min(ctx.DxDecomp.GetK(), 20) & ~1u;
            int medianY = ctx.LastYDiffMedian5[xyContext].Get();
            int diffY = ctx.DyDecomp.Decompress(_xyDecoder, medianY, (n == 1 ? 1u : 0u) | kBitsY);
            ctx.Last.Y = ctx.Last.Y + diffY;
            ctx.LastYDiffMedian5[xyContext].Add(diffY);

            if (_zDecoder != null)
            {
                uint kBitsZ = (ctx.DxDecomp.GetK() + ctx.DyDecomp.GetK()) / 2;
                kBitsZ = Math.Min(kBitsZ, 18) & ~1u;
                int n8 = NumberReturnLevel8Ctx[Math.Min(15, n), Math.Min(15, r)];
                ctx.Last.Z = ctx.ZDecomp.Decompress(_zDecoder, ctx.LastZ[n8], (n == 1 ? 1u : 0u) | kBitsZ);
                ctx.LastZ[n8] = ctx.Last.Z;
            }

            if (_classDecoder != null)
            {
                int clsCtx = ((r == 1 && r >= n) ? 1 : 0) | ((ctx.Last.Classification & 0x1F) << 1);
                ctx.Last.Classification = (byte)_classDecoder.DecodeSymbol(ctx.ClassificationModel[Math.Min(63, clsCtx)]);
            }

            if (_flagsDecoder != null)
            {
                // Build context: classFlags | (scanDir<<4) | (eof<<5)
                int lastClassFlags = ctx.Last.Flags & 0x0F;
                int lastScanDir = (ctx.Last.Flags >> 6) & 0x01;
                int lastEof = (ctx.Last.Flags >> 7) & 0x01;
                int mergedLastFlags = lastClassFlags | (lastScanDir << 4) | (lastEof << 5);
                uint decoded = _flagsDecoder.DecodeSymbol(ctx.FlagsModel[Math.Min(63, mergedLastFlags)]);
                // Apply decoded to Flags, preserving scanner channel bits (4-5)
                byte newFlags = (byte)((ctx.Last.Flags & 0x30) |
                                        ((int)decoded & 0x0F) |
                                        ((((int)decoded >> 4) & 0x01) << 6) |
                                        ((((int)decoded >> 5) & 0x01) << 7));
                ctx.Last.Flags = newFlags;
            }

            if (_intensityDecoder != null)
            {
                int intensityCtx = (gpsTimeChangedBit ? 1 : 0) | (((r >= n) ? 1 : 0) << 1) | (((r == 1) ? 1 : 0) << 2);
                ctx.Last.Intensity = (ushort)ctx.IntensityDecomp.Decompress(_intensityDecoder, ctx.LastIntensity[intensityCtx], (uint)(intensityCtx >> 1));
                ctx.LastIntensity[intensityCtx] = ctx.Last.Intensity;
            }

            if (scanAngleChanged && _scanAngleDecoder != null)
            {
                ctx.Last.ScanAngle = (short)ctx.ScanAngleDecomp.Decompress(_scanAngleDecoder, ctx.Last.ScanAngle, (uint)(gpsTimeChangedBit ? 1 : 0));
            }

            if (_userDataDecoder != null)
            {
                int userCtx = ctx.Last.UserData / 4;
                ctx.Last.UserData = (byte)_userDataDecoder.DecodeSymbol(ctx.UserDataModel[Math.Min(63, userCtx)]);
            }

            if (pointSourceChanged && _pointSourceIdDecoder != null)
            {
                ctx.Last.PointSourceId = (ushort)ctx.PointSourceIdDecomp.Decompress(_pointSourceIdDecoder, ctx.Last.PointSourceId, 0);
            }

            if (gpsTimeChangedBit && _gpsTimeDecoder != null)
            {
                DecompressGpsTime(ctx, ctx.Last);
            }
            ctx.GpsTimeChange = gpsTimeChangedBit;

            var point = ctx.Last;

            // Pack point to bytes - size depends on format
            int resultSize = 30;
            if (_pointFormat == 7) resultSize = 36; // 30 + 6 RGB
            if (_pointFormat == 8) resultSize = 38; // 30 + 6 RGB + 2 NIR
            
            byte[] result = new byte[resultSize];
            point.Pack(result, 0);
            
            // Decompress RGB if format 7/8
            if (_pointFormat == 7 || _pointFormat == 8)
            {
                DecompressRgb(sc, result, 30);
            }
            
            // Decompress NIR if format 8
            if (_pointFormat == 8)
            {
                // NIR decompression would go here
                // For now, just leave it as zeros
            }
            
            return result;
        }

        private void DecompressGpsTime(ChannelContext ctx, LasPoint14 point)
        {
            if (_gpsTimeDecoder == null)
            {
                point.GpsTime = ctx.Last.GpsTime;
                ctx.LastGpsTime[0] = point.GpsTime;
                return;
            }
            // Simplified GPS time decompression - uses GPS time decoder
            uint multi = _gpsTimeDecoder.DecodeSymbol(ctx.GpsTimeMultiModel);
            
            if (multi == 0)
            {
                // No change
                point.GpsTime = ctx.Last.GpsTime;
            }
            else if (multi == 1)
            {
                // Use last difference
                long diff = BitConverter.DoubleToInt64Bits(ctx.Last.GpsTime) + ctx.LastGpsTimeDiff[0];
                point.GpsTime = BitConverter.Int64BitsToDouble(diff);
                ctx.LastGpsTimeDiff[0] = (int)(BitConverter.DoubleToInt64Bits(point.GpsTime) - BitConverter.DoubleToInt64Bits(ctx.Last.GpsTime));
            }
            else
            {
                // Decompress new difference
                int diff = ctx.GpsTimeDecomp.Decompress(_gpsTimeDecoder, ctx.LastGpsTimeDiff[0], 0);
                long gpsInt = BitConverter.DoubleToInt64Bits(ctx.Last.GpsTime) + diff;
                point.GpsTime = BitConverter.Int64BitsToDouble(gpsInt);
                ctx.LastGpsTimeDiff[0] = diff;
            }

            ctx.LastGpsTime[0] = point.GpsTime;
        }
        
        private void DecompressRgb(int channel, byte[] buffer, int offset)
        {
            // If no RGB decoder, copy last RGB values
            if (_rgbDecoder == null)
            {
                BitConverter.GetBytes(_lastR[channel]).CopyTo(buffer, offset);
                BitConverter.GetBytes(_lastG[channel]).CopyTo(buffer, offset + 2);
                BitConverter.GetBytes(_lastB[channel]).CopyTo(buffer, offset + 4);
                return;
            }
            
            if (_rgbContexts == null)
            {
                BitConverter.GetBytes(_lastR[channel]).CopyTo(buffer, offset);
                BitConverter.GetBytes(_lastG[channel]).CopyTo(buffer, offset + 2);
                BitConverter.GetBytes(_lastB[channel]).CopyTo(buffer, offset + 4);
                return;
            }
            var ctx = _rgbContexts[channel];
            ushort lastR = _lastR[channel];
            ushort lastG = _lastG[channel];
            ushort lastB = _lastB[channel];
            
            // Decode the symbol that indicates which RGB components changed
            uint sym = _rgbDecoder.DecodeSymbol(ctx.UsedModel);
            
            ushort r, g, b;
            
            // Decompress R low byte
            if ((sym & (1 << 0)) != 0)
            {
                byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[0]);
                r = (ushort)((byte)(corr + (lastR & 0xFF)));
            }
            else
                r = (ushort)(lastR & 0xFF);
            
            // Decompress R high byte
            if ((sym & (1 << 1)) != 0)
            {
                byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[1]);
                r |= (ushort)((byte)(corr + (lastR >> 8)) << 8);
            }
            else
                r |= (ushort)(lastR & 0xFF00);
            
            // Check if G and B are different from R
            if ((sym & (1 << 6)) != 0)
            {
                int diff = (r & 0xFF) - (lastR & 0xFF);
                
                // Decompress G low byte
                if ((sym & (1 << 2)) != 0)
                {
                    byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[2]);
                    g = (ushort)((byte)(corr + Clamp((lastG & 0xFF) + diff)));
                }
                else
                    g = (ushort)(lastG & 0xFF);
                
                // Decompress B low byte
                if ((sym & (1 << 4)) != 0)
                {
                    byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[4]);
                    diff = (diff + ((g & 0xFF) - (lastG & 0xFF))) / 2;
                    b = (ushort)((byte)(corr + Clamp((lastB & 0xFF) + diff)));
                }
                else
                    b = (ushort)(lastB & 0xFF);
                
                // Decompress high bytes
                diff = (r >> 8) - (lastR >> 8);
                
                // Decompress G high byte
                if ((sym & (1 << 3)) != 0)
                {
                    byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[3]);
                    g |= (ushort)((byte)(corr + Clamp((lastG >> 8) + diff)) << 8);
                }
                else
                    g |= (ushort)(lastG & 0xFF00);
                
                // Decompress B high byte
                if ((sym & (1 << 5)) != 0)
                {
                    byte corr = (byte)_rgbDecoder.DecodeSymbol(ctx.DiffModels[5]);
                    diff = (diff + (g >> 8) - (lastG >> 8)) / 2;
                    b |= (ushort)((byte)(corr + Clamp((lastB >> 8) + diff)) << 8);
                }
                else
                    b |= (ushort)(lastB & 0xFF00);
            }
            else
            {
                // G and B are the same as R
                g = r;
                b = r;
            }
            
            // Update last RGB values
            _lastR[channel] = r;
            _lastG[channel] = g;
            _lastB[channel] = b;
            
            // Pack RGB into buffer
            BitConverter.GetBytes(r).CopyTo(buffer, offset);
            BitConverter.GetBytes(g).CopyTo(buffer, offset + 2);
            BitConverter.GetBytes(b).CopyTo(buffer, offset + 4);
        }
        
        private static byte Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }
    }
}


