using System;
using System.Runtime.InteropServices;

namespace Copc.LazPerf
{
    /// <summary>
    /// Utility functions for LAZ decompression
    /// </summary>
    public static class LazUtils
    {
        /// <summary>
        /// Unpack little-endian int32 from byte array
        /// </summary>
        public static int UnpackInt32(byte[] data, int offset)
        {
            return data[offset] |
                   (data[offset + 1] << 8) |
                   (data[offset + 2] << 16) |
                   (data[offset + 3] << 24);
        }

        /// <summary>
        /// Unpack little-endian uint32 from byte array
        /// </summary>
        public static uint UnpackUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] |
                         (data[offset + 1] << 8) |
                         (data[offset + 2] << 16) |
                         (data[offset + 3] << 24));
        }

        /// <summary>
        /// Unpack little-endian int16 from byte array
        /// </summary>
        public static short UnpackInt16(byte[] data, int offset)
        {
            return (short)(data[offset] | (data[offset + 1] << 8));
        }

        /// <summary>
        /// Unpack little-endian uint16 from byte array
        /// </summary>
        public static ushort UnpackUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        /// <summary>
        /// Pack int32 to byte array in little-endian format
        /// </summary>
        public static void PackInt32(int value, byte[] data, int offset)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        /// <summary>
        /// Pack uint16 to byte array in little-endian format
        /// </summary>
        public static void PackUInt16(ushort value, byte[] data, int offset)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        /// <summary>
        /// Clear a specific bit in a value
        /// </summary>
        public static T ClearBit<T>(T value, int bit) where T : struct
        {
            if (typeof(T) == typeof(uint))
            {
                uint uval = (uint)(object)value;
                return (T)(object)(uval & ~(1U << bit));
            }
            if (typeof(T) == typeof(int))
            {
                int ival = (int)(object)value;
                return (T)(object)(ival & ~(1 << bit));
            }
            throw new NotSupportedException($"Type {typeof(T)} not supported");
        }
    }

    /// <summary>
    /// Streaming median calculator for 5 values
    /// </summary>
    public class StreamingMedian<T> where T : struct, IComparable<T>
    {
        private T[] _values;
        private bool _high;

        public StreamingMedian()
        {
            _values = new T[5];
            _high = true;
        }

        public void Init()
        {
            for (int i = 0; i < 5; i++)
                _values[i] = default(T);
            _high = true;
        }

        public void Add(T v)
        {
            if (_high)
            {
                if (v.CompareTo(_values[2]) < 0)
                {
                    _values[4] = _values[3];
                    _values[3] = _values[2];
                    if (v.CompareTo(_values[0]) < 0)
                    {
                        _values[2] = _values[1];
                        _values[1] = _values[0];
                        _values[0] = v;
                    }
                    else if (v.CompareTo(_values[1]) < 0)
                    {
                        _values[2] = _values[1];
                        _values[1] = v;
                    }
                    else
                    {
                        _values[2] = v;
                    }
                }
                else
                {
                    if (v.CompareTo(_values[3]) < 0)
                    {
                        _values[4] = _values[3];
                        _values[3] = v;
                    }
                    else
                    {
                        _values[4] = v;
                    }
                    _high = false;
                }
            }
            else
            {
                if (_values[2].CompareTo(v) < 0)
                {
                    _values[0] = _values[1];
                    _values[1] = _values[2];
                    if (_values[4].CompareTo(v) < 0)
                    {
                        _values[2] = _values[3];
                        _values[3] = _values[4];
                        _values[4] = v;
                    }
                    else if (_values[3].CompareTo(v) < 0)
                    {
                        _values[2] = _values[3];
                        _values[3] = v;
                    }
                    else
                    {
                        _values[2] = v;
                    }
                }
                else
                {
                    if (_values[1].CompareTo(v) < 0)
                    {
                        _values[0] = _values[1];
                        _values[1] = v;
                    }
                    else
                    {
                        _values[0] = v;
                    }
                    _high = true;
                }
            }
        }

        public T Get()
        {
            return _values[2];
        }
    }
}

