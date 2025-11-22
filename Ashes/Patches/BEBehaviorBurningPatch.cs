using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Ashes;
[HarmonyPatch(typeof(BEBehaviorBurning), "KillFire")]
public class BEBehaviorBurningPatch
{
    public static void Postfix(BEBehaviorBurning __instance, bool consumeFuel)
    {
        if (!consumeFuel) return;

        // Short burns don't leave ashes.
        float burnDuration = __instance.startDuration;
        if (burnDuration < 10f) return;
        
        BlockPos fuelPos = __instance.FuelPos;
        IWorldAccessor world = __instance.Blockentity.Api.World;

        // Just to be safe, make sure there's nothing there. May not be necessary.
        Block currentBlock = world.BlockAccessor.GetBlock(fuelPos);
        if (currentBlock.BlockId != 0 && !currentBlock.Code.Path.Contains("fire")) return;

        // Longer burns = layerier ashes.
        int layers = GameMath.Clamp((int)Math.Ceiling(burnDuration / 25f), 1, 5);
        if (world.Rand.NextDouble() < 0.125) return;
        if (world.Rand.NextDouble() < 0.25) layers = (int)(layers * world.Rand.NextDouble());
        if (layers <= 0) return;
        
        Block ashBlock = world.GetBlock(new AssetLocation("smelltheashes", $"ashpile-{layers}"));
        if (ashBlock == null) return;
        
        world.BlockAccessor.SetBlock(ashBlock.BlockId, fuelPos);
                    
        ashBlock.OnBlockPlaced(world, fuelPos, null);

        world.BlockAccessor.TriggerNeighbourBlockUpdate(fuelPos);
    }
}
