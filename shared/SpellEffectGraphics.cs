// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

public static class SpellEffectGraphics
{
    // Reserved Sanctuary statics, vacant in the installed stock art and aliases.
    public const ushort BashSwing = 62000;
    public const ushort BashImpact = 62004;
    public const ushort BashSkid = 62008;
    public const ushort StunSwing = 62012;
    public const ushort StunImpact = 62016;
    public const ushort StunConcussion = 62020;
    public const ushort ConcentrateSwing = 62024;
    public const ushort ConcentrateImpact = 62028;
    public const ushort ConcentrateFocus = 62032;
    public const ushort BerserkSwing = 62036;
    public const ushort BerserkImpact = 62040;
    public const ushort BerserkState = 62044;
    public const ushort BerserkFade = 62048;
    public const int BarbarianStrikeFrames = 4;

    public static bool IsBarbarianStrikeAnimation(ushort graphic) => graphic is >= BashSwing and <= BerserkFade
        && (graphic - BashSwing) % BarbarianStrikeFrames == 0;
    public static bool IsBarbarianStrikeStatus(ushort graphic) => graphic is ConcentrateFocus or BerserkState;
    public static bool IsBarbarianStrikeAttached(ushort graphic) => graphic is BashSwing or StunSwing or StunConcussion
        or ConcentrateSwing or ConcentrateFocus or BerserkSwing or BerserkState or BerserkFade;
    public static bool IsBarbarianStrikeImpact(ushort graphic) => graphic is BashImpact or StunImpact or ConcentrateImpact or BerserkImpact;

    public const ushort LeapTakeoff = 62052;
    public const ushort LeapLanding = 62056;
    public const ushort LeapSettle = 62060;
    public const ushort LeapAirTrail = 62064;
    public const ushort LeapAttackLanding = 62096;
    public const ushort LeapAttackAreaContact = 62100;
    public const ushort LeapAttackSwing = 62104;
    public const ushort LeapAttackImpact = 62108;
    public const ushort WhirlwindSpin = 62112;
    public const ushort WhirlwindWake = 62116;
    public const ushort WhirlwindImpact = 62120;
    public const ushort WhirlwindFade = 62124;
    public const int BarbarianMotionFrames = 4;

    public static bool IsBarbarianMotionAnimation(ushort graphic) =>
        graphic is (>= LeapTakeoff and <= LeapSettle) or (>= LeapAttackLanding and <= WhirlwindFade)
        && (graphic - LeapTakeoff) % BarbarianMotionFrames == 0;

    public const ushort DoubleSwingLead = 62128;
    public const ushort DoubleSwingFollow = 62132;
    public const ushort DoubleSwingLeadImpact = 62136;
    public const ushort DoubleSwingFollowImpact = 62140;
    public const ushort FrenzyLead = 62144;
    public const ushort FrenzyFollow = 62148;
    public const ushort FrenzySpeed = 62152;
    public const ushort FrenzyFade = 62156;
    public const ushort FrenzyImpact = 62160;
    public const ushort DoubleThrowImpact = 62164;
    public const ushort DoubleThrowKnife = 62168;
    public const ushort DoubleThrowAxe = 62176;
    public const ushort DoubleThrowJavelin = 62184;
    public const ushort DoubleThrowGlaive = 62216;
    public const ushort DoubleThrowPilum = 62248;
    public const int BarbarianPairedFrames = 4;
    public const int BarbarianThrowSpinFrames = 8;

    public static bool IsBarbarianPairedAnimation(ushort graphic) =>
        graphic is >= DoubleSwingLead and <= DoubleThrowImpact
        && (graphic - DoubleSwingLead) % BarbarianPairedFrames == 0;
    public static bool IsBarbarianPairedAttached(ushort graphic) => graphic is
        DoubleSwingLead or DoubleSwingFollow or FrenzyLead or FrenzyFollow or FrenzySpeed or FrenzyFade;
    public static bool IsBarbarianThrowFlight(ushort graphic) => graphic is
        >= DoubleThrowKnife and < DoubleThrowPilum + 8 * BarbarianPairedFrames;

    public const ushort MartialOrbs = 62280;
    public const int MartialOrbFrames = 3;
    public const int MartialChargeCount = 3;
    public const int MartialFamilies = 6;
    public const ushort MartialSwing = 62336;
    public const ushort MartialContact = 62360;
    public const ushort MartialFireRing = 62384;
    public const ushort MartialThunderRing = 62388;
    public const ushort MartialIceRing = 62392;
    public const ushort MartialIceCube = 62396;
    public const ushort MartialFistsField = 62400;
    public const ushort MartialPhoenixField = 62404;
    public const int MartialFrames = 4;

    public static bool IsMartialOrb(ushort graphic) => graphic >= MartialOrbs
        && graphic < MartialOrbs + MartialFamilies * MartialChargeCount * MartialOrbFrames
        && (graphic - MartialOrbs) % MartialOrbFrames == 0;
    public static bool IsMartialAnimation(ushort graphic) => graphic is >= MartialSwing and <= MartialIceRing
        && (graphic - MartialSwing) % MartialFrames == 0;
    public static bool IsMartialSwing(ushort graphic) => graphic is >= MartialSwing and < MartialContact;

    public const ushort DragonTalonSweep = 62408;
    public const ushort DragonClawLead = 62440;
    public const ushort DragonClawFollow = 62472;
    public const ushort DragonTailSweep = 62504;
    public const ushort DragonFlightSweep = 62536;
    public const ushort DragonTalonImpact = 62568;
    public const ushort DragonClawLeadImpact = 62572;
    public const ushort DragonClawFollowImpact = 62576;
    public const ushort DragonTailImpact = 62580;
    public const ushort DragonFlightImpact = 62584;
    public const ushort DragonDeflect = 62588;
    public const ushort DragonTalonSkid = 62592;
    public const ushort DragonTailBurst = 62596;
    public const ushort DragonTailEmbers = 62600;
    public const ushort DragonFlightDepart = 62604;
    public const ushort DragonFlightArrive = 62608;
    public const int DragonFrames = 4;

    public static bool IsDragonAnimation(ushort graphic) => graphic is >= DragonTalonSweep and <= DragonFlightArrive
        && (graphic - DragonTalonSweep) % DragonFrames == 0;
    public static bool IsDragonSweep(ushort graphic) => graphic is >= DragonTalonSweep and < DragonTalonImpact;

    public const int AssassinBladeFrames = 4;
    public const ushort BladeSentinelRise = 62612;
    public const ushort BladeSentinelBody = 62616;
    public const ushort BladeSentinelHit = 62620;
    public const ushort BladeSentinelFade = 62624;
    public const ushort BladeFuryRelease = 62628;
    public const ushort BladeFuryFlight = 62632;
    public const ushort BladeFuryHit = 62664;
    public const ushort BladeFuryFade = 62668;
    public const ushort BladeShieldRaise = 62672;
    public const ushort BladeShieldRing = 62676;
    public const ushort BladeShieldHit = 62680;
    public const ushort BladeShieldFade = 62684;

    public static bool IsBladeSentinelBody(ushort graphic) =>
        graphic is >= BladeSentinelRise and < BladeSentinelHit;
    public static bool IsAssassinBladeAnimation(ushort graphic) => graphic is
        BladeSentinelHit or BladeSentinelFade or BladeFuryRelease or BladeFuryHit or BladeFuryFade
        or BladeShieldRaise or BladeShieldRing or BladeShieldHit or BladeShieldFade;
    public static bool IsAssassinBladeAttached(ushort graphic) => graphic is
        BladeFuryRelease or BladeShieldRaise or BladeShieldRing or BladeShieldFade;

    public const int AssassinShadowFrames = 4;
    public const ushort BurstSpeedCast = 62688;
    public const ushort BurstSpeedWard = 62692;
    public const ushort BurstSpeedEnd = 62696;
    public const ushort FadeCast = 62700;
    public const ushort FadeWard = 62704;
    public const ushort FadeEnd = 62708;
    public const ushort VenomCast = 62712;
    public const ushort VenomCoat = 62716;
    public const ushort VenomEnd = 62720;
    public const ushort CloakWave = 62724;
    public const ushort CloakMantle = 62728;
    public const ushort CloakBlind = 62732;
    public const ushort CloakExposure = 62736;
    public const ushort CloakEnd = 62740;
    public const ushort PsychicHammerCast = 62744;
    public const ushort PsychicHammerHit = 62748;
    public const ushort MindBlastWave = 62752;
    public const ushort MindBlastHit = 62756;
    public const ushort MindBlastCast = 62760;
    public static bool IsAssassinShadowAnimation(ushort graphic) => graphic is >= BurstSpeedCast and <= MindBlastCast
        && (graphic - BurstSpeedCast) % AssassinShadowFrames == 0;
    public static bool IsAssassinShadowLoop(ushort graphic) => graphic is
        BurstSpeedWard or FadeWard or VenomCoat or CloakMantle or CloakBlind or CloakExposure;
    public static bool IsAssassinShadowTorso(ushort graphic) =>
        graphic is >= FadeCast and <= VenomEnd or CloakMantle or CloakEnd or PsychicHammerCast or MindBlastCast;

    public const ushort ShadowWarriorCast = 61600;
    public const ushort ShadowWarriorArrival = 61604;
    public const ushort ShadowWarriorVeil = 61608;
    public const ushort ShadowWarriorDeparture = 61612;
    public const ushort ShadowMasterCast = 61616;
    public const ushort ShadowMasterArrival = 61620;
    public const ushort ShadowMasterVeil = 61624;
    public const ushort ShadowMasterDeparture = 61628;
    public static bool IsShadowSummonAnimation(ushort graphic) => graphic is >= ShadowWarriorCast and <= ShadowMasterDeparture
        && (graphic - ShadowWarriorCast) % AssassinShadowFrames == 0;
    public static bool IsShadowSummonCast(ushort graphic) => graphic is ShadowWarriorCast or ShadowMasterCast;
    public static bool IsShadowSummonVeil(ushort graphic) => graphic is ShadowWarriorVeil or ShadowMasterVeil;

    public const int WarlockDemonFrames = 4;
    public const ushort GoatmanCast = 61400;
    public const ushort GoatmanArrival = 61404;
    public const ushort GoatmanContact = 61408;
    public const ushort GoatmanDeparture = 61412;
    public const ushort TaintedCast = 61416;
    public const ushort TaintedArrival = 61420;
    public const ushort TaintedFireWard = 61424;
    public const ushort TaintedDeparture = 61428;
    public const ushort DefilerCast = 61432;
    public const ushort DefilerArrival = 61436;
    public const ushort DefilerLinkMark = 61440;
    public const ushort DefilerDeparture = 61444;
    public const ushort DefilerLinkContact = 61448;
    public static bool IsWarlockDemonAnimation(ushort graphic) => graphic is >= GoatmanCast and <= DefilerLinkContact
        && (graphic - GoatmanCast) % WarlockDemonFrames == 0;
    public static bool IsWarlockDemonCast(ushort graphic) => graphic is GoatmanCast or TaintedCast or DefilerCast;
    public static bool IsWarlockDemonLoop(ushort graphic) => graphic is TaintedFireWard or DefilerLinkMark;

    public const int WarlockRitualFrames = 4;
    public const ushort ConsumeCast = 61000;
    public const ushort ConsumeSource = 61004;
    public const ushort ConsumeTravel = 61008;
    public const ushort ConsumeWard = 61040;
    public const ushort ConsumeFade = 61044;
    public const ushort EngorgeCast = 61100;
    public const ushort EngorgeSource = 61104;
    public const ushort EngorgeTravel = 61108;
    public const ushort EngorgeHeal = 61140;
    public const ushort EngorgeWard = 61144;
    public const ushort DeathMarkCast = 61200;
    public const ushort DeathMarkDeparture = 61204;
    public const ushort DeathMarkArrival = 61208;
    public const ushort DeathMarkGlyph = 61212;
    public const ushort BloodBoilCast = 61220;
    public const ushort BloodBoilRing = 61224;
    public const ushort BloodBoilHit = 61228;
    public const ushort BloodBoilSource = 61232;
    public const ushort BindDemonChannel = 61300;
    public const ushort BindDemonTravel = 61304;
    public const ushort BindDemonSeal = 61336;
    public const ushort BindDemonSuccess = 61340;
    public static bool IsWarlockRitualAnimation(ushort graphic) => graphic is ConsumeCast or ConsumeSource or ConsumeWard
        or ConsumeFade or EngorgeCast or EngorgeSource or EngorgeHeal or EngorgeWard or DeathMarkCast or DeathMarkDeparture
        or DeathMarkArrival or DeathMarkGlyph or BloodBoilCast or BloodBoilRing or BloodBoilHit or BloodBoilSource
        or BindDemonChannel or BindDemonSeal or BindDemonSuccess;
    public static bool IsWarlockRitualLoop(ushort graphic) => graphic is ConsumeWard or EngorgeWard or DeathMarkGlyph
        or BindDemonChannel or BindDemonSeal;
    public static bool IsWarlockRitualTorso(ushort graphic) => graphic is ConsumeCast or EngorgeCast or EngorgeHeal
        or DeathMarkCast or DeathMarkGlyph or BloodBoilCast or BindDemonChannel;

    public const int WarlockFireFrames = 4;
    public const ushort RingOfFireCast = 61500;
    public const ushort RingOfFireTravel = 61504;
    public const ushort RingOfFireContact = 61508;
    public const ushort RingOfFireBurst = 61512;
    public const ushort FlameWaveCast = 61516;
    public const ushort FlameWaveFront = 61520;
    public const ushort FlameWaveField = 61524;
    public const ushort FlameWaveFade = 61528;
    public const ushort ApocalypseWarning = 61532;
    public const ushort ApocalypseEruption = 61536;
    public const ushort ApocalypseContact = 61540;
    public static bool IsWarlockFireAnimation(ushort graphic) => graphic is RingOfFireCast or RingOfFireContact
        or RingOfFireBurst or FlameWaveCast or FlameWaveField or FlameWaveFade or ApocalypseWarning
        or ApocalypseEruption or ApocalypseContact;
    public static bool IsWarlockFireLoop(ushort graphic) => graphic is FlameWaveField or ApocalypseWarning;

    public const int SigilFrames = 4;
    public const ushort LethargySigilCast = 61700;
    public const ushort LethargySigilField = 61704;
    public const ushort LethargySigilMark = 61708;
    public const ushort LethargySigilFade = 61712;
    public const ushort RancorSigilCast = 61716;
    public const ushort RancorSigilField = 61720;
    public const ushort RancorSigilMark = 61724;
    public const ushort RancorSigilConfusion = 61728;
    public const ushort RancorSigilFade = 61732;
    public const ushort DeathSigilCast = 61736;
    public const ushort DeathSigilField = 61740;
    public const ushort DeathSigilMark = 61744;
    public const ushort DeathSigilBurst = 61748;
    public const ushort DeathSigilFade = 61752;
    public const ushort DeathSigilBurstRing = 61756;
    public const ushort DeathSigilContact = 61760;
    public static bool IsSigilAnimation(ushort graphic) => graphic is >= LethargySigilCast and <= DeathSigilContact
        && (graphic - LethargySigilCast) % SigilFrames == 0;
    public static bool IsSigilField(ushort graphic) => graphic is LethargySigilField or RancorSigilField or DeathSigilField;
    public static bool IsSigilMark(ushort graphic) => graphic is LethargySigilMark or RancorSigilMark or DeathSigilMark;

    public const int MiasmaFrames = 4;
    public const ushort MiasmaBoltCast = 60800;
    public const ushort MiasmaBoltFlight = 60804;
    public const ushort MiasmaBoltBurst = 60836;
    public const ushort MiasmaBoltCloud = 60840;
    public const ushort MiasmaBoltFade = 60852;
    public const ushort MiasmaContact = 60856;
    public const ushort MiasmaChainCast = 60900;
    public const ushort MiasmaChainLink = 60904;
    public const ushort MiasmaChainLatch = 60936;
    public const ushort MiasmaChainCloud = 60940;
    public const ushort MiasmaChainMistFade = 60952;
    public const ushort MiasmaChainFade = 60956;
    public static bool IsMiasmaCloud(ushort graphic) => graphic is >= MiasmaBoltCloud and < MiasmaBoltFade
        && (graphic - MiasmaBoltCloud) % MiasmaFrames == 0
        || graphic is >= MiasmaChainCloud and < MiasmaChainMistFade
        && (graphic - MiasmaChainCloud) % MiasmaFrames == 0;
    public static bool IsMiasmaAnimation(ushort graphic) => IsMiasmaCloud(graphic)
        || graphic is MiasmaBoltCast or MiasmaBoltBurst or MiasmaBoltFade or MiasmaContact
            or MiasmaChainCast or MiasmaChainLatch or MiasmaChainMistFade or MiasmaChainFade;

    public const int WeaponHexFrames = 4;
    public const ushort HexBaneCast = 60600;
    public const ushort HexBaneCoat = 60604;
    public const ushort HexBaneMark = 60608;
    public const ushort HexBaneContact = 60612;
    public const ushort HexBaneFade = 60616;
    public const ushort HexPurgeCast = 60620;
    public const ushort HexPurgeCoat = 60624;
    public const ushort HexPurgeMark = 60628;
    public const ushort HexPurgeBurst = 60632;
    public const ushort HexPurgeRadius = 60636;
    public const ushort HexPurgeContact = 60640;
    public const ushort HexPurgeFade = 60644;
    public const ushort HexSiphonCast = 60648;
    public const ushort HexSiphonCoat = 60652;
    public const ushort HexSiphonMark = 60656;
    public const ushort HexSiphonLife = 60660;
    public const ushort HexSiphonMana = 60664;
    public const ushort HexSiphonFade = 60668;
    public static bool IsWeaponHexCoat(ushort graphic) => graphic is HexBaneCoat or HexPurgeCoat or HexSiphonCoat;
    public static bool IsWeaponHexMark(ushort graphic) => graphic is HexBaneMark or HexPurgeMark or HexSiphonMark;
    public static bool IsWeaponHexAnimation(ushort graphic) => graphic is >= HexBaneCast and <= HexSiphonFade
        && (graphic - HexBaneCast) % WeaponHexFrames == 0 && !IsWeaponHexCoat(graphic);

    public const int WarlockPsychicFrames = 4;
    public const ushort PsychicWardCast = 60700;
    public const ushort PsychicWardShell = 60704;
    public const ushort PsychicWardCatch = 60708;
    public const ushort PsychicWardBreak = 60712;
    public const ushort EldritchBlastCast = 60724;
    public const ushort EldritchBlastAura = 60728;
    public const ushort EldritchBlastRing = 60732;
    public const ushort EldritchBlastRefresh = 60736;
    public const ushort EldritchBlastContact = 60740;
    public const ushort EldritchBlastFade = 60744;
    public static bool IsWarlockPsychicAnimation(ushort graphic) => graphic is PsychicWardCast or PsychicWardShell
        or PsychicWardCatch or PsychicWardBreak or EldritchBlastCast or EldritchBlastAura or EldritchBlastRing
        or EldritchBlastRefresh or EldritchBlastContact or EldritchBlastFade;
    public static bool IsWarlockPsychicLoop(ushort graphic) => graphic is PsychicWardShell or EldritchBlastAura;

    public const int WarlockBladeFrames = 4;
    public const ushort CleaveSweep = 60500;
    public const ushort CleaveContact = 60532;
    public const ushort BladeWarpCast = 60300;
    public const ushort BladeWarpFlight = 60304;
    public const ushort BladeWarpImpact = 60336;
    public const ushort BladeWarpRadius = 60340;
    public const ushort BladeWarpDeparture = 60344;
    public const ushort BladeWarpArrival = 60348;
    public const ushort BladeWarpContact = 60352;
    public const ushort EchoingStrikeCast = 60000;
    public const ushort EchoingStrikeOut = 60004;
    public const ushort EchoingStrikeReturn = 60036;
    public const ushort EchoingStrikeTurn = 60068;
    public const ushort EchoingStrikeHit = 60072;
    public const ushort EchoingStrikeFade = 60076;
    public const ushort EchoingStrikeDeflect = 60080;
    public const ushort MirroredBladesCast = 60100;
    public const ushort MirroredBladesPrimary = 60104;
    public const ushort MirroredBladesCopy = 60136;
    public const ushort MirroredBladesArrow = 60168;
    public const ushort MirroredBladesBolt = 60200;
    public const ushort MirroredBladesThrow = 60232;
    public const ushort MirroredBladesHit = 60264;
    public const ushort MirroredBladesFade = 60268;
    public static bool IsCleaveSweep(ushort graphic) => graphic is >= CleaveSweep and < CleaveContact
        && (graphic - CleaveSweep) % WarlockBladeFrames == 0;
    private static bool IsMirroredMelee(ushort graphic) => graphic is >= MirroredBladesPrimary and < MirroredBladesArrow
        && (graphic - MirroredBladesPrimary) % WarlockBladeFrames == 0;
    public static bool IsWarlockBladeAnimation(ushort graphic) => IsCleaveSweep(graphic) || IsMirroredMelee(graphic)
        || graphic is CleaveContact or BladeWarpCast or BladeWarpImpact or BladeWarpRadius or BladeWarpDeparture
            or BladeWarpArrival or BladeWarpContact or EchoingStrikeCast or EchoingStrikeTurn or EchoingStrikeHit
            or EchoingStrikeFade or EchoingStrikeDeflect or MirroredBladesCast or MirroredBladesHit or MirroredBladesFade;

    public const int AssassinLobFrames = 4;
    public const int FireBlastRadiusSubtiles = 5;
    public const ushort FireBlastThrow = 62764;
    public const ushort FireBlastFlight = 62768;
    public const ushort FireBlastFuse = 62772;
    public const ushort FireBlastBurst = 62776;
    public const ushort ShockWebThrow = 62780;
    public const ushort ShockWebFlight = 62784;
    public const ushort ShockWebField = 62788;
    public const ushort ShockWebHit = 62792;
    public const ushort ShockWebFade = 62796;
    public static bool IsAssassinLobAnimation(ushort graphic) => graphic is
        FireBlastThrow or FireBlastBurst or ShockWebThrow or ShockWebField or ShockWebHit or ShockWebFade;

    public const int SentryFrames = 4;
    public const int SentryModelFrames = 3 * SentryFrames;
    public const ushort SentryModel = 61800;
    public const ushort SentryWakeFlight = 61860;
    public const ushort SentryInfernoFlight = 61892;
    public const ushort SentryChargedFlight = 61924;
    public const ushort DeathSentryBurst = 61928;
    public const ushort SentryFireFlash = 61932;
    public const ushort SentryLightningFlash = 61936;
    public const ushort SentryLightningFlight = 61940;
    public const ushort SentryModelGone = 61972;
    public static bool IsSentryModelFrame(ushort graphic) => graphic is >= SentryModel and < SentryWakeFlight
        && (graphic - SentryModel) % SentryModelFrames < 2 * SentryFrames;
    public static bool IsSentryCollapse(ushort graphic) => graphic is >= SentryModel and < SentryWakeFlight
        && (graphic - SentryModel) % SentryModelFrames == 2 * SentryFrames;
    public static bool IsSentryFlash(ushort graphic) => graphic is SentryFireFlash or SentryLightningFlash;
    public static bool IsHiddenSummonFrame(ushort graphic) => graphic is HydraHeadGone or SentryModelGone;

    // This block is above the installed ClassicUO archive's stock static range.
    public const ushort FrozenArmorCast = 62800;
    public const ushort FrozenArmorWard = 62804;
    public const ushort FrozenArmorReply = 62808;
    public const ushort FrozenArmorFade = 62812;
    public const ushort ShiverArmorCast = 62816;
    public const ushort ShiverArmorWard = 62820;
    public const ushort ShiverArmorReply = 62824;
    public const ushort ShiverArmorFade = 62828;
    public const ushort ChillingArmorCast = 62832;
    public const ushort ChillingArmorWard = 62836;
    public const ushort ChillingArmorReply = 62840;
    public const ushort ChillingArmorFade = 62844;
    public const ushort EnergyShieldCast = 62848;
    public const ushort EnergyShieldWard = 62852;
    public const ushort EnergyShieldCatch = 62856;
    public const ushort EnergyShieldFade = 62888;
    public const int WardFrames = 4;
    public const int WardOneShot = 6;

    public const ushort NovaRing = 62900;
    public const ushort NovaContact = 62904;
    public const ushort NovaCast = 62908;
    public const ushort TelekinesisCast = 62912;
    public const ushort TelekinesisContact = 62916;
    public const ushort TelekinesisObject = 62920;
    public const ushort TeleportDeparture = 62924;
    public const ushort TeleportArrival = 62928;
    public const int UtilityFrames = 4;
    public const int UtilityDuration = 6;
    public const int NovaDuration = 11;

    public const ushort EnchantCast = 62932;
    public const ushort EnchantWard = 62936;
    public const ushort EnchantContact = 62940;
    public const ushort EnchantFade = 62944;
    public const ushort HydraHeadRight = 62948;
    public const ushort HydraHeadLeft = 62964;
    public const ushort HydraHeadGone = 62980;
    public const ushort HydraBoltFlight = 62984;
    public const ushort HydraBoltContact = 63016;
    public const int HydraPoseFrames = 4;

    public const ushort MagicArrowFlight = 63020;
    public const ushort MagicArrowBolt = 63052;
    public const ushort GuidedArrowFlight = 63084;
    public const ushort GuidedArrowBolt = 63116;
    public const ushort ArrowContact = 63148;
    public const ushort MagicArrowContact = 63152;
    public const ushort GuidedArrowContact = 63156;
    public const int ArrowContactFrames = 4;
    // Elemental bow families each contain eight arrow and eight bolt headings.
    public const ushort FireArrow = 63160;
    public const ushort ExplodingArrow = 63176;
    public const ushort ImmolationArrow = 63192;
    public const ushort ColdArrow = 63208;
    public const ushort IceArrow = 63224;
    public const ushort FreezingArrow = 63240;
    public const ushort FireArrowContact = 63256;
    public const ushort ExplodingArrowBurst = 63260;
    public const ushort ImmolationArrowIgnition = 63264;
    public const ushort ImmolationArrowField = 63268;
    public const ushort ColdArrowContact = 63272;
    public const ushort IceArrowContact = 63276;
    public const ushort FreezingArrowBurst = 63280;
    public const int ElementalArrowBoltOffset = 8;
    public const int ArrowBurstDuration = 8;

    public const ushort LightningJavelin = 63284;
    public const ushort FuryJavelin = 63292;
    public const ushort PoisonJavelin = 63300;
    public const ushort PlagueJavelin = 63308;
    public const ushort JavelinVenomContact = 63316;
    public const ushort PoisonJavelinCloud = 63320;
    public const ushort PlagueJavelinBurst = 63324;
    public const ushort PlagueJavelinCloud = 63328;
    public const int JavelinEffectFrames = 4;
    public const int JavelinBurstDuration = 8;

    public const ushort JabContact = 63332;
    public const ushort FendContact = 63336;
    public const ushort ImpaleContact = 63340;
    public const ushort PowerStrikeContact = 63344;
    public const ushort ImpaleSlow = 63348;
    public const int ThrustEffectFrames = 4;

    public const ushort InnerSightCast = 63352;
    public const ushort InnerSightMark = 63356;
    public const ushort SlowMissilesCast = 63360;
    public const ushort SlowMissilesMark = 63364;
    public const ushort DecoyArrival = 63368;
    public const ushort DecoyMirror = 63372;
    public const ushort DecoyFade = 63376;
    public const ushort ValkyrieArrival = 63380;
    public const ushort ValkyrieAura = 63384;
    public const ushort ValkyrieFade = 63388;
    public const int AmazonUtilityFrames = 4;
    public const int AmazonUtilityBurst = 8;
    public static bool IsAmazonUtilityLoop(ushort graphic) =>
        graphic is InnerSightMark or SlowMissilesMark or DecoyMirror or ValkyrieAura;

    public const ushort DodgeFlash = 63392;
    public const ushort AvoidFlash = 63396;
    public const ushort EvadeTrail = 63400;
    public const int AmazonAvoidanceFrames = 4;
    public const int AmazonAvoidanceDuration = 5;
    public static bool IsAmazonAvoidance(ushort graphic) => graphic is DodgeFlash or AvoidFlash or EvadeTrail;

    public const ushort CurseAmplify = 63404;
    public const ushort CurseWeaken = 63408;
    public const ushort CurseDecrepify = 63412;
    public const ushort CurseLowerResist = 63416;
    public const ushort CurseIronMaiden = 63420;
    public const ushort CurseLifeTap = 63424;
    public const ushort CurseDimVision = 63428;
    public const ushort CurseTerror = 63432;
    public const ushort CurseConfuse = 63436;
    public const ushort CurseAttract = 63440;
    public const ushort CurseCast = 63444;
    public const ushort CurseFade = 63448;
    public const ushort LifeTapHeal = 63452;
    public const ushort IronMaidenReturn = 63456;
    public const int CurseFrames = 4;
    public const int CurseBurstDuration = 7;
    public static bool IsCurseMark(ushort graphic) => graphic is >= CurseAmplify and <= CurseAttract
        && (graphic - CurseAmplify) % CurseFrames == 0
        || graphic is HowlMark or TauntMark or BattleCryMark or GrimWardMark or CloakBlind or CloakExposure;

    public const ushort BoneArmorWard = 63460;
    public const ushort BoneArmorBreak = 63464;
    public const ushort CorpseExplosion = 63468;
    public const int BoneEffectFrames = 4;
    public const ushort BoneWallRise = 63472;
    public const ushort BonePrisonRise = 63476;
    public const int BoneBarrierRiseFrames = 4;
    public const ushort BoneBarrierConnector = 63480;
    public const ushort BoneBarrierRubble = 63481;
    public const ushort BoneArmorCatch = 63482;
    public static bool IsBoneBarrierBodyArt(ushort graphic) =>
        graphic is >= BoneWallRise and < BoneBarrierConnector or BoneBarrierRubble;

    public const ushort PoisonNovaWisp = 63483;
    public const ushort PoisonAfflicted = 63515;
    public const ushort PoisonDaggerContact = 63519;
    public const ushort PoisonExplosionBloom = 63523;
    public const ushort PoisonNovaCast = 63527;
    public const ushort PoisonClear = 63531;
    public const ushort PoisonExplosionMist = 63535;
    public const int PoisonFrames = 4;
    public const int GroundEffectPositionBias = 64;
    public static bool IsPoisonTravel(ushort graphic) =>
        graphic is >= PoisonNovaWisp and < PoisonAfflicted
        || graphic >= PoisonExplosionMist && graphic < PoisonExplosionMist + PoisonFrames;

    public const ushort SkeletonRise = 63539;
    public const ushort SkeletalMageRise = 63543;
    public const ushort ReviveRise = 63547;
    public const ushort UndeadFade = 63551;
    public const int UndeadRitualFrames = 4;
    public const int UndeadRitualDuration = 10;

    public const ushort ClayGolemRise = 63555;
    public const ushort BloodGolemRise = 63559;
    public const ushort IronGolemRise = 63563;
    public const ushort FireGolemRise = 63567;
    public const ushort ClayGolemContact = 63571;
    public const ushort BloodGolemHeal = 63575;
    public const ushort IronGolemSpark = 63579;
    public const ushort GolemRubble = 63583;
    public const ushort GolemFirePulse = 63587;
    public const ushort GolemFireDeath = 63591;
    public const ushort GolemSlow = 63595;
    public const int GolemEffectFrames = 4;
    public static bool IsGolemEffect(ushort graphic) => graphic is >= ClayGolemRise and <= GolemSlow
        && (graphic - ClayGolemRise) % GolemEffectFrames == 0;

    public const ushort WoundActive = 63599;
    public const ushort WoundFade = 63603;
    public const int WoundFrames = 4;

    public const ushort DruidWolfEnter = 63607;
    public const ushort DruidWolfLeave = 63611;
    public const ushort DruidBearEnter = 63615;
    public const ushort DruidBearLeave = 63619;
    public const ushort DruidMaulImpact = 63623;
    public const ushort DruidFeralImpact = 63627;
    public const ushort DruidMaulCharge = 63631;
    public const ushort DruidFeralCharge = 63643;
    public const ushort DruidLastCharge = 63651;
    public const ushort DruidFireClaws = 63655;
    public const ushort DruidHungerBite = 63659;
    public const ushort DruidHungerMana = 63663;
    public const ushort DruidFuryImpact = 63667;
    public const int DruidFormEffectFrames = 4;
    public static bool IsDruidFormEffect(ushort graphic) => graphic is >= DruidWolfEnter and <= DruidFuryImpact
        && (graphic - DruidWolfEnter) % DruidFormEffectFrames == 0;
    public static bool IsDruidCharge(ushort graphic) => graphic is >= DruidMaulCharge and <= DruidLastCharge
        && IsDruidFormEffect(graphic);

    public const ushort ShockWaveFlight = 63671;
    public const ushort ShockWaveImpact = 63703;
    public const ushort CombatStun = 63707;
    public const ushort ShockWaveRelease = 63711;
    public const ushort RabiesBite = 63715;
    public const ushort RabiesCarrier = 63719;
    public const ushort RabiesSpread = 63723;
    public const ushort RabiesFade = 63727;
    public const ushort FirestormRelease = 63731;
    public const ushort FirestormHead = 63735;
    public const ushort FissureOpening = 63767;
    public const ushort FissureCrack = 63771;
    public const ushort FissureCrackMirrored = 63775;
    public const ushort BoulderFormation = 63779;
    public const ushort BoulderRolling = 63783;
    public const ushort BoulderBurst = 63787;
    public const ushort BoulderContact = 63791;
    public const ushort DruidFireBed = 63795;
    public const int DruidGroundFireFrames = 4;
    public const ushort TwisterTravel = 63799;
    public const ushort TornadoTravel = 63803;
    public const ushort DruidWindRelease = 63807;
    public const ushort DruidWindContact = 63811;
    public const ushort TwisterFade = 63815;
    public const ushort TornadoFade = 63819;
    public const ushort TornadoPulse = 63823;
    public const ushort HurricaneRelease = 63827;
    public const ushort HurricaneCore = 63831;
    public const ushort HurricaneFade = 63835;
    public const ushort HurricaneWisp = 63839;
    public const int DruidWindFrames = 4;
    public const ushort ArcticBlastRelease = 63871;
    public const ushort ArcticBlastFlight = 63903;
    public const ushort ArcticBlastFade = 63935;
    public const ushort ArcticBlastContact = 63939;
    public const ushort CycloneArmorRaise = 63943;
    public const ushort CycloneArmorWard = 63947;
    public const ushort CycloneArmorCatch = 63951;
    public const ushort CycloneArmorBreak = 63955;
    public const int DruidColdFrames = 4;
    public const ushort VolcanoOpening = 63959;
    public const ushort VolcanoVent = 63963;
    public const ushort VolcanoDebris = 63967;
    public const ushort VolcanoCooling = 63971;
    public const ushort VolcanoLanding = 63975;
    public const ushort ArmageddonRelease = 63979;
    public const ushort ArmageddonAura = 63983;
    public const ushort ArmageddonWarning = 63987;
    public const ushort ArmageddonFall = 63991;
    public const ushort ArmageddonImpact = 64001;
    public const int DruidEruptionFrames = 4;
    public const int ArmageddonFallFrames = 10;
    public const ushort RavenArrival = 64005;
    public const ushort SpiritWolfArrival = 64009;
    public const ushort DireWolfArrival = 64013;
    public const ushort GrizzlyArrival = 64017;
    public const ushort RavenDeparture = 64021;
    public const ushort SpiritWolfDeparture = 64025;
    public const ushort DireWolfDeparture = 64029;
    public const ushort GrizzlyDeparture = 64033;
    public const ushort RavenContact = 64037;
    public const ushort SpiritWolfContact = 64041;
    public const ushort DireWolfContact = 64045;
    public const ushort GrizzlyContact = 64049;
    public const ushort DireWolfRage = 64053;
    public const ushort RavenBlind = 64057;
    public const ushort GrizzlySmite = 64061;
    public const int DruidAnimalFrames = 4;
    public const ushort OakSageArrival = 64065;
    public const ushort WolverineArrival = 64069;
    public const ushort BarbsArrival = 64073;
    public const ushort OakSageAura = 64077;
    public const ushort WolverineAura = 64081;
    public const ushort BarbsAura = 64085;
    public const ushort DruidSpiritDeparture = 64089;
    public const ushort BarbsRetaliation = 64093;
    public const int DruidSpiritFrames = 4;
    public const ushort CreeperArrival = 64097;
    public const ushort CarrionArrival = 64101;
    public const ushort SolarArrival = 64105;
    public const ushort CreeperDeparture = 64109;
    public const ushort CarrionDeparture = 64113;
    public const ushort SolarDeparture = 64117;
    public const ushort CreeperHead = 64121;
    public const ushort CreeperMat = 64153;
    public const ushort CreeperMatFade = 64157;
    public const ushort VineCorpse = 64161;
    public const ushort CarrionRecovery = 64165;
    public const ushort SolarRecovery = 64169;
    public const ushort CreeperPoison = 64173;
    public const int DruidVineFrames = 4;

    public static bool IsDruidVineAnimation(ushort graphic)
        => graphic is (>= CreeperArrival and <= SolarDeparture or >= VineCorpse and <= CreeperPoison)
            && (graphic - CreeperArrival) % DruidVineFrames == 0;

    public const ushort HolyFirePulse = 64177;
    public const ushort HolyFreezePulse = 64181;
    public const ushort HolyShockPulse = 64185;
    public const ushort HolyFireAura = 64189;
    public const ushort HolyFreezeAura = 64193;
    public const ushort HolyShockAura = 64197;
    public const ushort HolyFreezeSlow = 64201;
    public const ushort HolyFireContact = 64205;
    public const ushort HolyFreezeContact = 64209;
    public const ushort HolyShockContact = 64213;
    public const int HolyAuraFrames = 4;
    public const int HolyAuraRadiusUnitsPerTile = 16;
    public const int HolyAuraRingWidth = 240;
    public const int HolyAuraRingHeight = 132;

    public static bool IsHolyAuraAnimation(ushort graphic) => graphic is >= HolyFirePulse and <= HolyShockContact
        && (graphic - HolyFirePulse) % HolyAuraFrames == 0;

    public static bool IsRadiusPulse(ushort graphic) =>
        graphic is HolyFirePulse or HolyFreezePulse or HolyShockPulse or SanctuaryPulse or GrimWardPulse
            or LeapLanding or LeapAttackLanding or MartialFireRing or MartialThunderRing or MartialIceRing
            or DragonTailBurst or CloakWave or MindBlastWave or DeathSentryBurst or BloodBoilRing or ApocalypseWarning
            or DeathSigilBurstRing or HexPurgeRadius or EldritchBlastRing or BladeWarpRadius || IsSigilField(graphic);

    public static bool IsPaladinAuraLoop(ushort graphic) => graphic is HolyFireAura or HolyFreezeAura or HolyShockAura or HolyFreezeSlow
        || IsPaladinResistanceWard(graphic) || IsPaladinRecoveryLoop(graphic) || IsPaladinOffenseLoop(graphic)
        || graphic is DefianceAura or VigorAura or ThornsAura or HolyShieldShell or SanctuaryAura or ConvictionAura or ConvictionMark
            or ConversionMark;

    public const ushort HolyBoltFlight = 64217;
    public const ushort HeavenBoltFlight = 64249;
    public const ushort BlessedHammerFlight = 64281;
    public const ushort HolyBoltRelease = 64285;
    public const ushort HolyBoltHeal = 64289;
    public const ushort HolyBoltContact = 64293;
    public const ushort HolyBoltFade = 64297;
    public const ushort BlessedHammerRelease = 64301;
    public const ushort BlessedHammerContact = 64305;
    public const ushort BlessedHammerFade = 64309;
    public const ushort HeavenFistWarning = 64313;
    public const ushort HeavenFistStrike = 64317;
    public const ushort HeavenFistBurst = 64321;
    public const int PaladinSpellFrames = 4;

    public static bool IsPaladinProjectileSample(ushort graphic) => graphic is >= HolyBoltFlight and < HolyBoltRelease;

    public static bool IsPaladinSpellAnimation(ushort graphic) => graphic is >= HolyBoltRelease and <= HeavenFistBurst
        && (graphic - HolyBoltRelease) % PaladinSpellFrames == 0;

    public const ushort ResistFireWard = 64325;
    public const ushort ResistColdWard = 64329;
    public const ushort ResistLightningWard = 64333;
    public const ushort SalvationWard = 64337;

    public static bool IsPaladinResistanceWard(ushort graphic) => graphic is ResistFireWard or ResistColdWard or ResistLightningWard or SalvationWard;

    public const ushort PrayerAura = 64341;
    public const ushort CleansingAura = 64345;
    public const ushort MeditationAura = 64349;
    public const ushort RedemptionAura = 64353;
    public const ushort PrayerHeal = 64357;
    public const ushort CleansingPurify = 64361;
    public const ushort MeditationMana = 64365;
    public const ushort RedemptionCorpse = 64369;
    public const ushort RedemptionLife = 64373;
    public const ushort RedemptionMana = 64377;
    public const ushort RedemptionThread = 64381;
    public const int RedemptionThreadFrames = 32;

    public static bool IsPaladinRecoveryLoop(ushort graphic) => graphic is PrayerAura or CleansingAura or MeditationAura or RedemptionAura;

    public static bool IsPaladinRecoveryAnimation(ushort graphic) => graphic is >= PrayerAura and <= RedemptionMana
        && (graphic - PrayerAura) % PaladinSpellFrames == 0;

    public const ushort MightAura = 64413;
    public const ushort BlessedAimAura = 64417;
    public const ushort ConcentrationAura = 64421;
    public const ushort FanaticismAura = 64425;
    public const ushort ConcentrationGuard = 64429;

    public static bool IsPaladinOffenseLoop(ushort graphic) => graphic is MightAura or BlessedAimAura or ConcentrationAura or FanaticismAura;

    public const ushort DefianceAura = 64433;
    public const ushort VigorAura = 64437;
    public const ushort ThornsAura = 64441;
    public const ushort ThornsContact = 64445;
    public const ushort HolyShieldRaise = 64449;
    public const ushort HolyShieldShell = 64453;
    public const ushort HolyShieldBlock = 64457;
    public const ushort HolyShieldFade = 64461;

    public static bool IsPaladinDefenseAnimation(ushort graphic) => graphic is >= DefianceAura and <= HolyShieldFade
        && (graphic - DefianceAura) % PaladinSpellFrames == 0;

    public const ushort SanctuaryAura = 64465;
    public const ushort SanctuaryPulse = 64469;
    public const ushort SanctuaryContact = 64473;
    public const ushort ConvictionAura = 64477;
    public const ushort ConvictionMark = 64481;
    public const ushort ConvictionContact = 64485;

    public static bool IsPaladinHostileAuraAnimation(ushort graphic) => graphic is >= SanctuaryAura and <= ConvictionContact
        && (graphic - SanctuaryAura) % HolyAuraFrames == 0;

    public const ushort SacrificeSwing = 64489;
    public const ushort SacrificeCost = 64493;
    public const ushort SacrificeImpact = 64497;
    public const ushort SmiteSwing = 64501;
    public const ushort SmiteImpact = 64505;
    public const ushort VengeanceSwing = 64509;
    public const ushort VengeanceImpact = 64513;
    public const ushort ZealSwing = 64517;
    public const ushort ZealImpact = 64521;
    public const ushort ChargeLaunch = 64525;
    public const ushort ChargeTrail = 64529;
    public const ushort ChargeImpact = 64533;
    public const ushort ConversionSwing = 64537;
    public const ushort ConversionLock = 64541;
    public const ushort ConversionMark = 64545;
    public const ushort ConversionFade = 64549;

    public static bool IsPaladinMeleeAnimation(ushort graphic) => graphic is >= SacrificeSwing and <= ConversionFade
        && (graphic - SacrificeSwing) % PaladinSpellFrames == 0;

    public const ushort HowlCast = 64553;
    public const ushort TauntCast = 64557;
    public const ushort BattleCryCast = 64561;
    public const ushort WarCryCast = 64565;
    public const ushort HowlMark = 64569;
    public const ushort TauntMark = 64573;
    public const ushort BattleCryMark = 64577;
    public const ushort WarCryImpact = 64581;
    public const ushort HowlWave = 64585;
    public const ushort BattleCryWave = 64617;
    public const ushort WarCryWave = 64649;
    public const int BarbarianCryFrames = 4;
    public const ushort ShoutCast = 64681;
    public const ushort BattleOrdersCast = 64685;
    public const ushort BattleCommandCast = 64689;
    public const ushort ShoutWard = 64693;
    public const ushort BattleOrdersWard = 64697;
    public const ushort BattleCommandWard = 64701;
    public const ushort ShoutFade = 64705;
    public const ushort BattleOrdersFade = 64709;
    public const ushort BattleCommandFade = 64713;
    public const ushort ShoutWave = 64717;
    public const ushort BattleOrdersWave = 64749;
    public const ushort BattleCommandWave = 64781;

    public static bool IsBarbarianCryAnimation(ushort graphic) =>
        graphic is (>= HowlCast and <= WarCryImpact) or (>= ShoutCast and <= BattleCommandFade)
        && (graphic - HowlCast) % BarbarianCryFrames == 0;
    public static bool IsBarbarianCryWard(ushort graphic) => graphic is ShoutWard or BattleOrdersWard or BattleCommandWard;

    public const ushort FindPotionCast = 64813;
    public const ushort FindPotionSearch = 64817;
    public const ushort FindPotionYield = 64821;
    public const ushort FindPotionEmpty = 64825;
    public const ushort FindItemCast = 64829;
    public const ushort FindItemSearch = 64833;
    public const ushort FindItemYield = 64837;
    public const ushort FindItemEmpty = 64841;
    public const ushort GrimWardRise = 64845;
    public const ushort GrimWardBody = 64857;
    public const ushort GrimWardFall = 64869;
    public const ushort GrimWardMark = 64881;
    public const ushort GrimWardPulse = 64885;

    public static bool IsBarbarianCorpseAnimation(ushort graphic) => graphic is >= FindPotionCast and <= GrimWardPulse
        && (graphic - FindPotionCast) % BarbarianCryFrames == 0;
    public static bool IsGrimWardBody(ushort graphic) => graphic is GrimWardBody or GrimWardBody + 4 or GrimWardBody + 8;

    public static bool IsDruidSpiritAura(ushort graphic) => graphic is OakSageAura or WolverineAura or BarbsAura;
    public static bool IsDruidSpiritAnimation(ushort graphic) => graphic is >= OakSageArrival and <= BarbsRetaliation
        && (graphic - OakSageArrival) % DruidSpiritFrames == 0;

    public static bool IsDruidAnimalAnimation(ushort graphic) => graphic is >= RavenArrival and <= GrizzlySmite
        && (graphic - RavenArrival) % DruidAnimalFrames == 0;

    public static bool IsArcticRelease(ushort graphic) => graphic is >= ArcticBlastRelease and < ArcticBlastFlight
        && (graphic - ArcticBlastRelease) % DruidColdFrames == 0;
    public const int DruidStatusEffectFrames = 4;
    public static bool HasFractionalGroundPosition(ushort graphic) =>
        IsPoisonTravel(graphic) || IsPaladinProjectileSample(graphic) || graphic is >= ShockWaveFlight and < ShockWaveImpact
            or >= FirestormHead and < FissureOpening or >= FissureCrack and <= BoulderBurst
            or >= DruidFireBed and < DruidFireBed + DruidGroundFireFrames
            or >= TwisterTravel and < DruidWindRelease or TwisterFade or TornadoFade or TornadoPulse
            or >= HurricaneWisp and < HurricaneWisp + 8 * DruidWindFrames
            or >= ArcticBlastFlight and < ArcticBlastContact
            or >= VolcanoVent and < VolcanoCooling or VolcanoLanding
            or >= ArmageddonWarning and <= ArmageddonImpact
            or >= CreeperHead and < VineCorpse or HolyBoltFade or BlessedHammerFade
            or >= RedemptionThread and < RedemptionThread + RedemptionThreadFrames
            or >= HowlWave and < WarCryWave + 8 * BarbarianCryFrames
            or >= ShoutWave and < BattleCommandWave + 8 * BarbarianCryFrames
            or >= LeapAirTrail and < LeapAttackLanding or >= MartialIceCube and < MartialIceCube + MartialFrames
            or >= BladeFuryFlight and < BladeFuryHit
            or >= FireBlastFlight and <= FireBlastBurst
            or >= ShockWebFlight and <= ShockWebField or ShockWebFade
            or >= SentryWakeFlight and < DeathSentryBurst
            or >= SentryLightningFlight and < SentryLightningFlight + 8 * SentryFrames
            or >= ConsumeTravel and < ConsumeWard or >= EngorgeTravel and < EngorgeHeal
            or >= BindDemonTravel and < BindDemonSeal
            or >= RingOfFireTravel and < RingOfFireTravel + WarlockFireFrames
            or >= FlameWaveFront and <= FlameWaveField or FlameWaveFade
            or >= MiasmaBoltFlight and < MiasmaBoltCloud or >= MiasmaChainLink and < MiasmaChainCloud
            or MiasmaBoltFade or MiasmaChainMistFade or MiasmaChainFade
            or >= BladeWarpFlight and < BladeWarpImpact
            or >= EchoingStrikeOut and < EchoingStrikeTurn
            or >= MirroredBladesArrow and < MirroredBladesHit or MirroredBladesFade
            || IsMiasmaCloud(graphic)
            || IsCleaveSweep(graphic)
            || IsMirroredMelee(graphic)
            || IsBarbarianThrowFlight(graphic);

    public static bool IsHydraHeadFrame(ushort graphic) => graphic is >= HydraHeadRight and <= HydraHeadGone;
    public static bool IsEnchantAnimation(ushort graphic) =>
        graphic is EnchantCast or EnchantWard or EnchantContact or EnchantFade;

    public const ushort BoneSpearFlight = 64900;
    public const ushort BoneSpearShatter = 64908;
    public const int BoneSpearShatterFrames = 8;
    public const ushort BoneSpearContact = 64916;

    public const ushort MeteorWarning = 64920;
    public const ushort MeteorFall = 64924;
    public const ushort MeteorImpact = 64928;
    public const ushort MeteorFire = 64932;
    public const int MeteorFrames = 4;
    public const int MeteorFallMilliseconds = 400;
    public const int MeteorWarningDuration = 48;
    public const int MeteorImpactDuration = 8;
    public const int MeteorFireDuration = 4;

    public const ushort BlizzardFall = 64936;
    public const ushort BlizzardContact = 64944;
    public const ushort BlizzardFrost = 64948;
    public const int BlizzardFallFrames = 8;
    public const int BlizzardShardDuration = 7;

    public const ushort FrozenOrb = 64952;
    public const ushort FrozenOrbBolt = 64956;
    public const ushort FrozenOrbBurst = 64988;
    public const ushort FrozenOrbContact = 64992;
    public const int ColdFrames = 4;

    // The original red and blue flare art occupies 65000 through 65045.
    public const ushort StrafeArrow = 65046;
    public const ushort StrafeBolt = 65054;

    public const ushort TeethFlight = 65062;
    public const ushort TeethContact = 65094;
    public const ushort BoneSpiritFlight = 65098;
    public const ushort BoneSpiritBurst = 65130;
    public const int NecromancerFrames = 4;

    public const ushort AbyssOpen = 65134;
    public const ushort AbyssField = 65138;
    public const ushort AbyssPull = 65142;
    public const ushort AbyssCollapse = 65146;
    public const int AbyssFrames = 4;
    public const int AbyssPulseMilliseconds = 480;
    public const int AbyssRefreshDuration = 10;

    public const ushort ChargedBoltFlight = 65150;
    public const ushort ChargedBoltContact = 65182;
    public const ushort LightningFlight = 65186;
    public const ushort LightningContact = 65218;
    public const ushort ChainLightningFlight = 65222;
    public const ushort ChainLightningContact = 65254;
    public const ushort StaticFieldSmall = 65258;
    public const ushort StaticFieldMedium = 65262;
    public const ushort StaticFieldLarge = 65266;
    public const ushort StaticFieldContact = 65270;
    public const ushort ThunderStormCharge = 65274;
    public const ushort ThunderStormStrike = 65278;
    public const int LightningFrames = 4;
    public const int StaticFieldDuration = 8;
    public const int ThunderStormChargeDuration = 12;
    public const int ThunderStormStrikeDuration = 6;

    public const ushort IceBoltFlight = 65282;
    public const ushort IceBoltContact = 65314;
    public const ushort IceBlastFlight = 65318;
    public const ushort IceBlastContact = 65350;
    public const ushort GlacialSpikeFlight = 65354;
    public const ushort GlacialSpikeBurst = 65386;
    public const ushort FrostNovaRing = 65390;
    public const ushort FrostNovaContact = 65394;
    public const ushort Chilled = 65400;
    public const ushort Frozen = 65404;
    public const ushort ColdCorpseShatter = 65408;
    public const ushort Thaw = 65412;
    public const int FrostNovaDuration = 11;
    public const int GlacialSpikeBurstDuration = 8;

    public const ushort FireBoltFlight = 65416;
    public const ushort FireBoltContact = 65448;
    public const ushort InfernoFlight = 65452;
    public const ushort InfernoContact = 65484;
    public const ushort BlazeCast = 65488;
    public const ushort BlazeField = 65492;
    public const ushort BlazeFade = 65496;
    public const ushort FireWallIgnite = 65500;
    public const ushort FireWallField = 65504;
    public const ushort FireWallFade = 65508;
    public const int FireFrames = 4;

    public static bool IsColdStatus(ushort graphic) => graphic is Chilled or Frozen;
    public static bool IsFireField(ushort graphic) => graphic is BlazeField or FireWallField or ImmolationArrowField
        or MartialFistsField or MartialPhoenixField;
    public static bool IsJavelinCloud(ushort graphic) => graphic is PoisonJavelinCloud or PlagueJavelinCloud;
    public static bool IsSorceressWard(ushort graphic) =>
        graphic is FrozenArmorWard or ShiverArmorWard or ChillingArmorWard or EnergyShieldWard;
    public static bool IsWardAnimation(ushort graphic) => graphic is >= FrozenArmorCast and <= EnergyShieldFade
        && (graphic - FrozenArmorCast) % WardFrames == 0;

    public static bool IsColdProjectile(ushort graphic) => graphic is
        >= IceBoltFlight and < IceBoltContact or >= IceBlastFlight and < IceBlastContact
            or >= GlacialSpikeFlight and < GlacialSpikeBurst;

    // Convert world motion to the eight directions of the isometric view.
    public static int ProjectileFacing(double vx, double vy) =>
        (int)Math.Round(Math.Atan2(vx + vy, vx - vy) / (Math.PI / 4)) & 7;
}
