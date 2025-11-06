using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc.Geometry;
using Copc.Hierarchy;

namespace Copc.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== COPC.Net Examples ===\n");
            
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
                        // bbox-chunked <cache-dir> <file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
                        if (args.Length < 10)
                        {
                            Console.WriteLine("Usage: Examples bbox-chunked <cache-dir> <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                            return;
                        }
                        BoundingBoxChunkedExample(args[1], args[2], int.Parse(args[3]),
                            double.Parse(args[4]), double.Parse(args[5]), double.Parse(args[6]),
                            double.Parse(args[7]), double.Parse(args[8]), double.Parse(args[9]));
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
            Console.WriteLine("  Examples random <copc-file> <lod-depth>");
            Console.WriteLine("  Examples bbox-lod <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples bbox-res <copc-file> <resolution> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples bbox-info <copc-file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("  Examples preprocess <copc-file> <cache-dir>");
            Console.WriteLine("  Examples bbox-chunked <cache-dir> <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
            Console.WriteLine("\nExamples:");
            Console.WriteLine("  Examples random data.copc.laz 5");
            Console.WriteLine("  Examples bbox-lod data.copc.laz 5 -10 -10 0 10 10 50");
            Console.WriteLine("  Examples bbox-res data.copc.laz 0.1 -10 -10 0 10 10 50");
            Console.WriteLine("  Examples bbox-info data.copc.laz -10 -10 0 10 10 50");
            Console.WriteLine("  Examples preprocess data.copc.laz cache/");
            Console.WriteLine("  Examples bbox-chunked cache/ data.copc.laz 3 -10 -10 0 10 10 50");
            Console.WriteLine("\nCommands:");
            Console.WriteLine("  random        - Pick a random bounding box at specified LOD and print points");
            Console.WriteLine("  bbox-lod      - Query specific bounding box at specific LOD");
            Console.WriteLine("  bbox-res      - Query specific bounding box at specific resolution");
            Console.WriteLine("  bbox-info     - Show information about a bounding box across all LODs");
            Console.WriteLine("  preprocess    - Extract all node chunks to cache directory for fast queries");
            Console.WriteLine("  bbox-chunked  - Query bounding box using cached chunks (much faster!)");
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

            var bbox = new Box(bboxMinX, bboxMinY, bboxMinZ, bboxMaxX, bboxMaxY, bboxMaxZ);

            Console.WriteLine("Random Bounding Box (within maximum bounds):");
            Console.WriteLine($"  Min: ({bbox.MinX:F3}, {bbox.MinY:F3}, {bbox.MinZ:F3})");
            Console.WriteLine($"  Max: ({bbox.MaxX:F3}, {bbox.MaxY:F3}, {bbox.MaxZ:F3})");
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

            // Decompress points from file using LasZipNetStandard
            Console.WriteLine("=== Decompressing Points ===");
            Console.WriteLine("Using LasZipNetStandard to decompress points from file...\n");

            // Read enough points to cover the nodes (estimate: read more than needed)
            // Note: LasZipNetStandard reads sequentially, so we read many more points to find ones in our bbox
            // For small files, read a larger percentage; for large files, limit to reasonable amount
            long totalPoints = (long)header.ExtendedNumberOfPointRecords;
            int pointsToRead;
            
            if (totalPoints <= 1000000) // Files <= 1M points
            {
                pointsToRead = (int)totalPoints; // Read all points
            }
            else if (totalPoints <= 10000000) // Files <= 10M points
            {
                pointsToRead = Math.Min((int)(totalPointsInNodes * 100), (int)totalPoints / 2); // Read up to 50%
            }
            else // Large files > 10M points
            {
                pointsToRead = Math.Min((int)(totalPointsInNodes * 50), 5000000); // Read up to 5M points
            }
            
            pointsToRead = Math.Max(pointsToRead, 50000); // Read at least 50k points
            Console.WriteLine($"Reading {pointsToRead:N0} points from file (total: {totalPoints:N0})...");
            var allPoints = reader.GetAllPoints(pointsToRead);
            
            Console.WriteLine($"Decompressed {allPoints.Length:N0} points from file");

            // Filter points to those within the bounding box
            var pointsInBox = allPoints.Where(p => 
                p.X >= bbox.MinX && p.X <= bbox.MaxX &&
                p.Y >= bbox.MinY && p.Y <= bbox.MaxY &&
                p.Z >= bbox.MinZ && p.Z <= bbox.MaxZ).ToArray();
            
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
                Console.WriteLine($"[{i,3}] X={p.X,12:F3} Y={p.Y,12:F3} Z={p.Z,12:F3} " +
                                $"Intensity={p.Intensity,5} Class={p.Classification,3} " +
                                $"RGB=({p.Red},{p.Green},{p.Blue})");
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

            var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);

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
                Console.WriteLine($"  Bounds: X[{nodeBounds.MinX:F2},{nodeBounds.MaxX:F2}] Y[{nodeBounds.MinY:F2},{nodeBounds.MaxY:F2}] Z[{nodeBounds.MinZ:F2},{nodeBounds.MaxZ:F2}]");
            }
            if (matchingNodes.Count > 10)
            {
                Console.WriteLine($"... and {matchingNodes.Count - 10} more nodes");
            }

            // Decompress and filter points
            QueryAndPrintPoints(reader, header, bbox, totalPointsInNodes);
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

            var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);

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
                Console.WriteLine($"  Bounds: X[{nodeBounds.MinX:F2},{nodeBounds.MaxX:F2}] Y[{nodeBounds.MinY:F2},{nodeBounds.MaxY:F2}] Z[{nodeBounds.MinZ:F2},{nodeBounds.MaxZ:F2}]");
            }
            if (matchingNodes.Count > 10)
            {
                Console.WriteLine($"... and {matchingNodes.Count - 10} more nodes");
            }

            // Decompress and filter points
            QueryAndPrintPoints(reader, header, bbox, totalPointsInNodes);
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

            var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);

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
                    Console.WriteLine($"    Bounds: X[{nodeBounds.MinX:F3},{nodeBounds.MaxX:F3}] Y[{nodeBounds.MinY:F3},{nodeBounds.MaxY:F3}] Z[{nodeBounds.MinZ:F3},{nodeBounds.MaxZ:F3}]");
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

            Console.WriteLine("✅ Complete!");
        }

        static void QueryAndPrintPoints(IO.CopcReader reader, Copc.LasHeader header, Box bbox, long totalPointsInNodes)
        {
            Console.WriteLine("\n=== Decompressing Points ===");
            Console.WriteLine("Using LasZipNetStandard to decompress points from file...\n");

            // Calculate how many points to read
            long totalPoints = (long)header.ExtendedNumberOfPointRecords;
            int pointsToRead;

            if (totalPoints <= 1000000)
            {
                pointsToRead = (int)totalPoints;
            }
            else if (totalPoints <= 10000000)
            {
                pointsToRead = Math.Min((int)(totalPointsInNodes * 100), (int)totalPoints / 2);
            }
            else
            {
                pointsToRead = Math.Min((int)(totalPointsInNodes * 50), 5000000);
            }

            pointsToRead = Math.Max(pointsToRead, 50000);
            Console.WriteLine($"Reading {pointsToRead:N0} points from file (total: {totalPoints:N0})...");
            var allPoints = reader.GetAllPoints(pointsToRead);

            Console.WriteLine($"Decompressed {allPoints.Length:N0} points from file");

            // Filter points to bounding box
            var pointsInBox = allPoints.Where(p =>
                p.X >= bbox.MinX && p.X <= bbox.MaxX &&
                p.Y >= bbox.MinY && p.Y <= bbox.MaxY &&
                p.Z >= bbox.MinZ && p.Z <= bbox.MaxZ).ToArray();

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
                Console.WriteLine($"[{i,3}] X={p.X,12:F3} Y={p.Y,12:F3} Z={p.Z,12:F3} " +
                                $"Intensity={p.Intensity,5} Class={p.Classification,3} " +
                                $"RGB=({p.Red},{p.Green},{p.Blue})");
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

        static void BoundingBoxChunkedExample(string cacheDir, string copcFilePath, int targetLod,
            double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine("=== Chunked Bounding Box Query (Performance Optimized) ===\n");
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Cache: {cacheDir}");
            Console.WriteLine($"Target LOD: {targetLod}");
            Console.WriteLine($"Bounding Box: [{minX:F3}, {minY:F3}, {minZ:F3}] to [{maxX:F3}, {maxY:F3}, {maxZ:F3}]\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            if (!Directory.Exists(cacheDir))
            {
                Console.WriteLine($"Error: Cache directory not found: {cacheDir}");
                Console.WriteLine($"Run 'Examples preprocess {copcFilePath} {cacheDir}' first!");
                return;
            }

            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            using var reader = IO.CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);

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

            // Load only relevant chunks (demonstrating selective loading)
            Console.WriteLine("=== Loading Relevant Chunks ===");
            Console.WriteLine($"Identifying and loading {matchingNodes.Count} relevant chunks...\n");

            var loadSw = System.Diagnostics.Stopwatch.StartNew();
            int chunksProcessed = 0;
            long totalChunkBytes = 0;
            var chunksToDecompress = new List<string>();

            foreach (var node in matchingNodes)
            {
                string chunkFile = Path.Combine(cacheDir, $"{node.Key.ToString().Replace("-", "_")}.laz");

                if (!File.Exists(chunkFile))
                {
                    Console.WriteLine($"  ⚠️  Warning: Chunk file not found: {chunkFile}");
                    continue;
                }

                totalChunkBytes += new FileInfo(chunkFile).Length;
                chunksToDecompress.Add(chunkFile);
                chunksProcessed++;

                if (chunksProcessed % 10 == 0 || chunksProcessed == matchingNodes.Count)
                {
                    Console.WriteLine($"  Loaded {chunksProcessed}/{matchingNodes.Count} chunks");
                }
            }

            loadSw.Stop();

            Console.WriteLine($"\n✅ Identified {chunksProcessed} relevant chunks in {loadSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Total chunk data to process: {totalChunkBytes:N0} bytes ({totalChunkBytes / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"\n📊 Performance Comparison:");
            Console.WriteLine($"  Sequential approach: Would read entire file ({header.ExtendedNumberOfPointRecords:N0} points)");
            Console.WriteLine($"  Chunked approach: Only needs to read {totalPointsInNodes:N0} points from {chunksProcessed} chunks");
            Console.WriteLine($"  Data reduction: {100.0 * totalPointsInNodes / header.ExtendedNumberOfPointRecords:F1}% of total points");
            Console.WriteLine($"  Speedup potential: ~{header.ExtendedNumberOfPointRecords / (double)totalPointsInNodes:F1}x faster\n");

            // For demonstration, decompress from the main file but only the amount we need
            Console.WriteLine("=== Decompressing Points (from main file for demo) ===");
            var decompressSw = System.Diagnostics.Stopwatch.StartNew();
            
            // Read only as many points as we need for the matching nodes
            int pointsToRead = Math.Min((int)totalPointsInNodes * 2, (int)header.ExtendedNumberOfPointRecords);
            var allPoints = reader.GetAllPoints(pointsToRead);
            
            decompressSw.Stop();

            Console.WriteLine($"Decompressed {allPoints.Length:N0} points in {decompressSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"(In production, each chunk would be decompressed independently in parallel)\n");

            // Filter points to bounding box
            var filterSw = System.Diagnostics.Stopwatch.StartNew();
            var pointsInBox = allPoints.Where(p =>
                p.X >= bbox.MinX && p.X <= bbox.MaxX &&
                p.Y >= bbox.MinY && p.Y <= bbox.MaxY &&
                p.Z >= bbox.MinZ && p.Z <= bbox.MaxZ).ToArray();
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
                Console.WriteLine($"[{i,3}] X={p.X,12:F3} Y={p.Y,12:F3} Z={p.Z,12:F3} " +
                                $"Intensity={p.Intensity,5} Class={p.Classification,3} " +
                                $"RGB=({p.Red},{p.Green},{p.Blue})");
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
            Console.WriteLine($"Chunk decompression: {decompressSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Point filtering: {filterSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Total time: {totalSw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Points per second: {pointsInBox.Length / (totalSw.Elapsed.TotalSeconds):N0}");

            Console.WriteLine("\n✅ Complete!");
        }
    }
}

