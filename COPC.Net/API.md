# COPC.Net API Documentation

## Overview

COPC.Net is a C# library for reading Cloud Optimized Point Cloud (COPC) files. It provides efficient access to COPC metadata, hierarchy, and compressed point data.

## Current Status

### ✅ Fully Supported Features

- **File Reading**: Open and read COPC files from file paths or streams
- **Header Parsing**: Access LAS header information
- **COPC Metadata**: Read COPC configuration (spacing, hierarchy offsets, extents)
- **Hierarchy Navigation**: Load and traverse the octree hierarchy
- **Spatial Queries**: Find nodes by bounding box or voxel key
- **Compressed Data Access**: Extract compressed LAZ chunks
- **WKT Support**: Read coordinate system information

### ⚠️ Limitations

- **Point Decompression**: LAZ decompression of COPC chunks is not currently implemented due to LasZip.Net compatibility limitations with COPC chunk format
- **Workarounds**: 
  - Extract compressed chunks and use external tools (PDAL, copclib)
  - Or read entire COPC file with external tools

## Core Classes

### CopcReader

Main class for reading COPC files.

```csharp
// Open a COPC file
using var reader = CopcReader.Open("data.copc.laz");

// Access configuration
var header = reader.Config.LasHeader;
var info = reader.Config.CopcInfo;
var wkt = reader.Config.Wkt;

// Load hierarchy
var rootPage = reader.LoadRootHierarchyPage();
var allNodes = reader.GetAllNodes();

// Query specific node
var key = new VoxelKey(4, 11, 9, 0);
var node = reader.GetNode(key);

// Get compressed data
if (node != null)
{
    byte[] compressed = reader.GetPointDataCompressed(node);
    // Use external tool to decompress
}

// Spatial query
var bbox = new Box(-5, 10, 50, 0, 15, 60);
var nodes = reader.GetNodesIntersectBox(bbox);
```

### CopcConfig

Contains COPC file configuration.

```csharp
public class CopcConfig
{
    public LasHeader LasHeader { get; }
    public CopcInfo CopcInfo { get; }
    public CopcExtents? CopcExtents { get; }
    public string? Wkt { get; }
}
```

### LasHeader

LAS file header information.

**Key Properties:**
- `VersionMajor`, `VersionMinor`: LAS version
- `PointDataFormat`: Point format ID (0-10)
- `PointDataRecordLength`: Size of each point in bytes
- `ExtendedNumberOfPointRecords`: Total number of points
- `MinX`, `MaxX`, `MinY`, `MaxY`, `MinZ`, `MaxZ`: File bounds
- `XScaleFactor`, `YScaleFactor`, `ZScaleFactor`: Coordinate scaling
- `XOffset`, `YOffset`, `ZOffset`: Coordinate offsets

### CopcInfo

COPC-specific metadata.

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
}
```

### VoxelKey

Identifies a node in the octree hierarchy.

```csharp
public class VoxelKey
{
    public int D { get; } // Depth
    public int X { get; } // X coordinate
    public int Y { get; } // Y coordinate
    public int Z { get; } // Z coordinate
    
    public VoxelKey(int d, int x, int y, int z);
    
    // Get spatial bounds of this voxel
    public Box GetBounds(LasHeader header, CopcInfo info);
    
    // Get resolution (point spacing) at this depth
    public double GetResolution(LasHeader header, CopcInfo info);
    
    // Check if this is a child of another key
    public bool ChildOf(VoxelKey other);
}
```

### Node

Represents a leaf node containing point data.

```csharp
public class Node : Entry
{
    public VoxelKey Key { get; }
    public long Offset { get; }      // File offset to compressed data
    public int ByteSize { get; }      // Size of compressed data
    public int PointCount { get; }    // Number of points in this node
    public VoxelKey PageKey { get; }  // Parent page key
}
```

### Page

Represents an interior node in the hierarchy.

```csharp
public class Page : Entry
{
    public VoxelKey Key { get; }
    public long Offset { get; }
    public int ByteSize { get; }
    public bool Loaded { get; }
    public Dictionary<string, Entry> Children { get; }
}
```

### Box

3D axis-aligned bounding box.

```csharp
public class Box
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
    
    public Box(double minX, double minY, double minZ,
               double maxX, double maxY, double maxZ);
    
    public bool Intersects(Box other);
    public bool Within(Box other);
    public bool Contains(double x, double y, double z);
}
```

## CopcReader Methods

### Opening Files

```csharp
// From file path
public static CopcReader Open(string filePath);

// From stream
public static CopcReader Open(Stream stream, bool leaveOpen = true);
```

### Hierarchy Access

```csharp
// Load root page
public Page LoadRootHierarchyPage();

// Load a specific page
public void LoadHierarchyPage(Page page);

// Get specific node by key
public Node? GetNode(VoxelKey key);

// Get all nodes (loads entire hierarchy)
public List<Node> GetAllNodes();
```

### Spatial Queries

```csharp
// Get nodes within bounding box (completely inside)
public List<Node> GetNodesWithinBox(Box box, double resolution = 0);

// Get nodes that intersect bounding box
public List<Node> GetNodesIntersectBox(Box box, double resolution = 0);

// Get nodes at specific resolution
public List<Node> GetNodesAtResolution(double resolution);

// Get depth level for desired resolution
public int GetDepthAtResolution(double resolution);
```

### Data Access

```csharp
// Get compressed point data for a node
public byte[] GetPointDataCompressed(Node node);

// Get compressed data (same as above, for compatibility)
public byte[] GetPointData(Node node);
```

## Usage Examples

### Basic File Reading

```csharp
using Copc.IO;

using var reader = CopcReader.Open("data.copc.laz");

Console.WriteLine($"Points: {reader.Config.LasHeader.ExtendedNumberOfPointRecords:N0}");
Console.WriteLine($"Format: {reader.Config.LasHeader.PointDataFormat}");
Console.WriteLine($"Bounds: X[{reader.Config.LasHeader.MinX},{reader.Config.LasHeader.MaxX}]");
```

### Hierarchy Navigation

```csharp
// Load root and explore
var root = reader.LoadRootHierarchyPage();
Console.WriteLine($"Root has {root.Children.Count} children");

// Get all nodes
var allNodes = reader.GetAllNodes();
Console.WriteLine($"Total nodes: {allNodes.Count}");
Console.WriteLine($"Total points: {allNodes.Sum(n => (long)n.PointCount):N0}");

// Group by depth
var byDepth = allNodes.GroupBy(n => n.Key.D);
foreach (var group in byDepth)
{
    Console.WriteLine($"Depth {group.Key}: {group.Count()} nodes");
}
```

### Spatial Query

```csharp
// Define bounding box
var bbox = new Box(
    minX: 100, minY: 200, minZ: 10,
    maxX: 150, maxY: 250, maxZ: 50
);

// Find intersecting nodes
var nodes = reader.GetNodesIntersectBox(bbox);
Console.WriteLine($"Found {nodes.Count} nodes");

// Get compressed data from each node
foreach (var node in nodes)
{
    byte[] compressed = reader.GetPointDataCompressed(node);
    Console.WriteLine($"Node {node.Key}: {compressed.Length} compressed bytes");
    
    // Save to file for external processing
    File.WriteAllBytes($"node_{node.Key}.laz.chunk", compressed);
}
```

### Query Specific Node

```csharp
// Query by voxel key
var key = new VoxelKey(depth: 4, x: 11, y: 9, z: 0);
var node = reader.GetNode(key);

if (node != null)
{
    Console.WriteLine($"Node {node.Key}:");
    Console.WriteLine($"  Points: {node.PointCount}");
    Console.WriteLine($"  Compressed: {node.ByteSize} bytes");
    
    var bounds = key.GetBounds(reader.Config.LasHeader, reader.Config.CopcInfo);
    Console.WriteLine($"  Bounds: X[{bounds.MinX:F2},{bounds.MaxX:F2}]");
}
```

### Level of Detail Queries

```csharp
// Get resolution at depth 5
var resolution = VoxelKey.GetResolutionAtDepth(5, reader.Config.LasHeader, reader.Config.CopcInfo);
Console.WriteLine($"Resolution at depth 5: {resolution:F6}");

// Find depth for desired resolution
int depth = reader.GetDepthAtResolution(0.1);
Console.WriteLine($"Depth for 0.1m resolution: {depth}");

// Get all nodes at that resolution
var nodes = reader.GetNodesAtResolution(0.1);
Console.WriteLine($"Nodes at 0.1m resolution: {nodes.Count}");
```

## Coordinate Transformations

Points in COPC files are stored as scaled integers. To convert to real-world coordinates:

```csharp
// Given a raw point coordinate (from decompressed data)
int rawX = ...; // From point data

// Convert to actual coordinate
double actualX = rawX * header.XScaleFactor + header.XOffset;
double actualY = rawY * header.YScaleFactor + header.YOffset;
double actualZ = rawZ * header.ZScaleFactor + header.ZOffset;
```

## Resolution and LOD

COPC uses an octree structure where each depth level has a specific resolution (point spacing):

```csharp
// Resolution at depth D:
double resolution = info.Spacing / Math.Pow(2, depth);

// Or use the helper:
double resolution = VoxelKey.GetResolutionAtDepth(depth, header, info);
```

**Depth levels:**
- Depth 0: Root node (coarsest)
- Depth 1: 8 children
- Depth 2: 64 children  
- Depth N: 8^N potential children
- Higher depth = finer resolution

## Working with Compressed Data

Since decompression is not yet implemented, you can extract compressed chunks and process them externally:

### Option 1: PDAL

```bash
# Convert entire file
pdal translate input.copc.laz output.las

# Or extract specific region
pdal translate input.copc.laz output.las \
  --filters.crop.bounds="([100,150],[200,250],[10,50])"
```

### Option 2: Python copclib

```python
import copclib as copc

reader = copc.FileReader("data.copc.laz")

# Query node
key = copc.VoxelKey(4, 11, 9, 0)
node = reader.FindNode(key)

# Get decompressed points
points = reader.GetPoints(node)
for point in points:
    print(f"({point.X}, {point.Y}, {point.Z})")
```

### Option 3: Export Chunks

```csharp
// Export all chunks for external processing
using var reader = CopcReader.Open("data.copc.laz");
var nodes = reader.GetAllNodes();

foreach (var node in nodes)
{
    var data = reader.GetPointDataCompressed(node);
    var filename = $"chunk_{node.Key.D}_{node.Key.X}_{node.Key.Y}_{node.Key.Z}.bin";
    File.WriteAllBytes(filename, data);
}
```

## Error Handling

```csharp
try
{
    using var reader = CopcReader.Open("data.copc.laz");
    // Use reader...
}
catch (FileNotFoundException)
{
    Console.WriteLine("File not found");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Invalid COPC file: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Best Practices

1. **Use `using` statements**: CopcReader implements IDisposable
2. **Cache hierarchy**: Call `GetAllNodes()` once if you need multiple queries
3. **Spatial filtering**: Use bounding box queries instead of loading all nodes
4. **Resolution filtering**: Specify target resolution to limit nodes loaded
5. **External tools**: For decompression, use PDAL or copclib

## Performance Tips

- Loading hierarchy is fast (< 100ms for typical files)
- Spatial queries only load necessary pages
- Compressed data access is direct file reads (very fast)
- Use resolution parameter to limit detail level
- Cache node lists when doing multiple queries

## Future Enhancements

Planned improvements:
- Native LAZ chunk decompression
- Streaming point access
- Write support
- Parallel loading
- Extended statistics

## See Also

- [COPC Specification](https://copc.io/)
- [LAS Specification](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities)
- [PDAL](https://pdal.io/)
- [copclib](https://github.com/RockRobotic/copc-lib)
