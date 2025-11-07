using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Copc.Cache;
using Copc.IO;

namespace Copc.Examples
{
	public static class CacheHeavyExample
	{
		// 8 GB in MB
		private const int CacheSizeMb = 8192;

		public static void Run(string copcFilePath, int passes = 3)
		{
			Console.WriteLine("=== Heavy Cache Stress Test (8GB) ===\n");

			if (!File.Exists(copcFilePath))
			{
				Console.WriteLine($"Error: File not found: {copcFilePath}");
				return;
			}

			using var cachedReader = CachedCopcReader.Open(copcFilePath, cacheSizeMB: CacheSizeMb);
			var cache = cachedReader.Cache;
			var header = cachedReader.Config.LasHeader;

			Console.WriteLine($"File bounds: X[{header.MinX:F2}, {header.MaxX:F2}], " +
				$"Y[{header.MinY:F2}, {header.MaxY:F2}], " +
				$"Z[{header.MinZ:F2}, {header.MaxZ:F2}]\n");
			Console.WriteLine($"Configured cache size: {CacheSizeMb / 1024.0:F2} GB");

			var allNodes = cachedReader.GetAllNodes();
			if (allNodes.Count == 0)
			{
				Console.WriteLine("No nodes found in file.");
				return;
			}

			// Shuffle nodes to avoid locality and create worst-case churn
			var rng = new Random(12345);
			List<Hierarchy.Node> Shuffle(List<Hierarchy.Node> source)
			{
				var list = source.ToList();
				for (int i = list.Count - 1; i > 0; i--)
				{
					int j = rng.Next(i + 1);
					(list[i], list[j]) = (list[j], list[i]);
				}
				return list;
			}

			// Print rough estimate of memory if everything were cached (based on estimated bytes/point)
			long approxTotalPoints = allNodes.Sum(n => (long)n.PointCount);
			Console.WriteLine($"Nodes: {allNodes.Count:N0}, Points: {approxTotalPoints:N0}");
			Console.WriteLine("Starting stress load in passes. This will progressively add more data to the cache.\n");

			var stopwatch = Stopwatch.StartNew();
			int batch = Math.Max(100, allNodes.Count / 50); // ~2% per progress log, min 100

			for (int pass = 1; pass <= Math.Max(1, passes); pass++)
			{
				Console.WriteLine($"=== Pass {pass}/{passes} ===");
				var nodes = Shuffle(allNodes);

				int loadedNodes = 0;
				long loadedPoints = 0;
				var passSw = Stopwatch.StartNew();

				foreach (var node in nodes)
				{
					var points = cachedReader.GetPointsFromNode(node);
					loadedNodes++;
					loadedPoints += points.Length;

					if (loadedNodes % batch == 0)
					{
						var stats = cache.GetStatistics();
						Console.WriteLine(
							$"  Loaded {loadedNodes:N0}/{nodes.Count:N0} nodes | " +
							$"Points: {loadedPoints:N0} | " +
							$"Mem: {stats.CurrentMemoryBytes / 1024.0 / 1024.0:F0} MB / {stats.MaxMemoryBytes / 1024.0 / 1024.0:F0} MB " +
							$"({stats.MemoryUsagePercent:F1}%) | Hits: {stats.TotalHits:N0} | Misses: {stats.TotalMisses:N0} | Evictions: {stats.TotalEvictions:N0}");
					}
				}

				passSw.Stop();
				var endStats = cache.GetStatistics();
				Console.WriteLine($"Pass {pass} complete in {passSw.Elapsed.TotalSeconds:F1}s. " +
					$"Cached nodes: {endStats.CachedNodeCount:N0}, Mem usage: {endStats.MemoryUsagePercent:F1}% (Evictions: {endStats.TotalEvictions:N0}).\n");
			}

			stopwatch.Stop();
			var finalStats = cache.GetStatistics();
			Console.WriteLine("=== Final Cache Statistics ===");
			Console.WriteLine(finalStats);
			Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F1}s");
		}
	}
}


