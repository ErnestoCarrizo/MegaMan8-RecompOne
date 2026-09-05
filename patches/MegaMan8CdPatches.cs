using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;
using Recompiled;

namespace MegaMan8Recomp.Patches;

internal static class MegaMan8CdPatches
{
    private const uint StreamStateAddress = 0x001C335Cu;
    private const uint StreamReading = 1u;
    private const uint StreamComplete = 2u;
    private const uint LoaderAddress = 0x001554F4u;
    private const uint LoaderInhibitAddress = 0x001555B6u;

    public static void CdReady(CpuContext c, IMemory m)
    {
        // Mega Man 8 polls CdReady in non-blocking mode before starting a read.
        // RecompOne retains the previous Complete result, whereas PsyQ reports
        // CdlNoIntr here when no asynchronous read is active.
        if (c.A0 != 0 && m.ReadU32(StreamStateAddress) != StreamReading)
        {
            c.V0 = 0;
            return;
        }

        LibCd.CdReady(c, m);
    }

    public static void CdControl(CpuContext c, IMemory m)
    {
        LibCd.CdControl(c, m);
    }

    public static void PollCdLoader(CpuContext c, IMemory m)
    {
        var savedA0 = c.A0;
        var savedA1 = c.A1;
        var savedA2 = c.A2;
        var savedA3 = c.A3;

        // RecompOne normally advances asynchronous CD reads from PresentFrame.
        // Mega Man 8 waits here without presenting another frame, so deliver one
        // sector per poll. Keeping the pump outside CdControl also permits the
        // game's callback to restart a read without re-entering the active pump.
        if (m.ReadU32(StreamStateAddress) == StreamReading)
        {
            try
            {
                c.A0 = 1;
                c.A1 = 0;
                LibCd.CdReady(c, m);
            }
            finally
            {
                c.A0 = savedA0;
                c.A1 = savedA1;
                c.A2 = savedA2;
                c.A3 = savedA3;
            }
        }

        var complete = m.ReadU32(StreamStateAddress) == StreamComplete;
        c.A0 = LoaderAddress;
        MegaMan8.func_800FC274(c, m);
        c.V0 = complete && m.ReadU8(LoaderInhibitAddress) == 0 ? 1u : 0u;
    }
}
