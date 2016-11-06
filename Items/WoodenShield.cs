﻿using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace WeaponOut.Items
{
	public class WoodenShield : ModItem
    {
        public override bool Autoload(ref string name, ref string texture, IList<EquipType> equips)
        {
            equips.Add(EquipType.Shield);
            return true;
        }
        public override void SetDefaults()
        {
            item.name = "Round Shield";
            item.width = 24;
            item.height = 28;
            item.toolTip = "Grants 10 damage knockback immunity"; //see playerfx
            item.accessory = true;
            item.defense = 1;
            item.value = Item.sellPrice(0,0,15,0);
        }

        public override void AddRecipes()
        {
            ModRecipe recipe = new ModRecipe(mod);
            recipe.AddIngredient(ItemID.IronBar, 5);
            recipe.anyIronBar = true;
            recipe.AddIngredient(ItemID.Wood, 10);
            recipe.anyWood = true;
            recipe.AddTile(TileID.WorkBenches);
            recipe.SetResult(this);
            recipe.AddRecipe();
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            PlayerFX modPlayer = player.GetModPlayer<PlayerFX>(mod);
            modPlayer.DamageKnockbackThreshold = 10;
        }
    }
}
