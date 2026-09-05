using Recompiled;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Memory;

var bringupTrace = args.Any(arg => string.Equals(arg, "--bringup-trace", StringComparison.OrdinalIgnoreCase));
var sdkLog = args.Any(arg => string.Equals(arg, "--sdk-log", StringComparison.OrdinalIgnoreCase));
var cuePath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));

if (bringupTrace) BringupMonitor.Enable();
if (sdkLog)
{
    RecompOne.Runtime.Log.SdkOn = true;
    RecompOne.Runtime.Log.CdOn = true;
    Console.WriteLine("[Bringup] registros SDK y CD activados");
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
