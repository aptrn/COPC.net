using System;
using System.Collections.Generic;

namespace Copc.Hierarchy
{
    /// <summary>
    /// Represents a page in the COPC hierarchy.
    /// A page contains references to child nodes or other pages.
    /// A page is an entry where PointCount == -1.
    /// </summary>
    public class Page : Entry
    {
        /// <summary>
        /// Indicates whether the page data has been loaded into memory.
        /// </summary>
        public bool Loaded { get; set; }

        /// <summary>
        /// The child entries (nodes or pages) contained in this page.
        /// Only populated when the page is loaded.
        /// </summary>
        public Dictionary<string, Entry> Children { get; set; }

        public Page() : base()
        {
            Loaded = false;
            Children = new Dictionary<string, Entry>();
        }

        public Page(Entry entry) : base(entry.Key, entry.Offset, entry.ByteSize, -1)
        {
            Loaded = false;
            Children = new Dictionary<string, Entry>();
        }

        public Page(VoxelKey key, long offset, int byteSize) : base(key, offset, byteSize, -1)
        {
            Loaded = false;
            Children = new Dictionary<string, Entry>();
        }

        public override bool IsValid()
        {
            // If a page is "loaded" it doesn't matter the offset/size
            return (Loaded || (Offset >= 0 && ByteSize >= 0)) && Key.IsValid();
        }

        public override bool IsPage()
        {
            return IsValid() && PointCount == -1;
        }

        public override string ToString()
        {
            return $"Page {Key}: off={Offset}, size={ByteSize}, loaded={Loaded}, children={Children.Count}";
        }
    }

    /// <summary>
    /// Result of parsing a hierarchy page, containing both nodes and sub-pages.
    /// </summary>
    public class HierarchySubtree
    {
        public Dictionary<string, Node> Nodes { get; set; }
        public Dictionary<string, Page> Pages { get; set; }

        public HierarchySubtree()
        {
            Nodes = new Dictionary<string, Node>();
            Pages = new Dictionary<string, Page>();
        }
    }
}

