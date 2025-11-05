using System;
using System.Linq;
using Copc.Geometry;

namespace Copc.Examples
{
    /// <summary>
    /// Example showing what you CAN do with the COPC library:
    /// - Query the hierarchy
    /// - Get node information  
    /// - Access compressed data
    /// - Spatial queries
    /// </summary>
    public static class QueryHierarchy
    {
        public static void ShowDetailedQuery(string filePath)
        {
            Console.WriteLine($"COPC Hierarchy Query Example");
            Console.WriteLine($"File: {filePath}\n");

            using var reader = IO.CopcReader.Open(filePath);

            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            // Show file overview
            Console.WriteLine("=== File Overview ===");
            Console.WriteLine($"Total Points: {header.ExtendedNumberOfPointRecords:N0}");
            Console.WriteLine($"Bounds: ({header.MinX:F2}, {header.MinY:F2}, {header.MinZ:F2})");
            Console.WriteLine($"     to ({header.MaxX:F2}, {header.MaxY:F2}, {header.MaxZ:F2})");
            Console.WriteLine($"Point Format: {header.PointDataFormat}");
            Console.WriteLine($"Point Size: {header.PointDataRecordLength} bytes");
            Console.WriteLine($"Root Spacing: {info.Spacing:F6}");

            // Get all nodes
            Console.WriteLine("\n=== Hierarchy Structure ===");
            var allNodes = reader.GetAllNodes();
            Console.WriteLine($"Total Nodes: {allNodes.Count}");

            var depthGroups = allNodes.GroupBy(n => n.Key.D).OrderBy(g => g.Key);
            Console.WriteLine("\nNodes by depth:");
            foreach (var group in depthGroups)
            {
                var totalPoints = group.Sum(n => (long)n.PointCount);
                var totalCompressed = group.Sum(n => (long)n.ByteSize);
                var avgCompressionRatio = (double)totalCompressed / (totalPoints * header.PointDataRecordLength);
                
                Console.WriteLine($"  Depth {group.Key,2}: {group.Count(),5} nodes, " +
                                $"{totalPoints,10:N0} points, " +
                                $"compressed to {totalCompressed,10:N0} bytes ({avgCompressionRatio:P1})");
            }

            // Pick a random small area
            Console.WriteLine("\n=== Spatial Query Example ===");
            var random = new Random();
            double rangeX = header.MaxX - header.MinX;
            double rangeY = header.MaxY - header.MinY;
            double rangeZ = header.MaxZ - header.MinZ;

            double sizeX = rangeX * 0.05;
            double sizeY = rangeY * 0.05;
            double sizeZ = rangeZ * 0.05;

            double minX = header.MinX + random.NextDouble() * (rangeX - sizeX);
            double minY = header.MinY + random.NextDouble() * (rangeY - sizeY);
            double minZ = header.MinZ + random.NextDouble() * (rangeZ - sizeZ);

            var bbox = new Box(minX, minY, minZ, minX + sizeX, minY + sizeY, minZ + sizeZ);

            Console.WriteLine($"Query Box: ({bbox.MinX:F2}, {bbox.MinY:F2}, {bbox.MinZ:F2})");
            Console.WriteLine($"        to ({bbox.MaxX:F2}, {bbox.MaxY:F2}, {bbox.MaxZ:F2})");

            var matchingNodes = reader.GetNodesIntersectBox(bbox);
            Console.WriteLine($"\nFound {matchingNodes.Count} node(s) intersecting this box:");

            long totalPointsInBox = 0;
            long totalBytesInBox = 0;

            foreach (var node in matchingNodes.Take(10))
            {
                totalPointsInBox += node.PointCount;
                totalBytesInBox += node.ByteSize;

                var nodeBounds = node.Key.GetBounds(header, info);
                Console.WriteLine($"\n  Node {node.Key}:");
                Console.WriteLine($"    Points: {node.PointCount:N0}");
                Console.WriteLine($"    Compressed Size: {node.ByteSize:N0} bytes");
                Console.WriteLine($"    Bounds: ({nodeBounds.MinX:F2}, {nodeBounds.MinY:F2}, {nodeBounds.MinZ:F2})");
                Console.WriteLine($"         to ({nodeBounds.MaxX:F2}, {nodeBounds.MaxY:F2}, {nodeBounds.MaxZ:F2})");

                // Show what you CAN do with the node
                Console.WriteLine($"    Available operations:");
                Console.WriteLine($"      - Get compressed data: reader.GetPointDataCompressed(node)");
                Console.WriteLine($"      - Node offset in file: {node.Offset}");
                Console.WriteLine($"      - Resolution: {node.Key.GetResolution(header, info):F6}");
            }

            if (matchingNodes.Count > 10)
            {
                foreach (var node in matchingNodes.Skip(10))
                {
                    totalPointsInBox += node.PointCount;
                    totalBytesInBox += node.ByteSize;
                }
                Console.WriteLine($"\n  ... and {matchingNodes.Count - 10} more nodes");
            }

            Console.WriteLine($"\nTotal in query box:");
            Console.WriteLine($"  Points: {totalPointsInBox:N0}");
            Console.WriteLine($"  Compressed data: {totalBytesInBox:N0} bytes");
            Console.WriteLine($"  Estimated uncompressed: {totalPointsInBox * header.PointDataRecordLength:N0} bytes");

            // Show how to access compressed data
            if (matchingNodes.Count > 0)
            {
                Console.WriteLine("\n=== Accessing Compressed Data ===");
                var firstNode = matchingNodes[0];
                var compressedData = reader.GetPointDataCompressed(firstNode);
                
                Console.WriteLine($"Successfully read {compressedData.Length:N0} bytes of compressed point data");
                Console.WriteLine($"This data can be:");
                Console.WriteLine($"  - Saved to a file for later processing");
                Console.WriteLine($"  - Passed to PDAL for decompression");
                Console.WriteLine($"  - Analyzed for compression statistics");
                Console.WriteLine($"  - Streamed to other systems");
                
                // Show first few bytes (header of LAZ chunk)
                Console.WriteLine($"\nFirst 16 bytes (hex): {BitConverter.ToString(compressedData.Take(16).ToArray())}");
            }

            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine("The COPC.Net library successfully:");
            Console.WriteLine("  ✓ Reads COPC file metadata");
            Console.WriteLine("  ✓ Navigates the octree hierarchy");
            Console.WriteLine("  ✓ Performs spatial queries");
            Console.WriteLine("  ✓ Accesses compressed point data");
            Console.WriteLine("  ✓ Calculates resolutions and bounds");
            Console.WriteLine("\nFor point decompression, use PDAL:");
            Console.WriteLine("  pdal translate input.copc.laz output.las");
        }
    }
}

