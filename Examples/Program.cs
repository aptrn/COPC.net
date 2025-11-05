using System;
using System.Linq;

namespace Copc.Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("COPC.Net Examples");
            Console.WriteLine("=================\n");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: Examples <command> [options]");
                Console.WriteLine("\nCommands:");
                Console.WriteLine("  info <file>              - Display COPC file information");
                Console.WriteLine("  hierarchy <file>         - Display hierarchy structure");
                Console.WriteLine("  bbox <file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>");
                Console.WriteLine("                           - Query points within bounding box");
                Console.WriteLine("  resolution <file> <res>  - Get nodes at resolution");
                Console.WriteLine("  sample <file> [maxPts]   - Sample random bbox and show point coords");
                Console.WriteLine("  query <file>             - Detailed hierarchy query example");
                Console.WriteLine("  stream <file>            - Real-time streaming example");
                Console.WriteLine("  realtime <file>          - REALISTIC real-time approach for C#");
                Console.WriteLine("  production <file>        - PRODUCTION 60FPS streaming system");
                Console.WriteLine("  preprocess <file> <dir>  - Extract chunks for preprocessing");
                return;
            }

            string command = args[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "info":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide a file path");
                            return;
                        }
                        ShowFileInfo(args[1]);
                        break;

                    case "hierarchy":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide a file path");
                            return;
                        }
                        ShowHierarchy(args[1]);
                        break;

                    case "bbox":
                        if (args.Length < 8)
                        {
                            Console.WriteLine("Error: Please provide file path and bounding box coordinates");
                            return;
                        }
                        QueryBoundingBox(args[1],
                            double.Parse(args[2]), double.Parse(args[3]), double.Parse(args[4]),
                            double.Parse(args[5]), double.Parse(args[6]), double.Parse(args[7]));
                        break;

                    case "resolution":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Error: Please provide file path and resolution");
                            return;
                        }
                        QueryResolution(args[1], double.Parse(args[2]));
                        break;

                    case "sample":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide file path");
                            return;
                        }
                        int maxPoints = args.Length >= 3 ? int.Parse(args[2]) : 100;
                        SamplePoints.SampleRandomBox(args[1], maxPoints);
                        break;

                    case "query":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide file path");
                            return;
                        }
                        QueryHierarchy.ShowDetailedQuery(args[1]);
                        break;

                    case "stream":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide file path");
                            return;
                        }
                        RealTimeStreaming.StreamPointsForViewport(args[1]);
                        break;

                    case "realtime":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide file path");
                            return;
                        }
                        RealTimeApproach.CacheBasedStreaming(args[1]);
                        break;

                    case "production":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Error: Please provide file path");
                            return;
                        }
                        Production60FPS.Demo(args[1]);
                        break;

                    case "preprocess":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Error: Please provide file path and output directory");
                            return;
                        }
                        ChunkPreprocessor.PreprocessFile(args[1], args[2]);
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ShowFileInfo(string filePath)
        {
            Console.WriteLine($"Reading COPC file: {filePath}\n");

            using var reader = IO.CopcReader.Open(filePath);

            Console.WriteLine("=== LAS Header ===");
            var header = reader.Config.LasHeader;
            Console.WriteLine($"Version: {header.VersionMajor}.{header.VersionMinor}");
            Console.WriteLine($"Point Format: {header.PointDataFormat}");
            Console.WriteLine($"Point Count: {header.ExtendedNumberOfPointRecords}");
            Console.WriteLine($"Point Size: {header.PointDataRecordLength} bytes");
            Console.WriteLine($"Bounds: ({header.MinX}, {header.MinY}, {header.MinZ}) -> ({header.MaxX}, {header.MaxY}, {header.MaxZ})");
            Console.WriteLine($"Scale: ({header.XScaleFactor}, {header.YScaleFactor}, {header.ZScaleFactor})");
            Console.WriteLine($"Offset: ({header.XOffset}, {header.YOffset}, {header.ZOffset})");

            Console.WriteLine("\n=== COPC Info ===");
            var info = reader.Config.CopcInfo;
            Console.WriteLine($"Center: ({info.CenterX}, {info.CenterY}, {info.CenterZ})");
            Console.WriteLine($"Half Size: {info.HalfSize}");
            Console.WriteLine($"Spacing: {info.Spacing}");
            Console.WriteLine($"Root Hierarchy Offset: {info.RootHierarchyOffset}");
            Console.WriteLine($"Root Hierarchy Size: {info.RootHierarchySize}");
            Console.WriteLine($"GPS Time Range: [{info.GpsTimeMinimum}, {info.GpsTimeMaximum}]");

            if (!string.IsNullOrEmpty(reader.Config.Wkt))
            {
                Console.WriteLine("\n=== Spatial Reference ===");
                Console.WriteLine($"WKT: {reader.Config.Wkt.Substring(0, Math.Min(100, reader.Config.Wkt.Length))}...");
            }

            Console.WriteLine("\n=== Hierarchy Statistics ===");
            var allNodes = reader.GetAllNodes();
            Console.WriteLine($"Total Nodes: {allNodes.Count}");
            
            var depthGroups = allNodes.GroupBy(n => n.Key.D);
            Console.WriteLine("\nNodes per depth level:");
            foreach (var group in depthGroups.OrderBy(g => g.Key))
            {
                var totalPoints = group.Sum(n => (long)n.PointCount);
                var resolution = Hierarchy.VoxelKey.GetResolutionAtDepth(group.Key, header, info);
                Console.WriteLine($"  Depth {group.Key}: {group.Count()} nodes, {totalPoints} points, resolution={resolution:F6}");
            }
        }

        static void ShowHierarchy(string filePath)
        {
            Console.WriteLine($"Reading COPC hierarchy: {filePath}\n");

            using var reader = IO.CopcReader.Open(filePath);

            var rootPage = reader.LoadRootHierarchyPage();
            Console.WriteLine($"Root Page: {rootPage}");

            Console.WriteLine("\nTop-level entries:");
            foreach (var kvp in rootPage.Children.Take(10))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            if (rootPage.Children.Count > 10)
            {
                Console.WriteLine($"  ... and {rootPage.Children.Count - 10} more entries");
            }
        }

        static void QueryBoundingBox(string filePath, double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Console.WriteLine($"Querying bounding box in: {filePath}");
            Console.WriteLine($"Box: ({minX}, {minY}, {minZ}) -> ({maxX}, {maxY}, {maxZ})\n");

            using var reader = IO.CopcReader.Open(filePath);

            var box = new Geometry.Box(minX, minY, minZ, maxX, maxY, maxZ);
            var nodes = reader.GetNodesIntersectBox(box);

            Console.WriteLine($"Found {nodes.Count} nodes intersecting the bounding box:");

            long totalPoints = 0;
            foreach (var node in nodes.Take(20))
            {
                totalPoints += node.PointCount;
                Console.WriteLine($"  {node.Key}: {node.PointCount} points");
            }

            if (nodes.Count > 20)
            {
                foreach (var node in nodes.Skip(20))
                {
                    totalPoints += node.PointCount;
                }
                Console.WriteLine($"  ... and {nodes.Count - 20} more nodes");
            }

            Console.WriteLine($"\nTotal points in intersecting nodes: {totalPoints:N0}");
        }

        static void QueryResolution(string filePath, double resolution)
        {
            Console.WriteLine($"Querying nodes at resolution: {resolution}");
            Console.WriteLine($"File: {filePath}\n");

            using var reader = IO.CopcReader.Open(filePath);

            int depth = reader.GetDepthAtResolution(resolution);
            Console.WriteLine($"Target depth level: {depth}");

            var nodes = reader.GetNodesAtResolution(resolution);
            Console.WriteLine($"Found {nodes.Count} nodes at this resolution");

            long totalPoints = 0;
            foreach (var node in nodes.Take(10))
            {
                totalPoints += node.PointCount;
                Console.WriteLine($"  {node.Key}: {node.PointCount} points");
            }

            if (nodes.Count > 10)
            {
                foreach (var node in nodes.Skip(10))
                {
                    totalPoints += node.PointCount;
                }
                Console.WriteLine($"  ... and {nodes.Count - 10} more nodes");
            }

            Console.WriteLine($"\nTotal points at this resolution: {totalPoints:N0}");
        }
    }
}

