using System;

namespace Copc
{
    /// <summary>
    /// LAS file header structure (LAS 1.0 - 1.4).
    /// </summary>
    public class LasHeader
    {
        // File signature (should be "LASF")
        public ushort FileSourceID { get; set; }
        public ushort GlobalEncoding { get; set; }
        
        // Project ID (GUID)
        public uint ProjectIDGuidData1 { get; set; }
        public ushort ProjectIDGuidData2 { get; set; }
        public ushort ProjectIDGuidData3 { get; set; }
        public byte[] ProjectIDGuidData4 { get; set; } = new byte[8];
        
        // Version
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }
        
        // System and generating software
        public byte[] SystemIdentifier { get; set; } = new byte[32];
        public byte[] GeneratingSoftware { get; set; } = new byte[32];
        
        // File creation
        public ushort FileCreationDay { get; set; }
        public ushort FileCreationYear { get; set; }
        
        // Header info
        public ushort HeaderSize { get; set; }
        public uint OffsetToPointData { get; set; }
        public uint NumberOfVariableLengthRecords { get; set; }
        
        // Point data format
        public byte PointDataFormat { get; set; }
        public ushort PointDataRecordLength { get; set; }
        
        /// <summary>
        /// Gets the base point format (without compression bit).
        /// In LAZ files, bit 7 indicates compression, so we mask it off.
        /// </summary>
        public byte BasePointFormat => (byte)(PointDataFormat & 0x7F);
        
        /// <summary>
        /// Gets whether the point data is compressed (bit 7 set).
        /// </summary>
        public bool IsCompressed => (PointDataFormat & 0x80) != 0;
        
        // Legacy point counts (LAS 1.0-1.3)
        public uint NumberOfPointRecords { get; set; }
        public uint[] NumberOfPointsByReturn { get; set; } = new uint[5];
        
        // Scale and offset
        public double XScaleFactor { get; set; }
        public double YScaleFactor { get; set; }
        public double ZScaleFactor { get; set; }
        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public double ZOffset { get; set; }
        
        // Min/Max bounds
        public double MaxX { get; set; }
        public double MinX { get; set; }
        public double MaxY { get; set; }
        public double MinY { get; set; }
        public double MaxZ { get; set; }
        public double MinZ { get; set; }
        
        // LAS 1.3+ waveform data
        public ulong StartOfWaveformDataPacketRecord { get; set; }
        
        // LAS 1.4+ extended fields
        public ulong StartOfFirstExtendedVariableLengthRecord { get; set; }
        public uint NumberOfExtendedVariableLengthRecords { get; set; }
        public ulong ExtendedNumberOfPointRecords { get; set; }
        public ulong[] ExtendedNumberOfPointsByReturn { get; set; } = new ulong[15];
        
        public LasHeader()
        {
            // Initialize with defaults
            VersionMajor = 1;
            VersionMinor = 4;
            HeaderSize = 375; // LAS 1.4 header size
            XScaleFactor = 0.01;
            YScaleFactor = 0.01;
            ZScaleFactor = 0.01;
        }
        
        public override string ToString()
        {
            return $"LAS {VersionMajor}.{VersionMinor} Format {PointDataFormat}, " +
                   $"{Math.Max(NumberOfPointRecords, ExtendedNumberOfPointRecords)} points";
        }
    }
    
    /// <summary>
    /// LAS Variable Length Record.
    /// </summary>
    public class LasVariableLengthRecord
    {
        public ushort Reserved { get; set; }
        public byte[] UserID { get; set; } = new byte[16];
        public ushort RecordID { get; set; }
        public ushort RecordLengthAfterHeader { get; set; }
        public byte[] Description { get; set; } = new byte[32];
        public byte[]? Data { get; set; }
    }
}

