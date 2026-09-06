using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Sdk;

namespace MegaMan8Recomp.Patches;

internal static class MegaMan8DisplayModePatches
{
    private static readonly uint[] DispEnvironments =
    [
        0x001CF458u,
        0x001CF4F8u
    ];

    private static int _guardFrames;

    public static void Enable()
    {
        Event.AddListener<OverlayLoadedEvent>(OnOverlayLoaded);
        Event.AddListener<VSyncEvent>(OnVSync);
    }

    private static void OnOverlayLoaded(OverlayLoadedEvent e)
    {
        if (!string.Equals(e.Name, "title", StringComparison.OrdinalIgnoreCase) &&
            !e.Name.StartsWith("demo_stage_", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var env in DispEnvironments)
            e.Memory.WriteU8(env + 0x11u, 0);

        // Las películas STR dejan la GPU en RGB24. Los overlays vuelven a usar
        // el framebuffer normal de 320x240 y 15 bits.
        RestoreCurrentEnvironment(e.Context, e.Memory);
        _guardFrames = 120;
        Console.WriteLine($"[MM8.Video] modo de pantalla 15-bit restaurado al cargar {e.Name}");
    }

    private static void OnVSync(VSyncEvent e)
    {
        if (_guardFrames <= 0) return;
        _guardFrames--;
        RestoreCurrentEnvironment(e.Context, e.Memory);
    }

    private static void RestoreCurrentEnvironment(
        RecompOne.Runtime.Context.CpuContext context,
        IMemory memory)
    {
        var drawEnvironment = memory.ReadU32(0x00170334u) & 0x1FFFFFFFu;
        if (drawEnvironment is < 0x001CF3FCu or > 0x001CF49Cu)
        {
            RecompOne.Runtime.Runtime.Gpu?.WriteGp1(0x08000001u);
            return;
        }

        var displayEnvironment = drawEnvironment + 0x5Cu;
        memory.WriteU8(displayEnvironment + 0x11u, 0);
        var snapshot = context.Snapshot();
        context.A0 = displayEnvironment;
        LibGpu.PutDispEnv(context, memory);
        context.Restore(snapshot);
    }
}
