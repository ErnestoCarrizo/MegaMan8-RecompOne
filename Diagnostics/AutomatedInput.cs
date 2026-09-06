using RecompOne.Runtime.Events;

internal static class AutomatedInput
{
    private readonly record struct Pulse(long StartFrame, long Duration, ushort Button, string Name);

    private static readonly Pulse[] TitleSequence =
    [
        new(240, 8, 1 << 3, "Start"),
        new(360, 8, 1 << 14, "Cross"),
        new(480, 8, 1 << 14, "Cross"),
        new(600, 8, 1 << 14, "Cross"),
        new(720, 8, 1 << 3, "Start")
    ];

    private static bool _armed;
    private static bool _completed;
    private static long _framesSinceTitle;

    public static void EnableTitleProgression()
    {
        Event.AddListener<OverlayLoadedEvent>(OnOverlayLoaded);
        Event.AddListener<VSyncEvent>(OnVSync);
        Console.WriteLine("[AutoInput] piloto de avance desde el título activado");
    }

    private static void OnOverlayLoaded(OverlayLoadedEvent e)
    {
        if (_armed || _completed || !string.Equals(e.Name, "title", StringComparison.OrdinalIgnoreCase))
            return;

        _armed = true;
        _framesSinceTitle = 0;
        Console.WriteLine("[AutoInput] overlay title detectado; la primera pulsación será en 240 cuadros");
    }

    private static void OnVSync(VSyncEvent e)
    {
        if (!_armed) return;
        _framesSinceTitle++;

        foreach (var pulse in TitleSequence)
        {
            if (_framesSinceTitle != pulse.StartFrame) continue;
            LatchPress(e, pulse);
            break;
        }

        if (_framesSinceTitle > TitleSequence[^1].StartFrame + TitleSequence[^1].Duration)
        {
            _armed = false;
            _completed = true;
            Console.WriteLine("[AutoInput] secuencia de título terminada");
        }
    }

    private static void LatchPress(VSyncEvent e, Pulse pulse)
    {
        const uint heldAddress = 0x001B2954u;
        const uint pressedAddress = 0x001B2958u;
        var gameButton = SwapBytes(pulse.Button);
        var before = e.Memory.ReadU16(pressedAddress);
        e.Memory.WriteU16(heldAddress, (ushort)(e.Memory.ReadU16(heldAddress) | gameButton));
        e.Memory.WriteU16(pressedAddress, (ushort)(before | gameButton));
        Console.WriteLine(
            $"[AutoInput] {pulse.Name} fijado después de leer el pad en +{_framesSinceTitle}: " +
            $"pressed 0x{before:X4} -> 0x{e.Memory.ReadU16(pressedAddress):X4}");
    }

    private static ushort SwapBytes(ushort value) => (ushort)((value >> 8) | (value << 8));
}
