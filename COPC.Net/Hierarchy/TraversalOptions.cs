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
        /// returns a tuple indicating (1) whether to accept the entry, and (2) whether to continue traversing children.
        /// 
        /// Return values:
        /// - accept: true to add this node to results (only applies to nodes, not pages), false to skip it
        /// - continueToChildren: true to continue descending into children, false to stop and skip all descendants
        /// 
        /// This allows per-entry control over LOD cascade behavior. For example:
        /// - (true, true): Accept node/page and continue to finer LODs (traverse all levels)
        /// - (true, false): Accept node but stop here - skip all descendants (useful when desired resolution is reached)
        /// - (false, true): Skip this node but check children (useful for coarse-to-fine selection)
        /// - (false, false): Skip this entry and its entire subtree (prune this branch)
        /// 
        /// Note: This is called for both nodes and pages. Pages are containers and cannot be "accepted" 
        /// (accept is ignored for pages), but continueToChildren controls whether to traverse into them.
        /// For nodes, accept controls whether to include the node in results, and continueToChildren 
        /// controls whether to process child nodes at finer LOD levels.
        /// </summary>
        public Func<NodeTraversalContext, (bool accept, bool continueToChildren)> ResolutionPredicate { get; set; } = _ => (true, true);
    }
}


