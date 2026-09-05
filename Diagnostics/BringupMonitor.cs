using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;

internal static class BringupMonitor
{
    private sealed record MemoryValue(string Name, uint Address, int Size);
    private sealed record CodeRange(string Name, uint Address, uint Size);

    private static readonly MemoryValue[] Values =
    [
        new("bossId", 0x0015B1B8u, 1),
        new("bossLife", 0x0015B1BBu, 1),
        new("currentTaskSlot", 0x001FC100u, 4),
        new("cdSectorsDone", 0x00155518u, 4),
        new("cdSectorsExpected", 0x0015551Cu, 4),
        new("cdStreamFlags", 0x0015552Bu, 1),
        new("playerState", 0x0015E23Du, 1),
        new("resetChordFrames", 0x0015E238u, 4),
        new("playerX", 0x0015E24Au, 2),
        new("playerY", 0x0015E24Eu, 2),
        new("playerLife", 0x0015E283u, 1),
        new("bolts", 0x0016D2F0u, 1),
        new("weapon", 0x0016DC08u, 1),
        new("cdStreamState", 0x001C335Cu, 4),
        new("cdBytesDone", 0x001C3364u, 4),
        new("level", 0x001C336Eu, 1),
        new("lives", 0x001C3370u, 1),
        new("checkpointHalf", 0x001C3374u, 1),
        new("sceneIndex", 0x001CF840u, 1),
        new("sceneSubstate", 0x001CF844u, 1),
        new("task0State", 0x001FC000u, 2),
        new("task1State", 0x001FC050u, 2),
        new("task2State", 0x001FC0A0u, 2),
        new("task3State", 0x001FC0F0u, 2),
        new("sceneState", 0x001D28F8u, 4)
    ];

    private static readonly CodeRange[] CodeRanges =
    [
        new("sdkCodeWindow", 0x000D7800u, 0xC00u),
        new("openingBackgroundRenderer", 0x000F98D8u, 0x120u)
    ];

    private static readonly Dictionary<uint, uint> LastValues = [];
    private static readonly Dictionary<uint, uint> LastCodeHashes = [];
    private static readonly HashSet<uint> VSyncCallers = [];
    private static bool _enabled;

    public static void Enable()
    {
        if (_enabled) return;
        _enabled = true;

        Event.AddListener<VSyncEvent>(OnVSync);
        Event.AddListener<OverlayLoadedEvent>(OnOverlayLoaded);
        Console.WriteLine($"[Bringup] monitor activado: {Values.Length} valores, {CodeRanges.Length} regiones de código");
    }

    private static void OnOverlayLoaded(OverlayLoadedEvent e)
    {
        Console.WriteLine($"[Bringup] overlay cargado: {e.Name}");
    }

    private static void OnVSync(VSyncEvent e)
    {
        var caller = e.Context.RA;
        if (VSyncCallers.Add(caller))
            Console.WriteLine($"[Bringup] nuevo llamador de VSync: 0x{caller:X8} (frame {e.Frame})");

        foreach (var value in Values)
        {
            var current = ReadValue(e.Memory, value);
            if (!LastValues.TryGetValue(value.Address, out var previous))
            {
                LastValues[value.Address] = current;
                Console.WriteLine(
                    $"[Bringup] estado inicial {value.Name} @ 0x{value.Address:X6} = 0x{current:X8}");
            }
            else if (current != previous)
            {
                LastValues[value.Address] = current;
                Console.WriteLine(
                    $"[Bringup] cambio frame {e.Frame}: {value.Name} @ 0x{value.Address:X6} 0x{previous:X8} -> 0x{current:X8}");
            }
        }

        foreach (var range in CodeRanges)
        {
            var current = HashRange(e.Memory, range.Address, range.Size);
            if (!LastCodeHashes.TryGetValue(range.Address, out var previous))
            {
                LastCodeHashes[range.Address] = current;
                Console.WriteLine(
                    $"[Bringup] firma inicial {range.Name} @ 0x{range.Address:X6}+0x{range.Size:X} = 0x{current:X8}");
            }
            else if (current != previous)
            {
                LastCodeHashes[range.Address] = current;
                Console.WriteLine(
                    $"[Bringup] código modificado frame {e.Frame}: {range.Name} 0x{previous:X8} -> 0x{current:X8}");
            }
        }

        if (e.Frame % 60 == 0)
        {
            var overlays = string.Join(',', Dispatcher.ActiveNames);
            Console.WriteLine(
                $"[Bringup] pulso frame {e.Frame}: caller=0x{caller:X8} overlays=[{overlays}]");
        }
    }

    private static uint ReadValue(IMemory memory, MemoryValue value)
    {
        return value.Size switch
        {
            1 => memory.ReadU8(value.Address),
            2 => memory.ReadU16(value.Address),
            4 => memory.ReadU32(value.Address),
            _ => throw new InvalidOperationException($"Tamaño de observación no soportado: {value.Size}")
        };
    }

    private static uint HashRange(IMemory memory, uint address, uint size)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;
        var hash = offsetBasis;

        for (uint i = 0; i < size; i++)
            hash = unchecked((hash ^ memory.ReadU8(address + i)) * prime);

        return hash;
    }
}
