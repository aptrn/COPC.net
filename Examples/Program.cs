using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc.Hierarchy;
using CopcBox = Copc.Geometry.Box;
using Stride.Core.Mathematics;

namespace Copc.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== COPC.Net Examples ===\n");
            
            // Handle global flags (e.g., --debug) and normalize args
            var argList = new List<string>(args);
            bool debug = false;
            for (int i = argList.Count - 1; i >= 0; i--)
            {
                var a = argList[i];
                if (string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-d", StringComparison.OrdinalIgnoreCase))
                {
                    debug = true;
                    argList.RemoveAt(i);
                }
            }
            Copc.Utils.DebugConfig.LazPerfDebug = debug;

            args = argList.ToArray();

            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            string command = args[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "traversal-presets":
                        // traversal-presets <file>
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples traversal-presets <copc-file>");
                            return;
                        }
                        TraversalPresetsExample.Run(args[1]);
                        break;
                    case "cache-update-bench":
                        // cache-update-bench <file> [cacheMB]
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples cache-update-bench <copc-file> [cacheMB]");
                            return;
                        }
                        int cacheMb = args.Length >= 3 ? int.Parse(args[2]) : 512;
                        CacheUpdateBenchmark.Run(args[1], cacheMb);
                        break;

                    case "random":
                        // random <file> <lod>
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: Examples random <copc-file> <lod-depth>");
                            return;
                        }
                        RandomBoundingBoxExample(args[1], int.Parse(args[2]));
                        break;

                    case "bbox-lod":
                        // bbox-lod <file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
                        if (args.Length < 9)
                        {
                            Console.WriteLine("Usage: Examples bbox-lod <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                            return;
                        }
                        BoundingBoxWithLodExample(args[1], int.Parse(args[2]), 
                            double.Parse(args[3]), double.Parse(args[4]), double.Parse(args[5]),
                            double.Parse(args[6]), double.Parse(args[7]), double.Parse(args[8]));
                        break;

                    case "bbox-res":
                        // bbox-res <file> <resolution> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
                        if (args.Length < 9)
                        {
                            Console.WriteLine("Usage: Examples bbox-res <copc-file> <resolution> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                            return;
                        }
                        BoundingBoxWithResolutionExample(args[1], double.Parse(args[2]),
                            double.Parse(args[3]), double.Parse(args[4]), double.Parse(args[5]),
                            double.Parse(args[6]), double.Parse(args[7]), double.Parse(args[8]));
                        break;

                    case "bbox-info":
                        // bbox-info <file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
                        if (args.Length < 8)
                        {
                            Console.WriteLine("Usage: Examples bbox-info <copc-file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                            return;
                        }
                        BoundingBoxInfoExample(args[1],
                            double.Parse(args[2]), double.Parse(args[3]), double.Parse(args[4]),
                            double.Parse(args[5]), double.Parse(args[6]), double.Parse(args[7]));
                        break;

                    case "preprocess":
                        // preprocess <file> <cache-dir>
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: Examples preprocess <copc-file> <cache-dir>");
                            return;
                        }
                        PreprocessChunks(args[1], args[2]);
                        break;

                    case "bbox-chunked":
                        // bbox-chunked <file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
                        if (args.Length < 9)
                        {
                            Console.WriteLine("Usage: Examples bbox-chunked <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                            return;
                        }
                        BoundingBoxChunkedExample(args[1], int.Parse(args[2]),
                            double.Parse(args[3]), double.Parse(args[4]), double.Parse(args[5]),
                            double.Parse(args[6]), double.Parse(args[7]), double.Parse(args[8]));
                        break;

                    case "frustum-chunked":
                        // frustum-chunked <file> <resolution> <m00> <m01> ... <m33>
                        if (args.Length < 19)
                        {
                            Console.WriteLine("Usage: Examples frustum-chunked <copc-file> <resolution> <m00> <m01> <m02> <m03> <m10> <m11> <m12> <m13> <m20> <m21> <m22> <m23> <m30> <m31> <m32> <m33>");
                            Console.WriteLine("\nMatrix elements should be provided in row-major order (16 values)");
                            return;
                        }
                        // Parse resolution and matrix elements
                        double resolution = double.Parse(args[2]);
                        double[] matrix = new double[16];
                        for (int i = 0; i < 16; i++)
                        {
                            matrix[i] = double.Parse(args[3 + i]);
                        }
                        FrustumChunkedExample(args[1], matrix, resolution);
                        break;

                    case "frustum-test":
                        // frustum-test - Run frustum unit tests
                        FrustumTests.RunAllTests();
                        break;

                    case "layer-bbox":
                        // layer-bbox <file> <layer>
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: Examples layer-bbox <copc-file> <layer>");
                            return;
                        }
                        LayerBoundingBoxExample.Run(args[1], int.Parse(args[2]));
                        break;

                    case "layer-compare":
                        // layer-compare <file> <layer1> <layer2> ... <layerN>
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: Examples layer-compare <copc-file> <layer1> <layer2> ... <layerN>");
                            return;
                        }
                        int[] layers = new int[args.Length - 2];
                        for (int i = 2; i < args.Length; i++)
                        {
                            layers[i - 2] = int.Parse(args[i]);
                        }
                        LayerBoundingBoxExample.CompareMultipleLayers(args[1], layers);
                        break;

                    case "radius-basic":
                        // radius-basic <file> <centerX> <centerY> <centerZ> <radius>
                        if (args.Length < 6)
                        {
                            Console.WriteLine("Usage: Examples radius-basic <copc-file> <centerX> <centerY> <centerZ> <radius>");
                            return;
                        }
                        RadiusQueryExample.BasicRadiusQuery(args[1]);
                        break;

                    case "radius":
                        // radius <file> <centerX> <centerY> <centerZ> <radius> [resolution]
                        if (args.Length < 6)
                        {
                            Console.WriteLine("Usage: Examples radius <copc-file> <centerX> <centerY> <centerZ> <radius> [resolution]");
                            return;
                        }
                        using (var reader = IO.CopcReader.Open(args[1]))
                        {
                            double centerX = double.Parse(args[2]);
                            double centerY = double.Parse(args[3]);
                            double centerZ = double.Parse(args[4]);
                            double radius = double.Parse(args[5]);
                            double targetResolution = args.Length > 6 ? double.Parse(args[6]) : 0;
                            
                            var nodes = reader.GetNodesWithinRadius(centerX, centerY, centerZ, radius, targetResolution);
                            Console.WriteLine($"Found {nodes.Count} nodes within {radius}m of ({centerX}, {centerY}, {centerZ})");
                            Console.WriteLine($"Total points: {nodes.Sum(n => (long)n.PointCount):N0}");
                            
                            if (nodes.Count > 0 && nodes.Count <= 10)
                            {
                                Console.WriteLine("\nNode details:");
                                foreach (var node in nodes)
                                {
                                    var bounds = node.Key.GetBounds(reader.Config.LasHeader, reader.Config.CopcInfo);
                                    Console.WriteLine($"  {node.Key}: {node.PointCount} points, bounds: {bounds}");
                                }
                            }

                            // Decompress and print points
                            if (nodes.Count > 0)
                            {
                                Console.WriteLine("\n=== Decompressing Points ===");
                                long totalPointsInNodes = nodes.Sum(n => (long)n.PointCount);
                                Console.WriteLine($"Decompressing {nodes.Count} nodes ({totalPointsInNodes:N0} points)...\n");
                                
                                var allPoints = reader.GetPointsFromNodes(nodes);
                                Console.WriteLine($"Decompressed {allPoints.Length:N0} points");

                                // Filter by actual distance
                                var sphere = new BoundingSphere(new Vector3((float)centerX, (float)centerY, (float)centerZ), (float)radius);
                                var pointsInRadius = allPoints.Where(p =>
                                {
                                    double dx = p.X - centerX;
                                    double dy = p.Y - centerY;
                                    double dz = p.Z - centerZ;
                                    double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                    return distance <= radius;
                                }).ToArray();

                                Console.WriteLine($"Points within radius: {pointsInRadius.Length:N0}\n");

                                if (pointsInRadius.Length > 0)
                                {
                                    int pointsToPrint = Math.Min(20, pointsInRadius.Length);
                                    Console.WriteLine($"Showing first {pointsToPrint} points:\n");

                    for (int i = 0; i < pointsToPrint; i++)
                    {
                        var p = pointsInRadius[i];
                        double dx = p.X - centerX;
                        double dy = p.Y - centerY;
                        double dz = p.Z - centerZ;
                        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        
                        PointPrintHelper.PrintPointWithDistance(i, p, distance, reader.Config.ExtraDimensions);
                    }
                                    Console.WriteLine("\n✅ Complete!");
                                }
                            }
                        }
                        break;

                    case "radius-compare":
                        // radius-compare <file> <centerX> <centerY> <centerZ> <radius>
                        if (args.Length < 6)
                        {
                            Console.WriteLine("Usage: Examples radius-compare <copc-file> <centerX> <centerY> <centerZ> <radius>");
                            return;
                        }
                        RadiusQueryExample.CompareBoxVsRadiusQuery(args[1]);
                        break;

                    case "lazperf-test":
                        // lazperf-test <file>
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples lazperf-test <copc-file>");
                            return;
                        }
                        LazPerfNodeTest.Run(args[1]);
                        break;

                    case "chunk-decompress":
                        // chunk-decompress <file>
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples chunk-decompress <copc-file>");
                            return;
                        }
                        ChunkDecompressionExample.Run(args[1]);
                        break;

                    case "cache":
                        // cache <file>
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples cache <copc-file>");
                            return;
                        }
                        CacheExample.Run(args[1]);
                        break;

                    case "cache-heavy":
                        // cache-heavy <file> [passes]
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples cache-heavy <copc-file> [passes]");
                            return;
                        }
                        int passes = args.Length >= 3 ? int.Parse(args[2]) : 3;
                        CacheHeavyExample.Run(args[1], passes);
                        break;

                    case "stride":
                        // stride <file>
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: Examples stride <copc-file>");
                            return;
                        }
                        StrideFormatExample.Run(args[1]);
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        PrintUsage();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  Examples traversal-presets <copc-file>");
            Console.WriteLine("  Examples random <copc-file> <lod-depth>");
            Console.WriteLine("  Examples bbox-lod <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples bbox-res <copc-file> <resolution> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples bbox-info <copc-file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples preprocess <copc-file> <cache-dir>");
            Console.WriteLine("  Examples bbox-chunked <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples frustum-chunked <copc-file> <resolution> <m00> ... <m33>");
            Console.WriteLine("  Examples frustum-test");
            Console.WriteLine("  Examples layer-bbox <copc-file> <layer>");
            Console.WriteLine("  Examples layer-compare <copc-file> <layer1> <layer2> ... <layerN>");
            Console.WriteLine("  Examples radius <copc-file> <centerX> <centerY> <centerZ> <radius> [resolution]");
            Console.WriteLine("  Examples radius-compare <copc-file> <centerX> <centerY> <centerZ> <radius>");
            Console.WriteLine("  Examples lazperf-test <copc-file>");
            Console.WriteLine("  Examples chunk-decompress <copc-file>");
            Console.WriteLine("  Examples cache <copc-file>");
            Console.WriteLine("  Examples cache-heavy <copc-file> [passes]");
            Console.WriteLine("  Examples stride <copc-file>");
            Console.WriteLine("  Examples cache-update-bench <copc-file> [cacheMB]");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  Examples random data.copc.laz 5");
            Console.WriteLine("  Examples bbox-lod data.copc.laz 5 -10 -10 0 10 10 50");
            Console.WriteLine("  Examples bbox-res data.copc.laz 0.1 -10 -10 0 10 10 50");
            Console.WriteLine("  Examples bbox-info data.copc.laz -10 -10 0 10 10 50");
            Console.WriteLine("  Examples preprocess data.copc.laz cache/");
            Console.WriteLine("  Examples bbox-chunked data.copc.laz 3 -10 -10 0 10 10 50");
            Console.WriteLine("  Examples frustum-chunked data.copc.laz 0.1 1 0 0 0 0 1 0 0 0 0 -1 -10 0 0 -1 0");
            Console.WriteLine("  Examples frustum-test");
            Console.WriteLine("  Examples layer-bbox data.copc.laz 2");
            Console.WriteLine("  Examples layer-compare data.copc.laz 0 1 2 3 4");
            Console.WriteLine("  Examples radius data.copc.laz 500 500 50 100");
            Console.WriteLine("  Examples radius data.copc.laz 500 500 50 100 0.1");
            Console.WriteLine("  Examples radius-compare data.copc.laz 500 500 50 100");
            Console.WriteLine("  Examples lazperf-test data.copc.laz");
            Console.WriteLine("  Examples chunk-decompress data.copc.laz");
            Console.WriteLine("  Examples cache data.copc.laz");
            Console.WriteLine("  Examples stride data.copc.laz");
            Console.WriteLine("\nCommands:");
            Console.WriteLine("  random          - Pick a random bounding box at specified LOD and print points");
            Console.WriteLine("  bbox-lod        - Query specific bounding box at specific LOD and print points");
            Console.WriteLine("  bbox-res        - Query specific bounding box at specific resolution and print points");
            Console.WriteLine("  bbox-info       - Show information about a bounding box across all LODs and print sample points");
            Console.WriteLine("  preprocess      - Extract all node chunks to cache directory (optional)");
            Console.WriteLine("  bbox-chunked    - Query bounding box and decompress/print points");
            Console.WriteLine("  frustum-chunked - Query frustum (camera view) and decompress/print points");
            Console.WriteLine("  frustum-test    - Run frustum functionality tests");
            Console.WriteLine("  layer-bbox      - Get bounding boxes for all nodes at a specific layer and print sample points");
            Console.WriteLine("  layer-compare   - Compare node statistics across multiple layers and print sample points");
            Console.WriteLine("  radius          - Query nodes within a spherical radius from a point and print points");
            Console.WriteLine("  radius-compare  - Compare box vs radius query efficiency and print points");
            Console.WriteLine("  lazperf-test    - Test lazperf decompression on root node and print XYZ coords");
            Console.WriteLine("  chunk-decompress - Comprehensive chunk decompression examples");
            Console.WriteLine("  cache           - Demonstrate smart caching system for efficient data access");
            Console.WriteLine("  cache-heavy     - Stress test cache with 8GB and repeated loads");
            Console.WriteLine("  stride          - Export cached data in Stride engine format (Vector4 positions/colors)");
            Console.WriteLine("  cache-update-bench - Benchmark cache Update() + separated data retrieval across phases");
        }

        static void RandomBoundingBoxExample(string copcFilePath, int targetLod)
        {
            // Validate file exists
            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            Console.WriteLine($"Opening COPC file: {copcFilePath}");
            Console.WriteLine($"Target LOD (depth): {targetLod}\n");

            // Open the COPC file
            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            // Read the actual bounds of the entire point cloud from the header
            double cloudMinX = header.MinX;
            double cloudMaxX = header.MaxX;
            double cloudMinY = header.MinY;
            double cloudMaxY = header.MaxY;
            double cloudMinZ = header.MinZ;
            double cloudMaxZ = header.MaxZ;

            // Display the maximum bounds of the point cloud
            Console.WriteLine("Point Cloud Maximum Bounds:");
            Console.WriteLine($"  X: [{cloudMinX:F3}, {cloudMaxX:F3}] (range: {cloudMaxX - cloudMinX:F3})");
            Console.WriteLine($"  Y: [{cloudMinY:F3}, {cloudMaxY:F3}] (range: {cloudMaxY - cloudMinY:F3})");
            Console.WriteLine($"  Z: [{cloudMinZ:F3}, {cloudMaxZ:F3}] (range: {cloudMaxZ - cloudMinZ:F3})");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Get resolution at target LOD
            var resolution = VoxelKey.GetResolutionAtDepth(targetLod, header, info);
            Console.WriteLine($"Resolution at LOD {targetLod}: {resolution:F6}\n");

            // Generate random bounding box (10% of cloud extent) WITHIN the maximum bounds
            var random = new Random();
            double cloudRangeX = cloudMaxX - cloudMinX;
            double cloudRangeY = cloudMaxY - cloudMinY;
            double cloudRangeZ = cloudMaxZ - cloudMinZ;

            // Bounding box size is 10% of the cloud extent in each dimension
            double boxSizeX = cloudRangeX * 0.1;
            double boxSizeY = cloudRangeY * 0.1;
            double boxSizeZ = cloudRangeZ * 0.1;

            // Generate random position ensuring the box stays completely within cloud bounds
            double bboxMinX = cloudMinX + random.NextDouble() * (cloudRangeX - boxSizeX);
            double bboxMinY = cloudMinY + random.NextDouble() * (cloudRangeY - boxSizeY);
            double bboxMinZ = cloudMinZ + random.NextDouble() * (cloudRangeZ - boxSizeZ);

            double bboxMaxX = bboxMinX + boxSizeX;
            double bboxMaxY = bboxMinY + boxSizeY;
            double bboxMaxZ = bboxMinZ + boxSizeZ;

            // Validate the bounding box is within cloud bounds
            if (bboxMinX < cloudMinX || bboxMaxX > cloudMaxX ||
                bboxMinY < cloudMinY || bboxMaxY > cloudMaxY ||
                bboxMinZ < cloudMinZ || bboxMaxZ > cloudMaxZ)
            {
                Console.WriteLine("⚠️  ERROR: Generated bounding box exceeds cloud bounds!");
                return;
            }

            var bbox = new CopcBox(bboxMinX, bboxMinY, bboxMinZ, bboxMaxX, bboxMaxY, bboxMaxZ);

            Console.WriteLine("Random Bounding Box (within maximum bounds):");
            Console.WriteLine($"  Min: ({bbox.Min.X:F3}, {bbox.Min.Y:F3}, {bbox.Min.Z:F3})");
            Console.WriteLine($"  Max: ({bbox.Max.X:F3}, {bbox.Max.Y:F3}, {bbox.Max.Z:F3})");
            Console.WriteLine($"  Size: {boxSizeX:F3} x {boxSizeY:F3} x {boxSizeZ:F3}");
            Console.WriteLine($"  ✓ Bounding box is completely within cloud bounds\n");

            // Get all nodes at target LOD
            var allNodesAtLod = reader.GetNodesAtResolution(resolution);
            Console.WriteLine($"Total nodes at LOD {targetLod}: {allNodesAtLod.Count}");

            // Filter nodes that intersect the bounding box
            var matchingNodes = allNodesAtLod.Where(n => 
            {
                var nodeBounds = n.Key.GetBounds(header, info);
                return nodeBounds.Intersects(bbox);
            }).ToList();
            
            Console.WriteLine($"Nodes intersecting bounding box: {matchingNodes.Count}");

            if (matchingNodes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No nodes found in the random bounding box. Try running again with a different LOD or larger bbox!");
                return;
            }

            // Calculate total points in matching nodes
            long totalPointsInNodes = matchingNodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in matching nodes: {totalPointsInNodes:N0}\n");

            // Decompress points from relevant nodes using LAZ-perf
            Console.WriteLine("=== Decompressing Points ===");
            Console.WriteLine("Using LAZ-perf to decompress only relevant node chunks...\n");

            // Get points from matching nodes only (much more efficient!)
            long totalPoints = (long)header.ExtendedNumberOfPointRecords;
            Console.WriteLine($"Decompressing {matchingNodes.Count} nodes ({totalPointsInNodes:N0} points) from file (total: {totalPoints:N0})...");
            
            var allPoints = reader.GetPointsFromNodes(matchingNodes);
            Console.WriteLine($"Decompressed {allPoints.Length:N0} points from {matchingNodes.Count} nodes");

            // Filter points to those within the bounding box
            var pointsInBox = allPoints.Where(p => 
                p.X >= bbox.Min.X && p.X <= bbox.Max.X &&
                p.Y >= bbox.Min.Y && p.Y <= bbox.Max.Y &&
                p.Z >= bbox.Min.Z && p.Z <= bbox.Max.Z).ToArray();
            
            Console.WriteLine($"Points within bounding box: {pointsInBox.Length:N0}\n");

            if (pointsInBox.Length == 0)
            {
                Console.WriteLine("⚠️  No points found within the bounding box.");
                return;
            }

            // Print first 20 points in the bounding box
            Console.WriteLine("=== Points in Bounding Box ===");
            int pointsToPrint = Math.Min(20, pointsInBox.Length);
            Console.WriteLine($"Showing first {pointsToPrint} points:\n");

            for (int i = 0; i < pointsToPrint; i++)
            {
                var p = pointsInBox[i];
                PointPrintHelper.PrintPointWithRGB(i, p, reader.Config.ExtraDimensions);
            }

            // Print statistics
            Console.WriteLine($"\n=== Statistics ===");
            Console.WriteLine($"X range: [{pointsInBox.Min(p => p.X):F3}, {pointsInBox.Max(p => p.X):F3}]");
            Console.WriteLine($"Y range: [{pointsInBox.Min(p => p.Y):F3}, {pointsInBox.Max(p => p.Y):F3}]");
            Console.WriteLine($"Z range: [{pointsInBox.Min(p => p.Z):F3}, {pointsInBox.Max(p => p.Z):F3}]");
            Console.WriteLine($"Average intensity: {pointsInBox.Average(p => p.Intensity):F1}");
            
            var classifications = pointsInBox.GroupBy(p => p.Classification)
                .Select(g => new { Class = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);
            
            Console.WriteLine($"\nClassification breakdown:");
            foreach (var classGroup in classifications.Take(5))
            {
                Console.WriteLine($"  Class {classGroup.Class}: {classGroup.Count:N0} points ({100.0 * classGroup.Count / pointsInBox.Length:F1}%)");
            }

            Console.WriteLine("\n✅ Complete!");
        }

        static void BoundingBoxWithLodExample(string copcFilePath, int targetLod, 
            double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine("=== Bounding Box Query with LOD ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Target LOD: {targetLod}");
            Console.WriteLine($"Bounding Box: [{minX:F3}, {minY:F3}, {minZ:F3}] to [{maxX:F3}, {maxY:F3}, {maxZ:F3}]\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            var bbox = new CopcBox(minX, minY, minZ, maxX, maxY, maxZ);

            // Display cloud bounds
            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Get resolution at target LOD
            var resolution = VoxelKey.GetResolutionAtDepth(targetLod, header, info);
            Console.WriteLine($"Resolution at LOD {targetLod}: {resolution:F6}\n");

            // Get all nodes at target LOD that intersect the bounding box
            var allNodesAtLod = reader.GetNodesAtResolution(resolution);
            var matchingNodes = allNodesAtLod.Where(n =>
            {
                var nodeBounds = n.Key.GetBounds(header, info);
                return nodeBounds.Intersects(bbox);
            }).ToList();

            Console.WriteLine($"Total nodes at LOD {targetLod}: {allNodesAtLod.Count}");
            Console.WriteLine($"Nodes intersecting bounding box: {matchingNodes.Count}");

            if (matchingNodes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No nodes found in the bounding box at this LOD.");
                return;
            }

            long totalPointsInNodes = matchingNodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in matching nodes: {totalPointsInNodes:N0}\n");

            // Show details of each matching node
            Console.WriteLine($"=== Matching Nodes ===");
            for (int i = 0; i < Math.Min(10, matchingNodes.Count); i++)
            {
                var node = matchingNodes[i];
                var nodeBounds = node.Key.GetBounds(header, info);
                Console.WriteLine($"Node {i + 1}: {node.Key}");
                Console.WriteLine($"  Point count: {node.PointCount:N0}");
                Console.WriteLine($"  Bounds: Min({nodeBounds.Min.X:F2},{nodeBounds.Min.Y:F2},{nodeBounds.Min.Z:F2}) Max({nodeBounds.Max.X:F2},{nodeBounds.Max.Y:F2},{nodeBounds.Max.Z:F2})");
            }
            if (matchingNodes.Count > 10)
            {
                Console.WriteLine($"... and {matchingNodes.Count - 10} more nodes");
            }

            // Decompress and filter points
            QueryAndPrintPoints(reader, header, bbox, totalPointsInNodes, matchingNodes);
        }

        static void BoundingBoxWithResolutionExample(string copcFilePath, double targetResolution,
            double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine("=== Bounding Box Query with Resolution ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Target Resolution: {targetResolution:F6}");
            Console.WriteLine($"Bounding Box: [{minX:F3}, {minY:F3}, {minZ:F3}] to [{maxX:F3}, {maxY:F3}, {maxZ:F3}]\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            var bbox = new CopcBox(minX, minY, minZ, maxX, maxY, maxZ);

            // Display cloud bounds
            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Find the LOD that matches this resolution
            int targetLod = reader.GetDepthAtResolution(targetResolution);
            var actualResolution = VoxelKey.GetResolutionAtDepth(targetLod, header, info);
            
            Console.WriteLine($"Calculated LOD for resolution {targetResolution:F6}: {targetLod}");
            Console.WriteLine($"Actual resolution at LOD {targetLod}: {actualResolution:F6}\n");

            // Get all nodes at this resolution that intersect the bounding box
            var allNodesAtResolution = reader.GetNodesAtResolution(actualResolution);
            var matchingNodes = allNodesAtResolution.Where(n =>
            {
                var nodeBounds = n.Key.GetBounds(header, info);
                return nodeBounds.Intersects(bbox);
            }).ToList();

            Console.WriteLine($"Total nodes at resolution {actualResolution:F6}: {allNodesAtResolution.Count}");
            Console.WriteLine($"Nodes intersecting bounding box: {matchingNodes.Count}");

            if (matchingNodes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No nodes found in the bounding box at this resolution.");
                return;
            }

            long totalPointsInNodes = matchingNodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in matching nodes: {totalPointsInNodes:N0}\n");

            // Show details of each matching node
            Console.WriteLine($"=== Matching Nodes ===");
            for (int i = 0; i < Math.Min(10, matchingNodes.Count); i++)
            {
                var node = matchingNodes[i];
                var nodeBounds = node.Key.GetBounds(header, info);
                Console.WriteLine($"Node {i + 1}: {node.Key}");
                Console.WriteLine($"  Point count: {node.PointCount:N0}");
                Console.WriteLine($"  Bounds: Min({nodeBounds.Min.X:F2},{nodeBounds.Min.Y:F2},{nodeBounds.Min.Z:F2}) Max({nodeBounds.Max.X:F2},{nodeBounds.Max.Y:F2},{nodeBounds.Max.Z:F2})");
            }
            if (matchingNodes.Count > 10)
            {
                Console.WriteLine($"... and {matchingNodes.Count - 10} more nodes");
            }

            // Decompress and filter points
            QueryAndPrintPoints(reader, header, bbox, totalPointsInNodes, matchingNodes);
        }

        static void BoundingBoxInfoExample(string copcFilePath,
            double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine("=== Bounding Box Information Across LODs ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Bounding Box: [{minX:F3}, {minY:F3}, {minZ:F3}] to [{maxX:F3}, {maxY:F3}, {maxZ:F3}]\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            var bbox = new CopcBox(minX, minY, minZ, maxX, maxY, maxZ);

            // Display cloud bounds
            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Get all nodes in the file
            var allNodes = reader.GetAllNodes();
            
            // Group nodes by LOD and filter by bounding box intersection
            var nodesByLod = allNodes
                .Where(n =>
                {
                var nodeBounds = n.Key.GetBounds(header, info);
                return nodeBounds.Intersects(bbox);
                })
                .GroupBy(n => n.Key.D)
                .OrderBy(g => g.Key)
                .ToList();

            if (nodesByLod.Count == 0)
            {
                Console.WriteLine("⚠️  No nodes found intersecting this bounding box at any LOD.");
                return;
            }

            Console.WriteLine($"=== Information by LOD ===\n");
            Console.WriteLine($"{"LOD",-5} {"Resolution",-12} {"Nodes",-8} {"Points",-15} {"Avg Points/Node",-18}");
            Console.WriteLine(new string('-', 70));

            foreach (var lodGroup in nodesByLod)
            {
                int lod = lodGroup.Key;
                var nodes = lodGroup.ToList();
                var resolution = VoxelKey.GetResolutionAtDepth(lod, header, info);
                long totalPoints = nodes.Sum(n => (long)n.PointCount);
                double avgPointsPerNode = (double)totalPoints / nodes.Count;

                Console.WriteLine($"{lod,-5} {resolution,-12:F6} {nodes.Count,-8} {totalPoints,-15:N0} {avgPointsPerNode,-18:F0}");
            }

            // Show summary
            Console.WriteLine(new string('-', 70));
            int totalNodesInBox = nodesByLod.Sum(g => g.Count());
            long totalPointsInBox = nodesByLod.Sum(g => g.Sum(n => (long)n.PointCount));
            Console.WriteLine($"Total: {nodesByLod.Count} LODs, {totalNodesInBox:N0} nodes, {totalPointsInBox:N0} points\n");

            // Show detailed breakdown for a few LODs
            Console.WriteLine("=== Detailed Node Information ===\n");
            var lodsToShow = nodesByLod.Take(3).ToList();
            
            foreach (var lodGroup in lodsToShow)
            {
                int lod = lodGroup.Key;
                var nodes = lodGroup.Take(5).ToList();
                var resolution = VoxelKey.GetResolutionAtDepth(lod, header, info);

                Console.WriteLine($"LOD {lod} (Resolution: {resolution:F6}):");
                foreach (var node in nodes)
                {
                    var nodeBounds = node.Key.GetBounds(header, info);
                    Console.WriteLine($"  {node.Key}: {node.PointCount:N0} points");
                    Console.WriteLine($"    Bounds: Min({nodeBounds.Min.X:F3},{nodeBounds.Min.Y:F3},{nodeBounds.Min.Z:F3}) Max({nodeBounds.Max.X:F3},{nodeBounds.Max.Y:F3},{nodeBounds.Max.Z:F3})");
                }
                if (lodGroup.Count() > 5)
                {
                    Console.WriteLine($"  ... and {lodGroup.Count() - 5} more nodes");
                }
                Console.WriteLine();
            }

            if (nodesByLod.Count > 3)
            {
                Console.WriteLine($"... and {nodesByLod.Count - 3} more LODs\n");
            }

            // Decompress and print points from first LOD
            if (nodesByLod.Count > 0)
            {
                var firstLodGroup = nodesByLod.First();
                var firstLodNodes = firstLodGroup.Take(3).ToList(); // Take first 3 nodes
                
                if (firstLodNodes.Count > 0)
                {
                    Console.WriteLine($"=== Decompressing Sample Points from LOD {firstLodGroup.Key} ===\n");
                    
                    long totalPointsInNodes = firstLodNodes.Sum(n => (long)n.PointCount);
                    Console.WriteLine($"Decompressing {firstLodNodes.Count} nodes ({totalPointsInNodes:N0} points)...");
                    
                    var allPoints = reader.GetPointsFromNodes(firstLodNodes);
                    Console.WriteLine($"Decompressed {allPoints.Length:N0} points\n");

                    // Filter points to bounding box
                    var pointsInBox = allPoints.Where(p =>
                        p.X >= bbox.Min.X && p.X <= bbox.Max.X &&
                        p.Y >= bbox.Min.Y && p.Y <= bbox.Max.Y &&
                        p.Z >= bbox.Min.Z && p.Z <= bbox.Max.Z).ToArray();

                    Console.WriteLine($"Points within bounding box: {pointsInBox.Length:N0}\n");

                    if (pointsInBox.Length > 0)
                    {
                        // Print first 10 points
                        int pointsToPrint = Math.Min(10, pointsInBox.Length);
                        Console.WriteLine($"Showing first {pointsToPrint} points:\n");

            for (int i = 0; i < pointsToPrint; i++)
            {
                var p = pointsInBox[i];
                PointPrintHelper.PrintPoint(i, p, reader.Config.ExtraDimensions);
            }
                        Console.WriteLine();
                    }
                }
            }

            Console.WriteLine("✅ Complete!");
        }

        static void QueryAndPrintPoints(IO.CopcReader reader, Copc.LasHeader header, CopcBox bbox, long totalPointsInNodes, List<Hierarchy.Node> matchingNodes)
        {
            Console.WriteLine("\n=== Decompressing Points ===");
            Console.WriteLine("Using LAZ-perf to decompress only relevant node chunks...\n");

            // Decompress only matching nodes using LAZ-perf
            long totalPoints = (long)header.ExtendedNumberOfPointRecords;
            Console.WriteLine($"Decompressing {matchingNodes.Count} nodes ({totalPointsInNodes:N0} points) from file (total: {totalPoints:N0})...");
            
            var allPoints = reader.GetPointsFromNodes(matchingNodes);
            Console.WriteLine($"Decompressed {allPoints.Length:N0} points from {matchingNodes.Count} nodes");

            // Filter points to bounding box
            var pointsInBox = allPoints.Where(p =>
                p.X >= bbox.Min.X && p.X <= bbox.Max.X &&
                p.Y >= bbox.Min.Y && p.Y <= bbox.Max.Y &&
                p.Z >= bbox.Min.Z && p.Z <= bbox.Max.Z).ToArray();

            Console.WriteLine($"Points within bounding box: {pointsInBox.Length:N0}\n");

            if (pointsInBox.Length == 0)
            {
                Console.WriteLine("⚠️  No points found within the bounding box.");
                return;
            }

            // Print first 20 points
            Console.WriteLine("=== Points in Bounding Box ===");
            int pointsToPrint = Math.Min(20, pointsInBox.Length);
            Console.WriteLine($"Showing first {pointsToPrint} points:\n");

            for (int i = 0; i < pointsToPrint; i++)
            {
                var p = pointsInBox[i];
                PointPrintHelper.PrintPointWithRGB(i, p, reader.Config.ExtraDimensions);
            }

            // Print statistics
            Console.WriteLine($"\n=== Statistics ===");
            Console.WriteLine($"X range: [{pointsInBox.Min(p => p.X):F3}, {pointsInBox.Max(p => p.X):F3}]");
            Console.WriteLine($"Y range: [{pointsInBox.Min(p => p.Y):F3}, {pointsInBox.Max(p => p.Y):F3}]");
            Console.WriteLine($"Z range: [{pointsInBox.Min(p => p.Z):F3}, {pointsInBox.Max(p => p.Z):F3}]");
            Console.WriteLine($"Average intensity: {pointsInBox.Average(p => p.Intensity):F1}");

            var classifications = pointsInBox.GroupBy(p => p.Classification)
                .Select(g => new { Class = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            Console.WriteLine($"\nClassification breakdown:");
            foreach (var classGroup in classifications.Take(5))
            {
                Console.WriteLine($"  Class {classGroup.Class}: {classGroup.Count:N0} points ({100.0 * classGroup.Count / pointsInBox.Length:F1}%)");
            }

            Console.WriteLine("\n✅ Complete!");
        }

        static void PreprocessChunks(string copcFilePath, string cacheDir)
        {
            Console.WriteLine("=== Preprocessing COPC Chunks ===\n");
            Console.WriteLine($"Input file: {copcFilePath}");
            Console.WriteLine($"Cache directory: {cacheDir}\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            Directory.CreateDirectory(cacheDir);

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var allNodes = reader.GetAllNodes();

            Console.WriteLine($"Total points: {header.ExtendedNumberOfPointRecords:N0}");
            Console.WriteLine($"Total nodes: {allNodes.Count:N0}");
            Console.WriteLine($"Point format: {header.PointDataFormat}\n");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int processed = 0;
            long totalCompressedBytes = 0;

            Console.WriteLine("Extracting compressed chunks...");

            foreach (var node in allNodes)
            {
                try
                {
                    // Get compressed chunk data
                    var compressedData = reader.GetPointDataCompressed(node);
                    totalCompressedBytes += compressedData.Length;

                    // Save to file named by node key
                    string chunkFile = Path.Combine(cacheDir, $"{node.Key.ToString().Replace("-", "_")}.laz");
                    File.WriteAllBytes(chunkFile, compressedData);

                    processed++;

                    if (processed % 100 == 0)
                    {
                        Console.WriteLine($"  Processed {processed:N0}/{allNodes.Count:N0} nodes... ({100.0 * processed / allNodes.Count:F1}%)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error processing node {node.Key}: {ex.Message}");
                }
            }

            sw.Stop();

            Console.WriteLine($"\n✅ Preprocessing Complete!");
            Console.WriteLine($"Processed: {processed:N0} nodes in {sw.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine($"Total compressed data: {totalCompressedBytes:N0} bytes ({totalCompressedBytes / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds / processed:F2}ms per node");
            Console.WriteLine($"\n📝 Note: Extracted chunks are raw LAZ-compressed data from COPC nodes.");
            Console.WriteLine($"For complete LAZ files, each chunk would need proper headers prepended.");
            Console.WriteLine($"\nNow you can use 'bbox-chunked' to demonstrate selective chunk loading!");
        }

        static void BoundingBoxChunkedExample(string copcFilePath, int targetLod,
            double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine("=== Chunked Bounding Box Query (Performance Optimized) ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Target LOD: {targetLod}");
            Console.WriteLine($"Bounding Box: [{minX:F3}, {minY:F3}, {minZ:F3}] to [{maxX:F3}, {maxY:F3}, {maxZ:F3}]\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            var bbox = new CopcBox(minX, minY, minZ, maxX, maxY, maxZ);

            // Display cloud bounds
            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Get resolution at target LOD
            var resolution = VoxelKey.GetResolutionAtDepth(targetLod, header, info);
            Console.WriteLine($"Resolution at LOD {targetLod}: {resolution:F6}\n");

            // Find matching nodes
            var querySw = System.Diagnostics.Stopwatch.StartNew();
            var allNodesAtLod = reader.GetNodesAtResolution(resolution);
            var matchingNodes = allNodesAtLod.Where(n =>
            {
                var nodeBounds = n.Key.GetBounds(header, info);
                return nodeBounds.Intersects(bbox);
            }).ToList();
            querySw.Stop();

            Console.WriteLine($"Total nodes at LOD {targetLod}: {allNodesAtLod.Count}");
            Console.WriteLine($"Nodes intersecting bounding box: {matchingNodes.Count}");
            Console.WriteLine($"Query time: {querySw.ElapsedMilliseconds}ms\n");

            if (matchingNodes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No nodes found in the bounding box at this LOD.");
                return;
            }

            long totalPointsInNodes = matchingNodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in matching nodes: {totalPointsInNodes:N0}\n");

            Console.WriteLine($"📊 Performance Comparison:");
            Console.WriteLine($"  Full file approach: Would read entire file ({header.ExtendedNumberOfPointRecords:N0} points)");
            Console.WriteLine($"  Chunked approach: Only needs to read {totalPointsInNodes:N0} points from {matchingNodes.Count} chunks");
            Console.WriteLine($"  Data reduction: {100.0 * totalPointsInNodes / header.ExtendedNumberOfPointRecords:F1}% of total points");
            Console.WriteLine($"  Speedup potential: ~{header.ExtendedNumberOfPointRecords / (double)totalPointsInNodes:F1}x faster\n");

            // Decompress only the relevant nodes using LAZ-perf
            Console.WriteLine("=== Decompressing Points ===");
            var decompressSw = System.Diagnostics.Stopwatch.StartNew();
            
            var allPoints = reader.GetPointsFromNodes(matchingNodes);
            
            decompressSw.Stop();

            Console.WriteLine($"Decompressed {allPoints.Length:N0} points in {decompressSw.ElapsedMilliseconds}ms\n");

            // Filter points to bounding box
            var filterSw = System.Diagnostics.Stopwatch.StartNew();
            var pointsInBox = allPoints.Where(p =>
                p.X >= bbox.Min.X && p.X <= bbox.Max.X &&
                p.Y >= bbox.Min.Y && p.Y <= bbox.Max.Y &&
                p.Z >= bbox.Min.Z && p.Z <= bbox.Max.Z).ToArray();
            filterSw.Stop();

            Console.WriteLine($"Points within bounding box: {pointsInBox.Length:N0}");
            Console.WriteLine($"Filter time: {filterSw.ElapsedMilliseconds}ms\n");

            if (pointsInBox.Length == 0)
            {
                Console.WriteLine("⚠️  No points found within the bounding box.");
                totalSw.Stop();
                Console.WriteLine($"\n⏱️  Total time: {totalSw.ElapsedMilliseconds}ms");
                return;
            }

            // Print first 20 points
            Console.WriteLine("=== Points in Bounding Box ===");
            int pointsToPrint = Math.Min(20, pointsInBox.Length);
            Console.WriteLine($"Showing first {pointsToPrint} points:\n");

            for (int i = 0; i < pointsToPrint; i++)
            {
                var p = pointsInBox[i];
                PointPrintHelper.PrintPointWithRGB(i, p, reader.Config.ExtraDimensions);
            }

            // Print statistics
            Console.WriteLine($"\n=== Statistics ===");
            Console.WriteLine($"X range: [{pointsInBox.Min(p => p.X):F3}, {pointsInBox.Max(p => p.X):F3}]");
            Console.WriteLine($"Y range: [{pointsInBox.Min(p => p.Y):F3}, {pointsInBox.Max(p => p.Y):F3}]");
            Console.WriteLine($"Z range: [{pointsInBox.Min(p => p.Z):F3}, {pointsInBox.Max(p => p.Z):F3}]");
            Console.WriteLine($"Average intensity: {pointsInBox.Average(p => p.Intensity):F1}");

            var classifications = pointsInBox.GroupBy(p => p.Classification)
                .Select(g => new { Class = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            Console.WriteLine($"\nClassification breakdown:");
            foreach (var classGroup in classifications.Take(5))
            {
                Console.WriteLine($"  Class {classGroup.Class}: {classGroup.Count:N0} points ({100.0 * classGroup.Count / pointsInBox.Length:F1}%)");
            }

            totalSw.Stop();

            Console.WriteLine($"\n=== Performance Summary ===");
            Console.WriteLine($"Query planning: {querySw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Point decompression: {decompressSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Point filtering: {filterSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Points per second: {pointsInBox.Length / (totalSw.Elapsed.TotalSeconds):N0}");

            Console.WriteLine("\n✅ Complete!");
        }

        static void FrustumChunkedExample(string copcFilePath, double[] viewProjectionMatrix, double targetResolution)
        {
            Console.WriteLine("=== Chunked Frustum Query (Camera View - Performance Optimized) ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Target Resolution: {targetResolution:F3}m\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            // Display cloud bounds
            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]");
            Console.WriteLine($"  Total points: {header.ExtendedNumberOfPointRecords:N0}\n");

            // Create frustum from view-projection matrix
            Console.WriteLine("=== Creating Frustum from View-Projection Matrix ===");
            Console.WriteLine("Matrix (row-major):");
            for (int row = 0; row < 4; row++)
            {
                Console.Write("  [");
                for (int col = 0; col < 4; col++)
                {
                    Console.Write($"{viewProjectionMatrix[row * 4 + col],8:F3}");
                    if (col < 3) Console.Write(", ");
                }
                Console.WriteLine("]");
            }

            // Find matching nodes using frustum
            var querySw = System.Diagnostics.Stopwatch.StartNew();
            var visibleNodes = reader.GetNodesIntersectFrustum(viewProjectionMatrix, targetResolution);
            querySw.Stop();

            Console.WriteLine($"=== Frustum Query Results ===");
            Console.WriteLine($"Nodes intersecting frustum: {visibleNodes.Count}");
            Console.WriteLine($"Query time: {querySw.ElapsedMilliseconds}ms\n");

            if (visibleNodes.Count == 0)
            {
                Console.WriteLine("\n⚠️  No nodes found within the camera frustum at this resolution.");
                Console.WriteLine("Try adjusting the view-projection matrix or resolution.");
                return;
            }

            long totalPointsInNodes = visibleNodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in visible nodes: {totalPointsInNodes:N0}\n");

            // Show LOD distribution
            var byDepth = visibleNodes.GroupBy(n => n.Key.D).OrderBy(g => g.Key);
            Console.WriteLine("LOD Distribution:");
            foreach (var group in byDepth)
            {
                double res = VoxelKey.GetResolutionAtDepth(group.Key, header, info);
                Console.WriteLine($"  LOD {group.Key} (res: {res:F4}m): {group.Count()} nodes, {group.Sum(n => (long)n.PointCount):N0} points");
            }

            Console.WriteLine($"\n📊 Performance Comparison:");
            Console.WriteLine($"  Full scan approach: Would read entire file ({header.ExtendedNumberOfPointRecords:N0} points)");
            Console.WriteLine($"  Frustum culling: Only needs to read {totalPointsInNodes:N0} points from {visibleNodes.Count} chunks");
            Console.WriteLine($"  Data reduction: {100.0 * totalPointsInNodes / header.ExtendedNumberOfPointRecords:F1}% of total points");
            Console.WriteLine($"  Speedup potential: ~{header.ExtendedNumberOfPointRecords / (double)totalPointsInNodes:F1}x faster");
            Console.WriteLine($"\n💡 Use Case: Real-time rendering - load only points visible from camera!");

            // Decompress only the relevant visible nodes using LAZ-perf
            Console.WriteLine("\n=== Decompressing Visible Points ===");
            var decompressSw = System.Diagnostics.Stopwatch.StartNew();
            
            var allPoints = reader.GetPointsFromNodes(visibleNodes);
            
            decompressSw.Stop();

            Console.WriteLine($"Decompressed {allPoints.Length:N0} points in {decompressSw.ElapsedMilliseconds}ms\n");

            // Filter points to frustum (rough approximation using bounding boxes)
            var filterSw = System.Diagnostics.Stopwatch.StartNew();
            var pointsInFrustum = new List<IO.CopcPoint>();
            
            foreach (var node in visibleNodes)
            {
                var nodeBounds = node.Key.GetBounds(header, info);
                
                // For demo, collect points from nodes we know intersect the frustum
                var nodePoints = allPoints.Where(p =>
                    p.X >= nodeBounds.Min.X && p.X <= nodeBounds.Max.X &&
                    p.Y >= nodeBounds.Min.Y && p.Y <= nodeBounds.Max.Y &&
                    p.Z >= nodeBounds.Min.Z && p.Z <= nodeBounds.Max.Z).ToList();
                
                pointsInFrustum.AddRange(nodePoints);
            }
            
            filterSw.Stop();

            Console.WriteLine($"Points in frustum (approximate): {pointsInFrustum.Count:N0}");
            Console.WriteLine($"Filter time: {filterSw.ElapsedMilliseconds}ms\n");

            if (pointsInFrustum.Count == 0)
            {
                Console.WriteLine("⚠️  No points found within the frustum.");
                totalSw.Stop();
                Console.WriteLine($"\n⏱️  Total time: {totalSw.ElapsedMilliseconds}ms");
                return;
            }

            // Print first 20 points
            Console.WriteLine("=== Points Visible from Camera ===");
            int pointsToPrint = Math.Min(20, pointsInFrustum.Count);
            Console.WriteLine($"Showing first {pointsToPrint} points:\n");

            for (int i = 0; i < pointsToPrint; i++)
            {
                var p = pointsInFrustum[i];
                PointPrintHelper.PrintPointWithRGB(i, p, reader.Config.ExtraDimensions);
            }

            // Print statistics
            Console.WriteLine($"\n=== Statistics ===");
            Console.WriteLine($"X range: [{pointsInFrustum.Min(p => p.X):F3}, {pointsInFrustum.Max(p => p.X):F3}]");
            Console.WriteLine($"Y range: [{pointsInFrustum.Min(p => p.Y):F3}, {pointsInFrustum.Max(p => p.Y):F3}]");
            Console.WriteLine($"Z range: [{pointsInFrustum.Min(p => p.Z):F3}, {pointsInFrustum.Max(p => p.Z):F3}]");
            Console.WriteLine($"Average intensity: {pointsInFrustum.Average(p => p.Intensity):F1}");

            var classifications = pointsInFrustum.GroupBy(p => p.Classification)
                .Select(g => new { Class = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            Console.WriteLine($"\nClassification breakdown:");
            foreach (var classGroup in classifications.Take(5))
            {
                Console.WriteLine($"  Class {classGroup.Class}: {classGroup.Count:N0} points ({100.0 * classGroup.Count / pointsInFrustum.Count:F1}%)");
            }

            totalSw.Stop();

            Console.WriteLine($"\n=== Performance Summary ===");
            Console.WriteLine($"Frustum extraction: <1ms");
            Console.WriteLine($"Query planning: {querySw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Point decompression: {decompressSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Frustum filtering: {filterSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Points per second: {pointsInFrustum.Count / (totalSw.Elapsed.TotalSeconds):N0}");

            Console.WriteLine("\n✅ Complete!");
            Console.WriteLine("\n💡 Tip: In a real rendering application, you would:");
            Console.WriteLine("   1. Extract frustum from camera's view-projection matrix every frame");
            Console.WriteLine("   2. Query visible nodes using GetNodesIntersectFrustum()");
            Console.WriteLine("   3. Decompress only those node chunks");
            Console.WriteLine("   4. Upload to GPU for rendering");
            Console.WriteLine("   5. Repeat for each frame with updated camera position");
        }
    }
}

