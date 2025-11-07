using System;
using System.Collections.Generic;

namespace Copc
{
    /// <summary>
    /// Configuration for a COPC file, containing all metadata needed to read/write the file.
    /// </summary>
    public class CopcConfig
    {
        /// <summary>
        /// The LAS header information.
        /// </summary>
        public LasHeader LasHeader { get; set; }

        /// <summary>
        /// COPC-specific information.
        /// </summary>
        public CopcInfo CopcInfo { get; set; }

        /// <summary>
        /// Extended extents information (optional).
        /// </summary>
        public CopcExtents? CopcExtents { get; set; }

        /// <summary>
        /// WKT (Well-Known Text) spatial reference system string (optional).
        /// </summary>
        public string? Wkt { get; set; }

        /// <summary>
        /// Extra dimensions defined in the Extra Bytes VLR (optional).
        /// These are custom per-point attributes beyond the standard LAS fields.
        /// </summary>
        public List<ExtraDimension> ExtraDimensions { get; set; } = new List<ExtraDimension>();

        public CopcConfig()
        {
            LasHeader = new LasHeader();
            CopcInfo = new CopcInfo();
            CopcExtents = null;
            Wkt = null;
        }

        public CopcConfig(LasHeader header, CopcInfo info, CopcExtents? extents = null, string? wkt = null)
        {
            LasHeader = header;
            CopcInfo = info;
            CopcExtents = extents;
            Wkt = wkt;
        }

        public override string ToString()
        {
            return $"CopcConfig: LAS {LasHeader.VersionMajor}.{LasHeader.VersionMinor}, " +
                   $"Format {LasHeader.PointDataFormat}, {CopcInfo}";
        }
    }

    /// <summary>
    /// Writer-specific configuration for COPC files.
    /// Provides mutable access to header and info for building COPC files.
    /// </summary>
    public class CopcConfigWriter : CopcConfig
    {
        public CopcConfigWriter() : base()
        {
        }

        public CopcConfigWriter(byte pointFormatId, Geometry.Vector3? scale = null, Geometry.Vector3? offset = null,
                               string? wkt = null) : base()
        {
            LasHeader.PointDataFormat = pointFormatId;
            LasHeader.VersionMajor = 1;
            LasHeader.VersionMinor = 4; // COPC requires LAS 1.4
            
            var actualScale = scale ?? Geometry.Vector3.DefaultScale();
            var actualOffset = offset ?? Geometry.Vector3.DefaultOffset();
            
            LasHeader.XScaleFactor = actualScale.X;
            LasHeader.YScaleFactor = actualScale.Y;
            LasHeader.ZScaleFactor = actualScale.Z;
            LasHeader.XOffset = actualOffset.X;
            LasHeader.YOffset = actualOffset.Y;
            LasHeader.ZOffset = actualOffset.Z;

            Wkt = wkt;
        }

        public CopcConfigWriter(CopcConfig config) : base(config.LasHeader, config.CopcInfo, config.CopcExtents, config.Wkt)
        {
        }
    }
}

