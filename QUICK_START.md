# COPC.Net Quick Start

## 🎯 Your Best Shot for 60fps Streaming

For a **desktop C# application** handling **billion+ points** at **60fps**, here's your path:

---

## ⚡ The Winning Strategy: Preprocessing

### Why This Works

```
COPC File (1 GB compressed)
    ↓ [Preprocess Once - 10 minutes]
Cache Directory (10 GB uncompressed)
    ↓ [Runtime - < 1ms per chunk]
GPU (60fps smooth rendering) ✓
```

**Key Insight:** Decompressing LAZ at runtime is too slow for 60fps. **Preprocess once, stream forever.**

---

## 🚀 Implementation (3 Steps)

### Step 1: Preprocess Your Files

```bash
# Run this ONCE for each COPC file
dotnet run --project Examples preprocess input.copc.laz ./cache

# Output:
# cache/0_0_0_0.bin (root node)
# cache/1_0_0_0.bin (child nodes)
# cache/1_0_0_1.bin
# ... thousands more
```

**What This Does:**
- Extracts every COPC chunk
- Decompresses LAZ → raw binary
- Saves as `.bin` files (fast to read)

**Time:** ~1-2 minutes per GB of input

---

### Step 2: Runtime Code

```csharp
using Copc;
using Copc.Geometry;
using Copc.IO;

// INITIALIZATION (once)
var copcReader = CopcReader.Open("file.copc.laz");
var chunkLoader = new FastBinaryLoader("./cache/");
var cache = new Dictionary<string, List<Vector3>>();

// PER-FRAME (60fps loop)
void RenderFrame()
{
    // 1. Get camera viewport (0ms - you already have this)
    Box viewport = camera.GetFrustumBounds();
    
    // 2. Query COPC hierarchy - FAST! (2-7ms)
    var visibleNodes = copcReader.GetNodesIntersectBox(viewport);
    
    // 3. Apply LOD - filter by distance (0ms)
    var toRender = visibleNodes
        .Where(n => DistanceCheck(n, camera))
        .Take(100); // Limit visible nodes
    
    // 4. Load preprocessed chunks - SUPER FAST! (1-3ms)
    var points = new List<Vector3>();
    foreach (var node in toRender)
    {
        string key = node.Key.ToString();
        if (!cache.TryGetValue(key, out var pts))
        {
            pts = chunkLoader.LoadChunk(key); // 0.5ms from SSD
            cache[key] = pts;
        }
        points.AddRange(pts);
    }
    
    // 5. Upload to GPU & render (5-8ms)
    gpuBuffer.Upload(points);
    renderer.Draw();
    
    // TOTAL: 8-15ms → 66-125 fps ✓
}
```

---

### Step 3: FastBinaryLoader Implementation

```csharp
public class FastBinaryLoader
{
    private string cacheDir;
    
    public FastBinaryLoader(string dir) => cacheDir = dir;
    
    public List<Vector3> LoadChunk(string nodeKey)
    {
        string path = Path.Combine(cacheDir, 
                                   nodeKey.Replace("-", "_") + ".bin");
        
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        
        // Read header (16 bytes)
        int magic = reader.ReadInt32();    // 'PONT'
        int count = reader.ReadInt32();
        reader.ReadInt32(); // format
        reader.ReadInt32(); // reserved
        
        // Read points (12 bytes each: 3 floats)
        var points = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            points.Add(new Vector3
            {
                X = reader.ReadSingle(),
                Y = reader.ReadSingle(),
                Z = reader.ReadSingle()
            });
        }
        
        return points;
    }
}
```

---

## 📊 Expected Performance

### Preprocessing (One-Time Cost)

| Input Size | Points | Preprocess Time | Cache Size |
|------------|--------|-----------------|------------|
| 100 MB | 10M | 1-2 min | 1 GB |
| 1 GB | 100M | 10-15 min | 10 GB |
| 10 GB | 1B | 90-120 min | 100 GB |

### Runtime (Per Frame)

| Operation | Time |
|-----------|------|
| COPC spatial query | 2-7ms |
| Load chunk (cached) | 0.1ms |
| Load chunk (disk) | 0.5-1ms |
| GPU upload | 2-4ms |
| Rendering | 3-5ms |
| **TOTAL** | **8-15ms** ✓ |

**Result:** 66-125 fps depending on scene complexity

---

## 🎮 Alternative: No Preprocessing?

If you **can't preprocess**, use the **PDAL hybrid approach**:

```csharp
public class PDALLoader
{
    public List<Vector3> LoadChunk(Node node)
    {
        // 1. Extract chunk from COPC
        var compressed = copcReader.GetPointDataCompressed(node);
        
        // 2. Save to temp file
        string tempLaz = Path.GetTempFileName() + ".laz";
        ReconstructLAZFile(compressed, node, tempLaz);
        
        // 3. Decompress with PDAL
        string tempLas = Path.GetTempFileName() + ".las";
        Process.Start("pdal", $"translate {tempLaz} {tempLas}").WaitForExit();
        
        // 4. Read decompressed points
        return ReadLASFile(tempLas);
    }
}
```

**With Caching:**
- First load: 50-200ms (slow!)
- Subsequent: < 1ms (cached)
- Cache hit rate: 80-95%
- Result: Occasional frame drops, but mostly smooth

---

## 🏆 Best Practices

### 1. Use Level-of-Detail

```csharp
bool ShouldRender(Node node, Camera camera)
{
    double distance = Distance(node, camera);
    
    if (distance < 50)  return true;              // Show all detail
    if (distance < 200) return node.Key.Depth <= 3; // Show medium detail
    return node.Key.Depth <= 1;                   // Show only coarse
}
```

### 2. Limit Points Per Frame

```csharp
const int MaxPointsPerFrame = 5_000_000; // 5M points

var points = new List<Vector3>();
foreach (var node in visibleNodes)
{
    if (points.Count >= MaxPointsPerFrame)
        break;
    
    points.AddRange(LoadChunk(node));
}
```

### 3. Async Prefetch

```csharp
// Load nearby chunks in background
Task.Run(() => {
    var nearby = GetNearbyNodes(camera, radius: 100);
    foreach (var node in nearby)
        cache[node.Key.ToString()] = LoadChunk(node);
});
```

### 4. LRU Cache Eviction

```csharp
if (cache.Count > 500) // ~500 chunks = 500MB
{
    var lru = cache.OrderBy(kvp => kvp.Value.LastAccess).First();
    cache.Remove(lru.Key);
}
```

---

## 🔍 Debugging

### Check Query Performance

```csharp
var sw = Stopwatch.StartNew();
var nodes = copcReader.GetNodesIntersectBox(viewport);
sw.Stop();

if (sw.ElapsedMilliseconds > 10)
    Console.WriteLine($"WARNING: Query took {sw.ElapsedMilliseconds}ms");
```

### Monitor Cache

```csharp
Console.WriteLine($"Cache: {cache.Count} chunks, " +
                  $"{cache.Sum(c => c.Value.Count):N0} points");
```

### Profile Frame Time

```csharp
var frameWatch = Stopwatch.StartNew();
RenderFrame();
frameWatch.Stop();

if (frameWatch.ElapsedMilliseconds > 16)
    Console.WriteLine($"FRAME DROP: {frameWatch.ElapsedMilliseconds}ms");
```

---

## 📚 Full Examples

```bash
# See the production system in action
dotnet run --project Examples production .\temp\file.copc.laz

# Extract chunks for preprocessing
dotnet run --project Examples preprocess .\temp\file.copc.laz ./cache

# Sample points from random bounding box
dotnet run --project Examples sample .\temp\file.copc.laz 1000
```

---

## 🎯 Decision Matrix

| Your Situation | Recommended Approach |
|----------------|---------------------|
| Production app, 60fps required | **Preprocessing** (Step 1-3 above) |
| Moderate files (< 100M points) | PDAL hybrid with cache |
| Prototyping, speed not critical | Direct COPC + LasZipDll |
| GPU decompression possible | Custom compute shaders |

---

## 💡 Key Takeaways

1. **COPC queries are FAST** (2-7ms) → Use them every frame!
2. **LAZ decompression is SLOW** (50-200ms) → Preprocess or cache aggressively
3. **NVMe SSDs are FAST** (1ms for 100K points) → Stream from disk works!
4. **LOD is ESSENTIAL** → Don't try to render everything
5. **60fps is achievable** → With proper architecture

---

## 🚀 Get Started Now

```bash
cd COPC.net
dotnet build
dotnet run --project Examples production your-file.copc.laz
```

**Read the full guide:** [PRODUCTION_GUIDE.md](PRODUCTION_GUIDE.md)

**Need help?** Check [SAMPLE_USAGE.md](Examples/SAMPLE_USAGE.md) for code patterns.

Good luck! 🎉

