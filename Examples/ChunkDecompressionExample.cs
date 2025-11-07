using System;
using System.Linq;
using Copc.IO;
using Copc.LazPerf;
using Copc.Geometry;
using Copc.Hierarchy;

namespace Copc.Examples
{
    /// <summary>
    /// Example demonstrating how to use the new chunk decompression capabilities
    /// to decompress individual COPC node chunks directly from memory
    /// </summary>
    public static class ChunkDecompressionExample
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== COPC Chunk Decompression Example ===\n");

            using var reader = CopcReader.Open(copcFilePath);
            var config = reader.Config;
            
            Console.WriteLine($"File: {copcFilePath}");
            Console.WriteLine($"Point Format: {config.LasHeader.PointDataFormat} (base: {config.LasHeader.BasePointFormat})");
            Console.WriteLine($"Point Size: {config.LasHeader.PointDataRecordLength} bytes");
            Console.WriteLine($"Total Points: {config.LasHeader.ExtendedNumberOfPointRecords}");
            Console.WriteLine($"Compressed: {config.LasHeader.IsCompressed}");
            Console.WriteLine();

            // Example 1: Decompress points from the root node
            Console.WriteLine("--- Example 1: Decompressing Root Node ---");
            DecompressRootNode(reader);
            Console.WriteLine();

            // Example 2: Decompress points from a specific layer
            Console.WriteLine("--- Example 2: Decompressing Layer 2 Nodes ---");
            DecompressLayerNodes(reader, layer: 2);
            Console.WriteLine();

            // Example 3: Spatial query - decompress points within a box
            Console.WriteLine("--- Example 3: Spatial Query - Points in Bounding Box ---");
            DecompressPointsInBox(reader);
            Console.WriteLine();

            // Example 4: Decompress a single node chunk
            Console.WriteLine("--- Example 4: Single Node Chunk Decompression ---");
            DecompressSingleNode(reader);
            Console.WriteLine();
        }

        private static void DecompressRootNode(CopcReader reader)
        {
            var rootPage = reader.LoadRootHierarchyPage();
            var rootNode = rootPage.Children.Values
                .OfType<Hierarchy.Node>()
                .FirstOrDefault(n => n.Key.D == 0);

            if (rootNode == null)
            {
                Console.WriteLine("No root node found!");
                return;
            }

            Console.WriteLine($"Root Node Key: {rootNode.Key}");
            Console.WriteLine($"Point Count: {rootNode.PointCount}");
            
            // Get compressed data
            var compressedData = reader.GetPointDataCompressed(rootNode);
            Console.WriteLine($"Compressed Size: {compressedData.Length} bytes");

            // Decompress the chunk using LazDecompressor (handles 0/6/7/8 appropriately)
            int pf = reader.Config.LasHeader.BasePointFormat;
            int ps = reader.Config.LasHeader.PointDataRecordLength;
            var points = LazDecompressor.DecompressChunk(pf, ps, compressedData, rootNode.PointCount, reader.Config.LasHeader);

            // Read first 5 points
            int pointsToShow = Math.Min(5, points.Length);
            Console.WriteLine($"\nFirst {pointsToShow} points:");
            for (int i = 0; i < pointsToShow; i++)
            {
                var p = points[i];
                Console.WriteLine($"  Point {i}: ({p.X:F3}, {p.Y:F3}, {p.Z:F3}) Intensity={p.Intensity} Class={p.Classification}");
            }
        }

        private static void DecompressLayerNodes(CopcReader reader, int layer)
        {
            var nodes = reader.GetNodesAtLayer(layer);
            Console.WriteLine($"Found {nodes.Count} nodes at layer {layer}");

            int totalPoints = 0;
            int nodesProcessed = 0;
            int maxNodesToProcess = 3; // Process first 3 nodes as example

            foreach (var node in nodes.Take(maxNodesToProcess))
            {
                Console.WriteLine($"\nNode {node.Key}:");
                Console.WriteLine($"  Point Count: {node.PointCount}");
                
                var compressedData = reader.GetPointDataCompressed(node);
                
                // Decompress all points at once via LazDecompressor
                var pointsData = LazDecompressor.DecompressChunk(
                    reader.Config.LasHeader.BasePointFormat,
                    reader.Config.LasHeader.PointDataRecordLength,
                    compressedData,
                    node.PointCount,
                    reader.Config.LasHeader
                );

                totalPoints += pointsData.Length;
                nodesProcessed++;

                // Show statistics about first point
                if (pointsData.Length > 0)
                {
                    var fp = pointsData[0];
                    Console.WriteLine($"  First point: ({fp.X:F3}, {fp.Y:F3}, {fp.Z:F3})");
                }
            }

            Console.WriteLine($"\nProcessed {nodesProcessed} nodes with {totalPoints} total points");
        }

        private static void DecompressPointsInBox(CopcReader reader)
        {
            var header = reader.Config.LasHeader;
            
            // Create a bounding box in the center of the dataset
            double centerX = (header.MinX + header.MaxX) / 2;
            double centerY = (header.MinY + header.MaxY) / 2;
            double centerZ = (header.MinZ + header.MaxZ) / 2;
            double size = Math.Min(header.MaxX - header.MinX, header.MaxY - header.MinY) * 0.1;
            
            var box = new Box(
                centerX - size/2, centerY - size/2, centerZ - size/2,
                centerX + size/2, centerY + size/2, centerZ + size/2
            );

            Console.WriteLine($"Query Box: ({box.MinX:F3}, {box.MinY:F3}, {box.MinZ:F3}) to " +
                            $"({box.MaxX:F3}, {box.MaxY:F3}, {box.MaxZ:F3})");

            var nodes = reader.GetNodesIntersectBox(box);
            Console.WriteLine($"Found {nodes.Count} intersecting nodes");

            int totalPoints = 0;
            int maxNodesToShow = 2;

            foreach (var node in nodes.Take(maxNodesToShow))
            {
                var compressedData = reader.GetPointDataCompressed(node);
                
                // Decompress to typed points
                var pts = LazDecompressor.DecompressChunk(
                    reader.Config.LasHeader.BasePointFormat,
                    reader.Config.LasHeader.PointDataRecordLength,
                    compressedData,
                    node.PointCount,
                    reader.Config.LasHeader
                );

                totalPoints += pts.Length;
                Console.WriteLine($"  Node {node.Key}: {pts.Length} points decompressed");
            }

            if (nodes.Count > maxNodesToShow)
            {
                Console.WriteLine($"  ... and {nodes.Count - maxNodesToShow} more nodes");
            }

            Console.WriteLine($"Total points in query: {totalPoints}+");
        }

        private static void DecompressSingleNode(CopcReader reader)
        {
            // Get a node at layer 1
            var nodes = reader.GetNodesAtLayer(1);
            if (nodes.Count == 0)
            {
                Console.WriteLine("No nodes found at layer 1");
                return;
            }

            var node = nodes[0];
            Console.WriteLine($"Decompressing node: {node.Key}");
            Console.WriteLine($"Point count: {node.PointCount}");
            Console.WriteLine($"Byte size (compressed): {node.ByteSize}");

            var compressedData = reader.GetPointDataCompressed(node);

            // Decompress all points and compute statistics
            var nodePoints = LazDecompressor.DecompressChunk(
                reader.Config.LasHeader.BasePointFormat,
                reader.Config.LasHeader.PointDataRecordLength,
                compressedData,
                node.PointCount,
                reader.Config.LasHeader
            );

            int totalPoints = nodePoints.Length;
            double sumX = 0, sumY = 0, sumZ = 0;
            int minIntensity = int.MaxValue, maxIntensity = int.MinValue;

            for (int i = 0; i < totalPoints; i++)
            {
                var p = nodePoints[i];
                sumX += p.X;
                sumY += p.Y;
                sumZ += p.Z;
                if (p.Intensity < minIntensity) minIntensity = p.Intensity;
                if (p.Intensity > maxIntensity) maxIntensity = p.Intensity;
            }

            Console.WriteLine($"\nStatistics:");
            Console.WriteLine($"  Centroid: ({sumX/totalPoints:F3}, {sumY/totalPoints:F3}, {sumZ/totalPoints:F3})");
            Console.WriteLine($"  Intensity range: {minIntensity} - {maxIntensity}");
            Console.WriteLine($"  Compression ratio: {(double)(totalPoints * reader.Config.LasHeader.PointDataRecordLength) / compressedData.Length:F2}x");
        }
    }
}

