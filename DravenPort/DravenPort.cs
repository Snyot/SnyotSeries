using HesaEngine.SDK;
using HesaEngine.SDK.Enums;
using HesaEngine.SDK.GameObjects;
using SharpDX;
using System.Collections.Generic;
using System.Linq;
using static HesaEngine.SDK.Orbwalker;

namespace DravenPort
{
    public class DravenPort : IScript
    {
        public string Name => "Draven Port";
        public string Version => "1.0.0";
        public string Author => "";

        float[] LastSpells = new float[14] {
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f,
            1000f
        };
        BuffType[] CC_BUFFS = new BuffType[] {
            BuffType.Stun,
            BuffType.Silence,
            BuffType.Taunt,
            BuffType.Polymorph,
            BuffType.Slow,
            BuffType.Snare,
            BuffType.Fear,
            BuffType.Charm,
            BuffType.Suppression,
            BuffType.Blind,
            BuffType.Knockup,
            BuffType.Knockback
        };

        internal class ChannelSpell
        {
            internal string Name = "";
            internal string SpellName = "";
            internal float Duration = 0f;
            internal bool IsChannel = false;
        }

        Dictionary<string, ChannelSpell> CHANNEL_SPELLS = new Dictionary<string, ChannelSpell>()
        {
            { "CaitlynAceintheHole",  new ChannelSpell() { Name = "Caitlyn", SpellName = "R | Ace in the Hole", Duration = 1, IsChannel = true } },
            { "Crowstorm",  new ChannelSpell() { Name = "FiddleSticks", SpellName = "R | Crowstorm", Duration = 1.5f, IsChannel = true } },
            { "Drain",  new ChannelSpell() { Name = "FiddleSticks", SpellName = "W | Drain", Duration = 5, IsChannel = true } },
            { "ReapTheWhirlwind",  new ChannelSpell() { Name = "Janna", SpellName = "R | Monsoon", Duration = 3, IsChannel = true } },
            { "KarthusFallenOne",  new ChannelSpell() { Name = "Karthus", SpellName = "R | Requiem", Duration = 3, IsChannel = true } },
            { "KatarinaR",  new ChannelSpell() { Name = "Katarina", SpellName = "R | Death Lotus", Duration = 2.5f, IsChannel = true } },
            { "LucianR",  new ChannelSpell() { Name = "Lucian", SpellName = "R | The Culling", Duration = 3, IsChannel = false } },
            { "AlZaharNetherGrasp",  new ChannelSpell() { Name = "Malzahar", SpellName = "R | Nether Grasp", Duration = 2.5f, IsChannel = true } },
            { "Meditate",  new ChannelSpell() { Name = "MasterYi", SpellName = "W | Meditate", Duration = 4, IsChannel = true } },
            { "MissFortuneBulletTime",  new ChannelSpell() { Name = "MissFortune", SpellName = "R | Bullet Time", Duration = 3, IsChannel = true } },
            { "AbsoluteZero",  new ChannelSpell() { Name = "Nunu", SpellName = "R | Absoulte Zero", Duration = 3, IsChannel = true } },
            { "PantheonRJump",  new ChannelSpell() { Name = "Pantheon", SpellName = "R | Jump", Duration = 2, IsChannel = true } },
            { "ShenStandUnited",  new ChannelSpell() { Name = "Shen", SpellName = "R | Stand United", Duration = 3, IsChannel = true } },
            { "Destiny",  new ChannelSpell() { Name = "TwistedFate", SpellName = "R | Destiny", Duration = 1.5f, IsChannel = true } },
            { "UrgotSwap2",  new ChannelSpell() { Name = "Urgot", SpellName = "R | Hyper-Kinetic Position Reverser", Duration = 1, IsChannel = true } },
            { "VarusQ",  new ChannelSpell() { Name = "Varus", SpellName = "Q | Piercing Arrow", Duration = 4, IsChannel = true } },
            { "VelkozR",  new ChannelSpell() { Name = "Velkoz", SpellName = "R | Lifeform Disintegration Ray", Duration = 2.5f, IsChannel = true } },
            { "XerathLocusOfPower2",  new ChannelSpell() { Name = "Xerath", SpellName = "R | Rite of the Arcane", Duration = 10, IsChannel = true } }
        };

        internal class ActiveChannel
        {
            internal Obj_AI_Base Unit = null;
            internal AttackableUnit Target = null;
            internal float StartTime = 0;
            internal float Duration = 0;
            internal bool IsChannel = false;
            internal string Name = "";
        }

        List<ActiveChannel> ActiveChannels = new List<ActiveChannel>();

        internal class ActiveAxe
        {
            internal GameObject Axe = null;
            internal float StartTime = 0;
        }
        List<ActiveAxe> Axes = new List<ActiveAxe>();

        float AttackDelayCastOffsetPercent = -0.14385608724014f;
        float AttackDelayOffsetPercent = -0.079375f;

        AIHeroClient myHero => ObjectManager.Me;
        OrbwalkerInstance Orbwalk => Core.Orbwalker;

        Menu MyMenu, ComboMenu, AxeMenu, AutoWMenu, GapCloserMenu, AntiChannelMenu, MiscMenu, QssMenu;
        Spell Q, W, E, R;

        int AxeCount = 0;
        int LastQ = 0;
        int LastAttack = 0;

        GameObject R_Obj = null;

        public void OnInitialize()
        {
            Game.OnGameLoaded += Boot;
        }

        private void Boot()
        {
            if (myHero.Hero != Champion.Draven) return;

            MyMenu = Menu.AddMenu("Draven");

            ComboMenu = MyMenu.AddSubMenu("Combo");
            ComboMenu.Add(new MenuCheckbox("CQ", "Use Q", true));
            ComboMenu.Add(new MenuCheckbox("CW", "Use W", true));
            ComboMenu.Add(new MenuCheckbox("CE", "Use E", true));
            ComboMenu.Add(new MenuCheckbox("CR", "Use R", true));

            AxeMenu = MyMenu.AddSubMenu("Axe Catch");
            AxeMenu.Add(new MenuCheckbox("Enabled", "Enabled", true));
            AxeMenu.Add(new MenuCheckbox("ACW", "Use W On Uncatchable Axe", true));
            AxeMenu.Add(new MenuCheckbox("OC2", "Only Catch If Axes < 2", true));
            
            AutoWMenu = MyMenu.AddSubMenu("Auto W");
            AutoWMenu.Add(new MenuCheckbox("Enabled", "Enabled", true));
            AutoWMenu.Add(new MenuCheckbox("AC", "On Axe Catch", true));
            AutoWMenu.Add(new MenuSlider("ACC", "If %Health Less Than", 1, 100, 50));
            AutoWMenu.Add(new MenuKeybind("AWK", "On Axe Catch", new KeyBind(SharpDX.DirectInput.Key.T, MenuKeybindType.Toggle, true)));
            AutoWMenu.Add(new MenuCheckbox("AWS", "On Slows", true));

            GapCloserMenu = MyMenu.AddSubMenu("Anti-GapClose");
            GapCloserMenu.Add(new MenuCheckbox("AGCE", "Use E", true));
            Core.DelayAction(() =>
            {
                foreach (var enemy in ObjectManager.Heroes.Enemies)
                {
                    GapCloserMenu.Add(new MenuCheckbox(enemy.NetworkId.ToString(), enemy.CharData.BaseSkinName + " Enabled", true));
                }
            }, 100);

            AntiChannelMenu = MyMenu.AddSubMenu("Anti-Channel");
            AntiChannelMenu.Add(new MenuCheckbox("Enabled", "Enabled", true));
            AntiChannelMenu.Add(new MenuCheckbox("ACE", "Use E", true));
            Core.DelayAction(() =>
            {
                foreach (var enemy in ObjectManager.Heroes.Enemies)
                {
                    var champMenu = AntiChannelMenu.AddSubMenu(enemy.CharData.BaseSkinName);
                    foreach(var spell in enemy.Spells.Where(x => (int)x.Slot >= (int) SpellSlot.Q && (int) x.Slot <= (int)SpellSlot.R))
                        champMenu.Add(new MenuCheckbox(enemy.Name + " " + spell.SpellData.Name, spell.SpellData.Name + " Enabled", true));
                }
            }, 100);

            MiscMenu = MyMenu.AddSubMenu("Misc");

            QssMenu = MiscMenu.AddSubMenu("QSS");
            QssMenu.Add(new MenuCheckbox("Enabled", "Enabled", true));
            QssMenu.Add(new MenuSlider("QSSC", "If %Health Less Than", 1, 100, 80));
            foreach(var buffType in CC_BUFFS)
            {
                QssMenu.Add(new MenuCheckbox(buffType.ToString(), buffType.ToString(), true));
            }

            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 1050);
            E.SetSkillshot(0.25f, 130, 1400, false, SkillshotType.SkillshotLine);
            R = new Spell(SpellSlot.R, 3000);
            R.SetSkillshot(0.25f, 160f, 2000f, false, SkillshotType.SkillshotLine);

            AxeCount = 0;
            if(myHero.HasBuff("dravenspinningleft"))
                AxeCount = 2;

            if (myHero.HasBuff("DravenSpinning"))
                AxeCount = 1;

            Game.OnTick += Game_OnTick;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
            Obj_AI_Base.OnBuffGained += Obj_AI_Base_OnBuffGained;
            Obj_AI_Base.OnBuffLost += Obj_AI_Base_OnBuffLost;
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
        }

        private void GameObject_OnDelete(GameObject sender, System.EventArgs args)
        {
            if (sender == null) return;
            if (R_Obj != null && sender.Handle == R_Obj.Handle)
            {
                R_Obj = null;
            } else if (sender.Name == "Draven_Base_Q_reticle_self.troy")
            {
                var axe = Axes.FirstOrDefault(x => x.Axe.Handle == sender.Handle);
                if(axe != null)
                {
                    Axes.Remove(axe);
                }
            }
        }

        private void GameObject_OnCreate(GameObject sender, System.EventArgs args)
        {
            if (sender == null) return;
            /*if(sender.IsSpell && sender.SpellOwner.IsMe && sender.SpellName == "DravenR")
            {
                R_Obj = sender;
            }*/
            if(sender.Name == "Draven_Base_Q_reticle_self.troy")
            {
                Axes.Add(new ActiveAxe()
                {
                    Axe = sender,
                    StartTime = Game.ClockTime
                });
            }
        }

        private void Obj_AI_Base_OnBuffLost(Obj_AI_Base sender, HesaEngine.SDK.Args.Obj_AI_BaseBuffLostEventArgs args)
        {
            if(sender != null && args.Buff != null && sender.IsMe)
            {
                if(args.Buff.Name == "DravenSpinning")
                {
                    AxeCount = 0;
                }
            }
        }

        private void Obj_AI_Base_OnBuffGained(Obj_AI_Base sender, HesaEngine.SDK.Args.Obj_AI_BaseBuffGainedEventArgs args)
        {
            if(sender != null && args.Buff != null && sender.IsMe)
            {
                if(CC_BUFFS.Contains(args.Buff.Type) && QssMenu.Get<MenuCheckbox>("Enabled").Checked && QssMenu.Get<MenuCheckbox>(args.Buff.Type.ToString()).Checked)
                {
                    var qssSlot = Shop.GetItemSpellSlot(ItemId.Quicksilver_Sash);
                    if (qssSlot != SpellSlot.Unknown)
                    {
                        if(Shop.CanUseItem(ItemId.Quicksilver_Sash))
                        {
                            Shop.UseItem((int)ItemId.Quicksilver_Sash);
                        }
                    }
                    else
                    {
                        qssSlot = Shop.GetItemSpellSlot(ItemId.Mercurial_Scimitar);
                        if (qssSlot != SpellSlot.Unknown)
                        {
                            if (Shop.CanUseItem(ItemId.Mercurial_Scimitar))
                            {
                                Shop.UseItem((int)ItemId.Mercurial_Scimitar);
                            }
                        }
                    }
                }else if(args.Buff.Type == BuffType.Slow && W.IsReady() && AutoWMenu.Get<MenuCheckbox>("AWS").Checked)
                {
                    W.Cast();
                }else if(args.Buff.Name == "DravenSpinning")
                {
                    if(AxeCount < 2)
                    {
                        if((Game.ClockTime - LastQ) > 0.5f)
                        {
                            OnAxeCatch();
                        }
                    }
                    AxeCount = 1;
                }
                else if (args.Buff.Name.ToLower() == "dravenspinningleft")
                {
                    AxeCount = 2;
                    if ((Game.ClockTime - LastQ) > 0.5f)
                    {
                        OnAxeCatch();
                    }
                }
            }
        }

        private void Obj_AI_Base_OnNewPath(Obj_AI_Base unit, HesaEngine.SDK.Args.GameObjectNewPathEventArgs args)
        {
            if (unit == null) return;
            if(unit.ObjectType == GameObjectType.AIHeroClient && unit.IsEnemy && args.IsDash && GapCloserMenu.Get<MenuCheckbox>("AGCE").Checked && GapCloserMenu.Get<MenuCheckbox>(unit.NetworkId.ToString()) != null && GapCloserMenu.Get<MenuCheckbox>(unit.NetworkId.ToString()).Checked && GetDistance(args.Path.Last()) <= GetDistance(unit) && GetDistance(args.Path.Last()) <= E.Range && unit.IsValidTarget())
            {
                CastE(unit);
            }
            if (unit.ObjectType == GameObjectType.AIHeroClient && unit.IsEnemy && args.IsDash && args.Path.Length > 1)
            {
                for (var i = 0; i < ActiveChannels.Count; i++)
                {
                    var channel = ActiveChannels[i];
                    if (channel.Unit.NetworkId == unit.NetworkId && channel.IsChannel)
                    {
                        ActiveChannels.Remove(channel);
                    }
                }
            }
        }

        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, HesaEngine.SDK.Args.GameObjectProcessSpellCastEventArgs args)
        {
            if (sender == null) return;
            if(sender.IsMe)
            {
                if(args.SData.Name == "DravenSpinning")
                {
                    LastQ = Game.GameTimeTickCount;
                }else if (args.SData.Name.Contains("Attack"))
                {
                    LastAttack = Game.GameTimeTickCount - (Game.Ping / 2);
                }
            }else if(args.SData != null && sender.ObjectType == GameObjectType.AIHeroClient && sender.IsEnemy && CHANNEL_SPELLS.ContainsKey(args.SData.Name))
            {
                var k = CHANNEL_SPELLS[args.SData.Name];
                if(k.Name == sender.BaseSkinName)
                {
                    ActiveChannels.Add(new ActiveChannel()
                    {
                        Unit = sender,
                        Target = args.Target,
                        StartTime = Game.ClockTime,
                        Duration = k.Duration,
                        IsChannel = k.IsChannel,
                        Name = args.SData.Name
                    });
                }
            }
        }

        private void Drawing_OnDraw(System.EventArgs args)
        {
            
        }

        private void Game_OnTick()
        {
            if(Orbwalk.ActiveMode == OrbwalkingMode.Combo)
            {
                Combo();
            }
            if(AxeMenu.Get<MenuCheckbox>("Enabled").Checked)
            {
                CatchAxes();
            }
            if (AntiChannelMenu.Get<MenuCheckbox>("Enabled").Checked)
            {
                if (E.IsReady() && AntiChannelMenu.Get<MenuCheckbox>("ACE").Checked)
                {
                    for (var i = 0; i < ActiveChannels.Count; i++)
                    {
                        var channel = ActiveChannels[i];
                        var spellEndTime = (channel.StartTime + channel.Duration);
                        if (channel.Unit.IsDead || Game.ClockTime >= spellEndTime)
                        {
                            ActiveChannels.Remove(channel);
                        } else
                        {
                            var champMenu = AntiChannelMenu.SubMenu(channel.Unit.CharData.BaseSkinName);
                            if(champMenu != null && champMenu.Get<MenuCheckbox>(channel.Unit.Name + " " + channel.Name).Checked && channel.Unit.IsValidTarget())
                            {
                                var distance = GetDistance(channel.Unit) - myHero.BoundingRadius;
                                var timeToReach = E.Delay + (distance / E.Speed);
                                if(spellEndTime - Game.ClockTime > timeToReach)
                                {
                                    CastE(channel.Unit);
                                }
                            }
                        }
                    }
                }
            }
        }

        float GetDistance(GameObject unit)
        {
            if (unit == null) return float.MaxValue;
            return myHero.ServerPosition.To2D().Distance(unit.ServerPosition.To2D());
        }

        float GetDistance(GameObject from, GameObject unit)
        {
            if (from == null || unit == null) return float.MaxValue;
            return from.ServerPosition.To2D().Distance(unit.ServerPosition.To2D());
        }

        float GetDistance(Vector3 from, GameObject unit)
        {
            if (from == null || unit == null) return float.MaxValue;
            return from.To2D().Distance(unit.ServerPosition.To2D());
        }

        float GetDistance(Vector3 pos)
        {
            if (pos == null || pos == Vector3.Zero) return float.MaxValue;
            return myHero.ServerPosition.To2D().Distance(pos.To2D());
        }

        float GetWindUp()
        {
            return (1 / (0.625f / (AttackDelayOffsetPercent + 1) * myHero.AttackSpeed)) * (AttackDelayCastOffsetPercent + 0.3f);
        }

        float GetAnimationTime()
        {
            return 1 / (0.625f / (AttackDelayOffsetPercent + 1) * myHero.AttackSpeed);
        }

        private void Combo()
        {
        }

        private void CatchAxes()
        {
            Orbwalk.SetOrbwalkingPoint(Vector3.Zero);
            if (AxeMenu.Get<MenuCheckbox>("OC2").Checked && AxeCount == 2)
                return;
            ActiveAxe best = null;
            ActiveAxe best2 = null;
            var closest = float.MaxValue;
            //1st Axe
            foreach(var axe in Axes)
            {
                var distance = GetDistance(axe.Axe);
                if(distance < closest)
                {
                    closest = distance;
                    best = axe;
                }
            }
            if (best == null) return;
            var bestAxe = best.Axe;
            //2nd Axe
            if(Axes.Count > 1)
            {
                closest = float.MaxValue;
                foreach (var axe in Axes)
                {
                    if (axe != best)
                    {
                        var distance = GetDistance(axe.Axe);
                        if (distance < closest)
                        {
                            closest = distance;
                            best2 = axe;
                        }
                    }
                }
            }
            //Lets pick them:D
            var animationTime = GetAnimationTime();
            var windUp = GetWindUp();
            var timeToLand = (best.StartTime + 2) - Game.ClockTime;
            var Distance = GetDistance(bestAxe) - bestAxe.BoundingRadius;
            var nextAttack = LastAttack + (animationTime * 1000) + (Game.Ping / 2) + 25;
            var timeToReach = (Distance / myHero.MovementSpeed);
            var TimeTillNextAttack = (nextAttack - Game.GameTimeTickCount) / 1000;
            if(timeToReach > TimeTillNextAttack)
            {
                timeToReach = (float)(timeToReach + windUp + (windUp * System.Math.Floor((timeToReach - TimeTillNextAttack) / animationTime)));
            }
            var MousePos = Game.CursorPosition;
            var CatchPos = MousePos;
            if(best2 != null)
            {
                CatchPos = bestAxe.Position + (best2.Axe.Position - bestAxe.Position).Normalized() * System.Math.Min(bestAxe.BoundingRadius * 0.8f, GetDistance(best2.Axe, bestAxe));
            }else
                CatchPos = bestAxe.Position + (MousePos - bestAxe.Position).Normalized() * System.Math.Min(bestAxe.BoundingRadius * 0.8f, GetDistance(MousePos, bestAxe));
            if(timeToLand > timeToReach)
            {
                Orbwalk.SetOrbwalkingPoint(CatchPos);
            }else if(AxeMenu.Get<MenuCheckbox>("AxeMenu").Checked && W.IsReady())
            {
                var NewMs = myHero.MovementSpeed * (1 + (0.35f + (0.05f * W.Level)));
                var NewTimeToReach = Distance / NewMs;
                if(NewTimeToReach > TimeTillNextAttack)
                {
                    timeToReach = (float)(timeToReach + windUp + (windUp * System.Math.Floor((NewTimeToReach - TimeTillNextAttack) / animationTime)));
                }
                if(timeToLand > NewTimeToReach)
                {
                    W.Cast();
                    Orbwalk.SetOrbwalkingPoint(CatchPos);
                }
            }
        }

        private void OnAxeCatch()
        {
            if(W.IsReady() && AutoWMenu.Get<MenuCheckbox>("Enabled").Checked && AutoWMenu.Get<MenuCheckbox>("AC").Checked && AutoWMenu.Get<MenuCheckbox>("AWK").Checked && myHero.HealthPercent <= AutoWMenu.Get<MenuSlider>("ACC").CurrentValue)
            {
                W.Cast();
            }
        }

        private void CastE(Obj_AI_Base unit)
        {
            if(unit.IsValidTarget(E.Range))
            {
                var prediction = E.GetPrediction(unit);
                if((int)prediction.Hitchance >= (int)HitChance.High)
                {
                    E.Cast(prediction.CastPosition);
                }
            }
        }
    }
}