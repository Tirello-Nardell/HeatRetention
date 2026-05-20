using HeatRetention.Extensions;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace HeatRetention
{
    [ProtoContract]
    public class ModConfigFile
    {
        public static ModConfigFile Current { get; set; } = null!;
        [ProtoMember(1)] public int OakumDurability { get; set; } = 64;
        [ProtoMember(2)] public int CostPerBlock { get; set; } = 1;
    }

    public class Core : ModSystem
    {
        public static string? ModId { get; private set; }
        public static int Divider { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            ModConfigFile.Current = api.LoadOrCreateConfig<ModConfigFile>("heatretention.json");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            var channel = api.Network.RegisterChannel("heatretention")
               .RegisterMessageType<ModConfigFile>();
            api.Event.PlayerJoin += byPlayer => { channel.SendPacket(ModConfigFile.Current, byPlayer); };
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.RegisterChannel("heatretention")
                .RegisterMessageType<ModConfigFile>()
                .SetMessageHandler<ModConfigFile>(packet =>
                {
                    ModConfigFile.Current = packet;
                });
        }

        public override void Start(ICoreAPI api)
        {
            ModId = Mod.Info.ModID;

            api.RegisterItemClass($"{ModId}:ItemOakum", typeof(ItemOakum));

            api.RegisterBlockBehaviorClass($"{ModId}:BlockHeatRetention", typeof(BlockBehaviorHeatRetention));

            api.RegisterBlockEntityBehaviorClass($"{ModId}:HeatRetention", typeof(BlockEntityBehaviorHeatRetention));

            api.Event.MatchesGridRecipe += OnMatchesGridRecipe;
        }

        // Reject the oakum craft and repair recipes if the player has spread the non-oakum
        // ingredient across multiple slots. Forces a single-slot stack and removes the ambiguity
        // about which slot "counts" toward the result.
        private bool OnMatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
        {
            var recipeName = recipe.Name?.ToString();
            if (recipeName != $"{ModId}:oakum" && recipeName != $"{ModId}:repair") return true;

            int nonOakumSlots = 0;
            foreach (var slot in ingredients)
            {
                if (slot.Empty) continue;
                if (slot.Itemstack?.Collectible is ItemOakum) continue;
                nonOakumSlots++;
                if (nonOakumSlots > 1) return false;
            }
            return true;
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Server)
            {
                var recipe = CraftRecipe(api);
                RepairRecipe(api, recipe);
            }

            foreach (var block in api.World.Blocks)
            {
                if (block.FirstCodePart() == "chiseledblock")
                {
                    BlockBehaviorHeatRetention blockBehavior = new(block);
                    blockBehavior.OnLoaded(api);
                    block.BlockBehaviors = block.BlockBehaviors.Append(blockBehavior);

                    if (api.Side == EnumAppSide.Server)
                    {
                        block.BlockEntityBehaviors = block.BlockEntityBehaviors.Append(
                            new BlockEntityBehaviorType() { Name = $"{ModId}:HeatRetention", properties = null });
                    }
                }
            }
        }

        private static GridRecipe CraftRecipe(ICoreAPI api)
        {
            foreach (var grecipe in api.World.GridRecipes)
            {
                if (grecipe.Name?.ToString() != $"{ModId}:oakum") continue;

                var resolved = grecipe.ResolvedIngredients;
                if (resolved == null) continue;

                if (resolved.Length > 1)
                {
                    HashSet<int> quantities = new();
                    foreach (var ingredient in resolved)
                    {
                        if (ingredient != null) quantities.Add(ingredient.Quantity);
                    }

                    Divider = GCD(quantities.ToArray());

                    foreach (var ingredient in resolved)
                    {
                        if (ingredient?.ResolvedItemStack == null) continue;
                        ingredient.ResolvedItemStack.StackSize = ingredient.Quantity /= Divider;
                    }
                }
                else if (resolved.Length == 1 && resolved[0]?.ResolvedItemStack != null)
                {
                    var first = resolved[0]!;
                    Divider = first.Quantity;
                    first.ResolvedItemStack!.StackSize = first.Quantity = 1;
                }
                return grecipe;
            }
            throw new System.NotSupportedException($"[{ModId}] Craft recipe not found");
        }

        // The 1.20.4+ API nulls out GridRecipe.Ingredients on the server after recipe resolving,
        // so we use ResolvedIngredients here. Both views reference the same CraftingRecipeIngredient
        // instances, so updating the array updates what the matcher sees too.
        private static void RepairRecipe(ICoreAPI api, GridRecipe currentCraftRecipe)
        {
            var craftIngredients = currentCraftRecipe.ResolvedIngredients;
            if (craftIngredients == null) return;

            foreach (var grecipe in api.World.GridRecipes)
            {
                if (grecipe.Name?.ToString() != $"{ModId}:repair") continue;
                if (grecipe.ResolvedIngredients == null) continue;

                foreach (var ingredient in grecipe.ResolvedIngredients)
                {
                    if (ingredient?.ResolvedItemStack == null) continue;
                    if (ingredient.Code?.ToString() == $"{ModId}:oakum") continue;

                    int hash = ingredient.ResolvedItemStack.Id;
                    foreach (var ing in craftIngredients)
                    {
                        if (ing?.ResolvedItemStack == null) continue;
                        if (ing.ResolvedItemStack.Id != hash) continue;
                        ingredient.ResolvedItemStack.StackSize = ingredient.Quantity = ing.Quantity;
                    }
                }
            }
        }

        private static int GCD(int[] numbers)
        {
            if (numbers.Length < 2)
            {
                return 0;
            }

            int result = numbers[0];
            for (int i = 1; i < numbers.Length; i++)
            {
                result = GCD(result, numbers[i]);
            }
            return result;
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }
}
