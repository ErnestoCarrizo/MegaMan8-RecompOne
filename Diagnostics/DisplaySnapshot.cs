using RecompOne.Runtime.Events;

internal static class DisplaySnapshot
{
    private const int SnapshotInterval = 120;
    private const int MaximumSnapshots = 40;

    private static string _outputDirectory = "";
    private static int _saved;
    private static string _phase = "boot";
    private static int _phaseFrame;

    public static void Enable(string outputDirectory)
    {
        _outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(_outputDirectory);
        Event.AddListener<OverlayLoadedEvent>(OnOverlayLoaded);
        Event.AddListener<VSyncEvent>(OnVSync);
        Console.WriteLine($"[Snapshot] capturas de pantalla activadas en: {_outputDirectory}");
    }

    private static void OnVSync(VSyncEvent e)
    {
        _phaseFrame++;
        if (_saved >= MaximumSnapshots || _phaseFrame % SnapshotInterval != 0)
            return;

        var gpu = RecompOne.Runtime.Runtime.Gpu;
        if (gpu is null || !gpu.DisplayEnabled || gpu.DisplayWidth <= 0 || gpu.DisplayHeight <= 0)
            return;

        var path = Path.Combine(
            _outputDirectory,
            $"{_phase}-frame-{e.Frame:D5}-{gpu.DisplayWidth}x{gpu.DisplayHeight}.bmp");
        var nonBlackPixels = WriteBmp(path, gpu);
        _saved++;
        Console.WriteLine(
            $"[Snapshot] {_phase} frame {e.Frame}: {path} " +
            $"({nonBlackPixels:N0} píxeles no negros, rgb24={gpu.Display24Bit})");
    }

    private static void OnOverlayLoaded(OverlayLoadedEvent e)
    {
        _phase = Sanitize(e.Name);
        _phaseFrame = 0;
        _saved = 0;
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        return new string(chars);
    }

    private static int WriteBmp(string path, RecompOne.Runtime.Gpu gpu)
    {
        var width = gpu.DisplayWidth;
        var height = gpu.DisplayHeight;
        var rowBytes = width * 3;
        var stride = (rowBytes + 3) & ~3;
        var imageBytes = stride * height;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x4D42);
        writer.Write(54 + imageBytes);
        writer.Write(0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(-height); // BMP de arriba hacia abajo.
        writer.Write((ushort)1);
        writer.Write((ushort)24);
        writer.Write(0);
        writer.Write(imageBytes);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        var padding = stride - rowBytes;
        var nonBlackPixels = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ReadRgb(gpu, x, y, out var r, out var g, out var b);
                if ((r | g | b) != 0) nonBlackPixels++;
                writer.Write(b);
                writer.Write(g);
                writer.Write(r);
            }

            for (var i = 0; i < padding; i++) writer.Write((byte)0);
        }

        return nonBlackPixels;
    }

    private static void ReadRgb(RecompOne.Runtime.Gpu gpu, int x, int y, out byte r, out byte g, out byte b)
    {
        var vram = gpu.Vram;
        if (gpu.Display24Bit)
        {
            var byteOffset = ((gpu.DisplayY + y) * RecompOne.Runtime.Gpu.VramWidth + gpu.DisplayX) * 2 + x * 3;
            r = ReadVramByte(vram, byteOffset);
            g = ReadVramByte(vram, byteOffset + 1);
            b = ReadVramByte(vram, byteOffset + 2);
            return;
        }

        var row = ((gpu.DisplayY + y) & (RecompOne.Runtime.Gpu.VramHeight - 1)) * RecompOne.Runtime.Gpu.VramWidth;
        var pixel = vram[row + ((gpu.DisplayX + x) & (RecompOne.Runtime.Gpu.VramWidth - 1))];
        r = (byte)((pixel & 0x1F) << 3);
        g = (byte)(((pixel >> 5) & 0x1F) << 3);
        b = (byte)(((pixel >> 10) & 0x1F) << 3);
    }

    private static byte ReadVramByte(ushort[] vram, int byteOffset)
    {
        var halfword = (byteOffset >> 1) & (RecompOne.Runtime.Gpu.VramWidth * RecompOne.Runtime.Gpu.VramHeight - 1);
        var value = vram[halfword];
        return (byte)((byteOffset & 1) == 0 ? value & 0xFF : value >> 8);
    }
}
