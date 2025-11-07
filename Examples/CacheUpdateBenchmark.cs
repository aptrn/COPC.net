using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Copc.Cache;
using Copc.Hierarchy;
using Copc.IO;

namespace Copc.Examples
{
	public static class CacheUpdateBenchmark
	{
		public static void Run(string copcFilePath, int cacheSizeMb = 512)
		{
			Console.WriteLine("=== Cache Update Benchmark ===\n");
			Console.WriteLine($"File: {copcFilePath}");
			Console.WriteLine($"Cache size: {cacheSizeMb} MB\n");

			if (!File.Exists(copcFilePath))
			{
				Console.WriteLine($"Error: File not found: {copcFilePath}");
				return;
			}

			using var cachedReader = CachedCopcReader.Open(copcFilePath, cacheSizeMb);

			// Collect nodes and sample sets
			var allNodes = cachedReader.Reader.GetAllNodes();
			if (allNodes.Count == 0)
			{
				Console.WriteLine("No nodes found in file.");
				return;
			}

			int firstSetCount = Math.Min(200, allNodes.Count);
			var rng = new Random(12345);
			var shuffled = allNodes.OrderBy(_ => rng.Next()).ToList();
			var first200 = shuffled.Take(firstSetCount).ToList();
			// Freeze node sets so both Stride and raw tests use exactly the same nodes
			var phase1Nodes = first200.ToArray();

			// Phase 1: 200 random nodes (Stride separated)
			Console.WriteLine($"Phase 1: Update with {phase1Nodes.Length} new nodes, then GetCacheDataSeparated()...");
			var t1 = UpdateAndGetSeparated(cachedReader, phase1Nodes);
			Console.WriteLine($"  Time: {t1.TotalMilliseconds:F1} ms\n");

			// Phase 2: 100 old (from first200) + 100 new (not in first200) (Stride separated)
			int oldCount = Math.Min(100, first200.Count);
			int remainingForNew = Math.Max(0, firstSetCount - oldCount); // handle small files gracefully
			var old100 = first200.OrderBy(_ => rng.Next()).Take(oldCount).ToList();
			var remainingPool = allNodes.Except(first200).OrderBy(_ => rng.Next()).ToList();
			int newCount = Math.Min(100, remainingPool.Count);
			var new100 = remainingPool.Take(newCount).ToList();
			var mixed200 = old100.Concat(new100).ToList();
			var phase2Nodes = mixed200.ToArray();
			var phase3Nodes = phase1Nodes; // same as phase 1

			Console.WriteLine($"Phase 2: Update with {old100.Count} old + {new100.Count} new nodes, then GetCacheDataSeparated()...");
			var t2 = UpdateAndGetSeparated(cachedReader, phase2Nodes);
			Console.WriteLine($"  Time: {t2.TotalMilliseconds:F1} ms\n");

			// Phase 3: same 200 nodes as Phase 1 (Stride separated)
			Console.WriteLine($"Phase 3: Update with same {phase3Nodes.Length} nodes, then GetCacheDataSeparated()...");
			var t3 = UpdateAndGetSeparated(cachedReader, phase3Nodes);
			Console.WriteLine($"  Time: {t3.TotalMilliseconds:F1} ms\n");

			Console.WriteLine("=== Summary (update + get separated data) ===");
			Console.WriteLine($"Phase 1: {t1.TotalMilliseconds:F1} ms");
			Console.WriteLine($"Phase 2: {t2.TotalMilliseconds:F1} ms");
			Console.WriteLine($"Phase 3: {t3.TotalMilliseconds:F1} ms");

			// Clear cache before raw section to ensure identical starting conditions
			cachedReader.Cache.Clear();

			// Repeat the same phases but retrieving raw CopcPoint[] from cache (no Stride conversion)
			Console.WriteLine();
			Console.WriteLine("=== Cache Update Benchmark (raw COPC points, no Stride) ===\n");

			Console.WriteLine($"Phase 1 (raw): Update with {phase1Nodes.Length} new nodes, then get CopcPoint[] from cache...");
			var t1raw = UpdateAndGetRawFromCache(cachedReader, phase1Nodes);
			Console.WriteLine($"  Time: {t1raw.TotalMilliseconds:F1} ms\n");

			Console.WriteLine($"Phase 2 (raw): Update with {old100.Count} old + {new100.Count} new nodes, then get CopcPoint[] from cache...");
			var t2raw = UpdateAndGetRawFromCache(cachedReader, phase2Nodes);
			Console.WriteLine($"  Time: {t2raw.TotalMilliseconds:F1} ms\n");

			Console.WriteLine($"Phase 3 (raw): Update with same {phase3Nodes.Length} nodes, then get CopcPoint[] from cache...");
			var t3raw = UpdateAndGetRawFromCache(cachedReader, phase3Nodes);
			Console.WriteLine($"  Time: {t3raw.TotalMilliseconds:F1} ms\n");

			Console.WriteLine("=== Summary (update + get raw CopcPoint[]) ===");
			Console.WriteLine($"Phase 1 (raw): {t1raw.TotalMilliseconds:F1} ms");
			Console.WriteLine($"Phase 2 (raw): {t2raw.TotalMilliseconds:F1} ms");
			Console.WriteLine($"Phase 3 (raw): {t3raw.TotalMilliseconds:F1} ms");
		}

		private static TimeSpan UpdateAndGetSeparated(CachedCopcReader cachedReader, IEnumerable<Node> nodes)
		{
			var sw = Stopwatch.StartNew();
			cachedReader.Update(nodes);
			var data = cachedReader.GetCacheDataSeparatedFromNodes(nodes);
			// Touch some fields to avoid dead-code elimination and report a tiny bit of context
			int count = data.Count;
			Console.WriteLine($"  Cached points (separated): {count:N0}");
			sw.Stop();
			return sw.Elapsed;
		}

		private static TimeSpan UpdateAndGetRawFromCache(CachedCopcReader cachedReader, IEnumerable<Node> nodes)
		{
			var sw = Stopwatch.StartNew();
			cachedReader.Update(nodes);
			var cache = cachedReader.Cache;
			var allPoints = new List<CopcPoint>();
			foreach (var node in nodes)
			{
				if (cache.TryGetPoints(node.Key, out var pts) && pts != null && pts.Length > 0)
				{
					allPoints.AddRange(pts);
				}
			}
			Console.WriteLine($"  Cached points (raw): {allPoints.Count:N0}");
			sw.Stop();
			return sw.Elapsed;
		}
	}
}


