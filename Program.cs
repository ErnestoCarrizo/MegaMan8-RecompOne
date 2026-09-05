using Recompiled;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Memory;

var cuePath = args.Length > 0 ? args[0] : null;

if (cuePath is not null)
{
    ConfigManager.Load();
    ConfigManager.Game.CdPath = Path.GetFullPath(cuePath);
    ConfigManager.SaveGame();
}

RecompOne.Runtime.Runtime.Run(
    () => Entry.Run(new PSMemory(), cuePath, "Mega Man 8 RecompOne"));

return 0;
