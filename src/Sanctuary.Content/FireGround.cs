using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record BlazeState(DateTime UntilUtc, int Rank, int Speed, int Map, int X, int Y, int Z);
public sealed record FirePatch(long Id, uint Owner, CombatAbility Ability, int Map, double X, double Y, int Z,
    int Min256, int Max256, int DamageRate, DateTime UntilUtc, DateTime NextUtc, int Size = 1)
{
    public int SkillRank{get;init;}
}
public sealed record FireMaker(long Id, uint Owner, int Rank, int Map, double X, double Y, int Z, double Dx, double Dy,
    int Min256, int Max256, int CellX, int CellY, DateTime UntilUtc, DateTime NextUtc);
public sealed record FireCooldown(uint Owner, CombatAbility Ability, DateTime UntilUtc);
public sealed record MeteorDrop(long Id, uint Owner, int Rank, int Map, int X, int Y, int Z,
    int Min256, int Max256, double Radius, DateTime ImpactUtc);
public sealed record FireGroundSave(FirePatch[] Patches, FireMaker[] Makers, FireCooldown[] Cooldowns, MeteorDrop[] Meteors = null);

public sealed class FireGroundProfiles : GenericPersistence
{
    public FireGroundProfiles() : base("SanctuaryFireGround", 115) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(FireGround.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown fire-ground version"); }
        FireGround.Restore(JsonSerializer.Deserialize<FireGroundSave>(reader.ReadString()));
    }
}

public static class FireGround
{
    private static readonly Dictionary<long, FirePatch> _patches = new();
    private static readonly Dictionary<long, FireMaker> _makers = new();
    private static readonly Dictionary<long, MeteorDrop> _meteors = new();
    private static readonly Dictionary<(uint Owner, CombatAbility Ability), DateTime> _cooldowns = new();
    private static readonly List<long> _keys = new();
    private static long _nextId;
    public static FireGroundProfiles Profiles { get; private set; }
    public static void Configure() => Profiles = new();
    public static void Initialize() => Timer.StartTimer(TimeSpan.FromMilliseconds(40), TimeSpan.FromMilliseconds(40), Tick);
    public static bool Ready(PlayerMobile owner, CombatAbility ability) => !_cooldowns.TryGetValue((owner.Serial.Value, ability), out var until) || until <= TimedEffects.Now;
    public static void Delay(PlayerMobile owner, CombatAbility ability)
    {
        int frames = SkillMechanics.For(ability).Number("localdelay");
        if (frames > 0) { _cooldowns[(owner.Serial.Value, ability)] = TimedEffects.Now.AddMilliseconds(frames * 40); }
    }
    public static BlazeState Blaze(Mobile owner) => SorceressEffects.Profiles.Find(owner)?.Blaze is { } state
        && state.UntilUtc > TimedEffects.Now ? state : null;
    public static int Speed(PlayerMobile owner) => Blaze(owner)?.Speed ?? 0;
    public static void CastBlaze(PlayerMobile owner, SkillValues values)
    {
        var state = new BlazeState(TimedEffects.Now.AddMilliseconds(values.Eval("auralencalc") * 40), values.Rank,
            (int)values.Eval("passivecalc1"), owner.Map.MapID, owner.X, owner.Y, owner.Z);
        SorceressEffects.Profiles.Store(owner, (SorceressEffects.Profiles.Find(owner) ?? new()) with { Blaze = state });
        owner.FixedEffect(0x375A, 10, 10, 0x489, 0); // Placeholder Blaze aura.
    }
    public static void Moved(PlayerMobile owner)
    {
        if (Blaze(owner) is not { } blaze) { return; }
        var now = TimedEffects.Now;
        bool adjacent = owner.Map.MapID == blaze.Map && Math.Abs(owner.X - blaze.X) <= 1 && Math.Abs(owner.Y - blaze.Y) <= 1
            && Math.Abs(owner.Z - blaze.Z) < 16;
        if (adjacent && (owner.X != blaze.X || owner.Y != blaze.Y) && !SpellHelper.IsTown(owner.Location, owner))
        {
            var values = SkillMechanics.Values(owner, CombatAbility.Blaze, blaze.Rank); var damage = Damage(owner, values);
            int frames = NecromancerSummons.Formula("blaze", "Range", values);
            for (int step = 1; step <= 2; step++)
            { Add(owner, CombatAbility.Blaze, blaze.X + (owner.X - blaze.X) * step / 2.0, blaze.Y + (owner.Y - blaze.Y) * step / 2.0, owner.Z, damage.Min, damage.Max, frames, now); }
        }
        SorceressEffects.Profiles.Store(owner, SorceressEffects.Profiles.Find(owner) with
        { Blaze = blaze with { Map = owner.Map.MapID, X = owner.X, Y = owner.Y, Z = owner.Z } });
    }
    private static (int Min, int Max) Damage(PlayerMobile owner, SkillValues values)
        => Enhance(owner, values.Damage256(false), values.Damage256(true));
    private static (int Min, int Max) Enhance(PlayerMobile owner, int minimum, int maximum)
    {
        int potion = CombatPotions.Bonus(owner, "magical-penetration");
        return (Scale(minimum), Scale(maximum));
        int Scale(int value) => (int)Math.Clamp(NamedElements.Enhance(owner, value * (100 + potion) / 100.0, "fire"), 0, int.MaxValue);
    }
    internal static int MissileDamage256(string key,int rank,int synergy,bool maximum)
    {
        string field=maximum?"EMax":"EMin",growth=maximum?"MaxELev":"MinELev";
        long damage=SkillValues.Tier(NecromancerSummons.Source(key,field),rank,i=>NecromancerSummons.Source(key,growth+i));
        // Missile endpoints truncate synergy before HitShift; skill endpoints
        // have a different order. Complete source helper differentials cover it.
        damage+=damage*synergy/100;
        return (int)Math.Clamp(damage<<NecromancerSummons.Source(key,"HitShift"),0,int.MaxValue);
    }
    public static int MeteorFireDamage256(SkillValues values,bool maximum)
        =>MissileDamage256("meteorfire",values.Rank,NecromancerSummons.Formula("meteorfire","EDmgSymPerCalc",values),maximum);
    internal static void AddImmolation(PlayerMobile owner,int rank,double x,double y,int z,int minimum,int maximum,int frames,DateTime at)
    {
        long id=++_nextId;
        _patches.Add(id,new(id,owner.Serial.Value,CombatAbility.ImmolationArrow,owner.Map.MapID,x,y,z,minimum,maximum,
            NecromancerSummons.Source("immolationfire","DamageRate"),at.AddMilliseconds(frames*40L),at.AddMilliseconds(40),1){SkillRank=rank});
    }
    internal static void AddFists(PlayerMobile owner,int rank,double x,double y,int z,int minimum,int maximum,DateTime at)
    {
        long id=++_nextId;const string key="fistsoffirefirewall";
        _patches.Add(id,new(id,owner.Serial.Value,CombatAbility.FistsOfFire,owner.Map.MapID,x,y,z,minimum,maximum,
            NecromancerSummons.Source(key,"DamageRate"),at.AddMilliseconds(NecromancerSummons.Source(key,"Range")*40L),
            at.AddMilliseconds(40),NecromancerSummons.Source(key,"Size")){SkillRank=rank});
    }
    public static void CastMeteor(PlayerMobile owner, Point3D target, SkillValues values)
    {
        var damage = Damage(owner, values); long id = ++_nextId;
        _meteors.Add(id, new(id, owner.Serial.Value, values.Rank, owner.Map.MapID, target.X, target.Y, target.Z,
            damage.Min, damage.Max, SkillGeometry.Tiles(values.Eval("aurarangecalc")),
            TimedEffects.Now.AddMilliseconds(NecromancerSummons.Source("meteorcenter", "Range") * 40)));
        Effects.SendLocationEffect(target, owner.Map, 0x3709, 60, 10, 0x489, 0); // Placeholder falling-meteor marker.
    }
    private static int Roll(int minimum, int maximum) => maximum <= minimum ? minimum : Utility.RandomMinMax(minimum, maximum - 1);
    private static void MeteorImpact(PlayerMobile owner, MeteorDrop meteor, DateTime now)
    {
        var center = new Point3D(meteor.X, meteor.Y, meteor.Z);
        int impact = Roll(meteor.Min256, meteor.Max256); // One source roll shared by the area impact.
        foreach (var target in SkillCombat.Targets(owner, center, meteor.Radius)) { SkillCombat.FireImpact(owner, target, impact); }
        var values = SkillMechanics.Values(owner, CombatAbility.Meteor, meteor.Rank);
        var fire = Enhance(owner, MeteorFireDamage256(values, false), MeteorFireDamage256(values, true));
        var definition = SkillMechanics.For(CombatAbility.Meteor);
        int frames = definition.Parameter(3) + (values.Rank - 1) * definition.Parameter(4);
        int[] xs = [2, -2, 0, 0, -3, 0, 3, -1, 1, -1, 2, -4, -3, -1, 0, 1, 3, 4];
        int[] ys = [-2, -2, 2, 5, 3, 3, 3, 2, 1, -1, -1, -2, -2, -3, -4, -3, -3, -2];
        int step = Math.Max(1, NecromancerSummons.Source("meteorcenter", "sHitPar2"));
        for (int i = 0; i < xs.Length; i += step)
        { Add(owner, CombatAbility.Meteor, meteor.X + SkillGeometry.Tiles(xs[i]), meteor.Y + SkillGeometry.Tiles(ys[i]), meteor.Z,
            fire.Min, fire.Max, frames > 0 ? frames : NecromancerSummons.Source("meteorfire", "Range"), now); }
        Effects.SendLocationEffect(center, owner.Map, 0x36BD, 20, 10, 0x489, 0); // Placeholder impact.
    }
    public static void CastWall(PlayerMobile owner, Point3D target, SkillValues values)
    {
        var damage = Damage(owner, values); var now = TimedEffects.Now;
        Add(owner, CombatAbility.FireWall, target.X, target.Y, target.Z, damage.Min, damage.Max, NecromancerSummons.Source("firewall", "Range"), now);
        double dx = owner.X - target.X, dy = owner.Y - target.Y, length = Math.Sqrt(dx * dx + dy * dy);
        if (length == 0) { dx = 1; dy = 0; length = 1; }
        int frames = NecromancerSummons.Formula("firewallmaker", "Range", values);
        for (int sign = -1; sign <= 1; sign += 2)
        {
            long id = ++_nextId;
            _makers.Add(id, new(id, owner.Serial.Value, values.Rank, owner.Map.MapID, target.X, target.Y, target.Z,
                -dy / length * sign, dx / length * sign, damage.Min, damage.Max, target.X * 2, target.Y * 2,
                now.AddMilliseconds(frames * 40), now.AddMilliseconds(40)));
        }
    }
    private static void Add(PlayerMobile owner, CombatAbility ability, double x, double y, int z, int min, int max, int frames, DateTime now)
    {
        long id = ++_nextId; string key = ability switch { CombatAbility.Blaze => "blaze", CombatAbility.Meteor => "meteorfire", _ => "firewall" };
        _patches.Add(id, new(id, owner.Serial.Value, ability, owner.Map.MapID, x, y, z, min, max,
            NecromancerSummons.Source(key, "DamageRate"), now.AddMilliseconds(frames * 40), now.AddMilliseconds(40), NecromancerSummons.Source(key, "Size", 1)));
    }
    private static PlayerMobile Owner(uint serial,DiabloClass characterClass=DiabloClass.Sorceress) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == characterClass ? player : null;
    private static bool SourceField(CombatAbility ability)=>ability is CombatAbility.ImmolationArrow or CombatAbility.FistsOfFire;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>();
        foreach (var (id, patch) in _patches) { if (patch.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _patches.Remove(id); } ids.Clear();
        foreach (var (id, maker) in _makers) { if (maker.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _makers.Remove(id); }
        ids.Clear(); foreach (var (id, meteor) in _meteors) { if (meteor.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _meteors.Remove(id); }
    }
    private static void Tick()=>Tick(TimedEffects.Now);
    internal static void Tick(DateTime now)
    { _keys.Clear(); _keys.AddRange(_meteors.Keys);
        foreach (long id in _keys)
        {
            var meteor = _meteors[id]; var owner = Owner(meteor.Owner);
            if (owner == null || owner.Map != Map.Internal && (owner.Map.MapID != meteor.Map || SpellHelper.IsTown(owner.Location, owner)))
            { _meteors.Remove(id); continue; }
            if (now < meteor.ImpactUtc) { continue; }
            _meteors.Remove(id);
            if (owner.Map != Map.Internal) { MeteorImpact(owner, meteor, now); } // Missed offline impacts do not replay on login.
        }
        _keys.Clear(); _keys.AddRange(_makers.Keys);
        foreach (long id in _keys)
        {
            if (!_makers.TryGetValue(id, out var maker)) { continue; }
            var owner = Owner(maker.Owner);
            if (owner == null || maker.UntilUtc <= now || owner.Map != Map.Internal && (owner.Map.MapID != maker.Map || SpellHelper.IsTown(owner.Location, owner)))
            { _makers.Remove(id); continue; }
            if (now < maker.NextUtc || owner.Map == Map.Internal) { continue; }
            double speed = SkillGeometry.Velocity(NecromancerSummons.Source("firewallmaker", "Vel"));
            double x = maker.X + maker.Dx * speed, y = maker.Y + maker.Dy * speed;
            var from = new Point3D((int)Math.Round(maker.X), (int)Math.Round(maker.Y), maker.Z + 12);
            var to = new Point3D((int)Math.Round(x), (int)Math.Round(y), maker.Z + 12);
            if (!owner.Map.LineOfSight(from, to)) { _makers.Remove(id); continue; }
            int cx = (int)Math.Round(x * 2), cy = (int)Math.Round(y * 2);
            if (cx != maker.CellX || cy != maker.CellY)
            { Add(owner, CombatAbility.FireWall, cx / 2.0, cy / 2.0, maker.Z, maker.Min256, maker.Max256, NecromancerSummons.Source("firewall", "Range"), now); }
            _makers[id] = maker with { X = x, Y = y, CellX = cx, CellY = cy, NextUtc = now.AddMilliseconds(40) };
        }
        _keys.Clear(); _keys.AddRange(_patches.Keys);
        foreach (long id in _keys)
        {
            if (!_patches.TryGetValue(id, out var patch) || SourceField(patch.Ability)) { continue; }
            var owner = Owner(patch.Owner);
            if (owner == null || patch.UntilUtc <= now || owner.Map != Map.Internal && (owner.Map.MapID != patch.Map || SpellHelper.IsTown(owner.Location, owner)))
            { _patches.Remove(id); continue; }
            if (now < patch.NextUtc || owner.Map == Map.Internal) { continue; }
            _patches[id] = patch with { NextUtc = now.AddMilliseconds(40) };
            var point = new Point3D((int)Math.Round(patch.X), (int)Math.Round(patch.Y), patch.Z);
            foreach (var target in SkillCombat.Targets(owner, point, 2))
            {
                double dx = target.X - patch.X, dy = target.Y - patch.Y;
                double radius = 0.5 + SkillGeometry.Tiles(patch.Size) / 2; // Provisional player/projectile footprint.
                if (dx * dx + dy * dy <= radius * radius)
                { SkillCombat.FireFrame(owner, target, Roll(patch.Min256, patch.Max256), patch.DamageRate); }
            }
            if ((now.Ticks / TimeSpan.TicksPerMillisecond / 40) % 5 == 0)
            { Effects.SendLocationEffect(point, owner.Map, 0x398C, 5, 10, 0x489, 0); } // Placeholder ground flame.
        }
        TickSourceFields(now);
    }
    private static void TickSourceFields(DateTime at)
    {
        var queue=new PriorityQueue<FirePatch,(DateTime,long)>();
        foreach(var value in _patches.Values)
        {if(SourceField(value.Ability) && value.NextUtc<=at){queue.Enqueue(value,(value.NextUtc,value.Id));}}
        while(queue.TryDequeue(out var due,out _))
        {
            if(!_patches.TryGetValue(due.Id,out var patch) || patch.NextUtc!=due.NextUtc){continue;}
            var owner=Owner(patch.Owner,patch.Ability==CombatAbility.FistsOfFire?DiabloClass.Assassin:DiabloClass.Amazon);
            if(owner==null || patch.NextUtc>=patch.UntilUtc || owner.Map!=Map.Internal && (owner.Map.MapID!=patch.Map || SpellHelper.IsTown(owner.Location,owner)))
            {_patches.Remove(patch.Id);continue;}
            if(owner.Map==Map.Internal){_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};continue;}
            var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
            if(!owner.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);continue;}
            _patches[patch.Id]=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};
            if(ImmolationArrows.Recipient(owner,patch) is{} target)
            {
                int damage=WeaponMissileDamage.RollValue(patch.Min256,patch.Max256,0,WeaponMissiles.DamageRoll);
                // No GETHIT flag on the field; the separate19/128 SOFTHIT
                // draw affects source result/refresh flags, never weapon procs.
                ImmolationArrows.SoftHitRoll(128);
                SkillCombat.FireFrame(owner,target,damage,patch.DamageRate);
            }
            if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0)
            {Effects.SendLocationEffect(point,owner.Map,0x398C,3,10,0x489,0);} // Identified flame placeholder.
            if(_patches.TryGetValue(patch.Id,out var next) && next.NextUtc<=at){queue.Enqueue(next,(next.NextUtc,next.Id));}
        }
    }
    public static FireGroundSave Snapshot() => new(new List<FirePatch>(_patches.Values).ToArray(), new List<FireMaker>(_makers.Values).ToArray(), Cooldowns(),
        new List<MeteorDrop>(_meteors.Values).ToArray());
    private static FireCooldown[] Cooldowns()
    {
        var rows = new List<FireCooldown>();
        foreach (var (key, until) in _cooldowns) { if (until > TimedEffects.Now) { rows.Add(new(key.Owner, key.Ability, until)); } }
        return rows.ToArray();
    }
    public static void Restore(FireGroundSave saved)
    {
        if (saved?.Patches == null || saved.Makers == null || saved.Cooldowns == null) { throw new InvalidDataException("Missing fire-ground state"); }
        _patches.Clear(); _makers.Clear(); _meteors.Clear(); _cooldowns.Clear(); _nextId = 0;
        foreach (var patch in saved.Patches)
        {
            if (patch.Id <= 0 || patch.Min256 < 0 || patch.Max256 < patch.Min256 || patch.Size is < 1 or > 8
                || patch.Ability is not (CombatAbility.Blaze or CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.ImmolationArrow or CombatAbility.FistsOfFire)
                || patch.Ability==CombatAbility.ImmolationArrow && (patch.SkillRank<1 || patch.Size!=1 || patch.DamageRate!=41 || patch.X*2!=Math.Floor(patch.X*2) || patch.Y*2!=Math.Floor(patch.Y*2))
                || patch.Ability==CombatAbility.FistsOfFire && (patch.Owner==0 || !((Serial)patch.Owner).IsMobile || patch.SkillRank is <1 or >255 || patch.Size!=2 || patch.DamageRate!=0 || patch.X*2!=Math.Floor(patch.X*2) || patch.Y*2!=Math.Floor(patch.Y*2))
                || !double.IsFinite(patch.X + patch.Y) || !_patches.TryAdd(patch.Id, patch)) { throw new InvalidDataException("Invalid fire patch"); }
            _nextId = Math.Max(_nextId, patch.Id);
        }
        foreach (var maker in saved.Makers)
        {
            if (maker.Id <= 0 || maker.Rank <= 0 || !double.IsFinite(maker.X + maker.Y + maker.Dx + maker.Dy)
                || _patches.ContainsKey(maker.Id) || !_makers.TryAdd(maker.Id, maker)) { throw new InvalidDataException("Invalid wall maker"); }
            _nextId = Math.Max(_nextId, maker.Id);
        }
        foreach (var cooldown in saved.Cooldowns)
        { if (!_cooldowns.TryAdd((cooldown.Owner, cooldown.Ability), cooldown.UntilUtc)) { throw new InvalidDataException("Duplicate skill cooldown"); } }
        foreach (var meteor in saved.Meteors ?? Array.Empty<MeteorDrop>())
        {
            if (meteor.Id <= 0 || meteor.Rank <= 0 || meteor.Min256 < 0 || meteor.Max256 < meteor.Min256 || !double.IsFinite(meteor.Radius)
                || meteor.Radius <= 0 || _patches.ContainsKey(meteor.Id) || _makers.ContainsKey(meteor.Id) || !_meteors.TryAdd(meteor.Id, meteor))
            { throw new InvalidDataException("Invalid pending meteor"); }
            _nextId = Math.Max(_nextId, meteor.Id);
        }
    }
}
