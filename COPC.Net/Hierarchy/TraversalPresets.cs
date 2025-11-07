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
                DesiredResolution = _ => resolution,
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
                DesiredResolution = _ => resolution,
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
                DesiredResolution = _ => resolution,
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
    }
}


