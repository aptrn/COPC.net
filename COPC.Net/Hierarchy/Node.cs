using System;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Represents a node in the COPC hierarchy that contains point data.
    /// A node is an entry where PointCount >= 0.
    /// </summary>
    public class Node : Entry
    {
        /// <summary>
        /// The key of the page that contains this node (if applicable).
        /// </summary>
        public VoxelKey PageKey { get; set; }

        public Node() : base()
        {
            PageKey = VoxelKey.InvalidKey();
        }

        public Node(Entry entry, VoxelKey pageKey = default) : base(entry.Key, entry.Offset, entry.ByteSize, entry.PointCount)
        {
            PageKey = pageKey;
        }

        public Node(VoxelKey key, long offset, int byteSize, int pointCount, VoxelKey pageKey = default)
            : base(key, offset, byteSize, pointCount)
        {
            PageKey = pageKey;
        }

        public override string ToString()
        {
            return $"Node {Key}: off={Offset}, size={ByteSize}, count={PointCount}, pageKey={PageKey}";
        }
    }
}

