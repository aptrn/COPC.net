# COPC.Net Project Structure

```
COPC.net/
│
├── COPC.Net/                          # Main library
│   ├── Copc/                          # Core COPC structures
│   │   ├── CopcInfo.cs                # COPC metadata (160 bytes VLR)
│   │   ├── CopcExtents.cs             # Extended dimension extents
│   │   └── CopcConfig.cs              # Configuration container
│   │
│   ├── Geometry/                      # Geometric primitives
│   │   ├── Vector3.cs                 # 3D vector (X, Y, Z)
│   │   └── Box.cs                     # Axis-aligned bounding box
│   │
│   ├── Hierarchy/                     # Octree hierarchy
│   │   ├── VoxelKey.cs                # Voxel identifier (D-X-Y-Z)
│   │   ├── Entry.cs                   # Base hierarchy entry
│   │   ├── Node.cs                    # Point data container
│   │   └── Page.cs                    # Hierarchy metadata container
│   │
│   ├── IO/                            # File I/O
│   │   ├── CopcReader.cs              # COPC file reader ⭐
│   │   └── CopcWriter.cs              # COPC file writer (stub)
│   │
│   ├── Utils/                         # Utilities
│   │   └── BinaryExtensions.cs        # Binary I/O helpers
│   │
│   ├── COPC.Net.csproj                # Project file
│   └── API.md                         # API documentation
│
├── Examples/                          # Example applications
│   ├── Program.cs                     # CLI tool with commands:
│   │                                  #   - info: File information
│   │                                  #   - hierarchy: Show structure
│   │                                  #   - bbox: Spatial query
│   │                                  #   - resolution: Resolution query
│   ├── Examples.csproj                # Project file
│   └── README.md                      # Usage examples
│
├── repos/                             # Reference implementations
│   ├── LasZip.Net/                    # C# LAZ compression library
│   ├── copc.js/                       # TypeScript COPC reference
│   └── copc-lib/                      # C++ COPC reference
│
├── prompts/                           # Initial requirements
│   └── 1-setup.md                     # Your original request
│
├── COPC.Net.sln                       # Visual Studio solution
├── README.md                          # Main documentation
├── IMPLEMENTATION_SUMMARY.md          # Implementation details
└── PROJECT_STRUCTURE.md               # This file
```

## Component Relationships

```
┌─────────────────────────────────────────────────────────┐
│                     User Application                     │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                    Copc.IO.CopcReader                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ Open()       │  │ GetAllNodes()│  │ GetPointData()  │
│  │ LoadHierarchy│  │ SpatialQuery │  │ GetCompressed() │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
└──────┬────────────────────┬───────────────────┬─────────┘
       │                    │                   │
       ▼                    ▼                   ▼
┌─────────────┐    ┌────────────────┐  ┌──────────────┐
│ CopcConfig  │    │   Hierarchy    │  │  LasZip.Net  │
│ ┌─────────┐ │    │ ┌────────────┐ │  │ ┌──────────┐ │
│ │LasHeader│ │    │ │ VoxelKey   │ │  │ │Decompress│ │
│ │CopcInfo │ │    │ │ Node       │ │  │ │ Points   │ │
│ │  Wkt    │ │    │ │ Page       │ │  │ └──────────┘ │
│ └─────────┘ │    │ └────────────┘ │  └──────────────┘
└─────────────┘    └────────────────┘
       │                    │
       ▼                    ▼
┌─────────────┐    ┌────────────────┐
│  Geometry   │    │   Utilities    │
│ ┌─────────┐ │    │ ┌────────────┐ │
│ │ Vector3 │ │    │ │  Binary    │ │
│ │   Box   │ │    │ │ Extensions │ │
│ └─────────┘ │    │ └────────────┘ │
└─────────────┘    └────────────────┘
```

## Data Flow: Reading a COPC File

```
┌────────────────────┐
│  Open COPC File    │
└─────────┬──────────┘
          │
          ▼
┌────────────────────┐
│  Read LAS Header   │
└─────────┬──────────┘
          │
          ▼
┌────────────────────┐
│    Read VLRs       │ ──────> Find "copc" VLR (Record ID 1)
└─────────┬──────────┘         Find "LASF_Projection" VLR (WKT)
          │
          ▼
┌────────────────────┐
│ Parse COPC Info    │ ──────> Center, HalfSize, Spacing
└─────────┬──────────┘         RootHierarchyOffset/Size
          │                    GPS Time Range
          ▼
┌────────────────────┐
│ Create CopcConfig  │
└─────────┬──────────┘
          │
          ▼
┌────────────────────┐
│  Return Reader     │
└────────────────────┘
```

## Hierarchy Loading Flow

```
┌──────────────────────────┐
│ LoadRootHierarchyPage()  │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Seek to Root Offset      │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Read Root Page Data      │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Parse 32-byte Entries    │ ────┬───> PointCount >= 0 → Node
└──────────────────────────┘     └───> PointCount == -1 → Page
             │
             ▼
┌──────────────────────────┐
│   Cache Nodes/Pages      │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   Return Root Page       │
└──────────────────────────┘
```

## Spatial Query Flow

```
┌──────────────────────────┐
│ GetNodesIntersectBox(box)│
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   GetAllNodes()          │ ────> Loads hierarchy recursively
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   For each Node:         │
│   - Calculate bounds     │
│   - Test intersection    │
│   - Check resolution     │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│   Return matching Nodes  │
└──────────────────────────┘
```

## Key Classes Quick Reference

| Class | Purpose | Key Methods |
|-------|---------|-------------|
| `CopcReader` | Main entry point for reading | `Open()`, `GetAllNodes()`, `GetPointData()` |
| `CopcInfo` | COPC metadata | `Parse()`, `ToBytes()` |
| `VoxelKey` | Octree position | `GetChildren()`, `GetParent()`, `GetBounds()` |
| `Node` | Point data reference | Inherits from `Entry`, adds `PageKey` |
| `Page` | Hierarchy reference | Inherits from `Entry`, adds `Children`, `Loaded` |
| `Box` | Bounding box | `Intersects()`, `Contains()`, `Within()` |

## File Format

```
COPC File Structure:
┌────────────────────────────┐
│      LAS 1.4 Header        │ 375 bytes
├────────────────────────────┤
│      COPC Info VLR         │ User ID: "copc", Record ID: 1
│      (160 bytes data)      │ 
├────────────────────────────┤
│    WKT VLR (optional)      │ User ID: "LASF_Projection"
├────────────────────────────┤
│   Other VLRs (optional)    │
├────────────────────────────┤
│   Root Hierarchy Page      │ At offset from CopcInfo
│   (32-byte entries)        │ Size from CopcInfo
├────────────────────────────┤
│   Child Hierarchy Pages    │ Referenced by entries
├────────────────────────────┤
│   LAZ Point Data Chunks    │ One per Node
│   (compressed)             │
└────────────────────────────┘
```

## Entry Structure (32 bytes)

```
Bytes  0-3:   int32   D (depth)
Bytes  4-7:   int32   X coordinate
Bytes  8-11:  int32   Y coordinate
Bytes 12-15:  int32   Z coordinate
Bytes 16-23:  uint64  Offset (file position)
Bytes 24-27:  int32   Byte Size
Bytes 28-31:  int32   Point Count (-1 for Page, ≥0 for Node)
```

## Example Usage Patterns

### Pattern 1: Full Scan
```csharp
var reader = CopcReader.Open("file.copc.laz");
var nodes = reader.GetAllNodes();
foreach (var node in nodes)
    ProcessPoints(reader.GetPointData(node));
```

### Pattern 2: Spatial Query
```csharp
var bbox = new Box(xMin, yMin, zMin, xMax, yMax, zMax);
var nodes = reader.GetNodesIntersectBox(bbox);
foreach (var node in nodes)
    ProcessPoints(reader.GetPointData(node));
```

### Pattern 3: Progressive Resolution
```csharp
for (double res = 10.0; res >= 0.5; res /= 2)
{
    var nodes = reader.GetNodesAtResolution(res);
    DisplayPoints(nodes); // Lower detail to higher detail
}
```

### Pattern 4: Hierarchy Navigation
```csharp
var rootPage = reader.LoadRootHierarchyPage();
foreach (var entry in rootPage.Children.Values)
{
    if (entry is Node node)
        ProcessNode(node);
    else if (entry is Page page)
        ProcessPage(page);
}
```

## Build and Test

```bash
# Build solution
dotnet build COPC.Net.sln

# Run examples
dotnet run --project Examples info file.copc.laz
dotnet run --project Examples hierarchy file.copc.laz
dotnet run --project Examples bbox file.copc.laz 0 0 0 100 100 100
dotnet run --project Examples resolution file.copc.laz 1.0

# Use in your project
# Add project reference in your .csproj:
# <ProjectReference Include="path/to/COPC.Net/COPC.Net.csproj" />
```

## Dependencies

```
Your Application
    ↓
COPC.Net (this library)
    ↓
LasZip.Net (LAZ compression)
    ↓
.NET 8.0 Runtime
```

## Performance Characteristics

- **Hierarchy Loading**: O(n) where n = number of entries in page
- **Spatial Query**: O(n) where n = total nodes (could be optimized with spatial index)
- **Resolution Query**: O(n) with filtering
- **Point Data Loading**: O(p) where p = point count in node
- **Memory**: Caches all loaded pages and nodes

## Thread Safety

⚠️ **Not Thread-Safe**: Create separate `CopcReader` instances per thread

```csharp
// ❌ Don't share reader between threads
var reader = CopcReader.Open("file.copc.laz");
Parallel.ForEach(nodes, node => reader.GetPointData(node));

// ✅ Create reader per thread
Parallel.ForEach(nodes, node => {
    using var reader = CopcReader.Open("file.copc.laz");
    reader.GetPointData(node);
});
```

