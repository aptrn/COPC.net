using System;
using System.Collections.Generic;
using System.Text;
using Copc.IO;

namespace Copc.Examples
{
    /// <summary>
    /// Helper class for printing point information including extra dimensions
    /// </summary>
    public static class PointPrintHelper
    {
        /// <summary>
        /// Formats extra dimensions from a point's ExtraBytes field
        /// </summary>
        public static string FormatExtraDimensions(CopcPoint point, List<ExtraDimension>? extraDimensions)
        {
            if (extraDimensions == null || extraDimensions.Count == 0 || point.ExtraBytes == null || point.ExtraBytes.Length == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            int offset = 0;

            foreach (var dim in extraDimensions)
            {
                int dimSize = dim.GetDataSize() * dim.GetComponentCount();
                
                if (offset + dimSize > point.ExtraBytes.Length)
                {
                    break; // Not enough data
                }

                try
                {
                    var values = dim.ExtractAsFloat32(point.ExtraBytes, offset);
                    
                    if (values.Length == 1)
                    {
                        sb.Append($" {dim.Name}={values[0]:F4}");
                    }
                    else
                    {
                        sb.Append($" {dim.Name}=[{string.Join(", ", Array.ConvertAll(values, v => $"{v:F4}"))}]");
                    }
                }
                catch
                {
                    sb.Append($" {dim.Name}=<error>");
                }

                offset += dimSize;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Prints a single point with all its attributes including extra dimensions
        /// </summary>
        public static void PrintPoint(int index, CopcPoint point, List<ExtraDimension>? extraDimensions = null)
        {
            var extraInfo = FormatExtraDimensions(point, extraDimensions);
            
            Console.WriteLine($"[{index,3}] X={point.X,12:F3} Y={point.Y,12:F3} Z={point.Z,12:F3} " +
                            $"Intensity={point.Intensity,5} Class={point.Classification,3}{extraInfo}");
        }

        /// <summary>
        /// Prints a point with RGB color
        /// </summary>
        public static void PrintPointWithRGB(int index, CopcPoint point, List<ExtraDimension>? extraDimensions = null)
        {
            var extraInfo = FormatExtraDimensions(point, extraDimensions);
            
            Console.WriteLine($"[{index,3}] X={point.X,12:F3} Y={point.Y,12:F3} Z={point.Z,12:F3} " +
                            $"Intensity={point.Intensity,5} Class={point.Classification,3} " +
                            $"RGB=({point.Red},{point.Green},{point.Blue}){extraInfo}");
        }

        /// <summary>
        /// Prints a point with distance information
        /// </summary>
        public static void PrintPointWithDistance(int index, CopcPoint point, double distance, List<ExtraDimension>? extraDimensions = null)
        {
            var extraInfo = FormatExtraDimensions(point, extraDimensions);
            
            Console.WriteLine($"[{index,3}] X={point.X,12:F3} Y={point.Y,12:F3} Z={point.Z,12:F3} " +
                            $"Distance={distance,8:F3}m Intensity={point.Intensity,5} Class={point.Classification,3}{extraInfo}");
        }
    }
}

