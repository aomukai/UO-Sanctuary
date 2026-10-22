// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public readonly record struct GearDamage(double Physical, double Fire, double Cold, double Lightning, double Poison, int PoisonMs = 3000, int ColdMs = 2000, double Magic = 0)
{
    public double Total => Physical + Fire + Cold + Lightning + Poison + Magic;
}

public readonly record struct PreparedWeaponImpact(GearDamage Damage,GearDamage Parts,int Absorbed256,int? LifeDamage256=null);

public static class GearEffects
{
    public static void Configure()
    {
        ClassGearHooks.MovementDelay = (mobile, seconds) => SourceTerror.MovementDelay(mobile,PaladinVigor.MovementDelay(mobile,seconds)) * 100 / (100 - MovementSlow(mobile));
        ClassGearHooks.AttackDelay = (mobile, delay) => TimeSpan.FromTicks(delay.Ticks * 100 / Math.Clamp(100 + (mobile is BaseCreature?PaladinOffense.AttackRate(mobile):0) - AttackSlow(mobile),15,175));
    }
    [CallPriority(100)]
    public static void Initialize()
    {
        var hits = Mobile.HitsRegenRateHandler;
        Mobile.HitsRegenRateHandler = mobile => RegenerationRate(mobile, hits?.Invoke(mobile) ?? Mobile.DefaultHitsRate, "life-per-five");
        var mana = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = mobile => RegenerationRate(mobile, mana?.Invoke(mobile) ?? Mobile.DefaultManaRate, "mana-per-five");
        var stamina = Mobile.StamRegenRateHandler;
        Mobile.StamRegenRateHandler = mobile =>
        {
            var baseline = stamina?.Invoke(mobile) ?? Mobile.DefaultStamRate;
            int bonus=mobile is PlayerMobile player && ClassCombat.Enabled(player)?ClassGear.Bonus(player,"regen-stam"):0;
            bonus+=PaladinVigor.RecoveryPercent(mobile);
            return mobile.Alive && bonus!=0 ? TimeSpan.FromTicks(Math.Max(1,baseline.Ticks*100/Math.Max(1,100+bonus))):baseline;
        };
    }
    public static TimeSpan RegenerationRate(Mobile mobile, TimeSpan baseline, string code)
    {
        if (mobile is not PlayerMobile player || !ClassCombat.Enabled(player) || !player.Alive
            || !(code == "life-per-five" ? player.CanRegenHits : player.CanRegenMana) || baseline.Ticks <= 0) { return baseline; }
        double perSecond = 1 / baseline.TotalSeconds;
        if (code == "life-per-five") { perSecond += ClassGear.Bonus(player, "replenish-life") * 25.0 / 256; }
        else { perSecond *= (100 + Math.Max(0, ClassGear.Bonus(player, "mana-regen%") + SorceressCombat.Passive(player, CombatAbility.Warmth) + PaladinAuras.ManaRecoveryPercent(player))) / 100.0; }
        perSecond += ClassGear.Material(player, code) / 5.0;
        return TimeSpan.FromTicks(Math.Max(1, (long)(TimeSpan.TicksPerSecond / perSecond)));
    }
    public static int ChillPercent(Mobile mobile) => ChillPercent(mobile, Core.TickCount);
    public static int ChillPercent(Mobile mobile, long now) => Math.Max(mobile is PlayerMobile player
        && ClassGear.Bonus(player, "nofreeze") > 0 ? 0 : TimedEffects.Chill(mobile, TimedEffects.TimeAt(now)), EliteAuras.Slow(mobile));
    public static int MovementSlow(Mobile mobile) => Math.Clamp(ChillPercent(mobile)
        + TimedEffects.Slow(mobile, TimedEffects.Now) + NecromancerEffects.Slow(mobile) + PaladinHolyFreeze.VelocitySlow(mobile), 0, 75);
    public static int AttackSlow(Mobile mobile) => Math.Clamp(ChillPercent(mobile)
        + TimedEffects.Slow(mobile, TimedEffects.Now) + NecromancerEffects.Slow(mobile) + PaladinHolyFreeze.AttackSlow(mobile), 0, 85);
    public static bool ApplyChill(Mobile target, long now, int durationMs = 2000, Mobile attacker = null)
    {
        if (target.Deleted || !target.Alive || NamedElements.Resistance(attacker, target, "cold", target.ColdResistance) >= 100) { return false; }
        if (target is PlayerMobile player)
        {
            if (ClassGear.Bonus(player, "nofreeze") > 0) { return false; }
            if (ClassGear.Bonus(player, "half-freeze") > 0) { durationMs /= 2; }
        }
        int effect = 50;
        if (target is BaseCreature creature)
        {
            var row = CreatureProfiles.For(creature);
            if (row.Traits.ChillEffectiveness is { } percent) { effect = percent; }
            else if (row.SourceStats.TryGetValue("coldeffect", out var value) && int.TryParse(value, out int source))
            { effect = Math.Abs(source); }
        }
        effect = Math.Clamp(effect, 0, 50);
        int resistance = NamedElements.Resistance(attacker, target, "cold", target.ColdResistance);
        int duration = durationMs * Math.Clamp(100 - resistance, 0, 200) / 100;
        if (effect == 0 || duration == 0) { return false; }
        TimedEffects.Chill(target, effect, duration, TimedEffects.TimeAt(now));
        target.FixedEffect(0x374A, 10, 15, 0x480, 0);
        return true;
    }
    public static bool ApplyPoison(Mobile source, Mobile target, double total, long now, int durationMs = 3000)
    {
        total = NamedElements.Damage(source, target, total, "pois");
        if (target is PlayerMobile player)
        {
            int reduction = Math.Clamp(ClassGear.Bonus(player, "res-pois-len"), 0, 75);
            durationMs = durationMs * (100 - reduction) / 100;
            total *= (100 - reduction) / 100.0;
        }
        if (total <= 0 || durationMs <= 0 || target.Deleted || !target.Alive || target.Blessed) { return false; }
        if (!TimedEffects.Poison(source, target, total, durationMs, TimedEffects.TimeAt(now))) { return false; }
        target.FixedEffect(0x374A, 10, 15, 0x3F, 0);
        return true;
    }
    public static void Tick(long now) => TimedEffects.Tick(TimedEffects.TimeAt(now));
    public static void Clear(Mobile mobile) => TimedEffects.ClearGear(mobile);
    public static GearDamage Damage(PlayerMobile player, double roll, Mobile target = null,int? skillPhysicalPercent=null,Func<int,int> physicalRoll=null)
    {
        roll = Math.Clamp(roll, 0, 1);
        var weapon = CombatTiming.Equipped(player);
        int targetBonus=GemCombat.Undead(target)?ClassGear.Bonus(player,"dmg-undead%")+PaladinSanctuary.DamagePercent(player,target)
            :NamedCombat.Demon(target)?ClassGear.Bonus(player,"dmg-demon%"):0;
        var physical = ClassStats.PhysicalDamageRange(player,targetBonus);
        double value = physical.Min + (physical.Max - physical.Min) * roll;
        if(skillPhysicalPercent is{} skillPercent)
        {
            var range=ClassStats.SkillPhysicalRange256(player,skillPercent,targetBonus);
            int span=Math.Max(1,range.Max-range.Min);
            int rolled=(physicalRoll??Utility.Random)(span);
            value=(range.Min+Math.Clamp(rolled,0,span-1))/256.0+CombatPotions.Bonus(player,"deep-wounds");
        }
        if (weapon == null || ClassGear.Base(weapon) is not { } basis)
        {
            var unarmed = NamedGear.Poison(player, roll);
            int duration = unarmed.Count == 0 ? 3000 : unarmed.Length / unarmed.Count;
            return new(value, NamedGear.Elemental(player, "fire", roll) + SorceressEffects.EnchantFire(player, roll) + PaladinElements.Weapon(player,"fire",roll), NamedGear.Elemental(player, "cold", roll) + PaladinElements.Weapon(player,"cold",roll),
                NamedGear.Elemental(player, "lightning", roll) + PaladinElements.Weapon(player,"ltng",roll), unarmed.Rate * duration, duration, NamedGear.ColdDuration(player), NamedGear.Elemental(player, "magic", roll));
        }
        bool two = weapon.Layer == Layer.TwoHanded || basis.TwoHanded && !basis.OneOrTwoHanded;
        int min = two && basis.TwoHandMax > 0 ? basis.TwoHandMin : basis.MinDamage;
        int max = two && basis.TwoHandMax > 0 ? basis.TwoHandMax : basis.MaxDamage;
        if (ClassGear.Definition(weapon).Category == "Throwing" && basis.MissileMax > 0)
        { min = basis.MissileMin; max = basis.MissileMax; }
        double physicalReference = (min + (max - min) * roll) * Math.Max(10, 100 + ClassStats.PhysicalDamageBonus(player, weapon)+(skillPhysicalPercent??0)) / 10000.0;
        double reference = (min + (max - min) * roll) * Math.Max(0, 100 + ClassStats.DamageBonus(player, weapon) - PaladinAuras.DamagePercent(player)) / 10000.0;
        var poison = GemCombat.PoisonCombined(weapon, reference * ClassGear.Material(weapon, "damage-poison"), NamedGear.Poison(player, roll));
        double cold = reference * ClassGear.Material(weapon, "damage-cold");
        int coldMs = (cold > 0 ? 2000 : 0) + GemCombat.Duration(weapon, "cold-damage") + NamedGear.ColdDuration(player);
        return new(value + physicalReference * ClassGear.Material(weapon, "damage-physical"),
            reference * ClassGear.Material(weapon, "damage-fire") + GemCombat.Value(weapon, "fire-damage", roll) + NamedGear.Elemental(player, "fire", roll) + SorceressEffects.EnchantFire(player, roll) + PaladinElements.Weapon(player,"fire",roll),
            cold + GemCombat.Value(weapon, "cold-damage", roll) + NamedGear.Elemental(player, "cold", roll) + PaladinElements.Weapon(player,"cold",roll),
            reference * ClassGear.Material(weapon, "damage-lightning") + GemCombat.Value(weapon, "lightning-damage", roll) + NamedGear.Elemental(player, "lightning", roll) + PaladinElements.Weapon(player,"ltng",roll),
            poison.Total, poison.Duration, coldMs, NamedGear.Elemental(player, "magic", roll));
    }
    private static double WeaponElement(PlayerMobile player,double total,string element,double roll,bool ranged)
    {
        double aura=ranged?PaladinElements.Weapon(player,element,roll):0;
        double enhanced=NamedElements.Enhance(player,total-aura,element)+aura;
        return enhanced;
    }
    internal static PreparedWeaponImpact PrepareWeaponImpact(PlayerMobile player,Mobile target,double roll,Random effects,GearDamage damage,GearDamage? skillElements=null,int physicalToMagicPercent=0)
    {
        bool ranged=NamedCombat.Ranged(player);
        double rawPhysical=damage.Physical*NamedCombat.Deadly(player,effects);
        // Skill elements already include their source percentage/mastery rounding.
        // Ordinary weapon hits must also reach Energy Shield before resistance.
        double convertedMagic=0;
        if(physicalToMagicPercent>0)
        {
            var split=BarbarianConcentrate.Split(NamedElements.To256(rawPhysical),physicalToMagicPercent);
            rawPhysical=split.Physical256/256.0;convertedMagic=split.Magic256/256.0;
        }
        var extra=skillElements.GetValueOrDefault();
        var parts=SkillCombat.MitigatePrepared(player,target,new(NamedElements.Enhance(player,rawPhysical,"physical"),
            WeaponElement(player,damage.Fire,"fire",roll,ranged)+extra.Fire,
            WeaponElement(player,damage.Cold,"cold",roll,ranged)+extra.Cold,
            WeaponElement(player,damage.Lightning,"ltng",roll,ranged)+extra.Lightning,0,
            Magic:NamedElements.Enhance(player,damage.Magic,"mag")+convertedMagic),out int absorbed256);
        damage=damage with{Cold=damage.Cold+extra.Cold,ColdMs=damage.ColdMs+extra.ColdMs};
        return new(damage,parts,absorbed256);
    }
    public static int Strike(PlayerMobile player, Mobile target, double roll, Random effects = null, bool wearWeapon = true,GearDamage? skillElements=null,
        int? skillPhysicalPercent=null,Func<int,int> physicalRoll=null,Func<int,bool> beforeCommit=null,bool skillKnockback=false,GearDamage? preparedDamage=null,PreparedWeaponImpact? preparedImpact=null)
    {
        effects ??= Random.Shared;
        var weapon = CombatTiming.Equipped(player);
        bool ranged = CombatTiming.Weapon(weapon).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw;
        var packet=preparedImpact??PrepareWeaponImpact(player,target,roll,effects,preparedDamage??Damage(player,roll,target,skillPhysicalPercent,physicalRoll),skillElements);
        var damage=packet.Damage;var parts=packet.Parts;int absorbed256=packet.Absorbed256;
        double physical=parts.Physical,total=packet.LifeDamage256 is{} stored?stored/256.0:parts.Total;
        if(beforeCommit!=null && !beforeCommit(NamedElements.To256(physical))){return 0;}
        if(beforeCommit==null && absorbed256>0){SkillHealth.Heal(target,absorbed256);}
        int before = target.Hits; var impact = target.Location; var impactMap = target.Map;
        PaladinThorns.Prepared(player,target,(int)Math.Clamp(Math.Floor(physical*256),0,int.MaxValue),ranged);
        NamedCombat.Crushing(player, target, ranged, effects);
        if(beforeCommit!=null && absorbed256>0){SkillHealth.Heal(target,absorbed256);}
        int normalBefore = target.Hits;
        if (total > 0 && target.Alive && !target.Deleted)
        {
            SkillHealth.Damage(player,target,NamedElements.To256(total));
            if (target is BaseCreature creature)
            { CreatureHitRecovery.Apply(creature, player, Math.Max(0, before - target.Hits), true); }
            CombatDamage.Finish(target, player);
        }
        if (target.Alive && !target.Deleted)
        {
            if (damage.Cold > 0) { ApplyChill(target, Core.TickCount, damage.ColdMs, player); }
            if (damage.Poison > 0) { ApplyPoison(player, target, damage.Poison, Core.TickCount, damage.PoisonMs); }
        }
        int loss = Math.Max(0, before - target.Hits);
        if (loss > 0 && impactMap != null && impactMap != Map.Internal && ClassGear.Bonus(player, "bloody") > 0)
        { new Blood().MoveToWorld(impact, impactMap); }
        GemCombat.Leech(player, target, Math.Min(Math.Max(0, normalBefore - target.Hits), (int)Math.Floor(physical)));
        NecromancerEffects.WeaponHit(player, target, Math.Min(loss, (int)physical), ranged);
        NamedCombat.AfterStrike(player, target, ranged, effects,skipKnockback:skillKnockback);
        GemCombat.WeaponHit(player, target, loss, ranged);
        if (wearWeapon && weapon != null && NamedAmmunition.Maximum(weapon) == 0) { weapon.WearOnHit(player, target); }
        return Math.Max(0, before - target.Hits);
    }
    public static void WearArmor(PlayerMobile player, BaseWeapon weapon, int damage)
    {
        double chance = Utility.RandomDouble();
        var item = chance switch
        {
            < .07 => player.NeckArmor, < .14 => player.HandArmor, < .28 => player.ArmsArmor,
            < .43 => player.HeadArmor, < .65 => player.LegsArmor, _ => player.ChestArmor
        };
        switch (item)
        {
            case BaseArmor armor: armor.OnHit(weapon, damage); break;
            case BaseClothing clothing: clothing.OnHit(weapon, damage); break;
        }
    }
}
