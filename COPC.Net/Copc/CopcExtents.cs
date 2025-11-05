using System;
using System.Collections.Generic;

namespace Copc
{
    /// <summary>
    /// Represents extended extents information for COPC files.
    /// This can include statistics for extra bytes dimensions.
    /// </summary>
    public class CopcExtents
    {
        /// <summary>
        /// Dictionary of dimension name to (min, max) extent values.
        /// </summary>
        public Dictionary<string, (double Min, double Max)> Extents { get; set; }

        public CopcExtents()
        {
            Extents = new Dictionary<string, (double, double)>();
        }

        /// <summary>
        /// Adds or updates an extent for a dimension.
        /// </summary>
        public void SetExtent(string dimensionName, double min, double max)
        {
            Extents[dimensionName] = (min, max);
        }

        /// <summary>
        /// Gets the extent for a dimension if it exists.
        /// </summary>
        public bool TryGetExtent(string dimensionName, out double min, out double max)
        {
            if (Extents.TryGetValue(dimensionName, out var extent))
            {
                min = extent.Min;
                max = extent.Max;
                return true;
            }
            min = max = 0;
            return false;
        }

        public override string ToString()
        {
            return $"CopcExtents: {Extents.Count} dimensions";
        }
    }
}

