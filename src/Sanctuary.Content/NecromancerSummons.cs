// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

public sealed record SummonSource(string Key, Dictionary<string, System.Text.Json.JsonElement> Fields);
public static class NecromancerSummons
{
    private static readonly Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>> _sources = new(StringComparer.OrdinalIgnoreCase);
    public static void Load()
    {
        _sources.Clear();
        foreach (var row in BalanceCatalog.Read<SummonSource>("necromancer-summons")) { _sources.Add(row.Key, row.Fields); }
    }
    public static int Source(string key, string field, int fallback = 0) => _sources[key].TryGetValue(field, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out int number) ? number : fallback;
    public static int Source(string key, string field, MonsterDifficulty difficulty)
        => Source(key, difficulty == MonsterDifficulty.Normal ? field
            : (field == "minHP" ? "MinHP" : field == "maxHP" ? "MaxHP" : field)
                + (difficulty == MonsterDifficulty.Nightmare ? "(N)" : "(H)"));
    internal static IReadOnlyDictionary<string,JsonElement> Fields(string key)=>_sources[key];
    public static int Formula(string key, string field, SkillValues values)
    {
        if (!_sources[key].TryGetValue(field, out var value)) { return 0; }
        return (int)values.Formula(value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : value.GetRawText());
    }
    public static int Resistance(PlayerMobile owner) => SkillMechanics.Rank(owner, CombatAbility.SummonResist) is > 0
        ? SkillMechanics.Values(owner, CombatAbility.SummonResist).Diminishing(1, 2) : 0;
    public static bool Create(PlayerMobile owner, CombatAbility ability, Serial selected, Point3D point)
    {
        if(ability is not(CombatAbility.ClayGolem or CombatAbility.BloodGolem or CombatAbility.FireGolem or CombatAbility.RaiseSkeletalMage)){return false;}
        bool mage = ability == CombatAbility.RaiseSkeletalMage;
        var corpse = mage ? World.FindItem(selected) as Corpse : null;
        if (mage && !SkeletonPets.Usable(owner, corpse) || !owner.Map.CanSpawnMobile(point.X, point.Y, point.Z)) { return false; }
        var pet = new NecromancerMinion();
        bool committed=false;
        try
        {
            pet.Prepare(owner, ability);
            if (!pet.SetControlMaster(owner)) { pet.Delete(); return false; }
            pet.MoveToWorld(point, owner.Map);pet.IssueOrder(OrderType.Guard,null);
            PersistentMinions.Profiles.Track(pet);
            // Complete candidate setup before replacing a paid existing pet.
            pet.FixedParticles(0x376A,9,20,5024,EffectLayer.Waist);
            if(!mage){NecromancerGolems.Commit(owner,pet);}
            else
            {
                var replaced=(owner.AllFollowers??[]).OfType<NecromancerMinion>()
                    .Where(old=>old is{Alive:true,Deleted:false}&&old!=pet&&old.ControlMaster==owner&&old.IsMage).OrderBy(old=>old.Serial.Value).ToArray();
                int remove=Math.Max(0,replaced.Length+1-SkeletonPets.Limit(pet.Rank));
                foreach(var old in replaced.Take(remove)){old.Explode();old.Delete();}
                BattleCommand.Track(pet);
                NecromancerCombat.Consume(corpse);
            }
            committed=true;return true;
        }
        finally{if(!committed&&!pet.Deleted){pet.Delete();}}
    }
}

[SerializationGenerator(0)]
public partial class NecromancerMinion : SkeletonGuardian
{
    [SerializableField(0, setter: "private")] private CombatAbility _kind;
    [SerializableField(1, setter: "private")] private string _ranks = "{}";
    [SerializableField(2, setter: "private")] private int _element;
    [SerializableField(3, setter: "private")] private int _resistance;
    private MonsterProfile _summonProfile;
    private Dictionary<string, int> _snapshot;
    private long _nextAura = Core.TickCount;
    public int SummonMagicResistance => Kind == CombatAbility.None ? 0 : NecromancerSummons.Source(SkillMechanics.For(Kind).Text("summon"), "ResMa", SummonDifficulty);
    public int FireAbsorb => Kind == CombatAbility.FireGolem ? Values(Kind).Diminishing(1, 2) : 0;
    public int VelocityPercent => Kind==CombatAbility.None?0:Passive("velocitypercent");
    public bool IsMage => Kind == CombatAbility.RaiseSkeletalMage;
    private Dictionary<string, int> Snapshot => _snapshot ??= JsonSerializer.Deserialize<Dictionary<string, int>>(_ranks);
    private SkillValues Values(CombatAbility ability) => new(SkillMechanics.For(ability),
        key => Snapshot.GetValueOrDefault(key), key => Snapshot.GetValueOrDefault("base:" + key), Level);
    private int Passive(string stat)
    {
        var source = SkillMechanics.For(Kind);
        for (int i = 1; i <= 5; i++)
        {
            if (source.Text("passivestat" + i) == stat) { return (int)Values(Kind).Eval("passivecalc" + i); }
        }
        return 0;
    }
    public override (int Min,int Max) SkillWeaponDamage
    {
        get
        {
            if (Kind==CombatAbility.None) { return base.SkillWeaponDamage; }
            if (IsMage) { return (CombatProfile.MinDamage,CombatProfile.MaxDamage); }
            string key=SkillMechanics.For(Kind).Text("summon"); int percent=Math.Max(10,100+Passive("damagepercent")+CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this));
            int min=Math.Max(1,NecromancerSummons.Source(key,"A1MinD",SummonDifficulty)*percent/100);
            return (min,Math.Max(min,NecromancerSummons.Source(key,"A1MaxD",SummonDifficulty)*percent/100));
        }
    }
    internal override (int Min,int Max,int Percent) SourceWeaponBase
    {
        get
        {
            if(Kind==CombatAbility.None){return base.SourceWeaponBase;}
            if(IsMage){throw new NotSupportedException("Mage missiles do not use an equipment weapon packet");}
            string key=SkillMechanics.For(Kind).Text("summon");
            return (NecromancerSummons.Source(key,"A1MinD",SummonDifficulty),NecromancerSummons.Source(key,"A1MaxD",SummonDifficulty),Passive("damagepercent"));
        }
    }
    public override MonsterProfile CombatProfile => _summonProfile ??= BuildProfile();
    public override long SourceLife256
    {
        get
        {
            if(Kind==CombatAbility.None){return base.SourceLife256;}
            long basis=(long)(NecromancerSummons.Source(SkillMechanics.For(Kind).Text("summon"),"minHP",SummonDifficulty)+(IsMage?Mastery*8:0))*256;
            return basis+basis*(long)Values(Kind).Eval("calc1")/100;
        }
    }
    [Constructible] public NecromancerMinion() { }
    public override bool Permitted(PlayerMobile owner)
    {
        foreach (var history in ClassCreation.Profiles.Histories(owner.Serial))
        {
            if (history.Profile.Class == DiabloClass.Necromancer)
            { return history.Ranks.GetValueOrDefault(SkillMechanics.For(Kind).TalentId) > 0; }
        }
        return false;
    }
    public void Prepare(PlayerMobile owner, CombatAbility ability)
    {
        _kind = ability; _summonProfile = null; _snapshot = new(StringComparer.OrdinalIgnoreCase);
        foreach (var row in SkillMechanics.All)
        {
            if (row.CharacterClass != DiabloClass.Necromancer) { continue; }
            _snapshot[row.SourceKey] = NamedSkills.Rank(owner, row.TalentId);
            _snapshot["base:" + row.SourceKey] = ClassCreation.Profiles.GetRank(owner.Serial, row.TalentId);
        }
        _ranks = JsonSerializer.Serialize(_snapshot); _element = Utility.Random(4); _resistance = NecromancerSummons.Resistance(owner);
        Initialize(owner, SkillMechanics.Rank(owner, ability), NamedSkills.Rank(owner, "necromancer.skeletonmastery"));
        Appearance(); _summonProfile = null; SetHits(CombatProfile.Life); SetDamage(CombatProfile.MinDamage, CombatProfile.MaxDamage);
        RefreshResistances();
    }
    private void RefreshResistances()
    {
        if (Kind == CombatAbility.None) { return; }
        string key = SkillMechanics.For(Kind).Text("summon");
        SetResistance(ResistanceType.Physical, NecromancerSummons.Source(key, "ResDm", SummonDifficulty));
        SetResistance(ResistanceType.Fire, NecromancerSummons.Source(key, "ResFi", SummonDifficulty) - FireAbsorb + _resistance);
        SetResistance(ResistanceType.Cold, NecromancerSummons.Source(key, "ResCo", SummonDifficulty) + _resistance);
        SetResistance(ResistanceType.Energy, NecromancerSummons.Source(key, "ResLi", SummonDifficulty) + _resistance);
        SetResistance(ResistanceType.Poison, NecromancerSummons.Source(key, "ResPo", SummonDifficulty) + _resistance);
    }
    internal override void RefreshDifficulty()
    {
        _summonProfile = null; base.RefreshDifficulty(); RefreshResistances();
    }
    private void Appearance()
    {
        // Existing UO bodies deliberately serve as placeholders for D2 summons.
        Name = IsMage ? "a skeletal mage" : Kind == CombatAbility.ClayGolem ? "a clay golem"
            : Kind == CombatAbility.BloodGolem ? "a blood golem" : "a fire golem";
        Body = IsMage ? 56 : 14; Hue = Kind switch { CombatAbility.ClayGolem => 0x96D, CombatAbility.BloodGolem => 0x21, CombatAbility.FireGolem => 0x489, _ => 0 };
        RangeFight = IsMage ? 10 : 1;
    }
    private MonsterProfile BuildProfile()
    {
        if (Kind == CombatAbility.None) { return SkeletonStats.For(Level, Rank, Mastery, difficulty: SummonDifficulty); }
        string key = SkillMechanics.For(Kind).Text("summon"); var values = Values(Kind);
        int life = NecromancerSummons.Source(key, "minHP", SummonDifficulty), min = NecromancerSummons.Source(key, "A1MinD", SummonDifficulty), max = NecromancerSummons.Source(key, "A1MaxD", SummonDifficulty);
        int defense = NecromancerSummons.Source(key, "AC", SummonDifficulty), attack = NecromancerSummons.Source(key, "A1TH", SummonDifficulty);
        if (IsMage)
        {
            life = (life + Mastery * 8) * (100 + (int)values.Eval("calc1")) / 100;
            defense += (Rank + Mastery) * values.Param(5);
        }
        else
        {
            life = life * (100 + (int)values.Eval("calc1")) / 100;
            attack += Passive("tohit"); defense += Passive("armorclass");
            int damageBonus = Passive("damagepercent");
            min = min * (100 + damageBonus) / 100; max = max * (100 + damageBonus) / 100;
        }
        int petLevel = Math.Clamp(Rank + 3 * Level / 4, 1, Level);
        attack += NecromancerSummons.Source("level-" + petLevel, "L-TH", SummonDifficulty);
        defense += NecromancerSummons.Source("level-" + petLevel, "L-AC", SummonDifficulty);
        int movement = 720 * 8 / Math.Max(1, NecromancerSummons.Source(key, "Velocity", 8));
        movement = movement * 100 / (100 + Passive("velocitypercent"));
        return new(petLevel, Math.Max(1, life), attack, defense, Math.Max(1, min), Math.Max(min, max), IsMage ? 1000 : 720, movement, 0, 0, 0, 0, 0);
    }
    protected override bool CheckAttackHit(Mobile target) => IsMage || base.CheckAttackHit(target);
    protected override void Strike(Mobile target)
    {
        if (IsMage)
        {
            int level = Math.Max(1, Mastery + (Rank < 4 ? 0 : (Rank - 2) / 2));
            string missile = "necromage" + (_element + 1);
            int min = SkillValues.Tier(NecromancerSummons.Source(missile, "EMin"), level, i => NecromancerSummons.Source(missile, "MinELev" + i));
            int max = SkillValues.Tier(NecromancerSummons.Source(missile, "EMax"), level, i => NecromancerSummons.Source(missile, "MaxELev" + i));
            double damage = Utility.RandomMinMax(min, max);
            MovingEffect(target, 0x36E4, 5, 0, false, false);
            if(ClassAvoidance.Missile(target)){return;}
            if (_element == 0)
            { int frames = NecromancerSummons.Source(missile, "ELen"); GearEffects.ApplyPoison(this, target, damage * frames / 256, Core.TickCount, frames * 40); }
            else
            {
                SkillCombat.Hit(this, target, damage, _element switch { 1 => "cold", 2 => "fire", _ => "ltng" });
                if (_element == 1) { GearEffects.ApplyChill(target, Core.TickCount, (25 * level) * 40, this); }
            }
            return;
        }
        var hit = NpcEquipmentDamage.TryResolve(this,target,out var equippedHit)?equippedHit
            :SkillCombat.ResolveHitParts(this, target, new(Utility.RandomMinMax(SkillWeaponDamage.Min, SkillWeaponDamage.Max), 0, 0, 0, 0), true, true);
        int loss = hit.LifeLost;
        var values = Values(Kind);
        if (Kind == CombatAbility.ClayGolem && loss > 0) { TimedEffects.Slow(target, values.Diminishing(3, 4), TimedEffects.Now); }
        if (Kind == CombatAbility.BloodGolem) { HealFromBloodHit(target, hit.Physical, hit.LifeBefore); }
        if (Kind == CombatAbility.FireGolem)
        { SkillCombat.Hit(this, target, values.Damage(false) + (values.Damage(true) - values.Damage(false)) * Utility.RandomDouble(), "fire"); }
    }
    public static int BloodHealingBudget(int physical, int lifeBefore, int drain, int conversion)
        => (int)Math.Clamp(Math.Min((long)Math.Max(0, physical) * Math.Clamp(drain, 0, 100) / 100,
            Math.Max(0, lifeBefore)) * Math.Max(0, conversion) / 100, 0, int.MaxValue);
    public void HealFromBloodHit(Mobile target, int physical, int lifeBefore = int.MaxValue)
    {
        if (Kind != CombatAbility.BloodGolem || !Alive || Deleted || physical <= 0
            || target is not BaseCreature creature || ControlMaster is not { Alive: true, Deleted: false } owner) { return; }
        var values = Values(Kind);
        int healing = BloodHealingBudget(physical, lifeBefore, GemCombat.Drain(creature), (int)values.Eval("calc2"));
        // EventFunc23 allocates the owner's share first, then redirects either recipient's unused healing.
        int ownerShare = (int)((long)healing * values.Param(3) / 100);
        int remaining = healing - Heal(owner, ownerShare);
        remaining -= Heal(this, remaining);
        Heal(owner, remaining);
    }
    private static int Heal(Mobile recipient, int amount)
    {
        int accepted = Math.Min(Math.Max(0, recipient.HitsMax - recipient.Hits), amount);
        recipient.Hits += accepted; return accepted;
    }
    public override void OnThink()
    {
        base.OnThink();
        if (Kind != CombatAbility.FireGolem || Deleted || !Alive || Map == Map.Internal || ControlMaster is not PlayerMobile owner
            || Core.TickCount - _nextAura < 0) { return; }
        _nextAura = Core.TickCount + 2000;
        var fire = Values(Kind); int rank = Math.Min(30, fire.Linear(5, 6));
        var aura = new SkillValues(SkillMechanics.Source("Holy Fire"), key => key == "Holy Fire" ? rank : 0, _ => 0, Level);
        double radius = SkillGeometry.Tiles(aura.Linear(1, 2));
        foreach (var target in SkillCombat.Targets(owner, Location, radius))
        {
            double scale = 1 + Math.Max(0, 1 - this.GetDistanceToSqrt(target) / Math.Max(.5, radius));
            SkillCombat.Hit(this, target, (aura.Damage(false) + (aura.Damage(true) - aura.Damage(false)) * Utility.RandomDouble()) * scale, "fire");
        }
    }
    public void Explode()
    {
        if (Kind == CombatAbility.FireGolem && Alive && !Deleted && ControlMaster is PlayerMobile owner && Map != Map.Internal)
        {
            var values = Values(Kind);
            foreach (var target in SkillCombat.Targets(owner, Location, SkillGeometry.Tiles(values.Eval("calc4"))))
            { SkillCombat.HitParts(this, target, new(values.Eval("calc2"), values.Eval("calc3"), 0, 0, 0), true); }
        }
    }
    public override bool OnBeforeDeath()
    {
        Explode();
        return base.OnBeforeDeath();
    }
    [AfterDeserialization] private void AfterSummonLoad()
    {
        _snapshot = null; _summonProfile = null; _nextAura = Core.TickCount + 2000; Appearance();
        int life = Hits; SetHits(CombatProfile.Life); Hits = Math.Min(life, HitsMax);
        SetDamage(CombatProfile.MinDamage, CombatProfile.MaxDamage);
    }
}
