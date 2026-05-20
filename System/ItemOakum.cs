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
                ConsumeIngredientFromSlots(inSlots, recipe, createValue);
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
                }

                ConsumeIngredientFromSlots(inSlots, recipe, repairValue);
                return true;
            }

            return false;
        }

        // Pick the largest single matching stack per ingredient. Smaller stacks of the same
        // ingredient in other slots are ignored (not credited, not consumed) so spreading fibers
        // across slots is harmless rather than wasteful.
        private void CalculateCreateValue(ItemSlot[] inSlots, GridRecipe recipe, out int createValue)
        {
            createValue = int.MaxValue;

            foreach (var ingredient in recipe.ResolvedIngredients)
            {
                if (ingredient?.ResolvedItemStack == null) continue;
                // For the repair recipe one of the ingredients is the oakum itself; that slot is
                // skipped on the input side, so the ingredient must be skipped here too — otherwise
                // its forThisIngredient would clamp createValue to 0 and the repair would always
                // produce a single durability point regardless of how many fibers were provided.
                if (ingredient.ResolvedItemStack.Collectible == this) continue;

                int best = 0;
                foreach (var slot in inSlots)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Collectible == this) continue;
                    if (SlotMatches(slot, ingredient) && slot.StackSize > best) best = slot.StackSize;
                }

                int qty = Math.Max(1, ingredient.Quantity);
                int forThisIngredient = best / qty;
                if (forThisIngredient < createValue) createValue = forThisIngredient;
            }

            if (createValue == int.MaxValue || createValue <= 0) createValue = 1;
            if (Core.Divider > 0 && Core.Divider < createValue) createValue = Core.Divider;
        }

        // Consume from the single largest matching slot per ingredient. Other slots that also
        // contain the ingredient are left alone. The oakum ingredient (for repair) is handled
        // separately by ConsumeCraftingIngredients clearing the oakum input slot directly.
        private void ConsumeIngredientFromSlots(ItemSlot[] inSlots, GridRecipe recipe, int createValue)
        {
            foreach (var ingredient in recipe.ResolvedIngredients)
            {
                if (ingredient?.ResolvedItemStack == null) continue;
                if (ingredient.ResolvedItemStack.Collectible == this) continue;

                ItemSlot? bestSlot = null;
                foreach (var slot in inSlots)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Collectible == this) continue;
                    if (!SlotMatches(slot, ingredient)) continue;
                    if (bestSlot == null || slot.StackSize > bestSlot.StackSize) bestSlot = slot;
                }

                if (bestSlot == null) continue;
                int take = Math.Min(createValue * Math.Max(1, ingredient.Quantity), bestSlot.StackSize);
                bestSlot.TakeOut(take);
                bestSlot.MarkDirty();
            }
        }

        private static bool SlotMatches(ItemSlot slot, CraftingRecipeIngredient ingredient)
        {
            var slotStack = slot.Itemstack;
            var ingStack = ingredient.ResolvedItemStack;
            return slotStack.Class == ingStack.Class && slotStack.Collectible.Id == ingStack.Collectible.Id;
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
