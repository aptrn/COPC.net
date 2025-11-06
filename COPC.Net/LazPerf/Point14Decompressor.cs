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
        private class ChannelContext
        {
            // Models for various fields
            public List<ArithmeticModel> ChangedValuesModel;
            public ArithmeticModel ScannerChannelModel;
            public List<ArithmeticModel> NumberOfReturnsModel;
            public List<ArithmeticModel> ReturnNumberModel;
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
            }
        }

        private readonly ArithmeticDecoder _decoder;
        private readonly List<ChannelContext> _contexts;
        private int _lastChannel;

        public Point14Decompressor(ArithmeticDecoder decoder)
        {
            _decoder = decoder;
            _contexts = new List<ChannelContext>();
            
            // Initialize contexts for up to 4 scanner channels
            for (int i = 0; i < 4; i++)
                _contexts.Add(new ChannelContext());
            
            _lastChannel = 0;
        }

        public byte[] Decompress()
        {
            // Determine scanner channel
            int channel = 0;
            if (_contexts[_lastChannel].HaveLast)
            {
                uint sym = _decoder.DecodeSymbol(_contexts[_lastChannel].ScannerChannelModel);
                if (sym == 1)
                    channel = (_lastChannel + 1) & 0x03;
                else if (sym == 2)
                    channel = (_lastChannel + 2) & 0x03;
                else
                    channel = _lastChannel;
            }

            var ctx = _contexts[channel];
            var point = new LasPoint14();

            if (!ctx.HaveLast)
            {
                // First point - read uncompressed
                point.X = (int)_decoder.ReadInt();
                point.Y = (int)_decoder.ReadInt();
                point.Z = (int)_decoder.ReadInt();
                point.Intensity = _decoder.ReadShort();
                point.Returns = _decoder.ReadByte();
                point.Flags = _decoder.ReadByte();
                point.Classification = _decoder.ReadByte();
                point.UserData = _decoder.ReadByte();
                point.ScanAngle = (short)_decoder.ReadShort();
                point.PointSourceId = _decoder.ReadShort();
                point.GpsTime = _decoder.ReadDouble();
                
                // Initialize last values
                for (int i = 0; i < 8; i++)
                {
                    ctx.LastIntensity[i] = point.Intensity;
                    ctx.LastZ[i] = point.Z;
                }

                ctx.Last = point;
                ctx.HaveLast = true;
            }
            else
            {
                // Decompress changed values
                uint changedValues = _decoder.DecodeSymbol(ctx.ChangedValuesModel[0]);

                // Decompress X
                int medianContext = 0;
                int diffX = ctx.DxDecomp.Decompress(_decoder, 0, (uint)medianContext);
                point.X = ctx.Last.X + diffX;

                // Decompress Y
                int k = (ctx.DxDecomp.GetK() < 20) ? (int)ctx.DxDecomp.GetK() : 19;
                int diffY = ctx.DyDecomp.Decompress(_decoder, 0, (uint)k);
                point.Y = ctx.Last.Y + diffY;

                // Decompress Z
                k = ((ctx.DxDecomp.GetK() + ctx.DyDecomp.GetK()) / 2) < 20 ? 
                    (int)((ctx.DxDecomp.GetK() + ctx.DyDecomp.GetK()) / 2) : 19;
                int diffZ = ctx.ZDecomp.Decompress(_decoder, ctx.Last.Z, (uint)k);
                point.Z = diffZ;

                // Decompress intensity
                uint intensityContext = (changedValues >> 5) & 0x03;
                point.Intensity = (ushort)ctx.IntensityDecomp.Decompress(_decoder, ctx.LastIntensity[intensityContext], intensityContext);
                ctx.LastIntensity[intensityContext] = point.Intensity;

                // Decompress returns
                uint rnContext = ctx.Last.ReturnNumber;
                if (rnContext > 15) rnContext = 15;
                point.Returns = (byte)_decoder.DecodeSymbol(ctx.ReturnNumberModel[(int)rnContext]);
                
                uint nrContext = point.ReturnNumber;
                if (nrContext > 15) nrContext = 15;
                byte numReturns = (byte)_decoder.DecodeSymbol(ctx.NumberOfReturnsModel[(int)nrContext]);
                point.Returns = (byte)((numReturns << 4) | point.ReturnNumber);

                // Decompress flags
                uint classContext = (uint)ctx.Last.Classification;
                if (classContext > 31) classContext = 31;
                point.Flags = (byte)_decoder.DecodeSymbol(ctx.FlagsModel[(int)classContext]);

                // Decompress classification
                uint flagsContext = ((uint)point.Flags >> 4) | ((point.ScanDirectionFlag ? 1u : 0u) << 2);
                if (flagsContext > 31) flagsContext = 31;
                point.Classification = (byte)_decoder.DecodeSymbol(ctx.ClassificationModel[(int)flagsContext]);

                // Decompress user data
                uint userContext = (uint)point.Classification;
                if (userContext > 31) userContext = 31;
                point.UserData = (byte)_decoder.DecodeSymbol(ctx.UserDataModel[(int)userContext]);

                // Decompress scan angle
                uint angleContext = (changedValues >> 7) & 0x01;
                point.ScanAngle = (short)ctx.ScanAngleDecomp.Decompress(_decoder, ctx.Last.ScanAngle, angleContext);

                // Decompress point source ID
                point.PointSourceId = (ushort)ctx.PointSourceIdDecomp.Decompress(_decoder, ctx.Last.PointSourceId, 0);

                // Decompress GPS time
                DecompressGpsTime(ctx, point);

                ctx.Last = point;
            }

            _lastChannel = channel;

            // Pack point to bytes
            byte[] result = new byte[30]; // Format 6 base size
            point.Pack(result, 0);
            return result;
        }

        private void DecompressGpsTime(ChannelContext ctx, LasPoint14 point)
        {
            // Simplified GPS time decompression
            uint multi = _decoder.DecodeSymbol(ctx.GpsTimeMultiModel);
            
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
                int diff = ctx.GpsTimeDecomp.Decompress(_decoder, ctx.LastGpsTimeDiff[0], 0);
                long gpsInt = BitConverter.DoubleToInt64Bits(ctx.Last.GpsTime) + diff;
                point.GpsTime = BitConverter.Int64BitsToDouble(gpsInt);
                ctx.LastGpsTimeDiff[0] = diff;
            }

            ctx.LastGpsTime[0] = point.GpsTime;
        }
    }
}

