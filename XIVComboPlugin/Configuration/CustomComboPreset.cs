using System;
using XIVComboPlugin.JobActions;

namespace XIVComboPlugin
{
    //CURRENT HIGHEST FLAG IS 54
    [Flags]
    public enum CustomComboPreset : long
    {
        None = 0,

        // DRAGOON
        [CustomComboInfo("高跳 + 幻象冲", "当处于幻象冲预备状态下，替换高跳为幻象冲", 22, new uint[] { DRG.Jump, DRG.HighJump })]
        DragoonJumpFeature = 1L << 44,

        [CustomComboInfo("苍天龙血 - 坠星冲", "当处于红莲龙血状态下,替换苍天龙血为坠星冲", 22, new uint[] { DRG.BOTD })]
        DragoonBOTDFeature = 1L << 46,

        [CustomComboInfo("樱花怒放连击", "替换樱花怒放为相应的连击", 22, new uint[] { DRG.CTorment })]
        DragoonCoerthanTormentCombo = 1L << 0,

        [CustomComboInfo("山境酷刑连击", "替换山境酷刑为相应的连击 ", 22, new uint[] { DRG.ChaosThrust })]
        DragoonChaosThrustCombo = 1L << 1,

        [CustomComboInfo("直刺连击", "替换直刺为相应的连击", 22, new uint[] { DRG.FullThrust })]
        DragoonFullThrustCombo = 1L << 2,

        // DARK KNIGHT
        [CustomComboInfo("噬魂斩连击", "替换噬魂斩为相应的连击", 32, new uint[] { DRK.Souleater })]
        DarkSouleaterCombo = 1L << 3,

        [CustomComboInfo("刚魂连击", "替换刚魂为相应的连击", 32, new uint[] { DRK.StalwartSoul })]
        DarkStalwartSoulCombo = 1L << 4,

        // PALADIN
        [CustomComboInfo("沥血剑连击", "替换沥血剑为相应的连击", 19, new uint[] { PLD.GoringBlade })]
        PaladinGoringBladeCombo = 1L << 5,

        [CustomComboInfo("王权剑连击", "替换 王权剑/战女神之怒为相应的连击", 19, new uint[] { PLD.RoyalAuthority, PLD.RageOfHalone })]
        PaladinRoyalAuthorityCombo = 1L << 6,

        [CustomComboInfo("日珥斩连击", "替换日珥斩为相应的连击", 19, new uint[] { PLD.Prominence })]
        PaladinProminenceCombo = 1L << 7,

        [CustomComboInfo("安魂祈祷 - 悔罪", "当处于安魂祈祷状态下,替换安魂祈祷为悔罪", 19, new uint[] { PLD.Requiescat })]
        PaladinRequiescatCombo = 1L << 55,

        // WARRIOR
        [CustomComboInfo("暴风斩连击", "替换暴风斩为相应的连击", 21, new uint[] { WAR.StormsPath })]
        WarriorStormsPathCombo = 1L << 8,

        [CustomComboInfo("暴风碎连击", "替换暴风碎为相应的连击", 21, new uint[] { WAR.StormsEye })]
        WarriorStormsEyeCombo = 1L << 9,

        [CustomComboInfo("秘银暴风连击", "替换秘银暴风为相应的连击", 21, new uint[] { WAR.MythrilTempest })]
        WarriorMythrilTempestCombo = 1L << 10,

        // SAMURAI
        [CustomComboInfo("雪风连击", "替换雪风为相应的连击", 34, new uint[] { SAM.Yukikaze })]
        SamuraiYukikazeCombo = 1L << 11,

        [CustomComboInfo("月光连击", "替换月光为相应的连击", 34, new uint[] { SAM.Gekko })]
        SamuraiGekkoCombo = 1L << 12,

        [CustomComboInfo("花车连击", "替换花车为相应的连击", 34, new uint[] { SAM.Kasha })]
        SamuraiKashaCombo = 1L << 13,

        [CustomComboInfo("满月连击", "替换满月为相应的连击", 34, new uint[] { SAM.Mangetsu })]
        SamuraiMangetsuCombo = 1L << 14,

        [CustomComboInfo("樱花连击", "替换樱花为相应的连击", 34, new uint[] { SAM.Oka })]
        SamuraiOkaCombo = 1L << 15,

        [CustomComboInfo("星眼 - 心眼", "当没有触发时，替换星眼为心眼", 34, new uint[] { SAM.Seigan })]
        SamuraiThirdEyeFeature = 1L << 51,


        // NINJA
        [CustomComboInfo("强甲破点突连击", "替换强甲破点突为相应的连击", 30, new uint[] { NIN.ArmorCrush })]
        NinjaArmorCrushCombo = 1L << 17,

        [CustomComboInfo("旋风刃连击", "替换旋风刃为相应的连击", 30, new uint[] { NIN.AeolianEdge })]
        NinjaAeolianEdgeCombo = 1L << 18,

        [CustomComboInfo("八卦无刃杀连击", "替换八卦无刃杀为相应的连击", 30, new uint[] { NIN.HakkeM })]
        NinjaHakkeMujinsatsuCombo = 1L << 19,

        [CustomComboInfo("梦幻三段 - 断绝", "当处于断绝预备状态下，替换梦幻三段为断绝", 30, new uint[] { NIN.DWAD })]
        NinjaAssassinateFeature = 1L << 45,

        // GUNBREAKER
        [CustomComboInfo("迅连斩连击", "替换迅连斩为相应的连击", 37, new uint[] { GNB.SolidBarrel })]
        GunbreakerSolidBarrelCombo = 1L << 20,

        [CustomComboInfo("凶禽爪连击", "替换凶禽爪为相应的连击", 37, new uint[] { GNB.WickedTalon })]
        GunbreakerGnashingFangCombo = 1L << 21,

        [CustomComboInfo("凶禽爪 - 续剑", "除了凶禽爪连击, 替换凶禽爪为续剑", 37, new uint[] { GNB.WickedTalon })]
        GunbreakerGnashingFangCont = 1L << 52,

        [CustomComboInfo("恶魔杀连击", "替换恶魔杀为相应的连击", 37, new uint[] { GNB.DemonSlaughter })]
        GunbreakerDemonSlaughterCombo = 1L << 22,

        // MACHINIST
        [CustomComboInfo("狙击弹连击", "替换狙击弹为相应的连击", 31, new uint[] { MCH.HeatedCleanShot, MCH.CleanShot })]
        MachinistMainCombo = 1L << 23,

        [CustomComboInfo("散射(过热)", "在过热状态下，替换散射为自动弩", 31, new uint[] { MCH.SpreadShot })]
        MachinistSpreadShotFeature = 1L << 24,

        [CustomComboInfo("热冲击(过热)", "在过热状态下，替换超荷为热冲击", 31, new uint[] { MCH.Hypercharge })]
        MachinistOverheatFeature = 1L << 47,

        // BLACK MAGE
        [CustomComboInfo("天语-冰澈/炽炎", "根据相应状态，替换天语为冰澈/炽炎", 25, new uint[] { BLM.Enochian })]
        BlackEnochianFeature = 1L << 25,

        [CustomComboInfo("灵极魂/星灵移位", "当灵极魂可用时，替换星灵移位为灵极魂", 25, new uint[] { BLM.Transpose })]
        BlackManaFeature = 1L << 26,

        [CustomComboInfo("魔纹步/黑魔纹", "当黑魔纹激活时，替换黑魔纹为魔纹步", 25, new uint[] { BLM.LeyLines })]
        BlackLeyLines = 1L << 56,

        // ASTROLOGIAN
        [CustomComboInfo("抽卡/出卡", "没有卡被抽出时，替换出卡为抽卡", 33, new uint[] { AST.Play })]
        AstrologianCardsOnDrawFeature = 1L << 27,

        // SUMMONER
        [CustomComboInfo("亚灵神召唤整合", "整合龙神附体, 龙神召唤, 不死鸟附体为一个按键.\n整合死星核爆, 龙神迸发, 不死鸟迸发为一个按键", 27, new uint[] { SMN.DWT, SMN.Deathflare })]
        SummonerDemiCombo = 1L << 28,

        [CustomComboInfo("灵泉连击", "在处于灵泉状态下，替换灵泉之炎为炼狱之炎", 27, new uint[] { SMN.Ruin1, SMN.Ruin3 })]
        SummonerBoPCombo = 1L << 38,

        [CustomComboInfo("能量吸收-溃烂爆发", "以太超流未被消耗完时，替换溃烂爆发为能量吸收", 27, new uint[] { SMN.Fester })]
        SummonerEDFesterCombo = 1L << 39,

        [CustomComboInfo("能量抽取-痛苦核爆", "以太超流未被消耗完时，替换痛苦核爆为能量抽取", 27, new uint[] { SMN.Painflare })]
        SummonerESPainflareCombo = 1L << 40,

        // SCHOLAR
        [CustomComboInfo("异想的祥光/慰藉", "当炽天使被召唤时，替换异想的祥光为慰藉", 28, new uint[] { SCH.FeyBless })]
        ScholarSeraphConsolationFeature = 1L << 29,

        [CustomComboInfo("能量吸收 - 以太超流", "零档以太超流时,替换能量吸收为以太超流", 28, new uint[] { SCH.EnergyDrain })]
        ScholarEnergyDrainFeature = 1L << 37,

        // DANCER
        [CustomComboInfo("AoE GCD技能", "在没有触发时，将触发的AoE技能替换为相应的非触发AoE技能", 38, new uint[] { DNC.Bloodshower, DNC.RisingWindmill })]
        DancerAoeGcdFeature = 1L << 32,

        [CustomComboInfo("扇舞连击", "当扇舞·急预备时，替换扇舞·序和扇舞·破为扇舞·急", 38, new uint[] { DNC.FanDance1, DNC.FanDance2 })]
        DancerFanDanceCombo = 1L << 33,

        // WHITE MAGE
        [CustomComboInfo("安慰/苦难之心", "当苦难之心可以使用时，替换安慰之心为苦难之心 ", 24, new uint[] { WHM.Solace })]
        WhiteMageSolaceMiseryFeature = 1L << 35,

        [CustomComboInfo("狂喜/苦难之心", "当苦难之心可以使用时，替换狂喜之心为苦难之心", 24, new uint[] { WHM.Misery })]
        WhiteMageRaptureMiseryFeature = 1L << 36,

        // BARD
        [CustomComboInfo("放浪神 - 完美音调", "当处于放浪神的小步舞曲状态下，替换放浪神的小步舞曲为完美音调", 23, new uint[] { BRD.WanderersMinuet })]
        BardWandererPPFeature = 1L << 41,

        [CustomComboInfo("强力射击 into 直线射击", "当触发时，替换强力射击/爆发射击为直线射击/辉煌箭", 23, new uint[] { BRD.HeavyShot, BRD.BurstShot })]
        BardStraightShotUpgradeFeature = 1L << 42,

        // MONK
        [CustomComboInfo("AoE连击", "替换地烈劲为相应的AoE连击,当震脚可用时，替换为地烈劲", 20, new uint[] { MNK.Rockbreaker })]
        MnkAoECombo = 1L << 54,

        // RED MAGE
        [CustomComboInfo("AoE连击", "当连续咏唱/即刻咏唱可用时，替换赤烈风/赤震雷为冲击", 35, new uint[] { RDM.Veraero2, RDM.Verthunder2 })]
        RedMageAoECombo = 1L << 48,

        [CustomComboInfo("魔连攻连击", "替换魔连攻为相应的连击", 35, new uint[] { RDM.Redoublement })]
        RedMageMeleeCombo = 1L << 49,

        [CustomComboInfo("赤火/石 - 震荡", "当没有触发时，替换赤飞石/赤火炎为震荡/焦热", 35, new uint[] { RDM.Verstone, RDM.Verfire })]
        RedMageVerprocCombo = 1L << 53
    }

    public class CustomComboInfoAttribute : Attribute
    {
        internal CustomComboInfoAttribute(string fancyName, string description, byte classJob, uint[] abilities) {
            FancyName = fancyName;
            Description = description;
            ClassJob = classJob;
            Abilities = abilities;
        }

        public string FancyName { get; }
        public string Description { get; }
        public byte ClassJob { get; }
        public uint[] Abilities { get; }
    }
}
