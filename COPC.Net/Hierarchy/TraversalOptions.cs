using System;
using Copc;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Context passed to traversal delegates, describing the current entry (node or page)
    /// in the COPC hierarchy during traversal.
    /// </summary>
    public readonly struct NodeTraversalContext
    {
        public VoxelKey Key { get; }
        public Copc.Geometry.Box Bounds { get; }
        public bool IsNode { get; }
        public int PointCount { get; }
        public LasHeader Header { get; }
        public CopcInfo CopcInfo { get; }

        public int Depth => Key.D;

        public NodeTraversalContext(VoxelKey key, Copc.Geometry.Box bounds, bool isNode, int pointCount, LasHeader header, CopcInfo copcInfo)
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
        /// Resolution predicate: given the current entry context (node or page),
        /// returns a tuple indicating (1) whether to accept the node, and (2) whether to continue traversing children.
        /// 
        /// Return values:
        /// - accept: true to add this node to results, false to skip it
        /// - continueToChildren: true to continue descending into children, false to stop at this node
        /// 
        /// This allows per-node control over LOD cascade behavior. For example:
        /// - (true, true): Accept node and continue to finer LODs (common for progressive loading)
        /// - (true, false): Accept node but stop here (useful when desired resolution is reached)
        /// - (false, true): Skip this node but check children (useful for coarse-to-fine selection)
        /// - (false, false): Skip this node and its entire subtree
        /// 
        /// Note: This is only called for actual nodes, not for pages during traversal.
        /// </summary>
        public Func<NodeTraversalContext, (bool accept, bool continueToChildren)> ResolutionPredicate { get; set; } = _ => (true, true);
    }
}


