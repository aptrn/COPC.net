# COPC.Net

A C# library for reading and writing Cloud Optimized Point Cloud (COPC) files. This library provides a native .NET implementation of the COPC specification, building on top of LasZip.Net for LAZ compression support.

## Overview

COPC (Cloud Optimized Point Cloud) is a file format for storing point cloud data that enables efficient streaming and spatial querying. It's based on the LAS 1.4 specification with a specific organization that allows random access to spatially organized data.

This library was ported from the C++ [copc-lib](https://github.com/RockRobotic/copc-lib) and TypeScript [copc.js](https://github.com/connormanning/copc.js) implementations to provide a pure C# solution for working with COPC files.

## Features

- **Read COPC Files**: Parse and read COPC files with full metadata access
- **Spatial Queries**: Query point data by bounding box
- **Resolution Queries**: Access data at specific resolution levels
- **Hierarchy Navigation**: Navigate the octree hierarchy structure
- **Streaming Support**: Efficiently load only the data you need
- **LasZip Integration**: Full support for LAZ compression via LasZip.Net

## Installation

### Building from Source

```bash
git clone <your-repo-url>
cd COPC.net
dotnet build
```

### NuGet (Coming Soon)

```bash
dotnet add package COPC.Net
```

## Quick Start

### Reading a COPC File

```csharp
using Copc.IO;
using System;

// Open a COPC file
using var reader = CopcReader.Open("path/to/file.copc.laz");

// Access file metadata
var header = reader.Config.LasHeader;
Console.WriteLine($"Point Count: {header.ExtendedNumberOfPointRecords}");
Console.WriteLine($"Bounds: ({header.MinX}, {header.MinY}, {header.MinZ}) -> ({header.MaxX}, {header.MaxY}, {header.MaxZ})");

// Access COPC-specific info
var info = reader.Config.CopcInfo;
Console.WriteLine($"Spacing: {info.Spacing}");
Console.WriteLine($"Center: ({info.CenterX}, {info.CenterY}, {info.CenterZ})");
```

### Spatial Queries

```csharp
using Copc.Geometry;

// Define a bounding box
var bbox = new Box(
    minX: 100, minY: 200, minZ: 0,
    maxX: 150, maxY: 250, maxZ: 50
);

// Get all nodes that intersect the bounding box
var nodes = reader.GetNodesIntersectBox(bbox);

foreach (var node in nodes)
{
    Console.WriteLine($"Node {node.Key}: {node.PointCount} points");
    
    // Read the actual point data
    byte[] pointData = reader.GetPointData(node);
}
```

### Resolution-Based Queries

```csharp
// Get the depth level for a specific resolution
int depth = reader.GetDepthAtResolution(0.5); // 0.5m resolution

// Get all nodes at that resolution
var nodes = reader.GetNodesAtResolution(0.5);

Console.WriteLine($"Found {nodes.Count} nodes at resolution 0.5m");
```

### Hierarchy Navigation

```csharp
// Load the root hierarchy page
var rootPage = reader.LoadRootHierarchyPage();

Console.WriteLine($"Root page has {rootPage.Children.Count} children");

// Get all nodes in the entire hierarchy
var allNodes = reader.GetAllNodes();

// Group by depth
var nodesByDepth = allNodes.GroupBy(n => n.Key.D);
foreach (var group in nodesByDepth)
{
    Console.WriteLine($"Depth {group.Key}: {group.Count()} nodes");
}
```

## Architecture

### Core Components

- **Geometry**: Vector3, Box - Basic geometric primitives
- **Hierarchy**: VoxelKey, Entry, Node, Page - Octree hierarchy structures
- **Copc**: CopcInfo, CopcConfig - COPC metadata and configuration
- **IO**: CopcReader, CopcWriter - File I/O operations

### Data Structures

#### VoxelKey
Represents a position in the COPC octree hierarchy with depth (D) and 3D coordinates (X, Y, Z).

```csharp
var key = new VoxelKey(depth: 3, x: 4, y: 5, z: 2);
var children = key.GetChildren(); // Get all 8 child voxels
var parent = key.GetParent();     // Get parent voxel
```

#### Node
Represents a voxel that contains actual point data.

```csharp
public class Node : Entry
{
    public VoxelKey Key { get; }
    public long Offset { get; }        // File offset to point data
    public int ByteSize { get; }       // Size of compressed data
    public int PointCount { get; }     // Number of points
}
```

#### Page
Represents a voxel that contains references to other nodes or pages (hierarchy metadata).

```csharp
public class Page : Entry
{
    public Dictionary<string, Entry> Children { get; }
    public bool Loaded { get; }
}
```

## Examples

The Examples project demonstrates various use cases:

```bash
# Display file information
dotnet run --project Examples info path/to/file.copc.laz

# Show hierarchy structure
dotnet run --project Examples hierarchy path/to/file.copc.laz

# Query by bounding box
dotnet run --project Examples bbox path/to/file.copc.laz 100 200 0 150 250 50

# Query by resolution
dotnet run --project Examples resolution path/to/file.copc.laz 0.5
```

## COPC Specification

This library implements the [COPC specification](https://copc.io/). Key aspects include:

- **LAS 1.4 Required**: COPC files must be LAS 1.4 format
- **COPC Info VLR**: Special Variable Length Record at offset 375
- **Octree Organization**: Point data organized in a spatial octree
- **LAZ Compression**: Each node's points are LAZ-compressed
- **Hierarchy Structure**: Separate hierarchy data for efficient spatial queries

## Dependencies

- **.NET 8.0**: Target framework
- **LasZip.Net**: LAZ compression/decompression support

## Limitations

- **Writer Implementation**: The writer is currently a basic stub. Full writer implementation (including automatic hierarchy building and spatial indexing) is planned for future releases.
- **Point Format Support**: Currently supports standard LAS point formats. Custom extra bytes support is limited.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## License

MIT License - See LICENSE file for details

## Acknowledgments

This library is based on:
- [copc-lib](https://github.com/RockRobotic/copc-lib) - C++ implementation by Rock Robotic
- [copc.js](https://github.com/connormanning/copc.js) - TypeScript implementation by Connor Manning
- [LasZip.Net](https://github.com/shintadono/LasZip.Net) - C# port of LasZip

## References

- [COPC Specification](https://copc.io/)
- [LAS Specification](https://www.asprs.org/divisions-committees/lidar-division/laser-las-file-format-exchange-activities)
- [LAZ Compression](https://laszip.org/)

## Support

For questions and support, please open an issue on GitHub.

