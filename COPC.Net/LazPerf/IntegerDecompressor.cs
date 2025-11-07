using System;
using System.Collections.Generic;

namespace Copc.LazPerf
{
    /// <summary>
    /// Integer decompressor for predictive coding
    /// </summary>
    public class IntegerDecompressor
    {
        private readonly uint _bits;
        private readonly uint _contexts;
        private readonly uint _bitsHigh;
        private readonly uint _range;
        
        private uint _corrBits;
        private uint _corrRange;
        private int _corrMin;
        private int _corrMax;
        
        public uint K { get; private set; }
        
        private List<ArithmeticModel> _mBits;
        private ArithmeticBitModel _mCorrector0;
        private List<ArithmeticModel> _mCorrector;

        public IntegerDecompressor(uint bits = 16, uint contexts = 1, uint bitsHigh = 8, uint range = 0)
        {
            _bits = bits;
            _contexts = contexts;
            _bitsHigh = bitsHigh;
            _range = range;
            
            _mBits = new List<ArithmeticModel>();
            _mCorrector0 = new ArithmeticBitModel();
            _mCorrector = new List<ArithmeticModel>();
            
            K = 0;

            // Calculate corrector parameters
            if (range != 0)
            {
                _corrBits = 0;
                _corrRange = range;
                uint r = range;
                while (r != 0)
                {
                    r >>= 1;
                    _corrBits++;
                }
                if (_corrRange == (1U << ((int)_corrBits - 1)))
                {
                    _corrBits--;
                }
                _corrMin = -((int)(_corrRange / 2));
                _corrMax = _corrMin + (int)_corrRange - 1;
            }
            else if (bits != 0 && bits < 32)
            {
                _corrBits = bits;
                _corrRange = 1U << (int)bits;
                _corrMin = -((int)(_corrRange / 2));
                _corrMax = _corrMin + (int)_corrRange - 1;
            }
            else
            {
                _corrBits = 32;
                _corrRange = 0;
                _corrMin = int.MinValue;
                _corrMax = int.MaxValue;
            }
        }

        public void Init()
        {
            // Create models if not already created
            if (_mBits.Count == 0)
            {
                for (uint i = 0; i < _contexts; i++)
                    _mBits.Add(new ArithmeticModel(_corrBits + 1));

                // Initialize corrector models
                for (uint i = 1; i <= _corrBits; i++)
                {
                    uint v = i <= _bitsHigh ? (1U << (int)i) : (1U << (int)_bitsHigh);
                    _mCorrector.Add(new ArithmeticModel(v));
                }
            }
        }

        public int Decompress(ArithmeticDecoder decoder, int pred, uint context)
        {
            int corrector = ReadCorrector(decoder, _mBits[(int)context]);
            int real = pred + corrector;
            
            // Debug for X coordinate (bits=32, contexts=2)
            if (_bits == 32 && _contexts == 2 && (real < -1000000 || real > 1000000))
            {
                Console.WriteLine($"[DEBUG IntDecomp] pred={pred}, corrector={corrector}, real={real}, K={K}, _corrRange={_corrRange}");
            }
            
            if (real < 0)
                real += (int)_corrRange;
            else if ((uint)real >= _corrRange)
                real -= (int)_corrRange;

            return real;
        }

        public uint GetK() => K;

        private int ReadCorrector(ArithmeticDecoder decoder, ArithmeticModel mBits)
        {
            int c;

            // Decode which interval the corrector falls into
            K = decoder.DecodeSymbol(mBits);

            // Decode the exact location within the interval
            if (K != 0) // c is either smaller than 0 or bigger than 1
            {
                if (K < 32)
                {
                    if (K <= _bitsHigh) // Small k - do in one step
                    {
                        // Decompress c with the range coder
                        c = (int)decoder.DecodeSymbol(_mCorrector[(int)K - 1]);
                    }
                    else
                    {
                        // Larger k - need two steps
                        int k1 = (int)K - (int)_bitsHigh;
                        // Decompress higher bits with table
                        c = (int)decoder.DecodeSymbol(_mCorrector[(int)K - 1]);
                        // Read lower bits raw
                        int c1 = (int)decoder.ReadBits(k1);
                        // Put corrector back together
                        c = (c << k1) | c1;
                    }
                    
                    // Translate c back into its correct interval
                    if (c >= (1 << ((int)K - 1))) // c is in interval [2^(k-1) ... 2^k - 1]
                    {
                        // Translate back to interval [2^(k-1) + 1 ... 2^k]
                        c += 1;
                    }
                    else // c is in interval [0 ... 2^(k-1) - 1]
                    {
                        // Translate back to interval [-(2^k - 1) ... -(2^(k-1))]
                        c -= ((1 << (int)K) - 1);
                    }
                }
                else
                {
                    c = _corrMin;
                }
            }
            else // c is either 0 or 1
            {
                c = (int)decoder.DecodeBit(_mCorrector0);
            }

            return c;
        }
    }
}

