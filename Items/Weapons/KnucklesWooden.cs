﻿using System.Collections.Generic;
using Microsoft.Xna.Framework;

using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using WeaponOut.Items.Weapons.UseStyles;

namespace WeaponOut.Items.Weapons
{
    public class KnucklesWooden : ModItem
    {
        public override bool Autoload(ref string name, ref string texture, IList<EquipType> equips)
        {
            return ModConf.enableFists;
        }

        private FistStyle fist;
        public FistStyle Fist
        {
            get
            {
                if (fist == null)
                {
                    fist = new FistStyle(item, 3);
                }
                return fist;
            }
        }
        public override void SetDefaults()
        {
            item.name = "Wooden Knuckles";
            item.toolTip = "<right> to parry attacks";
            item.useStyle = FistStyle.useStyle;
            item.useTurn = false;
            item.useAnimation = 25;//actually treated as -2
            item.useTime = 25;

            item.width = 28;
            item.height = 28;
            item.damage = 5;
            item.knockBack = 2f;
            item.UseSound = SoundID.Item7;

            item.noUseGraphic = true;
            item.melee = true;
        }
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Fist.ModifyTooltips(tooltips, mod);
        }
        public override void AddRecipes()
        {
            ModRecipe recipe = new ModRecipe(mod);
            recipe.AddIngredient(ItemID.Wood, 3);
            recipe.anyWood = true;
            recipe.AddTile(TileID.WorkBenches);
            recipe.SetResult(this);
            recipe.AddRecipe();
        }

        public override void HoldItem(Player player)
        {
            if(Fist.HoldItemOnParryFrame(player, mod, true))
            {
                FistStyle.provideImmunity(player, 60);
            }
        }

        public override bool AltFunctionUse(Player player)
        {
            return Fist.AtlFunctionParry(player, mod, 15, 25);
        }

        public override bool UseItemFrame(Player player)
        {
            Fist.UseItemFrame(player);
            return true;
        }
        public override void UseItemHitbox(Player player, ref Rectangle hitbox, ref bool noHitbox)
        {
            noHitbox = Fist.UseItemHitbox(player, ref hitbox, 20, 8f, 8f, 8f);
        }

        public override void OnHitNPC(Player player, NPC target, int damage, float knockBack, bool crit)
        {
            int combo = Fist.OnHitNPC(player, target, true);
        }
    }
}
