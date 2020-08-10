using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Structs.JobGauge;
using Dalamud.Hooking;
using XIVComboPlugin.JobActions;
using Serilog;
using System.Threading.Tasks;
using System.Threading;
using Dalamud.Plugin;
using System.Dynamic;

namespace XIVComboPlugin
{
    public class IconReplacer
    {
        public delegate ulong OnCheckIsIconReplaceableDelegate(uint actionID);

        public delegate ulong OnGetIconDelegate(byte param1, uint param2);

        private IntPtr activeBuffArray = IntPtr.Zero;

        private readonly IconReplacerAddressResolver Address;
        private readonly Hook<OnCheckIsIconReplaceableDelegate> checkerHook;
        private readonly ClientState clientState;

        private readonly IntPtr comboTimer;

        private readonly XIVComboConfiguration Configuration;

        private readonly HashSet<uint> customIds;
        private readonly HashSet<uint> vanillaIds;
        private HashSet<uint> noUpdateIcons;
        private HashSet<uint> seenNoUpdate;

        private readonly Hook<OnGetIconDelegate> iconHook;
        private readonly IntPtr lastComboMove;
        private readonly IntPtr playerLevel;
        private readonly IntPtr playerJob;
        private byte lastJob = 0;

        private readonly IntPtr BuffVTableAddr;
        private float ping;

        private unsafe delegate int* getArray(long* address);

        private bool shutdown;

        public IconReplacer(SigScanner scanner, ClientState clientState, XIVComboConfiguration configuration)
        {
            ping = 0;
            shutdown = false;
            Configuration = configuration;
            this.clientState = clientState;

            Address = new IconReplacerAddressResolver();
            Address.Setup(scanner);

            comboTimer = scanner.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 80 7E 21 00", 0x178);
            lastComboMove = comboTimer + 0x4;

            playerLevel = scanner.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 88 45 EF", 0x4d) + 0x78;
            playerJob = playerLevel - 0xE;

            BuffVTableAddr = scanner.GetStaticAddressFromSig("48 89 05 ?? ?? ?? ?? 88 05 ?? ?? ?? ?? 88 05 ?? ?? ?? ??", 0);

            customIds = new HashSet<uint>();
            vanillaIds = new HashSet<uint>();
            noUpdateIcons = new HashSet<uint>();
            seenNoUpdate = new HashSet<uint>();

            PopulateDict();

            Log.Verbose("===== H O T B A R S =====");
            Log.Verbose("IsIconReplaceable address {IsIconReplaceable}", Address.IsIconReplaceable);
            Log.Verbose("GetIcon address {GetIcon}", Address.GetIcon);
            Log.Verbose("ComboTimer address {ComboTimer}", comboTimer);
            Log.Verbose("LastComboMove address {LastComboMove}", lastComboMove);
            Log.Verbose("PlayerLevel address {PlayerLevel}", playerLevel);

            iconHook = new Hook<OnGetIconDelegate>(Address.GetIcon, new OnGetIconDelegate(GetIconDetour), this);
            checkerHook = new Hook<OnCheckIsIconReplaceableDelegate>(Address.IsIconReplaceable,
                new OnCheckIsIconReplaceableDelegate(CheckIsIconReplaceableDetour), this);

            Task.Run(() =>
            {
                BuffTask();
            });
        }

        public void Enable()
        {
            iconHook.Enable();
            checkerHook.Enable();
        }

        public void Dispose()
        {
            shutdown = true;
            iconHook.Dispose();
            checkerHook.Dispose();
        }

        public void AddNoUpdate(uint [] ids)
        {
            foreach (uint id in ids)
            {
                if (!noUpdateIcons.Contains(id))
                    noUpdateIcons.Add(id);
            }
        }

        public void RemoveNoUpdate(uint [] ids)
        {
            foreach (uint id in ids)
            {
                if (noUpdateIcons.Contains(id))
                    noUpdateIcons.Remove(id);
                if (seenNoUpdate.Contains(id))
                    seenNoUpdate.Remove(id);
            }
        }
        private async void BuffTask()
        {
            while (!shutdown)
            {
                UpdateBuffAddress();
                await Task.Delay(1000);
            }
        }

        // I hate this function. This is the dumbest function to exist in the game. Just return 1.
        // Determines which abilities are allowed to have their icons updated.
        private ulong CheckIsIconReplaceableDetour(uint actionID)
        {
            if (!noUpdateIcons.Contains(actionID))
            {
                return 1;
            }
            if (!seenNoUpdate.Contains(actionID)) { 
                return 1;
            }
            return 0;
        }

        /// <summary>
        ///     Replace an ability with another ability
        ///     actionID is the original ability to be "used"
        ///     Return either actionID (itself) or a new Action table ID as the
        ///     ability to take its place.
        ///     I tend to make the "combo chain" button be the last move in the combo
        ///     For example, Souleater combo on DRK happens by dragging Souleater
        ///     onto your bar and mashing it.
        /// </summary>
        private ulong GetIconDetour(byte self, uint actionID)
        {
            if (lastJob != Marshal.ReadByte(playerJob))
            {
                lastJob = Marshal.ReadByte(playerJob);
                seenNoUpdate.Clear();
            }
            // TODO: More jobs, level checking for everything.
            if (noUpdateIcons.Contains(actionID) && !seenNoUpdate.Contains(actionID))
            {
                seenNoUpdate.Add(actionID);
                return actionID;
            }
            if (vanillaIds.Contains(actionID)) return iconHook.Original(self, actionID);
            if (!customIds.Contains(actionID)) return actionID;
            if (activeBuffArray == IntPtr.Zero) return iconHook.Original(self, actionID);

            // Don't clutter the spaghetti any worse than it already is.
            var lastMove = Marshal.ReadInt32(lastComboMove);
            var comboTime = Marshal.PtrToStructure<float>(comboTimer);
            var level = Marshal.ReadByte(playerLevel);
            // DRAGOON

            // Change Jump/High Jump into Mirage Dive when Dive Ready
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DragoonJumpFeature))
                if (actionID == DRG.Jump)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1243))
                        return DRG.MirageDive;
                    if (level >= 74)
                        return DRG.HighJump;
                    return DRG.Jump;
                }

            // Change Blood of the Dragon into Stardiver when in Life of the Dragon
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DragoonBOTDFeature))
                if (actionID == DRG.BOTD)
                {
                    if (level >= 80)
                        if (clientState.JobGauges.Get<DRGGauge>().BOTDState == BOTDState.LOTD)
                            return DRG.Stardiver;
                    return DRG.BOTD;
                    
                }

            // Replace Coerthan Torment with Coerthan Torment combo chain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DragoonCoerthanTormentCombo))
                if (actionID == DRG.CTorment)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == DRG.DoomSpike && level >= 62)
                            return DRG.SonicThrust;
                        if (lastMove == DRG.SonicThrust && level >= 72)
                            return DRG.CTorment;
                    }

                    return DRG.DoomSpike;
                }


            // Replace Chaos Thrust with the Chaos Thrust combo chain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DragoonChaosThrustCombo))
                if (actionID == DRG.ChaosThrust)
                {
                    if (comboTime > 0)
                    {
                        if ((lastMove == DRG.TrueThrust || lastMove == DRG.RaidenThrust)
                            && level >= 18) 
                                return DRG.Disembowel;
                        if (lastMove == DRG.Disembowel && level >= 50) 
                            return DRG.ChaosThrust;
                    }
                    UpdateBuffAddress();
                    if (SearchBuffArray(802) && level >= 56)
                        return DRG.FangAndClaw;
                    if (SearchBuffArray(803) && level >= 58)
                        return DRG.WheelingThrust;
                    if (SearchBuffArray(1863) && level >= 76)
                        return DRG.RaidenThrust;

                    return DRG.TrueThrust;
                }


            // Replace Full Thrust with the Full Thrust combo chain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DragoonFullThrustCombo))
                if (actionID == 84)
                {
                    if (comboTime > 0)
                    {
                        if ((lastMove == DRG.TrueThrust || lastMove == DRG.RaidenThrust)
                            && level >= 4)
                            return DRG.VorpalThrust;
                        if (lastMove == DRG.VorpalThrust && level >= 26)
                            return DRG.FullThrust;
                    }
                    UpdateBuffAddress();
                    if (SearchBuffArray(802) && level >= 56)
                        return DRG.FangAndClaw;
                    if (SearchBuffArray(803) && level >= 58)
                        return DRG.WheelingThrust;
                    if (SearchBuffArray(1863) && level >= 76)
                        return DRG.RaidenThrust;

                    return DRG.TrueThrust;
                }

            // DARK KNIGHT

            // Replace Souleater with Souleater combo chain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DarkSouleaterCombo))
                if (actionID == DRK.Souleater)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == DRK.HardSlash && level >= 2)
                            return DRK.SyphonStrike;
                        if (lastMove == DRK.SyphonStrike && level >= 26)
                            return DRK.Souleater;
                    }

                    return DRK.HardSlash;
                }

            // Replace Stalwart Soul with Stalwart Soul combo chain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DarkStalwartSoulCombo))
                if (actionID == DRK.StalwartSoul)
                {
                    if (comboTime > 0)
                        if (lastMove == DRK.Unleash && level >= 72)
                            return DRK.StalwartSoul;

                    return DRK.Unleash;
                }

            // PALADIN

            // Replace Goring Blade with Goring Blade combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.PaladinGoringBladeCombo))
                if (actionID == PLD.GoringBlade)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == PLD.FastBlade && level >= 4)
                            return PLD.RiotBlade;
                        if (lastMove == PLD.RiotBlade && level >= 54)
                            return PLD.GoringBlade;
                    }

                    return PLD.FastBlade;
                }

            // Replace Royal Authority with Royal Authority combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.PaladinRoyalAuthorityCombo))
                if (actionID == PLD.RoyalAuthority || actionID == PLD.RageOfHalone)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == PLD.FastBlade && level >= 4)
                            return PLD.RiotBlade;
                        if (lastMove == PLD.RiotBlade)
                        {
                            if (level >= 60)
                                return PLD.RoyalAuthority;
                            if (level >= 26)
                                return PLD.RageOfHalone;
                        }
                    }

                    return PLD.FastBlade;
                }

            // Replace Prominence with Prominence combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.PaladinProminenceCombo))
                if (actionID == PLD.Prominence)
                {
                    if (comboTime > 0)
                        if (lastMove == PLD.TotalEclipse && level >= 40)
                            return PLD.Prominence;

                    return PLD.TotalEclipse;
                }
            
            // Replace Requiescat with Confiteor when under the effect of Requiescat
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.PaladinRequiescatCombo))
                if (actionID == PLD.Requiescat)
                {
                    if (SearchBuffArray(1368) && level >= 80)
                        return PLD.Confiteor;
                    return PLD.Requiescat;
                }

            // WARRIOR

            // Replace Storm's Path with Storm's Path combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.WarriorStormsPathCombo))
                if (actionID == WAR.StormsPath)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == WAR.HeavySwing && level >= 4)
                            return WAR.Maim;
                        if (lastMove == WAR.Maim && level >= 26)
                            return WAR.StormsPath;
                    }

                    return 31;
                }

            // Replace Storm's Eye with Storm's Eye combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.WarriorStormsEyeCombo))
                if (actionID == WAR.StormsEye)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == WAR.HeavySwing && level >= 4)
                            return WAR.Maim;
                        if (lastMove == WAR.Maim && level >= 50)
                            return WAR.StormsEye;
                    }

                    return WAR.HeavySwing;
                }

            // Replace Mythril Tempest with Mythril Tempest combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.WarriorMythrilTempestCombo))
                if (actionID == WAR.MythrilTempest)
                {
                    if (comboTime > 0)
                        if (lastMove == WAR.Overpower && level >= 40)
                            return WAR.MythrilTempest;
                    return WAR.Overpower;
                }

            // SAMURAI

            // Replace Yukikaze with Yukikaze combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiYukikazeCombo))
                if (actionID == SAM.Yukikaze)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1233))
                        return SAM.Yukikaze;
                    if (comboTime > 0)
                        if (lastMove == SAM.Hakaze && level >= 50)
                            return SAM.Yukikaze;
                    return SAM.Hakaze;
                }

            // Replace Gekko with Gekko combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiGekkoCombo))
                if (actionID == SAM.Gekko)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1233))
                        return SAM.Gekko;
                    if (comboTime > 0)
                    {
                        if (lastMove == SAM.Hakaze && level >= 4)
                            return SAM.Jinpu;
                        if (lastMove == SAM.Jinpu && level >= 30)
                            return SAM.Gekko;
                    }

                    return SAM.Hakaze;
                }

            // Replace Kasha with Kasha combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiKashaCombo))
                if (actionID == SAM.Kasha)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1233))
                        return SAM.Kasha;
                    if (comboTime > 0)
                    {
                        if (lastMove == SAM.Hakaze && level >= 18)
                            return SAM.Shifu;
                        if (lastMove == SAM.Shifu && level >= 40)
                            return SAM.Kasha;
                    }

                    return SAM.Hakaze;
                }

            // Replace Mangetsu with Mangetsu combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiMangetsuCombo))
                if (actionID == SAM.Mangetsu)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1233))
                        return SAM.Mangetsu;
                    if (comboTime > 0)
                        if (lastMove == SAM.Fuga && level >= 35)
                            return SAM.Mangetsu;
                    return SAM.Fuga;
                }

            // Replace Oka with Oka combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiOkaCombo))
                if (actionID == SAM.Oka)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1233))
                        return SAM.Oka;
                    if (comboTime > 0)
                        if (lastMove == SAM.Fuga && level >= 45)
                            return SAM.Oka;
                    return SAM.Fuga;
                }

            // Turn Seigan into Third Eye when not procced
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SamuraiThirdEyeFeature))
                if (actionID == SAM.Seigan) {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1252)) return SAM.Seigan;
                    return SAM.ThirdEye;
                }

            // NINJA

            // Replace Armor Crush with Armor Crush combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.NinjaArmorCrushCombo))
                if (actionID == NIN.ArmorCrush)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == NIN.SpinningEdge && level >= 4)
                            return NIN.GustSlash;
                        if (lastMove == NIN.GustSlash && level >= 54)
                            return NIN.ArmorCrush;
                    }

                    return NIN.SpinningEdge;
                }

            // Replace Aeolian Edge with Aeolian Edge combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.NinjaAeolianEdgeCombo))
                if (actionID == NIN.AeolianEdge)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == NIN.SpinningEdge && level >= 4)
                            return NIN.GustSlash;
                        if (lastMove == NIN.GustSlash && level >= 26)
                            return NIN.AeolianEdge;
                    }

                    return NIN.SpinningEdge;
                }

            // Replace Hakke Mujinsatsu with Hakke Mujinsatsu combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.NinjaHakkeMujinsatsuCombo))
                if (actionID == NIN.HakkeM)
                {
                    if (comboTime > 0)
                        if (lastMove == NIN.DeathBlossom && level >= 52)
                            return NIN.HakkeM;
                    return NIN.DeathBlossom;
                }

            //Replace Dream Within a Dream with Assassinate when Assassinate Ready
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.NinjaAssassinateFeature))
                if (actionID == NIN.DWAD)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1955)) return NIN.Assassinate;
                    return NIN.DWAD;
                }

            // GUNBREAKER

            // Replace Solid Barrel with Solid Barrel combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.GunbreakerSolidBarrelCombo))
                if (actionID == GNB.SolidBarrel)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == GNB.KeenEdge && level >= 4)
                            return GNB.BrutalShell;
                        if (lastMove == GNB.BrutalShell && level >= 26)
                            return GNB.SolidBarrel;
                    }

                    return GNB.KeenEdge;
                }

            // Replace Wicked Talon with Gnashing Fang combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.GunbreakerGnashingFangCombo))
                if (actionID == GNB.WickedTalon)
                {
                    if (Configuration.ComboPresets.HasFlag(CustomComboPreset.GunbreakerGnashingFangCont))
                    {
                        if (level >= GNB.LevelContinuation)
                        {
                            UpdateBuffAddress();
                            if (SearchBuffArray(GNB.BuffReadyToRip))
                                return GNB.JugularRip;
                            if (SearchBuffArray(GNB.BuffReadyToTear))
                                return GNB.AbdomenTear;
                            if (SearchBuffArray(GNB.BuffReadyToGouge))
                                return GNB.EyeGouge;
                        }
                    }
                    var ammoComboState = clientState.JobGauges.Get<GNBGauge>().AmmoComboStepNumber;
                    switch(ammoComboState)
                    {
                        case 1:
                            return GNB.SavageClaw;
                        case 2:
                            return GNB.WickedTalon;
                        default:
                            return GNB.GnashingFang;
                    }
                }

            // Replace Demon Slaughter with Demon Slaughter combo
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.GunbreakerDemonSlaughterCombo))
                if (actionID == GNB.DemonSlaughter)
                {
                    if (comboTime > 0)
                        if (lastMove == GNB.DemonSlice && level >= 40)
                            return GNB.DemonSlaughter;
                    return GNB.DemonSlice;
                }

            // MACHINIST

            // Replace Clean Shot with Heated Clean Shot combo
            // Or with Heat Blast when overheated.
            // For some reason the shots use their unheated IDs as combo moves
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.MachinistMainCombo))
                if (actionID == MCH.CleanShot || actionID == MCH.HeatedCleanShot)
                {
                    if (comboTime > 0)
                    {
                        if (lastMove == MCH.SplitShot)
                        {
                            if (level >= 60)
                                return MCH.HeatedSlugshot;
                            if (level >= 2)
                                return MCH.SlugShot;
                        }

                        if (lastMove == MCH.SlugShot)
                        {
                            if (level >= 64)
                                return MCH.HeatedCleanShot;
                            if (level >= 26)
                                return MCH.CleanShot;
                        }
                    }

                    if (level >= 54)
                        return MCH.HeatedSplitShot;
                    return MCH.SplitShot;
                }

                        
            // Replace Hypercharge with Heat Blast when overheated
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.MachinistOverheatFeature))
                if (actionID == MCH.Hypercharge) {
                    var gauge = clientState.JobGauges.Get<MCHGauge>();
                    if (gauge.IsOverheated() && level >= 35)
                        return MCH.HeatBlast;
                    return MCH.Hypercharge;
                }
                
            // Replace Spread Shot with Auto Crossbow when overheated.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.MachinistSpreadShotFeature))
                if (actionID == MCH.SpreadShot)
                {
                    if (clientState.JobGauges.Get<MCHGauge>().IsOverheated() && level >= 52)
                        return MCH.AutoCrossbow;
                    return MCH.SpreadShot;
                }

            // BLACK MAGE

            // Enochian changes to B4 or F4 depending on stance.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.BlackEnochianFeature))
                if (actionID == BLM.Enochian)
                {
                    var gauge = clientState.JobGauges.Get<BLMGauge>();
                    if (gauge.IsEnoActive())
                    {
                        if (gauge.InUmbralIce() && level >= 58)
                            return BLM.Blizzard4;
                        if (level >= 60)
                            return BLM.Fire4;
                    }

                    return BLM.Enochian;
                }

            // Umbral Soul and Transpose
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.BlackManaFeature))
                if (actionID == BLM.Transpose)
                {
                    var gauge = clientState.JobGauges.Get<BLMGauge>();
                    if (gauge.InUmbralIce() && gauge.IsEnoActive() && level >= 76)
                        return BLM.UmbralSoul;
                    return BLM.Transpose;
                }

            // Ley Lines and BTL
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.BlackLeyLines))
                if (actionID == BLM.LeyLines)
                {
                    if (SearchBuffArray(737) && level >= 62)
                        return BLM.BTL;
                    return BLM.LeyLines;
                }

            // ASTROLOGIAN

            // Make cards on the same button as play
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.AstrologianCardsOnDrawFeature))
                if (actionID == AST.Play)
                {
                    var gauge = clientState.JobGauges.Get<ASTGauge>();
                    switch (gauge.DrawnCard())
                    {
                        case CardType.BALANCE:
                            return AST.Balance;
                        case CardType.BOLE:
                            return AST.Bole;
                        case CardType.ARROW:
                            return AST.Arrow;
                        case CardType.SPEAR:
                            return AST.Spear;
                        case CardType.EWER:
                            return AST.Ewer;
                        case CardType.SPIRE:
                            return AST.Spire;
                        /*
                        case CardType.LORD:
                            return 7444;
                        case CardType.LADY:
                            return 7445;
                        */
                        default:
                            return AST.Draw;
                    }
                }

            // SUMMONER

            // DWT changes. 
            // Now contains DWT, Deathflare, Summon Bahamut, Enkindle Bahamut, FBT, and Enkindle Phoenix.
            // What a monster of a button.
            /*
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SummonerDwtCombo))
                if (actionID == 3581)
                {
                    var gauge = clientState.JobGauges.Get<SMNGauge>();
                    if (gauge.TimerRemaining > 0)
                    {
                        if (gauge.ReturnSummon > 0)
                        {
                            if (gauge.IsPhoenixReady()) return 16516;
                            return 7429;
                        }

                        if (level >= 60) return 3582;
                    }
                    else
                    {
                        if (gauge.IsBahamutReady()) return 7427;
                        if (gauge.IsPhoenixReady())
                        {
                            if (level == 80) return 16549;
                            return 16513;
                        }

                        return 3581;
                    }
                }
                */
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SummonerDemiCombo))
            {

                // Replace Deathflare with demi enkindles
                if (actionID == SMN.Deathflare)
                {
                    var gauge = clientState.JobGauges.Get<SMNGauge>();
                    if (gauge.IsPhoenixReady())
                        return SMN.EnkindlePhoenix;
                    if (gauge.TimerRemaining > 0 && gauge.ReturnSummon != SummonPet.NONE)
                        return SMN.EnkindleBahamut;
                    return SMN.Deathflare;
                }

                //Replace DWT with demi summons
                if (actionID == SMN.DWT)
                {
                    var gauge = clientState.JobGauges.Get<SMNGauge>();
                    if (gauge.IsBahamutReady())
                        return SMN.SummonBahamut;
                    if (gauge.IsPhoenixReady() ||
                        gauge.TimerRemaining > 0 && gauge.ReturnSummon != SummonPet.NONE)
                    {
                        if (level >= 80)
                            return SMN.FBTHigh;
                        return SMN.FBTLow;
                    }
                    return SMN.DWT;
                }
            }

            // Ruin 1 now upgrades to Brand of Purgatory in addition to Ruin 3 and Fountain of Fire
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SummonerBoPCombo))
                if (actionID == SMN.Ruin1 || actionID == SMN.Ruin3)
                {
                    var gauge = clientState.JobGauges.Get<SMNGauge>();
                    if (gauge.TimerRemaining > 0)
                        if (gauge.IsPhoenixReady())
                        {
                            UpdateBuffAddress();
                            if (SearchBuffArray(1867))
                                return SMN.BrandOfPurgatory;
                            return SMN.FountainOfFire;
                        }

                    if (level >= 54)
                        return SMN.Ruin3;
                    return SMN.Ruin1;
                }

            // Change Fester into Energy Drain
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SummonerEDFesterCombo))
                if (actionID == SMN.Fester)
                {
                    if (!clientState.JobGauges.Get<SMNGauge>().HasAetherflowStacks())
                        return SMN.EnergyDrain;
                    return SMN.Fester;
                }

            //Change Painflare into Energy Syphon
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.SummonerESPainflareCombo))
                if (actionID == SMN.Painflare)
                {
                    if (!clientState.JobGauges.Get<SMNGauge>().HasAetherflowStacks())
                        return SMN.EnergySyphon;
                    if (level >= 52)
                        return SMN.Painflare;
                    return SMN.EnergySyphon;
                }

            // SCHOLAR

            // Change Fey Blessing into Consolation when Seraph is out.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.ScholarSeraphConsolationFeature))
                if (actionID == SCH.FeyBless)
                {
                    if (clientState.JobGauges.Get<SCHGauge>().SeraphTimer > 0) return SCH.Consolation;
                    return SCH.FeyBless;
                }

            // Change Energy Drain into Aetherflow when you have no more Aetherflow stacks.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.ScholarEnergyDrainFeature))
                if (actionID == SCH.EnergyDrain)
                {
                    if (clientState.JobGauges.Get<SCHGauge>().NumAetherflowStacks == 0) return SCH.Aetherflow;
                    return SCH.EnergyDrain;
                }

            // DANCER

            // AoE GCDs are split into two buttons, because priority matters
            // differently in different single-target moments. Thanks yoship.
            // Replaces each GCD with its procced version.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DancerAoeGcdFeature))
            {
                if (actionID == DNC.Bloodshower)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1817))
                        return DNC.Bloodshower;
                    return DNC.Bladeshower;
                }

                if (actionID == DNC.RisingWindmill)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1816))
                        return DNC.RisingWindmill;
                    return DNC.Windmill;
                }
            }

            // Fan Dance changes into Fan Dance 3 while flourishing.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.DancerFanDanceCombo))
            {
                if (actionID == DNC.FanDance1)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1820))
                        return DNC.FanDance3;
                    return DNC.FanDance1;
                }

                // Fan Dance 2 changes into Fan Dance 3 while flourishing.
                if (actionID == DNC.FanDance2)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(1820))
                        return DNC.FanDance3;
                    return DNC.FanDance2;
                }
            }

            // WHM

            // Replace Solace with Misery when full blood lily
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.WhiteMageSolaceMiseryFeature))
                if (actionID == WHM.Solace)
                {
                    if (clientState.JobGauges.Get<WHMGauge>().NumBloodLily == 3)
                        return WHM.Misery;
                    return WHM.Solace;
                }

            // Replace Solace with Misery when full blood lily
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.WhiteMageRaptureMiseryFeature))
                if (actionID == WHM.Rapture)
                {
                    if (clientState.JobGauges.Get<WHMGauge>().NumBloodLily == 3)
                        return WHM.Misery;
                    return WHM.Rapture;
                }

            // BARD

            // Replace Wanderer's Minuet with PP when in WM.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.BardWandererPPFeature))
                if (actionID == BRD.WanderersMinuet)
                {
                    if (clientState.JobGauges.Get<BRDGauge>().ActiveSong == CurrentSong.WANDERER)
                        return BRD.PitchPerfect;
                    return BRD.WanderersMinuet;
                }

            // Replace HS/BS with SS/RA when procced.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.BardStraightShotUpgradeFeature))
                if (actionID == BRD.HeavyShot || actionID == BRD.BurstShot)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(122))
                    {
                        if (level >= 70) return BRD.RefulgentArrow;
                        return BRD.StraightShot;
                    }

                    if (level >= 76) return BRD.BurstShot;
                    return BRD.HeavyShot;
                }

            // MONK
            
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.MnkAoECombo))
                if (actionID == MNK.Rockbreaker)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(110)) return MNK.Rockbreaker;
                    if (SearchBuffArray(107)) return MNK.AOTD;
                    if (SearchBuffArray(108)) return MNK.FourPointFury;
                    if (SearchBuffArray(109)) return MNK.Rockbreaker;
                    return MNK.AOTD;
                }

            // RED MAGE
           
            // Replace Veraero/thunder 2 with Impact when Dualcast is active
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.RedMageAoECombo))
            {
                if (actionID == RDM.Veraero2)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(167) || SearchBuffArray(1249))
                    {
                        if (level >= 66) return RDM.Impact;
                        return RDM.Scatter;
                    }
                    return RDM.Veraero2;
                }

                if (actionID == RDM.Verthunder2)
                {
                    UpdateBuffAddress();
                    if (SearchBuffArray(167) || SearchBuffArray(1249))
                    {
                        if (level >= 66) return RDM.Impact;
                        return RDM.Scatter;
                    }
                    return RDM.Verthunder2;
                }
            }


            // Replace Redoublement with Redoublement combo, Enchanted if possible.
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.RedMageMeleeCombo))
                if (actionID == RDM.Redoublement)
                {
                    var gauge = clientState.JobGauges.Get<RDMGauge>();
                    if ((lastMove == RDM.Riposte || lastMove == RDM.ERiposte) && level >= 35)
                    {
                        if (gauge.BlackGauge >= 25 && gauge.WhiteGauge >= 25)
                            return RDM.EZwerchhau;
                        return RDM.Zwerchhau;
                    }

                    if (lastMove == RDM.Zwerchhau && level >= 50)
                    {
                        if (gauge.BlackGauge >= 25 && gauge.WhiteGauge >= 25)
                            return RDM.ERedoublement;
                        return RDM.Redoublement;
                    }

                    if (gauge.BlackGauge >= 30 && gauge.WhiteGauge >= 30)
                        return RDM.ERiposte;
                    return RDM.Riposte;
                }
            if (Configuration.ComboPresets.HasFlag(CustomComboPreset.RedMageVerprocCombo))
            {
                if (actionID == RDM.Verstone)
                {
                    if (level >= 80 && (lastMove == RDM.Verflare || lastMove == RDM.Verholy)) return RDM.Scorch;
                    UpdateBuffAddress();
                    if (SearchBuffArray(1235)) return RDM.Verstone;
                    if (level < 62) return RDM.Jolt;
                    return RDM.Jolt2;
                }
                if (actionID == RDM.Verfire)
                {
                    if (level >= 80 && (lastMove == RDM.Verflare || lastMove == RDM.Verholy)) return RDM.Scorch;
                    UpdateBuffAddress();
                    if (SearchBuffArray(1234)) return RDM.Verfire;
                    if (level < 62) return RDM.Jolt;
                    return RDM.Jolt2;
                }
            }

            return iconHook.Original(self, actionID);
        }
        /*
        public void UpdatePing(ulong value)
        {
            ping = (float)(value)/1000;
        }
        */
        private bool SearchBuffArray(short needle)
        {
            if (activeBuffArray == IntPtr.Zero) return false;
            for (var i = 0; i < 60; i++)
                if (Marshal.ReadInt16(activeBuffArray + (12 * i)) == needle)
                    return true;
            return false;
        }

        private void UpdateBuffAddress()
        {
            try
            {
                activeBuffArray = FindBuffAddress();
            }
            catch (Exception)
            {
                //Before you're loaded in
                activeBuffArray = IntPtr.Zero;
            }
        }

        private unsafe IntPtr FindBuffAddress()
        {
            var num = Marshal.ReadIntPtr(BuffVTableAddr);
            var step2 = (IntPtr) (Marshal.ReadInt64(num) + 0x270);
            var step3 = Marshal.ReadIntPtr(step2);
            var callback = Marshal.GetDelegateForFunctionPointer<getArray>(step3);
            return (IntPtr) callback((long*) num) + 8;
        }

        private void PopulateDict() {
            customIds.Add(16477u);
            customIds.Add(88u);
            customIds.Add(84u);
            customIds.Add(3632u);
            customIds.Add(16468u);
            customIds.Add(3538u);
            customIds.Add(3539u);
            customIds.Add(16457u);
            customIds.Add(42u);
            customIds.Add(45u);
            customIds.Add(16462u);
            customIds.Add(7480u);
            customIds.Add(7481u);
            customIds.Add(7482u);
            customIds.Add(7484u);
            customIds.Add(7485u);
            customIds.Add(3563u);
            customIds.Add(2255u);
            customIds.Add(16488u);
            customIds.Add(16145u);
            customIds.Add(16150u);
            customIds.Add(16149u);
            customIds.Add(7413u);
            customIds.Add(2870u);
            customIds.Add(3575u);
            customIds.Add(149u);
            customIds.Add(17055u);
            customIds.Add(3582u);
            customIds.Add(3581u);
            customIds.Add(163u);
            customIds.Add(181u);
            customIds.Add(3578u);
            customIds.Add(16543u);
            customIds.Add(167u);
            customIds.Add(15994u);
            customIds.Add(15993u);
            customIds.Add(16007u);
            customIds.Add(16008u);
            customIds.Add(16531u);
            customIds.Add(16534u);
            customIds.Add(3559u);
            customIds.Add(97u);
            customIds.Add(16525u);
            customIds.Add(16524u);
            customIds.Add(7516u);
            customIds.Add(3566u);
            customIds.Add(92u);
            customIds.Add(3553u);
            customIds.Add(2873u);
            customIds.Add(3579u);
            customIds.Add(17209u);
            customIds.Add(7501u);
            customIds.Add(21u);
            customIds.Add(15996u);
            customIds.Add(15995u);
            customIds.Add(7511u);
            customIds.Add(7510u);
            customIds.Add(70u);
            customIds.Add(3573u);
            customIds.Add(7383u);
            vanillaIds.Add(15989u);
            vanillaIds.Add(15990u);
            vanillaIds.Add(15991u);
            vanillaIds.Add(15992u);
            vanillaIds.Add(15997u);
            vanillaIds.Add(15998u);
            vanillaIds.Add(16006u);
            vanillaIds.Add(16144u);
            vanillaIds.Add(16165u);
            vanillaIds.Add(16155u);
            vanillaIds.Add(16156u);
            vanillaIds.Add(16157u);
            vanillaIds.Add(16158u);
            vanillaIds.Add(17695u);
            vanillaIds.Add(17151u);
            vanillaIds.Add(17152u);
            vanillaIds.Add(18900u);
            vanillaIds.Add(18901u);
            vanillaIds.Add(18921u);
            vanillaIds.Add(18922u);
            vanillaIds.Add(18932u);
            vanillaIds.Add(18935u);
            vanillaIds.Add(18937u);
            vanillaIds.Add(18950u);
            vanillaIds.Add(18993u);
            vanillaIds.Add(18994u);
            vanillaIds.Add(18997u);
            vanillaIds.Add(18322u);
            vanillaIds.Add(17711u);
            vanillaIds.Add(17727u);
            vanillaIds.Add(17740u);
            vanillaIds.Add(17756u);
            vanillaIds.Add(17757u);
            vanillaIds.Add(17761u);
            vanillaIds.Add(17765u);
            vanillaIds.Add(17766u);
            vanillaIds.Add(17824u);
            vanillaIds.Add(17864u);
            vanillaIds.Add(17865u);
            vanillaIds.Add(17869u);
            vanillaIds.Add(16791u);
            vanillaIds.Add(16793u);
            vanillaIds.Add(16795u);
            vanillaIds.Add(16797u);
            vanillaIds.Add(16799u);
            vanillaIds.Add(16792u);
            vanillaIds.Add(16794u);
            vanillaIds.Add(16796u);
            vanillaIds.Add(16798u);
            vanillaIds.Add(16800u);
            vanillaIds.Add(16801u);
            vanillaIds.Add(16802u);
            vanillaIds.Add(16803u);
            vanillaIds.Add(16766u);
            vanillaIds.Add(16463u);
            vanillaIds.Add(16465u);
            vanillaIds.Add(16466u);
            vanillaIds.Add(16469u);
            vanillaIds.Add(16467u);
            vanillaIds.Add(16470u);
            vanillaIds.Add(16478u);
            vanillaIds.Add(16479u);
            vanillaIds.Add(16483u);
            vanillaIds.Add(16495u);
            vanillaIds.Add(16500u);
            vanillaIds.Add(16501u);
            vanillaIds.Add(16502u);
            vanillaIds.Add(16509u);
            vanillaIds.Add(16511u);
            vanillaIds.Add(16515u);
            vanillaIds.Add(16512u);
            vanillaIds.Add(16513u);
            vanillaIds.Add(16514u);
            vanillaIds.Add(16516u);
            vanillaIds.Add(16526u);
            vanillaIds.Add(16529u);
            vanillaIds.Add(16530u);
            vanillaIds.Add(16532u);
            vanillaIds.Add(16533u);
            vanillaIds.Add(16540u);
            vanillaIds.Add(16541u);
            vanillaIds.Add(16554u);
            vanillaIds.Add(16555u);
            vanillaIds.Add(16557u);
            vanillaIds.Add(16558u);
            vanillaIds.Add(10027u);
            vanillaIds.Add(8746u);
            vanillaIds.Add(8749u);
            vanillaIds.Add(8750u);
            vanillaIds.Add(8763u);
            vanillaIds.Add(8805u);
            vanillaIds.Add(8807u);
            vanillaIds.Add(8808u);
            vanillaIds.Add(8809u);
            vanillaIds.Add(8820u);
            vanillaIds.Add(8848u);
            vanillaIds.Add(8849u);
            vanillaIds.Add(8850u);
            vanillaIds.Add(8860u);
            vanillaIds.Add(8862u);
            vanillaIds.Add(8872u);
            vanillaIds.Add(8883u);
            vanillaIds.Add(8885u);
            vanillaIds.Add(8887u);
            vanillaIds.Add(8913u);
            vanillaIds.Add(17781u);
            vanillaIds.Add(9013u);
            vanillaIds.Add(7867u);
            vanillaIds.Add(7389u);
            vanillaIds.Add(7406u);
            vanillaIds.Add(7407u);
            vanillaIds.Add(7409u);
            vanillaIds.Add(7411u);
            vanillaIds.Add(7412u);
            vanillaIds.Add(7415u);
            vanillaIds.Add(7420u);
            vanillaIds.Add(7447u);
            vanillaIds.Add(7424u);
            vanillaIds.Add(7425u);
            vanillaIds.Add(7429u);
            vanillaIds.Add(7431u);
            vanillaIds.Add(7435u);
            vanillaIds.Add(7437u);
            vanillaIds.Add(7439u);
            vanillaIds.Add(7442u);
            vanillaIds.Add(7443u);
            vanillaIds.Add(7503u);
            vanillaIds.Add(7524u);
            vanillaIds.Add(7504u);
            vanillaIds.Add(7512u);
            vanillaIds.Add(7513u);
            vanillaIds.Add(7505u);
            vanillaIds.Add(7507u);
            vanillaIds.Add(7526u);
            vanillaIds.Add(7509u);
            vanillaIds.Add(3546u);
            vanillaIds.Add(3549u);
            vanillaIds.Add(3550u);
            vanillaIds.Add(3555u);
            vanillaIds.Add(3568u);
            vanillaIds.Add(3584u);
            vanillaIds.Add(3595u);
            vanillaIds.Add(3596u);
            vanillaIds.Add(3598u);
            vanillaIds.Add(3599u);
            vanillaIds.Add(3601u);
            vanillaIds.Add(3608u);
            vanillaIds.Add(4077u);
            vanillaIds.Add(4087u);
            vanillaIds.Add(4091u);
            vanillaIds.Add(4073u);
            vanillaIds.Add(2864u);
            vanillaIds.Add(302u);
            vanillaIds.Add(2259u);
            vanillaIds.Add(2260u);
            vanillaIds.Add(2261u);
            vanillaIds.Add(2263u);
            vanillaIds.Add(2866u);
            vanillaIds.Add(2868u);
            vanillaIds.Add(2872u);
            vanillaIds.Add(2878u);
            vanillaIds.Add(301u);
            vanillaIds.Add(38u);
            vanillaIds.Add(49u);
            vanillaIds.Add(51u);
            vanillaIds.Add(75u);
            vanillaIds.Add(98u);
            vanillaIds.Add(100u);
            vanillaIds.Add(113u);
            vanillaIds.Add(119u);
            vanillaIds.Add(127u);
            vanillaIds.Add(121u);
            vanillaIds.Add(132u);
            vanillaIds.Add(144u);
            vanillaIds.Add(153u);
            vanillaIds.Add(164u);
            vanillaIds.Add(178u);
            vanillaIds.Add(168u);
            vanillaIds.Add(172u);
            vanillaIds.Add(184u);
            vanillaIds.Add(226u);
            vanillaIds.Add(271u);
            vanillaIds.Add(243u);
            vanillaIds.Add(270u);
            vanillaIds.Add(272u);
            vanillaIds.Add(273u);
        }
    }
}
