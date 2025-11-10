using Copc.Geometry;

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
        public static TraversalOptions Frustum(Frustum frustum, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => frustum.IntersectsBox(ctx.Bounds),
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Bounding-box traversal. Keeps nodes whose bounds intersect the given box.
        /// Matches previous GetNodesIntersectBox behavior.
        /// </summary>
        public static TraversalOptions Box(Box box, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => ctx.Bounds.Intersects(box),
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Sphere/radius traversal. Keeps nodes whose bounds intersect the sphere.
        /// Matches previous GetNodesWithinRadius behavior.
        /// </summary>
        public static TraversalOptions Sphere(Sphere sphere, double resolution = 0.0, bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => sphere.IntersectsBox(ctx.Bounds),
                ResolutionPredicate = ctx => resolution <= 0 || ctx.NodeResolution <= resolution,
                ContinueAfterAccept = continueAfterAccept
            };
        }

        /// <summary>
        /// Distance-from-point traversal. Equivalent to a sphere of given radius around center.
        /// </summary>
        public static TraversalOptions DistanceFromPoint(Vector3 center, double maxDistance, double resolution = 0.0, bool continueAfterAccept = true)
        {
            var sphere = new Sphere(center, maxDistance);
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
            Frustum frustum, 
            Vector3 viewpoint, 
            double resolutionSlope, 
            double minResolution, 
            bool continueAfterAccept = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => frustum.IntersectsBox(ctx.Bounds),
                ResolutionPredicate = ctx =>
                {
                    var c = ctx.Bounds.Center;
                    double dx = c.X - viewpoint.X;
                    double dy = c.Y - viewpoint.Y;
                    double dz = c.Z - viewpoint.Z;
                    double dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double desiredResolution = System.Math.Max(minResolution, resolutionSlope * dist);
                    return ctx.NodeResolution <= desiredResolution;
                },
                ContinueAfterAccept = continueAfterAccept
            };
        }
    }
}


