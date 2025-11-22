using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ashes;

public class BlockAsh : Block
{
    public Block GetNextLayer(IWorldAccessor world)
    {
        int.TryParse(Code.Path.Split('-')[1], out int layer);
        string basecode = CodeWithoutParts(1);

        if (layer < 8) return world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-" + (layer + 1)));
        return this; 
    }

    public Block GetPrevLayer(IWorldAccessor world)
    {
        int.TryParse(Code.Path.Split('-')[1], out int layer);
        string basecode = CodeWithoutParts(1);

        if (layer > 1) return world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-" + (layer - 1)));
        return null;
    }

    public int CountLayers()
    {
        int.TryParse(Code.Path.Split('-')[1], out int layer);
        return layer;
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            failureCode = "claimed";
            return false;
        }

        Block block = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite));

        if (block is BlockAsh)
        {
            int.TryParse(block.Code.Path.Split('-')[1], out int layer);

            if (layer < 8)
            {
                Block nextBlock = ((BlockAsh)block).GetNextLayer(world);
                world.BlockAccessor.SetBlock(nextBlock.BlockId, blockSel.Position.AddCopy(blockSel.Face.Opposite));
                return true;
            }
        }
        
        return base.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
    }

    public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
    {
        int layer = 0;
        int.TryParse(Code.Path.Split('-')[1], out layer);

        if (layer == 8 && blockFace == BlockFacing.UP) return true;

        return false;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        string basecode = CodeWithoutParts(1);
        Block block = world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-1"));
        return new ItemStack(block);
    }

    public override bool CanAcceptFallOnto(IWorldAccessor world, BlockPos pos, Block fallingBlock, TreeAttribute blockEntityAttributes)
    {
        if (fallingBlock is BlockAsh)
        {
            BlockAsh ourBlock = world.BlockAccessor.GetBlock(pos) as BlockAsh;
            return ourBlock != null && ourBlock.CountLayers() < 8;
        }

        return false;
    }

    public override bool OnFallOnto(IWorldAccessor world, BlockPos pos, Block block, TreeAttribute blockEntityAttributes)
    {
        BlockAsh nBlock = world.BlockAccessor.GetBlock(pos) as BlockAsh;
        BlockAsh uBlock = block as BlockAsh;
        
        if (uBlock != null && nBlock?.CountLayers() < 8)
        {
            // Add layers from falling block (uBlock) to target block (nBlock).
            // Why uBlock and nBlock? Got me. But that's how the game calls them. 
            while (nBlock.CountLayers() < 8 && uBlock != null)
            {
                nBlock = nBlock.GetNextLayer(world) as BlockAsh;
                uBlock = uBlock.GetPrevLayer(world) as BlockAsh;
            }

            world.BlockAccessor.SetBlock(nBlock.BlockId, pos);

            // If there are extra layers in the falling block that don't fit, place them above.
            if (uBlock != null)
            {
                BlockPos upos = pos.UpCopy();
                Block aboveBlock = world.BlockAccessor.GetBlock(upos);
                if (aboveBlock.BlockId == 0) 
                {
                    world.BlockAccessor.SetBlock(uBlock.BlockId, upos);
                }
            }

            world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
            return true;
        }

        return base.OnFallOnto(world, pos, block, blockEntityAttributes);
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        TryFalling(world, pos);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);
        TryFalling(world, blockPos);
    }

    private void TryFalling(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side != EnumAppSide.Server) return;
        
        Block blockBelow = world.BlockAccessor.GetBlock(pos.DownCopy());
        bool canFallInto = blockBelow.Replaceable > 6000 || 
                         (blockBelow is BlockAsh ashBelow && ashBelow.CountLayers() < 8);

        if (!canFallInto) return;
        
        ICoreServerAPI sapi = world.Api as ICoreServerAPI;

        // Create a safe copy for later.
        BlockPos myPos = pos.Copy();

        // Taking a page out of BehaviorUnstableFalling's book.
        sapi.Event.EnqueueMainThreadTask(() =>
        {
            Block currentBlock = world.BlockAccessor.GetBlock(myPos);
            if (currentBlock.BlockId != this.BlockId) return;
            
            Entity entity = world.GetNearestEntity(myPos.ToVec3d().Add(0.5, 0.5, 0.5), 1, 1.5f, (e) =>
            {
                return e is EntityBlockFalling ebf && ebf.initialPos.Equals(myPos);
            });
            if (entity != null) return;
            
            EntityBlockFalling entityBf = new EntityBlockFalling(
                currentBlock, 
                world.BlockAccessor.GetBlockEntity(myPos), 
                myPos, 
                currentBlock.Sounds.Break,
                0f,
                false,
                0.5f
            );
                
            world.SpawnEntity(entityBf);
        }, "ashfalling");
    }
}