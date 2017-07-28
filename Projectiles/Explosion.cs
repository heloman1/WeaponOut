﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace WeaponOut.Projectiles
{
    /// <summary>
    /// ai[0] = charge
    /// ai[1] = explosion state (charging, released, exploding
    /// localai[0] = charging
    /// localai[1] = basedmg
    /// damage * (charge ^ 2) x35-870-3500
    /// log (charge * 10) x1-1.84-2.17
    /// 
    /// Possibly increase size by projectile velocity
    /// </summary>
    public class Explosion : ModProjectile
    {

        public static Texture2D textureTargetS;
        public static Texture2D textureTargetM;
        public static Texture2D textureTargetL;
        public static Texture2D textureTargetXL;
        public static Texture2D textureLaser;

        public const int chargeTicksIdle = 3; //bonus ticks for standing still
        public const int chargeTicksMax = 48 * (1 + chargeTicksIdle); //3 ticks per update
        public const float chargeTickGameMax = (chargeTicksMax / chargeTicksIdle);
        public const int castTicksTime = 5; //time per charge spent casting
        public const int fireTicksTime = 3; //time per charge spent exploding (penetrating projs gives npcs 10tick immune)
        public const float manaIncrease = 0.5f;//additive mana multiplier per increase
        public const int manaMaintainCost = 3;//cost per tick to maintain full charge
        public const float explosionScale = 1.15f; //width * this^15
        public const float farDistance = 2000;
        public const float maxDistance = 2500;
        //10, 15, 20, 25 etc.

        public float[] textureSizes = new float[maxCharge];
        public float[] textureAlphas = new float[maxCharge];
        public float[] textureCastAlphas = new float[2];
        public float centreY;
        public Vector2 GraphicCentre
        {
            get
            {
                return new Vector2(projectile.Center.X, centreY);
            }
        }

        private int ChargeLevel { get { return (int)projectile.ai[0]; } set { projectile.ai[0] = value; } }
        private int ExplosionState { get { return (int)projectile.ai[1]; } set { projectile.ai[1] = value; } }
        private int ChargeTick { get { return (int)projectile.localAI[0]; } set { projectile.localAI[0] = value; } }
        private int Damage { get { return (int)projectile.localAI[1]; } set { projectile.localAI[1] = value; } }
        private int manaCost { get { return (int)(Main.player[projectile.owner].inventory[Main.player[projectile.owner].selectedItem].mana * (1f + (ChargeLevel + 1) / 2f)); } }

        public const int maxCharge = 15;
        //Titanium/Hallowed Headgear, Diamond Robe, 
        //Crystal Ball Buff, Arcane Band of Starpower, Arcane Mana Regeneration Band, 
        //Arcane Magic Cuffs, three Arcane Accessories (one extra with Demon Heart)
        //For 600 mana, with max reduced mana mana cost from 10 -> 8 via Masterful modifier
        //sum 8 * (1+k/2), k=0 to n
        //chg 1, 2   3   4   5   6    7    8    9    10   11   12   13   14   15   16
        //    8, 20, 36, 56, 80, 108, 140, 176, 216, 260, 308, 360, 416, 476, 540, 608
        //NOTE: it seems 400 is maximum mana allowed, Must consume a mana pot at tier 6 to cast full power
        //

        private Vector2 zoomDiff = new Vector2();

        public override void SetStaticDefaults()
        {
            DisplayName.SetDefault("Explosion Charge");
        }
        public override void SetDefaults()
        {
            projectile.width = 100;
            projectile.height = 100;

            projectile.timeLeft = 3600;
            projectile.penetrate = -1;

            //projectile.friendly = true;
            projectile.magic = true;
            projectile.tileCollide = false;
            projectile.ignoreWater = true;

            projectile.netImportant = true;
        }

        public override void AI()
        {
            Player player = Main.player[projectile.owner];
            player.heldProj = projectile.whoAmI; //set to this

            projectile.velocity = Vector2.Zero;
            centreY = (centreY * chargeTickGameMax + projectile.Center.Y) / (chargeTickGameMax + 1);
            if (ExplosionState == 0f)
            {
                chargeState(player);
            }
            else if (ExplosionState == 1f)
            {
                castState(player);
            }
            else if (ExplosionState == 2f)
            {
                fireState(player);
            }
        }

        public override bool? CanCutTiles()
        {
            return false;
        }

        #region Behaviours

        private static bool canChannel(Player player)
        {
            return !player.dead && !player.frozen && !player.stoned && !player.webbed && !player.tongued;
        }
        /// <summary>
        /// Does all the "is holding and facing this projectile" things
        /// </summary>
        /// <param name="player"></param>
        private void playerFaceProjectileChannel(Player player)
        {
            if (projectile.damage != 0) //first tiome
            {
                Damage = (projectile.damage + Items.Weapons.Basic.StaffOfExplosion.baseDamage * 5) / 6;
                projectile.damage = 0;
                centreY = projectile.Center.Y;

                //-//Main.NewText("Damage: \n" + Damage);
            }

            Vector2 vectorDiff = player.Center - projectile.Center;
            if (vectorDiff.X < 0f)
            {
                player.ChangeDir(1);
                projectile.direction = 1;
            }
            else
            {
                player.ChangeDir(-1);
                projectile.direction = -1;
            }

            //stop generating new projectiles
            player.itemAnimation = player.itemAnimationMax;
            player.itemTime = player.itemAnimationMax;

            //point at projectile
            player.itemRotation = (vectorDiff * -1f * (float)projectile.direction).ToRotation();
            projectile.spriteDirection = ((vectorDiff.X > 0f) ? -1 : 1);
        }



        private void chargeState(Player player)
        {
            Vector2 staffTip = player.MountedCenter + new Vector2(
                player.inventory[player.selectedItem].width * 1.1f * (float)Math.Cos(player.itemRotation),
                player.inventory[player.selectedItem].width * 1.1f * (float)Math.Sin(player.itemRotation)
            ) * player.direction;
            int d = Dust.NewDust(staffTip - new Vector2(3 - player.direction * 2 + ChargeLevel * 0.05f, 3 + ChargeLevel * 0.05f),
                0, 0, 174, 0, 0, 0, Color.White, 0.3f + (0.1f * ChargeLevel));
            Main.dust[d].noGravity = true;
            Main.dust[d].velocity *= 0.3f;

            player.aggro += 900;//same as beetle armour

            //can't instantly use
            bool littleCharge = ChargeLevel == 0 && ChargeTick < chargeTicksMax / 2;

            float distance = Vector2.Distance(player.Center, projectile.Center);
            //Main.NewText("distance: \n" + distance + "/\n" + maxDistance);

            //cancelled if no longer channelling or can't act, or simply too far
            if ((player.channel || littleCharge)
                && canChannel(player) && distance <= maxDistance)
            {
                chargeTickLogic(player, ref staffTip, ref d, distance);
                chargeLevelLogic(player);

            }
            else
            {
                changeToCast();
            }
        }

        private void changeToCast()
        {
            ExplosionState = 1;
            projectile.netUpdate = true;
            //-//Main.NewText("Changing to CastState");
        }
        private void chargeTickLogic(Player player, ref Vector2 staffTip, ref int d, float distance)
        {
            //hold player usage
            playerFaceProjectileChannel(player);
            ChargeTick += 1; //increase charge a bit
            if (player.velocity.X == 0 && player.velocity.Y == 0 && distance <= farDistance)
            {
                ChargeTick += chargeTicksIdle; //increase charge

                //extra dust to indicate charging faster
                d = Dust.NewDust(staffTip - new Vector2(3 - player.direction * 2 + ChargeLevel * 0.05f, 3 + ChargeLevel * 0.05f),
                    0, 0, 174, 0, 0, 0, Color.White, 0.5f + (0.1f * ChargeLevel));
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.7f;

                //dust fly into staff
                Vector2 circleVector;
                float randAng = Main.rand.Next(-31416, 31417) * 0.0001f;
                circleVector = new Vector2(
                    ChargeLevel * 8 * (float)Math.Cos(randAng),
                    ChargeLevel * 8 * (float)Math.Sin(randAng)
                    );
                //dust spawn at cricle and move in
                d = Dust.NewDust(staffTip + circleVector, 0, 0,
                    106, circleVector.X, circleVector.Y, 0, Color.White, 0.1f);
                Main.dust[d].fadeIn = 0.8f;
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= -0.1f;

                chargeFX(true);
            }
            else
            {
                chargeFX(false);
            }
        }
        private void chargeLevelLogic(Player player)
        {
            if (ChargeLevel < maxCharge) //under full charge
            {
                if (ChargeTick >= chargeTicksMax) //past level tick
                {
                    ChargeTick = 0;//past, reset tick
                    //use manaflower
                    if (manaCost > player.statMana)
                    {
                        if (player.manaFlower)
                        {
                            player.QuickMana();
                        }
                        else
                        {
                            changeToCast();
                        }
                    }
                    //now has mana available?
                    if (player.statMana >= manaCost)
                    {
                        ChargeLevel++;
                        projectile.timeLeft += chargeTicksMax;

                        float recentre = projectile.width;
                        projectile.Size *= explosionScale;
                        recentre = (projectile.width - recentre) / 2f;
                        projectile.Center -= new Vector2(recentre, recentre * 1.75f);
                        textureSizes[ChargeLevel - 1] = projectile.width;

                        player.CheckMana(manaCost, true);
                        //-//Main.NewText("Increase... tier \n" + ChargeLevel + " | current manacost: \n" + manaCost + "| size: \n" + textureSizes[Math.Min(maxCharge - 1, ChargeLevel)]);

                        chargeNext();
                    }
                }
            }
            else //already fully charged
            {
                ChargeTick = 0;
                if (manaMaintainCost > player.statMana)
                {
                    if (player.manaFlower)
                    {
                        player.QuickMana();
                    }
                    else
                    {
                        changeToCast();
                    }
                }
                //now has mana available?
                if (player.statMana >= manaMaintainCost)
                {
                    player.CheckMana(manaMaintainCost, true);
                }
            }
        }

        
        private bool start = true;
        private int chargeTime;
        private void castState(Player player)
        {
            chargeTime = (2 + ChargeLevel) * castTicksTime;
            if (start) { ChargeTick = chargeTime; castStart(); start = false; }

            //release at lower power if can't act
            if (canChannel(player))
            {
                player.itemRotation = -1.5708f * player.direction;
            }
            else
            {
                //-//Main.NewText("Changing to weak FireState");
                ChargeLevel /= 2;
                ExplosionState = 2;
                projectile.netUpdate = true;
            }

            if (ChargeTick > 0)
            {
                ChargeTick--;

                //player stays still to cast
                player.itemAnimation = player.itemAnimationMax / 2;
                player.itemTime = player.itemAnimationMax / 2;
                player.noKnockback = true;
                player.velocity /= 2f;

                // -- casting code --
                
                Vector2 staffTip = player.MountedCenter + new Vector2(
                    player.inventory[player.selectedItem].width * 1.1f * (float)Math.Cos(player.itemRotation),
                    player.inventory[player.selectedItem].width * 1.1f * (float)Math.Sin(player.itemRotation)
                ) * player.direction;

                /*
                ////-//Main.NewText("casting... \n" + ((float)ChargeTick / castTicksTime));
                int d = Dust.NewDust(staffTip - new Vector2(3 - player.direction * 2, 3),
                    0, 0, 130, 0, 0, 0, Color.White, 0.5f + (0.1f * ((float)ChargeTick / castTicksTime)));
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = new Vector2(Main.dust[d].velocity.X * 0.1f, -5f + Main.dust[d].velocity.Y - ChargeLevel * 0.1f);
                 */
            }
            else
            {
                //-//Main.NewText("Changing to FireState");
                ExplosionState = 2;
                projectile.netUpdate = true;
            }
        }
        
        private void fireState(Player player)
        {
            if (ChargeTick >= 0)
            {
                ChargeTick = -1;
                projectile.timeLeft = Explosion.fireTicksTime * (1 + ChargeLevel);

                if (projectile.owner == Main.myPlayer)
                {
                    if (ChargeLevel > 0)
                    {
                        //goes up to x1000 damage
                        Damage = Damage + (int)(Damage * Math.Pow(2, ChargeLevel * 10f / Explosion.maxCharge));
                        projectile.knockBack *= 1 + (int)Math.Log10(ChargeLevel * 10);
                    }

                    //damage is reduced over a period of time due to fire time
                    //in this case, since it hits every 10 ticks
                    int divDamage = Damage / (1 + (projectile.timeLeft / 10));
                    projectile.damage = divDamage;

                    //-//Main.NewText("lifetime: \n" + projectile.timeLeft + " | dmg: \n" + divDamage + "/\n" + Damage);

                    Projectile.NewProjectile(projectile.Center, projectile.velocity,
                        mod.GetProjectile("Explooosion").projectile.type,
                        Damage,
                        projectile.knockBack,
                        projectile.owner,
                        projectile.ai[0],
                        projectile.ai[1]);
                }
            }

            projectile.scale += (Explosion.explosionScale - 1) / Explosion.fireTicksTime;
        }

        #endregion

        #region Visuaudio
        public void chargeFX(bool notMoving)
        {
            projectile.frameCounter++;
            if (projectile.frameCounter > (8 + maxCharge - ChargeLevel))
            {
                projectile.frameCounter = 0;
                Main.PlaySound(2, projectile.Center, 34);
            }
        }
        public void chargeNext()
        {
            Player player = Main.player[projectile.owner];
            if (Main.myPlayer == projectile.owner)
            {
                Main.PlaySound(25, player.position);
            }
        }

        public void castStart()
        {
            Player player = Main.player[projectile.owner];
            Main.PlaySound(2, player.position, 72);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Color lightColor)
        {
            drawChargeCircles(spriteBatch);

            drawCastingCircles(spriteBatch);

            return false;
        }
        
        private void drawChargeCircles(SpriteBatch spriteBatch)
        {
            Vector2 circleVector;

            //first circle dust
            for (int i = 0; i < 2; i++)
            {
                float randAng = Main.rand.Next(-31416, 31417) * 0.0001f;
                circleVector = new Vector2(
                    50 * (float)Math.Cos(randAng) - 4,
                    50 * (float)Math.Sin(randAng) - 4
                    );
                //still dust
                int d = Dust.NewDust(GraphicCentre + circleVector, 0, 0, 174, 0, 0, 0, Color.White, 0.1f);
                Main.dust[d].fadeIn = 0.4f;
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.25f;
                //dust moving out
                d = Dust.NewDust(GraphicCentre, 0, 0, 183, circleVector.X / 30, circleVector.Y / 30, 0, Color.White, 0.1f);
                Main.dust[d].fadeIn = 0.8f;
                Main.dust[d].noGravity = true;
            }

            ////-//Main.NewText("draw \n" + i + " with alpha \n" + alpha);
            float size;
            float alpha;
            Vector2 castCentre;
            float sizeCircle;
            float sizeOffset;
            float angle;
            for (int i = 0; i < Math.Min(textureSizes.Length, ChargeLevel); i++)
            {
                size = textureSizes[i];
                alpha = textureAlphas[i];
                if (ExplosionState < 2)
                {
                    if (alpha < 1f) { alpha += 0.02f; } else { alpha = 1f; }
                }
                else
                {
                    if (alpha > 0f) { alpha -= 0.03f; } else { alpha = 0; }
                }
                switch (i)
                {
                    case 0: ///////////////////////////////////////////////////////////////////// initial circle
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetM,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.05f),
                            textureTargetM.Bounds.Center.ToVector2(),
                            size * projectile.scale / textureTargetM.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 1: ///////////////////////////////////////////////////////////////////// flat centre
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetS,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetS.Bounds.Center.ToVector2(),
                            new Vector2(size, size / 3) * projectile.scale / textureTargetS.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 2: ///////////////////////////////////////////////////////////////////// initial circle inner
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetS,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.05f),
                            textureTargetS.Bounds.Center.ToVector2(),
                            textureSizes[0] * projectile.scale / textureTargetS.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 3: ///////////////////////////////////////////////////////////////////// backspin circle
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetS,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.08f),
                            textureTargetS.Bounds.Center.ToVector2(),
                            new Vector2(size, size / 3) * projectile.scale / textureTargetS.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 4: ///////////////////////////////////////////////////////////////////// orbiter circle 1
                        sizeCircle = size * (2 / 5f);
                        sizeOffset = size * (3 / 5f);
                        angle = (float)(Main.time % 62831) * -0.02f;
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            sizeOffset * 0.5f * (float)Math.Cos(angle),
                            sizeOffset * 0.5f * (float)Math.Sin(angle)
                        );

                        spriteBatch.Draw(textureTargetM,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.03f),
                            textureTargetM.Bounds.Center.ToVector2(),
                            new Vector2(sizeCircle, sizeCircle) * projectile.scale / textureTargetM.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 5: ///////////////////////////////////////////////////////////////////// orbiter circle 2
                        sizeCircle = size * (2 / 5f);
                        sizeOffset = size * (3 / 5f);
                        angle = (float)((Main.time + 20943) % 62831) * -0.02f;
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            sizeOffset * 0.5f * (float)Math.Cos(angle),
                            sizeOffset * 0.5f * (float)Math.Sin(angle)
                        );

                        spriteBatch.Draw(textureTargetM,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.02f),
                            textureTargetM.Bounds.Center.ToVector2(),
                            new Vector2(sizeCircle, sizeCircle) * projectile.scale / textureTargetM.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 6: ///////////////////////////////////////////////////////////////////// orbiter circle 3
                        sizeCircle = size * (2 / 5f);
                        sizeOffset = size * (3 / 5f);
                        angle = (float)((Main.time - 20943) % 62831) * -0.02f;
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            sizeOffset * 0.5f * (float)Math.Cos(angle),
                            sizeOffset * 0.5f * (float)Math.Sin(angle)
                        );

                        spriteBatch.Draw(textureTargetM,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.015f),
                            textureTargetM.Bounds.Center.ToVector2(),
                            new Vector2(sizeCircle, sizeCircle) * projectile.scale / textureTargetM.Width,
                            SpriteEffects.None,
                            0f);
                        break;

                    case 7: ///////////////////////////////////////////////////////////////////// larger circle
                        if (alpha > 0.3f) { alpha = 0.3f; }
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            (float)(Main.time * 0.01f),
                            textureTargetL.Bounds.Center.ToVector2(),
                            size * projectile.scale / textureTargetL.Width,
                            SpriteEffects.None,
                            0f);

                            //second dust circle
                            for (int j = 0; j < 2; j++)
                            {
                                float randAng = Main.rand.Next(-31416, 31417) * 0.0001f;
                                circleVector = new Vector2(
                                    textureTargetL.Width * 0.25f * (float)Math.Cos(randAng) - 20,
                                    textureTargetL.Height * 0.25f * (float)Math.Sin(randAng) - 24
                                    );
                                //still dust
                                int d = Dust.NewDust(GraphicCentre + circleVector,
                                    32, 32, 174, 0, 0, 0, Color.White, 0.1f);
                                Main.dust[d].fadeIn = 0.6f;
                                Main.dust[d].noGravity = true;
                                Main.dust[d].velocity *= 0.25f;
                            }
                        break;
                    case 8: ///////////////////////////////////////////////////////////////////// top circle
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 7f);
                        sizeOffset = size * (6 / 7f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            -sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetXL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetXL.Bounds.Center.ToVector2(),
                            new Vector2(size, sizeCircle) * projectile.scale / textureTargetXL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 9: ///////////////////////////////////////////////////////////////////// bottom circle
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 6f);
                        sizeOffset = size * (5 / 6f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetXL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetXL.Bounds.Center.ToVector2(),
                            new Vector2(size, sizeCircle) * projectile.scale / textureTargetXL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 10: ///////////////////////////////////////////////////////////////////// mid circle
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 7f);
                        castCentre = GraphicCentre - Main.screenPosition;

                        spriteBatch.Draw(textureTargetL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetL.Bounds.Center.ToVector2(),
                            new Vector2(size, sizeCircle) * projectile.scale / textureTargetL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 11: ///////////////////////////////////////////////////////////////////// guide topper 1
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 9f);
                        sizeOffset = size * (8 / 9f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            -sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetS,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetS.Bounds.Center.ToVector2(),
                            new Vector2(size * 0.7f, sizeCircle) * projectile.scale / textureTargetS.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 12: ///////////////////////////////////////////////////////////////////// guide topper 2
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 9f);
                        sizeOffset = size * (8 / 9f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            -sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetXL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetXL.Bounds.Center.ToVector2(),
                            new Vector2(size * 0.8f, sizeCircle) * projectile.scale / textureTargetXL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 13: ///////////////////////////////////////////////////////////////////// guide topper 3
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 9f);
                        sizeOffset = size * (8 / 9f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            -sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetXL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetXL.Bounds.Center.ToVector2(),
                            new Vector2(size * 0.85f, sizeCircle) * projectile.scale / textureTargetXL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                    case 14: ///////////////////////////////////////////////////////////////////// guide topper 4
                        if (alpha > 0.5f) { alpha = 0.5f; }
                        sizeCircle = size * (1 / 9f);
                        sizeOffset = size * (8 / 9f);
                        castCentre = GraphicCentre - Main.screenPosition
                            + new Vector2(
                            0,
                            -sizeOffset * 0.5f
                        );

                        spriteBatch.Draw(textureTargetXL,
                            castCentre,
                            null,
                            new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                            0,
                            textureTargetXL.Bounds.Center.ToVector2(),
                            new Vector2(size * 0.6f, sizeCircle) * projectile.scale / textureTargetXL.Width,
                            SpriteEffects.None,
                            0f);
                        break;
                }
                textureAlphas[i] = alpha;
            }
        }
        private void drawCastingCircles(SpriteBatch spriteBatch)
        {
            Player player = Main.player[projectile.owner];

            //when casting or standing still
            if (ExplosionState == 1
                || (ExplosionState == 0 && player.velocity.X == 0 && player.velocity.Y == 0))
            {
                //casting circle
                PlayerFX.drawMagicCast(player, spriteBatch, Color.OrangeRed, (int)projectile.timeLeft % 48 / 12);
            }
            float size;
            float alpha;
            Vector2 castCentre;
            float sizeCircle;
            if (ExplosionState != 0)
            {
                //set up casting from player
                int distance = 120 + ChargeLevel * 3;


                //Casting cicle
                if (ExplosionState == 1) textureCastAlphas[0] = 1f;
                if (ExplosionState == 2) textureCastAlphas[0] *= 0.9f;
                ////-//Main.NewText("cast circle target alpha \n" + textureCastAlphas[0]);
                alpha = textureCastAlphas[0];

                size = projectile.width;
                sizeCircle = size * (1 / 9f);
                castCentre = GraphicCentre
                    + new Vector2(
                    0,
                    -size * 0.75f
                );

                spriteBatch.Draw(textureTargetL,
                    castCentre - Main.screenPosition,
                    null,
                    new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                    0,
                    textureTargetL.Bounds.Center.ToVector2(),
                    new Vector2(size, sizeCircle) * projectile.scale / textureTargetL.Width,
                    SpriteEffects.None,
                    0f);

                //EPXLOSION LASER
                if (ExplosionState == 1)
                {
                    //burst FX
                    int d = Dust.NewDust(castCentre - new Vector2(2 + ChargeLevel * 0.2f, 0), 8, 0, 162,
                        0, 2, 0, Color.White, 1 + ChargeLevel * 0.4f);
                    Main.dust[d].noGravity = true; //loses velocity very fast
                    Main.dust[d].velocity.X *= ChargeLevel;

                    //from staff
                    drawLaser(spriteBatch, player.Top + new Vector2(0, -34), player.Top + new Vector2(0, -distance));

                    //to explosion
                    drawLaser(spriteBatch, castCentre, GraphicCentre);



                    //dust fly into top circle
                    Vector2 circleVector;
                    float randAng = Main.rand.Next(-31416, 31417) * 0.0001f;
                    circleVector = new Vector2(
                        58 * (float)Math.Cos(randAng),
                        10 * (float)Math.Sin(randAng)
                        );
                    //dust spawn at cricle and move in
                    d = Dust.NewDust(player.Top + new Vector2(-4, -distance - 4) + circleVector, 0, 0,
                        170, circleVector.X, circleVector.Y, 0, Color.White, 0.1f);
                    Main.dust[d].fadeIn = 1f;
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= -0.07f;
                }

                //Player top circles
                spriteBatch.Draw(textureTargetS,
                    player.Top + new Vector2(0, -distance) - Main.screenPosition,
                    null,
                    new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                    0,
                    textureTargetS.Bounds.Center.ToVector2(),
                    new Vector2(player.width * 8, player.width * 1.6f) * projectile.scale / textureTargetS.Width,
                    SpriteEffects.None,
                    0f);
                if (ChargeLevel < 8) return; //STOP if less than 8 charge
                spriteBatch.Draw(textureTargetM,
                    player.Top + new Vector2(0, -distance * 0.6f) - Main.screenPosition,
                    null,
                    new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                    0,
                    textureTargetM.Bounds.Center.ToVector2(),
                    new Vector2(player.width * 5, player.width) * projectile.scale / textureTargetM.Width,
                    SpriteEffects.None,
                    0f);
                if (ChargeLevel < 12) return; //STOP if less than 12 charge
                spriteBatch.Draw(textureTargetL,
                    player.Top + new Vector2(0, -distance * 0.3f) - Main.screenPosition,
                    null,
                    new Color(1f * alpha, 1f * alpha, 1f * alpha, alpha),
                    0,
                    textureTargetL.Bounds.Center.ToVector2(),
                    new Vector2(player.width * 4, player.width * 0.8f) * projectile.scale / textureTargetL.Width,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void drawLaser(SpriteBatch spritebatch, Vector2 start, Vector2 end)
        {
            try
            {
                ////-//Main.NewText("charge: \n" + ChargeTick + " / "  + chargeTime);
                Utils.DrawLaser(
                    spritebatch,
                    textureLaser,
                    start - Main.screenPosition,
                    end - Main.screenPosition,
                    new Vector2(Math.Max(0.1f, 4f / (3f + chargeTime - ChargeTick))),
                    new Utils.LaserLineFraming(ExplosionLaser)); //uses delegate (see method below)
            }
            catch { }
        }
        //define which frames are used in each stage (0 = start, 1 = mid, 2 = end
        private void ExplosionLaser(int stage, Vector2 currentPosition, float distanceLeft, Rectangle lastFrame, out float distCovered, out Rectangle frame, out Vector2 origin, out Color color)
        {
            color = Color.White;
            if (stage == 0)
            {
                distCovered = 33f;
                frame = new Rectangle(0, 0, 22, 22);
                origin = frame.Size() / 2f;
                return;
            }
            if (stage == 1)
            {
                frame = new Rectangle(0, 22, 22, 22);
                distCovered = (float)frame.Height;
                origin = new Vector2((float)(frame.Width / 2), 0f);
                return;
            }
            if (stage == 2)
            {
                distCovered = 22f;
                frame = new Rectangle(0, 44, 22, 22);
                origin = new Vector2((float)(frame.Width / 2), 1f);
                return;
            }
            distCovered = 9999f;
            frame = Rectangle.Empty;
            origin = Vector2.Zero;
            color = Color.Transparent;
        }
        #endregion
    }
}
