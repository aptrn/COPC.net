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
    /// Test demonstrating the continueToChildren behavior with all combinations using the new 3-boolean API:
    /// (isApproved, shouldDisplay, continueToChildren)
    /// (true, true, true): Approve, display, and continue - caches and displays everything
    /// (true, true, false): Approve, display, but stop - caches and displays just the first layer
    /// (false, false, true): Skip both, but continue - traverses all but takes nothing
    /// (false, false, false): Skip both and stop - traverses first layer only, takes nothing
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

            // Test 1: (true, true, true) - Approve, display, and continue to children
            Console.WriteLine("--- Test 1: (true, true, true) - Approve, display, and continue ---");
            Console.WriteLine("Expected: Accept ALL nodes at ALL levels for both cached and viewed");
            var options1 = new TraversalOptions
            {
                TraversalPredicate = _ => (true, true, true)
            };
            var result1 = reader.TraverseNodes(options1);
            PrintSummary(reader, result1, "All nodes");

            // Test 2: (true, true, false) - Approve, display, but stop at first layer
            Console.WriteLine("--- Test 2: (true, true, false) - Approve, display, but stop ---");
            Console.WriteLine("Expected: Accept ONLY root node (first layer) for both cached and viewed");
            var options2 = new TraversalOptions
            {
                TraversalPredicate = _ => (true, true, false)
            };
            var result2 = reader.TraverseNodes(options2);
            PrintSummary(reader, result2, "Root only");

            // Test 3: (false, false, true) - Skip both but continue to children
            Console.WriteLine("--- Test 3: (false, false, true) - Skip both but continue ---");
            Console.WriteLine("Expected: Accept NO nodes (traverses all but takes nothing)");
            var options3 = new TraversalOptions
            {
                TraversalPredicate = _ => (false, false, true)
            };
            var result3 = reader.TraverseNodes(options3);
            PrintSummary(reader, result3, "None");

            // Test 4: (false, false, false) - Skip both and stop at first layer
            Console.WriteLine("--- Test 4: (false, false, false) - Skip both and stop ---");
            Console.WriteLine("Expected: Accept NO nodes (traverses only root, takes nothing)");
            var options4 = new TraversalOptions
            {
                TraversalPredicate = _ => (false, false, false)
            };
            var result4 = reader.TraverseNodes(options4);
            PrintSummary(reader, result4, "None (root only traversed)");

            // Test 5: Dynamic - Accept only depth 0 and 1
            Console.WriteLine("--- Test 5: Dynamic - Accept depth 0 and 1 only ---");
            Console.WriteLine("Expected: Accept nodes at depth 0 and 1, continue to depth 2 but don't accept it");
            var options5 = new TraversalOptions
            {
                TraversalPredicate = ctx =>
                {
                    bool accept = ctx.Depth <= 1;
                    bool continueToChildren = ctx.Depth < 2; // Continue until we've processed depth 1's children
                    return (accept, accept, continueToChildren);
                }
            };
            var result5 = reader.TraverseNodes(options5);
            PrintSummary(reader, result5, "Depth 0 and 1");

            // Test 6: Different cached vs viewed - Cache all but only display depth 0 and 1
            Console.WriteLine("--- Test 6: Cache all, display depth 0-1 only ---");
            Console.WriteLine("Expected: CachedNodes has all nodes, ViewedNodes has only depth 0 and 1");
            var options6 = new TraversalOptions
            {
                TraversalPredicate = ctx =>
                {
                    bool isApproved = true; // Cache everything
                    bool shouldDisplay = ctx.Depth <= 1; // Only display depth 0 and 1
                    bool continueToChildren = true; // Continue to all children
                    return (isApproved, shouldDisplay, continueToChildren);
                }
            };
            var result6 = reader.TraverseNodes(options6);
            Console.WriteLine($"Result: Cached={result6.CachedNodes.Count} nodes, Viewed={result6.ViewedNodes.Count} nodes");
            Console.WriteLine($"Cached nodes:");
            PrintNodeList(reader, result6.CachedNodes);
            Console.WriteLine($"Viewed nodes:");
            PrintNodeList(reader, result6.ViewedNodes);
            Console.WriteLine();

            Console.WriteLine("\n✅ All tests complete!");
        }

        private static void PrintSummary(CopcReader reader, TraversalResult result, string label)
        {
            Console.WriteLine($"Result: Cached={result.CachedNodes.Count} nodes, Viewed={result.ViewedNodes.Count} nodes ({label})");
            
            if (result.ViewedNodes.Count > 0)
            {
                long totalPoints = result.ViewedNodes.Sum(n => (long)n.PointCount);
                Console.WriteLine($"Total viewed points: {totalPoints:N0}");

                var byDepth = result.ViewedNodes.GroupBy(n => n.Key.D)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Depth = g.Key, Count = g.Count() })
                    .ToList();

                Console.WriteLine("Viewed depth distribution:");
                foreach (var d in byDepth)
                {
                    double res = VoxelKey.GetResolutionAtDepth(d.Depth, reader.Config.LasHeader, reader.Config.CopcInfo);
                    Console.WriteLine($"  Depth {d.Depth}: {d.Count} nodes @ res {res:F6}m");
                }
            }
            Console.WriteLine();
        }

        private static void PrintNodeList(CopcReader reader, List<Node> nodes)
        {
            if (nodes.Count > 0)
            {
                long totalPoints = nodes.Sum(n => (long)n.PointCount);
                Console.WriteLine($"  Total: {nodes.Count} nodes, {totalPoints:N0} points");

                var byDepth = nodes.GroupBy(n => n.Key.D)
                    .OrderBy(g => g.Key)
                    .Select(g => new { Depth = g.Key, Count = g.Count() })
                    .ToList();

                Console.WriteLine("  Depth distribution:");
                foreach (var d in byDepth)
                {
                    double res = VoxelKey.GetResolutionAtDepth(d.Depth, reader.Config.LasHeader, reader.Config.CopcInfo);
                    Console.WriteLine($"    Depth {d.Depth}: {d.Count} nodes @ res {res:F6}m");
                }
            }
        }
    }
}
