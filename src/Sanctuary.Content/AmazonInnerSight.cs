// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record SourceAuraTarget(string Source,bool Npc,bool NoAura,bool Attackable);
public static class SourceAuraTargets
{
    private static readonly Dictionary<string,SourceAuraTarget> _rules=new(StringComparer.OrdinalIgnoreCase);
    public static void Configure()
    {
        foreach(var row in BalanceCatalog.Read<SourceAuraTarget>("source-aura-targets"))
        {if(string.IsNullOrEmpty(row.Source) || !_rules.TryAdd(row.Source,row)){throw new InvalidDataException("Invalid source aura target");}}
    }
    internal static SourceAuraTarget Rule(string source)=>source!=null?_rules.GetValueOrDefault(source):null;
    internal static bool Eligible(BaseCreature target)
    {
        if(target is not{Alive:true,Deleted:false} || target is BaseVendor){return false;}
        // Explicit native dungeon archetypes have their own combat profiles;
        // their ordinary attackable/hostile projection is not a D2 species certificate.
        if(target is EncounterCreature){return target.CanBeDamaged();}
        var row=Rule(CreatureProfiles.Definition(target).Reference);
        return row is{Npc:false,NoAura:false,Attackable:true};
    }
}
public sealed record InnerSightState(uint Target,uint Creator,int Rank,int Reduction,DateTime UntilUtc);
public sealed class InnerSightProfiles:GenericPersistence
{
    private InnerSightState[] _snapshot;
    public InnerSightProfiles():base("SanctuaryInnerSight",148){}
    public override void Serialize(){_snapshot=AmazonInnerSight.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Inner Sight version");}AmazonInnerSight.Restore(JsonSerializer.Deserialize<InnerSightState[]>(reader.ReadString()));}
    public override void PostDeserialize()=>AmazonInnerSight.Prune();
}
public static class AmazonInnerSight
{
    private static readonly Dictionary<uint,InnerSightState> _states=new();
    private static DateTime _nextMarker;
    public static InnerSightProfiles Profiles{get;private set;}
    public static void Configure(){Profiles=new();EventSink.WorldSave+=Prune;}
    public static void Initialize()
    {
        var created=CorpseRecoveryHooks.Created;CorpseRecoveryHooks.Created=corpse=>{Clear(corpse.Owner);created?.Invoke(corpse);};
        Timer.StartTimer(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),Tick);
    }
    private static bool Alive(Mobile target)=>target is{Alive:true,Deleted:false,IsDeadBondedPet:false};
    internal static InnerSightState State(Mobile target)=>Alive(target) && _states.TryGetValue(target.Serial.Value,out var state) && state.UntilUtc>TimedEffects.Now?state:null;
    internal static int Reduction(Mobile target)=>State(target)?.Reduction??0;
    internal static bool InRadius(int dx,int dy,int radius)=>(long)dx*dx*4+(long)dy*dy*4<=(long)radius*radius;
    internal static int DefenseValue(int basis,int percent,int reduction)
    {
        long raw=(long)basis-reduction;
        // Literal UNITS_GetDefense sign branch; retain it before the existing
        // nonnegative hit-defense clamp. Modern full stat-graph parity is open.
        return (int)Math.Clamp(raw+raw*percent/(raw<=0?-100:100),0,int.MaxValue);
    }
    internal static int Defense(Mobile target,int basis,int percent)=>State(target) is{} state?DefenseValue(basis,percent,state.Reduction):ClassStats.ScaleDefense(basis,percent);
    internal static InnerSightState Replacement(InnerSightState old,uint target,uint owner,int rank,int reduction,DateTime until,DateTime at)
    {
        if(old!=null && old.UntilUtc>at)
        {if(rank<old.Rank){return null;}if(rank==old.Rank){return old with{UntilUtc=until};}}
        return new(target,owner,rank,reduction,until);
    }
    private static bool Valid(PlayerMobile owner)=>owner.Map is not null && owner.Map!=Map.Internal && SpellHelper.CheckTown(owner.Location,owner);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now)=>SkillCombat.Begin(owner,CombatAbility.InnerSight,selected,point,now,(p,_,_,_)=>Valid(p));
    public static bool Release(PlayerMobile owner)
    {
        if(!AmazonCombat.Available(owner,CombatAbility.InnerSight) || !Valid(owner)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.InnerSight);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        int radius=(int)values.Eval("aurarangecalc");DateTime at=TimedEffects.Now;var targets=new List<BaseCreature>();
        foreach(var target in owner.Map.GetMobilesInRange<BaseCreature>(owner.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            // Filter0x8583 has no optional aura LOS0x200 flag. The shared targets
            // helper adds LOS, so it cannot represent this source enumeration.
            if(InRadius(target.X-owner.X,target.Y-owner.Y,radius) && Math.Abs(target.Z-owner.Z)<16
                && ClassCombat.IsHostile(owner,target) && SourceAuraTargets.Eligible(target) && !SpellHelper.IsTown(target.Location,target)){targets.Add(target);}
        }
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(owner,target,values,at);}
        owner.FixedEffect(0x375A,10,10,0x481,0); // Identified cast_innersight placeholder.
        return true; // An empty or weaker-rank cast still spends its once-only mana.
    }
    internal static bool Apply(PlayerMobile owner,Mobile target,SkillValues values,DateTime at)
    {
        if(!Alive(target) || values.Rank is <1 or >255){return false;}
        var next=Replacement(_states.GetValueOrDefault(target.Serial.Value),target.Serial.Value,owner.Serial.Value,values.Rank,
            Math.Max(0,-(int)values.Eval("aurastatcalc1")),at.AddMilliseconds(values.Eval("auralencalc")*40),at);
        if(next==null){return false;}_states[target.Serial.Value]=next;Marker(target);return true;
    }
    private static void Marker(Mobile target)=>target.FixedEffect(0x375A,10,20,0x481,0); // Identified innersight mark/light placeholder.
    public static void Clear(Mobile target){if(target!=null){_states.Remove(target.Serial.Value);}}
    internal static void Prune()
    {foreach(var pair in _states.ToArray()){if(pair.Value.UntilUtc<=TimedEffects.Now || !Alive(World.FindMobile((Serial)pair.Key))){_states.Remove(pair.Key);}}}
    private static void Tick()
    {Prune();if(TimedEffects.Now<_nextMarker){return;}_nextMarker=TimedEffects.Now.AddSeconds(2);foreach(uint id in _states.Keys){if(World.FindMobile((Serial)id) is{} target){Marker(target);}}}
    public static InnerSightState[] Snapshot(){Prune();return _states.Values.OrderBy(x=>x.Target).ToArray();}
    public static void Restore(InnerSightState[] states)
    {
        if(states==null || states.Length>1000000){throw new InvalidDataException("Missing Inner Sight states");}
        _states.Clear();foreach(var state in states)
        {if(!((Serial)state.Target).IsMobile || state.Target==0 || !((Serial)state.Creator).IsMobile || state.Creator==0 || state.Rank is <1 or >255 || state.Reduction is <0 or >1000000 || !_states.TryAdd(state.Target,state)){throw new InvalidDataException("Invalid Inner Sight state");}}
    }
}
