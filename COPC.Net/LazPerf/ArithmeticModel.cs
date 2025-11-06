using System;

namespace Copc.LazPerf
{
    /// <summary>
    /// Arithmetic model for symbol probability distribution (general multi-symbol model)
    /// </summary>
    public class ArithmeticModel
    {
        public uint Symbols { get; private set; }
        public uint[] Distribution { get; private set; }
        public uint[] SymbolCount { get; private set; }
        public uint[]? DecoderTable { get; private set; }
        
        public uint TotalCount { get; set; }
        public uint UpdateCycle { get; set; }
        public uint SymbolsUntilUpdate { get; set; }
        public uint LastSymbol { get; private set; }
        public uint TableSize { get; private set; }
        public int TableShift { get; private set; }

        public ArithmeticModel(uint symbols, uint[]? initTable = null)
        {
            if (symbols < 2 || symbols > (1 << 11))
                throw new ArgumentException("Invalid number of symbols");

            Symbols = symbols;
            LastSymbol = symbols - 1;

            // Determine if we need a decoder table for faster lookups
            if (symbols > 16)
            {
                int tableBits = 3;
                while (symbols > (1U << (tableBits + 2)))
                    tableBits++;
                TableSize = 1U << tableBits;
                TableShift = ArithmeticConstants.DM_LengthShift - tableBits;
                DecoderTable = new uint[TableSize + 2];
            }
            else
            {
                DecoderTable = null;
                TableSize = 0;
                TableShift = 0;
            }

            Distribution = new uint[symbols];
            SymbolCount = new uint[symbols];

            TotalCount = 0;
            UpdateCycle = symbols;

            // Initialize symbol counts
            if (initTable != null)
            {
                for (uint k = 0; k < symbols; k++)
                    SymbolCount[k] = initTable[k];
            }
            else
            {
                for (uint k = 0; k < symbols; k++)
                    SymbolCount[k] = 1;
            }

            Update();
            SymbolsUntilUpdate = UpdateCycle = (symbols + 6) >> 1;
        }

        public void Update()
        {
            // Halve counts when threshold is reached
            if ((TotalCount += UpdateCycle) > ArithmeticConstants.DM_MaxCount)
            {
                TotalCount = 0;
                for (uint n = 0; n < Symbols; n++)
                {
                    TotalCount += (SymbolCount[n] = (SymbolCount[n] + 1) >> 1);
                }
            }

            // Compute cumulative distribution and decoder table
            uint sum = 0;
            uint scale = 0x80000000U / TotalCount;

            if (TableSize == 0)
            {
                // No table - just compute distribution
                for (uint k = 0; k < Symbols; k++)
                {
                    Distribution[k] = (scale * sum) >> (31 - ArithmeticConstants.DM_LengthShift);
                    sum += SymbolCount[k];
                }
            }
            else
            {
                // Compute both distribution and decoder table
                uint s = 0;
                for (uint k = 0; k < Symbols; k++)
                {
                    Distribution[k] = (scale * sum) >> (31 - ArithmeticConstants.DM_LengthShift);
                    sum += SymbolCount[k];
                    uint w = Distribution[k] >> TableShift;
                    while (s < w)
                        DecoderTable![++s] = k - 1;
                }
                DecoderTable![0] = 0;
                while (s <= TableSize)
                    DecoderTable![++s] = Symbols - 1;
            }

            // Set frequency of model updates
            UpdateCycle = (5 * UpdateCycle) >> 2;
            uint maxCycle = (Symbols + 6) << 3;

            if (UpdateCycle > maxCycle)
                UpdateCycle = maxCycle;
            SymbolsUntilUpdate = UpdateCycle;
        }
    }

    /// <summary>
    /// Arithmetic bit model for binary probability (single bit model)
    /// </summary>
    public class ArithmeticBitModel
    {
        public uint UpdateCycle { get; private set; }
        public uint BitsUntilUpdate { get; set; }
        public uint Bit0Prob { get; private set; }
        public uint Bit0Count { get; set; }
        public uint BitCount { get; private set; }

        public ArithmeticBitModel()
        {
            // Initialize to equiprobable model
            Bit0Count = 1;
            BitCount = 2;
            Bit0Prob = 1U << (ArithmeticConstants.BM_LengthShift - 1);
            // Start with frequent updates
            UpdateCycle = BitsUntilUpdate = 4;
        }

        public void Update()
        {
            // Halve counts when threshold is reached
            if ((BitCount += UpdateCycle) > ArithmeticConstants.BM_MaxCount)
            {
                BitCount = (BitCount + 1) >> 1;
                Bit0Count = (Bit0Count + 1) >> 1;
                if (Bit0Count == BitCount)
                    BitCount++;
            }

            // Compute scaled bit 0 probability
            uint scale = 0x80000000U / BitCount;
            Bit0Prob = (Bit0Count * scale) >> (31 - ArithmeticConstants.BM_LengthShift);

            // Set frequency of model updates
            UpdateCycle = (5 * UpdateCycle) >> 2;
            if (UpdateCycle > 64)
                UpdateCycle = 64;
            BitsUntilUpdate = UpdateCycle;
        }
    }
}

