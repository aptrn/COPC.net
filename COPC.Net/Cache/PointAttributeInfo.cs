using System;
using System.Collections.Generic;
using Copc.IO;

namespace Copc.Cache
{
    /// <summary>
    /// Describes a point cloud attribute that can be used for rendering.
    /// </summary>
    public class PointAttributeInfo
    {
        /// <summary>
        /// Attribute name (e.g., "Intensity", "Classification", "Red", "Green", "Blue")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Data type of the attribute
        /// </summary>
        public AttributeDataType DataType { get; set; }

        /// <summary>
        /// Number of components (1 for scalar, 3 for RGB, 4 for RGBA)
        /// </summary>
        public int ComponentCount { get; set; }

        /// <summary>
        /// Minimum value (if known)
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Maximum value (if known)
        /// </summary>
        public double? MaxValue { get; set; }

        /// <summary>
        /// Whether this attribute is always present
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Description of what this attribute represents
        /// </summary>
        public string Description { get; set; } = string.Empty;

        public override string ToString()
        {
            var range = MinValue.HasValue && MaxValue.HasValue 
                ? $" [{MinValue.Value}-{MaxValue.Value}]" 
                : "";
            return $"{Name} ({DataType}, {ComponentCount} component(s)){range} - {Description}";
        }
    }

    /// <summary>
    /// Data type of an attribute
    /// </summary>
    public enum AttributeDataType
    {
        Float32,
        UInt8,
        UInt16,
        Int32,
        Double
    }

    /// <summary>
    /// Contains information about all available attributes in a point cloud.
    /// </summary>
    public class PointCloudAttributeMetadata
    {
        /// <summary>
        /// LAS point format (0-10)
        /// </summary>
        public int PointFormat { get; set; }

        /// <summary>
        /// All available attributes
        /// </summary>
        public List<PointAttributeInfo> Attributes { get; set; } = new List<PointAttributeInfo>();

        /// <summary>
        /// Whether the point cloud has RGB data
        /// </summary>
        public bool HasRGB { get; set; }

        /// <summary>
        /// Whether the point cloud has GPS time
        /// </summary>
        public bool HasGpsTime { get; set; }

        /// <summary>
        /// Whether the point cloud has NIR (Near Infrared)
        /// </summary>
        public bool HasNIR { get; set; }

        /// <summary>
        /// Point data record length in bytes
        /// </summary>
        public int PointRecordLength { get; set; }

        /// <summary>
        /// Gets an attribute by name
        /// </summary>
        public PointAttributeInfo? GetAttribute(string name)
        {
            return Attributes.Find(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if an attribute exists
        /// </summary>
        public bool HasAttribute(string name)
        {
            return GetAttribute(name) != null;
        }

        public override string ToString()
        {
            return $"LAS Format {PointFormat}: {Attributes.Count} attributes, " +
                   $"RGB={HasRGB}, GPS={HasGpsTime}, NIR={HasNIR}";
        }
    }

    /// <summary>
    /// Helper class to extract attribute metadata from LAS header.
    /// </summary>
    public static class PointAttributeMetadataExtractor
    {
        /// <summary>
        /// Extracts attribute metadata from a LAS header.
        /// </summary>
        public static PointCloudAttributeMetadata ExtractFromHeader(LasHeader header)
        {
            var metadata = new PointCloudAttributeMetadata
            {
                PointFormat = header.BasePointFormat,
                PointRecordLength = header.PointDataRecordLength
            };

            // Always present in all formats
            AddCommonAttributes(metadata);

            // Format-specific attributes
            switch (header.BasePointFormat)
            {
                case 0:
                    // Format 0: Basic
                    metadata.HasRGB = false;
                    metadata.HasGpsTime = false;
                    metadata.HasNIR = false;
                    break;

                case 1:
                    // Format 1: Basic + GPS Time
                    metadata.HasGpsTime = true;
                    AddGpsTimeAttribute(metadata);
                    break;

                case 2:
                    // Format 2: Basic + RGB
                    metadata.HasRGB = true;
                    AddRGBAttributes(metadata);
                    break;

                case 3:
                    // Format 3: Basic + GPS Time + RGB
                    metadata.HasGpsTime = true;
                    metadata.HasRGB = true;
                    AddGpsTimeAttribute(metadata);
                    AddRGBAttributes(metadata);
                    break;

                case 6:
                    // Format 6: Extended (1.4) + GPS Time
                    metadata.HasGpsTime = true;
                    AddGpsTimeAttribute(metadata);
                    AddExtendedAttributes(metadata);
                    break;

                case 7:
                    // Format 7: Extended (1.4) + GPS Time + RGB
                    metadata.HasGpsTime = true;
                    metadata.HasRGB = true;
                    AddGpsTimeAttribute(metadata);
                    AddRGBAttributes(metadata);
                    AddExtendedAttributes(metadata);
                    break;

                case 8:
                    // Format 8: Extended (1.4) + GPS Time + RGB + NIR
                    metadata.HasGpsTime = true;
                    metadata.HasRGB = true;
                    metadata.HasNIR = true;
                    AddGpsTimeAttribute(metadata);
                    AddRGBAttributes(metadata);
                    AddNIRAttribute(metadata);
                    AddExtendedAttributes(metadata);
                    break;

                default:
                    // Other formats: assume basic set
                    break;
            }

            return metadata;
        }

        private static void AddCommonAttributes(PointCloudAttributeMetadata metadata)
        {
            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "X",
                DataType = AttributeDataType.Double,
                ComponentCount = 1,
                IsRequired = true,
                Description = "X coordinate in world space"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Y",
                DataType = AttributeDataType.Double,
                ComponentCount = 1,
                IsRequired = true,
                Description = "Y coordinate in world space"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Z",
                DataType = AttributeDataType.Double,
                ComponentCount = 1,
                IsRequired = true,
                Description = "Z coordinate (elevation) in world space"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Intensity",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Pulse return intensity (0-65535)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "ReturnNumber",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 1,
                MaxValue = 15,
                IsRequired = true,
                Description = "Return number (1-15)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "NumberOfReturns",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 1,
                MaxValue = 15,
                IsRequired = true,
                Description = "Number of returns for this pulse (1-15)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Classification",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 255,
                IsRequired = true,
                Description = "LAS classification code (0-255)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "ScanAngle",
                DataType = AttributeDataType.Float32,
                ComponentCount = 1,
                MinValue = -180,
                MaxValue = 180,
                IsRequired = true,
                Description = "Scan angle in degrees"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "UserData",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 255,
                IsRequired = true,
                Description = "User data (0-255)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "PointSourceId",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Point source ID (0-65535)"
            });
        }

        private static void AddGpsTimeAttribute(PointCloudAttributeMetadata metadata)
        {
            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "GpsTime",
                DataType = AttributeDataType.Double,
                ComponentCount = 1,
                IsRequired = true,
                Description = "GPS time stamp"
            });
        }

        private static void AddRGBAttributes(PointCloudAttributeMetadata metadata)
        {
            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Red",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Red color channel (0-65535)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Green",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Green color channel (0-65535)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "Blue",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Blue color channel (0-65535)"
            });
        }

        private static void AddNIRAttribute(PointCloudAttributeMetadata metadata)
        {
            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "NIR",
                DataType = AttributeDataType.UInt16,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 65535,
                IsRequired = true,
                Description = "Near Infrared channel (0-65535)"
            });
        }

        private static void AddExtendedAttributes(PointCloudAttributeMetadata metadata)
        {
            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "ScanDirectionFlag",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 1,
                IsRequired = true,
                Description = "Scan direction flag (0 or 1)"
            });

            metadata.Attributes.Add(new PointAttributeInfo
            {
                Name = "EdgeOfFlightLine",
                DataType = AttributeDataType.UInt8,
                ComponentCount = 1,
                MinValue = 0,
                MaxValue = 1,
                IsRequired = true,
                Description = "Edge of flight line flag (0 or 1)"
            });
        }
    }
}

