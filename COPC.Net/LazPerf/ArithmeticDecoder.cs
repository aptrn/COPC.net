using System;

namespace Copc.LazPerf
{
    /// <summary>
    /// Constants for arithmetic coding
    /// </summary>
    public static class ArithmeticConstants
    {
        public const uint AC_MinLength = 0x01000000U;
        public const uint AC_MaxLength = 0xFFFFFFFFU;
        
        // Maximum values for binary models
        public const int BM_LengthShift = 13;
        public const uint BM_MaxCount = 1U << BM_LengthShift;
        
        // Maximum values for general models
        public const int DM_LengthShift = 15;
        public const uint DM_MaxCount = 1U << DM_LengthShift;
    }

    /// <summary>
    /// Stream interface for reading compressed data
    /// </summary>
    public interface IInStream
    {
        byte GetByte();
        void GetBytes(byte[] buffer, int length);
    }

    /// <summary>
    /// Memory-based input stream for reading from byte arrays
    /// </summary>
    public class MemoryInStream : IInStream
    {
        private readonly byte[] _data;
        private int _index;

        public MemoryInStream(byte[] data, int offset = 0)
        {
            _data = data;
            _index = offset;
        }

        public byte GetByte()
        {
            if (_index >= _data.Length)
                throw new InvalidOperationException("End of stream reached");
            return _data[_index++];
        }

        public void GetBytes(byte[] buffer, int length)
        {
            if (_index + length > _data.Length)
                throw new InvalidOperationException("Not enough data in stream");
            Array.Copy(_data, _index, buffer, 0, length);
            _index += length;
        }

        public void Copy(int count, out byte[] buffer)
        {
            buffer = new byte[count];
            GetBytes(buffer, count);
        }
    }

    /// <summary>
    /// Arithmetic decoder - core decompression engine based on range coding
    /// </summary>
    public class ArithmeticDecoder
    {
        private uint _value;
        private uint _length;
        private readonly IInStream _inStream;
        private bool _hasData;

        public ArithmeticDecoder(IInStream inStream)
        {
            _inStream = inStream;
            _value = 0;
            _length = ArithmeticConstants.AC_MaxLength;
            _hasData = false;
        }

        public IInStream GetInStream() => _inStream;

        public void InitStream(int count)
        {
            if (count > 0)
            {
                ReadInitBytes();
                _hasData = true;
            }
        }

        public void ReadInitBytes()
        {
            _value = ((uint)_inStream.GetByte() << 24) |
                     ((uint)_inStream.GetByte() << 16) |
                     ((uint)_inStream.GetByte() << 8) |
                     _inStream.GetByte();
        }

        public uint DecodeBit(ArithmeticBitModel model)
        {
            uint x = model.Bit0Prob * (_length >> ArithmeticConstants.BM_LengthShift);
            uint sym = _value >= x ? 1U : 0U;

            if (sym == 0)
            {
                _length = x;
                model.Bit0Count++;
            }
            else
            {
                _value -= x;
                _length -= x;
            }

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            if (--model.BitsUntilUpdate == 0)
                model.Update();

            return sym;
        }

        public uint DecodeSymbol(ArithmeticModel model)
        {
            uint n, sym, x, y = _length;

            if (model.DecoderTable != null)
            {
                uint dv = _value / (_length >>= ArithmeticConstants.DM_LengthShift);
                uint t = dv >> model.TableShift;

                sym = model.DecoderTable[t];
                n = model.DecoderTable[t + 1] + 1;

                while (n > sym + 1)
                {
                    uint k = (sym + n) >> 1;
                    if (model.Distribution[k] > dv)
                        n = k;
                    else
                        sym = k;
                }

                x = model.Distribution[sym] * _length;
                if (sym != model.LastSymbol)
                    y = model.Distribution[sym + 1] * _length;
            }
            else
            {
                x = sym = 0;
                _length >>= ArithmeticConstants.DM_LengthShift;
                uint k = (n = model.Symbols) >> 1;

                do
                {
                    uint z = _length * model.Distribution[k];
                    if (z > _value)
                    {
                        n = k;
                        y = z;
                    }
                    else
                    {
                        sym = k;
                        x = z;
                    }
                } while ((k = (sym + n) >> 1) != sym);
            }

            _value -= x;
            _length = y - x;

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            model.SymbolCount[sym]++;

            if (--model.SymbolsUntilUpdate == 0)
                model.Update();

            return sym;
        }

        public uint ReadBit()
        {
            uint sym = _value / (_length >>= 1);
            _value -= _length * sym;

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            return sym;
        }

        public uint ReadBits(int bits)
        {
            if (bits <= 0 || bits > 32)
                throw new ArgumentException("Bits must be between 1 and 32");

            if (bits > 19)
            {
                uint tmp = ReadShort();
                bits -= 16;
                uint tmp1 = ReadBits(bits) << 16;
                return tmp1 | tmp;
            }

            uint sym = _value / (_length >>= bits);
            _value -= _length * sym;

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            return sym;
        }

        public byte ReadByte()
        {
            uint sym = _value / (_length >>= 8);
            _value -= _length * sym;

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            return (byte)sym;
        }

        public ushort ReadShort()
        {
            uint sym = _value / (_length >>= 16);
            _value -= _length * sym;

            if (_length < ArithmeticConstants.AC_MinLength)
                RenormDecInterval();

            return (ushort)sym;
        }

        public uint ReadInt()
        {
            uint lowerInt = ReadShort();
            uint upperInt = ReadShort();
            return (upperInt << 16) | lowerInt;
        }

        public float ReadFloat()
        {
            uint u = ReadInt();
            return BitConverter.ToSingle(BitConverter.GetBytes(u), 0);
        }

        public ulong ReadInt64()
        {
            ulong lowerInt = ReadInt();
            ulong upperInt = ReadInt();
            return (upperInt << 32) | lowerInt;
        }

        public double ReadDouble()
        {
            ulong u = ReadInt64();
            return BitConverter.Int64BitsToDouble((long)u);
        }

        public bool Valid() => _hasData;

        private void RenormDecInterval()
        {
            do
            {
                _value = (_value << 8) | _inStream.GetByte();
            } while ((_length <<= 8) < ArithmeticConstants.AC_MinLength);
        }
    }
}

