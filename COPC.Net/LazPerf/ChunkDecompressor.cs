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
        private int _pointFormat;
        private int _pointSize;
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
            
            // Create decoder
            _decoder = new ArithmeticDecoder(inStream);
            
            // Initialize the decoder with the chunk data
            // Skip first 4 bytes which contain the initial value for the arithmetic decoder
            _decoder.ReadInitBytes();

            // Create appropriate decompressor based on point format
            switch (pointFormat)
            {
                case 0:
                    if (pointSize != 20)
                        throw new ArgumentException($"Point format 0 should have size 20, got {pointSize}");
                    _point10Decompressor = new Point10Decompressor(_decoder);
                    break;
                    
                case 1:
                case 2:
                case 3:
                    throw new NotImplementedException($"Point format {pointFormat} not yet implemented. Currently only format 0 is supported.");
                    
                case 6:
                case 7:
                case 8:
                    throw new NotImplementedException($"Point format {pointFormat} not yet implemented. Currently only format 0 is supported.");
                    
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

            if (_decoder == null)
                throw new InvalidOperationException("Decoder not initialized");

            switch (_pointFormat)
            {
                case 0:
                    if (_point10Decompressor == null)
                        throw new InvalidOperationException("Point10 decompressor not initialized");
                    return _point10Decompressor.Decompress();
                    
                default:
                    throw new NotImplementedException($"Point format {_pointFormat} decompression not implemented");
            }
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
            _isOpen = false;
        }

        /// <summary>
        /// Check if decompressor is open
        /// </summary>
        public bool IsOpen => _isOpen;
    }
}

