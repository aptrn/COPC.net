using System;
using Copc;
using Copc.Geometry;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Context passed to traversal delegates, describing the current entry (node or page)
    /// in the COPC hierarchy during traversal.
    /// </summary>
    public readonly struct NodeTraversalContext
    {
        public VoxelKey Key { get; }
        public Box Bounds { get; }
        public bool IsNode { get; }
        public int PointCount { get; }
        public LasHeader Header { get; }
        public CopcInfo CopcInfo { get; }

        public int Depth => Key.D;

        public NodeTraversalContext(VoxelKey key, Box bounds, bool isNode, int pointCount, LasHeader header, CopcInfo copcInfo)
        {
            Key = key;
            Bounds = bounds;
            IsNode = isNode;
            PointCount = pointCount;
            Header = header;
            CopcInfo = copcInfo;
        }

        /// <summary>
        /// Resolution (spacing) of the current key in world units.
        /// </summary>
        public double NodeResolution => VoxelKey.GetResolutionAtDepth(Key.D, Header, CopcInfo);
    }

    /// <summary>
    /// Options for generic hierarchy traversal.
    /// </summary>
    public sealed class TraversalOptions
    {
        /// <summary>
        /// Spatial predicate: given the current entry context (node or page),
        /// return true if this entry (and its subtree) should be considered.
        /// If this returns false, traversal prunes the entire subtree at this entry.
        /// </summary>
        public Func<NodeTraversalContext, bool> SpatialPredicate { get; set; } = _ => true;

        /// <summary>
        /// Resolution selector: given the current entry context (node or page),
        /// return the desired maximum spacing (resolution) for accepting nodes.
        /// If the returned value is &lt;= 0, resolution filtering is disabled for this entry.
        /// A node is accepted when nodeResolution &lt;= desiredResolution.
        /// </summary>
        public Func<NodeTraversalContext, double> DesiredResolution { get; set; } = _ => 0.0;

        /// <summary>
        /// If true, traversal continues descending even after accepting a node at a given depth.
        /// This can produce nodes at multiple depths (LOD cascade). If false, once a node is
        /// accepted at a given key, its subtree is not traversed further.
        /// </summary>
        public bool ContinueAfterAccept { get; set; } = true;
    }
}


