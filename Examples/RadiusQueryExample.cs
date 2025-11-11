using System;
using System.Linq;
using Copc.IO;
using Stride.Core.Mathematics;

namespace Copc.Examples
{
    /// <summary>
    /// Example demonstrating how to query COPC nodes using a spherical radius.
    /// This is useful for efficiently loading points within a certain distance from a position (omnidirectional query).
    /// </summary>
    public static class RadiusQueryExample
    {
        /// <summary>
        /// Basic example: Query nodes within a radius from a point
        /// </summary>
        public static void BasicRadiusQuery(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Define a center point and radius
            // In this example, we're querying points within 100 meters of position (500, 500, 50)
            double centerX = 500;
            double centerY = 500;
            double centerZ = 50;
            double radius = 100; // meters

            // Query nodes that intersect with the sphere
            var nodes = reader.GetNodesWithinRadius(centerX, centerY, centerZ, radius);

            Console.WriteLine($"Found {nodes.Count} nodes within {radius}m of ({centerX}, {centerY}, {centerZ})");
            Console.WriteLine($"Total points: {nodes.Sum(n => (long)n.PointCount):N0}");

            // Process each node
            foreach (var node in nodes)
            {
                var bounds = node.Key.GetBounds(reader.Config.LasHeader, reader.Config.CopcInfo);
                Console.WriteLine($"Node {node.Key}: {node.PointCount} points, bounds: {bounds}");
            }

            // Decompress and print points
            if (nodes.Count > 0)
            {
                Console.WriteLine("\n=== Decompressing Points ===");
                long totalPointsInNodes = nodes.Sum(n => (long)n.PointCount);
                Console.WriteLine($"Decompressing {nodes.Count} nodes ({totalPointsInNodes:N0} points)...\n");
                
                var allPoints = reader.GetPointsFromNodes(nodes);
                Console.WriteLine($"Decompressed {allPoints.Length:N0} points");

                // Filter by actual distance
                var pointsInRadius = allPoints.Where(p =>
                {
                    double dx = p.X - centerX;
                    double dy = p.Y - centerY;
                    double dz = p.Z - centerZ;
                    double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    return distance <= radius;
                }).ToArray();

                Console.WriteLine($"Points within radius: {pointsInRadius.Length:N0}\n");

                if (pointsInRadius.Length > 0)
                {
                    int pointsToPrint = Math.Min(15, pointsInRadius.Length);
                    Console.WriteLine($"Showing first {pointsToPrint} points:\n");

                    for (int i = 0; i < pointsToPrint; i++)
                    {
                        var p = pointsInRadius[i];
                        double dx = p.X - centerX;
                        double dy = p.Y - centerY;
                        double dz = p.Z - centerZ;
                        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        
                        PointPrintHelper.PrintPointWithDistance(i, p, distance, reader.Config.ExtraDimensions);
                    }
                    Console.WriteLine("\n✅ Complete!");
                }
            }
        }

        /// <summary>
        /// Advanced example: Query with resolution filtering
        /// </summary>
        public static void RadiusQueryWithResolution(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Define query parameters
            Vector3 center = new Vector3(500, 500, 50);
            double radius = 100; // meters

            // Query with a minimum resolution of 0.1 meters
            // This limits the level of detail to avoid loading too many points
            double targetResolution = 0.1;
            var nodes = reader.GetNodesWithinRadius(center, radius, targetResolution);

            Console.WriteLine($"Found {nodes.Count} nodes within {radius}m at resolution <= {targetResolution}m");
            
            // Group by depth to see the hierarchy
            var byDepth = nodes.GroupBy(n => n.Key.D).OrderBy(g => g.Key);
            foreach (var group in byDepth)
            {
                double resolution = Copc.Hierarchy.VoxelKey.GetResolutionAtDepth(
                    group.Key, 
                    reader.Config.LasHeader, 
                    reader.Config.CopcInfo
                );
                Console.WriteLine($"  Depth {group.Key} (res: {resolution:F3}m): {group.Count()} nodes, {group.Sum(n => (long)n.PointCount):N0} points");
            }
        }

        /// <summary>
        /// Example using Sphere object directly for multiple queries
        /// </summary>
        public static void ReusableSphereQuery(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Create sphere once and reuse it
            Vector3 center = new Vector3(500, 500, 50);
            double radius = 100;
            var sphere = new BoundingSphere(center, (float)radius);

            Console.WriteLine($"Sphere: {sphere}");

            // Query at different resolutions using the same sphere
            double[] resolutions = { 1.0, 0.5, 0.1 };
            
            foreach (double resolution in resolutions)
            {
                var nodes = reader.GetNodesWithinRadius(sphere, resolution);
                Console.WriteLine($"\nResolution {resolution}m: {nodes.Count} nodes, {nodes.Sum(n => (long)n.PointCount):N0} points");
            }
        }

        /// <summary>
        /// Example: Multiple concentric radius queries (e.g., for LOD rings)
        /// </summary>
        public static void ConcentricRadiusQueries(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Define a center point (e.g., camera position or point of interest)
            Vector3 center = new Vector3(500, 500, 50);

            // Query multiple distance ranges with different LOD levels
            var lodRings = new[]
            {
                (radius: 50.0, resolution: 0.05),   // Close range: high detail
                (radius: 100.0, resolution: 0.1),   // Medium range: medium detail
                (radius: 200.0, resolution: 0.25),  // Far range: low detail
                (radius: 500.0, resolution: 1.0)    // Very far: very low detail
            };

            Console.WriteLine($"LOD rings around position {center}:");
            
            foreach (var ring in lodRings)
            {
                var nodes = reader.GetNodesWithinRadius(center, ring.radius, ring.resolution);
                Console.WriteLine($"  Radius {ring.radius}m (res {ring.resolution}m): {nodes.Count} nodes, {nodes.Sum(n => (long)n.PointCount):N0} points");
            }
        }

        /// <summary>
        /// Example: Finding nearest neighbor candidates
        /// This gets nodes near a point, which can then be searched for the actual nearest points
        /// </summary>
        public static void NearestNeighborSearch(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Point to find neighbors for
            Vector3 queryPoint = new Vector3(500, 500, 50);
            
            // Start with a small search radius
            double searchRadius = 10;
            
            var nodes = reader.GetNodesWithinRadius(queryPoint, searchRadius);
            
            Console.WriteLine($"Finding nearest neighbors to {queryPoint}");
            Console.WriteLine($"Initial search radius: {searchRadius}m");
            Console.WriteLine($"Found {nodes.Count} nodes to search");
            
            if (nodes.Count == 0)
            {
                Console.WriteLine("No nodes found, try increasing the search radius");
            }
            else
            {
                Console.WriteLine($"Candidate nodes contain {nodes.Sum(n => (long)n.PointCount):N0} points");
                Console.WriteLine("Next step: Decompress these nodes and find actual nearest points");
                
                // In a real application, you would:
                // 1. Decompress the nodes to get individual points
                // 2. Calculate distances from queryPoint to each point
                // 3. Sort by distance and return the k nearest points
            }
        }

        /// <summary>
        /// Example: Progressive loading based on distance
        /// Useful for streaming applications where you load progressively more detailed data
        /// </summary>
        public static void ProgressiveLoadingByDistance(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            Vector3 center = new Vector3(500, 500, 50);
            
            // Progressive loading: start with low detail, then increase
            var loadingStages = new[]
            {
                (name: "Initial (coarse)", radius: 200.0, resolution: 1.0),
                (name: "Refine 1", radius: 200.0, resolution: 0.5),
                (name: "Refine 2", radius: 100.0, resolution: 0.1),
                (name: "High detail", radius: 50.0, resolution: 0.05)
            };

            Console.WriteLine("Progressive loading simulation:");
            
            foreach (var stage in loadingStages)
            {
                var nodes = reader.GetNodesWithinRadius(center, stage.radius, stage.resolution);
                long totalPoints = nodes.Sum(n => (long)n.PointCount);
                
                Console.WriteLine($"\n{stage.name}:");
                Console.WriteLine($"  Radius: {stage.radius}m, Resolution: {stage.resolution}m");
                Console.WriteLine($"  Nodes: {nodes.Count}, Points: {totalPoints:N0}");
                
                // In a real application, you would load and render these nodes here
            }
        }

        /// <summary>
        /// Example: Spatial density analysis
        /// Count points in spherical regions to understand point cloud density
        /// </summary>
        public static void SpatialDensityAnalysis(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            // Get the bounds of the entire point cloud
            var header = reader.Config.LasHeader;
            Vector3 cloudCenter = new Vector3(
                (float)((header.MaxX + header.MinX) / 2),
                (float)((header.MaxY + header.MinY) / 2),
                (float)((header.MaxZ + header.MinZ) / 2)
            );

            // Analyze density at different radii
            double[] radii = { 10, 25, 50, 100, 200 };
            
            Console.WriteLine($"Density analysis around center point {cloudCenter}:");
            
            foreach (double radius in radii)
            {
                var nodes = reader.GetNodesWithinRadius(cloudCenter, radius);
                long totalPoints = nodes.Sum(n => (long)n.PointCount);
                
                // Calculate volume and density
                double volume = (4.0 / 3.0) * Math.PI * radius * radius * radius;
                double density = totalPoints / volume;
                
                Console.WriteLine($"  Radius {radius}m: {totalPoints:N0} points, density: {density:F2} points/m³");
            }
        }

        /// <summary>
        /// Example: Filtering by distance
        /// Shows how to test if nodes are completely within or partially within the radius
        /// </summary>
        public static void FilterByDistanceRelation(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            Vector3 center = new Vector3(500, 500, 50);
            double radius = 100;
            var sphere = new BoundingSphere(center, (float)radius);

            var allNodes = reader.GetNodesWithinRadius(sphere);
            
            int fullyInside = 0;
            int partiallyInside = 0;

            foreach (var node in allNodes)
            {
                var bounds = node.Key.GetBounds(reader.Config.LasHeader, reader.Config.CopcInfo);
                
                if (sphere.Intersects(ref bounds))
                {
                    // Node bounding box partially intersects the sphere
                    partiallyInside++;
                    Console.WriteLine($"Node {node.Key}: PARTIALLY inside (need to filter {node.PointCount} points)");
                }
            }

            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"  Fully inside: {fullyInside} nodes");
            Console.WriteLine($"  Partially inside: {partiallyInside} nodes");
            Console.WriteLine($"  Total: {allNodes.Count} nodes");
        }

        /// <summary>
        /// Example: Comparing box query vs radius query
        /// Shows the difference between bounding box and spherical queries
        /// </summary>
        public static void CompareBoxVsRadiusQuery(string copcFilePath)
        {
            using var reader = CopcReader.Open(copcFilePath);

            Vector3 center = new Vector3(500, 500, 50);
            double radius = 100;

            // Radius query
            var radiusNodes = reader.GetNodesWithinRadius(center, radius);
            long radiusPoints = radiusNodes.Sum(n => (long)n.PointCount);

            // Equivalent box query (circumscribing box)
            var box = new BoundingBox(
                new Vector3(center.X - (float)radius, center.Y - (float)radius, center.Z - (float)radius),
                new Vector3(center.X + (float)radius, center.Y + (float)radius, center.Z + (float)radius)
            );
            var boxNodes = reader.GetNodesIntersectBox(box);
            long boxPoints = boxNodes.Sum(n => (long)n.PointCount);

            Console.WriteLine("Query comparison:");
            Console.WriteLine($"\nRadius query (sphere, r={radius}m):");
            Console.WriteLine($"  Nodes: {radiusNodes.Count}");
            Console.WriteLine($"  Points: {radiusPoints:N0}");
            
            Console.WriteLine($"\nBox query (circumscribing cube):");
            Console.WriteLine($"  Nodes: {boxNodes.Count}");
            Console.WriteLine($"  Points: {boxPoints:N0}");
            
            Console.WriteLine($"\nDifference:");
            Console.WriteLine($"  Extra nodes in box: {boxNodes.Count - radiusNodes.Count}");
            Console.WriteLine($"  Extra points in box: {(boxPoints - radiusPoints):N0}");
            Console.WriteLine($"  Efficiency: Radius query returns {(100.0 * radiusNodes.Count / boxNodes.Count):F1}% of box query nodes");

            // Decompress and print points from radius query
            if (radiusNodes.Count > 0)
            {
                Console.WriteLine("\n=== Decompressing Points from Radius Query ===");
                Console.WriteLine($"Decompressing {radiusNodes.Count} nodes ({radiusPoints:N0} points)...\n");
                
                var allPoints = reader.GetPointsFromNodes(radiusNodes);
                Console.WriteLine($"Decompressed {allPoints.Length:N0} points");

                // Filter by actual distance
                var pointsInRadius = allPoints.Where(p =>
                {
                    double dx = p.X - center.X;
                    double dy = p.Y - center.Y;
                    double dz = p.Z - center.Z;
                    double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    return distance <= radius;
                }).ToArray();

                Console.WriteLine($"Points within radius: {pointsInRadius.Length:N0}\n");

                if (pointsInRadius.Length > 0)
                {
                    int pointsToPrint = Math.Min(10, pointsInRadius.Length);
                    Console.WriteLine($"Showing first {pointsToPrint} points:\n");

                    for (int i = 0; i < pointsToPrint; i++)
                    {
                        var p = pointsInRadius[i];
                        double dx = p.X - center.X;
                        double dy = p.Y - center.Y;
                        double dz = p.Z - center.Z;
                        double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        
                        Console.WriteLine($"[{i,3}] X={p.X,12:F3} Y={p.Y,12:F3} Z={p.Z,12:F3} " +
                                        $"Distance={distance,8:F3}m");
                    }
                    Console.WriteLine("\n✅ Complete!");
                }
            }
        }
    }
}

