using Recompiled;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Memory;
using MegaMan8Recomp.Patches;

var bringupTrace = args.Any(arg => string.Equals(arg, "--bringup-trace", StringComparison.OrdinalIgnoreCase));
var sdkLog = args.Any(arg => string.Equals(arg, "--sdk-log", StringComparison.OrdinalIgnoreCase));
var videoLog = args.Any(arg => string.Equals(arg, "--video-log", StringComparison.OrdinalIgnoreCase));
var videoSnapshots = args.Any(arg => string.Equals(arg, "--video-snapshots", StringComparison.OrdinalIgnoreCase));
var autoProgress = args.Any(arg => string.Equals(arg, "--auto-progress", StringComparison.OrdinalIgnoreCase));
var cuePath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));

MegaMan8DisplayModePatches.Enable();
if (bringupTrace) BringupMonitor.Enable();
if (videoSnapshots) DisplaySnapshot.Enable(Path.Combine("logs", "video-snapshots"));
if (autoProgress) AutomatedInput.EnableTitleProgression();
if (sdkLog)
{
    RecompOne.Runtime.Log.SdkOn = true;
    RecompOne.Runtime.Log.CdOn = true;
    Console.WriteLine("[Bringup] registros SDK y CD activados");
}

if (videoLog)
{
    RecompOne.Runtime.Log.DmaOn = true;
    RecompOne.Runtime.Log.MdecOn = true;
    Console.WriteLine("[Bringup] registros DMA y MDEC activados");
}

if (cuePath is not null)
{
    ConfigManager.Load();
    ConfigManager.Game.CdPath = Path.GetFullPath(cuePath);
    ConfigManager.SaveGame();
}

RecompOne.Runtime.Runtime.Run(
    () => Entry.Run(new PSMemory(), cuePath, "Mega Man 8 RecompOne"));

return 0;
