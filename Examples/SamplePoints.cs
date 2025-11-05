using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Copc.Geometry;
using Copc.Hierarchy;
using LasZip;

namespace Copc.Examples
{
    /// <summary>
    /// Helper class for sampling and displaying points from COPC files.
    /// </summary>
    public static class SamplePoints
    {
        /// <summary>
        /// Samples a random small bounding box from the COPC file and displays point coordinates.
        /// </summary>
        public static void SampleRandomBox(string filePath, int maxPoints = 100)
        {
            Console.WriteLine($"Sampling random bounding box from: {filePath}\n");

            using var reader = IO.CopcReader.Open(filePath);

            var header = reader.Config.LasHeader;
            var info = reader.Config.CopcInfo;

            // Calculate a small random bounding box (about 1% of the file extent)
            double rangeX = header.MaxX - header.MinX;
            double rangeY = header.MaxY - header.MinY;
            double rangeZ = header.MaxZ - header.MinZ;

            // Make it small - about 1-5% of the total extent
            double sizeX = rangeX * 0.03;
            double sizeY = rangeY * 0.03;
            double sizeZ = rangeZ * 0.03;

            // Random position within the file bounds
            var random = new Random();
            double minX = header.MinX + random.NextDouble() * (rangeX - sizeX);
            double minY = header.MinY + random.NextDouble() * (rangeY - sizeY);
            double minZ = header.MinZ + random.NextDouble() * (rangeZ - sizeZ);

            var bbox = new Box(minX, minY, minZ, minX + sizeX, minY + sizeY, minZ + sizeZ);

            Console.WriteLine($"Random Bounding Box:");
            Console.WriteLine($"  Min: ({bbox.MinX:F2}, {bbox.MinY:F2}, {bbox.MinZ:F2})");
            Console.WriteLine($"  Max: ({bbox.MaxX:F2}, {bbox.MaxY:F2}, {bbox.MaxZ:F2})");
            Console.WriteLine($"  Size: {sizeX:F2} x {sizeY:F2} x {sizeZ:F2}\n");

            // Find nodes in this bounding box
            var nodes = reader.GetNodesIntersectBox(bbox);
            Console.WriteLine($"Found {nodes.Count} node(s) intersecting the bounding box\n");

            if (nodes.Count == 0)
            {
                Console.WriteLine("No nodes found in the sampled area. Try running again.");
                return;
            }

            // Sample points from the first node
            var node = nodes[0];
            Console.WriteLine($"Sampling from node {node.Key} ({node.PointCount} points)");

            try
            {
                var points = ReadPointsFromNode(filePath, node, header, reader.Config.LasHeader.Vlrs, maxPoints);
                
                Console.WriteLine($"\nShowing first {Math.Min(maxPoints, points.Count)} point(s):\n");
                Console.WriteLine("    X              Y              Z          ");
                Console.WriteLine("------------------------------------------------");

                int count = 0;
                foreach (var point in points.Take(maxPoints))
                {
                    Console.WriteLine($"{count,3}: {point.X,13:F3}  {point.Y,13:F3}  {point.Z,13:F3}");
                    count++;
                }

                Console.WriteLine($"\nTotal points read: {points.Count}");

                // Show some statistics
                if (points.Count > 0)
                {
                    Console.WriteLine($"\nStatistics:");
                    Console.WriteLine($"  X range: {points.Min(p => p.X):F3} to {points.Max(p => p.X):F3}");
                    Console.WriteLine($"  Y range: {points.Min(p => p.Y):F3} to {points.Max(p => p.Y):F3}");
                    Console.WriteLine($"  Z range: {points.Min(p => p.Z):F3} to {points.Max(p => p.Z):F3}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading points: {ex.Message}");
                Console.WriteLine("\nNote: Decompressing COPC chunks is complex. Each node is independently compressed.");
                Console.WriteLine("Alternative: Use the compressed data for analysis, or use PDAL for full decompression:");
                Console.WriteLine("  pdal translate input.copc.laz output.las");
                Console.WriteLine("\nYou can still query the hierarchy and get node information:");
                Console.WriteLine($"  - This node has {node.PointCount} points");
                Console.WriteLine($"  - Compressed size: {node.ByteSize} bytes");
                Console.WriteLine($"  - Compression ratio: {(double)node.ByteSize / (node.PointCount * header.PointDataRecordLength):P1}");
            }
        }

        /// <summary>
        /// Reads points from a node using LasZipDll.
        /// This creates a temporary LAZ file with the node's chunk and decompresses it.
        /// </summary>
        private static List<SimplePoint> ReadPointsFromNode(string copcFilePath, Node node, LasHeader header, 
                                                             System.Collections.Generic.List<LasVariableLengthRecord> vlrs, int maxPoints)
        {
            var points = new List<SimplePoint>();

            // For COPC files, each node's points are stored as a separate compressed chunk
            // We need to extract that chunk and create a minimal LAZ file to decompress it
            string tempLazFile = Path.Combine(Path.GetTempPath(), $"copc_temp_{Guid.NewGuid()}.laz");

            try
            {
                // Read the compressed chunk from the COPC file
                byte[] compressedChunk;
                using (var fileStream = File.OpenRead(copcFilePath))
                {
                    fileStream.Seek(node.Offset, SeekOrigin.Begin);
                    compressedChunk = new byte[node.ByteSize];
                    fileStream.Read(compressedChunk, 0, node.ByteSize);
                }

                // Create a minimal LAZ file with this chunk
                // This is a simplified approach - a complete implementation would need to
                // properly reconstruct the LAZ file format
                using (var tempFile = File.Create(tempLazFile))
                using (var writer = new BinaryWriter(tempFile))
                {
                    // Write LAS signature
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("LASF"));
                    
                    // Write minimal header (this is simplified)
                    writer.Write(header.FileSourceID);
                    writer.Write(header.GlobalEncoding);
                    writer.Write(header.ProjectIDGuidData1);
                    writer.Write(header.ProjectIDGuidData2);
                    writer.Write(header.ProjectIDGuidData3);
                    
                    // Ensure arrays are exactly the right size
                    byte[] guid4 = new byte[8];
                    Array.Copy(header.ProjectIDGuidData4, 0, guid4, 0, Math.Min(8, header.ProjectIDGuidData4.Length));
                    writer.Write(guid4);
                    
                    writer.Write(header.VersionMajor);
                    writer.Write(header.VersionMinor);
                    
                    // Ensure arrays are exactly 32 bytes
                    byte[] sysId = new byte[32];
                    Array.Copy(header.SystemIdentifier, 0, sysId, 0, Math.Min(32, header.SystemIdentifier.Length));
                    writer.Write(sysId);
                    
                    byte[] genSoft = new byte[32];
                    Array.Copy(header.GeneratingSoftware, 0, genSoft, 0, Math.Min(32, header.GeneratingSoftware.Length));
                    writer.Write(genSoft);
                    
                    writer.Write(header.FileCreationDay);
                    writer.Write(header.FileCreationYear);
                    
                    // Find and include the laszip VLR (Record ID 22204) which is required for decompression
                    var laszipVlr = vlrs.FirstOrDefault(v => 
                        System.Text.Encoding.ASCII.GetString(v.UserID).TrimEnd('\0') == "laszip encoded" && 
                        v.RecordID == 22204);
                    
                    uint numVlrs = laszipVlr != null ? 1u : 0u;
                    
                    // Calculate proper header size for LAS 1.4
                    ushort headerSize = 375;
                    writer.Write(headerSize);
                    
                    // Calculate offset to point data (header + VLRs)
                    uint vlrSize = numVlrs > 0 && laszipVlr != null ? (uint)(54 + laszipVlr.RecordLengthAfterHeader) : 0; // VLR header is 54 bytes
                    uint offsetToPoints = (uint)headerSize + vlrSize;
                    writer.Write(offsetToPoints);
                    
                    writer.Write(numVlrs); // Number of VLRs
                    writer.Write(header.PointDataFormat);
                    writer.Write(header.PointDataRecordLength);
                    writer.Write((uint)node.PointCount); // Legacy point count
                    
                    // Legacy points by return
                    for (int i = 0; i < 5; i++)
                        writer.Write((uint)0);
                    
                    writer.Write(header.XScaleFactor);
                    writer.Write(header.YScaleFactor);
                    writer.Write(header.ZScaleFactor);
                    writer.Write(header.XOffset);
                    writer.Write(header.YOffset);
                    writer.Write(header.ZOffset);
                    writer.Write(header.MaxX);
                    writer.Write(header.MinX);
                    writer.Write(header.MaxY);
                    writer.Write(header.MinY);
                    writer.Write(header.MaxZ);
                    writer.Write(header.MinZ);
                    
                    // LAS 1.3+
                    writer.Write((ulong)0); // Start of waveform data
                    
                    // LAS 1.4+
                    writer.Write((ulong)0); // Start of first EVLR
                    writer.Write((uint)0);  // Number of EVLRs
                    writer.Write((ulong)node.PointCount); // Extended point count
                    
                    // Extended points by return
                    for (int i = 0; i < 15; i++)
                        writer.Write((ulong)0);
                    
                    // Pad to header size
                    while (tempFile.Position < headerSize)
                        writer.Write((byte)0);
                    
                    // Write the laszip VLR if we have one
                    if (laszipVlr != null)
                    {
                        writer.Write(laszipVlr.Reserved);
                        writer.Write(laszipVlr.UserID);
                        writer.Write(laszipVlr.RecordID);
                        writer.Write(laszipVlr.RecordLengthAfterHeader);
                        writer.Write(laszipVlr.Description);
                        if (laszipVlr.Data != null)
                            writer.Write(laszipVlr.Data);
                    }
                    
                    // Write the compressed chunk
                    writer.Write(compressedChunk);
                }

                // Now decompress using LasZipDll
                var lazDll = new LasZipDll();
                lazDll.OpenReader(tempLazFile, out bool compressed);

                int pointsToRead = Math.Min(maxPoints, node.PointCount);

                for (int i = 0; i < pointsToRead; i++)
                {
                    lazDll.ReadPoint();
                    var point = lazDll.Point;

                    // Apply scale and offset to get real-world coordinates
                    double x = point.X * header.XScaleFactor + header.XOffset;
                    double y = point.Y * header.YScaleFactor + header.YOffset;
                    double z = point.Z * header.ZScaleFactor + header.ZOffset;

                    points.Add(new SimplePoint { X = x, Y = y, Z = z });
                }

                lazDll.CloseReader();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nDetailed error: {ex.Message}");
                Console.WriteLine("\nNote: Decompressing individual COPC chunks is challenging because:");
                Console.WriteLine("  1. Each chunk is independently compressed with LAZ");
                Console.WriteLine("  2. The compression state/context is self-contained per chunk");
                Console.WriteLine("  3. LasZipDll expects a specific LAZ file structure");
                Console.WriteLine("\nWorkaround: Extract compressed chunks and use PDAL:");
                Console.WriteLine("  # Save the chunk to a file, then:");
                Console.WriteLine("  pdal translate chunk.laz output.las");
                Console.WriteLine("\nOr use the COPC library features:");
                Console.WriteLine("  - Query nodes by bounding box");
                Console.WriteLine("  - Get hierarchy information");
                Console.WriteLine("  - Access compressed data for custom processing");
                throw;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempLazFile))
                {
                    try { File.Delete(tempLazFile); } catch { }
                }
            }

            return points;
        }

        private struct SimplePoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
        }
    }
}

