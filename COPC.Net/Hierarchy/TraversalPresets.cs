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
        /// <param name="frustum">The frustum to test against.</param>
        /// <param name="resolution">Target resolution. If &gt; 0, nodes are accepted only if their resolution &lt;= this value.</param>
        /// <param name="continueToChildren">Whether to continue traversing to finer LODs after accepting a node.</param>
        public static TraversalOptions Frustum(BoundingFrustum frustum, double resolution = 0.0, bool continueToChildren = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx =>
                {
                    var sb = ctx.Bounds.ToStride();
                    var bext = new BoundingBoxExt(sb.Minimum, sb.Maximum);
                    return frustum.Contains(ref bext);
                },
                ResolutionPredicate = ctx =>
                {
                    bool accept = resolution <= 0 || ctx.NodeResolution <= resolution;
                    return (accept, continueToChildren);
                }
            };
        }

        /// <summary>
        /// Bounding-box traversal. Keeps nodes whose bounds intersect the given box.
        /// Matches previous GetNodesIntersectBox behavior.
        /// </summary>
        /// <param name="box">The bounding box to test against.</param>
        /// <param name="resolution">Target resolution. If &gt; 0, nodes are accepted only if their resolution &lt;= this value.</param>
        /// <param name="continueToChildren">Whether to continue traversing to finer LODs after accepting a node.</param>
        public static TraversalOptions Box(Copc.Geometry.Box box, double resolution = 0.0, bool continueToChildren = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx => ctx.Bounds.Intersects(box),
                ResolutionPredicate = ctx =>
                {
                    bool accept = resolution <= 0 || ctx.NodeResolution <= resolution;
                    return (accept, continueToChildren);
                }
            };
        }

        /// <summary>
        /// Sphere/radius traversal. Keeps nodes whose bounds intersect the sphere.
        /// Matches previous GetNodesWithinRadius behavior.
        /// </summary>
        /// <param name="sphere">The bounding sphere to test against.</param>
        /// <param name="resolution">Target resolution. If &gt; 0, nodes are accepted only if their resolution &lt;= this value.</param>
        /// <param name="continueToChildren">Whether to continue traversing to finer LODs after accepting a node.</param>
        public static TraversalOptions Sphere(BoundingSphere sphere, double resolution = 0.0, bool continueToChildren = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx =>
                {
                    var sb = ctx.Bounds.ToStride();
                    return sphere.Intersects(ref sb);
                },
                ResolutionPredicate = ctx =>
                {
                    bool accept = resolution <= 0 || ctx.NodeResolution <= resolution;
                    return (accept, continueToChildren);
                }
            };
        }

        /// <summary>
        /// Distance-from-point traversal. Equivalent to a sphere of given radius around center.
        /// </summary>
        /// <param name="center">The center point.</param>
        /// <param name="maxDistance">Maximum distance from center.</param>
        /// <param name="resolution">Target resolution. If &gt; 0, nodes are accepted only if their resolution &lt;= this value.</param>
        /// <param name="continueToChildren">Whether to continue traversing to finer LODs after accepting a node.</param>
        public static TraversalOptions DistanceFromPoint(Vector3 center, double maxDistance, double resolution = 0.0, bool continueToChildren = true)
        {
            var sphere = new BoundingSphere(center, (float)maxDistance);
            return Sphere(sphere, resolution, continueToChildren);
        }

        /// <summary>
        /// Frustum-based traversal with adaptive resolution based on distance from a point (typically a camera).
        /// Resolution increases (coarser) with distance from the viewpoint.
        /// </summary>
        /// <param name="frustum">The frustum to use for spatial culling.</param>
        /// <param name="viewpoint">The point from which distance is measured (typically camera position).</param>
        /// <param name="resolutionSlope">How much resolution increases per unit of distance (e.g., 0.01 means 1cm per meter).</param>
        /// <param name="minResolution">Minimum resolution clamp for nearby nodes.</param>
        /// <param name="continueToChildren">Whether to continue traversing to finer LODs after accepting a node.</param>
        public static TraversalOptions FrustumWithDistanceResolution(
            BoundingFrustum frustum, 
            Vector3 viewpoint, 
            double resolutionSlope, 
            double minResolution, 
            bool continueToChildren = true)
        {
            return new TraversalOptions
            {
                SpatialPredicate = ctx =>
                {
                    var sb = ctx.Bounds.ToStride();
                    var bext = new BoundingBoxExt(sb.Minimum, sb.Maximum);
                    return frustum.Contains(ref bext);
                },
                ResolutionPredicate = ctx =>
                {
                    var sb = ctx.Bounds.ToStride();
                    var center = (sb.Minimum + sb.Maximum) * 0.5f;
                    double dx = center.X - viewpoint.X;
                    double dy = center.Y - viewpoint.Y;
                    double dz = center.Z - viewpoint.Z;
                    double dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    double desiredResolution = System.Math.Max(minResolution, resolutionSlope * dist);
                    bool accept = ctx.NodeResolution <= desiredResolution;
                    return (accept, continueToChildren);
                }
            };
        }
    }
}


