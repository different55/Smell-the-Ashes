using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace Ashes;
public class AshesSystem : ModSystem
{
    private static Harmony HarmonyInstance { get; set; }
    
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        HarmonyInstance = new Harmony(Mod.Info.ModID);
        HarmonyInstance.PatchAll();
        api.RegisterBlockClass("BlockAsh", typeof(BlockAsh));
    }
    
    public override void Dispose()
    {
        HarmonyInstance?.UnpatchAll(Mod.Info.ModID);
        HarmonyInstance = null;
        base.Dispose();
    }
}
