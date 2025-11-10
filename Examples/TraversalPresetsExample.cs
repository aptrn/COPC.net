using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc;
using Copc.Geometry;
using Copc.Hierarchy;
using Copc.IO;

namespace Copc.Examples
{
    public static class TraversalPresetsExample
    {
        public static void Run(string copcFilePath)
        {
            Console.WriteLine("=== Traversal Presets + Custom Delegates Example ===\n");
            Console.WriteLine($"File: {copcFilePath}");

            if (!File.Exists(copcFilePath))
            {
                Console.WriteLine($"Error: File not found: {copcFilePath}");
                return;
            }

            using var reader = CopcReader.Open(copcFilePath);
            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            Console.WriteLine("Point Cloud Bounds:");
            Console.WriteLine($"  X: [{header.MinX:F3}, {header.MaxX:F3}]");
            Console.WriteLine($"  Y: [{header.MinY:F3}, {header.MaxY:F3}]");
            Console.WriteLine($"  Z: [{header.MinZ:F3}, {header.MaxZ:F3}]\n");

            // Derive some convenient test shapes
            var cloudBox = new Box(header.MinX, header.MinY, header.MinZ, header.MaxX, header.MaxY, header.MaxZ);
            var center = cloudBox.Center;
            double halfX = (header.MaxX - header.MinX) * 0.5;
            double halfY = (header.MaxY - header.MinY) * 0.5;
            double halfZ = (header.MaxZ - header.MinZ) * 0.5;

            // Box (20% of extent around center)
            var testBox = new Box(
                center.X - halfX * 0.2, center.Y - halfY * 0.2, center.Z - halfZ * 0.2,
                center.X + halfX * 0.2, center.Y + halfY * 0.2, center.Z + halfZ * 0.2);

            // Sphere radius at 25% of largest half extent
            double sphereRadius = Math.Max(halfX, Math.Max(halfY, halfZ)) * 0.25;
            var testSphere = new Sphere(center, sphereRadius);

            // Simple orthographic frustum from a viewing box in front of center (slightly offset on -Z)
            var frustumBox = new Box(
                center.X - halfX * 0.3, center.Y - halfY * 0.3, center.Z - halfZ * 0.05,
                center.X + halfX * 0.3, center.Y + halfY * 0.3, center.Z + halfZ * 0.6);
            var testFrustum = FrustumFromBox(frustumBox);

            // Choose a sample resolution target (~ one LOD deeper than root)
            double sampleResolution = Math.Max(1e-9, info.Spacing / 2.0);

            // 1) Preset: Bounding Box
            Console.WriteLine("--- Preset: Bounding Box ---");
            var boxOptions = TraversalPresets.Box(testBox, resolution: sampleResolution, continueAfterAccept: true);
            var boxNodes = reader.TraverseNodes(boxOptions);
            PrintNodeSummary(reader, boxNodes);

            // 2) Preset: Sphere (radius)
            Console.WriteLine("--- Preset: Sphere (Radius) ---");
            var sphereOptions = TraversalPresets.Sphere(testSphere, resolution: sampleResolution, continueAfterAccept: true);
            var sphereNodes = reader.TraverseNodes(sphereOptions);
            PrintNodeSummary(reader, sphereNodes);

            // 3) Preset: Distance From Point (same as sphere)
            Console.WriteLine("--- Preset: Distance From Point ---");
            var distOptions = TraversalPresets.DistanceFromPoint(center, sphereRadius, resolution: sampleResolution, continueAfterAccept: true);
            var distNodes = reader.TraverseNodes(distOptions);
            PrintNodeSummary(reader, distNodes);

            // 4) Preset: Frustum
            Console.WriteLine("--- Preset: Frustum ---");
            var frustumOptions = TraversalPresets.Frustum(testFrustum, resolution: sampleResolution, continueAfterAccept: true);
            var frustumNodes = reader.TraverseNodes(frustumOptions);
            PrintNodeSummary(reader, frustumNodes);

            // 5) Preset: Frustum with distance-based adaptive resolution
            Console.WriteLine("--- Preset: Frustum + Camera Distance Adaptive Resolution ---");
            // Pick a camera position in front of the frustum box along -Z
            var camera = new Vector3(center.X, center.Y, frustumBox.MinZ - (frustumBox.MaxZ - frustumBox.MinZ) * 0.2);
            double camSlope = info.Spacing * 0.01;      // spacing grows with distance
            double camMinRes = info.Spacing / 8.0;       // clamp minimum resolution near camera

            var frustumCameraOptions = TraversalPresets.FrustumWithDistanceResolution(
                testFrustum, camera, camSlope, camMinRes, continueAfterAccept: true);
            var frustumCameraNodes = reader.TraverseNodes(frustumCameraOptions);
            PrintNodeSummary(reader, frustumCameraNodes);

            // Custom delegate behaviors
            // A) Vertical slab selector: accept nodes within upper half Z slab (no resolution filter)
            Console.WriteLine("--- Custom: Upper Z Slab (spatial only) ---");
            var upperSlab = new Box(header.MinX, header.MinY, center.Z, header.MaxX, header.MaxY, header.MaxZ);
            var slabOptions = new TraversalOptions
            {
                SpatialPredicate = ctx => ctx.Bounds.Intersects(upperSlab),
                ResolutionPredicate = _ => true, // accept all nodes (no resolution filtering)
                ContinueAfterAccept = true
            };
            var slabNodes = reader.TraverseNodes(slabOptions);
            PrintNodeSummary(reader, slabNodes);

            // B) Adaptive resolution by distance to focus point (near = fine, far = coarse)
            Console.WriteLine("--- Custom: Adaptive Resolution by Distance ---");
            var focus = center;
            double slope = info.Spacing * 0.01; // meters of spacing per meter of distance
            double minResolution = info.Spacing / 8.0; // clamp to a fine minimum

            var adaptiveOptions = new TraversalOptions
            {
                SpatialPredicate = _ => true, // no spatial pruning
                ResolutionPredicate = ctx =>
                {
                    var c = ctx.Bounds.Center;
                    double dx = c.X - focus.X;
                    double dy = c.Y - focus.Y;
                    double dz = c.Z - focus.Z;
                    double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double desiredResolution = Math.Max(minResolution, slope * dist);
                    return ctx.NodeResolution <= desiredResolution;
                },
                ContinueAfterAccept = true
            };
            var adaptiveNodes = reader.TraverseNodes(adaptiveOptions);
            PrintNodeSummary(reader, adaptiveNodes);

            Console.WriteLine("\n✅ Done.");
        }

        private static void PrintNodeSummary(CopcReader reader, List<Node> nodes)
        {
            Console.WriteLine($"Nodes: {nodes.Count}");
            if (nodes.Count == 0)
            {
                Console.WriteLine();
                return;
            }

            long totalPoints = nodes.Sum(n => (long)n.PointCount);
            Console.WriteLine($"Total points in nodes: {totalPoints:N0}");

            var byDepth = nodes.GroupBy(n => n.Key.D).OrderBy(g => g.Key).ToList();
            Console.WriteLine("LOD Distribution:");
            foreach (var g in byDepth)
            {
                double res = VoxelKey.GetResolutionAtDepth(g.Key, reader.Config.LasHeader, reader.Config.CopcInfo);
                Console.WriteLine($"  LOD {g.Key}: {g.Count()} nodes @ res {res:F4}m");
            }
            Console.WriteLine();
        }

        private static Frustum FrustumFromBox(Box b)
        {
            // Create 6 planes with inward-facing normals for an orthographic frustum
            var left = Plane.FromNormalAndPoint(new Vector3(+1, 0, 0), new Vector3(b.MinX, 0, 0));
            var right = Plane.FromNormalAndPoint(new Vector3(-1, 0, 0), new Vector3(b.MaxX, 0, 0));
            var bottom = Plane.FromNormalAndPoint(new Vector3(0, +1, 0), new Vector3(0, b.MinY, 0));
            var top = Plane.FromNormalAndPoint(new Vector3(0, -1, 0), new Vector3(0, b.MaxY, 0));
            var near = Plane.FromNormalAndPoint(new Vector3(0, 0, +1), new Vector3(0, 0, b.MinZ));
            var far = Plane.FromNormalAndPoint(new Vector3(0, 0, -1), new Vector3(0, 0, b.MaxZ));
            return new Frustum(left, right, bottom, top, near, far);
        }
    }
}


