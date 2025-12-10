using System;
using System.Collections.Generic;
using Copc;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Result of a hierarchy traversal operation.
    /// </summary>
    public sealed class TraversalResult
    {
        /// <summary>
        /// All nodes that were approved (first boolean = true).
        /// These are the nodes that should be cached.
        /// </summary>
        public List<Node> CachedNodes { get; set; } = new List<Node>();

        /// <summary>
        /// All nodes that should be displayed/viewed (second boolean = true).
        /// These are the nodes that should be rendered or processed for display.
        /// </summary>
        public List<Node> ViewedNodes { get; set; } = new List<Node>();
    }

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
        /// Traversal predicate: given the current entry context (node or page),
        /// returns a tuple with three booleans indicating:
        /// 1. isApproved: Whether the node is approved/accepted for caching
        /// 2. shouldDisplay: Whether the node should be displayed/viewed
        /// 3. continueToChildren: Whether to continue traversing to child nodes
        /// 
        /// Return values (isApproved, shouldDisplay, continueToChildren):
        /// - (true, true, true): Accept node, display it, and continue to finer LODs
        /// - (true, true, false): Accept and display node, but stop here - skip all descendants
        /// - (true, false, true): Accept node but don't display, check children (useful for loading coarser data while traversing)
        /// - (true, false, false): Accept node but don't display, skip descendants
        /// - (false, true, true): Don't accept but display, continue to children (unusual but allowed)
        /// - (false, true, false): Don't accept but display, skip descendants (unusual but allowed)
        /// - (false, false, true): Skip this node for both, but check children (useful for coarse-to-fine selection)
        /// - (false, false, false): Skip this entry and its entire subtree (prune this branch)
        /// 
        /// Note: This is called for both nodes and pages. Pages are containers and cannot be "accepted" or "displayed"
        /// (first two booleans are ignored for pages), but continueToChildren controls whether to traverse into them.
        /// For nodes:
        /// - isApproved controls whether to include the node in the "cached nodes" list
        /// - shouldDisplay controls whether to include the node in the "viewed nodes" list
        /// - continueToChildren controls whether to process child nodes at finer LOD levels
        /// </summary>
        public Func<NodeTraversalContext, (bool isApproved, bool shouldDisplay, bool continueToChildren)> TraversalPredicate { get; set; } = _ => (true, true, true);
    }
}


