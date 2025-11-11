using Stride.Core.Mathematics;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Ready-made factory methods for common traversal strategies.
    /// These return TraversalOptions configured to mirror the existing
    /// GetNodesIntersectFrustum / GetNodesIntersectBox / GetNodesWithinRadius behavior.
    /// </summary>
    public static class TraversalPresets
    {
        /// <summary>
        /// Frustum-based traversal. Keeps nodes whose bounds intersect the frustum.
        /// If resolution &gt; 0, only nodes with spacing &lt;= resolution are kept.
        /// </summary>
        public static TraversalOptions Frustum(BoundingFrustum frustum, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => { var b = ctx.Bounds; var bext = new BoundingBoxExt(b.Minimum, b.Maximum); return frustum.Contains(ref bext); },
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Bounding-box traversal. Keeps nodes whose bounds intersect the given box.
        /// Matches previous GetNodesIntersectBox behavior.
        /// </summary>
        public static TraversalOptions Box(BoundingBox box, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => { var b = ctx.Bounds; return b.Intersects(ref box); },
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Sphere/radius traversal. Keeps nodes whose bounds intersect the sphere.
        /// Matches previous GetNodesWithinRadius behavior.
        /// </summary>
        public static TraversalOptions Sphere(BoundingSphere sphere, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => { var b = ctx.Bounds; return sphere.Intersects(ref b); },
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Distance-from-point traversal. Equivalent to a sphere of given radius around center.
        /// </summary>
        public static TraversalOptions DistanceFromPoint(Vector3 center, double maxDistance, double resolution = 0.0, bool continueAfterAccept = true)
        {
            var sphere = new BoundingSphere(center, (float)maxDistance);
            return Sphere(sphere, resolution, continueAfterAccept);
        }

        /// <summary>
        /// Frustum-based traversal with adaptive resolution based on distance from a point (typically a camera).
        /// Resolution increases (coarser) with distance from the viewpoint.
        /// </summary>
        /// <param name="frustum">The frustum to use for spatial culling.</param>
        /// <param name="viewpoint">The point from which distance is measured (typically camera position).</param>
        /// <param name="resolutionSlope">How much resolution increases per unit of distance (e.g., 0.01 means 1cm per meter).</param>
        /// <param name="minResolution">Minimum resolution clamp for nearby nodes.</param>
        /// <param name="continueAfterAccept">Whether to continue traversing after accepting a node.</param>
        public static TraversalOptions FrustumWithDistanceResolution(
            BoundingFrustum frustum, 
            Vector3 viewpoint, 
            double resolutionSlope, 
            double minResolution, 
            bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => { var b = ctx.Bounds; var bext = new BoundingBoxExt(b.Minimum, b.Maximum); return frustum.Contains(ref bext); },
                ResolutionPredicate = ctx =>
                {
                    var center = (ctx.Bounds.Minimum + ctx.Bounds.Maximum) * 0.5f;
                    double dx = center.X - viewpoint.X;
                    double dy = center.Y - viewpoint.Y;
                    double dz = center.Z - viewpoint.Z;
                    double dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double desiredResolution = System.Math.Max(minResolution, resolutionSlope * dist);
                    return ctx.NodeResolution <= desiredResolution;
                },
                ContinueAfterAccept = continueAfterAccept
            };
        }
    }
}


