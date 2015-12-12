﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using AdEvade.Config;
using AdEvade.Config.Controls;
using AdEvade.Data;
using AdEvade.Data.EvadeSpells;
using AdEvade.Draw;
using AdEvade.Helpers;
using AdEvade.Utils;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using CheckBox = EloBuddy.SDK.Menu.Values.CheckBox;
using PositionInfo = AdEvade.Data.PositionInfo;
using SpellData = AdEvade.Data.Spells.SpellData;
using Debug = AdEvade.Draw.Debug;
using MainMenu = EloBuddy.SDK.Menu.MainMenu;
using Menu = EloBuddy.SDK.Menu.Menu;
using Spell = AdEvade.Data.Spells.Spell;

// ReSharper disable AccessToStaticMemberViaDerivedType

namespace AdEvade
{
    public class AdEvade
    {
        public const string LastUpdate = "9:10 AM, 10th December 2015";
        public static SpellDetector SpellDetector;
        private static SpellDrawer _spellDrawer;
        //private static EvadeTester _evadeTester;
        private static EvadeSpell _evadeSpell;
        //private static SpellTester _spellTester;
        private static AutoSetPing _autoSetPing;

        public static SpellSlot LastSpellCast;
        public static float LastSpellCastTime = 0;

        public static float LastWindupTime = 0;

        public static float LastTickCount = 0;
        public static float LastStopEvadeTime = 0;

        public static Vector3 LastMovementBlockPos = Vector3.Zero;
        public static float LastMovementBlockTime = 0;

        public static float LastEvadeOrderTime = 0;
        public static float LastIssueOrderGameTime = 0;
        public static float LastIssueOrderTime = 0;
        public static PlayerIssueOrderEventArgs LastIssueOrderArgs = null;

        public static Vector2 LastMoveToPosition = Vector2.Zero;
        public static Vector2 LastMoveToServerPos = Vector2.Zero;
        public static Vector2 LastStopPosition = Vector2.Zero;

        public static DateTime AssemblyLoadTime = DateTime.Now;

        public static bool IsDodging = false;
        public static bool DodgeOnlyDangerous = false;

        public static bool HasGameEnded = false;
        public static bool IsChanneling = false;
        public static Vector2 ChannelPosition = Vector2.Zero;

        public static Data.PositionInfo LastPosInfo;

        public static EvadeCommand LastEvadeCommand = new EvadeCommand
        {
            IsProcessed = true,
            Timestamp = EvadeUtils.TickCount
        };

        public static EvadeCommand LastBlockedUserMoveTo = new EvadeCommand
        {
            IsProcessed = true,
            Timestamp = EvadeUtils.TickCount
        };

        public static float LastDodgingEndTime = 0;

        public static Menu Menu;

        public static float SumCalculationTime = 0;
        public static float NumCalculationTime = 0;
        public static float AvgCalculationTime = 0;
        public AdEvade()
        {
            Load();
        }

        private void Load()
        {
            //Loading.OnLoadingComplete += Game_OnGameLoad;
            Loading.OnLoadingComplete += Game_OnGameLoad_Disabled;
        }

        private void Game_OnGameLoad_Disabled(EventArgs args)
        {
            ConsoleDebug.WriteLineColor("Failed loading AdEvade...", ConsoleColor.Red);
            ConsoleDebug.WriteLine("   Disabled due to needed core update (as of 5.24), please be patient!");
            Chat.Print("<font color='#ff0000'>Failed loading AdEvade...</font>");
            Chat.Print("   Disabled due to needed core update (as of 5.24), please be patient!");
            Menu = MainMenu.AddMenu("AdEvade (Disabled)", "AdEvade", "AdEvade (Disabled)");
            Menu.AddGroupLabel("Disabled due to needed core update!");
            Menu.AddLabel("As the latest update has caused issues with getting buffs and sending movement commands\n" +
                          " AdEvade can not be fixed.\n");
            Menu.AddSeparator();
            Menu.AddLabel("Please be patient for an update and in the mean time use EvadePlus");
        }

        private void Game_OnGameLoad(EventArgs args)
        {
            ConsoleDebug.WriteLineColor("Loading...", ConsoleColor.Blue, true);
            ConfigPluginControler.LoadConfigPresets();

            try
            {
                Menu = MainMenu.AddMenu("AdEvade", "AdEvade");
                ConsoleDebug.WriteLineColor("   Creating Menu...", ConsoleColor.Yellow, true);
                Menu.AddGroupLabel("AdEvade (EzEvade Port)");
                Menu.AddLabel("Please report any bugs or anything you think is a ");
                Menu.AddLabel("problem / issue, on the GitHub Issues Section, or with a reply to the AdEvade forum thread.");
                Menu.Add("OpenGithub", new CheckBox("Open Github's Issues Section in browser", false)).OnValueChange +=
                    delegate(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs changeArgs)
                    {
                        if (changeArgs.OldValue == false && changeArgs.NewValue)
                        {
                            sender.CurrentValue = false;
                            Process.Start(@"https://github.com/coman3/EloBuddy.Addons/issues");
                        }
                    };
                Menu.AddLabel("All Credit for the actual evading (Movement and detection) in this assembly ");
                Menu.AddLabel("goes to the creator of EzEvade.");
                Menu.AddSeparator(100);

                Menu.AddLabel("Created By: Coman3");
                Menu.AddLabel("     Github: https://github.com/coman3/");
                Menu.Add("OpenGithubComan3", new CheckBox("Open Coman3's Github in Browser", false)).OnValueChange +=
                   delegate (ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs changeArgs)
                   {
                       if (changeArgs.OldValue == false && changeArgs.NewValue)
                       {
                           sender.CurrentValue = false;
                           Process.Start(@"https://github.com/coman3/");
                       }
                   };
                Menu.AddLabel("Last Update: " + LastUpdate);

                Menu mainMenu = Menu.AddSubMenu("Main", "Main");
                mainMenu.Add(ConfigValue.DodgeSkillShots.Name(), new DynamicKeyBind(ConfigValue.DodgeSkillShots, "Dodge SkillShots", true, KeyBind.BindTypes.PressToggle, 'K').KeyBind);
                mainMenu.Add(ConfigValue.ActivateEvadeSpells.Name(), new DynamicKeyBind(ConfigValue.ActivateEvadeSpells, "Use Evade Spells", true, KeyBind.BindTypes.PressToggle, 'K').KeyBind);
                mainMenu.Add(ConfigValue.OnlyDodgeDangerous.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.OnlyDodgeDangerous, "Dodge Only Dangerous", false).CheckBox);
                mainMenu.Add(ConfigValue.DodgeFowSpells.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.DodgeFowSpells, "Dodge FOW SkillShots", true).CheckBox);
                mainMenu.Add(ConfigValue.DodgeCircularSpells.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.DodgeCircularSpells, "Dodge Circular SkillShots", true).CheckBox);
                mainMenu.AddSeparator();
                mainMenu.Add(ConfigValue.DodgeDangerousKeysEnabled.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.DodgeDangerousKeysEnabled, "Enable Dodge Only Dangerous Keys", false).CheckBox);

                mainMenu.Add(ConfigValue.DodgeDangerousKey1.Name(), new DynamicKeyBind(ConfigValue.DodgeDangerousKey1, "Dodge Only Dangerous Key", false, KeyBind.BindTypes.HoldActive, 32).KeyBind);
                mainMenu.Add(ConfigValue.DodgeDangerousKey2.Name(), new DynamicKeyBind(ConfigValue.DodgeDangerousKey2, "Dodge Only Dangerous Key 2", false, KeyBind.BindTypes.HoldActive, 'V').KeyBind);

                mainMenu.AddSeparator();
                mainMenu.AddGroupLabel("Evade Mode");

                var sliderEvadeMode = new StringSlider(ConfigDataType.Data, "EvadeMode", "Evade Mode", 2, SpellConfigProperty.None, Enum.GetNames(typeof(EvadeMode)));
                sliderEvadeMode.Slider.Slider.OnValueChange += OnEvadeModeChange;
                mainMenu.Add("EvadeMode", sliderEvadeMode.Slider.Slider);

                ConsoleDebug.WriteLineColor("       Detecting Spells...", ConsoleColor.Yellow, true);
                SpellDetector = new SpellDetector(Menu);
                _evadeSpell = new EvadeSpell(Menu);

                ConsoleDebug.WriteLineColor("       Adding Humanizer and Miscellaneous Menus...", ConsoleColor.Yellow, true);
                Menu miscMenu = Menu.AddSubMenu("Misc Settings", "MiscSettings");
                miscMenu.Add(ConfigValue.HighPrecision.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.HighPrecision, "Enhanced Dodge Precision", false).CheckBox);
                miscMenu.Add(ConfigValue.RecalculatePath.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.RecalculatePath, "Recalculate Path", true).CheckBox);
                miscMenu.Add(ConfigValue.ContinueMovement.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.ContinueMovement, "Continue Last Movement", true).CheckBox);
                miscMenu.Add(ConfigValue.CalculateWindupDelay.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.CalculateWindupDelay, "Calculate Windup Delay", true).CheckBox);
                miscMenu.Add(ConfigValue.CheckSpellCollision.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.CheckSpellCollision, "Check Spell Collision", false).CheckBox);
                miscMenu.Add(ConfigValue.PreventDodgingUnderTower.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.PreventDodgingUnderTower, "Prevent Dodging Under Tower", false).CheckBox);
                miscMenu.Add(ConfigValue.PreventDodgingNearEnemy.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.PreventDodgingNearEnemy, "Prevent Dodging Near Enemies", false).CheckBox);
                miscMenu.Add(ConfigValue.AdvancedSpellDetection.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.AdvancedSpellDetection, "Advanced Spell Detection", false).CheckBox);
                miscMenu.Add(ConfigValue.ExtraDetectionRange.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraDetectionRange, "Extra Detection Range", 1000, 500, 5000).Slider);
                //TODO: Add Reset
                //miscMenu.AddSeparator(100);
                //miscMenu.AddGroupLabel("Reset");
                //miscMenu.Add("ResetConfig", new DynamicCheckBox(ConfigDataType.Data, "ResetConfig", "Reset Properties", false).CheckBox);

                Menu fastEvadeMenu = Menu.AddSubMenu("Fast Evade", "FastEvade");
                fastEvadeMenu.Add(ConfigValue.FastMovementBlock.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.FastMovementBlock, "Fast Movement Block", false).CheckBox);
                fastEvadeMenu.Add(ConfigValue.FastEvadeActivationTime.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.FastEvadeActivationTime, "FastEvade Activation Time", 65, 0, 500).Slider);
                fastEvadeMenu.Add(ConfigValue.SpellActivationTime.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.SpellActivationTime, "Spell Activation Time", 200, 0, 1000).Slider);
                fastEvadeMenu.Add(ConfigValue.RejectMinDistance.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.RejectMinDistance, "Collision Distance Buffer", 10, 0, 100).Slider);

                Menu limiterMenu = Menu.AddSubMenu("Humanizer", "Limiter");
                limiterMenu.Add(ConfigValue.ClickOnlyOnce.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.ClickOnlyOnce, "Click Only Once", true).CheckBox);
                limiterMenu.Add(ConfigValue.EnableEvadeDistance.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.EnableEvadeDistance, "Extended Evade", false).CheckBox);
                limiterMenu.Add(ConfigValue.TickLimiter.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.TickLimiter, "Tick Limiter", 100, 0, 500).Slider);
                limiterMenu.Add(ConfigValue.SpellDetectionTime.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.SpellDetectionTime, "Spell Detection Time", 0, 0, 1000).Slider);
                limiterMenu.Add(ConfigValue.ReactionTime.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ReactionTime, "Reaction Time", 0, 0, 500).Slider);
                limiterMenu.Add(ConfigValue.DodgeInterval.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.DodgeInterval, "Dodge Interval", 0, 0, 2000).Slider);
                

                Menu bufferMenu = Menu.AddSubMenu("Adv. Humanizer", "ExtraBuffers");
                bufferMenu.Add(ConfigValue.ExtraPingBuffer.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraPingBuffer, "Extra Ping Buffer", 65, 0, 200).Slider);
                bufferMenu.Add(ConfigValue.ExtraCpaDistance.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraCpaDistance, "Extra Collision Distance", 10, 0, 150).Slider);
                bufferMenu.Add(ConfigValue.ExtraSpellRadius.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraSpellRadius, "Extra Spell Radius", 0, 0, 100).Slider);
                bufferMenu.Add(ConfigValue.ExtraEvadeDistance.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraEvadeDistance, "Extra Evade Distance", 100, 0, 300).Slider);
                //bufferMenu.Add(ConfigValue.ExtraSpellRadius.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.ExtraSpellRadius, "Extra Avoid Distance", 50, 0, 300).Slider);
                bufferMenu.Add(ConfigValue.MinimumComfortZone.Name(), new DynamicSlider(ConfigDataType.Data, ConfigValue.MinimumComfortZone, "Minimum Distance to Champions", 550, 0, 1000).Slider);

                Menu debugMenu = Menu.AddSubMenu("Debug", "DebugMenu");

                debugMenu.AddGroupLabel("Debug");
                debugMenu.Add(ConfigValue.ShowDebugInfo.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.ShowDebugInfo, "Show Debug Info", false).CheckBox);
                //debugMenu.Add("DebugWithMySpells", new DynamicCheckBox(ConfigDataType.Data, "DebugWithMySpells", "Detect and draw my spells", false).CheckBox); //TODO: Remove From Addon

                debugMenu.AddSeparator();
                debugMenu.Add(ConfigValue.EnableSpellTester.Name(), new DynamicCheckBox(ConfigDataType.Data, ConfigValue.EnableSpellTester, "Enable Spell Tester", false).CheckBox);
                debugMenu.AddLabel("Press F5 after enabling / disabling the Spell Tester to load / unload it.");
                _spellDrawer = new SpellDrawer(Menu);
                //_spellTester = new SpellTester(Menu);

                Debug.DrawTopLeft("Showing Debug info...");

                ConsoleDebug.WriteLineColor("   Hooking Events...", ConsoleColor.Yellow, true);
                Player.OnIssueOrder += Game_OnIssueOrder;
                Spellbook.OnCastSpell += Game_OnCastSpell;
                Game.OnUpdate += Game_OnGameUpdate;

                AIHeroClient.OnProcessSpellCast += Game_OnProcessSpell;

                Game.OnEnd += Game_OnGameEnd;
                SpellDetector.OnProcessDetectedSpells += SpellDetector_OnProcessDetectedSpells;
                Orbwalker.OnPreAttack += Orbwalking_BeforeAttack;

                ConsoleDebug.WriteLineColor("   Setting Loaded Presets Values...", ConsoleColor.Yellow, true);
                ConfigPluginControler.SelectedPreset.LoadConfig();

            }
            catch (Exception e)
            {
                Chat.Print(e);
            }
            ConsoleDebug.WriteLineColor("Successfully Loaded!", ConsoleColor.Green, true);
        }

        private void OnEvadeModeChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs changeArgs)
        {
            //TODO: Change
            var mode = sender.DisplayName;

            if (mode == "Very Smooth")
            {
                ConfigValue.FastEvadeActivationTime.SetInt(0);
                ConfigValue.RejectMinDistance.SetInt(0);
                ConfigValue.ExtraCpaDistance.SetInt(0);
                ConfigValue.ExtraPingBuffer.SetInt(40);
            }
            else if (mode == "Smooth")
            {
                ConfigValue.FastEvadeActivationTime.SetInt(65);
                ConfigValue.RejectMinDistance.SetInt(10);
                ConfigValue.ExtraCpaDistance.SetInt(10);
                ConfigValue.ExtraPingBuffer.SetInt(65);
            }
        }

        private void Game_OnGameEnd(GameEndEventArgs args)
        {
            HasGameEnded = true;
        }

        private void Game_OnCastSpell(Spellbook spellbook, SpellbookCastSpellEventArgs args)
        {
            if (!spellbook.Owner.IsMe)
                return;

            var sData = spellbook.GetSpell(args.Slot);
            string name;

            if (SpellDetector.ChanneledSpells.TryGetValue(sData.Name, out name))
            {
                //Evade.isChanneling = true;
                //Evade.channelPosition = GameData.HeroInfo.serverPos2D;
                LastStopEvadeTime = EvadeUtils.TickCount + Game.Ping + 100;
            }

            if (EvadeSpell.LastSpellEvadeCommand != null &&
                EvadeSpell.LastSpellEvadeCommand.Timestamp + Game.Ping + 150 > EvadeUtils.TickCount)
            {
                args.Process = false;
            }

            LastSpellCast = args.Slot;
            LastSpellCastTime = EvadeUtils.TickCount;

            //moved from processPacket

            /*if (args.Slot == SpellSlot.Recall)
            {
                lastStopPosition = GameData.MyHero.ServerPosition.To2D();
            }*/

            if (Situation.ShouldDodge())
            {
                if (IsDodging && SpellDetector.Spells.Count() > 0)
                {
                    foreach (KeyValuePair<String, SpellData> entry in SpellDetector.WindupSpells)
                    {
                        SpellData spellData = entry.Value;

                        if (spellData.SpellKey == args.Slot) //check if it's a spell that we should block
                        {
                            args.Process = false;
                            return;
                        }
                    }
                }
            }

            foreach (var evadeSpell in EvadeSpell.EvadeSpells)
            {
                if (evadeSpell.IsItem == false && evadeSpell.SpellKey == args.Slot)
                {
                    if (evadeSpell.EvadeType == EvadeType.Blink
                        || evadeSpell.EvadeType == EvadeType.Dash)
                    {
                        //Block spell cast if flashing/blinking into spells
                        if (args.EndPosition.To2D().CheckDangerousPos(6, true)) //for blink + dash
                        {
                            args.Process = false;
                            return;
                        }

                        if (evadeSpell.EvadeType == EvadeType.Dash)
                        {
                            var extraDelayBuffer = Config.Properties.GetInt(ConfigValue.ExtraPingBuffer);
                            var extraDist = Config.Properties.GetInt(ConfigValue.ExtraCpaDistance);

                            var dashPos = Game.CursorPos.To2D(); //real pos?

                            if (evadeSpell.FixedRange)
                            {
                                var dir = (dashPos - GameData.MyHero.ServerPosition.To2D()).Normalized();
                                dashPos = GameData.MyHero.ServerPosition.To2D() + dir * evadeSpell.Range;
                            }

                            //Draw.RenderObjects.Add(new Draw.RenderPosition(dashPos, 1000));

                            var posInfo = EvadeHelper.CanHeroWalkToPos(dashPos, evadeSpell.Speed,
                                extraDelayBuffer + Game.Ping, extraDist);

                            if (posInfo.PosDangerLevel > 0)
                            {
                                args.Process = false;
                                return;
                            }
                        }

                        LastPosInfo = PositionInfo.SetAllUndodgeable(); //really?

                        if (IsDodging || EvadeUtils.TickCount < LastDodgingEndTime + 500)
                        {
                            EvadeCommand.MoveTo(Game.CursorPos.To2D()); //block moveto
                            LastStopEvadeTime = EvadeUtils.TickCount + Game.Ping + 100;
                        }
                    }
                    return;
                }
            }
        }

        private void Game_OnIssueOrder(Obj_AI_Base hero, PlayerIssueOrderEventArgs args)
        {

            if (!hero.IsMe)
                return;

            if (!Situation.ShouldDodge())
                return;

            if (args.Order == GameObjectOrder.MoveTo)
            {
                //movement block code goes in here
                if (IsDodging && SpellDetector.Spells.Count > 0)
                {
                    CheckHeroInDanger();

                    LastBlockedUserMoveTo = new EvadeCommand
                    {
                        Order = EvadeOrderCommand.MoveTo,
                        TargetPosition = args.TargetPosition.To2D(),
                        Timestamp = EvadeUtils.TickCount,
                        IsProcessed = false,
                    };

                    args.Process = false; //Block the command
                }
                else
                {
                    var movePos = args.TargetPosition.To2D();
                    var extraDelay = Config.Properties.GetInt(ConfigValue.ExtraPingBuffer);
                    if (EvadeHelper.CheckMovePath(movePos, Game.Ping + extraDelay))
                    {
                        /*if (() Properties.Properties.Data["AllowCrossing"].Cast<CheckBox>().CurrentValue)
                        {
                            var extraDelayBuffer = () Properties.Properties.Data["ExtraPingBuffer"]
                                 + 30;
                            var extraDist = () Properties.Properties.Data["ExtraCPADistance"]
                                 + 10;

                            var tPosInfo = EvadeHelper.CanHeroWalkToPos(movePos, GameData.HeroInfo.moveSpeed, extraDelayBuffer + Game.Ping, extraDist);

                            if (tPosInfo.posDangerLevel == 0)
                            {
                                lastPosInfo = tPosInfo;
                                return;
                            }
                        }*/

                        LastBlockedUserMoveTo = new EvadeCommand
                        {
                            Order = EvadeOrderCommand.MoveTo,
                            TargetPosition = args.TargetPosition.To2D(),
                            Timestamp = EvadeUtils.TickCount,
                            IsProcessed = false,
                        };

                        args.Process = false; //Block the command

                        if (EvadeUtils.TickCount - LastMovementBlockTime < 500 &&
                            LastMovementBlockPos.Distance(args.TargetPosition) < 100)
                        {
                            return;
                        }

                        LastMovementBlockPos = args.TargetPosition;
                        LastMovementBlockTime = EvadeUtils.TickCount;

                        var posInfo = EvadeHelper.GetBestPositionMovementBlock(movePos);
                        if (posInfo != null)
                        {
                            EvadeCommand.MoveTo(posInfo.Position);
                        }
                        return;
                    }
                    else
                    {
                        LastBlockedUserMoveTo.IsProcessed = true;
                    }
                }
            }
            else //need more logic
            {
                if (IsDodging)
                {
                    args.Process = false; //Block the command
                }
                else
                {
                    if (args.Order == GameObjectOrder.AttackUnit)
                    {
                        var target = args.Target;
                        if (target != null && target.GetType() == typeof(Obj_AI_Base) && ((Obj_AI_Base)target).IsValid())
                        {
                            var baseTarget = target as Obj_AI_Base;
                            if (GameData.HeroInfo.ServerPos2D.Distance(baseTarget.ServerPosition.To2D()) >
                                GameData.MyHero.AttackRange + GameData.HeroInfo.BoundingRadius + baseTarget.BoundingRadius)
                            {
                                var movePos = args.TargetPosition.To2D();
                                var extraDelay = Config.Properties.GetInt(ConfigValue.ExtraPingBuffer);
                                if (EvadeHelper.CheckMovePath(movePos, Game.Ping + extraDelay))
                                {
                                    args.Process = false; //Block the command
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            if (args.Process == true)
            {
                LastIssueOrderGameTime = Game.Time * 1000;
                LastIssueOrderTime = EvadeUtils.TickCount;
                LastIssueOrderArgs = args;

                if (args.Order == GameObjectOrder.MoveTo)
                {
                    LastMoveToPosition = args.TargetPosition.To2D();
                    LastMoveToServerPos = GameData.MyHero.ServerPosition.To2D();
                }

                if (args.Order == GameObjectOrder.Stop)
                {
                    LastStopPosition = GameData.MyHero.ServerPosition.To2D();
                }
            }
        }

        private void Orbwalking_BeforeAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            if (IsDodging)
            {
                args.Process = false; //Block orbwalking
            }
        }

        private void Game_OnProcessSpell(Obj_AI_Base hero, GameObjectProcessSpellCastEventArgs args)
        {
            if (!hero.IsMe)
            {
                return;
            }

            /*if (args.SData.Name.Contains("Recall"))
            {
                var distance = lastStopPosition.Distance(args.Start.To2D());
                float moveTime = 1000 * distance / GameData.MyHero.MoveSpeed;

                ConsoleDebug.WriteLine("Extra dist: " + distance + " Extra Delay: " + moveTime);
            }*/

            string name;
            if (SpellDetector.ChanneledSpells.TryGetValue(args.SData.Name, out name))
            {
                IsChanneling = true;
                ChannelPosition = GameData.MyHero.ServerPosition.To2D();
            }
            if (ConfigValue.CalculateWindupDelay.GetBool())
            {
                var castTime = (hero.Spellbook.CastTime - Game.Time) * 1000;

                if (castTime > 0 && !EloBuddy.SDK.Constants.AutoAttacks.IsAutoAttack(args.SData.Name)
                    && Math.Abs(castTime - GameData.MyHero.AttackCastDelay * 1000) > 1)
                {
                    LastWindupTime = EvadeUtils.TickCount + castTime - Game.Ping / 2;
                    if (IsDodging)
                    {
                        SpellDetector_OnProcessDetectedSpells(); //reprocess
                    }
                }
            }
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            try
            {
                CheckHeroInDanger();
                CheckDodgeOnlyDangerous();
                if (IsChanneling && ChannelPosition.Distance(GameData.HeroInfo.ServerPos2D) > 50
                    ) //TODO: !GameData.MyHero.IsChannelingImportantSpell()
                {
                    IsChanneling = false;
                }

                //if (() Properties.Properties.Data["ResetConfig"].Cast<CheckBox>().CurrentValue)
                //{
                //    ResetConfig();
                //    menu["ResetConfig"].Cast<CheckBox>().CurrentValue = false;
                //}

                //if (() Properties.Properties.Data["ResetConfig200"].Cast<CheckBox>().CurrentValue)
                //{
                //    SetPatchConfig();
                //    menu["ResetConfig200"].Cast<CheckBox>().CurrentValue = false;
                //}

                var limitDelay = ConfigValue.TickLimiter.GetInt();
                //Tick limiter                
                if (EvadeUtils.TickCount - LastTickCount > limitDelay
                    && EvadeUtils.TickCount > LastStopEvadeTime)
                {
                    DodgeSkillShots(); //walking           

                    ContinueLastBlockedCommand();
                    LastTickCount = EvadeUtils.TickCount;
                }

                EvadeSpell.UseEvadeSpell(); //using spells
                RecalculatePath();
            }
            catch (Exception e)
            {
                ConsoleDebug.WriteLine(e);
            }
        }

        private void RecalculatePath()
        {
            if (ConfigValue.RecalculatePath.GetBool() && IsDodging) //recheck path
            {
                if (LastPosInfo != null && !LastPosInfo.RecalculatedPath)
                {
                    var path = GameData.MyHero.Path;
                    if (path.Length > 0)
                    {
                        var movePos = path.Last().To2D();

                        if (movePos.Distance(LastPosInfo.Position) < 5) //more strict checking
                        {
                            var posInfo = EvadeHelper.CanHeroWalkToPos(movePos, GameData.HeroInfo.MoveSpeed, 0, 0,
                                false);
                            if (posInfo.PosDangerCount > LastPosInfo.PosDangerCount)
                            {
                                LastPosInfo.RecalculatedPath = true;

                                if (EvadeSpell.PreferEvadeSpell())
                                {
                                    LastPosInfo = PositionInfo.SetAllUndodgeable();
                                }
                                else
                                {
                                    var newPosInfo = EvadeHelper.GetBestPosition();
                                    if (newPosInfo.PosDangerCount < posInfo.PosDangerCount)
                                    {
                                        LastPosInfo = newPosInfo;
                                        CheckHeroInDanger();
                                        DodgeSkillShots();
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ContinueLastBlockedCommand()
        {
            if (ConfigValue.ContinueMovement.GetBool() && Situation.ShouldDodge())
            {
                var movePos = LastBlockedUserMoveTo.TargetPosition;
                var extraDelay = Config.Properties.GetInt(ConfigValue.ExtraPingBuffer);

                if (IsDodging == false && LastBlockedUserMoveTo.IsProcessed == false
                    && EvadeUtils.TickCount - LastEvadeCommand.Timestamp > Game.Ping + extraDelay
                    && EvadeUtils.TickCount - LastBlockedUserMoveTo.Timestamp < 1500)
                {
                    movePos = movePos + (movePos - GameData.HeroInfo.ServerPos2D).Normalized()
                              * EvadeUtils.Random.NextFloat(1, 65);

                    if (!EvadeHelper.CheckMovePath(movePos, Game.Ping + extraDelay))
                    {
                        //ConsoleDebug.WriteLine("Continue Movement");
                        //GameData.MyHero.IssueOrder(GameObjectOrder.MoveTo, movePos.To3D());
                        EvadeCommand.MoveTo(movePos);
                        LastBlockedUserMoveTo.IsProcessed = true;
                    }
                }
            }
        }

        private void CheckHeroInDanger()
        {
            bool playerInDanger = false;
            foreach (KeyValuePair<int, Spell> entry in SpellDetector.Spells)
            {
                Spell spell = entry.Value;

                if (LastPosInfo != null) //&& lastPosInfo.dodgeableSpells.Contains(spell.spellID))
                {
                    if (GameData.MyHero.ServerPosition.To2D().InSkillShot(spell, GameData.HeroInfo.BoundingRadius))
                    {
                        playerInDanger = true;
                        break;
                    }

                    if (ConfigValue.EnableEvadeDistance.GetBool() && EvadeUtils.TickCount < LastPosInfo.EndTime)
                    {
                        playerInDanger = true;
                        break;
                    }
                }
            }

            if (IsDodging && !playerInDanger)
            {
                LastDodgingEndTime = EvadeUtils.TickCount;
            }

            if (IsDodging == false && !Situation.ShouldDodge())
                return;

            IsDodging = playerInDanger;
        }

        private void DodgeSkillShots()
        {
            if (!Situation.ShouldDodge())
            {
                IsDodging = false;
                return;
            }

            /*
            if (isDodging && playerInDanger == false) //serverpos test
            {
                GameData.MyHero.IssueOrder(GameObjectOrder.HoldPosition, myHero, false);
            }*/

            if (IsDodging)
            {

                if (LastPosInfo != null)
                {

                    /*foreach (KeyValuePair<int, Spell> entry in SpellDetector.spells)
                    {
                        Spell spell = entry.Value;

                        ConsoleDebug.WriteLine("" + (int)(TickCount-spell.startTime));
                    }*/


                    Vector2 lastBestPosition = LastPosInfo.Position;

                    if (!ConfigValue.ClickOnlyOnce.GetBool() ||
                        !(GameData.MyHero.Path.Length > 0 &&
                          LastPosInfo.Position.Distance(GameData.MyHero.Path.Last().To2D()) < 5))
                        //|| lastPosInfo.timestamp > lastEvadeOrderTime)
                    {
                        EvadeCommand.MoveTo(lastBestPosition);
                        LastEvadeOrderTime = EvadeUtils.TickCount;
                    }
                }
            }
            else //if not dodging
            {
                //Check if hero will walk into a skillshot
                var path = GameData.MyHero.Path;
                if (path.Length > 0)
                {
                    var movePos = path[path.Length - 1].To2D();

                    if (EvadeHelper.CheckMovePath(movePos))
                    {
                        /*if (() Properties.Properties.Data["AllowCrossing"].Cast<CheckBox>().CurrentValue)
                        {
                            var extraDelayBuffer = () Properties.Properties.Data["ExtraPingBuffer"]
                                 + 30;
                            var extraDist = () Properties.Properties.Data["ExtraCPADistance"]
                                 + 10;

                            var tPosInfo = EvadeHelper.CanHeroWalkToPos(movePos, GameData.HeroInfo.moveSpeed, extraDelayBuffer + Game.Ping, extraDist);

                            if (tPosInfo.posDangerLevel == 0)
                            {
                                lastPosInfo = tPosInfo;
                                return;
                            }
                        }*/

                        var posInfo = EvadeHelper.GetBestPositionMovementBlock(movePos);
                        if (posInfo != null)
                        {
                            EvadeCommand.MoveTo(posInfo.Position);
                        }
                        return;
                    }
                }
            }
        }

        public void CheckLastMoveTo()
        {
            if (ConfigValue.FastMovementBlock.GetBool())
            {
                if (IsDodging == false && LastIssueOrderArgs != null
                    && LastIssueOrderArgs.Order == GameObjectOrder.MoveTo
                    && Game.Time * 1000 - LastIssueOrderGameTime < 500)
                {
                    Game_OnIssueOrder(GameData.MyHero, LastIssueOrderArgs);
                    LastIssueOrderArgs = null;
                }
            }
        }

        public static bool IsDodgeDangerousEnabled()
        {
            if (ConfigValue.OnlyDodgeDangerous.GetBool())
            {
                return true;
            }

            if (ConfigValue.DodgeDangerousKeysEnabled.GetBool())
            {
                if (ConfigValue.DodgeDangerousKey1.GetBool() || ConfigValue.DodgeDangerousKey2.GetBool())
                    return true;
            }

            return false;
        }

        public static void CheckDodgeOnlyDangerous() //Dodge only dangerous event
        {
            bool bDodgeOnlyDangerous = IsDodgeDangerousEnabled();

            if (DodgeOnlyDangerous && bDodgeOnlyDangerous)
            {
                SpellDetector.RemoveNonDangerousSpells();
                DodgeOnlyDangerous = true;
            }
            else
            {
                DodgeOnlyDangerous = bDodgeOnlyDangerous;
            }
        }

        public static void SetAllUndodgeable()
        {
            LastPosInfo = PositionInfo.SetAllUndodgeable();
        }

        private void SpellDetector_OnProcessDetectedSpells()
        {
            GameData.HeroInfo.UpdateInfo();
            if (!ConfigValue.DodgeSkillShots.GetBool())
            {
                LastPosInfo = PositionInfo.SetAllUndodgeable();
                EvadeSpell.UseEvadeSpell();
                return;
            }
            if (GameData.HeroInfo.ServerPos2D.CheckDangerousPos(0)
                || GameData.HeroInfo.ServerPos2DExtra.CheckDangerousPos(0))
            {
                if (EvadeSpell.PreferEvadeSpell())
                {
                    LastPosInfo = PositionInfo.SetAllUndodgeable();
                }
                else
                {
                    var calculationTimer = EvadeUtils.TickCount;

                    var posInfo = EvadeHelper.GetBestPosition();

                    var caculationTime = EvadeUtils.TickCount - calculationTimer;

                    if (NumCalculationTime > 0)
                    {
                        SumCalculationTime += caculationTime;
                        AvgCalculationTime = SumCalculationTime / NumCalculationTime;
                    }
                    NumCalculationTime += 1;

                    //ConsoleDebug.WriteLine("CalculationTime: " + caculationTime);

                    /*if (EvadeHelper.GetHighestDetectedSpellID() > EvadeHelper.GetHighestSpellID(posInfo))
                    {
                        return;
                    }*/
                    if (posInfo != null)
                    {
                        LastPosInfo = posInfo.CompareLastMovePos();

                        var travelTime = GameData.HeroInfo.ServerPos2DPing.Distance(LastPosInfo.Position) /
                                         GameData.MyHero.MoveSpeed;

                        LastPosInfo.EndTime = EvadeUtils.TickCount + travelTime * 1000 - 100;
                    }

                    CheckHeroInDanger();
                    DodgeSkillShots(); //walking
                    CheckLastMoveTo();
                    EvadeSpell.UseEvadeSpell(); //using spells
                }
            }
            else
            {
                LastPosInfo = PositionInfo.SetAllDodgeable();
                CheckLastMoveTo();
            }


            //ConsoleDebug.WriteLine("SkillsDodged: " + lastPosInfo.dodgeableSpells.Count + " DangerLevel: " + lastPosInfo.undodgeableSpells.Count);            
        }
    }
}