# COPC.Net Sample Usage Guide

Quick reference for using COPC.Net in your application.

## 🚀 Quick Start

### 1. Open a COPC File

```csharp
using Copc;
using Copc.IO;

var reader = CopcReader.Open("myfile.copc.laz");
var config = reader.Config;

Console.WriteLine($"Points: {config.LasHeader.ExtendedNumberOfPointRecords}");
Console.WriteLine($"Bounds: {config.CopcInfo.CenterX}, {config.CopcInfo.CenterY}");
```

### 2. Query by Bounding Box

```csharp
using Copc.Geometry;

// Define your area of interest
var bbox = new Box(
    minX: -5.0, minY: 8.0, minZ: 1050.0,
    maxX: 5.0, maxY: 18.0, maxZ: 1070.0
);

// Get all nodes that intersect this box
var nodes = reader.GetNodesIntersectBox(bbox);

Console.WriteLine($"Found {nodes.Count} nodes in bounding box");
```

### 3. Get Compressed Point Data

```csharp
foreach (var node in nodes)
{
    Console.WriteLine($"Node: {node.Key}");
    Console.WriteLine($"  Points: {node.PointCount}");
    Console.WriteLine($"  Offset: {node.Offset}");
    Console.WriteLine($"  Size: {node.ByteSize} bytes");
    
    // Get the raw compressed data
    byte[] compressedData = reader.GetPointDataCompressed(node);
    
    // Now you can:
    // - Decompress with LasZipDll (see examples)
    // - Save to file for processing with PDAL
    // - Cache for later use
}
```

### 4. Traverse the Hierarchy

```csharp
// Get all nodes at specific depth
var depth2Nodes = reader.GetAllNodes()
    .Where(n => n.Key.Depth == 2)
    .ToList();

// Get child nodes
var root = reader.GetAllNodes().First(n => n.Key.Depth == 0);
var children = reader.GetAllNodes()
    .Where(n => n.Key.Depth == 1)
    .ToList();
```

## 📋 Common Patterns

### Pattern 1: Camera Frustum Query (for Rendering)

```csharp
public class PointCloudRenderer
{
    private CopcReader copcReader;
    
    public void RenderFrame(Camera camera)
    {
        // Get viewport bounds from camera
        var frustum = camera.GetFrustumBounds();
        
        // Query visible nodes
        var visibleNodes = copcReader.GetNodesIntersectBox(frustum);
        
        // Apply level-of-detail based on distance
        var nodesToRender = visibleNodes
            .Where(node => ShouldRender(node, camera))
            .ToList();
        
        // Load and render points
        foreach (var node in nodesToRender)
        {
            var points = LoadPoints(node);
            RenderPoints(points);
        }
    }
    
    private bool ShouldRender(Node node, Camera camera)
    {
        double distance = DistanceToCamera(node, camera);
        
        // Closer = more detail
        if (distance < 50) return true;
        if (distance < 200) return node.Key.Depth <= 3;
        return node.Key.Depth <= 1;
    }
}
```

### Pattern 2: Progressive Loading

```csharp
public class ProgressiveLoader
{
    private CopcReader reader;
    private HashSet<string> loadedNodes;
    
    public void LoadArea(Box area)
    {
        var allNodes = reader.GetNodesIntersectBox(area)
            .OrderBy(n => n.Key.Depth) // Coarse to fine
            .ToList();
        
        foreach (var node in allNodes)
        {
            if (loadedNodes.Contains(node.Key.ToString()))
                continue;
            
            // Load this level
            var points = LoadPoints(node);
            DisplayPoints(points);
            
            loadedNodes.Add(node.Key.ToString());
            
            // Update UI
            NotifyProgress(loadedNodes.Count, allNodes.Count);
        }
    }
}
```

### Pattern 3: Spatial Analysis

```csharp
public class SpatialAnalyzer
{
    public double GetPointDensity(CopcReader reader, Box area)
    {
        var nodes = reader.GetNodesIntersectBox(area);
        long totalPoints = nodes.Sum(n => n.PointCount);
        
        double volume = area.Volume();
        return totalPoints / volume; // points per cubic unit
    }
    
    public List<Box> FindHighDensityRegions(CopcReader reader, double threshold)
    {
        var regions = new List<Box>();
        var allNodes = reader.GetAllNodes();
        
        foreach (var node in allNodes.Where(n => n.Key.Depth == 3))
        {
            var bounds = GetNodeBounds(node);
            double density = node.PointCount / bounds.Volume();
            
            if (density > threshold)
                regions.Add(bounds);
        }
        
        return regions;
    }
}
```

### Pattern 4: Caching Strategy

```csharp
public class CachedCopcReader
{
    private CopcReader reader;
    private Dictionary<string, byte[]> compressionCache;
    private Dictionary<string, List<Vector3>> pointCache;
    
    public List<Vector3> GetPoints(Node node)
    {
        string key = node.Key.ToString();
        
        // Check point cache
        if (pointCache.TryGetValue(key, out var cached))
            return cached;
        
        // Check compression cache
        byte[] compressed;
        if (!compressionCache.TryGetValue(key, out compressed))
        {
            compressed = reader.GetPointDataCompressed(node);
            compressionCache[key] = compressed;
        }
        
        // Decompress
        var points = Decompress(compressed, node);
        
        // Cache decompressed points
        pointCache[key] = points;
        EvictOldCacheEntries();
        
        return points;
    }
}
```

## 🎯 Real-World Examples

### Example 1: 3D Viewer

```csharp
class PointCloudViewer
{
    private CopcReader reader;
    private Box currentViewport;
    
    void OnCameraMove(Camera camera)
    {
        currentViewport = camera.GetFrustumBounds();
        RefreshVisiblePoints();
    }
    
    void RefreshVisiblePoints()
    {
        // Fast query (2-7ms)
        var visibleNodes = reader.GetNodesIntersectBox(currentViewport);
        
        // Load asynchronously
        Task.Run(() => LoadNodesAsync(visibleNodes));
    }
}
```

### Example 2: GIS Analysis

```csharp
class TerrainAnalyzer
{
    public double GetAverageElevation(CopcReader reader, Box region)
    {
        var nodes = reader.GetNodesIntersectBox(region);
        
        double totalZ = 0;
        long count = 0;
        
        foreach (var node in nodes)
        {
            var points = LoadPoints(node);
            foreach (var p in points)
            {
                if (region.Contains(p))
                {
                    totalZ += p.Z;
                    count++;
                }
            }
        }
        
        return totalZ / count;
    }
}
```

### Example 3: Game Engine Integration

```csharp
class UnityCopcLoader : MonoBehaviour
{
    private CopcReader copcReader;
    private GameObject pointCloudRoot;
    
    void Start()
    {
        copcReader = CopcReader.Open(copcFilePath);
        StartCoroutine(StreamPoints());
    }
    
    IEnumerator StreamPoints()
    {
        while (true)
        {
            var viewport = GetCameraBounds();
            var nodes = copcReader.GetNodesIntersectBox(viewport);
            
            foreach (var node in nodes.Take(10)) // Limit per frame
            {
                var points = LoadPoints(node);
                CreateMesh(points, node.Key.ToString());
                yield return null; // Next frame
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
}
```

## 🔧 Decompression Options

### Option A: Full COPC File (Simple)

```csharp
// Read sequentially through entire file
var dll = new LasZipDll();
dll.OpenReader(copcFilePath, out bool compressed);

for (int i = 0; i < totalPoints; i++)
{
    dll.ReadPoint();
    // Process point
}

dll.CloseReader();
```

### Option B: Reconstruct LAZ per Chunk (Complex but Works)

```csharp
private List<Vector3> DecompressNode(Node node)
{
    // See SamplePoints.cs for complete implementation
    
    // 1. Extract compressed chunk
    byte[] chunk = reader.GetPointDataCompressed(node);
    
    // 2. Create temp LAZ file with proper headers
    string tempFile = CreateTempLAZ(chunk, node);
    
    // 3. Decompress with LasZipDll
    var dll = new LasZipDll();
    dll.OpenReader(tempFile, out _);
    
    var points = new List<Vector3>();
    for (int i = 0; i < node.PointCount; i++)
    {
        dll.ReadPoint();
        points.Add(new Vector3
        {
            X = dll.Point.X * header.XScaleFactor + header.XOffset,
            Y = dll.Point.Y * header.YScaleFactor + header.YOffset,
            Z = dll.Point.Z * header.ZScaleFactor + header.ZOffset
        });
    }
    
    dll.CloseReader();
    File.Delete(tempFile);
    
    return points;
}
```

### Option C: PDAL External Process

```csharp
private void DecompressWithPDAL(string inputCopc, string outputLas, Box bounds)
{
    var psi = new ProcessStartInfo
    {
        FileName = "pdal",
        Arguments = $"translate {inputCopc} {outputLas} " +
                   $"--filters.crop.bounds=\"([{bounds.MinX},{bounds.MaxX}]," +
                   $"[{bounds.MinY},{bounds.MaxY}])\"",
        RedirectStandardOutput = true,
        UseShellExecute = false
    };
    
    using var process = Process.Start(psi);
    process.WaitForExit();
    
    // Now read the decompressed LAS file
    var points = ReadLASFile(outputLas);
}
```

## 📊 Performance Tips

1. **Query First, Load Later**
   - COPC queries are FAST (2-7ms)
   - Only load points you actually need

2. **Use LOD**
   - Show less detail for distant points
   - Filter by depth based on distance

3. **Cache Aggressively**
   - Cache compressed data
   - Cache decompressed points
   - Use LRU eviction

4. **Async Everything**
   - Query on main thread
   - Load/decompress on worker threads
   - Never block rendering

5. **Preprocess When Possible**
   - Extract and decompress chunks once
   - Store as fast binary format
   - Stream from disk at runtime

## 🎓 Learn More

- [Production 60fps Guide](../PRODUCTION_GUIDE.md) - Detailed streaming architectures
- [API Reference](../API.md) - Complete API documentation
- [Examples](./Program.cs) - More code samples

## 📞 Need Help?

Run the example programs:

```bash
# Basic info
dotnet run --project Examples info myfile.copc.laz

# Query examples
dotnet run --project Examples query myfile.copc.laz

# Production streaming demo
dotnet run --project Examples production myfile.copc.laz
```
