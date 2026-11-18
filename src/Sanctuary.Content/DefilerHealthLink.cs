// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record DefilerLinkMark(uint Target,int Rank,int SelfRank,int Percent,DateTime UntilUtc);
internal sealed record DefilerLinkGroup(uint Owner,DefilerLinkMark[] Marks)
{
    public string Profile{get;init;}="sanctuary-health-link-v1";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string Fault{get;init;}
}

// Approved Sanctuary rule,not an inferred implementation of protected do162.
// Groups live on their original body;the index and delivery queue are transient.
internal static class DefilerHealthLink
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerHealthLink));
    private static readonly HashSet<WarlockDefilerBody> Bodies=new();
    private sealed record Entry(WarlockDefilerBody Body,DefilerLinkMark Mark);
    private sealed record Pending(WarlockDefilerBody Body,DefilerLinkMark Mark,DirectDamageParts Parts);
    private static readonly Dictionary<uint,Entry> Index=new();
    private record struct MarkVisual(DateTime Until,DateTime Renew,Map Map);
    private static readonly Dictionary<uint,MarkVisual> MarkVisuals=new();
    private static DateTime _presentationAt;
    private static readonly Queue<Pending> Queue=new();
    private static bool Delivering;
    public static void Initialize()=>SkillDamageEvents.BeforeDirectDamage+=Damage;
    internal static void Validate(DefilerLinkGroup group,uint owner)
    {
        if(group==null){return;}
        if(group.Owner!=owner||group.Profile!="sanctuary-health-link-v1"||group.Marks==null||group.Marks.Length is<1 or>5
            ||group.Marks.Select(m=>m?.Target).Distinct().Count()!=group.Marks.Length)
        {throw new InvalidDataException("Malformed Defiler link group");}
        foreach(var m in group.Marks)
        {
            if(m==null||!((Serial)m.Target).IsMobile||m.Rank is<1 or>99||m.SelfRank is<1 or>99||m.Percent is<1 or>60
                ||m.UntilUtc.Kind!=DateTimeKind.Utc||m.UntilUtc==DateTime.MinValue
                ||m.Percent!=Math.Min(60,DefilerLinkNumbers.Capture(m.Rank,m.SelfRank).RawSharedPercent))
            {throw new InvalidDataException("Malformed Defiler link mark");}
        }
    }
    internal static void Track(WarlockDefilerBody b){if(b?.Prepared?.Links!=null){Bodies.Add(b);}}
    internal static void Forget(WarlockDefilerBody b)
    {
        if(b==null){return;}Bodies.Remove(b);
        foreach(var id in Index.Where(p=>p.Value.Body==b).Select(p=>p.Key).ToArray()){Index.Remove(id);}
    }
    private static PlayerMobile Owner(WarlockDefilerBody b)=>b?.ControlMaster as PlayerMobile;
    private static bool Active(WarlockDefilerBody b)=>b is{Alive:true,Deleted:false}&&Owner(b)is{Alive:true,Deleted:false,NetState:{Running:true}} p
        &&b.Prepared?.Owner==p.Serial.Value&&b.Permitted(p)&&PersistentMinions.Active(p,b)&&b.Map==p.Map&&b.Map!=null&&b.Map!=Map.Internal
        &&!MinionSafeAreas.Contains(p)&&!SpellHelper.IsTown(b.Location,p);
    private static bool Recipient(WarlockDefilerBody b,BaseCreature target,bool inRange)
    {
        var p=Owner(b);
        if(!Active(b)||target is not{Alive:true,Deleted:false}||target.Map!=b.Map
            ||!target.CanBeDamaged()||target.Blessed||!ClassCombat.IsHostile(p,target)||!b.CanBeHarmful(target,false)
            ||SpellHelper.IsTown(target.Location,p)){return false;}
        if(!inRange){return true;}
        int dx=target.X*2-b.X*2,dy=target.Y*2-b.Y*2;
        return Math.Abs(target.Z-b.Z)<16&&dx*dx+dy*dy<=625&&b.CanSee(target)&&b.InLOS(target);
    }
    private static void Store(WarlockDefilerBody b,DefilerLinkGroup group)
    {b.RestorePrepared(b.Prepared with{Links=group});if(group==null){Forget(b);}else{Bodies.Add(b);}}
    internal static void Tick(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Link clock must be UTC");}
        var next=new Dictionary<uint,Entry>();
        foreach(var b in Bodies.OrderBy(b=>b.Serial.Value).ToArray())
        {
            if(b is not{Alive:true,Deleted:false}||Owner(b)is not{Alive:true,Deleted:false} p||b.Prepared?.Owner!=p.Serial.Value)
            {if(b is{Deleted:false,Prepared:not null}){Store(b,null);}else{Forget(b);}continue;}
            var group=b.Prepared.Links;if(group==null){Forget(b);continue;}Validate(group,b.Prepared.Owner);
            var marks=group.Marks.Where(m=>m.UntilUtc>at&&World.FindMobile((Serial)m.Target)is BaseCreature{Alive:true,Deleted:false}).ToArray();
            if(marks.Length!=group.Marks.Length){Store(b,marks.Length==0?null:group with{Marks=marks});}
            foreach(var m in marks){if(!next.TryAdd(m.Target,new(b,m))){throw new InvalidDataException("One monster appears in two saved Defiler groups");}}
        }
        Index.Clear();foreach(var pair in next){Index.Add(pair.Key,pair.Value);}
    }
    internal static DefilerLinkMark Mark(Mobile target,DateTime at){Tick(at);return target!=null&&Index.TryGetValue(target.Serial.Value,out var e)?e.Mark:null;}
    internal static bool HasState(Mobile target,int state,DateTime at)
    {
        Tick(at);
        return state switch
        {
            210=>target!=null&&Index.ContainsKey(target.Serial.Value),
            213=>target is WarlockDefilerBody{Prepared.Links.Marks.Length:>0},
            _=>throw new NotSupportedException("No qualified Defiler state producer for state"+state)
        };
    }
    private static DefilerLinkMark[] Select(WarlockDefilerBody b,BaseCreature first,DateTime at)
    {
        Tick(at);if(!Recipient(b,first,true)){return [];}var n=DefilerLinkNumbers.Capture(b);
        int count=b.Prepared.Links?.Marks.Length??0,free=Math.Min(5,n.RawMaximumLinks)-count;
        if(free<=0){return [];}
        return SkillCombat.NearbySnapshot<BaseCreature>(b.Map,b.Location,13).Append(first).DistinctBy(m=>m.Serial)
            .Where(m=>Recipient(b,m,true)&&!Index.ContainsKey(m.Serial.Value))
            .OrderBy(m=>m==first?0:1).ThenBy(m=>m.GetDistanceToSqrt(b)).ThenBy(m=>m.Serial.Value)
            .Take(Math.Min(free,n.PerCast)).Select(m=>new DefilerLinkMark(m.Serial.Value,n.Rank,n.SelfRank,Math.Min(60,n.RawSharedPercent),at.AddMilliseconds(n.DurationFrames*40))).ToArray();
    }
    internal static bool CanCast(WarlockDefilerBody b,BaseCreature first,DateTime at)=>Select(b,first,at).Length>0;
    internal static bool Cast(WarlockDefilerBody b,BaseCreature first,DateTime at)
    {
        var candidates=Select(b,first,at);if(candidates.Length==0){return false;}
        Store(b,new(b.Prepared.Owner,(b.Prepared.Links?.Marks??[]).Concat(candidates).ToArray()));Tick(at);
        WarlockDemonVisuals.DefilerLinkRelease(b);
        if(DemonNativeState.Learned(b))
        {
            foreach(var m in candidates){PresentMark(World.FindMobile((Serial)m.Target),m,at);}
        }
        return true;
    }
    private static void PresentMark(Mobile target,DefilerLinkMark mark,DateTime at)
    {
        if(!PaladinAuraVisuals.InWorld(target)||target.Hidden){return;}
        if(MarkVisuals.TryGetValue(mark.Target,out var current)
            && current.Until==mark.UntilUtc && current.Map==target.Map && current.Renew>at){return;}
        var leaseEnd=at.AddMilliseconds(900);
        if(PaladinAuraVisuals.Attach(target,SpellEffectGraphics.DefilerLinkMark,mark.UntilUtc<leaseEnd?mark.UntilUtc:leaseEnd,at))
        {MarkVisuals[mark.Target]=new(mark.UntilUtc,at.AddMilliseconds(425),target.Map);}
    }
    internal static void PresentMarks(DateTime at)
    {
        if(at<_presentationAt){return;}
        _presentationAt=at.AddMilliseconds(200);
        foreach(var id in Index.Keys.Concat(MarkVisuals.Keys).Distinct().ToArray())
        {
            var target=World.FindMobile((Serial)id);
            if(Index.TryGetValue(id,out var entry)&&DemonNativeState.Learned(entry.Body)
                &&entry.Body.Prepared.Links is{Fault:null}&&Recipient(entry.Body,target as BaseCreature,false))
            {PresentMark(target,entry.Mark,at);}
            else if(MarkVisuals.Remove(id,out var old)&&target?.Map==old.Map)
            {PaladinAuraVisuals.Clear(target,SpellEffectGraphics.DefilerLinkMark);}
        }
    }
    private static void Fault(WarlockDefilerBody body,string reason)
    {
        if(body.Prepared?.Links is not{} group||group.Fault!=null){return;}
        Store(body,group with{Fault=reason});Log.Warning("Defiler link sharing refused unsupported context for {Actor}: {Reason}",body.Serial,reason);
    }
    private static void Damage(SkillDamageContact contact)
    {
        DateTime at=TimedEffects.Now;Tick(at);
        if(contact.Target==null||!Index.TryGetValue(contact.Target.Serial.Value,out var origin)||origin.Body.Prepared.Links is not{Fault:null} group
            ||!Recipient(origin.Body,contact.Target as BaseCreature,false)){return;}
        if(contact.Parts is not{} parts){Fault(origin.Body,"Direct damage lacks settled typed components");return;}
        try{parts.Validate(contact.Damage256);}catch(NotSupportedException e){Fault(origin.Body,e.Message);return;}
        parts=parts.Fraction(origin.Mark.Percent);if(parts.Total==0){return;}
        foreach(var mark in group.Marks){if(mark.Target!=contact.Target.Serial.Value){Queue.Enqueue(new(origin.Body,mark,parts));}}
        if(Delivering){return;}Delivering=true;
        try
        {
            while(Queue.TryDequeue(out var pending))
            {
                Tick(at);if(!Index.TryGetValue(pending.Mark.Target,out var e)||e.Body!=pending.Body||e.Mark!=pending.Mark
                    ||World.FindMobile((Serial)e.Mark.Target)is not BaseCreature target||!Recipient(e.Body,target,false)){continue;}
                try{Deliver(e.Body,target,pending.Parts,at);}catch(NotSupportedException ex){Fault(e.Body,ex.Message);}
            }
        }
        finally{Queue.Clear();Delivering=false;}
    }
    private static void Deliver(WarlockDefilerBody b,BaseCreature target,DirectDamageParts parts,DateTime at)
    {
        var owner=Owner(b);var metadata=PlayerWeaponContact.Metadata(target,true);
        PreparedMonsterDamagePacket Mitigate(PreparedMonsterDamagePacket packet)
        {
            var reduced=SkillCombat.MitigatePrepared(b,target,parts.Gear,out int absorbed,pierceOverride:_=>0);
            var typed=DirectDamageParts.From(reduced);
            return packet with{Physical256=typed.Physical256,Magic256=typed.Magic256,Fire256=typed.Fire256,Cold256=typed.Cold256,
                Lightning256=typed.Lightning256,Total256=checked((int)typed.Total),AbsorbedLife256=absorbed};
        }
        var packet=new PreparedMonsterDamagePacket(parts.Physical256,checked((int)parts.Total),0,0,33,45,true,
            Magic256:parts.Magic256,Fire256:parts.Fire256,Cold256:parts.Cold256,Lightning256:parts.Lightning256);
        var services=new InstantSpellNpcDamage(owner,target,metadata,null,at,"Health Link",()=>TelekinesisCombat.DefenderRoll(),emitter:b,npcMitigation:Mitigate);
        var point=target.Location;
        var map=target.Map;
        var result=PreparedMonsterDamage.ReleaseMissile(b,target,packet,new(NamedCombat.Elite(target),()=>false,()=>metadata?.Velocity??0,()=>target is BaseHire),
            max=>NpcEquipmentRandom.Limited(b,max),services,at);
        if(result.AfterHealth256<result.Before256){WarlockDemonVisuals.DefilerLinkImpact(b,target,point,map,at);}
    }
}
