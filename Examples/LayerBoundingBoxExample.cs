using System;
using System.IO;
using System.Linq;
using Copc.IO;
using Copc.Geometry;
using Copc.Hierarchy;

namespace Copc.Examples
{
    /// <summary>
    /// Example showing how to get bounding boxes for all nodes at a specific layer/depth.
    /// </summary>
    public class LayerBoundingBoxExample
    {
        public static void Run(string copcFilePath, int layer = 2)
        {
            Console.WriteLine("=== Layer Bounding Box Example ===\n");
            Console.WriteLine($"Input file: {copcFilePath}");
            Console.WriteLine($"Target layer: {layer}\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            Console.WriteLine($"File bounds: [{header.MinX:F2}, {header.MinY:F2}, {header.MinZ:F2}] -> [{header.MaxX:F2}, {header.MaxY:F2}, {header.MaxZ:F2}]");
            Console.WriteLine($"COPC center: ({info.CenterX:F2}, {info.CenterY:F2}, {info.CenterZ:F2})");
            Console.WriteLine($"COPC half size: {info.HalfSize:F2}\n");

            // Get bounding boxes for all nodes at the specified layer
            var boundingBoxes = reader.GetBoundingBoxesAtLayer(layer);

            Console.WriteLine($"Found {boundingBoxes.Count} nodes at layer {layer}:\n");

            int displayCount = 0;
            const int maxDisplay = 10;

            foreach (var (key, box) in boundingBoxes)
            {
                if (displayCount < maxDisplay)
                {
                    Console.WriteLine($"  Node {key}:");
                    Console.WriteLine($"    Min: ({box.MinX:F2}, {box.MinY:F2}, {box.MinZ:F2})");
                    Console.WriteLine($"    Max: ({box.MaxX:F2}, {box.MaxY:F2}, {box.MaxZ:F2})");
                    Console.WriteLine($"    Center: ({box.Center.X:F2}, {box.Center.Y:F2}, {box.Center.Z:F2})");
                    
                    // Calculate dimensions
                    double width = box.MaxX - box.MinX;
                    double height = box.MaxY - box.MinY;
                    double depth = box.MaxZ - box.MinZ;
                    Console.WriteLine($"    Dimensions: {width:F2} x {height:F2} x {depth:F2}\n");
                    
                    displayCount++;
                }
            }

            if (boundingBoxes.Count > maxDisplay)
            {
                Console.WriteLine($"  ... and {boundingBoxes.Count - maxDisplay} more nodes\n");
            }

            // Get statistics about the layer
            Console.WriteLine("Layer Statistics:");
            
            if (boundingBoxes.Count > 0)
            {
                // Calculate average dimensions
                double avgWidth = 0, avgHeight = 0, avgDepth = 0;
                foreach (var box in boundingBoxes.Values)
                {
                    avgWidth += box.MaxX - box.MinX;
                    avgHeight += box.MaxY - box.MinY;
                    avgDepth += box.MaxZ - box.MinZ;
                }
                avgWidth /= boundingBoxes.Count;
                avgHeight /= boundingBoxes.Count;
                avgDepth /= boundingBoxes.Count;

                Console.WriteLine($"  Average node dimensions: {avgWidth:F2} x {avgHeight:F2} x {avgDepth:F2}");
                Console.WriteLine($"  Theoretical max nodes at this depth: {Math.Pow(8, layer):N0}");
                Console.WriteLine($"  Actual nodes at this depth: {boundingBoxes.Count:N0}");
                Console.WriteLine($"  Fill ratio: {(boundingBoxes.Count / Math.Pow(8, layer)) * 100:F2}%");
            }

            // Also demonstrate getting just the nodes without bounding boxes
            Console.WriteLine($"\n=== Alternative: Get Nodes Only ===\n");
            var nodesAtLayer = reader.GetNodesAtLayer(layer);
            Console.WriteLine($"Found {nodesAtLayer.Count} nodes at layer {layer}");
            
            if (nodesAtLayer.Count > 0)
            {
                long totalPoints = 0;
                long totalBytes = 0;
                
                foreach (var node in nodesAtLayer)
                {
                    totalPoints += node.PointCount;
                    totalBytes += node.ByteSize;
                }
                
                Console.WriteLine($"  Total points in layer: {totalPoints:N0}");
                Console.WriteLine($"  Total compressed bytes: {totalBytes:N0}");
                Console.WriteLine($"  Average points per node: {(double)totalPoints / nodesAtLayer.Count:F2}");
                Console.WriteLine($"  Average bytes per node: {(double)totalBytes / nodesAtLayer.Count:F2}");
            }
        }

        /// <summary>
        /// Displays bounding boxes for multiple layers.
        /// </summary>
        public static void CompareMultipleLayers(string copcFilePath, int[] layers)
        {
            Console.WriteLine("=== Multi-Layer Comparison ===\n");
            Console.WriteLine($"Input file: {copcFilePath}");
            Console.WriteLine($"Comparing layers: {string.Join(", ", layers)}\n");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = CopcReader.Open(copcFilePath);

            Console.WriteLine("Layer | Nodes | Avg Dimensions (XxYxZ) | Total Points");
            Console.WriteLine("------|-------|------------------------|-------------");

            foreach (int layer in layers)
            {
                var boundingBoxes = reader.GetBoundingBoxesAtLayer(layer);
                var nodes = reader.GetNodesAtLayer(layer);

                if (boundingBoxes.Count > 0)
                {
                    // Calculate average dimensions
                    double avgWidth = 0;
                    foreach (var box in boundingBoxes.Values)
                    {
                        avgWidth += box.MaxX - box.MinX;
                    }
                    avgWidth /= boundingBoxes.Count;

                    long totalPoints = nodes.Sum(n => n.PointCount);

                    Console.WriteLine($"  {layer,2}  | {boundingBoxes.Count,5} | {avgWidth,20:F2} | {totalPoints,12:N0}");
                }
                else
                {
                    string na = "N/A";
                    string zero = "0";
                    Console.WriteLine($"  {layer,2}  | {boundingBoxes.Count,5} | {na,20} | {zero,12}");
                }
            }
        }
    }
}

