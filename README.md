# COPC.Net

A .NET 8.0 library for reading and writing Cloud Optimized Point Cloud (COPC) files.

This is vibe-coded off the [C++ implementation](https://github.com/RockRobotic/copc-lib).

## To-Do

  -  [x] 1. octree traversal strategy
  -  [x] 2. smart "caching" system. only unpack what is not already unpacked, checking against a buffer with fixed maximum size.
  -  [ ] 3. double check extra dimension retrival. we should get scalar fields values for each point. also give a list of all available extra dimension when starting.

## What is COPC?

COPC (Cloud Optimized Point Cloud) is an LAZ 1.4 file format organized as a clustered octree for efficient streaming and random access of point cloud data. It enables applications to read only the portions of a file they need, making it ideal for cloud storage and web-based visualization.

## Features

- Read and write COPC files (`.copc.laz`)
- Efficient octree-based spatial indexing
- Stream point data from local files or remote sources
- Multiple spatial query types:
  - **Bounding box queries** - Query points within axis-aligned boxes
  - **Frustum queries** - Query points visible from camera view (see [README_Frustum.md](README_Frustum.md))
  - **Radius queries** - Query points within distance from a point (omnidirectional) (see [README_Radius.md](README_Radius.md))
- Level-of-Detail (LOD) support with resolution filtering
- Access point cloud hierarchy and metadata
- **Smart caching system** - Optional RAM buffer with LRU eviction for improved performance (see [Cache README](COPC.Net/Cache/README.md))

## Requirements

**Important:** This library requires [LasZipNetStandard](https://github.com/Kuoste/LasZipNetStandard#) to correctly decompress COPC.LAZ files. The native LASZip libraries (`laszip64.dll` / `laszip64.so`) must be available in your output directory.

## Installation

Add a reference to `COPC.Net.csproj` in your project, and ensure LasZipNetStandard and its native dependencies are included.

## Basic Usage

```csharp
using Copc.IO;
using Copc.Geometry;

// Read COPC file
using var reader = CopcReader.Open("example.copc.laz");

// Get file info
var header = reader.Config.LasHeader;
var info = reader.Config.CopcInfo;

// Query points within a bounding box
var box = new Box(minX, minY, minZ, maxX, maxY, maxZ);
var nodesInBox = reader.GetNodesIntersectBox(box);

// Query points within radius from a position (omnidirectional)
var center = new Vector3(500, 500, 50);
var nodesInRadius = reader.GetNodesWithinRadius(center, radius: 100);

// Query points visible from camera frustum
var frustum = Frustum.FromViewProjectionMatrix(viewProjectionMatrix);
var visibleNodes = reader.GetNodesIntersectFrustum(frustum);

// Process nodes
foreach (var node in nodesInRadius)
{
    byte[] compressedData = reader.GetPointDataCompressed(node);
    // Decompress and process points...
}
```

### Using the Smart Cache

The smart cache system provides efficient memory-managed caching of decompressed point data:

```csharp
using Copc.Cache;

// Open file with automatic caching (512 MB RAM buffer)
using var cachedReader = CachedCopcReader.Open("example.copc.laz", cacheSizeMB: 512);

// Query points - first time loads from disk, subsequent queries use cache
var points = cachedReader.GetPointsInBox(boundingBox);

// Check cache performance
var stats = cachedReader.Cache.GetStatistics();
Console.WriteLine($"Hit rate: {stats.HitRate:F1}%");
```

The cache uses LRU (Least Recently Used) eviction and automatically manages memory. Perfect for:
- Interactive panning/zooming
- Real-time camera movement
- Progressive LOD loading
- Repeated spatial queries

See the [Cache README](COPC.Net/Cache/README.md) for detailed documentation and the `Examples` project for more examples.
See the `Examples` project for more detailed usage examples.
