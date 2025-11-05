using System;
using System.IO;
using System.Text;

namespace Copc.Utils
{
    /// <summary>
    /// Extension methods and utilities for binary I/O operations.
    /// </summary>
    public static class BinaryExtensions
    {
        /// <summary>
        /// Reads a null-terminated string from a byte array.
        /// </summary>
        public static string ReadNullTerminatedString(this byte[] data)
        {
            int nullIndex = Array.IndexOf(data, (byte)0);
            if (nullIndex < 0)
                nullIndex = data.Length;
            
            return Encoding.ASCII.GetString(data, 0, nullIndex);
        }

        /// <summary>
        /// Writes a string to a byte array with null-termination and padding.
        /// </summary>
        public static void WriteStringToByteArray(string str, byte[] dest, int maxLength)
        {
            if (str == null)
                str = string.Empty;

            int bytesToCopy = Math.Min(str.Length, maxLength - 1);
            Encoding.ASCII.GetBytes(str, 0, bytesToCopy, dest, 0);
            
            // Fill remaining with nulls
            for (int i = bytesToCopy; i < maxLength; i++)
            {
                dest[i] = 0;
            }
        }

        /// <summary>
        /// Reads exactly the specified number of bytes from a stream.
        /// Throws an exception if not enough bytes are available.
        /// </summary>
        public static byte[] ReadExactly(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;
            
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0)
                    throw new EndOfStreamException($"Expected to read {count} bytes but only got {totalRead}");
                totalRead += read;
            }
            
            return buffer;
        }

        /// <summary>
        /// Reads exactly the specified number of bytes from a stream into a buffer.
        /// </summary>
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                    throw new EndOfStreamException($"Expected to read {count} bytes but only got {totalRead}");
                totalRead += read;
            }
        }
    }
}

