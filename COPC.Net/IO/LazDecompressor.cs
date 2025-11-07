using System;
using System.IO;
using System.Collections.Generic;
using Copc.LazPerf;

namespace Copc.IO
{
    /// <summary>
    /// Provides point decompression capabilities for COPC files using LAZ-perf.
    /// This allows efficient decompression of individual chunks from memory.
    /// </summary>
    public class LazDecompressor
    {
        /// <summary>
        /// Decompresses a chunk of compressed point data using LAZ-perf.
        /// </summary>
        /// <param name="pointFormat">LAS point data record format</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="compressedData">Compressed chunk data</param>
        /// <param name="pointCount">Number of points in the chunk</param>
        /// <param name="header">LAS header with scale/offset information</param>
        /// <param name="extraDimensions">Optional extra dimensions to extract</param>
        /// <returns>Array of decompressed points</returns>
        public static CopcPoint[] DecompressChunk(int pointFormat, int pointSize, byte[] compressedData, int pointCount, LasHeader header, List<ExtraDimension>? extraDimensions = null)
        {
            // Use lazperf to decompress the chunk
            var decompressor = new ChunkDecompressor();
            decompressor.Open(pointFormat, pointSize, compressedData);

            var points = new CopcPoint[pointCount];
            
            // Calculate where extra bytes start
            int standardSize = GetStandardPointSize(pointFormat);
            int extraByteSize = pointSize - standardSize;
            
            for (int i = 0; i < pointCount; i++)
            {
                var pointData = decompressor.GetPoint();
                CopcPoint point;

                // Handle different point formats
                switch (pointFormat)
                {
                    case 0:
                        point = DecompressFormat0(pointData, header);
                        break;
                    
                    case 6:
                        point = DecompressFormat6(pointData, header);
                        break;
                    
                    case 7:
                        point = DecompressFormat7(pointData, header);
                        break;
                    
                    case 8:
                        point = DecompressFormat8(pointData, header);
                        break;
                    
                    default:
                        throw new NotImplementedException($"Point format {pointFormat} decompression not implemented");
                }

                // Extract extra bytes if present
                if (extraByteSize > 0 && pointData.Length > standardSize)
                {
                    point.ExtraBytes = new byte[extraByteSize];
                    Array.Copy(pointData, standardSize, point.ExtraBytes, 0, extraByteSize);
                }
                
                points[i] = point;
            }

            decompressor.Close();
            return points;
        }

        /// <summary>
        /// Gets the standard size of a point record for a given format (without extra bytes)
        /// </summary>
        private static int GetStandardPointSize(int pointFormat)
        {
            return pointFormat switch
            {
                0 => 20,
                1 => 28,
                2 => 26,
                3 => 34,
                6 => 30,
                7 => 36,
                8 => 38,
                _ => throw new NotImplementedException($"Point format {pointFormat} size not defined")
            };
        }

        private static CopcPoint DecompressFormat0(byte[] pointData, LasHeader header)
        {
            var lasPoint = LasPoint10.Unpack(pointData, 0);
            
            return new CopcPoint
            {
                X = lasPoint.X * header.XScaleFactor + header.XOffset,
                Y = lasPoint.Y * header.YScaleFactor + header.YOffset,
                Z = lasPoint.Z * header.ZScaleFactor + header.ZOffset,
                Intensity = lasPoint.Intensity,
                ReturnNumber = lasPoint.ReturnNumber,
                NumberOfReturns = lasPoint.NumberOfReturns,
                Classification = lasPoint.Classification,
                ScanAngle = lasPoint.ScanAngleRank,
                UserData = lasPoint.UserData,
                PointSourceId = lasPoint.PointSourceId,
                GpsTime = null,
                Red = null,
                Green = null,
                Blue = null
            };
        }

        private static CopcPoint DecompressFormat6(byte[] pointData, LasHeader header)
        {
            var lasPoint = LasPoint14.Unpack(pointData, 0);
            
            return new CopcPoint
            {
                X = lasPoint.X * header.XScaleFactor + header.XOffset,
                Y = lasPoint.Y * header.YScaleFactor + header.YOffset,
                Z = lasPoint.Z * header.ZScaleFactor + header.ZOffset,
                Intensity = lasPoint.Intensity,
                ReturnNumber = lasPoint.ReturnNumber,
                NumberOfReturns = lasPoint.NumberOfReturns,
                Classification = lasPoint.Classification,
                ScanAngle = lasPoint.ScanAngle * 0.006, // Convert to degrees (scan angle is in 0.006 degree units)
                UserData = lasPoint.UserData,
                PointSourceId = lasPoint.PointSourceId,
                GpsTime = lasPoint.GpsTime,
                ScanDirectionFlag = lasPoint.ScanDirectionFlag,
                EdgeOfFlightLine = lasPoint.EdgeOfFlightLine,
                Red = null,
                Green = null,
                Blue = null
            };
        }

        private static CopcPoint DecompressFormat7(byte[] pointData, LasHeader header)
        {
            var lasPoint = LasPoint14.Unpack(pointData, 0);
            var rgb = Rgb14.Unpack(pointData, 30); // RGB starts at byte 30
            
            return new CopcPoint
            {
                X = lasPoint.X * header.XScaleFactor + header.XOffset,
                Y = lasPoint.Y * header.YScaleFactor + header.YOffset,
                Z = lasPoint.Z * header.ZScaleFactor + header.ZOffset,
                Intensity = lasPoint.Intensity,
                ReturnNumber = lasPoint.ReturnNumber,
                NumberOfReturns = lasPoint.NumberOfReturns,
                Classification = lasPoint.Classification,
                ScanAngle = lasPoint.ScanAngle * 0.006,
                UserData = lasPoint.UserData,
                PointSourceId = lasPoint.PointSourceId,
                GpsTime = lasPoint.GpsTime,
                ScanDirectionFlag = lasPoint.ScanDirectionFlag,
                EdgeOfFlightLine = lasPoint.EdgeOfFlightLine,
                Red = rgb.Red,
                Green = rgb.Green,
                Blue = rgb.Blue
            };
        }

        private static CopcPoint DecompressFormat8(byte[] pointData, LasHeader header)
        {
            var lasPoint = LasPoint14.Unpack(pointData, 0);
            var rgb = Rgb14.Unpack(pointData, 30);
            var nir = Nir14.Unpack(pointData, 36); // NIR starts at byte 36
            
            return new CopcPoint
            {
                X = lasPoint.X * header.XScaleFactor + header.XOffset,
                Y = lasPoint.Y * header.YScaleFactor + header.YOffset,
                Z = lasPoint.Z * header.ZScaleFactor + header.ZOffset,
                Intensity = lasPoint.Intensity,
                ReturnNumber = lasPoint.ReturnNumber,
                NumberOfReturns = lasPoint.NumberOfReturns,
                Classification = lasPoint.Classification,
                ScanAngle = lasPoint.ScanAngle * 0.006,
                UserData = lasPoint.UserData,
                PointSourceId = lasPoint.PointSourceId,
                GpsTime = lasPoint.GpsTime,
                ScanDirectionFlag = lasPoint.ScanDirectionFlag,
                EdgeOfFlightLine = lasPoint.EdgeOfFlightLine,
                Red = rgb.Red,
                Green = rgb.Green,
                Blue = rgb.Blue,
                Nir = nir.Value
            };
        }

        /// <summary>
        /// Decompresses raw bytes from a chunk (without converting to CopcPoint).
        /// </summary>
        /// <param name="pointFormat">LAS point data record format</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="compressedData">Compressed chunk data</param>
        /// <param name="pointCount">Number of points in the chunk</param>
        /// <returns>Array of raw point data bytes</returns>
        public static byte[][] DecompressBytes(int pointFormat, int pointSize, byte[] compressedData, int pointCount)
        {
            return ChunkDecompressor.DecompressChunk(pointFormat, pointSize, compressedData, pointCount);
        }

        /// <summary>
        /// Decompresses raw bytes from a chunk into a flat array (without converting to CopcPoint).
        /// </summary>
        /// <param name="pointFormat">LAS point data record format</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="compressedData">Compressed chunk data</param>
        /// <param name="pointCount">Number of points in the chunk</param>
        /// <returns>Flat array of raw point data bytes</returns>
        public static byte[] DecompressBytesFlat(int pointFormat, int pointSize, byte[] compressedData, int pointCount)
        {
            return ChunkDecompressor.DecompressChunkFlat(pointFormat, pointSize, compressedData, pointCount);
        }
    }

    /// <summary>
    /// Represents a single point in a COPC point cloud.
    /// </summary>
    public class CopcPoint
    {
        // Coordinates (always present)
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // Intensity (always present)
        public ushort Intensity { get; set; }

        // Return information (always present)
        public byte ReturnNumber { get; set; }
        public byte NumberOfReturns { get; set; }
        public bool ScanDirectionFlag { get; set; }
        public bool EdgeOfFlightLine { get; set; }

        // Classification (always present)
        public byte Classification { get; set; }

        // Scan angle (always present, but format varies)
        public double ScanAngle { get; set; }

        // User data (always present)
        public byte UserData { get; set; }

        // Point source ID (always present)
        public ushort PointSourceId { get; set; }

        // GPS time (format dependent)
        public double? GpsTime { get; set; }

        // RGB (format dependent)
        public ushort? Red { get; set; }
        public ushort? Green { get; set; }
        public ushort? Blue { get; set; }

        // NIR (format dependent)
        public ushort? Nir { get; set; }

        // Extra bytes (format dependent)
        public byte[]? ExtraBytes { get; set; }

        public override string ToString()
        {
            return $"Point({X:F3}, {Y:F3}, {Z:F3}) I={Intensity} C={Classification}";
        }
    }
}

