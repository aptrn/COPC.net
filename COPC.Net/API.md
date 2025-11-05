# COPC.Net API Reference

## Namespace: Copc

### CopcInfo

COPC metadata stored in the COPC Info VLR.

```csharp
public class CopcInfo
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }
    public double HalfSize { get; set; }
    public double Spacing { get; set; }
    public ulong RootHierarchyOffset { get; set; }
    public ulong RootHierarchySize { get; set; }
    public double GpsTimeMinimum { get; set; }
    public double GpsTimeMaximum { get; set; }

    public Box GetCube();
    public static CopcInfo Parse(byte[] data);
    public byte[] ToBytes();
}
```

### CopcConfig

Configuration for a COPC file.

```csharp
public class CopcConfig
{
    public LasHeader LasHeader { get; set; }
    public CopcInfo CopcInfo { get; set; }
    public CopcExtents? CopcExtents { get; set; }
    public string? Wkt { get; set; }
}
```

### CopcExtents

Extended extents information.

```csharp
public class CopcExtents
{
    public Dictionary<string, (double Min, double Max)> Extents { get; set; }

    public void SetExtent(string dimensionName, double min, double max);
    public bool TryGetExtent(string dimensionName, out double min, out double max);
}
```

## Namespace: Copc.Geometry

### Vector3

3D vector with X, Y, Z coordinates.

```csharp
public struct Vector3
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public Vector3(double x, double y, double z);
    public static Vector3 DefaultScale();
    public static Vector3 DefaultOffset();
}
```

### Box

Axis-aligned bounding box.

```csharp
public struct Box
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }

    public Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ);
    public Box(Vector3 min, Vector3 max);
    public static Box FromCenterAndHalfSize(Vector3 center, double halfSize);

    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public Vector3 Center { get; }

    // Spatial predicates
    public bool Contains(Vector3 point);
    public bool Contains(Box other);
    public bool Within(Box other);
    public bool Intersects(Box other);
    public bool Crosses(Box other);
}
```

## Namespace: Copc.Hierarchy

### VoxelKey

Key for a voxel in the COPC octree hierarchy.

```csharp
public struct VoxelKey
{
    public int D { get; set; }  // Depth level
    public int X { get; set; }  // X coordinate
    public int Y { get; set; }  // Y coordinate
    public int Z { get; set; }  // Z coordinate

    public VoxelKey(int d, int x, int y, int z);
    public static VoxelKey InvalidKey();
    public static VoxelKey RootKey();

    public bool IsValid();
    public string ToString();
    public static VoxelKey FromString(string key);

    // Hierarchy navigation
    public VoxelKey Bisect(int direction);
    public List<VoxelKey> GetChildren();
    public VoxelKey GetParent();
    public VoxelKey GetParentAtDepth(int depth);
    public List<VoxelKey> GetParents(bool includeSelf = false);
    public bool ChildOf(VoxelKey parentKey);

    // Spatial operations
    public Box GetBounds(LasHeader header, CopcInfo copcInfo);
    public double GetResolution(LasHeader header, CopcInfo copcInfo);
    public static double GetSpanAtDepth(int depth, LasHeader header);
    public static double GetResolutionAtDepth(int depth, LasHeader header, CopcInfo copcInfo);
}
```

### Entry

Base class for Node and Page.

```csharp
public class Entry
{
    public const int EntrySize = 32;

    public VoxelKey Key { get; set; }
    public long Offset { get; set; }
    public int ByteSize { get; set; }
    public int PointCount { get; set; }

    public Entry();
    public Entry(VoxelKey key, long offset, int byteSize, int pointCount);

    public virtual bool IsValid();
    public virtual bool IsPage();

    public void Pack(BinaryWriter writer);
    public static Entry Unpack(BinaryReader reader);
}
```

### Node

Represents a voxel containing point data.

```csharp
public class Node : Entry
{
    public VoxelKey PageKey { get; set; }

    public Node();
    public Node(Entry entry, VoxelKey pageKey = default);
    public Node(VoxelKey key, long offset, int byteSize, int pointCount, VoxelKey pageKey = default);
}
```

### Page

Represents a voxel containing hierarchy data.

```csharp
public class Page : Entry
{
    public bool Loaded { get; set; }
    public Dictionary<string, Entry> Children { get; set; }

    public Page();
    public Page(Entry entry);
    public Page(VoxelKey key, long offset, int byteSize);

    public override bool IsValid();
    public override bool IsPage();
}
```

### HierarchySubtree

Result of parsing a hierarchy page.

```csharp
public class HierarchySubtree
{
    public Dictionary<string, Node> Nodes { get; set; }
    public Dictionary<string, Page> Pages { get; set; }
}
```

## Namespace: Copc.IO

### CopcReader

Reader for COPC files.

```csharp
public class CopcReader : IDisposable
{
    public CopcConfig Config { get; }

    // Opening files
    public static CopcReader Open(string filePath);
    public static CopcReader Open(Stream stream, bool leaveOpen = true);

    // Hierarchy operations
    public Page LoadRootHierarchyPage();
    public void LoadHierarchyPage(Page page);
    public Node? GetNode(VoxelKey key);
    public List<Node> GetAllNodes();

    // Point data operations
    public byte[] GetPointDataCompressed(Node node);
    public byte[] GetPointData(Node node);

    // Spatial queries
    public List<Node> GetNodesWithinBox(Box box, double resolution = 0);
    public List<Node> GetNodesIntersectBox(Box box, double resolution = 0);

    // Resolution queries
    public int GetDepthAtResolution(double resolution);
    public List<Node> GetNodesAtResolution(double resolution);

    public void Dispose();
}
```

### CopcWriter

Writer for COPC files (basic implementation).

```csharp
public class CopcWriter : IDisposable
{
    public static CopcWriter Create(string filePath, CopcConfigWriter config);
    public static CopcWriter Create(Stream stream, CopcConfigWriter config, bool leaveOpen = true);

    public void WriteHeader();
    public long WriteHierarchyPage(List<Entry> entries);

    public void Dispose();
}
```

## Common Patterns

### Opening and Reading

```csharp
using var reader = CopcReader.Open("file.copc.laz");
var info = reader.Config.CopcInfo;
var nodes = reader.GetAllNodes();
```

### Spatial Queries

```csharp
var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);
var nodes = reader.GetNodesIntersectBox(bbox);

foreach (var node in nodes)
{
    byte[] pointData = reader.GetPointData(node);
}
```

### Resolution-Based Loading

```csharp
// Get nodes at 1 meter resolution
var nodes = reader.GetNodesAtResolution(1.0);

// Or get depth level for resolution
int depth = reader.GetDepthAtResolution(1.0);
```

### Hierarchy Navigation

```csharp
var rootPage = reader.LoadRootHierarchyPage();

foreach (var kvp in rootPage.Children)
{
    if (kvp.Value is Node node)
    {
        Console.WriteLine($"Node: {node.PointCount} points");
    }
    else if (kvp.Value is Page page)
    {
        reader.LoadHierarchyPage(page);
        Console.WriteLine($"Page: {page.Children.Count} children");
    }
}
```

## Error Handling

Common exceptions:

- `InvalidDataException`: File is not a valid COPC file or data is corrupted
- `ObjectDisposedException`: Attempting to use a disposed reader/writer
- `FileNotFoundException`: File path does not exist
- `EndOfStreamException`: Unexpected end of file during read

Example:

```csharp
try
{
    using var reader = CopcReader.Open("file.copc.laz");
    // Work with reader...
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Invalid COPC file: {ex.Message}");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
```

## Performance Considerations

1. **Caching**: The reader caches loaded hierarchy pages and nodes
2. **Lazy Loading**: Hierarchy pages are only loaded when accessed
3. **Streaming**: Only load nodes that are needed for your query
4. **Memory**: Large files can require significant memory for full hierarchy traversal

### Best Practices

```csharp
// Good: Query only what you need
var nodes = reader.GetNodesIntersectBox(visibleArea, resolution: 1.0);

// Less efficient: Load everything
var allNodes = reader.GetAllNodes();

// Good: Progressive loading
for (double res = 10.0; res >= 0.5; res /= 2)
{
    var nodes = reader.GetNodesAtResolution(res);
    // Process nodes...
}
```

## Thread Safety

The `CopcReader` class is **not thread-safe**. Create separate reader instances for each thread or use appropriate synchronization.

```csharp
// NOT thread-safe
var reader = CopcReader.Open("file.copc.laz");
Parallel.ForEach(nodes, node => {
    var data = reader.GetPointData(node); // UNSAFE
});

// Thread-safe approach
Parallel.ForEach(nodes, node => {
    using var threadReader = CopcReader.Open("file.copc.laz");
    var data = threadReader.GetPointData(node); // Safe
});
```

