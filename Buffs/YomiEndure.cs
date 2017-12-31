﻿using Terraria;
using Terraria.ModLoader;
using Terraria.Localization;

namespace WeaponOut.Buffs
{
    public class YomiEndure : ModBuff
    {
        public int yomiEndure;
        public override void SetDefaults()
        {
            DisplayName.SetDefault("Breakfall");
            DisplayName.AddTranslation(GameCulture.Chinese, "减弱力量");
            Description.SetDefault("Damage taken is reduced by $REDUCE%");
            Description.AddTranslation(GameCulture.Chinese, "减少$REDUCE%所承受的伤害");
        }
        public override void ModifyBuffTip(ref string tip, ref int rare)
        {
            tip = tip.Replace("$REDUCE", "" + yomiEndure);
        }

        public override void Update(Player player, ref int buffIndex)
        {
            PlayerFX pfx = player.GetModPlayer<PlayerFX>();
            if (player.itemAnimation != 0 || pfx.yomiEndurance <= 0)
            {
                pfx.yomiEndurance = 0;
                player.ClearBuff(Type); return;
            }

            yomiEndure = (int)(pfx.yomiEndurance * 100f);
            player.endurance += pfx.yomiEndurance;
            pfx.yomiEndurance = 0;
        }
    }
}
