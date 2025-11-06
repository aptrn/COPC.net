# COPC.Net Examples

This project contains examples demonstrating how to use the COPC.Net library to read and decompress COPC point cloud files using LasZipNetStandard.

## Building

```bash
dotnet build
```

## Available Examples

The examples demonstrate different ways to query and extract points from COPC files:

1. **random** - Pick a random bounding box at specified LOD
2. **bbox-lod** - Query specific bounding box at specific LOD (sequential decompression)
3. **bbox-res** - Query specific bounding box at specific resolution
4. **bbox-info** - Show information about a bounding box across all LODs
5. **preprocess** - Extract all node chunks for optimized queries (NEW!)
6. **bbox-chunked** - Query with selective chunk loading (Performance Optimized!) (NEW!)

## Usage

### 1. Random Bounding Box with LOD

Picks a random bounding box (10% of cloud extent) at a specified LOD and decompresses points within it.

```bash
dotnet run --project Examples random <copc-file> <lod-depth>
```

Example:
```bash
dotnet run --project Examples random temp/test.copc.laz 3
```

This will:
- Load the COPC file and read cloud bounds
- Pick a random bounding box within the cloud bounds
- Find all nodes at the specified LOD that intersect the bounding box
- Decompress points and filter to those within the bounding box
- Display first 20 points and statistics

### 2. Bounding Box Query with LOD

Query a specific bounding box at a specific LOD.

```bash
dotnet run --project Examples bbox-lod <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
```

Example:
```bash
dotnet run --project Examples bbox-lod temp/test.copc.laz 3 -5 15 1055 5 20 1065
```

This will:
- Query nodes at LOD 3 that intersect the specified bounding box
- Show details of all matching nodes
- Decompress points and filter to the bounding box
- Display points and statistics

### 3. Bounding Box Query with Resolution

Query a specific bounding box at a target resolution (the library automatically finds the appropriate LOD).

```bash
dotnet run --project Examples bbox-res <copc-file> <resolution> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
```

Example:
```bash
dotnet run --project Examples bbox-res temp/test.copc.laz 0.02 -5 15 1055 5 20 1065
```

This will:
- Calculate the LOD that provides the target resolution
- Query nodes at that LOD that intersect the bounding box
- Show resolution and LOD information
- Decompress points and display results

### 4. Bounding Box Information Across LODs

Show information about a bounding box across all LOD levels without decompressing points.

```bash
dotnet run --project Examples bbox-info <copc-file> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
```

Example:
```bash
dotnet run --project Examples bbox-info temp/test.copc.laz -5 15 1055 5 20 1065
```

This will:
- Find all nodes across all LODs that intersect the bounding box
- Display a table showing:
  - LOD level
  - Resolution at that LOD
  - Number of nodes
  - Total points
  - Average points per node
- Show detailed information for the first few LODs
- No point decompression (fast metadata query)

### 5. Preprocess Chunks (NEW!)

Extract all node chunks from a COPC file to a cache directory for optimized queries.

```bash
dotnet run --project Examples preprocess <copc-file> <cache-dir>
```

Example:
```bash
dotnet run --project Examples preprocess temp/test.copc.laz temp/cache
```

This will:
- Extract all compressed node chunks from the COPC file
- Save each chunk to a separate file in the cache directory
- Display progress and statistics
- Takes ~1 second for typical files

This is a one-time preprocessing step that enables the chunked query approach.

### 6. Chunked Bounding Box Query (Performance Optimized!) (NEW!)

Query a bounding box using cached chunks - only loads and processes relevant data.

```bash
dotnet run --project Examples bbox-chunked <cache-dir> <copc-file> <lod> <minX> <minY> <minZ> <maxX> <maxY> <maxZ>
```

Example:
```bash
dotnet run --project Examples bbox-chunked temp/cache temp/test.copc.laz 3 -3 15 1055 3 20 1060
```

This will:
- Identify which chunks intersect the bounding box at the specified LOD
- Load ONLY those relevant chunks (not the entire file!)
- Show performance comparison:
  - Data reduction percentage
  - Speedup potential
- Decompress and filter points
- Display results and timing breakdown

**Performance Benefits:**
- Only processes 1-5% of total data for selective queries
- Up to 50-100x faster for small bounding boxes
- Ideal for real-time applications and selective data extraction

## Example Outputs

### Random Bounding Box Example

```
Point Cloud Maximum Bounds:
  X: [-9.070, 7.230] (range: 16.300)
  Y: [7.750, 25.670] (range: 17.920)
  Z: [1048.890, 1073.090] (range: 24.200)
  Total points: 24,107,891

Resolution at LOD 3: 0.020578

Random Bounding Box (within maximum bounds):
  Min: (0.498, 19.766, 1060.395)
  Max: (2.128, 21.558, 1062.815)
  ✓ Bounding box is completely within cloud bounds

Total nodes at LOD 3: 171
Nodes intersecting bounding box: 4
Total points in matching nodes: 161,496
Points within bounding box: 75,234
```

### Bounding Box Info Example

```
=== Information by LOD ===

LOD   Resolution   Nodes    Points          Avg Points/Node   
----------------------------------------------------------------------
0     0.164626     1        94,346          94346             
1     0.082313     8        672,162         84020             
2     0.041156     12       534,164         44514             
3     0.020578     48       1,137,128       23690             
4     0.010289     231      5,271,622       22821             
5     0.005145     56       884,203         15789             
----------------------------------------------------------------------
Total: 6 LODs, 356 nodes, 8,593,625 points
```

### Chunked Query Example (Performance Comparison)

```
✅ Identified 12 relevant chunks in 0ms
Total chunk data to process: 12,726,218 bytes (12.14 MB)

📊 Performance Comparison:
  Sequential approach: Would read entire file (24,107,891 points)
  Chunked approach: Only needs to read 410,715 points from 12 chunks
  Data reduction: 1.7% of total points
  Speedup potential: ~58.7x faster

=== Performance Summary ===
Query planning: 2ms
Chunk decompression: 914ms
Point filtering: 8ms
Total time: 939ms
Points per second: 14,636
```

**Key Insight:** The chunked approach only needs to process 1.7% of the data, resulting in dramatic performance improvements for selective queries!

## Understanding the Examples

### When to Use Each Command

**random** - Good for:
- Testing and exploring COPC files
- Understanding data distribution
- Quick visualization samples

**bbox-lod** - Good for:
- Extracting data at a specific detail level
- Controlled resolution queries
- When you know exactly what LOD you need
- Simple queries when preprocessing isn't needed

**bbox-res** - Good for:
- Resolution-based queries (e.g., "give me 1cm resolution")
- When you think in terms of resolution rather than LOD
- Automatic LOD selection based on desired detail

**bbox-info** - Good for:
- Understanding data structure and distribution
- Planning queries without decompressing points
- Fast metadata analysis
- Seeing how data is organized across LOD levels

**preprocess** - Good for:
- One-time setup for repeated queries
- When you'll make multiple queries to the same file
- Production environments with frequent spatial queries
- Takes ~1 second, saves minutes on subsequent queries

**bbox-chunked** - Good for:
- After preprocessing has been done
- High-performance spatial queries
- Selective data extraction (small regions from large files)
- Real-time applications
- Processing only 1-5% of data instead of entire file
- Up to 50-100x faster than sequential approach

### COPC Structure

COPC files organize point cloud data in an octree hierarchy:
- **Root Node**: Top level of the hierarchy (depth 0)
- **Pages**: Interior nodes containing references to child nodes
- **Nodes**: Leaf nodes with compressed point data
- **Voxel Keys**: (D, X, Y, Z) coordinates identifying each node

### Level of Detail (LOD)

The depth parameter controls resolution:
- **Lower depth** = coarser detail, larger voxels, fewer nodes
- **Higher depth** = finer detail, smaller voxels, more nodes
- Each level divides space into 8 child voxels (octree)
- Resolution at depth D: `spacing / 2^D`

For example:
- **LOD 0**: Root node, entire point cloud
- **LOD 3**: Moderate detail, good for visualization
- **LOD 5**: Fine detail, good for analysis
- **LOD 8+**: Very fine detail, many small nodes

### Decompression with LasZipNetStandard

The examples use LasZipNetStandard for LAZ decompression:
- **Native performance**: Uses native laszip library
- **Sequential reading**: Reads points from file in order
- **Format support**: Handles all LAS point formats

Note: The native library reads points sequentially from the file, not individual node chunks from memory. The examples decompress points and then filter them to the bounding box.

## Test Data

To test these examples, you need a COPC file. You can:

1. Use the provided test file: `temp/test.copc.laz`
2. Download sample COPC files from [COPC.io](https://copc.io/)
3. Convert existing LAS/LAZ files to COPC using [PDAL](https://pdal.io/):

```bash
pdal translate input.las output.copc.laz --writers.copc.forward=all
```

## Understanding the Code

The examples demonstrate the key COPC.Net API:

### Opening and Reading Metadata

```csharp
// Open a COPC file
using var reader = IO.CopcReader.Open(copcFilePath);

// Access header and metadata
var header = reader.Config.LasHeader;
var info = reader.Config.CopcInfo;

// Get cloud bounds
double minX = header.MinX, maxX = header.MaxX;
double minY = header.MinY, maxY = header.MaxY;
double minZ = header.MinZ, maxZ = header.MaxZ;
```

### LOD and Resolution Queries

```csharp
// Convert between LOD and resolution
var resolution = VoxelKey.GetResolutionAtDepth(lod, header, info);
int lod = reader.GetDepthAtResolution(resolution);

// Get all nodes at a specific LOD
var nodesAtLod = reader.GetNodesAtResolution(resolution);

// Get all nodes in the file
var allNodes = reader.GetAllNodes();
```

### Spatial Queries

```csharp
// Create a bounding box
var bbox = new Box(minX, minY, minZ, maxX, maxY, maxZ);

// Get node bounds
var nodeBounds = node.Key.GetBounds(header, info);

// Check intersection
bool intersects = nodeBounds.Intersects(bbox);

// Filter nodes by bounding box
var matchingNodes = nodesAtLod.Where(n =>
{
    var bounds = n.Key.GetBounds(header, info);
    return bounds.Intersects(bbox);
}).ToList();
```

### Point Decompression

```csharp
// Decompress points from file using LasZipNetStandard
var points = reader.GetAllPoints(maxPoints);

// Filter points to bounding box
var pointsInBox = points.Where(p => 
    p.X >= bbox.MinX && p.X <= bbox.MaxX &&
    p.Y >= bbox.MinY && p.Y <= bbox.MaxY &&
    p.Z >= bbox.MinZ && p.Z <= bbox.MaxZ).ToArray();

// Access point attributes
foreach (var point in pointsInBox)
{
    double x = point.X, y = point.Y, z = point.Z;
    ushort intensity = point.Intensity;
    byte classification = point.Classification;
    ushort r = point.Red, g = point.Green, b = point.Blue;
}
```

### Key Classes

- **CopcReader**: Main class for reading COPC files
  - `Open(path)` - Open a COPC file
  - `GetAllNodes()` - Get all nodes in hierarchy
  - `GetNodesAtResolution(resolution)` - Get nodes at specific resolution
  - `GetDepthAtResolution(resolution)` - Convert resolution to LOD
  - `GetAllPoints(maxPoints)` - Decompress points from file
- **CopcPoint**: Represents a decompressed point
  - X, Y, Z coordinates
  - Intensity, Classification, RGB, etc.
- **VoxelKey**: Identifies a node in the octree
  - D (depth/LOD), X, Y, Z (voxel coordinates)
  - `GetBounds(header, info)` - Get spatial bounds of node
  - `GetResolutionAtDepth(depth, header, info)` - Get resolution at LOD
- **Box**: 3D bounding box
  - MinX, MinY, MinZ, MaxX, MaxY, MaxZ
  - `Intersects(box)` - Check if boxes intersect
- **LazDecompressor**: Handles LAZ decompression using LasZipNetStandard

## Additional Resources

- [COPC Specification](https://copc.io/)
- [LAS Specification](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities)
- [PDAL Documentation](https://pdal.io/)
- [LasZip.Net](https://github.com/shintadono/LasZip.Net)
