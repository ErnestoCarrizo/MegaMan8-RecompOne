using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hle;
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
    private static bool _softwareVideoPresentation;

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

        RestoreAcceleratedPresentation();

        // Las películas STR dejan la GPU en RGB24. Los overlays vuelven a usar
        // el framebuffer normal de 320x240 y 15 bits.
        RestoreCurrentEnvironment(e.Context, e.Memory);
        _guardFrames = 120;
        Console.WriteLine($"[MM8.Video] modo de pantalla 15-bit restaurado al cargar {e.Name}");
    }

    private static void OnVSync(VSyncEvent e)
    {
        UpdateVideoPresentation();
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

    private static void UpdateVideoPresentation()
    {
        var gpu = RecompOne.Runtime.Runtime.Gpu;
        if (gpu is null) return;

        if (gpu.Display24Bit)
        {
            if (_softwareVideoPresentation) return;

            // Los cuadros MDEC se cargan desde una tarea cooperativa que no
            // posee el contexto OpenGL. La VRAM de CPU sí queda actualizada;
            // desactivar HLE durante RGB24 hace que la ventana presente esa
            // copia directamente en lugar de una textura acelerada obsoleta.
            GpuHle.Active = false;
            _softwareVideoPresentation = true;
            Console.WriteLine("[MM8.Video] presentación RGB24 cambiada a VRAM de CPU");
            return;
        }

        RestoreAcceleratedPresentation();
    }

    private static void RestoreAcceleratedPresentation()
    {
        if (!_softwareVideoPresentation) return;
        GpuHle.Active = GpuHle.Backend?.Ready == true;
        _softwareVideoPresentation = false;
        Console.WriteLine("[MM8.Video] renderizador acelerado restaurado para RGB15");
    }
}
