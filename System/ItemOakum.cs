using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace HeatRetention
{
    public class ItemOakum : Item
    {

        public override void OnLoaded(ICoreAPI api)
        {
            this.Durability = ModConfigFile.Current.OakumDurability;
            base.OnLoaded(api);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel,
            bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && api.World.BlockAccessor.GetBlock(blockSel.Position) is BlockChisel &&
                api.World.BlockAccessor.GetBlockEntity(blockSel.Position)
                .GetBehavior<BlockEntityBehaviorHeatRetention>().IsActivate())
            {
                if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    DamageItem(api.World, byEntity, slot, ModConfigFile.Current.CostPerBlock);
                }
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override List<ItemStack> GetHandBookStacks(ICoreClientAPI capi)
        {

            return base.GetHandBookStacks(capi);
        }

        public override void OnCreatedByCrafting(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            base.OnCreatedByCrafting(inSlots, outputSlot, byRecipe);

            // Prevent derp in the handbook
            if (outputSlot is DummySlot) return;
            if (byRecipe is not GridRecipe recipe) return;

            if (IsCreate(recipe))
            {
                CalculateCreateValue(inSlots, recipe, out int createValue);

                int maxDur = GetMaxDurability(outputSlot.Itemstack);

                outputSlot.Itemstack.Attributes.SetInt("durability", maxDur / Core.Divider * createValue);

            }

            if (IsRepair(recipe))
            {
                int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(outputSlot.Itemstack);
                int maxDur = GetMaxDurability(outputSlot.Itemstack);

                CalculateCreateValue(inSlots, recipe, out int createValue);

                createValue = (int)Math.Min((1 - (float)curDur /maxDur) * Core.Divider, createValue);

                outputSlot.Itemstack.Attributes.SetInt("durability", Math.Min(maxDur, (int)(curDur + maxDur / Core.Divider * createValue)));

            }
        }

        public override bool ConsumeCraftingIngredients(ItemSlot[] inSlots, ItemSlot outputSlot, IRecipeBase matchingRecipe)
        {
            if (matchingRecipe is not GridRecipe recipe) return false;
            if (IsCreate(recipe))
            {
                CalculateCreateValue(inSlots, recipe, out int createValue);

                foreach (var slot in inSlots)
                {
                    if (slot.Itemstack == null) continue;

                    var hash = slot.Itemstack.GetHashCode();

                    foreach (var ingredient in recipe.ResolvedIngredients)
                    {
                        if (ingredient.ResolvedItemStack.Id != hash) continue;
                        slot.TakeOut(createValue * ingredient.Quantity);
                    }
                }

                return true;
            }

            // Consume as much materials in the input grid as needed
            if (IsRepair(recipe))
            {
                CalculateRepairValue(inSlots, outputSlot, recipe, out int repairValue);

                foreach (var slot in inSlots)
                {
                    if (slot.Itemstack == null) continue;

                    if (slot.Itemstack.Collectible == this) { slot.Itemstack = null; continue; }

                    var hash = slot.Itemstack.GetHashCode();

                    foreach (var ingredient in recipe.ResolvedIngredients)
                    {
                        if (ingredient.ResolvedItemStack.Id != hash) continue;
                        slot.TakeOut(repairValue * ingredient.Quantity);
                    }
                }

                return true;
            }

            return false;
        }

        private void CalculateCreateValue(ItemSlot[] inSlots, GridRecipe recipe, out int createValue)
        {
            createValue = int.MaxValue;


            foreach (var slot in inSlots)
            {
                if (slot.Empty) continue;

                if (slot.Itemstack.Collectible == this) continue;

                var hash = slot.Itemstack.GetHashCode();
                foreach (var ingredient in recipe.ResolvedIngredients)
                {
                    if (ingredient.ResolvedItemStack.Id != hash) continue;
                    var _ = slot.StackSize / ingredient.Quantity;
                    if (_ < createValue)
                    {
                        createValue = _;
                    }
                }
            }

            if (createValue == int.MaxValue)
            {
                createValue = 1;
            }
            if (Core.Divider < createValue) { createValue = Core.Divider; }

        }

        private void CalculateRepairValue(ItemSlot[] inSlots, ItemSlot outputSlot, GridRecipe recipe, out int repairValue)
        {
            CalculateCreateValue(inSlots, recipe, out int createValue);

            var oakumSlot = inSlots.FirstOrDefault(slot => slot.Itemstack?.Collectible is ItemOakum);
            int curDur = outputSlot.Itemstack.Collectible.GetRemainingDurability(oakumSlot?.Itemstack);
            int maxDur = GetMaxDurability(outputSlot.Itemstack);

            repairValue = (int)Math.Min((1 - (float)curDur / maxDur) * Core.Divider, createValue);
        }

        private static bool IsRepair(GridRecipe recipe)
        {
            return recipe.Name.ToString() == ($"{Core.ModId}:repair");
        }

        private static bool IsCreate(GridRecipe recipe)
        {
            return recipe.Name.ToString() == ($"{Core.ModId}:oakum");
        }
    }
}
