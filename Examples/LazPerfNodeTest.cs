using System;
using System.Linq;
using Copc.IO;
using Copc.LazPerf;
using Copc.Hierarchy;

namespace Copc.Examples
{
    /// <summary>
    /// Simple test demonstrating lazperf decompression of a single COPC node
    /// with XYZ coordinate printing
    /// </summary>
    public static class LazPerfNodeTest
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== LazPerf Node Decompression Test ===\n");
            Console.WriteLine($"File: {copcFilePath}\n");

            using var reader = CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            
            Console.WriteLine($"Point Format: {header.PointDataFormat} (base: {header.BasePointFormat})");
            Console.WriteLine($"Point Size: {header.PointDataRecordLength} bytes");
            Console.WriteLine($"Total Points: {header.ExtendedNumberOfPointRecords}");
            Console.WriteLine($"Compressed: {header.IsCompressed}");
            Console.WriteLine($"Scale: ({header.XScaleFactor}, {header.YScaleFactor}, {header.ZScaleFactor})");
            Console.WriteLine($"Offset: ({header.XOffset}, {header.YOffset}, {header.ZOffset})");
            Console.WriteLine();

            // Get the root node
            var rootPage = reader.LoadRootHierarchyPage();
            var rootNode = rootPage.Children.Values
                .OfType<Node>()
                .FirstOrDefault(n => n.Key.D == 0);

            if (rootNode == null)
            {
                Console.WriteLine("ERROR: No root node found!");
                return;
            }

            Console.WriteLine($"Root Node Key: {rootNode.Key}");
            Console.WriteLine($"Point Count: {rootNode.PointCount}");
            Console.WriteLine($"Byte Size (compressed): {rootNode.ByteSize} bytes");
            Console.WriteLine();

            // Get compressed data for this node
            var compressedData = reader.GetPointDataCompressed(rootNode);
            Console.WriteLine($"Read {compressedData.Length} bytes of compressed data");

            // Use LazPerf ChunkDecompressor to decompress
            Console.WriteLine("Opening ChunkDecompressor...");
            var decompressor = new ChunkDecompressor();
            decompressor.Open(
                header.BasePointFormat,
                header.PointDataRecordLength,
                compressedData
            );

            Console.WriteLine($"Decompressor opened successfully");
            Console.WriteLine();

            // Decompress and print points
            int pointsToShow = Math.Min(20, rootNode.PointCount);
            Console.WriteLine($"Decompressing and showing first {pointsToShow} points:");
            Console.WriteLine("-----------------------------------------------------------");

            for (int i = 0; i < pointsToShow; i++)
            {
                // Decompress one point
                var pointData = decompressor.GetPoint();

                // Unpack based on point format
                double x, y, z;
                ushort intensity;
                byte classification;

                if (header.BasePointFormat == 0)
                {
                    // Format 0 (LAS 1.0-1.2 style)
                    var point = LasPoint10.Unpack(pointData, 0);
                    
                    // Apply scale and offset
                    x = point.X * header.XScaleFactor + header.XOffset;
                    y = point.Y * header.YScaleFactor + header.YOffset;
                    z = point.Z * header.ZScaleFactor + header.ZOffset;
                    intensity = point.Intensity;
                    classification = point.Classification;
                }
                else if (header.BasePointFormat >= 6 && header.BasePointFormat <= 8)
                {
                    // Format 6-8 (LAS 1.4 style)
                    var point = LasPoint14.Unpack(pointData, 0);
                    
                    // Apply scale and offset
                    x = point.X * header.XScaleFactor + header.XOffset;
                    y = point.Y * header.YScaleFactor + header.YOffset;
                    z = point.Z * header.ZScaleFactor + header.ZOffset;
                    intensity = point.Intensity;
                    classification = point.Classification;
                }
                else
                {
                    Console.WriteLine($"Unsupported point format: {header.BasePointFormat}");
                    decompressor.Close();
                    return;
                }

                // Note: LazPerf decompression example doesn't have access to CopcPoint, so extra dimensions not shown here
                Console.WriteLine($"Point {i,3}: X={x,15:F6}  Y={y,15:F6}  Z={z,15:F6}  I={intensity,5}  C={classification,3}");
            }

            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine($"\nSuccessfully decompressed {pointsToShow} points using LazPerf!");
            Console.WriteLine($"(Node contains {rootNode.PointCount} total points)");

            // Clean up
            decompressor.Close();
            Console.WriteLine("\nDecompressor closed successfully");
            Console.WriteLine($"\nCompression ratio: {(double)(rootNode.PointCount * header.PointDataRecordLength) / compressedData.Length:F2}x");
        }
    }
}

