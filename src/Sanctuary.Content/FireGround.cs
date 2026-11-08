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
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public PhoenixNpcSource PhoenixNpc{get;init;}
    public uint NpcCaster{get;init;}
    public ulong Seed{get;init;}
    // Optional V0 extension. Historical fields retain their captured legacy
    // endpoints; no rank/modifier/seed history is reconstructed for them.
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FirestormModifiers BlazeModifiers{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FirestormModifiers FireWallModifiers{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FirestormModifiers MeteorModifiers{get;init;}
}
public sealed record FireMaker(long Id, uint Owner, int Rank, int Map, double X, double Y, int Z, double Dx, double Dy,
    int Min256, int Max256, int CellX, int CellY, DateTime UntilUtc, DateTime NextUtc)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FireWallMotion Motion{get;init;}
}
public sealed record FireCooldown(uint Owner, CombatAbility Ability, DateTime UntilUtc);
public sealed record MeteorDrop(long Id, uint Owner, int Rank, int Map, int X, int Y, int Z,
    int Min256, int Max256, double Radius, DateTime ImpactUtc)
{
    public uint NpcCaster{get;init;}
    public ulong Seed{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FirestormModifiers MeteorModifiers{get;init;}
}
public sealed record FireGroundSave(FirePatch[] Patches, FireMaker[] Makers, FireCooldown[] Cooldowns, MeteorDrop[] Meteors = null);

public sealed class FireGroundProfiles : GenericPersistence
{
    public FireGroundProfiles() : base("SanctuaryFireGround", 115) { }
    public override void Serialize(IGenericWriter writer) { writer.Write(0); writer.Write(JsonSerializer.Serialize(FireGround.Snapshot())); }
    public override void Deserialize(IGenericReader reader)
    {
        if (reader.ReadInt() != 0) { throw new InvalidDataException("Unknown fire-ground version"); }
        FireGround.Restore(JsonSerializer.Deserialize<FireGroundSave>(reader.ReadString()),true);
    }
}

public static partial class FireGround
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
    public static bool Ready(PlayerMobile owner, CombatAbility ability)=>Ready(owner,ability,TimedEffects.Now);
    internal static bool Ready(Mobile caster,CombatAbility ability,DateTime at)=>!_cooldowns.TryGetValue((caster.Serial.Value,ability),out var until)||until<=at;
    internal static void DelayForms(PlayerMobile owner)
    {
        int frames=SkillMechanics.For(CombatAbility.Werewolf).Number("globaldelay");
        if(frames!=25||SkillMechanics.For(CombatAbility.Werebear).Number("globaldelay")!=frames)
        {throw new NotSupportedException("Unmapped shared form delay");}
        var until=TimedEffects.Now.AddMilliseconds(frames*40);
        _cooldowns[(owner.Serial.Value,CombatAbility.Werewolf)]=until;
        _cooldowns[(owner.Serial.Value,CombatAbility.Werebear)]=until;
    }
    public static void Delay(PlayerMobile owner, CombatAbility ability)=>Delay(owner,ability,TimedEffects.Now);
    internal static void Delay(Mobile owner,CombatAbility ability,DateTime at)
    {
        int frames = SkillMechanics.For(ability).Number("localdelay");
        if (frames > 0) { _cooldowns[(owner.Serial.Value, ability)] = at.AddMilliseconds(frames * 40); }
    }
    public static BlazeState Blaze(Mobile owner) => SorceressEffects.Profiles.Find(owner)?.Blaze is { } state
        && state.UntilUtc > TimedEffects.Now ? state : null;
    public static int Speed(PlayerMobile owner) => Blaze(owner)?.Speed ?? 0;
    public static void CastBlaze(PlayerMobile owner, SkillValues values)
        =>Blazes.Apply(owner,Blazes.Prepare(owner,values,TimedEffects.Now));
    public static void Moved(PlayerMobile owner)
        =>Blazes.Moved(owner);
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
    internal static void AddPhoenix(PlayerMobile owner,int rank,double x,double y,int z,int minimum,int maximum,DateTime at)
    {
        long id=++_nextId;const string key="royalstrikemeteorfire";
        _patches.Add(id,new(id,owner.Serial.Value,CombatAbility.PhoenixStrike,owner.Map.MapID,x,y,z,minimum,maximum,0,
            at.AddMilliseconds(MartialPhoenixPackets.MeteorFieldFrames(rank)*40L),at.AddMilliseconds(40),NecromancerSummons.Source(key,"Size")){SkillRank=rank});
    }
    public static void CastMeteor(PlayerMobile owner, Point3D target, SkillValues values)
    {
        CommitMeteor(owner,Meteors.Prepare(owner,target,values,TimedEffects.Now));
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
        int step = Math.Max(1, NecromancerSummons.Source("meteorcenter", "sHitPar2"));
        var offsets=MeteorOffsets();
        for(int i=0;i<offsets.Length;i+=step)
        {Add(owner,CombatAbility.Meteor,meteor.X+SkillGeometry.Tiles(offsets[i].X),meteor.Y+SkillGeometry.Tiles(offsets[i].Y),meteor.Z,
            fire.Min,fire.Max,frames>0?frames:NecromancerSummons.Source("meteorfire","Range"),now);}
        Effects.SendLocationEffect(center, owner.Map, 0x36BD, 20, 10, 0x489, 0); // Placeholder impact.
    }
    public static void CastWall(PlayerMobile owner, Point3D target, SkillValues values)
        =>CommitWall(owner,FireWalls.Prepare(owner,target,values,TimedEffects.Now));
    private static void Add(PlayerMobile owner, CombatAbility ability, double x, double y, int z, int min, int max, int frames, DateTime now)
    {
        long id = ++_nextId; string key = ability switch { CombatAbility.Blaze => "blaze", CombatAbility.Meteor => "meteorfire", _ => "firewall" };
        _patches.Add(id, new(id, owner.Serial.Value, ability, owner.Map.MapID, x, y, z, min, max,
            NecromancerSummons.Source(key, "DamageRate"), now.AddMilliseconds(frames * 40), now.AddMilliseconds(40), NecromancerSummons.Source(key, "Size", 1)));
    }
    private static PlayerMobile Owner(uint serial,DiabloClass characterClass=DiabloClass.Sorceress) => World.FindMobile((Serial)serial) is PlayerMobile { Alive: true, Deleted: false } player
        && ClassCreation.Profiles.TryGet(player.Serial, out var profile) && profile.Class == characterClass ? player : null;
    private static bool SourceField(FirePatch patch)=>patch.Ability is CombatAbility.ImmolationArrow or CombatAbility.FistsOfFire or CombatAbility.PhoenixStrike
        ||patch.Ability==CombatAbility.Blaze&&patch.BlazeModifiers!=null||patch.Ability==CombatAbility.FireWall&&patch.FireWallModifiers!=null||patch.Ability==CombatAbility.Meteor&&patch.MeteorModifiers!=null;
    public static void Cancel(Mobile owner)
    {
        var ids = new List<long>();
        foreach (var (id, patch) in _patches) { if (patch.Owner == owner.Serial.Value || patch.NpcCaster == owner.Serial.Value || patch.PhoenixNpc?.Actor == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _patches.Remove(id);_loadedWallFields.Remove(id);_loadedMeteorFields.Remove(id); } ids.Clear();
        foreach (var (id, maker) in _makers) { if (maker.Owner == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _makers.Remove(id);_loadedWallMakers.Remove(id); }
        ids.Clear(); foreach (var (id, meteor) in _meteors) { if (meteor.Owner == owner.Serial.Value || meteor.NpcCaster == owner.Serial.Value) { ids.Add(id); } }
        foreach (long id in ids) { _meteors.Remove(id);_missedMeteors.Remove(id); }
    }
    private static void Tick()=>Tick(TimedEffects.Now);
    internal static void Tick(DateTime now)=>Tick(now,null);
    internal static void Tick(DateTime now,bool? allowActions,uint? onlyNpcCaster=null)
    { _keys.Clear();foreach(var meteor in _meteors.Values){if(!onlyNpcCaster.HasValue||meteor.NpcCaster==onlyNpcCaster.Value){_keys.Add(meteor.Id);}}
        foreach (long id in _keys)
        {
            var meteor = _meteors[id];
            if(meteor.MeteorModifiers!=null){continue;}
            if(meteor.NpcCaster!=0)
            {
                if(now<meteor.ImpactUtc){continue;}_meteors.Remove(id);
                if(NpcSource(meteor.NpcCaster,meteor.Owner,meteor.Map,allowActions) is{} actor){NpcMeteorImpact(actor,meteor,meteor.ImpactUtc);}
                continue;
            }
            var owner = Owner(meteor.Owner);
            if (owner == null || owner.Map != Map.Internal && (owner.Map.MapID != meteor.Map || SpellHelper.IsTown(owner.Location, owner)))
            { _meteors.Remove(id); continue; }
            if (now < meteor.ImpactUtc) { continue; }
            _meteors.Remove(id);
            if (owner.Map != Map.Internal) { MeteorImpact(owner, meteor, now); } // Missed offline impacts do not replay on login.
        }
        _keys.Clear(); _keys.AddRange(_makers.Keys);
        foreach (long id in _keys)
        {
            if (onlyNpcCaster.HasValue || !_makers.TryGetValue(id, out var maker)||maker.Motion!=null) { continue; }
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
            if (!_patches.TryGetValue(id, out var patch) || SourceField(patch) || patch.NpcCaster!=0 || onlyNpcCaster.HasValue) { continue; }
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
        TickSourceFields(now,allowActions,onlyNpcCaster);
    }
    private static void TickSourceFields(DateTime at,bool? allowActions,uint? onlyNpcCaster)
    {
        var queue=new PriorityQueue<(long Id,int Kind),(DateTime At,long Id)>();
        foreach(var value in _patches.Values)
        {if((SourceField(value)||value.NpcCaster!=0) && value.NextUtc<=at && (!onlyNpcCaster.HasValue||(value.PhoenixNpc?.Actor??value.NpcCaster)==onlyNpcCaster.Value)){queue.Enqueue((value.Id,0),(value.NextUtc,value.Id));}}
        if(!onlyNpcCaster.HasValue){foreach(var m in _makers.Values){if(m.Motion!=null&&m.NextUtc<=at){queue.Enqueue((m.Id,1),(m.NextUtc,m.Id));}}}
        if(!onlyNpcCaster.HasValue){foreach(var m in _meteors.Values){if(m.MeteorModifiers!=null&&m.ImpactUtc<=at){queue.Enqueue((m.Id,2),(m.ImpactUtc,m.Id));}}}
        while(queue.TryDequeue(out var due,out var priority))
        {
            if(due.Kind==2)
            {
                if(!_meteors.TryGetValue(due.Id,out var m)||m.ImpactUtc!=priority.At){continue;}
                foreach(var child in TickPreparedMeteor(m,allowActions)){if(child.NextUtc<=at){queue.Enqueue((child.Id,0),(child.NextUtc,child.Id));}}continue;
            }
            if(due.Kind==1)
            {
                if(!_makers.TryGetValue(due.Id,out var maker)||maker.NextUtc!=priority.At){continue;}
                if(TickWallMaker(maker,at,allowActions) is{} child&&child.NextUtc<=at){queue.Enqueue((child.Id,0),(child.NextUtc,child.Id));}
                if(_makers.TryGetValue(due.Id,out var updated)&&updated.NextUtc<=at){queue.Enqueue((updated.Id,1),(updated.NextUtc,updated.Id));}
                continue;
            }
            if(!_patches.TryGetValue(due.Id,out var patch)||patch.NextUtc!=priority.At){continue;}
            if(patch.BlazeModifiers!=null||patch.FireWallModifiers!=null||patch.MeteorModifiers!=null)
            {
                TickBlaze(patch,at,allowActions);
                if(_patches.TryGetValue(patch.Id,out var blazeNext)&&blazeNext.NextUtc<=at){queue.Enqueue((blazeNext.Id,0),(blazeNext.NextUtc,blazeNext.Id));}
                continue;
            }
            if(patch.PhoenixNpc!=null)
            {
                try{TickPhoenixNpc(patch,allowActions);}
                catch(NotSupportedException e){_patches[patch.Id]=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40),PhoenixNpc=patch.PhoenixNpc with{Dormant=true,Fault=e.Message[..Math.Min(e.Message.Length,512)]}};}
                if(_patches.TryGetValue(patch.Id,out var phoenixNext)&&phoenixNext.NextUtc<=at){queue.Enqueue((phoenixNext.Id,0),(phoenixNext.NextUtc,phoenixNext.Id));}
                continue;
            }
            if(patch.NpcCaster!=0)
            {
                if(patch.NextUtc>=patch.UntilUtc){_patches.Remove(patch.Id);continue;}
                var actor=NpcSource(patch.NpcCaster,patch.Owner,patch.Map,allowActions);
                if(actor==null){if(patch.UntilUtc<=at){_patches.Remove(patch.Id);}else{_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};}continue;}
                NpcMeteorField(actor,patch);
                if(_patches.TryGetValue(patch.Id,out var npcNext)&&npcNext.NextUtc<=at){queue.Enqueue((npcNext.Id,0),(npcNext.NextUtc,npcNext.Id));}
                continue;
            }
            var owner=Owner(patch.Owner,patch.Ability==CombatAbility.ImmolationArrow?DiabloClass.Amazon:DiabloClass.Assassin);
            if(owner==null || patch.NextUtc>=patch.UntilUtc || owner.Map!=Map.Internal && (owner.Map.MapID!=patch.Map || SpellHelper.IsTown(owner.Location,owner)))
            {_patches.Remove(patch.Id);continue;}
            if(patch.Ability==CombatAbility.PhoenixStrike && (owner.Map==Map.Internal || !(allowActions??owner.NetState is{Running:true})))
            {if(patch.UntilUtc<=at){_patches.Remove(patch.Id);}else{_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};}continue;}
            if(owner.Map==Map.Internal){_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};continue;}
            var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
            if(!owner.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);continue;}
            _patches[patch.Id]=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};
            if(ImmolationArrows.Recipient(owner,patch) is{} target)
            {
                int damage=WeaponMissileDamage.RollValue(patch.Min256,patch.Max256,0,WeaponMissiles.DamageRoll);
                // No GETHIT flag on the field; the separate19/128 SOFTHIT
                // draw affects source result/refresh flags, never weapon procs.
                if(patch.Ability!=CombatAbility.PhoenixStrike){ImmolationArrows.SoftHitRoll(128);}
                SkillCombat.FireFrame(owner,target,damage,patch.DamageRate);
            }
            if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0)
            {Effects.SendLocationEffect(point,owner.Map,0x398C,3,10,0x489,0);} // Identified flame placeholder.
            if(_patches.TryGetValue(patch.Id,out var next) && next.NextUtc<=at){queue.Enqueue((next.Id,0),(next.NextUtc,next.Id));}
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
    public static void Restore(FireGroundSave saved,bool loaded=false)
    {
        if (saved?.Patches == null || saved.Makers == null || saved.Cooldowns == null) { throw new InvalidDataException("Missing fire-ground state"); }
        var patches=new Dictionary<long,FirePatch>();var makers=new Dictionary<long,FireMaker>();var meteors=new Dictionary<long,MeteorDrop>();var cooldowns=new Dictionary<(uint Owner,CombatAbility Ability),DateTime>();long nextId=0;
        foreach (var input in saved.Patches)
        {
            var patch=loaded&&input?.PhoenixNpc!=null?input with{PhoenixNpc=input.PhoenixNpc with{Dormant=true}}:input;
            if(patch==null){throw new InvalidDataException("Missing fire patch");}
            if (patch.Id <= 0 || patch.Min256 < 0 || patch.Max256 < patch.Min256 || patch.Size is < 1 or > 8
                || patch.Ability is not (CombatAbility.Blaze or CombatAbility.FireWall or CombatAbility.Meteor or CombatAbility.ImmolationArrow or CombatAbility.FistsOfFire or CombatAbility.PhoenixStrike)
                || patch.Ability==CombatAbility.ImmolationArrow && (patch.SkillRank<1 || patch.Size!=1 || patch.DamageRate!=41 || patch.X*2!=Math.Floor(patch.X*2) || patch.Y*2!=Math.Floor(patch.Y*2))
                || (patch.Ability is CombatAbility.FistsOfFire or CombatAbility.PhoenixStrike) && (patch.Owner==0 || !((Serial)patch.Owner).IsMobile || patch.SkillRank is <1 or >255 || patch.Size!=2 || patch.DamageRate!=0 || patch.X*2!=Math.Floor(patch.X*2) || patch.Y*2!=Math.Floor(patch.Y*2))
                || !double.IsFinite(patch.X + patch.Y) || !Blazes.Valid(patch) || !FireWalls.Valid(patch) || !Meteors.Valid(patch) || !ValidNpc(patch) || !ValidPhoenixNpc(patch) || !patches.TryAdd(patch.Id, patch)) { throw new InvalidDataException("Invalid fire patch"); }
            nextId = Math.Max(nextId, patch.Id);
        }
        foreach (var maker in saved.Makers)
        {
            if (maker.Id <= 0 || maker.Rank <= 0 || !FireWalls.Valid(maker) || !double.IsFinite(maker.X + maker.Y + maker.Dx + maker.Dy)
                || patches.ContainsKey(maker.Id) || !makers.TryAdd(maker.Id, maker)) { throw new InvalidDataException("Invalid wall maker"); }
            nextId = Math.Max(nextId, maker.Id);
        }
        foreach (var cooldown in saved.Cooldowns)
        { if (!cooldowns.TryAdd((cooldown.Owner, cooldown.Ability), cooldown.UntilUtc)) { throw new InvalidDataException("Duplicate skill cooldown"); } }
        foreach (var meteor in saved.Meteors ?? Array.Empty<MeteorDrop>())
        {
            if (meteor.Id <= 0 || meteor.Rank <= 0 || meteor.Min256 < 0 || meteor.Max256 < meteor.Min256 || !double.IsFinite(meteor.Radius)
                || meteor.Radius <= 0 || !Meteors.Valid(meteor) || !ValidNpc(meteor) || patches.ContainsKey(meteor.Id) || makers.ContainsKey(meteor.Id) || !meteors.TryAdd(meteor.Id, meteor))
            { throw new InvalidDataException("Invalid pending meteor"); }
            nextId = Math.Max(nextId, meteor.Id);
        }
        _loadedWallMakers.RemoveWhere(id=>!makers.ContainsKey(id));_loadedWallFields.RemoveWhere(id=>!patches.ContainsKey(id));
        _loadedMeteorFields.RemoveWhere(id=>!patches.ContainsKey(id));_missedMeteors.RemoveWhere(id=>!meteors.ContainsKey(id));
        if(loaded)
        {
            _missedMeteors.Clear();foreach(var m in meteors.Values){if(m.MeteorModifiers!=null&&m.ImpactUtc<=TimedEffects.Now){_missedMeteors.Add(m.Id);}}
            _loadedMeteorFields.Clear();foreach(var p in patches.Values){if(p.MeteorModifiers!=null){_loadedMeteorFields.Add(p.Id);}}
            _loadedWallMakers.Clear();foreach(var m in makers.Values){if(m.Motion!=null){_loadedWallMakers.Add(m.Id);}}
            _loadedWallFields.Clear();foreach(var p in patches.Values){if(p.FireWallModifiers!=null){_loadedWallFields.Add(p.Id);}}
        }
        _patches.Clear();foreach(var pair in patches){_patches.Add(pair.Key,pair.Value);}
        _makers.Clear();foreach(var pair in makers){_makers.Add(pair.Key,pair.Value);}
        _meteors.Clear();foreach(var pair in meteors){_meteors.Add(pair.Key,pair.Value);}
        _cooldowns.Clear();foreach(var pair in cooldowns){_cooldowns.Add(pair.Key,pair.Value);}_nextId=nextId;
    }
}
