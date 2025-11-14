using System;
using System.Diagnostics;
using System.Linq;
using Copc.Cache;
using Stride.Core.Mathematics;
using Copc.IO;
using CopcBox = Copc.Geometry.Box;

namespace Copc.Examples
{
    /// <summary>
    /// Demonstrates the smart caching system for efficient point cloud data access.
    /// </summary>
    public class CacheExample
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== COPC Smart Cache Example ===\n");

            // Example 1: Basic cache usage with manual management
            BasicCacheUsage(copcFilePath);

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 2: Using the convenient CachedCopcReader
            CachedReaderUsage(copcFilePath);

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 3: Cache performance comparison
            PerformanceComparison(copcFilePath);

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 4: Overlapping node requests (cache vs no cache)
            OverlappingNodesComparison(copcFilePath);

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 5: Cache statistics and monitoring
            CacheMonitoring(copcFilePath);
        }

        /// <summary>
        /// Example 1: Basic cache usage with manual management
        /// </summary>
        static void BasicCacheUsage(string copcFilePath)
        {
            Console.WriteLine("Example 1: Basic Cache Usage\n");

            using var reader = CopcReader.Open(copcFilePath);
            
            // Create a cache with 256 MB capacity
            var cache = PointCache.CreateWithMB(256);
            
            Console.WriteLine($"Created cache with {cache.MaxMemoryBytes / 1024.0 / 1024.0:F0} MB capacity\n");

            // Get some nodes
            var nodes = reader.GetNodesAtLayer(2);
            Console.WriteLine($"Found {nodes.Count} nodes at layer 2\n");

            // Load points through cache
            foreach (var node in nodes)
            {
                var points = cache.GetOrLoadPoints(node, reader);
                Console.WriteLine($"Node {node.Key}: {points.Length} points " +
                                $"(Cache: {(cache.Contains(node.Key) ? "HIT" : "MISS")})");
            }

            // Query same nodes again - should all be cache hits
            Console.WriteLine("\nQuerying same nodes again (should be cached):");
            foreach (var node in nodes)
            {
                var wasCached = cache.Contains(node.Key);
                var points = cache.GetOrLoadPoints(node, reader);
                Console.WriteLine($"Node {node.Key}: {points.Length} points " +
                                $"(Cache: {(wasCached ? "HIT" : "MISS")})");
            }

            Console.WriteLine($"\nCache stats: {cache.GetStatistics()}");
        }

        /// <summary>
        /// Example 2: Using the convenient CachedCopcReader
        /// </summary>
        static void CachedReaderUsage(string copcFilePath)
        {
            Console.WriteLine("Example 2: Cached Reader Usage\n");

            // Open file with automatic caching (512 MB cache)
            using var cachedReader = CachedCopcReader.Open(copcFilePath, cacheSizeMB: 512);
            
            var header = cachedReader.Config.LasHeader;
            Console.WriteLine($"File bounds: X[{header.MinX:F2}, {header.MaxX:F2}], " +
                            $"Y[{header.MinY:F2}, {header.MaxY:F2}], " +
                            $"Z[{header.MinZ:F2}, {header.MaxZ:F2}]\n");

            // Define a bounding box for spatial query
            double centerX = (header.MinX + header.MaxX) / 2;
            double centerY = (header.MinY + header.MaxY) / 2;
            double centerZ = (header.MinZ + header.MaxZ) / 2;
            double size = (header.MaxX - header.MinX) * 0.1; // 10% of total size

            var center = new Vector3((float)centerX, (float)centerY, (float)centerZ);
            var half = (float)size;
            var box = new CopcBox(centerX - half, centerY - half, centerZ - half, centerX + half, centerY + half, centerZ + half);

            Console.WriteLine($"Querying box around center: size={size:F2}\n");

            // First query - will load from disk
            var sw = Stopwatch.StartNew();
            var points1 = cachedReader.GetPointsInBox(box);
            sw.Stop();
            Console.WriteLine($"First query: {points1.Length} points in {sw.ElapsedMilliseconds} ms");

            // Second query - should be mostly cached
            sw.Restart();
            var points2 = cachedReader.GetPointsInBox(box);
            sw.Stop();
            Console.WriteLine($"Second query: {points2.Length} points in {sw.ElapsedMilliseconds} ms (cached)");

            // Show cache statistics
            var stats = cachedReader.Cache.GetStatistics();
            Console.WriteLine($"\n{stats}");
        }

        /// <summary>
        /// Example 3: Performance comparison between cached and uncached access
        /// </summary>
        static void PerformanceComparison(string copcFilePath)
        {
            Console.WriteLine("Example 3: Performance Comparison\n");

            using var reader = CopcReader.Open(copcFilePath);
            var cache = PointCache.CreateWithMB(512);

            // Get a set of nodes to query repeatedly
            var nodes = reader.GetNodesAtLayer(3);
            if (nodes.Count == 0)
            {
                Console.WriteLine("No nodes at layer 3, trying layer 2...");
                nodes = reader.GetNodesAtLayer(2);
            }
            if (nodes.Count == 0)
            {
                Console.WriteLine("No nodes found, skipping performance test");
                return;
            }

            // Take first 10 nodes for testing
            var testNodes = nodes.GetRange(0, Math.Min(10, nodes.Count));
            Console.WriteLine($"Testing with {testNodes.Count} nodes\n");

            // Test 1: Without cache (cold reads)
            Console.WriteLine("Test 1: Reading without cache (3 iterations)");
            var timesUncached = new long[3];
            for (int i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                int totalPoints = 0;
                foreach (var node in testNodes)
                {
                    var points = reader.GetPointsFromNode(node);
                    totalPoints += points.Length;
                }
                sw.Stop();
                timesUncached[i] = sw.ElapsedMilliseconds;
                Console.WriteLine($"  Iteration {i + 1}: {timesUncached[i]} ms ({totalPoints} points)");
            }

            // Test 2: With cache (first load + cached reads)
            Console.WriteLine("\nTest 2: Reading with cache (3 iterations)");
            cache.Clear(); // Start fresh
            var timesCached = new long[3];
            for (int i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                int totalPoints = 0;
                foreach (var node in testNodes)
                {
                    var points = cache.GetOrLoadPoints(node, reader);
                    totalPoints += points.Length;
                }
                sw.Stop();
                timesCached[i] = sw.ElapsedMilliseconds;
                var hitRate = cache.HitRate;
                Console.WriteLine($"  Iteration {i + 1}: {timesCached[i]} ms ({totalPoints} points, {hitRate:F1}% hit rate)");
            }

            // Calculate speedup
            double avgUncached = (timesUncached[1] + timesUncached[2]) / 2.0; // Skip first for fairness
            double avgCached = (timesCached[1] + timesCached[2]) / 2.0;
            double speedup = avgUncached / avgCached;
            
            Console.WriteLine($"\nAverage time without cache: {avgUncached:F1} ms");
            Console.WriteLine($"Average time with cache: {avgCached:F1} ms");
            Console.WriteLine($"Speedup: {speedup:F2}x faster");
        }

        /// <summary>
        /// Example 4: Overlapping node requests - demonstrates cache effectiveness
        /// </summary>
        static void OverlappingNodesComparison(string copcFilePath)
        {
            Console.WriteLine("Example 4: Overlapping Node Requests - Cache vs No Cache\n");

            using var reader = CopcReader.Open(copcFilePath);
            var allNodes = reader.GetAllNodes();
            
            if (allNodes.Count < 20)
            {
                Console.WriteLine($"Not enough nodes in file ({allNodes.Count}), skipping test");
                return;
            }

            // Select nodes for testing
            var firstBatch = allNodes.Take(10).ToList();
            // Second batch: 5 from first batch (overlap) + 5 new nodes
            var secondBatch = allNodes.Skip(5).Take(10).ToList();

            Console.WriteLine($"First batch:  10 nodes (indices 0-9)");
            Console.WriteLine($"Second batch: 10 nodes (indices 5-14)");
            Console.WriteLine($"Overlap:      5 nodes (indices 5-9)");
            Console.WriteLine($"New in 2nd:   5 nodes (indices 10-14)\n");

            // ========================================
            // Test 1: WITH CACHE
            // ========================================
            Console.WriteLine("=== WITH CACHE ===\n");
            var cache = PointCache.CreateWithMB(256);
            
            // First batch
            var sw1 = Stopwatch.StartNew();
            int totalPoints1 = 0;
            foreach (var node in firstBatch)
            {
                var points = cache.GetOrLoadPoints(node, reader);
                totalPoints1 += points.Length;
            }
            sw1.Stop();
            
            var stats1 = cache.GetStatistics();
            Console.WriteLine($"Batch 1: {firstBatch.Count} nodes, {totalPoints1:N0} points");
            Console.WriteLine($"  Time: {sw1.ElapsedMilliseconds} ms");
            Console.WriteLine($"  Cache hits: {stats1.TotalHits}, misses: {stats1.TotalMisses}");
            Console.WriteLine($"  Hit rate: {stats1.HitRate:F1}% (expected: 0% - all cold)");
            Console.WriteLine($"  Cached nodes: {cache.Count}");

            // Second batch (with overlap)
            var sw2 = Stopwatch.StartNew();
            int totalPoints2 = 0;
            foreach (var node in secondBatch)
            {
                var points = cache.GetOrLoadPoints(node, reader);
                totalPoints2 += points.Length;
            }
            sw2.Stop();
            
            var stats2 = cache.GetStatistics();
            long batch2Hits = stats2.TotalHits - stats1.TotalHits;
            long batch2Misses = stats2.TotalMisses - stats1.TotalMisses;
            
            Console.WriteLine($"\nBatch 2: {secondBatch.Count} nodes, {totalPoints2:N0} points");
            Console.WriteLine($"  Time: {sw2.ElapsedMilliseconds} ms");
            Console.WriteLine($"  Cache hits: {batch2Hits}, misses: {batch2Misses}");
            Console.WriteLine($"  Hit rate: {(double)batch2Hits / (batch2Hits + batch2Misses) * 100:F1}% (expected: ~50% - half cached)");
            Console.WriteLine($"  Cached nodes: {cache.Count}");
            
            long totalTimeCached = sw1.ElapsedMilliseconds + sw2.ElapsedMilliseconds;
            Console.WriteLine($"\nTotal time WITH cache: {totalTimeCached} ms");
            Console.WriteLine($"Overall hit rate: {stats2.HitRate:F1}%");

            // ========================================
            // Test 2: WITHOUT CACHE
            // ========================================
            Console.WriteLine("\n=== WITHOUT CACHE ===\n");
            
            // First batch (no cache)
            var sw3 = Stopwatch.StartNew();
            int totalPoints3 = 0;
            foreach (var node in firstBatch)
            {
                var points = reader.GetPointsFromNode(node);
                totalPoints3 += points.Length;
            }
            sw3.Stop();
            
            Console.WriteLine($"Batch 1: {firstBatch.Count} nodes, {totalPoints3:N0} points");
            Console.WriteLine($"  Time: {sw3.ElapsedMilliseconds} ms");
            Console.WriteLine($"  All loaded from disk");

            // Second batch (no cache - has to reload everything)
            var sw4 = Stopwatch.StartNew();
            int totalPoints4 = 0;
            foreach (var node in secondBatch)
            {
                var points = reader.GetPointsFromNode(node);
                totalPoints4 += points.Length;
            }
            sw4.Stop();
            
            Console.WriteLine($"\nBatch 2: {secondBatch.Count} nodes, {totalPoints4:N0} points");
            Console.WriteLine($"  Time: {sw4.ElapsedMilliseconds} ms");
            Console.WriteLine($"  All loaded from disk (including 5 overlapping nodes!)");
            
            long totalTimeUncached = sw3.ElapsedMilliseconds + sw4.ElapsedMilliseconds;
            Console.WriteLine($"\nTotal time WITHOUT cache: {totalTimeUncached} ms");

            // ========================================
            // Comparison
            // ========================================
            Console.WriteLine("\n=== COMPARISON ===\n");
            Console.WriteLine($"With cache:    {totalTimeCached} ms");
            Console.WriteLine($"Without cache: {totalTimeUncached} ms");
            
            if (totalTimeCached < totalTimeUncached)
            {
                double speedup = (double)totalTimeUncached / totalTimeCached;
                long timeSaved = totalTimeUncached - totalTimeCached;
                Console.WriteLine($"Speedup:       {speedup:F2}x faster with cache");
                Console.WriteLine($"Time saved:    {timeSaved} ms ({timeSaved / (double)totalTimeUncached * 100:F1}% reduction)");
            }
            else
            {
                Console.WriteLine($"Note: Cache overhead may dominate for very small datasets or fast storage");
            }

            // Explain the benefit
            Console.WriteLine("\n💡 Why caching helps:");
            Console.WriteLine($"   • Batch 1: Both methods load 10 nodes from disk (similar time)");
            Console.WriteLine($"   • Batch 2 WITHOUT cache: Reloads all 10 nodes including 5 that were just loaded!");
            Console.WriteLine($"   • Batch 2 WITH cache: Only loads 5 new nodes, serves 5 from cache");
            Console.WriteLine($"   • Result: Cache saves ~50% of disk I/O in second batch");
        }

        /// <summary>
        /// Example 5: Cache monitoring and statistics
        /// </summary>
        static void CacheMonitoring(string copcFilePath)
        {
            Console.WriteLine("Example 5: Cache Monitoring\n");

            using var reader = CopcReader.Open(copcFilePath);
            
            // Create a smaller cache to see eviction behavior
            var cache = PointCache.CreateWithMB(64); // Only 64 MB
            
            Console.WriteLine($"Created cache with {cache.MaxMemoryBytes / 1024.0 / 1024.0:F0} MB capacity");
            Console.WriteLine("Loading nodes until cache is full...\n");

            var nodes = reader.GetAllNodes();
            int loadedCount = 0;
            
            foreach (var node in nodes)
            {
                var points = cache.GetOrLoadPoints(node, reader);
                loadedCount++;
                
                // Print status every 10 nodes
                if (loadedCount % 10 == 0)
                {
                    var stats = cache.GetStatistics();
                    Console.WriteLine($"Loaded {loadedCount} nodes: " +
                                    $"{stats.CurrentMemoryBytes / 1024.0 / 1024.0:F2} MB / " +
                                    $"{stats.MaxMemoryBytes / 1024.0 / 1024.0:F2} MB " +
                                    $"({stats.MemoryUsagePercent:F1}%), " +
                                    $"{stats.CachedNodeCount} cached, " +
                                    $"{stats.TotalEvictions} evictions");
                }

                // Stop when we've seen some evictions
                if (cache.TotalEvictions > 20)
                    break;
            }

            Console.WriteLine($"\nFinal statistics:");
            Console.WriteLine(cache.GetStatistics());

            // Show some cached node details
            Console.WriteLine("\nTop 5 most recently accessed nodes:");
            var cachedNodes = cache.GetCachedNodes();
            for (int i = 0; i < Math.Min(5, cachedNodes.Count); i++)
            {
                var nodeInfo = cachedNodes[i];
                Console.WriteLine($"  {nodeInfo.Key}: {nodeInfo.PointCount} points, " +
                                $"{nodeInfo.MemorySize / 1024.0:F2} KB, " +
                                $"accessed {nodeInfo.AccessCount} times");
            }
        }
    }
}

