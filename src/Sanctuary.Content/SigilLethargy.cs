// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record LethargyList(int Rank,uint Creator,int Expiry,int Attack,int Defense,int Velocity,uint Flags=2);
internal sealed record LethargyOperation(bool Accepted,LethargyList List,int Removed,int Events,int Updates,int[] Writes);
internal static class SigilLethargyOperation
{
    internal static LethargyOperation Apply(int rank,uint creator,int frame,LethargyList old)
    {
        if(rank is <1 or >255||frame<0||frame>int.MaxValue-12||old!=null&&old.Expiry<=frame)
        {throw new NotSupportedException("Unqualified Lethargy rank/frame/old-state context");}
        if(old!=null&&rank<old.Rank){return new(false,old,0,0,0,[]);}
        bool retained=old!=null&&old.Rank==rank;int defense=-33-(rank-1);
        var list=retained?old with{Expiry=frame+12,Defense=defense,Velocity=-50}:new(rank,creator,frame+12,-50,defense,-50);
        int[] writes=retained?[16,defense,67,-50]:[68,-50,16,defense,67,-50];
        return new(true,list,old!=null&&!retained?1:0,1,1,writes);
    }
}
internal sealed record LethargyStatus(uint Target,uint Creator,int Rank,DateTime UntilUtc);
public sealed class SigilLethargyProfiles:GenericPersistence
{
    private LethargyStatus[] _snapshot;
    public SigilLethargyProfiles():base("SanctuarySigilLethargy",245){}
    public override void Serialize(){_snapshot=SigilLethargy.Snapshot();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(_snapshot));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Lethargy status version");}SigilLethargy.Restore(JsonSerializer.Deserialize<LethargyStatus[]>(reader.ReadString()));}
    public override void PostDeserialize()=>SigilLethargy.Prune();
}

// State192is NOT in the curse mask. Keep its list independent of ordinary
// curses/Attract,Fade curse resistance and Cleansing's curse-shortening pool.
internal static class SigilLethargy
{
    private static ImmutableDictionary<uint,LethargyStatus> _states=ImmutableDictionary<uint,LethargyStatus>.Empty;
    public static void Configure(){_=new SigilLethargyProfiles();SigilActors.Pulse+=Pulse;EventSink.WorldSave+=Prune;}
    internal static LethargyStatus State(Mobile target,DateTime? at=null)=>target is{Alive:true,Deleted:false}
        &&_states.TryGetValue(target.Serial.Value,out var state)&&state.UntilUtc>(at??TimedEffects.Now)?state:null;
    internal static int Attack(Mobile target)=>State(target)==null?0:-50;
    internal static int Velocity(Mobile target)=>State(target)==null?0:-50;
    internal static int Defense(Mobile target)=>State(target)is{} state?-33-(state.Rank-1):0;
    internal static double? MovementDelay(Mobile target,double seconds)
    {
        if(State(target)==null){return null;}
        int existing=target is NecromancerMinion minion?minion.VelocityPercent:0;
        int running=SourceTerror.State(target)is{Goal:not null,Combat:false} ai?ai.VelocityPercent:0;
        int rate=Math.Max(25,100+existing+running+PaladinVigor.VelocityPercent(target)+ReviveCombat.VelocityPercent(target)-GearEffects.MovementSlowTotal(target));
        return seconds*(100+existing)/rate;
    }
    internal static bool Apply(PlayerMobile owner,Item marker,BaseCreature target,int rank,DateTime at)
    {
        if(owner is not{Alive:true,Deleted:false}||marker is not{Deleted:false}||target is not{Alive:true,Deleted:false}
            ||owner.Map==null||owner.Map!=marker.Map||target.Map!=marker.Map||!ClassCombat.IsHostile(owner,target)
            ||!NecromancerCurseCasts.Eligible(target)||rank is <1 or >255||at.Kind!=DateTimeKind.Utc){return false;}
        var row=SkillMechanics.For(CombatAbility.SigilLethargy);
        if(row.SourceId!=393||row.Text("auratargetstate")!="sigillethargy"||row.Number("aurafilter")!=3||row.Number("auralencalc")!=12
            ||row.Text("aurastat1")!="attackrate"||row.Text("aurastat2")!="item_armor_percent"||row.Text("aurastat3")!="velocitypercent"
            ||PaladinCleansing.IsCurse("sigillethargy")){throw new NotSupportedException("Unqualified Lethargy state/packet");}
        var old=State(target,at);
        var result=SigilLethargyOperation.Apply(rank,marker.Serial.Value,0,old==null?null:new(old.Rank,old.Creator,
            checked((int)Math.Ceiling((old.UntilUtc-at).TotalMilliseconds/40)),-50,-33-(old.Rank-1),-50));
        if(!result.Accepted){return false;}
        _states=_states.SetItem(target.Serial.Value,new(target.Serial.Value,result.List.Creator,result.List.Rank,at.AddMilliseconds(480)));
        target.Delta(MobileDelta.Stat);owner.DoHarmful(target,true);
        target.FixedEffect(0x375A,10,15,0x48D,0); // Identified Lethargy recipient-hit placeholder.
        return true;
    }
    private static void Pulse(SigilActor actor,PlayerMobile owner,DateTime at)
    {
        if(actor.Ability!=CombatAbility.SigilLethargy||World.FindItem((Serial)actor.Marker)is not{Deleted:false} marker){return;}
        var values=SigilActors.Values(actor);int radius=checked((int)values.Eval("aurarangecalc"));
        var targets=new List<BaseCreature>();
        foreach(var target in marker.Map.GetMobilesInRange<BaseCreature>(marker.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius))))
        {
            if(AmazonInnerSight.InRadius(target.X-marker.X,target.Y-marker.Y,radius)&&Math.Abs(target.Z-marker.Z)<16){targets.Add(target);}
        }
        targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));foreach(var target in targets){Apply(owner,marker,target,actor.Rank,at);}
    }
    internal static void Clear(Mobile target){if(target!=null){_states=_states.Remove(target.Serial.Value);}}
    internal static void Prune()
    {foreach(var s in _states.Values){if(State(World.FindMobile((Serial)s.Target))==null){_states=_states.Remove(s.Target);}}}
    internal static LethargyStatus[] Snapshot()=>_states.Values.OrderBy(s=>s.Target).ToArray();
    internal static void Restore(LethargyStatus[] states)
    {
        var next=ImmutableDictionary<uint,LethargyStatus>.Empty;
        foreach(var s in states??throw new InvalidDataException("Missing Lethargy statuses"))
        {if(s==null||!((Serial)s.Target).IsMobile||!((Serial)s.Creator).IsItem||s.Rank is <1 or >255||s.UntilUtc.Kind!=DateTimeKind.Utc||s.UntilUtc<=DateTime.UnixEpoch||next.ContainsKey(s.Target)){throw new InvalidDataException("Invalid Lethargy status");}next=next.Add(s.Target,s);}
        _states=next;
    }
}
