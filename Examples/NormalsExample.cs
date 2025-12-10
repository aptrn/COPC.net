using System;
using System.IO;
using Copc.IO;
using Copc.Cache;

namespace Examples
{
    /// <summary>
    /// Example demonstrating how to extract normals from a COPC point cloud.
    /// </summary>
    public static class NormalsExample
    {
        public static void Run(string copcFilePath)
        {
            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"File not found: {copcFilePath}");
                return;
            }

            Console.WriteLine("=== Normals Extraction Example ===\n");

            using var reader = CopcReader.Open(copcFilePath);
            var config = reader.Config;

            Console.WriteLine($"File: {Path.GetFileName(copcFilePath)}");
            Console.WriteLine($"Point Format: {config.LasHeader.PointDataFormat}");
            Console.WriteLine($"Total Points: {config.LasHeader.ExtendedNumberOfPointRecords:N0}");

            // Check if extra dimensions are available (normals would be stored here)
            if (config.ExtraDimensions != null && config.ExtraDimensions.Count > 0)
            {
                Console.WriteLine($"\nExtra Dimensions Found: {config.ExtraDimensions.Count}");
                foreach (var dim in config.ExtraDimensions)
                {
                    Console.WriteLine($"  - {dim.Name} ({dim.DataType}, {dim.GetComponentCount()} components)");
                }
            }
            else
            {
                Console.WriteLine("\nNo extra dimensions found in this file.");
            }

            // Create a cache
            var cache = PointCache.CreateWithMB(100);
            cache.SetExtraDimensions(config.ExtraDimensions);

            // Get nodes from layer 0 (root level)
            var nodes = reader.GetNodesAtLayer(0);
            if (nodes == null || nodes.Count == 0)
            {
                Console.WriteLine("No nodes found at root layer.");
                return;
            }
            var rootNode = nodes[0];

            // Load some points
            Console.WriteLine("\nLoading points from root node...");
            var points = cache.GetOrLoadPoints(rootNode, reader);
            Console.WriteLine($"Loaded {points.Length:N0} points");

            // Build separated arrays (including normals if available)
            Console.WriteLine("\nExtracting separated arrays...");
            var strideData = cache.GetOrBuildStrideCacheDataSeparated(config.ExtraDimensions);

            Console.WriteLine($"Positions: {strideData.Positions?.Length ?? 0:N0}");
            Console.WriteLine($"Colors: {strideData.Colors?.Length ?? 0:N0}");
            Console.WriteLine($"Normals: {strideData.Normals?.Length ?? 0:N0}");

            // Check if normals are available (non-empty array)
            bool hasNormals = strideData.Normals != null && strideData.Normals.Length > 0;
            
            if (hasNormals)
            {
                Console.WriteLine("\n✓ Normals are available!");
                
                // Show first few normals
                Console.WriteLine("\nFirst 5 normals:");
                int displayCount = Math.Min(5, strideData.Normals!.Length);
                for (int i = 0; i < displayCount; i++)
                {
                    var n = strideData.Normals[i];
                    Console.WriteLine($"  [{i}] ({n.X:F3}, {n.Y:F3}, {n.Z:F3})");
                }

                // Calculate some statistics
                if (strideData.Normals.Length > 0)
                {
                    double sumLength = 0;
                    for (int i = 0; i < strideData.Normals.Length; i++)
                    {
                        var n = strideData.Normals[i];
                        double length = Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
                        sumLength += length;
                    }
                    double avgLength = sumLength / strideData.Normals.Length;
                    Console.WriteLine($"\nAverage normal length: {avgLength:F4}");
                }
            }
            else
            {
                Console.WriteLine("\n✗ Normals are NOT available in this file.");
                Console.WriteLine("The normals array is empty because no normal dimensions were found.");
            }

            // Clean up
            strideData.Dispose();
            cache.Dispose();

            Console.WriteLine("\nExample completed.");
        }
    }
}

