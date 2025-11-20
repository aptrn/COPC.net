using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc;
using Stride.Core.Mathematics;
using CopcBox = Copc.Geometry.Box;
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
            var cloudBox = new CopcBox(header.MinX, header.MinY, header.MinZ, header.MaxX, header.MaxY, header.MaxZ);
            var center = new Vector3((float)((cloudBox.Min.X + cloudBox.Max.X) * 0.5),
                                     (float)((cloudBox.Min.Y + cloudBox.Max.Y) * 0.5),
                                     (float)((cloudBox.Min.Z + cloudBox.Max.Z) * 0.5));
            double halfX = (header.MaxX - header.MinX) * 0.5;
            double halfY = (header.MaxY - header.MinY) * 0.5;
            double halfZ = (header.MaxZ - header.MinZ) * 0.5;

            // Box (20% of extent around center)
            var testBox = new CopcBox(
                center.X - (float)(halfX * 0.2), center.Y - (float)(halfY * 0.2), center.Z - (float)(halfZ * 0.2),
                center.X + (float)(halfX * 0.2), center.Y + (float)(halfY * 0.2), center.Z + (float)(halfZ * 0.2)
            );

            // Sphere radius at 25% of largest half extent
            double sphereRadius = Math.Max(halfX, Math.Max(halfY, halfZ)) * 0.25;
            var testSphere = new BoundingSphere(center, (float)sphereRadius);

            // Simple orthographic frustum from a viewing box in front of center (slightly offset on -Z)
            var frustumBox = new BoundingBox(
                new Vector3((float)(center.X - halfX * 0.3), (float)(center.Y - halfY * 0.3), (float)(center.Z - halfZ * 0.05)),
                new Vector3((float)(center.X + halfX * 0.3), (float)(center.Y + halfY * 0.3), (float)(center.Z + halfZ * 0.6))
            );
            var testFrustum = FrustumFromBox(frustumBox);

            // Choose a sample resolution target (~ one LOD deeper than root)
            double sampleResolution = Math.Max(1e-9, info.Spacing / 2.0);

            // 1) Preset: Bounding Box
            Console.WriteLine("--- Preset: Bounding Box ---");
            var boxOptions = TraversalPresets.Box(testBox, resolution: sampleResolution, continueToChildren: true);
            var boxNodes = reader.TraverseNodes(boxOptions);
            PrintNodeSummary(reader, boxNodes);

            // 2) Preset: Sphere (radius)
            Console.WriteLine("--- Preset: Sphere (Radius) ---");
            var sphereOptions = TraversalPresets.Sphere(testSphere, resolution: sampleResolution, continueToChildren: true);
            var sphereNodes = reader.TraverseNodes(sphereOptions);
            PrintNodeSummary(reader, sphereNodes);

            // 3) Preset: Distance From Point (same as sphere)
            Console.WriteLine("--- Preset: Distance From Point ---");
            var distOptions = TraversalPresets.DistanceFromPoint(center, sphereRadius, resolution: sampleResolution, continueToChildren: true);
            var distNodes = reader.TraverseNodes(distOptions);
            PrintNodeSummary(reader, distNodes);

            // 4) Preset: Frustum
            Console.WriteLine("--- Preset: Frustum ---");
            var frustumOptions = TraversalPresets.Frustum(testFrustum, resolution: sampleResolution, continueToChildren: true);
            var frustumNodes = reader.TraverseNodes(frustumOptions);
            PrintNodeSummary(reader, frustumNodes);

            // 5) Preset: Frustum with distance-based adaptive resolution
            Console.WriteLine("--- Preset: Frustum + Camera Distance Adaptive Resolution ---");
            // Pick a camera position in front of the frustum box along -Z
            var camera = new Vector3(center.X, center.Y, frustumBox.Minimum.Z - (frustumBox.Maximum.Z - frustumBox.Minimum.Z) * 0.2f);
            double camSlope = info.Spacing * 0.01;      // spacing grows with distance
            double camMinRes = info.Spacing / 8.0;       // clamp minimum resolution near camera

            var frustumCameraOptions = TraversalPresets.FrustumWithDistanceResolution(
                testFrustum, camera, camSlope, camMinRes, continueToChildren: true);
            var frustumCameraNodes = reader.TraverseNodes(frustumCameraOptions);
            PrintNodeSummary(reader, frustumCameraNodes);

            // Custom delegate behaviors
            // A) Vertical slab selector: accept nodes within upper half Z slab (no resolution filter)
            Console.WriteLine("--- Custom: Upper Z Slab (spatial only) ---");
            var upperSlab = new CopcBox(
                header.MinX, header.MinY, center.Z, header.MaxX, header.MaxY, (float)header.MaxZ
            );
            var slabOptions = new TraversalOptions
            {
                SpatialPredicate = ctx => ctx.Bounds.Intersects(upperSlab),
                ResolutionPredicate = _ => (true, true) // accept all nodes and continue to children
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
                    var sb = ctx.Bounds.ToStride();
                    var c = (sb.Minimum + sb.Maximum) * 0.5f;
                    double dx = c.X - focus.X;
                    double dy = c.Y - focus.Y;
                    double dz = c.Z - focus.Z;
                    double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double desiredResolution = Math.Max(minResolution, slope * dist);
                    bool accept = ctx.NodeResolution <= desiredResolution;
                    return (accept, true); // accept if resolution is fine enough, continue to children
                }
            };
            var adaptiveNodes = reader.TraverseNodes(adaptiveOptions);
            PrintNodeSummary(reader, adaptiveNodes);

            // C) Camera W-based LOD (perspective depth with aggressive falloff)
            Console.WriteLine("--- Custom: Camera W-based Perspective LOD ---");
            var cameraPos = new Vector3(center.X, center.Y, (float)(center.Z - halfZ * 0.5)); // camera below and in front
            var viewDir = Vector3.Normalize(new Vector3(0, 0, 1)); // looking up/forward along +Z
            
            // Much more aggressive parameters to make distance-based reduction evident
            double wSlope = info.Spacing * 2.0;  // 2x spacing growth per meter (was 0.01!)
            double wMinRes = info.Spacing * 2.0;  // start at 2x root spacing (was /8, which was too fine)
            
            var cameraWOptions = new TraversalOptions
            {
                SpatialPredicate = _ => true, // no spatial pruning
                ResolutionPredicate = ctx =>
                {
                    var sb = ctx.Bounds.ToStride();
                    var nodeCenter = (sb.Minimum + sb.Maximum) * 0.5f;
                    
                    // Compute vector from camera to node
                    var toNode = nodeCenter - cameraPos;
                    
                    // Option 1: Simple Euclidean distance (similar to perspective w)
                    double distEuclidean = toNode.Length();
                    
                    // Option 2: Project onto view direction (more like actual perspective depth)
                    // This gives you the "w" component in view space
                    double distViewSpace = Math.Max(0, Vector3.Dot(toNode, viewDir));
                    
                    // Use view-space depth for more camera-like behavior
                    double w = distViewSpace;
                    
                    // Desired resolution grows linearly with w (distance from camera)
                    double desiredResolution = Math.Max(wMinRes, wSlope * w);
                    
                    bool accept = ctx.NodeResolution <= desiredResolution;
                    
                    // Continue to children to allow finer LODs where needed
                    return (accept, true);
                }
            };
            var cameraWNodes = reader.TraverseNodes(cameraWOptions);
            PrintNodeSummary(reader, cameraWNodes);
            
            // Print some diagnostics to show the LOD behavior
            Console.WriteLine("LOD Parameters:");
            Console.WriteLine($"  Camera Position: {cameraPos}");
            Console.WriteLine($"  View Direction: {viewDir}");
            Console.WriteLine($"  Root Spacing: {info.Spacing:F4}m");
            Console.WriteLine($"  Min Resolution: {wMinRes:F4}m (at camera)");
            Console.WriteLine($"  Slope: {wSlope:F4} (resolution per meter)");
            Console.WriteLine($"  At 10m: {Math.Max(wMinRes, wSlope * 10):F4}m resolution");
            Console.WriteLine($"  At 50m: {Math.Max(wMinRes, wSlope * 50):F4}m resolution");
            Console.WriteLine($"  At 100m: {Math.Max(wMinRes, wSlope * 100):F4}m resolution");
            Console.WriteLine();

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

        private static BoundingFrustum FrustumFromBox(BoundingBox b)
        {
            // Build a simple orthographic frustum around the box, looking along -Z
            var center = (b.Minimum + b.Maximum) * 0.5f;
            var size = b.Maximum - b.Minimum;
            float width = System.Math.Max(1e-6f, size.X);
            float height = System.Math.Max(1e-6f, size.Y);
            float depth = System.Math.Max(1e-6f, size.Z);

            // Camera placed in front of the box along +Z, looking toward -Z
            var eye = center + new Vector3(0, 0, depth);
            var target = center;
            var up = Vector3.UnitY;

            var view = Matrix.LookAtRH(eye, target, up);
            var proj = Matrix.OrthoRH(width, height, 0.001f, depth * 2f);
            var vp = view * proj; // Stride expects Matrix in row-major with typical mul order
            return new BoundingFrustum(ref vp);
        }
    }
}


