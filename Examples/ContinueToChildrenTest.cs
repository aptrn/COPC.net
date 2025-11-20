using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc;
using Copc.Hierarchy;
using Copc.IO;

namespace Copc.Examples
{
    /// <summary>
    /// Test demonstrating the continueToChildren behavior with all 4 combinations:
    /// (true, true): Accept and continue - takes everything
    /// (true, false): Accept but stop - just takes the first layer
    /// (false, true): Skip but continue - traverses all but takes nothing
    /// (false, false): Skip and stop - traverses first layer only and takes nothing
    /// </summary>
    public static class ContinueToChildrenTest
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== continueToChildren Test ===\n");
            Console.WriteLine($"File: {copcFilePath}");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            Console.WriteLine($"Root spacing: {info.Spacing:F6}m\n");

            // Test 1: (true, true) - Accept and continue to children
            Console.WriteLine("--- Test 1: (true, true) - Accept and continue ---");
            Console.WriteLine("Expected: Accept ALL nodes at ALL levels");
            var options1 = new TraversalOptions
            {
                SpatialPredicate = _ => true,
                ResolutionPredicate = _ => (true, true)
            };
            var nodes1 = reader.TraverseNodes(options1);
            PrintSummary(reader, nodes1, "All nodes");

            // Test 2: (true, false) - Accept but stop at first layer
            Console.WriteLine("--- Test 2: (true, false) - Accept but stop ---");
            Console.WriteLine("Expected: Accept ONLY root node (first layer)");
            var options2 = new TraversalOptions
            {
                SpatialPredicate = _ => true,
                ResolutionPredicate = _ => (true, false)
            };
            var nodes2 = reader.TraverseNodes(options2);
            PrintSummary(reader, nodes2, "Root only");

            // Test 3: (false, true) - Skip but continue to children
            Console.WriteLine("--- Test 3: (false, true) - Skip but continue ---");
            Console.WriteLine("Expected: Accept NO nodes (traverses all but takes nothing)");
            var options3 = new TraversalOptions
            {
                SpatialPredicate = _ => true,
                ResolutionPredicate = _ => (false, true)
            };
            var nodes3 = reader.TraverseNodes(options3);
            PrintSummary(reader, nodes3, "None");

            // Test 4: (false, false) - Skip and stop at first layer
            Console.WriteLine("--- Test 4: (false, false) - Skip and stop ---");
            Console.WriteLine("Expected: Accept NO nodes (traverses only root, takes nothing)");
            var options4 = new TraversalOptions
            {
                SpatialPredicate = _ => true,
                ResolutionPredicate = _ => (false, false)
            };
            var nodes4 = reader.TraverseNodes(options4);
            PrintSummary(reader, nodes4, "None (root only traversed)");

            // Test 5: Dynamic - Accept only depth 0 and 1
            Console.WriteLine("--- Test 5: Dynamic - Accept depth 0 and 1 only ---");
            Console.WriteLine("Expected: Accept nodes at depth 0 and 1, continue to depth 2 but don't accept it");
            var options5 = new TraversalOptions
            {
                SpatialPredicate = _ => true,
                ResolutionPredicate = ctx =>
                {
                    bool accept = ctx.Depth <= 1;
                    bool continueToChildren = ctx.Depth < 2; // Continue until we've processed depth 1's children
                    return (accept, continueToChildren);
                }
            };
            var nodes5 = reader.TraverseNodes(options5);
            PrintSummary(reader, nodes5, "Depth 0 and 1");

            Console.WriteLine("\n✅ All tests complete!");
        }

        private static void PrintSummary(CopcReader reader, List<Node> nodes, string label)
        {
            Console.WriteLine($"Result: {nodes.Count} nodes ({label})");
            
            if (nodes.Count > 0)
            {
                long totalPoints = nodes.Sum(n => (long)n.PointCount);
                Console.WriteLine($"Total points: {totalPoints:N0}");

                var byDepth = nodes.GroupBy(n => n.Key.D)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Depth = g.Key, Count = g.Count() })
                    .ToList();

                Console.WriteLine("Depth distribution:");
                foreach (var d in byDepth)
                {
                    double res = VoxelKey.GetResolutionAtDepth(d.Depth, reader.Config.LasHeader, reader.Config.CopcInfo);
                    Console.WriteLine($"  Depth {d.Depth}: {d.Count} nodes @ res {res:F6}m");
                }
            }
            Console.WriteLine();
        }
    }
}

