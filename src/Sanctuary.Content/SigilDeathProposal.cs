// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record SigilDeathMark(uint Target,uint Creator,int Map,int Rank,DateTime UntilUtc,FirestormModifiers Modifiers,ulong Seed);
internal sealed record SigilDeathData(string ReferenceVersion,int Skill,int Explosion,Dictionary<string,JsonElement> Fields);
internal readonly record struct SigilDeathPacket(int Physical256,int Fire256);
internal readonly record struct SigilDeathBurst(uint Target,bool Exploded,int Recipients,SigilDeathPacket Packet,string Fault=null);

// Complete prepared Sanctuary candidate, not recovered event33/34 or do153.
// No production subscriptions, learned admission or legacy event-ID extension.
internal static class SigilDeathProposal
{
    internal static bool Approved=>false;
    private static ImmutableDictionary<uint,SigilDeathMark> _marks=ImmutableDictionary<uint,SigilDeathMark>.Empty;
    private sealed record Job(SigilDeathMark Mark,uint Corpse,DateTime DiedUtc);
    private static readonly Queue<Job> Jobs=new();
    private static bool _scheduled,_draining;
    private static readonly Lazy<SigilDeathData> Data=new(()=>BalanceCatalog.Read<SigilDeathData>("sigil-death").Single());
    internal static bool ValidModifiers(FirestormModifiers m)=>m!=null&&m.Potion is >=0 and <=1000000
        &&new[]{m.FireMastery,m.ColdMastery,m.LightningMastery,m.PoisonMastery}.All(n=>n is >=-100 and <=1000000);
    private static void Route()
    {
        var s=SkillMechanics.For(CombatAbility.SigilDeath);var d=Data.Value;int N(string key)=>d.Fields[key].GetInt32();
        if(s.SourceId!=400||s.Number("srvdofunc")!=157||s.Number("auralencalc")!=12||s.Number("aurastatcalc1")!=100
            ||s.Text("auratargetstate")!="sigildeath"||s.Number("auraeventfunc1")!=34||s.Number("auraeventfunc2")!=34||s.Number("auraeventfunc3")!=33
            ||s.Text("sumskill1")!="Sigil Death Explosion"||d.ReferenceVersion!="3.3.93847"||d.Skill!=400||d.Explosion!=403
            ||N("srvdofunc")!=153||N("TargetCorpse")!=1||N("ResultFlags")!=32||N("Param1")!=70||N("Param2")!=120||N("Param3")!=8||d.Fields.ContainsKey("Param4")&&N("Param4")!=0||N("Param5")!=50)
        {throw new NotSupportedException("Unqualified Sigil Death source suppliers");}
    }
    internal static SigilDeathPacket Packet(int baseLife256,ref ulong seed)
    {
        Route();if(baseLife256<=0||seed==0){throw new NotSupportedException("Death explosion needs captured base life and mark stream");}
        int lo=SourcePercentage.Calculate(baseLife256,70,100),hi=SourcePercentage.Calculate(baseLife256,120,100);
        int amount=SourceSpellPacketRoll.Range(ref seed,lo,hi);
        return new(SourcePercentage.Calculate(amount,50,100),SourcePercentage.Calculate(amount,50,100));
    }
    internal static void Validate(SigilDeathMark m)
    {
        if(m==null||!((Serial)m.Target).IsMobile||!((Serial)m.Creator).IsMobile||m.Map<0||m.Map>=Map.Maps.Length
            ||Map.Maps[m.Map]==null||Map.Maps[m.Map]==Map.Internal||m.Rank is <1 or >255||m.UntilUtc.Kind!=DateTimeKind.Utc
            ||m.UntilUtc<=DateTime.UnixEpoch||m.Seed==0||!ValidModifiers(m.Modifiers))
        {throw new InvalidDataException("Invalid prepared Death mark");}
    }
    internal static SigilDeathMark State(Mobile target,DateTime? at=null)=>target!=null&&_marks.TryGetValue(target.Serial.Value,out var m)&&m.UntilUtc>(at??TimedEffects.Now)?m:null;
    internal static void Clear(Mobile target){if(target!=null){_marks=_marks.Remove(target.Serial.Value);}}
    internal static bool PulsePrepared(SigilActor actor,PlayerMobile owner,DateTime at,Func<ulong> initialSeed=null)
    {
        Route();if(actor.Ability!=CombatAbility.SigilDeath||at.Kind!=DateTimeKind.Utc||actor.DeathModifiers==null||!ValidModifiers(actor.DeathModifiers)
            ||owner is not{Alive:true,Deleted:false}||actor.Owner!=owner.Serial.Value||World.FindItem((Serial)actor.Marker)is not{Deleted:false} item
            ||owner.Map!=item.Map||item.Map.MapID!=actor.Map||item.Location!=actor.Origin||MinionSafeAreas.Contains(owner)){return false;}
        if(!SigilActors.ClaimDeathPulse(actor)){return false;}
        int radius=checked((int)SigilActors.Values(actor).Eval("aurarangecalc"));
        var targets=SkillCombat.NearbySnapshot<BaseCreature>(item.Map,item.Location,(int)Math.Ceiling(SkillGeometry.Tiles(radius)));targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        foreach(var t in targets)
        {
            var source=PaladinConversion.SourceRule(t);
            if(!ClassCombat.IsHostile(owner,t)||!SourceAuraTargets.Eligible(t)||source?.Boss==true||t is not EncounterCreature&&source==null
                ||!t.CanBeDamaged()||!AmazonInnerSight.InRadius(t.X-item.X,t.Y-item.Y,radius)||Math.Abs(t.Z-item.Z)>=16
                ||Server.Spells.SpellHelper.IsTown(t.Location,t)){continue;}
            var old=State(t,at);if(old!=null&&old.Rank>actor.Rank){continue;}
            var mark=old!=null&&old.Rank==actor.Rank?old with{UntilUtc=at.AddMilliseconds(480)}:
                new SigilDeathMark(t.Serial.Value,owner.Serial.Value,actor.Map,actor.Rank,at.AddMilliseconds(480),actor.DeathModifiers,
                    old?.Seed??initialSeed?.Invoke()??SourceRandom.InitLow(unchecked((int)Random.Shared.NextInt64(1L<<32))));
            Validate(mark);_marks=_marks.SetItem(mark.Target,mark);
            t.FixedEffect(0x375A,10,15,0x48E,0); // Identified Death recipient-mark placeholder.
        }
        return true;
    }
    // Claim while native corpse capture still has original monster identity.
    // Queue draining waits until the native death/ownership cleanup returns.
    internal static bool QueuePrepared(BaseCreature target,Container body,DateTime at,bool schedule=true)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Death notification needs UTC");}
        if(target==null||!_marks.TryGetValue(target.Serial.Value,out var mark)){return false;}
        _marks=_marks.Remove(mark.Target);
        if(mark.UntilUtc<=at||body is not Corpse corpse){return false;}
        Jobs.Enqueue(new(mark,corpse.Serial.Value,at));
        if(schedule&&!_scheduled&&!_draining){_scheduled=true;Core.LoopContext.Post(()=>DrainPrepared());}
        return true;
    }
    internal static SigilDeathBurst[] DrainPrepared(bool? actions=null)
    {
        if(_draining){return [];}_scheduled=false;_draining=true;var results=new List<SigilDeathBurst>();
        try{while(Jobs.TryDequeue(out var job)){results.Add(Explode(job,actions));}}finally{_draining=false;}
        return results.ToArray();
    }
    private static SigilDeathBurst Explode(Job job,bool? actions)
    {
        var m=job.Mark;var owner=World.FindMobile((Serial)m.Creator)as PlayerMobile;var corpse=World.FindItem((Serial)job.Corpse)as Corpse;
        SigilDeathBurst Refused(string why=null)=>new(m.Target,false,0,default,why);
        if(owner is not{Alive:true,Deleted:false}||owner.Map?.MapID!=m.Map||!(actions??owner.NetState is{Running:true})||MinionSafeAreas.Contains(owner)
            ||!CorpseSkillRules.Available(corpse)||corpse.Map!=owner.Map) {return Refused();}
        if(CorpseSkillRules.Profiles.Find(corpse)?.BaseLife is not{Base256:>0} life||life.OriginalMonsterSerial!=m.Target)
        {return Refused("Sigil Death corpse lacks original captured source base life");}
        var targets=SkillCombat.NearbySnapshot<BaseCreature>(corpse.Map,corpse.Location,4);targets.Sort((a,b)=>a.Serial.CompareTo(b.Serial));
        bool Eligible(BaseCreature t)=>SourceDamageAreas.Recipient(owner,t,corpse.Map.MapID,corpse.X*2,corpse.Y*2,corpse.Z,8,true,corpse.Location);
        var recipients=new List<BaseCreature>();
        try{foreach(var t in targets){if(Eligible(t)){TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,t);recipients.Add(t);}}}
        catch(NotSupportedException ex){return Refused(ex.Message);}
        ulong seed=m.Seed;var packet=Packet(life.Base256,ref seed);var raw=DruidMoltenBoulder.Potion(m.Modifiers,new(packet.Physical256/256.0,packet.Fire256/256.0,0,0,0));
        if(!CorpseSkillRules.Reserve(corpse)){return Refused();}
        var center=corpse.Location;var map=corpse.Map;NecromancerCombat.Consume(corpse);int applied=0;
        foreach(var t in recipients)
        {
            // Consumption can delete an empty corpse; keep the captured center.
            if(!SourceDamageAreas.Recipient(owner,t,map.MapID,center.X*2,center.Y*2,center.Z,8,true,center)){continue;}
            using(CombatItemEvents.Suppress(owner,t))
            {owner.DoHarmful(t,true);var parts=SkillCombat.MitigatePrepared(owner,t,raw);SkillHealth.Damage(owner,t,NamedElements.To256(parts.Total));}
            applied++;
        }
        Effects.SendLocationEffect(center,map,0x36BD,15); // Identified Death explosion placeholder.
        return new(m.Target,true,applied,packet);
    }
    internal static SigilDeathMark[] Snapshot()
    {
        foreach(var m in _marks.Values){if(m.UntilUtc<=TimedEffects.Now||World.FindMobile((Serial)m.Target)is not{Alive:true,Deleted:false}){_marks=_marks.Remove(m.Target);}}
        return _marks.Count==0?null:_marks.Values.OrderBy(m=>m.Target).ToArray();
    }
    internal static ImmutableDictionary<uint,SigilDeathMark> ValidateSnapshot(SigilDeathMark[] marks)
    {
        if(_draining||Jobs.Count!=0){throw new InvalidDataException("Cannot restore Sigils across pending Death jobs");}
        var next=ImmutableDictionary<uint,SigilDeathMark>.Empty;
        foreach(var m in marks??[]){Validate(m);if(next.ContainsKey(m.Target)){throw new InvalidDataException("Duplicate Death mark");}next=next.Add(m.Target,m);}return next;
    }
    internal static void RestoreValidated(ImmutableDictionary<uint,SigilDeathMark> marks)=>_marks=marks;
}
