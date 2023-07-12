using System;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;
using DeathsTerminus.Enums;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria.Graphics.Effects;
using DeathsTerminus.Assets;
using Terraria.DataStructures;
using Terraria.GameContent.Bestiary;

namespace DeathsTerminus.NPCs.CataBoss
{
    [AutoloadBossHead]
    public class CataBoss : ModNPC
    {
        //ai[0] is attack type
        //ai[1] is attack timer
        //ai[2] and ai[3] are secondary values for attacks

        //localAI[0] is 0 if shadow cata and 1 if regular cata

        private bool canShieldBonk;
        private bool holdingShield;
        private bool onSlimeMount;
        private int iceShieldCooldown;
        private int hitDialogueCooldown;
        private int rodAnticheeseCooldown;
        private bool drawSpawnTransitionRing;
        private Color spawnTransitionColor = Color.Purple;
        private Color auraColor = Color.Purple;
        private bool useRainbowColorAura = false;
        private bool useRainbowColorTransition = false;
        private bool drawEyeTrail = false;
        private bool drawAura = false;
        private int auraCounter = 0;
        private bool killable = false;
        private float teleportTime;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Cataclysmic Armageddon");
            Main.npcFrameCount[NPC.type] = 6;

            NPCID.Sets.TrailCacheLength[NPC.type] = 26;
            NPCID.Sets.TrailingMode[NPC.type] = 3;

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

            NPCDebuffImmunityData debuffData = new()
            {
                ImmuneToAllBuffsThatAreNotWhips = true
            };
            NPCID.Sets.DebuffImmunitySets.Add(Type, debuffData);

            NPCID.Sets.NPCBestiaryDrawModifiers value = new(0);
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
        }

        public override void SetDefaults()
        {
            NPC.aiStyle = (int)AIStyles.CustomAI;
            NPC.width = 18;
            NPC.height = 40;
            DrawOffsetY = -5;

            NPC.defense = 0;
            NPC.lifeMax = 250;
            NPC.chaseable = false;
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath59;

            NPC.damage = 160;
            NPC.knockBackResist = 0f;

            NPC.value = Item.buyPrice(platinum: 1);

            NPC.npcSlots = 15f;
            NPC.boss = true;
            //bossBag = ItemType<CataBossBag>();

            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;

            Music = MusicID.Boss4;

            if (ModLoader.TryGetMod("DeathsTerminusMusic", out Mod DeathsTerminusMusic))
            {
                if (!Main.dedServ)
                    Music = MusicLoader.GetMusicSlot(DeathsTerminusMusic, "Sounds/Music/Lights_Aerial_Veil");
            }
        }
        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new List<IBestiaryInfoElement> {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,

                new FlavorTextBestiaryInfoElement("goo goo ga ga im cata i like lamps")
            });
        }
        public override void AI()
        {
            Player player = Main.player[NPC.target];
            if (!player.active || player.dead)
            {
                NPC.TargetClosest(false);
                player = Main.player[NPC.target];
                if (!player.active || player.dead)
                {
                    if (NPC.localAI[0] == 0)
                    {
                        ShadowCataFleeAnimation();
                    }
                    else
                    {
                        NPC.Transform(ModContent.NPCType<CataclysmicArmageddon>());
                    }

                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].hostile)
                        {
                            Main.projectile[i].active = false;
                        }
                    }
                    return;
                }
            }

            holdingShield = false;
            drawSpawnTransitionRing = false;
            if (iceShieldCooldown > 0)
            {
                iceShieldCooldown--;
            }
            if (hitDialogueCooldown > 0)
            {
                hitDialogueCooldown--;
            }
            if (rodAnticheeseCooldown > 0)
            {
                rodAnticheeseCooldown--;
            }
            if (drawAura)
            {
                auraCounter++;
            }

            //RoD arrival dusts
            if (teleportTime > 0)
            {
                if ((float)Main.rand.Next(100) <= 100f * teleportTime)
                {
                    int num2 = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.TeleportationPotion);
                    Main.dust[num2].scale = teleportTime * 1.5f;
                    Main.dust[num2].noGravity = true;
                    Dust obj2 = Main.dust[num2];
                    obj2.velocity *= 1.1f;
                }
                teleportTime -= 0.005f;
            }

            NPC.life = NPC.lifeMax;

            //RoD anticheese
            if (NPC.ai[0] < 23 && rodAnticheeseCooldown == 0 && player.HeldItem.type == ItemID.RodofDiscord && player.itemTime > 0)
            {
                rodAnticheeseCooldown = player.itemTime;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossRod>(), 80, 0f, Main.myPlayer, player.whoAmI);
                }
            }

            switch (NPC.ai[0])
            {
                case 0:
                    //5 secs each
                    SpawnAnimation();
                    break;
                case 1:
                    //3 secs each
                    SideScythesAttack();
                    break;
                case 2:
                case 7:
                    //7.3333 secs each
                    SideScythesAttackSpin();
                    break;
                case 3:
                case 8:
                    //4.3333 secs each
                    SideBlastsAttack();
                    break;
                case 4:
                    //8 secs each
                    IceSpiralAttack();
                    break;
                case 5:
                    //2.8 secs each
                    ShieldBonk();
                    break;
                case 6:
                    //2 secs each
                    SlimeBonk();
                    break;
                case 9:
                    //10 secs each
                    MothsAndLampAttack();
                    break;
                case 10:
                    //10 secs each
                    HeavenPetAttack();
                    break;
                case 11:
                    //5 secs each
                    Phase1To2Animation();
                    break;
                case 12:
                case 19:
                    //7.3333 secs each
                    SideScythesAttackHard();
                    break;
                case 13:
                case 21:
                    //5 secs each
                    SideBlastsAttackHard();
                    break;
                case 14:
                    //12 secs each
                    IceSpiralAttackHard();
                    break;
                case 15:
                    //2.5 secs each
                    SideSuperScythesAttack();
                    break;
                case 16:
                    //9 secs each
                    AncientDoomMinefield();
                    break;
                case 17:
                    //6 secs each
                    ShieldBonkHard();
                    break;
                case 18:
                    //10 secs each
                    SlimeBonkHard();
                    break;
                case 20:
                    //10 secs each
                    MothronsAndLampAttack();
                    break;
                case 22:
                    //10 secs each
                    HeavenPetAttackHard();
                    break;
                case 23:
                    //5 secs each
                    Phase2To3Animation();
                    break;
                case 24:
                    //15 secs each
                    IceScythesAttack();
                    break;
                case 25:
                    //12 secs each
                    AncientDoomMinefieldHard();
                    break;
                case 26:
                    //13 secs each
                    FishronsMothsAttack();
                    break;
                case 27:
                    //29 secs each
                    MothronsAndLampCircularAttack();
                    break;
                case 28:
                    if (Main.expertMode)
                    {
                        //5 secs each
                        Phase3To4Animation();
                    }
                    else
                    {
                        //4 secs each
                        DeathAnimation();
                    }
                    break;
                case 29:
                    if (Main.expertMode)
                    {
                        //32 secs each
                        MegaSprocketVsMegaBaddySuperCinematicDesperationAttack();
                    }
                    else
                    {
                        killable = true;
                        NPC.life = 0;
                        NPC.checkDead();
                    }
                    break;
                case 30:
                    //4 secs each
                    DeathAnimation();
                    break;
                case 31:
                    killable = true;
                    NPC.life = 0;
                    NPC.checkDead();
                    break;
            }
        }

        private void FlyToPoint(Vector2 goalPoint, Vector2 goalVelocity, float maxXAcc = 0.5f, float maxYAcc = 0.5f)
        {
            Vector2 goalOffset = goalPoint - goalVelocity - NPC.Center;
            Vector2 relativeVelocity = NPC.velocity - goalVelocity;

            //compute whether we'll overshoot or undershoot our X goal at our current velocity
            if (relativeVelocity.X * relativeVelocity.X / 2 / maxXAcc > Math.Abs(goalOffset.X) && (goalOffset.X > 0 ^ relativeVelocity.X < 0))
            {
                //overshoot
                NPC.velocity.X += maxXAcc * (goalOffset.X > 0 ? -1 : 1);
            }
            else
            {
                //undershoot
                NPC.velocity.X += maxXAcc * (goalOffset.X > 0 ? 1 : -1);
            }
            //compute whether we'll overshoot or undershoot our X goal at our current velocity
            if (relativeVelocity.Y * relativeVelocity.Y / 2 / maxYAcc > Math.Abs(goalOffset.Y) && (goalOffset.Y > 0 ^ relativeVelocity.Y < 0))
            {
                //overshoot
                NPC.velocity.Y += maxYAcc * (goalOffset.Y > 0 ? -1 : 1);
            }
            else
            {
                //undershoot
                NPC.velocity.Y += maxYAcc * (goalOffset.Y > 0 ? 1 : -1);
            }
        }

        //1 sec
        private void SpawnAnimation()
        {
            Player player = Main.player[NPC.target];

            if (NPC.localAI[0] == 0 && NPC.ai[1] == 0)
            {
                NPC.Center = player.Center + new Vector2(240 * (Main.rand.NextBool() ? 1 : -1), -240);
                NPC.velocity = Vector2.Zero;

                for (int i = 0; i < 128; i++)
                {
                    Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, ModContent.DustType<ShadowDust>(), Scale: 2).velocity = new Vector2((float)Math.Sin(Main.rand.NextFloat() * MathHelper.Pi / 2f) * 6f, 0).RotatedByRandom(MathHelper.TwoPi);
                }

                Main.LocalPlayer.GetModPlayer<DTPlayer>().screenShakeTime = 60;
                SoundEngine.PlaySound(SoundID.DoubleJump, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item122, NPC.Center);

                CombatText.NewText(NPC.getRect(), new Color(0, 76, 153), "So, here we are... it's about time you died!", true);

                //initialize custom death sound
                NPC.DeathSound = new("DeathsTerminus/Sounds/ShadowCataDeath") { Volume = .4f };
            }

            if ((NPC.Center - player.Center).Length() > 1000 && NPC.ai[1] == 0)
            {
                NPC.Center = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 1000;
            }

            spawnTransitionColor = Color.Purple;

            CataBossSky.celestialObject = 0;

            if (NPC.ai[1] == 120)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), new Color(0, 76, 153), "The real Cata isn't here at the moment", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), new Color(0, 76, 153), "So you think you can defeat me?", true);
                }
            }
            else if (NPC.ai[1] == 180)
            {
                SoundEngine.PlaySound(SoundID.Zombie105, NPC.Center);
            }
            else if (NPC.ai[1] == 299)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.Purple, "So I'll test you instead!", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.Purple, "Well then, let's see what you can do.", true);
                }
            }

            //transition animation
            if (NPC.ai[1] >= 180 && NPC.ai[1] < 299)
            {
                NPC.velocity = Vector2.Zero;

                drawSpawnTransitionRing = true;
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

                if (NPC.localAI[0] == 0)
                {
                    goalPosition = player.Center + new Vector2(-NPC.direction, -1) * 240;
                }

                FlyToPoint(goalPosition, Vector2.Zero);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 300)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void Phase1To2Animation()
        {
            //Pre-animation and text
            Player player = Main.player[NPC.target];

            spawnTransitionColor = Color.Orange;

            if (NPC.ai[1] == 120)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.Purple, "This is only the beginning!", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.Purple, "Not bad, you've survived a minute.", true);
                }
            }
            else if (NPC.ai[1] == 180)
            {
                SoundEngine.PlaySound(SoundID.Zombie105, NPC.Center);
            }
            else if (NPC.ai[1] == 299)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.Orange, "You've seen nothing yet challenger!", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.Orange, "But it will only get harder from here.", true);
                }

                drawEyeTrail = true;

                auraColor = spawnTransitionColor;

                CataBossSky.celestialObject = 1;
            }

            //transition animation
            if (NPC.ai[1] >= 180 && NPC.ai[1] < 299)
            {
                NPC.velocity = Vector2.Zero;

                drawSpawnTransitionRing = true;
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

                FlyToPoint(goalPosition, Vector2.Zero);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 300)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void Phase2To3Animation()
        {
            //Pre-animation and text
            Player player = Main.player[NPC.target];

            spawnTransitionColor = Color.LightBlue;

            if (NPC.ai[1] == 120)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.Orange, "What is your problem!?", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.Orange, "You're making good progress so far.", true);
                }
            }
            else if (NPC.ai[1] == 180)
            {
                SoundEngine.PlaySound(SoundID.Zombie105, NPC.Center);
            }
            else if (NPC.ai[1] == 299)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.LightBlue, "Trying to prove you've no Skill issues?!", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.LightBlue, "Let's see if you've got what it takes.", true);
                }

                drawAura = true;

                auraColor = spawnTransitionColor;

                CataBossSky.celestialObject = 2;
            }

            //transition animation
            if (NPC.ai[1] >= 180 && NPC.ai[1] < 299)
            {
                NPC.velocity = Vector2.Zero;

                drawSpawnTransitionRing = true;
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

                FlyToPoint(goalPosition, Vector2.Zero);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 300)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void Phase3To4Animation()
        {
            //Pre-animation and text
            Player player = Main.player[NPC.target];

            NPC.dontTakeDamage = true;
            useRainbowColorTransition = true;

            if (NPC.ai[1] >= 240 && NPC.localAI[0] != 0)
            {
                iceShieldCooldown += 2;
            }

            if (NPC.ai[1] == 120)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.LightBlue, "This isn't right, you're supposed to be dead!", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.LightBlue, "That's it, this has taken long enough.", true);
                }
            }
            else if (NPC.ai[1] == 180)
            {
                SoundEngine.PlaySound(SoundID.Zombie105, NPC.Center);
            }
            else if (NPC.ai[1] == 299)
            {
                if (NPC.localAI[0] == 0)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "I can't keep this form up much longer!", true);

                    for (int i = 0; i < 64; i++)
                    {
                        Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, ModContent.DustType<ShadowDust>(), Scale: 2).velocity = new Vector2((float)Math.Sin(Main.rand.NextFloat() * MathHelper.Pi / 2f) * 4f, 0).RotatedByRandom(MathHelper.TwoPi);
                    }

                    SoundEngine.PlaySound(SoundID.DoubleJump, NPC.Center);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "That wasn't meant to happen. Shadow, assistance?", true);

                    SoundEngine.PlaySound(SoundID.Item27, NPC.Center);

                    if (Main.netMode != NetmodeID.Server)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            Gore.NewGorePerfect(NPC.GetSource_FromThis(), NPC.Center - new Vector2(12, 35), new Vector2(6, 0).RotatedBy(i * MathHelper.TwoPi / 6), ModContent.Find<ModGore>("DeathsTerminus/CataBossIceShard").Type);
                            Gore.NewGorePerfect(NPC.GetSource_FromThis(), NPC.Center - new Vector2(12, 35), new Vector2(3, 0).RotatedBy((i + 0.5f) * MathHelper.TwoPi / 6), ModContent.Find<ModGore>("DeathsTerminus/CataBossIceShard").Type);
                        }
                    }
                }

                useRainbowColorAura = true;
                iceShieldCooldown = 0;

                CataBossSky.celestialObject = 3;
            }

            //transition animation
            if (NPC.ai[1] >= 180 && NPC.ai[1] < 299)
            {
                NPC.velocity = Vector2.Zero;

                drawSpawnTransitionRing = true;
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

                FlyToPoint(goalPosition, Vector2.Zero);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 300)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void DeathAnimation()
        {
            if (NPC.localAI[0] == 0)
            {
                //Pre-animation and text
                Player player = Main.player[NPC.target];

                Dust.NewDust(NPC.position, NPC.width, NPC.height, ModContent.DustType<ShadowDust>());

                if (NPC.ai[1] == 120)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "Congratulations...You've won...", true);
                }
                else if (NPC.ai[1] == 240)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "Next time, Make sure he's in your town?", true);
                }
                else if (NPC.ai[1] == 360)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "You shouldn't even have that Sigil....", true);
                }
                else if (NPC.ai[1] == 480)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "Your cheating interferes with my nap time....Bye!", true);
                }

                //death animation
                if (NPC.ai[1] >= 60)
                {
                    NPC.velocity = Vector2.Zero;
                }
                else
                {
                    if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                        NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                    NPC.spriteDirection = NPC.direction;
                    Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

                    FlyToPoint(goalPosition, Vector2.Zero);
                }

                NPC.ai[1]++;
                if (NPC.ai[1] == 600)
                {
                    for (int i = 0; i < 256; i++)
                    {
                        Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, ModContent.DustType<ShadowDust>(), Scale: 2).velocity = new Vector2((float)Math.Sin(Main.rand.NextFloat() * MathHelper.Pi / 2f) * 8f, 0).RotatedByRandom(MathHelper.TwoPi);
                    }

                    Main.LocalPlayer.GetModPlayer<DTPlayer>().screenShakeTime = 60;
                    SoundEngine.PlaySound(SoundID.DoubleJump, NPC.Center);

                    NPC.ai[1] = 0;
                    NPC.ai[0]++;
                }
            }
            else
            {
                //Pre-animation and text
                Player player = Main.player[NPC.target];

                if (NPC.ai[1] == 60 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 shotSpeed = new Vector2(0, 12).RotatedByRandom(0.5f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center - 180 * shotSpeed, shotSpeed, ModContent.ProjectileType<CataBossStar>(), 0, 0f, Main.myPlayer);
                }

                if (NPC.ai[1] == 120)
                {
                    CombatText.NewText(NPC.getRect(), Color.White, "Well, that was fun. I sure hope my current vulnerability won't suddenly become an issue.", true);
                }

                //death animation
                if (NPC.ai[1] >= 60)
                {
                    NPC.velocity = Vector2.Zero;
                }
                else
                {
                    if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                        NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                    NPC.spriteDirection = NPC.direction;
                    Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

                    FlyToPoint(goalPosition, Vector2.Zero);
                }

                NPC.ai[1]++;
                if (NPC.ai[1] == 240)
                {
                    NPC.ai[1] = 0;
                    NPC.ai[0]++;
                }
            }
        }

        private void ShadowCataFleeAnimation()
        {
            Player player = Main.player[NPC.target];

            if (NPC.localAI[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

                FlyToPoint(goalPosition, Vector2.Zero);
            }
            else if (NPC.localAI[1] < 120)
            {
                NPC.velocity = Vector2.Zero;
                if (NPC.localAI[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Zombie105, NPC.Center);
                }
            }
            else
            {
                NPC.velocity.Y -= 0.3f;
            }

            NPC.localAI[1]++;
            if (NPC.localAI[1] == 240)
            {
                NPC.active = false;
            }
        }

        //3 secs
        private void SideScythesAttack()
        {
            Player player = Main.player[NPC.target];
            if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
            NPC.spriteDirection = NPC.direction;
            Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

            FlyToPoint(goalPosition, Vector2.Zero);

            if (NPC.ai[1] >= 60 && NPC.ai[1] % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int numShots = 8;
                for (int i = 0; i < numShots; i++)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(1, 0).RotatedBy(i * MathHelper.TwoPi / numShots) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer);

                    Vector2 targetPoint = NPC.Center + new Vector2(800, 0).RotatedBy(i * MathHelper.TwoPi / numShots);
                    Vector2 launchPoint = targetPoint + new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                    launchPoint = targetPoint - new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                }
            }

            if (NPC.ai[1] >= 60 && NPC.ai[1] % 10 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 180)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        //7 secs 20 ticks
        private void SideScythesAttackSpin()
        {
            Player player = Main.player[NPC.target];

            if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
            NPC.spriteDirection = NPC.direction;
            Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 240;

            FlyToPoint(goalPosition, Vector2.Zero);

            if (NPC.ai[1] >= 60 && NPC.ai[1] < 220 && NPC.ai[1] % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int numShots = 8;
                for (int i = 0; i < numShots; i++)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(1, 0).RotatedBy(i * MathHelper.TwoPi / numShots + NPC.direction * (NPC.ai[1] - 60) / 100f) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer);

                    Vector2 targetPoint = NPC.Center + new Vector2(800, 0).RotatedBy(i * MathHelper.TwoPi / numShots + NPC.direction * (NPC.ai[1] - 60) / 100f);
                    Vector2 launchPoint = targetPoint + new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 + NPC.direction * (NPC.ai[1] - 60) / 100f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                    launchPoint = targetPoint - new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 + NPC.direction * (NPC.ai[1] - 60) / 100f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                }
            }
            else if (NPC.ai[1] >= 280 && NPC.ai[1] < 440 && NPC.ai[1] % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int numShots = 8;
                for (int i = 0; i < numShots; i++)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(1, 0).RotatedBy(i * MathHelper.TwoPi / numShots - NPC.direction * (NPC.ai[1] - 280) / 100f) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer);

                    Vector2 targetPoint = NPC.Center + new Vector2(800, 0).RotatedBy(i * MathHelper.TwoPi / numShots - NPC.direction * (NPC.ai[1] - 280) / 100f);
                    Vector2 launchPoint = targetPoint + new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 - NPC.direction * (NPC.ai[1] - 280) / 100f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                    launchPoint = targetPoint - new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 - NPC.direction * (NPC.ai[1] - 280) / 100f);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f);
                }
            }

            if (((NPC.ai[1] >= 60 && NPC.ai[1] < 220) || (NPC.ai[1] >= 280 && NPC.ai[1] < 440)) && NPC.ai[1] % 10 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 440)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        //4 secs 20 ticks
        private void SideBlastsAttack()
        {
            Player player = Main.player[NPC.target];

            if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
            NPC.spriteDirection = NPC.direction;
            Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 480;

            FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.4f, maxYAcc: 0.4f);

            int shotPeriod = 50;
            int numShots = 4;
            float shotSpeed = 0.5f;
            float shotDistanceFromPlayer = 200;

            if (NPC.ai[1] >= 60 && NPC.ai[1] % shotPeriod == 60 % shotPeriod && Main.netMode != NetmodeID.MultiplayerClient)
            {
                float angleRatio = shotDistanceFromPlayer / (player.Center - NPC.Center).Length();
                if (angleRatio > 1)
                {
                    angleRatio = 1;
                }

                int direction = NPC.ai[1] % (shotPeriod * 2) == 60 % (shotPeriod * 2) ? 1 : -1;
                Vector2 shotVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero).RotatedBy(direction * Math.Asin(angleRatio)) * shotSpeed;

                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, shotVelocity, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, -shotVelocity, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer);
            }

            if (NPC.ai[1] >= 90 && NPC.ai[1] % shotPeriod == 90 % shotPeriod)
            {
                SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 60 + shotPeriod * numShots)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        //8 secs
        private void IceSpiralAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else
            {
                NPC.velocity = Vector2.Zero;

                if (NPC.ai[1] == 60 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShield>(), 80, 0f, Main.myPlayer);
                    for (int i = -1; i <= 1; i++)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<RotatingIceShards>(), 80, 0f, Main.myPlayer, ai0: i);
                    }
                }

                if (NPC.ai[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Zombie88, NPC.Center);
                }
                if (NPC.ai[1] == 120)
                {
                    SoundEngine.PlaySound(SoundID.Item120, NPC.Center);
                }

                if (NPC.ai[1] % 10 == 0 && NPC.ai[1] < 360 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: -1);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 480)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        //2 secs 48 ticks
        private void ShieldBonk()
        {
            Player player = Main.player[NPC.target];

            holdingShield = true;

            if (NPC.ai[1] % 84 < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 180;

                FlyToPoint(goalPosition, player.velocity, 0.8f, 0.8f);
            }
            else if (NPC.ai[1] % 84 == 60)
            {
                canShieldBonk = true;

                NPC.width = 40;
                NPC.position.X -= 11;

                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                NPC.velocity.X += NPC.direction * 15;
                NPC.velocity.Y /= 2;
            }
            else if (NPC.ai[1] % 84 == 83)
            {
                if (canShieldBonk)
                {
                    NPC.width = 18;
                    NPC.position.X += 11;
                }

                canShieldBonk = false;

                NPC.velocity.X -= NPC.direction * 15;
            }

            //custom stuff for player EoC shield bonks
            //adapted from how the player detects SoC collision
            if (canShieldBonk)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead && Main.player[i].dash == 2 && Main.player[i].eocDash > 0 && Main.player[i].eocHit < 0)
                    {
                        Rectangle shieldHitbox = new Rectangle((int)((double)Main.player[i].position.X + (double)Main.player[i].velocity.X * 0.5 - 4.0), (int)((double)Main.player[i].position.Y + (double)Main.player[i].velocity.Y * 0.5 - 4.0), Main.player[i].width + 8, Main.player[i].height + 8);
                        if (shieldHitbox.Intersects(NPC.getRect()))
                        {
                            //custom stuff for player EoC shield bonks
                            //adapted from how the player detects SoC collision
                            NPC.width = 18;
                            NPC.position.X += 11;

                            NPC.direction *= -1;
                            NPC.velocity.X += NPC.direction * 30;
                            canShieldBonk = false;

                            SoundEngine.PlaySound(SoundID.NPCHit4, NPC.Center);

                            //redo the player's SoC bounce motion
                            int num40 = Main.player[i].direction;
                            if (Main.player[i].velocity.X < 0f)
                            {
                                num40 = -1;
                            }
                            if (Main.player[i].velocity.X > 0f)
                            {
                                num40 = 1;
                            }
                            Main.player[i].eocDash = 10;
                            Main.player[i].dashDelay = 30;
                            Main.player[i].velocity.X = -num40 * 9;
                            Main.player[i].velocity.Y = -4f;
                            Main.player[i].immune = true;
                            Main.player[i].immuneNoBlink = true;
                            Main.player[i].immuneTime = 4;
                            Main.player[i].eocHit = i;

                            break;
                        }
                    }
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 168)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void SlimeBonk()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(0, -1) * 360;

                FlyToPoint(goalPosition, player.velocity, 3f, 1f);
            }
            else
            {
                if (NPC.ai[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Item81, NPC.Center);

                    onSlimeMount = true;

                    if (NPC.velocity.Y < 0) NPC.velocity.Y /= 2;
                    NPC.velocity.X = player.velocity.X;

                    NPC.width = 40;
                    NPC.position.X -= 11;
                    NPC.height = 64;
                    DrawOffsetY = -15;

                    //mount dusts from slime mount
                    for (int i = 0; i < 100; i++)
                    {
                        int num2 = Dust.NewDust(new Vector2(NPC.position.X - 20f, NPC.position.Y), NPC.width + 40, NPC.height, DustID.BlueFairy);
                        Main.dust[num2].scale += (float)Main.rand.Next(-10, 21) * 0.01f;
                        Main.dust[num2].noGravity = true;
                        Dust obj2 = Main.dust[num2];
                        obj2.velocity += NPC.velocity * 0.8f;
                    }
                }
                else if (onSlimeMount)
                {
                    if (NPC.ai[1] < 119 && (NPC.velocity.Y < 0 || NPC.Hitbox.Top - 300 < player.Hitbox.Bottom))
                    {
                        NPC.velocity.Y += 0.9f;

                        FlyToPoint(player.Center, player.velocity, 0.05f, 0f);
                    }
                    else
                    {
                        NPC.velocity = player.velocity;
                        onSlimeMount = false;

                        NPC.width = 18;
                        NPC.position.X += 11;
                        NPC.height = 40;
                        DrawOffsetY = -5;
                    }
                }
                else
                {
                    NPC.velocity *= 0.98f;
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 120)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void MothsAndLampAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 1080;

                FlyToPoint(goalPosition, player.velocity, 0.8f, 0.8f);
            }
            else
            {
                if (NPC.ai[1] == 60)
                {
                    NPC.ai[2] = NPC.Center.X;
                    NPC.ai[3] = NPC.Center.Y;

                    SoundEngine.PlaySound(SoundID.Zombie104, NPC.Center + new Vector2(NPC.direction, 0) * 1500);
                    Main.LocalPlayer.GetModPlayer<DTPlayer>().screenShakeTime = 60;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SigilArena>(), 80, 0f, Main.myPlayer, ai0: 600);
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center + new Vector2(NPC.direction, 0) * 1500, new Vector2(-NPC.direction * 5, 0), ModContent.ProjectileType<SunLamp>(), 80, 0f, Main.myPlayer);
                    }
                }

                if (Main.netMode != NetmodeID.MultiplayerClient && NPC.ai[1] >= 90 && NPC.ai[1] <= 480 && NPC.ai[1] % 4 == 0)
                {
                    Vector2 arenaCenter = new Vector2(NPC.ai[2], NPC.ai[3]);

                    //ray X position
                    float relativeRayPosition = NPC.direction * 1500 - NPC.direction * 5 * (NPC.ai[1] + 30 - 60);

                    //angle of the arena still available
                    float availableAngle = (float)Math.Acos(relativeRayPosition / 1200f);
                    if (NPC.direction == 1)
                    {
                        availableAngle = MathHelper.Pi - availableAngle;
                    }
                    if (availableAngle > MathHelper.Pi / 2)
                    {
                        availableAngle = MathHelper.Pi / 2;
                    }

                    float availableHeight = (float)Math.Sqrt(1200f * 1200f - relativeRayPosition * relativeRayPosition);

                    float shotAngle = -NPC.direction * availableAngle * ((NPC.ai[1] / 1.618f / 4 % 1) * 2 - 1);
                    float goalHeight = arenaCenter.Y + -NPC.direction * shotAngle / availableAngle * availableHeight;

                    Projectile.NewProjectile(NPC.GetSource_FromThis(), arenaCenter + new Vector2(-1800f * NPC.direction, 0f).RotatedBy(shotAngle), new Vector2(32f * NPC.direction, 0).RotatedBy(shotAngle), ModContent.ProjectileType<BabyMothronProjectile>(), 80, 0f, Main.myPlayer, ai0: goalHeight, ai1: NPC.direction * 1500 - NPC.direction * 5 * (NPC.ai[1] - 60) + arenaCenter.X);
                }

                Vector2 goalPosition = new Vector2(NPC.ai[2], NPC.ai[3]) + new Vector2(-NPC.direction, 0) * 1080;

                FlyToPoint(goalPosition, Vector2.Zero, 0.25f, 0.25f);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 600)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void HeavenPetAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int proj = Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, (player.Center - NPC.Center).SafeNormalize(Vector2.Zero) * -6f, ModContent.ProjectileType<HeavenPetProjectile>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI);
                Main.projectile[proj].localAI[1] = NPC.localAI[0];
            }

            if (NPC.ai[1] < 600)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }

            if (NPC.ai[1] % 15 == 0 && NPC.ai[1] > 60)
            {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, (player.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 600)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        //7 secs 20 ticks
        private void SideScythesAttackHard()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 360;

                FlyToPoint(goalPosition, Vector2.Zero);
            }
            else
            {
                NPC.spriteDirection = player.Center.X > NPC.Center.X ? 1 : -1;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

                FlyToPoint(goalPosition, Vector2.Zero);
            }

            if (NPC.ai[1] >= 60 && NPC.ai[1] < 440 && NPC.ai[1] % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int numShots = 12;
                for (int i = 0; i < numShots; i++)
                {
                    float rotationValue = -NPC.direction * MathHelper.Pi / 2f * ((float)Math.Cos((NPC.ai[1] - 60) / 380f * MathHelper.TwoPi) - 1) / 2f;

                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(1, 0).RotatedBy(i * MathHelper.TwoPi / numShots + rotationValue) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai1: 1);

                    if (NPC.ai[1] % 10 == 0)
                    {
                        Vector2 targetPoint = NPC.Center + new Vector2(800, 0).RotatedBy(i * MathHelper.TwoPi / numShots + rotationValue);
                        Vector2 launchPoint = targetPoint + new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 + rotationValue);
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f, ai1: 1);
                        launchPoint = targetPoint - new Vector2(1920, 0).RotatedBy(i * MathHelper.TwoPi / numShots + MathHelper.PiOver2 + rotationValue);
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), launchPoint, (targetPoint - launchPoint).SafeNormalize(Vector2.Zero) * 30f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai0: 120f, ai1: 1);
                    }
                }
            }

            if ((NPC.ai[1] >= 60 && NPC.ai[1] < 440) && NPC.ai[1] % 10 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 440)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void SideSuperScythesAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
            }
            NPC.spriteDirection = NPC.direction;
            Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 360;

            FlyToPoint(goalPosition, Vector2.Zero);

            if (NPC.ai[1] >= 60 && NPC.ai[1] <= 90 && NPC.ai[1] % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int number = (int)(NPC.ai[1] - 60) / 10;

                for (int i = -number; i <= number; i++)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, new Vector2(NPC.direction, 0).RotatedBy(i / 3f * MathHelper.TwoPi / 6) * 0.5f, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1f);
                }
            }
            if (NPC.ai[1] - 30 >= 60 && NPC.ai[1] - 30 <= 90 && (NPC.ai[1] - 30) % 10 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 150)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void SideBlastsAttackHard()
        {
            Player player = Main.player[NPC.target];

            if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
            NPC.spriteDirection = NPC.direction;
            Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 720;

            FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.4f, maxYAcc: 0.4f);

            int shotPeriod = 60;
            int numShots = 4;
            float shotSpeed = 0.5f;
            float shotDistanceFromPlayer = 300;

            if (NPC.ai[1] >= 60 && NPC.ai[1] % shotPeriod == 60 % shotPeriod && Main.netMode != NetmodeID.MultiplayerClient)
            {
                float angleRatio = shotDistanceFromPlayer / (player.Center - NPC.Center).Length();
                if (angleRatio > 1)
                {
                    angleRatio = 1;
                }

                int direction = NPC.ai[1] % (shotPeriod * 2) == 60 % (shotPeriod * 2) ? 1 : -1;
                Vector2 shotVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero).RotatedBy(direction * Math.Asin(angleRatio)) * shotSpeed;

                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, shotVelocity, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, shotVelocity.RotatedBy(0.15f), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, shotVelocity.RotatedBy(-0.15f), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, -shotVelocity, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, -shotVelocity.RotatedBy(0.15f), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, -shotVelocity.RotatedBy(-0.15f), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 1);
            }

            if (NPC.ai[1] >= 90 && NPC.ai[1] % shotPeriod == 90 % shotPeriod)
            {
                SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 60 + shotPeriod * numShots)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void IceSpiralAttackHard()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else
            {
                NPC.velocity = Vector2.Zero;

                if (NPC.ai[1] == 60 || NPC.ai[1] == 240 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShield>(), 80, 0f, Main.myPlayer);
                    for (int i = -1; i <= 1; i++)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<RotatingIceShards>(), 80, 0f, Main.myPlayer, ai0: i, ai1: 0f);
                    }
                }

                if (NPC.ai[1] > 120 && NPC.ai[1] < 600 && NPC.ai[1] % 10 == 0)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, (player.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 0.5f, ModContent.ProjectileType<IceShard>(), 80, 0f, Main.myPlayer, ai0: 1.04f);
                }

                if (NPC.ai[1] == 60 || NPC.ai[1] == 240)
                {
                    SoundEngine.PlaySound(SoundID.Zombie88, NPC.Center);
                }
                if (NPC.ai[1] == 120 || NPC.ai[1] == 300)
                {
                    SoundEngine.PlaySound(SoundID.Item120, NPC.Center);
                }

                if (NPC.ai[1] % 10 == 0 && NPC.ai[1] < 600 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: -1);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 720)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void ShieldBonkHard()
        {
            int dashTime = 24;
            int downTime = 51;
            int numDashes = 4;

            Player player = Main.player[NPC.target];

            holdingShield = true;

            if (NPC.ai[1] < 60 || (NPC.ai[1] - 60) % (dashTime + downTime) > dashTime)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 180;

                FlyToPoint(goalPosition, player.velocity, 4f, 2f);
            }
            else if ((NPC.ai[1] - 60) % (dashTime + downTime) == 0)
            {
                canShieldBonk = true;

                NPC.width = 40;
                NPC.position.X -= 11;

                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                NPC.velocity.X += NPC.direction * 15;
                NPC.velocity.Y = player.velocity.Y / 2;
            }
            else if ((NPC.ai[1] - 60) % (dashTime + downTime) == dashTime)
            {
                if (canShieldBonk)
                {
                    NPC.width = 18;
                    NPC.position.X += 11;
                }

                canShieldBonk = false;

                NPC.velocity.X -= NPC.direction * 15;
            }
            else
            {
                //spawn moths
                if (NPC.ai[1] % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), player.Center + new Vector2(-NPC.spriteDirection * 2048 + player.velocity.X * 64f, player.velocity.Y * 64f), new Vector2(NPC.spriteDirection * 32, 0), ModContent.ProjectileType<MothProjectile>(), 80, 0f, Main.myPlayer);
                }
            }

            //custom stuff for player EoC shield bonks
            //adapted from how the player detects SoC collision
            if (canShieldBonk)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead && Main.player[i].dash == 2 && Main.player[i].eocDash > 0 && Main.player[i].eocHit < 0)
                    {
                        Rectangle shieldHitbox = new Rectangle((int)((double)Main.player[i].position.X + (double)Main.player[i].velocity.X * 0.5 - 4.0), (int)((double)Main.player[i].position.Y + (double)Main.player[i].velocity.Y * 0.5 - 4.0), Main.player[i].width + 8, Main.player[i].height + 8);
                        if (shieldHitbox.Intersects(NPC.getRect()))
                        {
                            //custom stuff for player EoC shield bonks
                            //adapted from how the player detects SoC collision
                            NPC.width = 18;
                            NPC.position.X += 11;

                            NPC.direction *= -1;
                            NPC.velocity.X += NPC.direction * 30;
                            canShieldBonk = false;

                            SoundEngine.PlaySound(SoundID.NPCHit4, NPC.Center);

                            //redo the player's SoC bounce motion
                            int num40 = Main.player[i].direction;
                            if (Main.player[i].velocity.X < 0f)
                            {
                                num40 = -1;
                            }
                            if (Main.player[i].velocity.X > 0f)
                            {
                                num40 = 1;
                            }
                            Main.player[i].eocDash = 10;
                            Main.player[i].dashDelay = 30;
                            Main.player[i].velocity.X = -num40 * 9;
                            Main.player[i].velocity.Y = -4f;
                            Main.player[i].immune = true;
                            Main.player[i].immuneNoBlink = true;
                            Main.player[i].immuneTime = 4;
                            Main.player[i].eocHit = i;

                            break;
                        }
                    }
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 60 + numDashes * (dashTime + downTime))
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void SlimeBonkHard()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(0, -240) + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else
            {
                bool justBounced = false;

                if (NPC.ai[1] == 60)
                {
                    justBounced = true;

                    SoundEngine.PlaySound(SoundID.Item81, NPC.Center);

                    onSlimeMount = true;

                    if (NPC.velocity.Y < 0) NPC.velocity.Y /= 2;
                    NPC.velocity.X = player.velocity.X;

                    NPC.width = 40;
                    NPC.position.X -= 11;
                    NPC.height = 64;
                    DrawOffsetY = -15;

                    //mount dusts from slime mount
                    for (int i = 0; i < 100; i++)
                    {
                        int num2 = Dust.NewDust(new Vector2(NPC.position.X - 20f, NPC.position.Y), NPC.width + 40, NPC.height, DustID.BlueFairy);
                        Main.dust[num2].scale += (float)Main.rand.Next(-10, 21) * 0.01f;
                        Main.dust[num2].noGravity = true;
                        Dust obj2 = Main.dust[num2];
                        obj2.velocity += NPC.velocity * 0.8f;
                    }
                }
                else if (onSlimeMount)
                {
                    if (NPC.ai[1] == 599)
                    {
                        NPC.velocity = player.velocity;
                        onSlimeMount = false;

                        NPC.width = 18;
                        NPC.position.X += 11;
                        NPC.height = 40;
                        DrawOffsetY = -5;

                        for (int i = 0; i < Main.maxProjectiles; i++)
                        {
                            if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<FishronPlatform>() && Main.projectile[i].ai[1] == 0)
                            {
                                Main.projectile[i].ai[1] = 1;
                            }
                            else if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<FloatingBubble>() && Main.projectile[i].ai[1] < 0)
                            {
                                Main.projectile[i].ai[0] = 1f;
                                Main.projectile[i].ai[1] = -0.2f;
                            }
                        }
                    }
                    else if (NPC.velocity.Y < 0 || NPC.Hitbox.Top - 240 < player.Hitbox.Bottom)
                    {
                        NPC.velocity.Y += 0.9f;

                        NPC.direction = NPC.velocity.X > 0 ? -1 : 1;
                        NPC.spriteDirection = -NPC.direction;
                    }
                    else
                    {
                        for (int i = 0; i < Main.maxProjectiles; i++)
                        {
                            if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<FishronPlatform>() && Main.projectile[i].ai[1] == 0)
                            {
                                Main.projectile[i].Kill();
                                break;
                            }
                        }

                        NPC.velocity.Y = Math.Min(-36f, NPC.velocity.Y - 36f);
                        NPC.velocity.X = player.velocity.X + (player.Center.X - NPC.Center.X) / 60f;

                        NPC.direction = NPC.velocity.X > 0 ? -1 : 1;
                        NPC.spriteDirection = -NPC.direction;

                        justBounced = true;
                    }
                }
                else
                {
                    NPC.velocity *= 0.98f;
                }

                //summon bigger fish
                if (justBounced)
                {
                    if (NPC.ai[1] <= 540 && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float determinant = Math.Max(0, (NPC.velocity.Y - player.velocity.Y) * (NPC.velocity.Y - player.velocity.Y) - 4 * 0.45f * (NPC.Center.Y - player.Center.Y + 240));
                        float eta = Math.Max(3, (-(NPC.velocity.Y - player.velocity.Y) + (float)Math.Sqrt(determinant)) / 0.9f);
                        float speed = 28f;
                        Vector2 targetPoint = new Vector2(NPC.Center.X + NPC.velocity.X * eta, player.Center.Y + 240 + player.velocity.Y * eta);
                        Vector2 shotPosition = targetPoint + new Vector2(NPC.direction * eta * speed, 0);
                        Vector2 shotVelocity = (targetPoint - shotPosition) / eta;

                        Projectile.NewProjectile(NPC.GetSource_FromThis(), shotPosition, shotVelocity, ModContent.ProjectileType<FishronPlatform>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI);
                    }
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 600)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void MothronsAndLampAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + new Vector2(-NPC.direction, 0) * 1080;

                FlyToPoint(goalPosition, player.velocity, 0.8f, 0.8f);
            }
            else
            {
                if (NPC.ai[1] == 60)
                {
                    NPC.ai[2] = NPC.Center.X;
                    NPC.ai[3] = NPC.Center.Y;

                    SoundEngine.PlaySound(SoundID.Zombie104, NPC.Center + new Vector2(NPC.direction, 0) * 1500);
                    Main.LocalPlayer.GetModPlayer<DTPlayer>().screenShakeTime = 60;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SigilArena>(), 80, 0f, Main.myPlayer, ai0: 600);
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center + new Vector2(NPC.direction, 0) * 1500, new Vector2(-NPC.direction * 5, 0), ModContent.ProjectileType<SunLamp>(), 80, 0f, Main.myPlayer, ai0: 1f);
                    }
                }

                //mothron singing sound
                if (NPC.ai[1] == 120)
                {
                    SoundEngine.PlaySound(SoundID.Zombie73.WithVolumeScale(2f).WithPitchOffset(-1), NPC.Center);
                }

                int period = 60;
                float number = 10;

                if (Main.netMode != NetmodeID.MultiplayerClient && NPC.ai[1] >= 90 && NPC.ai[1] <= 480 && NPC.ai[1] % period == 0)
                {
                    Vector2 arenaCenter = new Vector2(NPC.ai[2], NPC.ai[3]);

                    //ray X position
                    float relativeRayPosition = NPC.direction * 1500 - NPC.direction * 5 * (NPC.ai[1] + 30 - 60);

                    //angle of the arena still available
                    float availableAngle = (float)Math.Acos(relativeRayPosition / 1200f);
                    if (NPC.direction == 1)
                    {
                        availableAngle = MathHelper.Pi - availableAngle;
                    }
                    if (availableAngle > MathHelper.Pi / 2)
                    {
                        availableAngle = MathHelper.Pi / 2;
                    }

                    float availableHeight = (float)Math.Sqrt(1200f * 1200f - relativeRayPosition * relativeRayPosition);

                    for (int i = 0; i < number; i++)
                    {
                        float angleModifier = ((2 * i - (number - 1)) / number + ((NPC.ai[1] / 1.618f / period % 1) * 2 - 1) / number);
                        angleModifier = (angleModifier * angleModifier * angleModifier + angleModifier) / 2;

                        float shotAngle = -NPC.direction * availableAngle * angleModifier;
                        float goalHeight = arenaCenter.Y + -NPC.direction * shotAngle / availableAngle * availableHeight;

                        Projectile.NewProjectile(NPC.GetSource_FromThis(), arenaCenter + new Vector2(-1800f * NPC.direction, 0f).RotatedBy(shotAngle), new Vector2(32f * NPC.direction, 0).RotatedBy(shotAngle), ModContent.ProjectileType<MothronProjectile>(), 80, 0f, Main.myPlayer, ai0: goalHeight, ai1: NPC.direction * 1500 - NPC.direction * 5 * (NPC.ai[1] - 60) + arenaCenter.X);
                    }
                }

                Vector2 goalPosition = new Vector2(NPC.ai[2], NPC.ai[3]) + new Vector2(-NPC.direction, 0) * 1080;

                FlyToPoint(goalPosition, Vector2.Zero, 0.25f, 0.25f);
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 600)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void HeavenPetAttackHard()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] == 60 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int proj = Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<HeavenPetProjectile>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI, ai1: 0f);
                Main.projectile[proj].localAI[1] = NPC.localAI[0];

                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataLastPrism>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI, ai1: 0f);
            }
            Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 360;

            NPC.spriteDirection = NPC.direction;

            FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);

            NPC.ai[1]++;
            if (NPC.ai[1] == 600)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void AncientDoomMinefield()
        {
            Player player = Main.player[NPC.target];

            int numRings = 16;
            int period = 4;

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else if (((int)NPC.ai[1] - 60) / period <= numRings)
            {
                NPC.velocity = Vector2.Zero;

                //shoot inward-spiraling fireballs
                if (NPC.ai[1] == 60)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossFireballRing>(), 80, 0f, Main.myPlayer, ai0: 1200, ai1: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossFireballRing>(), 80, 0f, Main.myPlayer, ai0: 1400, ai1: -1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossFireballRing>(), 80, 0f, Main.myPlayer, ai0: 1600, ai1: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossFireballRing>(), 80, 0f, Main.myPlayer, ai0: 1800, ai1: -1);
                }

                //make a whole boatload of mines in sequence
                if ((NPC.ai[1] - 60) % period == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int i = ((int)NPC.ai[1] - 60) / period;

                    //ai0 is radius multiplier, ai1 is rotation
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossMine>(), 80, 0f, Main.myPlayer, ai0: 0, ai1: i);
                }

                if (NPC.ai[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Zombie89, NPC.Center);
                }
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 60;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.065f, maxYAcc: 0.065f);

                if (NPC.ai[1] == (1 + numRings) * period + 60)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBusterSword>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI, ai1: 1f);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 540)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void IceScythesAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.05f, maxYAcc: 0.05f);

                if (NPC.ai[1] == 59)
                {
                    //RoD dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));

                    NPC.Center = goalPosition;
                    NPC.velocity = Vector2.Zero;

                    //RoD arrival dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));
                    teleportTime = 1f;
                }
            }
            else
            {
                NPC.velocity = Vector2.Zero;

                if (NPC.ai[1] == 60 || NPC.ai[1] == 240 || NPC.ai[1] == 420 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShield>(), 80, 0f, Main.myPlayer);
                    for (int i = -1; i <= 1; i++)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<RotatingIceShards>(), 80, 0f, Main.myPlayer, ai0: i, ai1: 0f);
                    }
                }

                if (NPC.ai[1] == 120 || NPC.ai[1] == 300 || NPC.ai[1] == 480 || NPC.ai[1] == 660 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, NPC.DirectionTo(player.Center) * 0.5f, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 2);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, NPC.DirectionTo(player.Center).RotatedBy(MathHelper.PiOver2) * 0.5f, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 2);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, NPC.DirectionTo(player.Center).RotatedBy(-MathHelper.PiOver2) * 0.5f, ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 2);
                }

                if (NPC.ai[1] == 60 || NPC.ai[1] == 240 || NPC.ai[1] == 420)
                {
                    SoundEngine.PlaySound(SoundID.Zombie88, NPC.Center);
                }
                if (NPC.ai[1] == 120 || NPC.ai[1] == 300 || NPC.ai[1] == 480)
                {
                    SoundEngine.PlaySound(SoundID.Item120, NPC.Center);
                }
                if (NPC.ai[1] == 150 || NPC.ai[1] == 330 || NPC.ai[1] == 510 || NPC.ai[1] == 690)
                {
                    SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                }

                if (NPC.ai[1] % 10 == 0 && NPC.ai[1] < 780 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IceShardArena>(), 80, 0f, Main.myPlayer, ai0: -1);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 900)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void AncientDoomMinefieldHard()
        {
            Player player = Main.player[NPC.target];

            int numRings = 9;
            int period = 8;

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.05f, maxYAcc: 0.05f);

                if (NPC.ai[1] == 59)
                {
                    //RoD dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));

                    NPC.Center = goalPosition;
                    NPC.velocity = Vector2.Zero;

                    //RoD arrival dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));
                    teleportTime = 1f;
                }
            }
            else if (((int)NPC.ai[1] - 60) / period <= numRings)
            {
                NPC.velocity = Vector2.Zero;

                if (NPC.ai[1] == 60 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SigilArena>(), 80, 0f, Main.myPlayer, ai0: 600);
                }

                //make a whole boatload of mines in sequence
                if ((NPC.ai[1] - 60) % period == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int i = ((int)NPC.ai[1] - 60) / period;

                    //ai0 is radius multiplier, ai1 is rotation
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBossMine2>(), 80, 0f, Main.myPlayer, ai0: 0, ai1: i);
                }

                if (NPC.ai[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Zombie89, NPC.Center);
                }
            }
            else
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 60;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.065f, maxYAcc: 0.065f);

                if (NPC.ai[1] == (1 + numRings) * period + 60)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CataBusterSword>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI, ai1: 2f);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 720)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void FishronsMothsAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] < 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.05f, maxYAcc: 0.05f);
            }
            else
            {
                if (NPC.ai[1] == 60)
                {
                    SoundEngine.PlaySound(SoundID.Item81, NPC.Center);

                    onSlimeMount = true;

                    NPC.width = 40;
                    NPC.position.X -= 11;
                    NPC.height = 64;
                    DrawOffsetY = -15;

                    //mount dusts from slime mount
                    for (int i = 0; i < 100; i++)
                    {
                        int num2 = Dust.NewDust(new Vector2(NPC.position.X - 20f, NPC.position.Y), NPC.width + 40, NPC.height, DustID.BlueFairy);
                        Main.dust[num2].scale += (float)Main.rand.Next(-10, 21) * 0.01f;
                        Main.dust[num2].noGravity = true;
                        Dust obj2 = Main.dust[num2];
                        obj2.velocity += NPC.velocity * 0.8f;
                    }
                }

                NPC.velocity.Y += 0.6f;

                if (NPC.ai[1] % 120 == 30)
                {
                    NPC.velocity.Y = -32f;
                }
                else if (NPC.ai[1] % 120 == 60)
                {
                    float direction = player.velocity.X > 0 ? 1 : -1;

                    //RoD dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));

                    NPC.position = player.Center + new Vector2(-direction * 480, -2430);
                    NPC.velocity = Vector2.Zero;

                    //RoD arrival dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));
                    teleportTime = 1f;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromThis(), player.Center + new Vector2(direction * 2400, 0), new Vector2(-direction * 32, 0), ModContent.ProjectileType<DoomedFishron>(), 80, 0f, Main.myPlayer);
                        for (int i = 1; i <= 50; i++)
                        {
                            float yOffset = (float)(Math.Sqrt(1.5f * i - 0.5f)) * 256;
                            Projectile.NewProjectile(NPC.GetSource_FromThis(), player.Center + new Vector2(direction * 2400 - 0.5f * direction * Math.Abs(yOffset), yOffset), new Vector2(-direction * 32, 0), ModContent.ProjectileType<MothProjectile>(), 80, 0f, Main.myPlayer);
                            Projectile.NewProjectile(NPC.GetSource_FromThis(), player.Center + new Vector2(direction * 2400 - 0.5f * direction * Math.Abs(yOffset), -yOffset), new Vector2(-direction * 32, 0), ModContent.ProjectileType<MothProjectile>(), 80, 0f, Main.myPlayer);
                        }
                    }
                }
            }

            if (NPC.ai[1] == 779)
            {
                onSlimeMount = false;

                NPC.width = 18;
                NPC.position.X += 11;
                NPC.height = 40;
                DrawOffsetY = -5;
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 780)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void MothronsAndLampCircularAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.ai[1] <= 60)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 720;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.05f, maxYAcc: 0.05f);

                if (NPC.ai[1] == 60)
                {
                    //RoD dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));

                    NPC.Center = goalPosition;
                    NPC.velocity = Vector2.Zero;

                    //RoD arrival dusts
                    Main.TeleportEffect(NPC.Hitbox, 1, 0, MathHelper.Clamp(1f - teleportTime * 0.99f, 0.01f, 1f));
                    teleportTime = 1f;

                    NPC.ai[2] = NPC.Center.X;
                    NPC.ai[3] = NPC.Center.Y;
                }
            }

            //mothron singing sound
            if (NPC.ai[1] == 120)
            {
                SoundEngine.PlaySound(SoundID.Zombie73.WithVolumeScale(2f).WithPitchOffset(-1), NPC.Center);
            }

            if (NPC.ai[1] == 60 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SigilArena>(), 80, 0f, Main.myPlayer, ai0: 900);
                Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CelestialLamp>(), 80, 0f, Main.myPlayer);
            }
            if (NPC.ai[1] % 20 == 0 && NPC.ai[1] >= 120 && NPC.ai[1] < 900 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 7; i++)
                {
                    float rotation = (float)Math.Sin(NPC.ai[1] / 60f + NPC.ai[1] * NPC.ai[1] / 108000f) / 2f + i * MathHelper.TwoPi / 7;

                    Projectile.NewProjectile(NPC.GetSource_FromThis(), new Vector2(NPC.ai[2], NPC.ai[3]) + new Vector2(1200, 960).RotatedBy(rotation), new Vector2(0, -32).RotatedBy(rotation), ModContent.ProjectileType<MothronSpiralProjectile>(), 80, 0f, Main.myPlayer, ai0: 1);
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), new Vector2(NPC.ai[2], NPC.ai[3]) + new Vector2(1200, -960).RotatedBy(rotation), new Vector2(0, 32).RotatedBy(rotation), ModContent.ProjectileType<MothronSpiralProjectile>(), 80, 0f, Main.myPlayer, ai0: -1);
                }
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 1740)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        private void MegaSprocketVsMegaBaddySuperCinematicDesperationAttack()
        {
            Player player = Main.player[NPC.target];

            if (NPC.localAI[0] == 0)
            {
                if (Main.rand.Next(1920) < NPC.ai[1])
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, ModContent.DustType<ShadowDust>());
            }

            if (NPC.ai[1] == 60)
            {
                NPC.ai[2] = player.Center.X;
                NPC.ai[3] = player.Center.Y;

                Main.LocalPlayer.AddBuff(ModContent.BuffType<Buffs.MysteriousPresence>(), 1560);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(NPC.GetSource_FromThis(), new Vector2(NPC.ai[2], NPC.ai[3]), Vector2.Zero, ModContent.ProjectileType<MegaBaddy>(), 80, 0f, Main.myPlayer, ai0: player.whoAmI);
                }
            }
            else if (NPC.ai[1] == 120 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int proj = Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center, (new Vector2(NPC.ai[2], NPC.ai[3]) - NPC.Center) / 59f, ModContent.ProjectileType<MegaSprocket>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI, ai1: 2f);
                Main.projectile[proj].localAI[1] = NPC.localAI[0];
            }

            if (NPC.ai[1] < 120)
            {
                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = player.Center + (NPC.Center - player.Center).SafeNormalize(Vector2.Zero) * 240;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else if (NPC.ai[1] < 1620)
            {
                /*npc.direction = -player.direction;
                npc.spriteDirection = npc.direction;
                Vector2 goalPosition = new Vector2(npc.ai[2], npc.ai[3]) * 2 - player.Center;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);*/

                if (Math.Abs(player.Center.X - NPC.Center.X) > 8)
                    NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;
                NPC.spriteDirection = NPC.direction;
                Vector2 goalPosition = new Vector2(NPC.ai[2], NPC.ai[3]) + (new Vector2(NPC.ai[2], NPC.ai[3]) - player.Center).RotatedBy(-MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 50;

                FlyToPoint(goalPosition, Vector2.Zero, maxXAcc: 0.5f, maxYAcc: 0.5f);
            }
            else
            {
                NPC.velocity = Vector2.Zero;
            }

            NPC.ai[1]++;
            if (NPC.ai[1] == 1920)
            {
                NPC.ai[1] = 0;
                NPC.ai[0]++;
            }
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return canShieldBonk || (onSlimeMount && NPC.velocity.Y > target.velocity.Y);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            if (canShieldBonk)
            {
                NPC.width = 18;
                NPC.position.X += 11;

                NPC.direction *= -1;
                NPC.velocity.X += NPC.direction * 30;
                canShieldBonk = false;
            }
            else if (onSlimeMount)
            {
                NPC.velocity.Y = -24f;

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<FishronPlatform>() && Main.projectile[i].ai[1] == 0)
                    {
                        Main.projectile[i].ai[1] = 1;

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            float determinant = Math.Max(0, (NPC.velocity.Y - target.velocity.Y) * (NPC.velocity.Y - target.velocity.Y) - 4 * 0.45f * (NPC.Center.Y - target.Center.Y + 240));
                            float eta = Math.Max(3, (-(NPC.velocity.Y - target.velocity.Y) + (float)Math.Sqrt(determinant)) / 0.9f);
                            float speed = 28f;
                            Vector2 targetPoint = new Vector2(NPC.Center.X + NPC.velocity.X * eta, target.Center.Y + 240 + target.velocity.Y * eta);
                            Vector2 shotPosition = targetPoint + new Vector2(NPC.direction * eta * speed, 0);
                            Vector2 shotVelocity = (targetPoint - shotPosition) / eta;

                            Projectile.NewProjectile(NPC.GetSource_FromThis(), shotPosition, shotVelocity, ModContent.ProjectileType<FishronPlatform>(), 80, 0f, Main.myPlayer, ai0: NPC.whoAmI);
                        }

                        break;
                    }
                }
            }
        }

        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            if (hitDialogueCooldown == 0)
            {
                CombatText.NewText(NPC.getRect(), auraColor, "Save your energy, you can't hurt me.", true);
                hitDialogueCooldown = 120;
            }
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (hitDialogueCooldown == 0)
            {
                if (projectile.CountsAsClass(DamageClass.Ranged))
                {
                    CombatText.NewText(NPC.getRect(), auraColor, "Save your ammunition, it can't break my shield.", true);
                }
                else if (projectile.CountsAsClass(DamageClass.Magic))
                {
                    CombatText.NewText(NPC.getRect(), auraColor, "Wasting mana is all you're doing here.", true);
                }
                else if (projectile.minion || ProjectileID.Sets.MinionShot[projectile.type] || projectile.sentry || ProjectileID.Sets.SentryShot[projectile.type])
                {
                    CombatText.NewText(NPC.getRect(), auraColor, "Call off your minions, they won't target me.", true);
                }
                else if (projectile.CountsAsClass(DamageClass.Throwing))
                {
                    CombatText.NewText(NPC.getRect(), auraColor, "Throwing? Post-Moon Lord? Really?", true);
                }
                else
                {
                    CombatText.NewText(NPC.getRect(), auraColor, "Save your energy, you can't hurt me.", true);
                }
                hitDialogueCooldown = 120;
            }
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            modifiers.SetMaxDamage(1);
            iceShieldCooldown = 60;
        }

        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position)
        {
            return false;
        }

        public override void FindFrame(int frameHeight)
        {
            if (onSlimeMount)
            {
                NPC.frameCounter = 0;
                NPC.frame.Y = frameHeight * 5;
            }
            else
            {
                NPC.frameCounter++;
                if (NPC.frameCounter == 3)
                {
                    NPC.frameCounter = 0;
                    NPC.frame.Y += frameHeight;
                }
                if (NPC.frame.Y >= frameHeight * 5)
                {
                    NPC.frame.Y = 0;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int trailLength = 1;
            if (canShieldBonk || onSlimeMount)
            {
                trailLength = 5;
            }

            for (int i = trailLength - 1; i >= 0; i--)
            {
                if (i == 0 || i % 2 == (int)NPC.ai[1] % 2)
                {
                    float alpha = (trailLength - i) / (float)trailLength;
                    Vector2 center = NPC.oldPos[i] + new Vector2(NPC.width, NPC.height) / 2;

                    SpriteEffects effects;

                    if (drawSpawnTransitionRing)
                    {
                        Color useColor = useRainbowColorTransition ? Main.hslToRgb((auraCounter / 120f) % 1, 1f, 0.5f) : spawnTransitionColor;

                        Texture2D ringTexture = TextureAssets.Projectile[490].Value;
                        Texture2D ringTexture2 = TextureAssets.Extra[34].Value;
                        Rectangle frame = ringTexture.Frame();
                        Rectangle frame2 = ringTexture2.Frame();
                        effects = SpriteEffects.None;

                        float rotation = (NPC.ai[1] - 180) / 20f;
                        float alphaModifier = ((NPC.ai[1] - 180) / 120f) * ((NPC.ai[1] - 180) / 120f);
                        float scaleModifier = 1 - (NPC.ai[1] - 180) / 120f;

                        for (int j = -1; j < 3; j++)
                        {
                            Main.EntitySpriteDraw(ringTexture, center - Main.screenPosition, frame, useColor * alpha * alphaModifier * (float)Math.Pow(0.5, j), rotation, frame.Size() / 2f, scaleModifier * (float)Math.Pow(2, j), effects, 0f);
                            Main.EntitySpriteDraw(ringTexture2, center - Main.screenPosition, frame2, useColor * alpha * alphaModifier * (float)Math.Pow(0.5, j - 0.5), -rotation, frame2.Size() / 2f, scaleModifier * (float)Math.Pow(2, j), effects, 0f);
                        }

                        Texture2D silhouetteTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossSilhouette").Value;
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        Vector2 silhouetteOffset = new Vector2(-NPC.spriteDirection * 3, DrawOffsetY);

                        if (NPC.localAI[0] == 0)
                        {
                            silhouetteTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowCataBossSilhouette").Value;
                        }

                        for (int j = 1; j <= 6; j++)
                        {
                            for (int k = 0; k < 4; k++)
                            {
                                Vector2 individualOffset = new Vector2(j * scaleModifier * 128f, 0).RotatedBy(k * MathHelper.TwoPi / 4 + j * MathHelper.TwoPi / 8);

                                Main.EntitySpriteDraw(silhouetteTexture, center - Main.screenPosition + silhouetteOffset + individualOffset, NPC.frame, useColor * alpha * alphaModifier * ((7 - j) / 6f) * 0.5f, 0f, NPC.frame.Size() / 2f, 1f, effects, 0f);
                            }
                        }
                    }

                    if (drawAura)
                    {
                        Color useColor = useRainbowColorAura ? Main.hslToRgb((auraCounter / 120f) % 1, 1f, 0.5f) : auraColor;

                        Texture2D silhouetteTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossSilhouette").Value;
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        Vector2 silhouetteOffset = new Vector2(-NPC.spriteDirection * 3, DrawOffsetY);

                        if (NPC.localAI[0] == 0)
                        {
                            silhouetteTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowCataBossSilhouette").Value;
                        }

                        for (int k = 0; k < 8; k++)
                        {
                            Vector2 individualOffset = new Vector2(4, 0).RotatedBy(k * MathHelper.TwoPi / 8 + auraCounter / 20f);

                            Main.EntitySpriteDraw(silhouetteTexture, center - Main.screenPosition + silhouetteOffset + individualOffset, NPC.frame, useColor * alpha * 0.5f, 0f, NPC.frame.Size() / 2f, 1f, effects, 0f);
                        }
                    }

                    if (onSlimeMount)
                    {
                        Texture2D mountTexture = ModContent.Request<Texture2D>("Terraria/Images/Mount_Slime").Value;
                        Rectangle frame = mountTexture.Frame(1, 4, 0, 1);
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                        Vector2 mountOffset = new Vector2(NPC.spriteDirection * 0, 10);
                        Main.EntitySpriteDraw(mountTexture, center - Main.screenPosition + mountOffset, frame, Color.White * alpha, 0f, frame.Size() / 2f, 1f, effects, 0f);
                    }

                    Texture2D npcTexture = TextureAssets.Npc[NPC.type].Value;

                    if (NPC.localAI[0] == 0)
                    {
                        npcTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowCataBoss").Value;
                    }

                    effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    Vector2 npcOffset = new Vector2(-NPC.spriteDirection * 3, DrawOffsetY);
                    Main.EntitySpriteDraw(npcTexture, center - Main.screenPosition + npcOffset, NPC.frame, Color.White * alpha, 0f, NPC.frame.Size() / 2f, 1f, effects, 0f);

                    if (holdingShield)
                    {
                        Texture2D shieldTexture = ModContent.Request<Texture2D>("Terraria/Images/Acc_Shield_5").Value;
                        Rectangle frame = shieldTexture.Frame(1, 20);
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                        Vector2 shieldOffset = new Vector2(NPC.spriteDirection * 3, -4);

                        Main.EntitySpriteDraw(shieldTexture, center - Main.screenPosition + shieldOffset, frame, Color.White * alpha, 0f, frame.Size() / 2f, 1f, effects, 0f);
                    }

                    if (drawEyeTrail)
                    {
                        float eyeTrailLength = 20;
                        Texture2D eyeTexture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossEyeGlow").Value;
                        Rectangle frame = eyeTexture.Frame();
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        Vector2 eyeOffset = new Vector2(NPC.spriteDirection * 4, DrawOffsetY - 12);
                        Color useColor = useRainbowColorAura ? Main.hslToRgb((auraCounter / 120f) % 1, 1f, 0.5f) : auraColor;

                        for (int j = 0; j < eyeTrailLength; j++)
                        {
                            float scale = (eyeTrailLength - j) / eyeTrailLength;
                            Main.EntitySpriteDraw(eyeTexture, NPC.oldPos[i + j] + new Vector2(NPC.width, NPC.height) / 2 - Main.screenPosition + eyeOffset, frame, useColor * (alpha * scale * 0.75f), 0f, frame.Size() / 2f, scale, effects, 0f);
                            float scale2 = (eyeTrailLength - j - 0.5f) / eyeTrailLength;
                            Main.EntitySpriteDraw(eyeTexture, (NPC.oldPos[i + j] + NPC.oldPos[i + j + 1]) / 2 + new Vector2(NPC.width, NPC.height) / 2 - Main.screenPosition + eyeOffset, frame, useColor * (alpha * scale2 * 0.75f), 0f, frame.Size() / 2f, scale2, effects, 0f);
                        }
                    }

                    //draw RoD
                    if (teleportTime > 0.9f)
                    {
                        Texture2D rodTexture = TextureAssets.Item[ItemID.RodofDiscord].Value;
                        Rectangle frame = rodTexture.Frame();
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        Vector2 drawCenter = NPC.spriteDirection == 1 ? new Vector2(frame.Width, frame.Height) : new Vector2(0, frame.Height);
                        Vector2 rodOffset = new Vector2(NPC.spriteDirection * 0, 0);
                        float rodRotation = -NPC.spriteDirection * (teleportTime * 10 - 9.5f) * MathHelper.Pi + NPC.spriteDirection * MathHelper.PiOver2;

                        Main.EntitySpriteDraw(rodTexture, center - Main.screenPosition + rodOffset, frame, Color.White * alpha, rodRotation, drawCenter, 1f, effects, 0f);
                    }

                    if (iceShieldCooldown > 0)
                    {
                        Texture2D shieldTexture = TextureAssets.Projectile[464].Value;
                        Rectangle frame = shieldTexture.Frame();
                        effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                        float shieldAlpha = iceShieldCooldown / 120f;
                        Vector2 shieldOffset = new Vector2(0, -2);

                        Main.EntitySpriteDraw(shieldTexture, center - Main.screenPosition + shieldOffset, frame, Color.White * shieldAlpha * alpha, 0f, frame.Size() / 2f, 1f, effects, 0f);
                    }
                }
            }

            return false;
        }

        public override bool CheckDead()
        {
            if (killable)
            {
                //doesn't actually happen yet
                if (NPC.localAI[0] == 1)
                {
                    DTWorld.DownedCataBoss = true;
                    NPC.NewNPC(NPC.GetSource_FromThis(), (int)NPC.position.X, (int)NPC.position.Y + NPC.height / 2, ModContent.NPCType<CataclysmicArmageddon>());
                }
                return true;
            }
            NPC.life = NPC.lifeMax;
            return false;
        }

        public override bool CheckActive()
        {
            return false;
        }
    }

    public class CataBossScythe : ModProjectile
    {
        //demon scythe but no dust and it passes through tiles
        public override string Texture => "DeathsTerminus/NPCs/CataBoss/CataDemonScythe"; //"Terraria/Images/Projectile_" + ProjectileID.DemonSickle;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Demon Scythe");
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
            /*Texture2D texture = new Texture2D(Main.spriteBatch.GraphicsDevice, 64, 128, false, SurfaceFormat.Color);
			System.Collections.Generic.List<Color> list = new System.Collections.Generic.List<Color>();
			for (int j = 0; j < texture.Height; j++)
			{
				for (int i = 0; i < texture.Width; i++)
				{
					float x = i / (float)(texture.Width - 1);
                    float y = j / (float)(texture.Height - 1);

                    int r = 255;
					int g = 255;
					int b = 255;
					int alpha = (int)(255 * (1 - x) * 4 * y * (1 - y));

					list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
				}
			}
			texture.SetData(list.ToArray());
			texture.SaveAsPng(new FileStream(Main.SavePath + Path.DirectorySeparatorChar + "CataBossScytheTelegraph.png", FileMode.Create), texture.Width, texture.Height);
            
            texture = new Texture2D(Main.spriteBatch.GraphicsDevice, 64, 128, false, SurfaceFormat.Color);
			list = new System.Collections.Generic.List<Color>();
			for (int j = 0; j < texture.Height; j++)
			{
				for (int i = 0; i < texture.Width; i++)
				{
					float x = i / (float)(texture.Width - 1);
                    float y = j / (float)(texture.Height - 1);

                    float radiusSquared = (1 + x * x + 4 * y * (y - 1));

                    int r = 255;
					int g = 255;
					int b = 255;
					int alpha = radiusSquared > 1 ? 0 : (int)(255 * (1 - radiusSquared));

					list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
				}
			}
			texture.SetData(list.ToArray());
			texture.SaveAsPng(new FileStream(Main.SavePath + Path.DirectorySeparatorChar + "CataBossTelegraphCap.png", FileMode.Create), texture.Width, texture.Height);*/
        }

        public override void SetDefaults()
        {
            Projectile.width = 42; //48;
            Projectile.height = 42; //48;
            Projectile.alpha = 32;
            Projectile.light = 0.2f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;//0.9f;
            Projectile.timeLeft = 130;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation += Projectile.direction * 0.8f;
            Projectile.ai[0] += 1f;
            if (!(Projectile.ai[0] < 30f))
            {
                if (Projectile.ai[0] < 120f)
                {
                    Projectile.velocity *= 1.06f;
                }
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.ai[1] == 0)
            {
                Main.EntitySpriteDraw(TextureAssets.Projectile[Projectile.type].Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Purple * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(Projectile.velocity.Length(), Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Purple * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }
            else if (Projectile.ai[1] == 1)
            {
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataEclipseScythe").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Orange * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(Projectile.velocity.Length(), Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Orange * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }
            else if (Projectile.ai[1] == 2)
            {
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataCelestialScythe").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.LightBlue * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(Projectile.velocity.Length(), Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.LightBlue * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    public class CataBossSuperScythe : ModProjectile
    {
        //demon scythe but no dust and it passes through tiles
        public override string Texture => "DeathsTerminus/NPCs/CataBoss/CataDemonScythe"; //"Terraria/Images/Projectile_" + ProjectileID.DemonSickle;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Super Scythe");
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 42;//48;
            Projectile.height = 42;// 48;
            Projectile.alpha = 32;
            Projectile.light = 0.2f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.scale = 1f;// 0.9f;
            Projectile.timeLeft = 160;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation += Projectile.direction * 0.8f;
            Projectile.ai[0] += 1f;
            if (Projectile.ai[0] == 30f)
            {
                Projectile.velocity *= 60f;
            }

            if (Projectile.ai[0] >= 30 && (Projectile.ai[0] - 30) % 7 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity.SafeNormalize(Vector2.Zero) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai1: Projectile.ai[1]);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai1: Projectile.ai[1]);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(-MathHelper.PiOver2) * 0.5f, ModContent.ProjectileType<CataBossScythe>(), 80, 0f, Main.myPlayer, ai1: Projectile.ai[1]);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.ai[1] == 0)
            {
                Main.EntitySpriteDraw(TextureAssets.Projectile[Projectile.type].Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Purple * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(30, Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Purple * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }
            else if (Projectile.ai[1] == 1)
            {
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataEclipseScythe").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Orange * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(30, Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Orange * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }
            else if (Projectile.ai[1] == 2)
            {
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataCelestialScythe").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, TextureAssets.Projectile[Projectile.type].Value.Width, TextureAssets.Projectile[Projectile.type].Value.Height), Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(TextureAssets.Projectile[Projectile.type].Value.Width / 2f, TextureAssets.Projectile[Projectile.type].Value.Height / 2f), Projectile.scale, SpriteEffects.None, 0f);

                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.LightBlue * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(30, Projectile.width / 128f), SpriteEffects.None, 0f);
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.LightBlue * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    public class RotatingIceShards : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_35";

        private static int shardRadius = 12;
        private static int shardCount = 24;

        //ring of ice shards
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Ice Shard");
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 420;

            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Projectile.timeLeft <= 360)
            {
                float angle = MathHelper.TwoPi * Projectile.timeLeft / 360f;

                //set radius and rotation
                Projectile.localAI[1] = 600 * (float)Math.Sqrt(2 - 2 * Math.Cos(angle));
                Projectile.rotation = Projectile.ai[1] + Projectile.ai[0] * (angle + MathHelper.Pi) / 2;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < shardCount; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.localAI[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / shardCount);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < shardRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            for (int i = 0; i < shardCount; i++)
            {
                Rectangle frame = texture.Frame(1, 3);

                for (int j = Math.Min(Projectile.timeLeft, 360); j >= Math.Max(0, Projectile.timeLeft - 60); j--)
                {
                    float angle = MathHelper.TwoPi * j / 360f;
                    float radius = 600 * (float)Math.Sqrt(2 - 2 * Math.Cos(angle));
                    float rotation = Projectile.ai[1] + Projectile.ai[0] * (angle + MathHelper.Pi) / 2;
                    float alphaMultiplier = Math.Max(0, (60 - Projectile.timeLeft + j) / 60f);

                    Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + new Vector2(radius * Projectile.scale, 0).RotatedBy(rotation + i * MathHelper.TwoPi / shardCount), frame, Color.White * alphaMultiplier * 0.03f, rotation + i * MathHelper.TwoPi / shardCount - MathHelper.PiOver2 + Projectile.ai[0] * MathHelper.Pi * (1 + j / 360f), new Vector2(12, 37), Projectile.scale, SpriteEffects.None, 0f);
                }

                if (Projectile.timeLeft <= 360)
                    Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + new Vector2(Projectile.localAI[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / shardCount), frame, Color.White, Projectile.rotation + i * MathHelper.TwoPi / shardCount - MathHelper.PiOver2 + Projectile.ai[0] * MathHelper.Pi * (1 + Projectile.timeLeft / 360f), new Vector2(12, 37), Projectile.scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    public class IceShardArena : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_35";

        private static int shardRadius = 12;
        private static int shardCount = 24;

        //ring of ice shards
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Ice Shard");
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 200;

            Projectile.hide = true;
        }

        public override void AI()
        {
            //initial position = new Vector2(1200, ±2400)
            //velocity is new vector2(0, ±24)

            //set radius and rotation
            Vector2 positionOffset = new Vector2(1200, Projectile.ai[0] * 2400) + new Vector2(0, -Projectile.ai[0] * 24) * (200 - Projectile.timeLeft);

            Projectile.localAI[1] = positionOffset.Length();
            Projectile.rotation = positionOffset.ToRotation();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < shardCount; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.localAI[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / shardCount);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < shardRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            for (int i = 0; i < shardCount; i++)
            {
                Rectangle frame = texture.Frame(1, 3);

                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + new Vector2(Projectile.localAI[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / shardCount), frame, Color.White, MathHelper.PiOver2 * (Projectile.ai[0] + 1) + i * MathHelper.TwoPi / shardCount, new Vector2(12, 37), Projectile.scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    public class IceShard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_35";

        private static int shardRadius = 12;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Ice Shard");
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 200;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.velocity *= Projectile.ai[0];

            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 circleCenter = Projectile.Center;
            float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
            float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
            return new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < shardRadius;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 3);

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White, Projectile.rotation, new Vector2(12, 37), Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    public class IceShield : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_464";
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Ice Shield");
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.alpha = 96;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 60;
        }

        public override void AI()
        {
            Projectile.ai[0] += 0.01f;
            Projectile.rotation += Projectile.ai[0];
            Projectile.alpha = Projectile.timeLeft * 128 / 60;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 circleCenter = Projectile.Center;
            float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
            float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
            return new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame();

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, new Vector2(46, 51), Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    public class SigilArena : ModProjectile
    {
        private static int sigilRadius = 27;
        private static int sigilCount = 80;


        //the arena!
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Orbiting Sigil");
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.scale = 2f;
            Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.timeLeft = (int)Projectile.ai[0];
            }

            Projectile.scale = 1f;
            Projectile.hostile = true;

            //rotation increment
            Projectile.rotation += 0.01f;

            //set radius and center (replace with more dynamic AI later)
            Projectile.ai[1] = 1200 + Math.Max(0, 20 * (60 - Projectile.localAI[0])) + Math.Max(0, 20 * (60 - Projectile.timeLeft));

            if (Projectile.scale >= 1)
            {
                if ((Main.LocalPlayer.Center - Projectile.Center).Length() > Projectile.ai[1] * Projectile.scale)
                {
                    Vector2 normal = (Main.LocalPlayer.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Vector2 relativeVelocity = Main.LocalPlayer.velocity - Projectile.velocity;

                    Main.LocalPlayer.Center = Projectile.Center + normal * Projectile.ai[1] * Projectile.scale;

                    if (relativeVelocity.X * normal.X + relativeVelocity.Y * normal.Y > 0)
                    {
                        Main.LocalPlayer.velocity -= normal * (relativeVelocity.X * normal.X + relativeVelocity.Y * normal.Y);
                    }
                }
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 4)
                {
                    Projectile.frame = 0;
                }
            }

            Projectile.localAI[0]++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < sigilCount; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.ai[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < sigilRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            for (int i = 0; i < sigilCount; i++)
            {
                Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + new Vector2(Projectile.ai[1] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount), new Rectangle(0, 96 * Projectile.frame, 66, 96), Color.White * Projectile.scale, Projectile.rotation + i * MathHelper.TwoPi / sigilCount, new Vector2(33, 65), Projectile.scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    public class SunLamp : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Sun Lamp");
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
            /*
            int textureFramesX = 15;
            int textureFramesY = 4;
            int textureFrameWidth = 96;
            int textureFrameHeight = 603;
            Texture2D texture = new Texture2D(Main.spriteBatch.GraphicsDevice, 2 * textureFramesX * textureFrameWidth, textureFramesY * textureFrameHeight, false, SurfaceFormat.Color);
			System.Collections.Generic.List<Color> list = new System.Collections.Generic.List<Color>();
			for (int j = 0; j < texture.Height; j++)
			{
				for (int i = 0; i < texture.Width; i++)
				{
                    if (i < texture.Width / 2)
                    {
                        int frameX = i / textureFrameWidth;
                        int frameY = j / textureFrameHeight;
                        int frame = frameX + frameY * textureFramesX;
                        float x = Math.Abs(2 * (i % textureFrameWidth) / (float)(textureFrameWidth - 1) - 1);
                        float y = MathHelper.TwoPi * (j % textureFrameHeight) / (float)textureFrameHeight;

                        float waveFunction = (float)(
                                1 / 4f * Math.Cos(12 * (y + 12f) + frame * MathHelper.TwoPi / 12 + 12f) +
                                1 / 4f * Math.Cos(-15 * (y + 15f) + frame * MathHelper.TwoPi / 15 + 15f) +
                                1 / 4f * Math.Cos(-20 * (y + 20f) + frame * MathHelper.TwoPi / 20 + 20f) +
                                1 / 4f * Math.Cos(30 * (y + 30f) + frame * MathHelper.TwoPi / 30 + 30f)
                            );

                        float luminosityFactor = (float)Math.Pow((1 - Math.Pow(x, 4)), 2);
                        float waviness = (float)(0.5f * Math.Exp(-50 * Math.Pow(x - 0.8f, 2)));
                        float index = luminosityFactor * (1 - waviness * waveFunction);

                        int r = 255;
                        int g = 255 - (int)(64 * (1 - index));
                        int b = 255 - (int)(255 * (1 - index));
                        int alpha = (int)(255 * index);

                        list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
                    }
                    else
                    {
                        int frameX = (i - texture.Width) / textureFrameWidth;
                        int frameY = j / textureFrameHeight;
                        int frame = frameX + frameY * textureFramesX;
                        float x = Math.Abs(2 * (i % textureFrameWidth) / (float)(textureFrameWidth - 1) - 1);
                        float y = MathHelper.TwoPi * (j % textureFrameHeight) / (float)textureFrameHeight;

                        float waveFunction = (float)(
                                1 / 4f * Math.Cos(12 * (y + 12f) + frame * MathHelper.TwoPi / 12 + 12f) +
                                1 / 4f * Math.Cos(-15 * (y + 15f) + frame * MathHelper.TwoPi / 15 + 15f) +
                                1 / 4f * Math.Cos(-20 * (y + 20f) + frame * MathHelper.TwoPi / 20 + 20f) +
                                1 / 4f * Math.Cos(30 * (y + 30f) + frame * MathHelper.TwoPi / 30 + 30f)
                            );

                        float luminosityFactor = (float)Math.Pow((1 - Math.Pow(x, 4)), 2);
                        float waviness = (float)(0.5f * Math.Exp(-50 * Math.Pow(x - 0.8f, 2)));
                        float index = luminosityFactor * (1 - waviness * waveFunction);
                        float eclipseLuminosityFactor = (float)Math.Pow(1 - Math.Pow(1 - Math.Pow(x, 2), 64), Math.Pow(64, 4));

                        float hue = (index / 4f - 1 / 12f) % 1;
                        float saturation = (float)Math.Pow(eclipseLuminosityFactor, 2);
                        float luminosity = index * eclipseLuminosityFactor;
                        Color color = Main.hslToRgb(hue, saturation, luminosity);

                        int r = color.R;//448 - (int)(512 * index);
                        int g = color.G;//384 - (int)(512 * index);
                        int b = color.B;//256 - (int)(512 * index);
                        int alpha = x >= 1 ? 0 : (int)(255 * index);

                        list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
                    }
				}
			}
			texture.SetData(list.ToArray());
			texture.SaveAsPng(new FileStream(Main.SavePath + Path.DirectorySeparatorChar + "SunLamp.png", FileMode.Create), texture.Width, texture.Height);
            */
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 540;
            Projectile.scale = 0f;

            Projectile.hide = true;
        }

        public override void AI()
        {
            DelegateMethods.v3_1 = new Vector3(255 / 128f, 220 / 128f, 64 / 128f);
            Utils.PlotTileLine(Projectile.Center + new Vector2(0, 2048), Projectile.Center + new Vector2(0, -2048), 26, DelegateMethods.CastLight);

            if (Projectile.scale < 1f && Projectile.timeLeft > 60)
            {
                Projectile.scale += 1 / 60f;
            }
            else if (Projectile.timeLeft <= 60)
            {
                Projectile.scale -= 1 / 60f;
                Projectile.velocity.X *= 0.95f;
            }

            Projectile.direction = Projectile.velocity.X > 0 ? 1 : -1;

            if (Projectile.timeLeft > 60)
            {
                if (Projectile.Center.X < Main.LocalPlayer.Center.X ^ Projectile.direction == 1)
                {
                    Vector2 normal = new Vector2(Projectile.direction, 0);
                    Vector2 relativeVelocity = Main.LocalPlayer.velocity - Projectile.velocity;

                    Main.LocalPlayer.Center = new Vector2(Projectile.Center.X, Main.LocalPlayer.Center.Y);

                    if (relativeVelocity.X * normal.X + relativeVelocity.Y * normal.Y > 0)
                    {
                        Main.LocalPlayer.velocity -= normal * (relativeVelocity.X * normal.X + relativeVelocity.Y * normal.Y);
                    }
                }
            }

            Projectile.frame++;
            if ((Projectile.frame < 60 || Projectile.frame >= 120) && Projectile.ai[0] == 1)
            {
                Projectile.frame = 60;
            }
            else if (Projectile.frame >= 60 && Projectile.ai[0] == 0)
            {
                Projectile.frame = 0;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + new Vector2(0, 2048), Projectile.Center + new Vector2(0, -2048), 64 * Projectile.scale, ref point);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        //need to make wobble
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frame = texture.Frame(30, 4, Projectile.frame / 4, Projectile.frame % 4);

            for (int i = (int)((-Main.screenHeight + Main.screenPosition.Y - Projectile.Center.Y) / frame.Height - 1); i <= (int)((Main.screenHeight + Main.screenPosition.Y - Projectile.Center.Y) / frame.Height + 1); i++)
            {
                Main.EntitySpriteDraw(texture, new Vector2(0, frame.Height * i) + Projectile.Center - Main.screenPosition, frame, Color.White, Projectile.rotation, frame.Size() / 2, new Vector2(Projectile.scale, 1), SpriteEffects.None, 0f);
            }

            return false;
        }
    }

    public class BabyMothronProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_479";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Baby Mothron");
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 400;

            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Projectile.timeLeft > 400 - 33)
            {
                Projectile.velocity *= 0.95f;
            }
            else
            {
                Projectile.velocity.Y += Projectile.velocity.Y * Projectile.velocity.Y / (Projectile.Center.Y - Projectile.ai[0]);

                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 6;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.direction = Projectile.velocity.X > 0 ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            //test for death via ray of sunshine
            Projectile.ai[1] += 5 * Projectile.direction;
            if (Projectile.ai[1] < Projectile.Center.X ^ Projectile.direction == 1)
            {
                Projectile.Kill();
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 3)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCHit23, Projectile.Center);

            if (Main.netMode == NetmodeID.Server)
                return;
            Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 681, Projectile.scale);
            Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 682, Projectile.scale);
            Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 683, Projectile.scale);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 3, 0, Projectile.frame);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White, Projectile.rotation, frame.Size() / 2, Projectile.scale, effects, 0f);

            return false;
        }
    }

    public class HeavenPetProjectile : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Sprocket");
            Main.projFrames[Projectile.type] = 7;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private float cogRotation = 0;

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.alpha = 0;
            Projectile.light = 1f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 660;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.scale = Projectile.ai[1] + 1f;

            if (Projectile.timeLeft > 60)
            {
                Player player = Main.player[Main.npc[(int)Projectile.ai[0]].target];

                if (Projectile.timeLeft <= 600 - 60)
                {
                    Projectile.hostile = true;

                    Projectile.velocity -= new Vector2(0.25f, 0).RotatedBy(Projectile.rotation) / Projectile.scale;
                    Projectile.velocity *= 0.95f;

                    if (Projectile.timeLeft == 600 - 60)
                    {
                        SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);
                    }
                    else if (Projectile.timeLeft % 60 == 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item15, Projectile.Center);
                    }
                }
                else
                {
                    Projectile.hostile = false;

                    Projectile.velocity -= new Vector2(0.1f, 0).RotatedBy(Projectile.rotation);
                    Projectile.velocity *= 0.95f;
                }

                float maxTurn = 0.02f / Projectile.scale;

                float rotationOffset = (player.Center - Projectile.Center).ToRotation() - Projectile.rotation;
                while (rotationOffset > MathHelper.Pi)
                {
                    rotationOffset -= MathHelper.TwoPi;
                }
                while (rotationOffset < -MathHelper.Pi)
                {
                    rotationOffset += MathHelper.TwoPi;
                }
                if (rotationOffset > maxTurn)
                {
                    Projectile.rotation += maxTurn;
                }
                else if (rotationOffset < -maxTurn)
                {
                    Projectile.rotation -= maxTurn;
                }
                else
                {
                    Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
                }
            }
            else
            {
                Projectile.hostile = false;

                NPC boss = Main.npc[(int)Projectile.ai[0]];

                Projectile.velocity += (boss.Center + boss.velocity * Projectile.timeLeft - Projectile.Center - Projectile.velocity * Projectile.timeLeft) / (Projectile.timeLeft * Projectile.timeLeft);
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 7)
                {
                    Projectile.frame = 0;
                }
            }

            cogRotation += Projectile.velocity.X * 0.1f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + new Vector2(4096, 0).RotatedBy(Projectile.rotation), 22 * Projectile.scale, ref point);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle frame;
            SpriteEffects effects;

            //draw the pet
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            if (Projectile.localAI[1] == 0)
            {
                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/DarkPetProjectile").Value;
            }

            frame = texture.Frame(1, 7, 0, Projectile.frame);
            effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            float wingRotation = Projectile.velocity.X * 0.1f;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White, wingRotation, frame.Size() / 2, Projectile.scale, effects, 0f);

            Texture2D texture2 = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectile_Cog").Value;
            if (Projectile.localAI[1] == 0)
            {
                texture2 = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/DarkPetProjectile_Cog").Value;
            }

            frame = texture2.Frame();
            effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.EntitySpriteDraw(texture2, Projectile.Center - Main.screenPosition, frame, Color.White, cogRotation, frame.Size() / 2, Projectile.scale, effects, 0f);


            if (Projectile.timeLeft > 60)
            {
                //draw the prism
                //adapted from last prism drawcode
                Texture2D prismTexture = TextureAssets.Projectile[633].Value;
                frame = prismTexture.Frame(1, 5, 0, (Projectile.timeLeft / (Projectile.timeLeft <= 600 - 60 ? 1 : 3)) % 5);
                effects = SpriteEffects.None;
                Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(20 * Projectile.scale, 0).RotatedBy(Projectile.rotation);

                Main.EntitySpriteDraw(prismTexture, drawPosition, frame, Color.White, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);

                float scaleFactor2 = (float)Math.Cos(Math.PI * 2f * (Projectile.timeLeft / 30f)) * 2f + 2f;
                if (Projectile.timeLeft <= 600 - 60)
                {
                    scaleFactor2 = 4f;
                }
                for (float num350 = 0f; num350 < 4f; num350 += 1f)
                {
                    Main.EntitySpriteDraw(prismTexture, drawPosition + new Vector2(0, 1).RotatedBy(num350 * (Math.PI * 2f) / 4f) * scaleFactor2, frame, Color.White.MultiplyRGBA(new Color(255, 255, 255, 0)) * 0.03f, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);
                }

                if (Projectile.timeLeft > 600 - 60)
                {
                    //draw the telegraph line
                    float telegraphAlpha = (60 + Projectile.timeLeft - 600) / 30f * (600 - Projectile.timeLeft) / 30f;
                    Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectileTelegraph").Value, Projectile.Center + new Vector2(30 * Projectile.scale, 0).RotatedBy(Projectile.rotation) - Main.screenPosition, new Rectangle(0, 0, 1, 1), Color.White * telegraphAlpha, Projectile.rotation, new Vector2(0, 0.5f), new Vector2(4096, 1), SpriteEffects.None, 0f);
                }
                else
                {
                    //draw the beam
                    //adapted from last prism drawcode

                    for (int i = 0; i < 6; i++)
                    {
                        //texture
                        Texture2D tex7 = TextureAssets.Projectile[632].Value;
                        //laser length
                        float num528 = 4096;

                        Color value42 = Main.hslToRgb(i / 6f, 1f, 0.5f);
                        value42.A = 0;

                        Vector2 drawOffset = new Vector2(4, 0).RotatedBy(Projectile.timeLeft * 0.5f + i * MathHelper.TwoPi / 6);

                        //start position
                        Vector2 value45 = Projectile.Center.Floor() + drawOffset + new Vector2(36 * Projectile.scale, 0).RotatedBy(Projectile.rotation);

                        value45 += Vector2.UnitX.RotatedBy(Projectile.rotation) * Projectile.scale * 10.5f;
                        num528 -= Projectile.scale * 14.5f * Projectile.scale;
                        Vector2 vector90 = new Vector2(Projectile.scale);
                        DelegateMethods.f_1 = 1f;
                        DelegateMethods.c_1 = value42 * 0.75f * Projectile.Opacity;
                        _ = Projectile.oldPos[0] + new Vector2((float)Projectile.width, (float)Projectile.height) / 2f + Vector2.UnitY * Projectile.gfxOffY - Main.screenPosition;
                        Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90, DelegateMethods.RainbowLaserDraw);
                        DelegateMethods.c_1 = new Color(255, 255, 255, 127) * 0.75f * Projectile.Opacity;
                        Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90 / 2f, DelegateMethods.RainbowLaserDraw);
                    }
                }
            }

            return false;
        }
    }

    public class CataLastPrism : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_633";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Last Prism");
            Main.projFrames[Projectile.type] = 5;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.alpha = 0;
            Projectile.light = 1f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            Projectile.scale = Projectile.ai[1] + 1f;

            Player player = Main.player[Main.npc[(int)Projectile.ai[0]].target];

            if (Projectile.timeLeft <= 540 - 60)
            {
                Projectile.hostile = true;

                Projectile.Center = Main.npc[(int)Projectile.ai[0]].Center;

                if (Projectile.timeLeft == 540 - 60)
                {
                    SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);
                }
                else if (Projectile.timeLeft % 60 == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item15, Projectile.Center);
                }
            }
            else
            {
                Projectile.hostile = false;

                Projectile.Center = Main.npc[(int)Projectile.ai[0]].Center;
            }

            float maxTurn = 0.0175f / Projectile.scale;

            float rotationOffset = (player.Center - Projectile.Center).ToRotation() - Projectile.rotation;
            while (rotationOffset > MathHelper.Pi)
            {
                rotationOffset -= MathHelper.TwoPi;
            }
            while (rotationOffset < -MathHelper.Pi)
            {
                rotationOffset += MathHelper.TwoPi;
            }
            if (rotationOffset > maxTurn)
            {
                Projectile.rotation += maxTurn;
            }
            else if (rotationOffset < -maxTurn)
            {
                Projectile.rotation -= maxTurn;
            }
            else
            {
                Projectile.rotation = (player.Center - Projectile.Center).ToRotation();
            }

            while (Projectile.rotation >= MathHelper.TwoPi)
            {
                Projectile.rotation -= MathHelper.TwoPi;
            }
            while (Projectile.rotation < 0)
            {
                Projectile.rotation += MathHelper.TwoPi;
            }

            Main.npc[(int)Projectile.ai[0]].direction = (Projectile.rotation > MathHelper.PiOver2 && Projectile.rotation < 3 * MathHelper.PiOver2) ? -1 : 1;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + new Vector2(4096, 0).RotatedBy(Projectile.rotation), 44 * Projectile.scale, ref point);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle frame;
            SpriteEffects effects;

            //draw the prism
            //adapted from last prism drawcode
            Texture2D prismTexture = TextureAssets.Projectile[Projectile.type].Value;
            frame = prismTexture.Frame(1, 5, 0, (Projectile.timeLeft / (Projectile.timeLeft <= 600 - 60 ? 1 : 3)) % 5);
            effects = SpriteEffects.None;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(20 * Projectile.scale, 0).RotatedBy(Projectile.rotation);

            Main.EntitySpriteDraw(prismTexture, drawPosition, frame, Color.White, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);

            float scaleFactor2 = (float)Math.Cos(Math.PI * 2f * (Projectile.timeLeft / 30f)) * 2f + 2f;
            if (Projectile.timeLeft <= 540 - 60)
            {
                scaleFactor2 = 4f;
            }
            for (float num350 = 0f; num350 < 4f; num350 += 1f)
            {
                Main.EntitySpriteDraw(prismTexture, drawPosition + new Vector2(0, 1).RotatedBy(num350 * (Math.PI * 2f) / 4f) * scaleFactor2, frame, Color.White.MultiplyRGBA(new Color(255, 255, 255, 0)) * 0.03f, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            if (Projectile.timeLeft > 540 - 60)
            {
                //draw the telegraph line
                float telegraphAlpha = (60 + Projectile.timeLeft - 540) / 30f * (540 - Projectile.timeLeft) / 30f;
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectileTelegraph").Value, Projectile.Center + new Vector2(30 * Projectile.scale, 0).RotatedBy(Projectile.rotation) - Main.screenPosition, new Rectangle(0, 0, 1, 1), Color.White * telegraphAlpha, Projectile.rotation, new Vector2(0, 0.5f), new Vector2(4096, 1), SpriteEffects.None, 0f);
            }
            else
            {
                //draw the beam
                //adapted from last prism drawcode

                for (int i = 0; i < 6; i++)
                {
                    //texture
                    Texture2D tex7 = TextureAssets.Projectile[632].Value;
                    //laser length
                    float num528 = 4096;

                    Color value42 = Main.hslToRgb(i / 6f, 1f, 0.5f);
                    value42.A = 0;

                    Vector2 drawOffset = new Vector2(4, 0).RotatedBy(Projectile.timeLeft * 0.5f + i * MathHelper.TwoPi / 6);

                    //start position
                    Vector2 value45 = Projectile.Center.Floor() + drawOffset + new Vector2(46 * Projectile.scale, 0).RotatedBy(Projectile.rotation);

                    value45 += Vector2.UnitX.RotatedBy(Projectile.rotation) * Projectile.scale * 10.5f;
                    num528 -= Projectile.scale * 14.5f * Projectile.scale;
                    Vector2 vector90 = new Vector2(Projectile.scale * 2);
                    DelegateMethods.f_1 = 1f;
                    DelegateMethods.c_1 = value42 * 0.75f * Projectile.Opacity;
                    _ = Projectile.oldPos[0] + new Vector2((float)Projectile.width, (float)Projectile.height) / 2f + Vector2.UnitY * Projectile.gfxOffY - Main.screenPosition;
                    Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90, DelegateMethods.RainbowLaserDraw);
                    DelegateMethods.c_1 = new Color(255, 255, 255, 127) * 0.75f * Projectile.Opacity;
                    Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90 / 2f, DelegateMethods.RainbowLaserDraw);
                }
            }

            return false;
        }
    }

    public class FishronPlatform : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_370";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Sitting Duke");
            Main.projFrames[Projectile.type] = 8;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        float bubbleShotProgress = 0f;
        int bubbleCount = 0;

        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 400;

            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Projectile.ai[1] == 0)
            {
                NPC boss = Main.npc[(int)Projectile.ai[0]];
                Player player = Main.player[boss.target];

                float determinant = Math.Max(0, (boss.velocity.Y - player.velocity.Y) * (boss.velocity.Y - player.velocity.Y) - 4 * 0.45f * (boss.Center.Y - player.Center.Y + 240));
                float eta = Math.Max(3, (-(boss.velocity.Y - player.velocity.Y) + (float)Math.Sqrt(determinant)) / 0.9f);
                Vector2 targetPoint = new Vector2(boss.Center.X + boss.velocity.X * eta, player.Center.Y + 240 + player.velocity.Y * eta + Projectile.height);
                Vector2 targetVelocity = (targetPoint - Projectile.Center) / eta;
                Projectile.velocity += (targetVelocity - Projectile.velocity) / 10f;

                bubbleShotProgress += Math.Abs(Projectile.velocity.X);
                if (bubbleShotProgress >= 80)
                {
                    bubbleShotProgress = 0;
                    bubbleCount++;
                    if (bubbleCount == 4)
                    {
                        bubbleCount = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 0.995f, ai1: 0.1f);
                    }
                    else
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 0.95f, ai1: -0.08f);
                    }
                }
            }
            else
            {
                Projectile.velocity.Y += 0.15f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.direction = Projectile.velocity.X > 0 ? 1 : -1;
            Projectile.spriteDirection = Projectile.direction;

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 8)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            if (timeLeft > 0)
            {
                SoundEngine.PlaySound(SoundID.NPCHit14, Projectile.Center);
                if (Main.netMode != NetmodeID.Server)
                {
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitX * 20f * (float)Projectile.direction, Projectile.velocity, 576, Projectile.scale);
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitY * 30f, Projectile.velocity, 574, Projectile.scale);
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, 575, Projectile.scale);
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center + Vector2.UnitX * 20f * (float)Projectile.direction, Projectile.velocity, 573, Projectile.scale);
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitY * 30f, Projectile.velocity, 574, Projectile.scale);
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, 575, Projectile.scale);
                }
                NPC boss = Main.npc[(int)Projectile.ai[0]];

                for (int i = 0; i < 12; i++)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(boss.velocity.X * 0.5f, 0) + new Vector2(6, 0).RotatedBy(i * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 0.99f, ai1: 0.08f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(boss.velocity.X * 0.5f, 0) + new Vector2(3, 0).RotatedBy((i + 0.5f) * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 0.99f, ai1: 0.08f);
                }
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 8, 0, Projectile.frame);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i], frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            return false;
        }
    }

    public class DoomedFishron : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_370";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Doomed Fishron");
            Main.projFrames[Projectile.type] = 8;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        float bubbleShotProgress = 0f;

        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 90;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.direction = Projectile.velocity.X > 0 ? 1 : -1;
            Projectile.spriteDirection = Projectile.direction;

            bubbleShotProgress += Math.Abs(Projectile.velocity.X);

            float bubbleShotProgressRequired = 160 * 200 / (200f + Projectile.timeLeft);

            if (bubbleShotProgress >= bubbleShotProgressRequired)
            {
                bubbleShotProgress -= bubbleShotProgressRequired;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - new Vector2(Projectile.direction * bubbleShotProgress, 0), Vector2.Zero, ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f, ai1: 0.05f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - new Vector2(Projectile.direction * bubbleShotProgress, 0), Vector2.Zero, ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f, ai1: -0.05f);
                }
            }

            if (Projectile.timeLeft == 60)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(6, 0).RotatedBy(i * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f);//, ai0: 0.99f, ai1: 0.08f);
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(3, 0).RotatedBy((i + 0.5f) * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f);//, ai0: 0.99f, ai1: 0.08f);
                    }
                }
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 8)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCHit14, Projectile.Center);
            if (Main.netMode == NetmodeID.Server)
            {
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitX * 20f * (float)Projectile.direction, Projectile.velocity, 576, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitY * 30f, Projectile.velocity, 574, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, 575, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center + Vector2.UnitX * 20f * (float)Projectile.direction, Projectile.velocity, 573, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center - Vector2.UnitY * 30f, Projectile.velocity, 574, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, 575, Projectile.scale);
            }
            for (int i = 0; i < 12; i++)
            {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(6, 0).RotatedBy(i * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f);//, ai0: 0.99f, ai1: 0.08f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(3, 0).RotatedBy((i + 0.5f) * MathHelper.TwoPi / 8), ModContent.ProjectileType<FloatingBubble>(), 80, 0f, Main.myPlayer, ai0: 1f);//, ai0: 0.99f, ai1: 0.08f);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 8, 0, Projectile.frame);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i], frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Teal * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(Projectile.velocity.Length(), Projectile.width / 128f), SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.Teal * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);

            return false;
        }
    }

    public class FloatingBubble : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_371";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Death Bubble");
            Main.projFrames[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 480;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.velocity *= Projectile.ai[0];
            Projectile.velocity.Y -= Projectile.ai[1];

            Projectile.rotation += Projectile.velocity.X * 0.1f;
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCDeath3, Projectile.Center);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 2, 0, Projectile.frame);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White * 0.5f, Projectile.rotation, new Vector2(24, 24), Projectile.scale, effects, 0f);

            return false;
        }
    }

    public class MothProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_205";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Moth");
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 400;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.direction = Projectile.velocity.X > 0 ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 3)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame(1, 3, 0, Projectile.frame);
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 drawOffset = new Vector2(0, -14);

            Main.EntitySpriteDraw(texture, Projectile.Center + drawOffset - Main.screenPosition, frame, Color.White, Projectile.rotation, frame.Size() / 2, Projectile.scale, effects, 0f);

            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.DarkOliveGreen * 0.25f, Projectile.velocity.ToRotation(), new Vector2(0, 64), new Vector2(Projectile.velocity.Length(), Projectile.width / 128f), SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), Color.DarkOliveGreen * 0.25f, (-Projectile.velocity).ToRotation(), new Vector2(0, 64), new Vector2(Projectile.width / 128f, Projectile.width / 128f), SpriteEffects.None, 0f);

            return false;
        }
    }

    public class MothronProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_477";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Mothron");
            Main.projFrames[Projectile.type] = 6;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 400;

            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Projectile.timeLeft > 400 - 33)
            {
                Projectile.velocity *= 0.95f;
            }
            else
            {
                Projectile.velocity.Y += Projectile.velocity.Y * Projectile.velocity.Y / (Projectile.Center.Y - Projectile.ai[0]);

                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 6f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.direction = Projectile.velocity.X > 0 ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            //test for death via ray of sunshine
            Projectile.ai[1] += 5 * Projectile.direction;
            if (Projectile.ai[1] < Projectile.Center.X ^ Projectile.direction == 1)
            {
                Projectile.Kill();
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 6)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCDeath44.WithVolumeScale(0.5f), Projectile.Center);
            if (Main.netMode != NetmodeID.Server)
            {
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 687, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 688, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 689, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 690, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, Projectile.velocity, 691, Projectile.scale);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 3)
            {
                Rectangle frame = texture.Frame(1, 6, 0, ((Projectile.frame * 3 + Projectile.frameCounter + 6 - i) / 3) % 6);

                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                if (i != 0) alpha /= 2;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i], frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            return false;
        }
    }

    public class CataBossMine : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.AncientDoom;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Celestial Doom");

            Main.projFrames[Projectile.type] = 5;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private int sigilCount
        {
            get
            {
                return Projectile.ai[1] == 0 ? 1 : (int)Projectile.ai[1] * 4;
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 0;
            Projectile.light = 0.5f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 480;

            Projectile.hide = true;
        }

        public override void AI()
        {
            float mineTime = 90f;

            if ((480 - Projectile.timeLeft) < mineTime)
            {
                Projectile.hostile = false;

                Projectile.ai[0] += 3.125f * Projectile.ai[1] * (mineTime - (480 - Projectile.timeLeft)) / mineTime;

                Projectile.alpha = (int)(256 - 128 * ((480 - Projectile.timeLeft) / mineTime));
            }
            else
            {
                Projectile.hostile = true;
                Projectile.alpha = 0;
            }

            Projectile.rotation = Projectile.ai[1];

            if (Projectile.timeLeft < 30)
            {
                Vector2 oldCenter = Projectile.Center;
                Projectile.width += 4;
                Projectile.height += 4;
                Projectile.Center = oldCenter;
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 5)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < sigilCount; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.ai[0] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            for (int i = 0; i < sigilCount; i++)
            {
                Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(Projectile.ai[0] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount);

                if (Projectile.timeLeft >= 30)
                {
                    Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

                    Rectangle frame = texture.Frame(1, 5, 0, Projectile.frame);
                    SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f), 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
                else
                {
                    Texture2D texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CelestialDoom").Value;

                    Rectangle frame = texture.Frame(1, 5, 0, Projectile.frame);
                    SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f) * (1 - Projectile.width / 168f), 0f, frame.Size() / 2, Projectile.width / 48f, effects, 0f);

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f) * (Projectile.timeLeft / 30f), 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
            }
            return false;
        }
    }

    public class CataBossMine2 : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.AncientDoom;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Celestial Doom");

            Main.projFrames[Projectile.type] = 5;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private int sigilCount
        {
            get
            {
                return Projectile.ai[1] == 0 ? 1 : (int)Projectile.ai[1] * 6;
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 0;
            Projectile.light = 0.5f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 600;

            Projectile.hide = true;
        }

        public override void AI()
        {
            float mineTime = 90f;

            if ((600 - Projectile.timeLeft) < mineTime)
            {
                Projectile.hostile = false;

                Projectile.ai[0] += 3.75f * Projectile.ai[1] * (mineTime - (600 - Projectile.timeLeft)) / mineTime;

                Projectile.alpha = (int)(256 - 128 * ((600 - Projectile.timeLeft) / mineTime));
            }
            else
            {
                Projectile.hostile = true;
                Projectile.alpha = 0;
            }

            Projectile.rotation = Projectile.ai[1] * 4f / 6f;

            if (Projectile.timeLeft < 30)
            {
                Vector2 oldCenter = Projectile.Center;
                Projectile.width += 4;
                Projectile.height += 4;
                Projectile.Center = oldCenter;
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 5)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < sigilCount; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.ai[0] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            for (int i = 0; i < sigilCount; i++)
            {
                Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(Projectile.ai[0] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / sigilCount);

                if (Projectile.timeLeft >= 30)
                {
                    Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

                    Rectangle frame = texture.Frame(1, 5, 0, Projectile.frame);
                    SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f), 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
                else
                {
                    Texture2D texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CelestialDoom").Value;

                    Rectangle frame = texture.Frame(1, 5, 0, Projectile.frame);
                    SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f) * (1 - Projectile.width / 168f), 0f, frame.Size() / 2, Projectile.width / 48f, effects, 0f);

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f) * (Projectile.timeLeft / 30f), 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
            }
            return false;
        }
    }

    public class CataBusterSword : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_426";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Buster Sword");
            Main.projFrames[Projectile.type] = 1;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 140;
            Projectile.height = 140;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 2f;
            Projectile.timeLeft = 400;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.localAI[0] = 1;

                Projectile.direction = Main.npc[(int)Projectile.ai[0]].spriteDirection;
                Projectile.spriteDirection = Projectile.direction;

                Projectile.rotation = Projectile.AngleTo(Main.player[Main.npc[(int)Projectile.ai[0]].target].Center) - 3 * MathHelper.PiOver4;

                if (Projectile.ai[1] == 2f)
                {
                    Projectile.timeLeft = 600;
                }

                Projectile.localAI[1] = Projectile.timeLeft;
            }

            if (Projectile.spriteDirection != Main.npc[(int)Projectile.ai[0]].spriteDirection)
            {
                Projectile.rotation = 3 * MathHelper.PiOver2 - Projectile.rotation;
            }

            Projectile.direction = Main.npc[(int)Projectile.ai[0]].spriteDirection;
            Projectile.spriteDirection = Projectile.direction;

            Projectile.rotation += (float)Math.Sin(Projectile.timeLeft / Projectile.localAI[1] * MathHelper.Pi) * 50f * MathHelper.Pi / Projectile.localAI[1] / 2f * Projectile.direction * Projectile.ai[1];
            Projectile.Center = Main.npc[(int)Projectile.ai[0]].Center;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + new Vector2(140, -140).RotatedBy(Projectile.rotation), 20 * Projectile.scale, ref point);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame();

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                SpriteEffects effects = Projectile.oldSpriteDirection[i] == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Vector2 center = Projectile.oldSpriteDirection[i] == 1 ? new Vector2(0, 80) : new Vector2(70, 80);
                float rotationOffset = Projectile.oldSpriteDirection[i] == 1 ? 0 : MathHelper.PiOver2;
                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;

                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i] + rotationOffset, center, Projectile.scale, effects, 0f);
            }

            return false;
        }
    }

    public class CataBossFireballRing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistBossFireBall;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Celestial Flame");

            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        int numFireballs = 32;

        public override void SetDefaults()
        {
            Projectile.width = 38;
            Projectile.height = 38;
            Projectile.alpha = 0;
            Projectile.light = 0.5f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 90;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation += Projectile.ai[1] * 0.02f;
            Projectile.ai[0] -= 20f / 3f;

            if (Projectile.timeLeft <= 30)
            {
                Projectile.alpha += 8;
            }

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 4)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            for (int i = 0; i < numFireballs; i++)
            {
                Vector2 circleCenter = Projectile.Center + new Vector2(Projectile.ai[0] * Projectile.scale, 0).RotatedBy(Projectile.rotation + i * MathHelper.TwoPi / numFireballs);
                float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
                float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
                if (new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float trailLength = 10f;

            for (int i = 0; i < numFireballs; i++)
            {
                for (int j = 0; j < trailLength; j++)
                {
                    float oldAI0 = Projectile.ai[0] + (20f / 3f) * j;
                    float oldRotation = Projectile.rotation - Projectile.ai[1] * 0.02f * j;
                    float alpha = 1 - j / trailLength;

                    Vector2 drawPosition = Projectile.Center - Main.screenPosition + new Vector2(oldAI0 * Projectile.scale, 0).RotatedBy(oldRotation + i * MathHelper.TwoPi / numFireballs);

                    Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

                    Rectangle frame = texture.Frame(1, 4, 0, Projectile.frame);
                    SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

                    Main.EntitySpriteDraw(texture, drawPosition, frame, Color.White * (1 - Projectile.alpha / 255f) * alpha, oldRotation, frame.Size() / 2, Projectile.scale * alpha, effects, 0f);
                }
            }
            return false;
        }
    }

    public class MegaSprocket : ModProjectile
    {
        public override string Texture => "DeathsTerminus/NPCs/CataBoss/HeavenPetProjectile";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Mega Sprocket");
            Main.projFrames[Projectile.type] = 7;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
            /*Texture2D texture = new Texture2D(Main.spriteBatch.GraphicsDevice, 512, 512, false, SurfaceFormat.Color);
			List<Color> list = new List<Color>();
			for (int j = 0; j < texture.Height; j++)
			{
				for (int i = 0; i < texture.Width; i++)
                {
                    float x = (2 * (i - 50) / (float)(texture.Width - 100 - 1) - 1);
                    float y = (2 * (j - 50) / (float)(texture.Height - 100 - 1) - 1);

                    float radius = (float)Math.Sqrt(x * x + y * y);
                    float theta = (float)Math.Atan2(x, y);

                    float edgeAmount = 0.3f;
                    float edgeThin = 16;
                    float edgePosition = 0.8f;

                    float index = (float)(Math.Pow(radius, 4) + (Math.Cos(2 * theta - MathHelper.PiOver2) + 1) * edgeAmount * (Math.Exp(-Math.Pow(edgeThin * (radius - edgePosition), 2)) + Math.Exp(-Math.Pow(edgeThin * (radius + edgePosition), 2))));
                    if (radius > 1) index = index * 4 - 3;
                    index = (float)Math.Max(0, Math.Min(2 - index, index));

                    int r = 255;
                    int g = 255;
                    int b = 255;
                    int alpha = (int)(255 * index);

                    list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
				}
			}
			texture.SetData(list.ToArray());
			texture.SaveAsPng(new FileStream(Main.SavePath + Path.DirectorySeparatorChar + "MegaSprocketShield.png", FileMode.Create), texture.Width, texture.Height);*/
        }

        private const int lifeTime = 1800;

        private float cogRotation = 0;
        private float shieldAlpha = 0;

        public override void SetDefaults()
        {
            Projectile.width = 66;
            Projectile.height = 66;
            Projectile.alpha = 0;
            Projectile.light = 1f;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = lifeTime;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.scale = Projectile.ai[1] + 1f;
            }

            if (Projectile.localAI[0] == 60)
            {
                Projectile.hostile = true;
            }

            Vector2 oldCenter = Projectile.Center;
            Projectile.width = (int)(Projectile.scale * 66);
            Projectile.height = (int)(Projectile.scale * 66);
            Projectile.Center = oldCenter;

            if (Projectile.timeLeft == lifeTime)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(1, 0).RotatedBy(i * MathHelper.TwoPi / 6f), ModContent.ProjectileType<MegaSprocketPrism>(), 80, 0f, Main.myPlayer, ai0: i, ai1: Projectile.whoAmI);
                }
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 7)
                {
                    Projectile.frame = 0;
                }
            }

            if (Projectile.timeLeft > 300)
            {
                Projectile.velocity *= (1 - 1 / 60f);

                shieldAlpha = (shieldAlpha + 1 / 240f) / (1 + 1 / 240f);
            }
            else if (Projectile.timeLeft > 150)
            {
                Projectile.velocity *= (1 - 1 / 60f);

                Projectile.hostile = false;

                if (shieldAlpha > 0)
                {
                    shieldAlpha = shieldAlpha - 1 / 150f;
                }
                else
                {
                    shieldAlpha = 0;
                }
            }
            else
            {
                NPC boss = Main.npc[(int)Projectile.ai[0]];

                Projectile.velocity += (boss.Center + boss.velocity * Projectile.timeLeft - Projectile.Center - Projectile.velocity * Projectile.timeLeft) / (Projectile.timeLeft * Projectile.timeLeft);
                Projectile.scale -= 2 / 150f;
            }

            Projectile.localAI[0]++;
            cogRotation += Projectile.localAI[0] / 50000f + Projectile.localAI[0] * Projectile.localAI[0] / 100000000f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 circleCenter = Projectile.Center;
            float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
            float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
            return new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle frame;
            SpriteEffects effects;

            //draw the pet
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            if (Projectile.localAI[1] == 0)
            {
                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/DarkPetProjectile").Value;
            }

            frame = texture.Frame(1, 7, 0, Projectile.frame);
            effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            float wingRotation = Projectile.velocity.X * 0.1f;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White, wingRotation, frame.Size() / 2, Projectile.scale, effects, 0f);

            Texture2D texture2 = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectile_Cog").Value;
            if (Projectile.localAI[1] == 0)
            {
                texture2 = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/DarkPetProjectile_Cog").Value;
            }

            frame = texture2.Frame();
            effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.EntitySpriteDraw(texture2, Projectile.Center - Main.screenPosition, frame, Color.White, cogRotation, frame.Size() / 2, Projectile.scale, effects, 0f);

            Texture2D texture3 = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/MegaSprocketShield").Value;
            frame = texture3.Frame();

            Main.EntitySpriteDraw(texture3, Projectile.Center - Main.screenPosition, frame, Color.White * shieldAlpha, cogRotation / 2, frame.Size() / 2, Projectile.scale / 6f, effects, 0f);

            return false;
        }
    }

    public class MegaSprocketPrism : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_633";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Last Prism");
            Main.projFrames[Projectile.type] = 5;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private const int lifeTime = 1200;
        private bool firingPrism
        {
            get { return Projectile.timeLeft <= lifeTime - 120; }
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.alpha = 0;
            Projectile.light = 1f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = lifeTime;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0)
            {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            Projectile owner = Main.projectile[(int)Projectile.ai[1]];

            float radius = 48 + Math.Max(0, (Projectile.localAI[0] - 120));
            Projectile.rotation += Projectile.localAI[0] / 50000f + Projectile.localAI[0] * Projectile.localAI[0] / 100000000f;

            Projectile.velocity = owner.Center + new Vector2(radius, 0).RotatedBy(Projectile.rotation) - Projectile.Center;

            Projectile.hostile = firingPrism;

            Projectile.localAI[0]++;

            if (Projectile.timeLeft == lifeTime - 120)
            {
                SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);
            }
            else if (Projectile.timeLeft % 60 == 0 && Projectile.timeLeft <= lifeTime - 120)
            {
                SoundEngine.PlaySound(SoundID.Item15, Projectile.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + new Vector2(20, 0).RotatedBy(Projectile.rotation), Projectile.Center + new Vector2(4096, 0).RotatedBy(Projectile.rotation), 44 * Projectile.scale, ref point);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle frame;
            SpriteEffects effects;

            //Prisms here are different, each one is a different individual color
            Color value42 = Main.hslToRgb((Projectile.ai[0] / 6f + Projectile.localAI[0] / 360f) % 1, 1f, 0.5f);
            value42.A = 0;

            //draw connections to prisms
            float radius = 48 + Math.Max(0, (Projectile.localAI[0] - 120));
            float alpha = 1 - radius / 1200f;
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), value42 * (alpha * 0.4f), Projectile.rotation + MathHelper.Pi, new Vector2(0, 64), new Vector2(radius / 64f, Projectile.width / 64f), SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center - Main.screenPosition, new Rectangle(0, 0, 64, 128), value42 * (alpha * 0.4f), Projectile.rotation, new Vector2(0, 64), new Vector2(Projectile.width / 64f, Projectile.width / 64f), SpriteEffects.None, 0f);

            //draw prism telegraph glow/background glow
            float scaleModifier = Projectile.timeLeft <= lifeTime - 120 ? 5 : 1;
            float alphaModifier = Projectile.timeLeft <= lifeTime - 120 ? 0.5f : 0.2f;
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossScytheTelegraph").Value, Projectile.Center + new Vector2(8 * scaleModifier * Projectile.scale, 0).RotatedBy(Projectile.rotation) - Main.screenPosition, new Rectangle(0, 0, 64, 128), value42 * alphaModifier, Projectile.rotation, new Vector2(0, 64), new Vector2((1200 - radius) / 64f, Projectile.width / 128f * scaleModifier), SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/CataBossTelegraphCap").Value, Projectile.Center + new Vector2(8 * scaleModifier * Projectile.scale, 0).RotatedBy(Projectile.rotation) - Main.screenPosition, new Rectangle(0, 0, 64, 128), value42 * alphaModifier, Projectile.rotation + MathHelper.Pi, new Vector2(0, 64), new Vector2(Projectile.width / 128f * scaleModifier, Projectile.width / 128f * scaleModifier), SpriteEffects.None, 0f);

            //draw the prism
            //adapted from last prism drawcode
            Texture2D prismTexture = TextureAssets.Projectile[Projectile.type].Value;
            frame = prismTexture.Frame(1, 5, 0, (Projectile.timeLeft / (Projectile.timeLeft <= 600 - 60 ? 1 : 3)) % 5);
            effects = SpriteEffects.None;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            Main.EntitySpriteDraw(prismTexture, drawPosition, frame, Color.White, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);

            float scaleFactor2 = (float)Math.Cos(Math.PI * 2f * (Projectile.timeLeft / 30f)) * 2f + 2f;
            if (Projectile.timeLeft <= lifeTime - 120)
            {
                scaleFactor2 = 4f;
            }
            for (float num350 = 0f; num350 < 4f; num350 += 1f)
            {
                Main.EntitySpriteDraw(prismTexture, drawPosition + new Vector2(0, 1).RotatedBy(num350 * (Math.PI * 2f) / 4f) * scaleFactor2, frame, Color.White.MultiplyRGBA(new Color(255, 255, 255, 0)) * 0.03f, Projectile.rotation + MathHelper.PiOver2, frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            if (Projectile.timeLeft > lifeTime - 120)
            {
                //draw the telegraph line
                float telegraphAlpha = (120 - lifeTime + Projectile.timeLeft) / 30f * (lifeTime - 60 - Projectile.timeLeft) / 30f;
                Main.EntitySpriteDraw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectileTelegraph").Value, Projectile.Center + new Vector2(10 * Projectile.scale, 0).RotatedBy(Projectile.rotation) - Main.screenPosition, new Rectangle(0, 0, 1, 1), Color.White * telegraphAlpha, Projectile.rotation, new Vector2(0, 0.5f), new Vector2(4096, 1), SpriteEffects.None, 0f);
            }
            if (firingPrism)
            {
                //draw the beam
                //adapted from last prism drawcode

                for (int i = 0; i < 6; i++)
                {
                    //texture
                    Texture2D tex7 = TextureAssets.Projectile[632].Value;
                    //laser length
                    float num528 = 1200 - (48 + Math.Max(0, (Projectile.localAI[0] - 120)));

                    Vector2 drawOffset = new Vector2(4, 0).RotatedBy(Projectile.timeLeft * 0.5f + i * MathHelper.TwoPi / 6);

                    //start position
                    Vector2 value45 = Projectile.Center.Floor() + drawOffset + new Vector2(26 * Projectile.scale, 0).RotatedBy(Projectile.rotation);

                    value45 += Vector2.UnitX.RotatedBy(Projectile.rotation) * Projectile.scale * 10.5f;
                    num528 -= Projectile.scale * 14.5f * Projectile.scale;
                    Vector2 vector90 = new Vector2(Projectile.scale * 2);
                    DelegateMethods.f_1 = 1f;
                    DelegateMethods.c_1 = value42 * 0.75f * Projectile.Opacity;
                    _ = Projectile.oldPos[0] + new Vector2((float)Projectile.width, (float)Projectile.height) / 2f + Vector2.UnitY * Projectile.gfxOffY - Main.screenPosition;
                    Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90, DelegateMethods.RainbowLaserDraw);
                    DelegateMethods.c_1 = new Color(255, 255, 255, 127) * 0.75f * Projectile.Opacity;
                    Utils.DrawLaser(Main.spriteBatch, tex7, value45 - Main.screenPosition, value45 + Vector2.UnitX.RotatedBy(Projectile.rotation) * num528 - Main.screenPosition, vector90 / 2f, DelegateMethods.RainbowLaserDraw);
                }
            }

            return false;
        }
    }

    public class MegaBaddy : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Mysterious Presence");
            Main.projFrames[Projectile.type] = 7;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        private const int initTime = 60;
        private const int lifeTime = 1200;
        private const int fadeTime = 180;
        private const int departTime = 300;
        private const float projectileRadius = 1200;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.alpha = 255;
            Projectile.light = 1f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 2f;
            Projectile.timeLeft = initTime + lifeTime + fadeTime + departTime;
        }

        public override void AI()
        {
            if (Projectile.timeLeft > lifeTime + fadeTime + departTime)
            {
                Projectile.scale -= 1f / initTime;
                Projectile.alpha = (int)(((Projectile.timeLeft - lifeTime - fadeTime - departTime) / (float)initTime) * 255);
            }
            else if (Projectile.timeLeft > fadeTime + departTime)
            {
                Projectile.scale = 1f;

                float x = (lifeTime - (Projectile.timeLeft - fadeTime - departTime)) / (float)lifeTime;
                float a = 1 / 8f; //starting increment
                float b = 1 / 4f; //ending increment
                float c = 1 / 1f; //amount by which it 'dips'
                float increment = a + (b - a) * x - c * x * x * (1 - x) * (1 - x);
                Projectile.localAI[0] += increment;

                if (Projectile.localAI[0] >= 1 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.localAI[0] -= 1;

                    for (int i = 0; i < 6; i++)
                    {
                        float shotExtraTime = Projectile.localAI[0] / increment;
                        float shotRotation = (lifeTime - (Projectile.timeLeft - fadeTime - departTime) - shotExtraTime) * (lifeTime - (Projectile.timeLeft - fadeTime - departTime) - shotExtraTime) / 52000f + i * MathHelper.TwoPi / 6f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + new Vector2(projectileRadius, 0).RotatedBy(shotRotation) + new Vector2(-4, 0).RotatedBy(shotRotation) * shotExtraTime, new Vector2(-4, 0).RotatedBy(shotRotation), ModContent.ProjectileType<Shadow>(), 80, 0f, Main.myPlayer);
                    }
                }
            }
            else
            {
                Projectile.hostile = false;
            }

            if (Projectile.timeLeft - departTime < 450)
            {
                Projectile.alpha = Math.Max(0, (int)(255 - ((Projectile.timeLeft - departTime) / 450f) * 255));
            }

            if (Projectile.timeLeft > departTime)
            {
                Projectile.rotation = Projectile.DirectionTo(Main.player[(int)Projectile.ai[0]].Center).ToRotation();
                Projectile.spriteDirection = Projectile.DirectionTo(Main.player[(int)Projectile.ai[0]].Center).X > 0 ? 1 : -1;
            }
            else if (Projectile.timeLeft == departTime - 90 || Projectile.timeLeft == departTime - 120)
            {
                Projectile.spriteDirection *= -1;
            }
        }

        public override bool? CanHitNPC(NPC target)
        {
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 circleCenter = Projectile.Center;

            float furthestX = targetHitbox.X + targetHitbox.Size().X / 2 - circleCenter.X > 0 ? targetHitbox.X + targetHitbox.Size().X : targetHitbox.X;
            float furthestY = targetHitbox.Y + targetHitbox.Size().Y / 2 - circleCenter.Y > 0 ? targetHitbox.Y + targetHitbox.Size().Y : targetHitbox.Y;
            return new Vector2(circleCenter.X - furthestX, circleCenter.Y - furthestY).Length() > projectileRadius * Projectile.scale;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture;
            Rectangle frame;
            float numDraws;
            SpriteEffects effects = SpriteEffects.None;

            float alphaModifier = 1 - Projectile.alpha / 255f;

            if (alphaModifier > 0)
            {
                //draw the baddy
                //draw the baddy's particles
                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowParticle").Value;
                numDraws = 1600;
                Rectangle[] frameList = new Rectangle[]
                {
                new Rectangle(0,0,14,14),
                new Rectangle(16,4,10,10),
                new Rectangle(28,6,8,8)
                };
                for (int i = 0; i < numDraws; i++)
                {
                    frame = frameList[i % 3];

                    float distanceInwards = (i * i + (i % 3 + 2) * (lifeTime - Projectile.timeLeft)) % 60;
                    float rotationOffset = (i * i) % numDraws + (i * i + (i % 3 + 2) * (lifeTime - Projectile.timeLeft)) / 60;
                    float alpha = (60 - distanceInwards) / 60;
                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale - distanceInwards, 0).RotatedBy(i * MathHelper.TwoPi / numDraws + rotationOffset) - Main.screenPosition, frame, Color.White * alpha * alphaModifier, 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowParticleBig").Value;
                frame = texture.Frame();
                numDraws = 800;
                for (int i = 0; i < numDraws; i++)
                {
                    float distanceInwards = (i * i + 2 * (lifeTime - Projectile.timeLeft)) % 60 - texture.Width / 2;
                    float rotationOffset = (i * i) % numDraws + (i * i + 2 * (lifeTime - Projectile.timeLeft)) / 60;
                    float alpha = (60 - distanceInwards) / 60;
                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale - distanceInwards, 0).RotatedBy(i * MathHelper.TwoPi / numDraws + rotationOffset) - Main.screenPosition, frame, Color.White * alpha * alphaModifier, 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }

                //draw the baddy's body
                texture = TextureAssets.Projectile[Projectile.type].Value;
                frame = texture.Frame();
                numDraws = 48;

                for (int i = 0; i < numDraws; i++)
                {
                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale, 0).RotatedBy(i * MathHelper.TwoPi / numDraws) - Main.screenPosition, frame, Color.White * alphaModifier, i * MathHelper.TwoPi / 48, new Vector2(0, 0.5f), Projectile.scale * projectileRadius, effects, 0f);
                }

                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/FogParticle").Value;
                numDraws = 1600;
                frameList = new Rectangle[]
                {
                new Rectangle(0,0,54,10),
                new Rectangle(0,16,32,6),
                new Rectangle(54,14,26,6),
                new Rectangle(80,18,32,8),
                new Rectangle(94,0,60,10),
                new Rectangle(130,22,20,6)
                };
                for (int i = 0; i < numDraws; i++)
                {
                    frame = frameList[i % 6];

                    float timeAlive = (i * i + (lifeTime - Projectile.timeLeft)) % 60;
                    float rotationOffset = (i * i) % numDraws + (i * i + (lifeTime - Projectile.timeLeft)) / 60;
                    float distanceOutwards = 40 + (i * i) % numDraws;
                    float alpha = timeAlive * (60 - timeAlive) / 900f;

                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale + distanceOutwards, 0).RotatedBy(i * MathHelper.TwoPi / numDraws + rotationOffset) - Main.screenPosition, frame, Color.White * alpha * alphaModifier, 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }

                texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/LightningParticle").Value;
                numDraws = 400;
                frameList = new Rectangle[]
                {
                new Rectangle(0,0,8,16),
                new Rectangle(10,0,12,8),
                new Rectangle(24,0,18,16),
                new Rectangle(44,0,12,22),
                new Rectangle(58,0,24,18),
                new Rectangle(84,0,16,20)
                };
                for (int i = 0; i < numDraws; i++)
                {
                    frame = frameList[i % 6];

                    float timeAlive = (i * i + (lifeTime - Projectile.timeLeft)) % 60;
                    float rotationOffset = MathHelper.Pi + (i * i) % numDraws + (i * i + (lifeTime - Projectile.timeLeft)) / 60;
                    float distanceOutwards = 40 + (i * i) % numDraws * 3;
                    float alpha = (60 - timeAlive) / 60f;

                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale + distanceOutwards, 0).RotatedBy(i * MathHelper.TwoPi / numDraws + rotationOffset) - Main.screenPosition, frame, Color.White * alpha * alphaModifier, 0f, frame.Size() / 2, Projectile.scale, effects, 0f);
                }
            }

            //draw the baddy's eyes
            texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/BaddyPet_Eyes").Value;
            frame = texture.Frame(1, 2, 0, Projectile.timeLeft > departTime ? 0 : 1);
            effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 drawEyesOffset = Projectile.timeLeft > departTime ? Vector2.Zero : new Vector2(0, -(float)Math.Exp((departTime - Projectile.timeLeft) / 10f - 15));

            Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale + 240, 0).RotatedBy(Projectile.rotation) - Main.screenPosition + drawEyesOffset, frame, Color.White, 0f, frame.Size() / 2, 4f, effects, 0f);

            if (Projectile.timeLeft > fadeTime + departTime)
            {
                for (int i = 0; i < 6; i++)
                {
                    float rotation = ((lifeTime - (Projectile.timeLeft - fadeTime - departTime)) * (lifeTime - (Projectile.timeLeft - fadeTime - departTime)) / 50000f + i * MathHelper.TwoPi / 6f) % MathHelper.TwoPi;
                    effects = rotation < MathHelper.Pi ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    Main.EntitySpriteDraw(texture, Projectile.Center + new Vector2(projectileRadius * Projectile.scale + 120, 0).RotatedBy(rotation) - Main.screenPosition, frame, Color.White * alphaModifier, 0f, frame.Size() / 2, 2f, effects, 0f);
                }
            }

            return false;
        }
    }

    public class Shadow : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Shadow");
            Main.projFrames[Projectile.type] = 1;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 275;

            Projectile.hide = true;
        }

        public override void AI()
        {
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/ShadowOutline").Value;

            Rectangle frame = texture.Frame();
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i], frame.Size() / 2, Projectile.scale * alpha, effects, 0f);
            }

            Texture2D texture2 = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame2 = texture2.Frame();

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(texture2, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame2, Color.White * alpha, Projectile.oldRot[i], frame2.Size() / 2, Projectile.scale * alpha, effects, 0f);
            }

            return false;
        }
    }

    public class MothronSpiralProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_477";

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Mothron");
            Main.projFrames[Projectile.type] = 6;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.alpha = 0;
            Projectile.light = 0f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.scale = 1f;
            Projectile.timeLeft = 314 + 30;

            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Projectile.timeLeft == 314)
            {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 6;
            }
            else if (Projectile.timeLeft < 314)
            {
                Projectile.velocity = Projectile.velocity.RotatedBy(-Projectile.ai[0] * MathHelper.Pi / 314f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            Projectile.direction = Projectile.velocity.X > 0 ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;

            //frame stuff
            Projectile.frameCounter++;
            if (Projectile.frameCounter == 3)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame == 6)
                {
                    Projectile.frame = 0;
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.NPCDeath44.WithVolumeScale(0.5f), Projectile.Center);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 3)
            {
                Rectangle frame = texture.Frame(1, 6, 0, ((Projectile.frame * 3 + Projectile.frameCounter + 6 - i) / 3) % 6);

                float alpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                if (i != 0) alpha /= 2;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + Projectile.Center - Projectile.position - Main.screenPosition, frame, Color.White * alpha, Projectile.oldRot[i], frame.Size() / 2, Projectile.scale, effects, 0f);
            }

            return false;
        }
    }

    public class CelestialLamp : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Celestial Lamp");

            /*Texture2D texture = new Texture2D(Main.spriteBatch.GraphicsDevice, 512, 512, false, SurfaceFormat.Color);
			System.Collections.Generic.List<Color> list = new System.Collections.Generic.List<Color>();
			for (int j = 0; j < texture.Height; j++)
			{
				for (int i = 0; i < texture.Width; i++)
                {
                    float x = (2 * i / (float)(texture.Width - 1) - 1);
                    float y = (2 * j / (float)(texture.Width - 1) - 1);

                    float distanceSquared = x * x + y * y;
                    float index = 1 - distanceSquared;

                    int r = 255 - (int)(64 * (1 - index));
                    int g = 255 - (int)(128 * (1 - index));
                    int b = 255;
                    int alpha = distanceSquared >= 1 ? 0 : (int)(255 * index);

                    list.Add(new Color((int)(r * alpha / 255f), (int)(g * alpha / 255f), (int)(b * alpha / 255f), alpha));
				}
			}
			texture.SetData(list.ToArray());
			texture.SaveAsPng(new FileStream(Main.SavePath + Path.DirectorySeparatorChar + "CelestialLamp.png", FileMode.Create), texture.Width, texture.Height);*/
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 512;
            Projectile.height = 512;
            Projectile.alpha = 0;
            Projectile.light = 4f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 1620;
            Projectile.scale = 1 / 600f;
        }

        public override void AI()
        {
            if (Projectile.scale < 2f && Projectile.timeLeft > 60)
            {
                Projectile.scale = (Projectile.scale + 1 / 600f) / (2 + 1 / 600f) * 2;
            }
            else if (Projectile.timeLeft <= 60)
            {
                Projectile.scale -= 1 / 60f;
            }

            Vector2 oldCenter = Projectile.Center;
            Projectile.width = (int)(512 * Projectile.scale);
            Projectile.height = (int)(512 * Projectile.scale);
            Projectile.Center = oldCenter;

            if (Projectile.timeLeft == 60 || Projectile.timeLeft == 300 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 12; i++)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0.7f, 0).RotatedBy(i * MathHelper.TwoPi / 12), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 2f);
                }
            }
            else if (Projectile.timeLeft == 180 || Projectile.timeLeft == 420 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 12; i++)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0.7f, 0).RotatedBy((i + 0.5f) * MathHelper.TwoPi / 12), ModContent.ProjectileType<CataBossSuperScythe>(), 80, 0f, Main.myPlayer, ai1: 2f);
                }
            }
            if (Projectile.timeLeft == 30 || Projectile.timeLeft == 150 || Projectile.timeLeft == 270 || Projectile.timeLeft == 390)
            {
                SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 circleCenter = Projectile.Center;
            float nearestX = Math.Max(targetHitbox.X, Math.Min(circleCenter.X, targetHitbox.X + targetHitbox.Size().X));
            float nearestY = Math.Max(targetHitbox.Y, Math.Min(circleCenter.Y, targetHitbox.Y + targetHitbox.Size().Y));
            return new Vector2(circleCenter.X - nearestX, circleCenter.Y - nearestY).Length() < Projectile.width / 2;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame();

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, frame.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    public class CataBossStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Falling Star");
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }
        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.tileCollide = false;
            Projectile.light = 0.9f;
            Projectile.scale = 1.2f;
        }

        public override void AI()
        {
            if (Projectile.ai[1] == 0f && !Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height))
            {
                Projectile.ai[1] = 1f;
                Projectile.netUpdate = true;
            }
            if (Projectile.timeLeft < 3600 - 180)
            {
                Projectile.tileCollide = true;
            }
            if (Projectile.soundDelay == 0)
            {
                Projectile.soundDelay = 20 + Main.rand.Next(40);
                SoundEngine.PlaySound(SoundID.Item9, Projectile.position);
            }
            Projectile.rotation += (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y)) * 0.01f * (float)Projectile.direction;
            if (Projectile.ai[1] == 1f)
            {
                Projectile.light = 0.9f;
                if (Main.rand.NextBool(10))
                {
                    Vector2 position30 = Projectile.position;
                    int width27 = Projectile.width;
                    int height27 = Projectile.height;
                    float speedX13 = Projectile.velocity.X * 0.5f;
                    float speedY13 = Projectile.velocity.Y * 0.5f;
                    Color newColor = default(Color);
                    Dust.NewDust(position30, width27, height27, DustID.Enchanted_Pink, speedX13, speedY13, 150, newColor, 1.2f);
                }
                if (Main.rand.NextBool(20))
                {
                    Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, new Vector2(Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f), Main.rand.Next(16, 18));
                }
            }
        }

        public override void Kill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);
            int num537 = 10;
            int num538 = 3;
            for (int num539 = 0; num539 < num537; num539++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Pink, Projectile.velocity.X * 0.1f, Projectile.velocity.Y * 0.1f, 150, default(Color), 1.2f);
            }
            for (int num540 = 0; num540 < num538; num540++)
            {
                int num541 = Main.rand.Next(16, 18);
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, new Vector2(Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f), num541);
            }
            for (int num542 = 0; num542 < 10; num542++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Gold, Projectile.velocity.X * 0.1f, Projectile.velocity.Y * 0.1f, 150, default(Color), 1.2f);
            }
            for (int num543 = 0; num543 < 3; num543++)
            {
                Gore.NewGore(Projectile.GetSource_FromThis(), Projectile.position, new Vector2(Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f), Main.rand.Next(16, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            lightColor = Color.White;
            return true;
        }
    }

    public class CataBossRod : ModProjectile
    {
        //rod of discord of doom
        public override string Texture => "Terraria/Images/Item_" + ItemID.RodofDiscord;

        public override void SetStaticDefaults()
        {
            // DisplayName.SetDefault("Rod of Judgement");
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.scale = 10f;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 1200;

            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.rotation += Projectile.velocity.X / 0.1f;

            Player player = Main.player[(int)Projectile.ai[0]];
            Projectile.velocity += ((player.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Math.Max(16, player.velocity.Length() + 4) - Projectile.velocity) / 20f;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            Rectangle frame = texture.Frame();

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, frame, Color.White * (1 - Projectile.alpha / 255f), Projectile.rotation, frame.Size() / 2, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    public class ShadowDust : ModDust
    {
        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.noLight = true;
            dust.velocity *= 1.5f;

            dust.frame = Main.rand.Next(new Rectangle[]
            {
                new Rectangle(0,0,14,14),
                new Rectangle(0,16,10,10),
                new Rectangle(0,28,8,8),
            });
            dust.position -= dust.frame.Size() / 2;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            return Color.White;
        }

        public override bool Update(Dust dust)
        {
            dust.position += dust.velocity;
            dust.scale -= 0.02f;
            if (dust.scale < 0.5f)
            {
                dust.active = false;
            }
            return false;
        }
    }

    public class CataBossSky : CustomSky
    {
        public static int celestialObject;

        private bool isActive;
        public const int ECLIPSE_FRAME_SIZE = 512;
        private static int eclipseFrame;

        public override void OnLoad()
        {
        }

        public override void Update(GameTime gameTime)
        {
            eclipseFrame++;
            if (eclipseFrame == 60) eclipseFrame = 0;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            //draw the sky and the eclipse of doom
            if (maxDepth >= 0 && minDepth < 0)
            {
                spriteBatch.Draw(ModContent.Request<Texture2D>("DeathsTerminus/NPCs/CataBoss/HeavenPetProjectileTelegraph").Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black);

                switch (celestialObject)
                {
                    case 1:
                        if (TextureCache.EclipseTexture != null)
                            spriteBatch.Draw(TextureCache.EclipseTexture.Value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2 - 300) - new Vector2(ECLIPSE_FRAME_SIZE / 2), new Rectangle((eclipseFrame % 10) * ECLIPSE_FRAME_SIZE, (eclipseFrame / 10) * ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE), Color.White);
                        break;
                    case 2:
                        if (TextureCache.BlueSunTexture != null)
                            spriteBatch.Draw(TextureCache.BlueSunTexture.Value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2 - 300) - new Vector2(ECLIPSE_FRAME_SIZE / 2), new Rectangle((eclipseFrame % 10) * ECLIPSE_FRAME_SIZE, (eclipseFrame / 10) * ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE), Color.White);
                        break;
                    case 3:
                        if (TextureCache.RainbowSunTexture != null)
                            spriteBatch.Draw(TextureCache.RainbowSunTexture.Value, new Vector2(Main.screenWidth / 2, Main.screenHeight / 2 - 300) - new Vector2(ECLIPSE_FRAME_SIZE / 2), new Rectangle((eclipseFrame % 10) * ECLIPSE_FRAME_SIZE, (eclipseFrame / 10) * ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE, ECLIPSE_FRAME_SIZE), Color.White);
                        break;
                }
            }
        }

        public override void Activate(Vector2 position, params object[] args)
        {
            isActive = true;
        }

        public override void Deactivate(params object[] args)
        {
            isActive = false;
        }

        public override void Reset()
        {
            isActive = false;
        }

        public override bool IsActive()
        {
            return isActive;
        }
    }
    public class CataScene : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override void SpecialVisuals(Player player, bool isActive)
        {
            bool cataBoss = NPC.AnyNPCs(ModContent.NPCType<CataBoss>());
            player.ManageSpecialBiomeVisuals("DeathsTerminus:CataBoss", cataBoss);
        }
        public override bool IsSceneEffectActive(Player player)
        {
            return NPC.AnyNPCs(ModContent.NPCType<CataBoss>());
        }
    }
}
