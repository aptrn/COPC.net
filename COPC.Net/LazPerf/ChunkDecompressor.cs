using System;

namespace Copc.LazPerf
{
    /// <summary>
    /// Main API for decompressing LAZ chunks (for COPC files)
    /// This allows decompressing individual chunks from memory, unlike LasZip which requires sequential file access
    /// </summary>
    public class ChunkDecompressor
    {
        private ArithmeticDecoder? _decoder;
        private Point10Decompressor? _point10Decompressor;
        private Point14Decompressor? _point14Decompressor;
        private int _pointFormat;
        private int _pointSize;
        private int _basePointSize;  // Size without extra bytes
        private bool _isOpen;

        public ChunkDecompressor()
        {
            _isOpen = false;
        }

        /// <summary>
        /// Open a chunk for decompression
        /// </summary>
        /// <param name="pointFormat">LAS point data record format (0-10)</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="chunkData">Compressed chunk data</param>
        public void Open(int pointFormat, int pointSize, byte[] chunkData)
        {
            if (_isOpen)
                throw new InvalidOperationException("ChunkDecompressor is already open. Call Close() first.");

            _pointFormat = pointFormat;
            _pointSize = pointSize;

            // Create input stream from chunk data
            var inStream = new MemoryInStream(chunkData);

            // Create appropriate decompressor based on point format
            switch (pointFormat)
            {
                case 0:
                    _basePointSize = 20;
                    if (pointSize != _basePointSize)
                        throw new ArgumentException($"Point format 0 should have size {_basePointSize}, got {pointSize}");
                    // Format 0 uses a single arithmetic decoder
                    _decoder = new ArithmeticDecoder(inStream);
                    _decoder.ReadInitBytes();
                    _point10Decompressor = new Point10Decompressor(_decoder);
                    break;
                    
                case 1:
                case 2:
                case 3:
                    throw new NotImplementedException($"Point format {pointFormat} not yet implemented. Currently only formats 0 and 6-8 are supported.");
                    
                case 6:
                    _basePointSize = 30;
                    if (pointSize < _basePointSize)
                        throw new ArgumentException($"Point format 6 should have size >= {_basePointSize}, got {pointSize}");
                    // Format 6/7/8 use multiple arithmetic decoders internally
                    int extraBytes6 = pointSize - _basePointSize;
                    _point14Decompressor = new Point14Decompressor(inStream, pointFormat, extraBytes6);
                    break;
                    
                case 7:
                    _basePointSize = 36;
                    if (pointSize < _basePointSize)
                        throw new ArgumentException($"Point format 7 should have size >= {_basePointSize}, got {pointSize}");
                    // Format 6/7/8 use multiple arithmetic decoders internally
                    int extraBytes7 = pointSize - _basePointSize;
                    _point14Decompressor = new Point14Decompressor(inStream, pointFormat, extraBytes7);
                    break;
                    
                case 8:
                    _basePointSize = 38;
                    if (pointSize < _basePointSize)
                        throw new ArgumentException($"Point format 8 should have size >= {_basePointSize}, got {pointSize}");
                    // Format 6/7/8 use multiple arithmetic decoders internally
                    int extraBytes8 = pointSize - _basePointSize;
                    _point14Decompressor = new Point14Decompressor(inStream, pointFormat, extraBytes8);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported point format: {pointFormat}");
            }

            _isOpen = true;
        }

        /// <summary>
        /// Decompress a single point from the chunk
        /// </summary>
        /// <returns>Decompressed point data as byte array</returns>
        public byte[] GetPoint()
        {
            if (!_isOpen)
                throw new InvalidOperationException("ChunkDecompressor is not open. Call Open() first.");

            byte[] baseData;
            
            switch (_pointFormat)
            {
                case 0:
                    if (_point10Decompressor == null)
                        throw new InvalidOperationException("Point10 decompressor not initialized");
                    baseData = _point10Decompressor.Decompress();
                    break;
                    
                case 6:
                case 7:
                case 8:
                    if (_point14Decompressor == null)
                        throw new InvalidOperationException("Point14 decompressor not initialized");
                    baseData = _point14Decompressor.Decompress();
                    break;
                    
                default:
                    throw new NotImplementedException($"Point format {_pointFormat} decompression not implemented");
            }
            
            // If point has extra bytes beyond what the decompressor returned, pad with zeros
            // Note: Point14Decompressor now returns base point + RGB (for format 7/8), but not extra bytes yet
            // TODO: Properly decompress extra bytes using byte14 field compressor
            if (_pointSize > baseData.Length)
            {
                var fullData = new byte[_pointSize];
                Array.Copy(baseData, 0, fullData, 0, baseData.Length);
                // Extra bytes are left as zeros for now
                return fullData;
            }
            
            return baseData;
        }

        /// <summary>
        /// Decompress all points from a chunk
        /// </summary>
        /// <param name="pointFormat">LAS point data record format</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="chunkData">Compressed chunk data</param>
        /// <param name="pointCount">Number of points in the chunk</param>
        /// <returns>Array of decompressed point data</returns>
        public static byte[][] DecompressChunk(int pointFormat, int pointSize, byte[] chunkData, int pointCount)
        {
            var decompressor = new ChunkDecompressor();
            decompressor.Open(pointFormat, pointSize, chunkData);

            var points = new byte[pointCount][];
            for (int i = 0; i < pointCount; i++)
            {
                points[i] = decompressor.GetPoint();
            }

            decompressor.Close();
            return points;
        }

        /// <summary>
        /// Decompress all points from a chunk into a flat byte array
        /// </summary>
        /// <param name="pointFormat">LAS point data record format</param>
        /// <param name="pointSize">Size of each point record in bytes</param>
        /// <param name="chunkData">Compressed chunk data</param>
        /// <param name="pointCount">Number of points in the chunk</param>
        /// <returns>Flat array of decompressed point data</returns>
        public static byte[] DecompressChunkFlat(int pointFormat, int pointSize, byte[] chunkData, int pointCount)
        {
            var decompressor = new ChunkDecompressor();
            decompressor.Open(pointFormat, pointSize, chunkData);

            var result = new byte[pointCount * pointSize];
            for (int i = 0; i < pointCount; i++)
            {
                var pointData = decompressor.GetPoint();
                Array.Copy(pointData, 0, result, i * pointSize, pointSize);
            }

            decompressor.Close();
            return result;
        }

        /// <summary>
        /// Close the decompressor and release resources
        /// </summary>
        public void Close()
        {
            _decoder = null;
            _point10Decompressor = null;
            _point14Decompressor = null;
            _isOpen = false;
        }

        /// <summary>
        /// Check if decompressor is open
        /// </summary>
        public bool IsOpen => _isOpen;
    }
}

