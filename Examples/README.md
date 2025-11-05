# COPC.Net Examples

This project contains examples demonstrating how to use the COPC.Net library.

## Building

```bash
dotnet build
```

## Running Examples

### Display File Information

Shows comprehensive information about a COPC file including header data, COPC metadata, and hierarchy statistics.

```bash
dotnet run --project Examples info <path-to-copc-file>
```

Example output:
```
=== LAS Header ===
Version: 1.4
Point Format: 7
Point Count: 1000000
...

=== COPC Info ===
Center: (500000.0, 4500000.0, 100.0)
Half Size: 10000.0
...

=== Hierarchy Statistics ===
Total Nodes: 127
Nodes per depth level:
  Depth 0: 1 nodes, 1000 points, resolution=10.000000
  Depth 1: 8 nodes, 8000 points, resolution=5.000000
  ...
```

### Show Hierarchy Structure

Displays the hierarchy structure starting from the root page.

```bash
dotnet run --project Examples hierarchy <path-to-copc-file>
```

### Query by Bounding Box

Queries and displays nodes that intersect with a given bounding box.

```bash
dotnet run --project Examples bbox <path-to-copc-file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
```

Example:
```bash
dotnet run --project Examples bbox data.copc.laz 100 200 0 150 250 50
```

### Query by Resolution

Finds nodes at a specific resolution level.

```bash
dotnet run --project Examples resolution <path-to-copc-file> <resolution>
```

Example (0.5 meter resolution):
```bash
dotnet run --project Examples resolution data.copc.laz 0.5
```

### Sample Point Coordinates

Samples a random small bounding box and displays actual point coordinates.

```bash
dotnet run --project Examples sample <path-to-copc-file> [maxPoints]
```

Examples:
```bash
# Show up to 100 points (default)
dotnet run --project Examples sample data.copc.laz

# Show up to 50 points
dotnet run --project Examples sample data.copc.laz 50
```

This will:
- Pick a random small area (3% of file extent)
- Find nodes in that area
- Decompress and read actual point data
- Display X, Y, Z coordinates
- Show coordinate statistics

## Code Examples

### Basic Reading

```csharp
using var reader = CopcReader.Open("path/to/file.copc.laz");

// Access metadata
var info = reader.Config.CopcInfo;
Console.WriteLine($"Spacing: {info.Spacing}");

// Get all nodes
var nodes = reader.GetAllNodes();
Console.WriteLine($"Total nodes: {nodes.Count}");
```

### Spatial Query

```csharp
// Define area of interest
var bbox = new Box(
    minX: 100, minY: 200, minZ: 0,
    maxX: 150, maxY: 250, maxZ: 50
);

// Get nodes that intersect
var nodes = reader.GetNodesIntersectBox(bbox);

// Process each node
foreach (var node in nodes)
{
    // Get compressed data
    byte[] compressed = reader.GetPointDataCompressed(node);
    
    // Or get decompressed data
    byte[] decompressed = reader.GetPointData(node);
    
    Console.WriteLine($"Node {node.Key} has {node.PointCount} points");
}
```

### Progressive Resolution Loading

```csharp
// Start with low resolution
double[] resolutions = { 10.0, 5.0, 2.0, 1.0, 0.5 };

foreach (var resolution in resolutions)
{
    var nodes = reader.GetNodesAtResolution(resolution);
    
    Console.WriteLine($"Resolution {resolution}m: {nodes.Count} nodes");
    
    // Load and process points at this resolution
    foreach (var node in nodes)
    {
        var pointData = reader.GetPointData(node);
        // Process points...
    }
}
```

### Hierarchy Traversal

```csharp
// Load root page
var rootPage = reader.LoadRootHierarchyPage();

// Recursively traverse
void TraverseHierarchy(Page page, int indent = 0)
{
    if (!page.Loaded)
        reader.LoadHierarchyPage(page);
    
    foreach (var kvp in page.Children)
    {
        string prefix = new string(' ', indent * 2);
        
        if (kvp.Value is Node node)
        {
            Console.WriteLine($"{prefix}Node {node.Key}: {node.PointCount} points");
        }
        else if (kvp.Value is Page childPage)
        {
            Console.WriteLine($"{prefix}Page {childPage.Key}");
            TraverseHierarchy(childPage, indent + 1);
        }
    }
}

TraverseHierarchy(rootPage);
```

## Test Data

To test these examples, you need COPC files. You can:

1. Download sample COPC files from [COPC.io](https://copc.io/)
2. Convert existing LAS/LAZ files to COPC using [PDAL](https://pdal.io/) or [untwine](https://github.com/hobuinc/untwine)

### Converting LAS to COPC with PDAL

```bash
pdal translate input.las output.copc.laz --writers.copc.forward=all
```

## Common Use Cases

### 1. Progressive Streaming Viewer

Load low-resolution data first for quick preview, then progressively load higher resolution data:

```csharp
// Load coarse overview
var overviewNodes = reader.GetNodesAtResolution(10.0);

// Display overview...

// Then load detailed data for visible area
var detailNodes = reader.GetNodesIntersectBox(visibleBbox, resolution: 0.5);
```

### 2. Area-of-Interest Extraction

Extract all points within a specific area:

```csharp
var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);
var nodes = reader.GetNodesWithinBox(bbox);

foreach (var node in nodes)
{
    var points = reader.GetPointData(node);
    // Process or save points...
}
```

### 3. Multi-Resolution Analysis

Analyze data at different resolutions:

```csharp
for (int depth = 0; depth < 8; depth++)
{
    var resolution = VoxelKey.GetResolutionAtDepth(depth, 
        reader.Config.LasHeader, 
        reader.Config.CopcInfo);
    
    var nodes = reader.GetNodesAtResolution(resolution);
    
    Console.WriteLine($"Depth {depth}: {nodes.Count} nodes at {resolution:F3}m resolution");
}
```

## Performance Tips

1. **Cache Hierarchy Pages**: The reader automatically caches loaded pages
2. **Spatial Queries**: Use bounding box queries to minimize data loading
3. **Resolution Queries**: Start with coarse resolution for previews
4. **Dispose Properly**: Always dispose readers to free resources

```csharp
// Good - using statement ensures disposal
using (var reader = CopcReader.Open("file.copc.laz"))
{
    // Work with reader...
}

// Also good - explicit disposal
var reader = CopcReader.Open("file.copc.laz");
try
{
    // Work with reader...
}
finally
{
    reader.Dispose();
}
```

