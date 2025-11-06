using System;
using System.IO;
using System.Collections.Generic;
using LasZipNative = Kuoste.LasZipNetStandard.LasZip;
using LasPointNative = Kuoste.LasZipNetStandard.LasPoint;

namespace Copc.IO
{
    /// <summary>
    /// Provides point decompression capabilities for COPC files using native LASzip library.
    /// Note: This reads points sequentially from the file, not individual chunks from memory.
    /// </summary>
    public class LazDecompressor
    {
        /// <summary>
        /// Decompresses all points from a COPC file.
        /// Note: This reads all points sequentially. For large files, consider using maxPoints parameter.
        /// </summary>
        /// <param name="filePath">Path to the COPC file</param>
        /// <param name="maxPoints">Maximum number of points to read (-1 for all)</param>
        /// <returns>Array of decompressed points</returns>
        public static CopcPoint[] DecompressFromFile(string filePath, int maxPoints = -1)
        {
            var lasZip = new LasZipNative(out _);
            
            if (!lasZip.OpenReader(filePath))
            {
                throw new InvalidOperationException("Failed to open COPC file");
            }
            
            try
            {
                var header = lasZip.GetReaderHeader();
                ulong totalPoints = Math.Max(header.NumberOfPointRecords, header.ExtendedNumberOfPointRecords);
                int pointsToRead = maxPoints < 0 ? (int)Math.Min(totalPoints, int.MaxValue) : maxPoints;
                
                var points = new CopcPoint[pointsToRead];
                var lasPoint = new LasPointNative();
                
                for (int i = 0; i < pointsToRead; i++)
                {
                    lasZip.ReadPoint(ref lasPoint);
                    
                    points[i] = new CopcPoint
                    {
                        X = lasPoint.X,
                        Y = lasPoint.Y,
                        Z = lasPoint.Z,
                        Intensity = lasPoint.Intensity,
                        ReturnNumber = (byte)lasPoint.ReturnNumber,
                        NumberOfReturns = (byte)lasPoint.NumberOfReturns,
                        Classification = (byte)lasPoint.Classification,
                        ScanAngle = lasPoint.ScanAngleRank,
                        UserData = (byte)lasPoint.UserData,
                        PointSourceId = lasPoint.PointSourceId,
                        GpsTime = lasPoint.GpsTime,
                        Red = (ushort)lasPoint.Red,
                        Green = (ushort)lasPoint.Green,
                        Blue = (ushort)lasPoint.Blue
                    };
                }
                
                return points;
            }
            finally
            {
                lasZip.CloseReader();
                lasZip.DestroyReader();
            }
        }

        /// <summary>
        /// Direct chunk decompression is not supported.
        /// The native library reads points sequentially from the file.
        /// Use DecompressFromFile() to read points from the entire COPC file.
        /// </summary>
        public static byte[] DecompressBytes(byte[] compressedData, LasHeader header, int pointCount = -1)
        {
            throw new NotSupportedException(
                "Direct chunk decompression from memory is not supported by the native LASzip library. " +
                "Use LazDecompressor.DecompressFromFile(filePath, maxPoints) to read points from the file.");
        }

        /// <summary>
        /// Decompresses points from file and returns them as an array of Point objects.
        /// This is the same as DecompressFromFile().
        /// </summary>
        public static CopcPoint[] DecompressPoints(string filePath, int maxPoints = -1)
        {
            return DecompressFromFile(filePath, maxPoints);
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

