# COPC.Net

A .NET 8.0 library for reading and writing Cloud Optimized Point Cloud (COPC) files.

## What is COPC?

COPC (Cloud Optimized Point Cloud) is an LAZ 1.4 file format organized as a clustered octree for efficient streaming and random access of point cloud data. It enables applications to read only the portions of a file they need, making it ideal for cloud storage and web-based visualization.

## Features

- Read and write COPC files (`.copc.laz`)
- Efficient octree-based spatial indexing
- Stream point data from local files or remote sources
- Query points by spatial extent (bounding box)
- Access point cloud hierarchy and metadata

## Requirements

**Important:** This library requires [LasZipNetStandard](https://github.com/Kuoste/LasZipNetStandard#) to correctly decompress COPC.LAZ files. The native LASZip libraries (`laszip64.dll` / `laszip64.so`) must be available in your output directory.

## Installation

Add a reference to `COPC.Net.csproj` in your project, and ensure LasZipNetStandard and its native dependencies are included.

## Basic Usage

```csharp
using COPC.Net.IO;
using COPC.Net.Geometry;

// Read COPC file
using var reader = new CopcReader("example.copc.laz");

// Get file info
var info = reader.CopcInfo;
var bounds = reader.CopcExtents;

// Query points within a bounding box
var queryBox = new Box(
    new Vector3(minX, minY, minZ),
    new Vector3(maxX, maxY, maxZ)
);

var points = reader.GetPointsInBox(queryBox);
```

See the `Examples` project for more detailed usage examples.

## License

Apache-2.0
