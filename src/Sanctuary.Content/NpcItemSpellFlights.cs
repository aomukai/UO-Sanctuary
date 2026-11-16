// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record NpcItemSpellSource(uint Actor,string Binding,NpcItemSpellPacket Packet)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]public bool DeathNova{get;init;}
}

// Explicit original-NPC emission context for the shared Sorceress flight
// engine. Original item callbacks and their child-seed allocation are separate.
internal static class NpcItemSpellFlights
{
    internal static string Binding(SkeletonGuardian body)=>body is IronGolemBody golem?IronGolemFireBall.Binding(golem):OriginalNpcEquipment.Binding(body);
    internal static int Level(SkeletonGuardian body)=>body is IronGolemBody golem?golem.Prepared.Recipe.OwnerLevel:OriginalNpcEquipment.Level(body);
    internal static bool Attached(SkeletonGuardian body,PlayerMobile owner,int map,string binding=null)
    {
        if(body is not{Alive:true,Deleted:false}||owner is not{Alive:true,Deleted:false}||body.ControlMaster!=owner
            ||!(body is IronGolemBody golem?IronGolemFireBall.Attached(golem,owner):OriginalNpcEquipment.WorldEnabled(body))
            ||body.Map==null||body.Map==Map.Internal||body.Map.MapID!=map||owner.Map!=body.Map
            ||!PersistentMinions.Active(owner,body)||!body.Permitted(owner)||SpellHelper.IsTown(body.Location,owner)) {return false;}
        var current=Binding(body);return binding==null||current==binding;
    }
    internal static bool AttachedSource(SkeletonGuardian body,PlayerMobile owner,int map,NpcItemSpellSource context)
        =>context.DeathNova?body is AssassinShadowBody shadow&&ShadowDeathNovas.Attached(shadow,owner,map,context):Attached(body,owner,map,context.Binding);
    internal static bool CanHarm(SkeletonGuardian body,PlayerMobile owner,Mobile target,NpcItemSpellSource context)
        =>context.DeathNova?owner.CanBeHarmful(target,false):body.CanBeHarmful(target,false);
    internal static void Validate(SorceressSpellFlight flight)
    {
        var source=flight.Npc;var p=source?.Packet;
        if(source==null||!((Serial)source.Actor).IsMobile||source.Actor==flight.Owner||source.Binding is not{Length:64}
            ||source.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))||p==null||p.Skill is not(36 or 38 or 39 or 44 or 47 or 48 or 49)
            ||p.Rank!=flight.Rank||p.Rank is<1 or>63||NpcItemSpellDamage.Ability(p.Skill)!=flight.Ability||p.Element!=(p.Skill is 36 or 47?"fire":p.Skill is 39 or 44?"cold":"ltng")
            ||p.Missile!=(p.Skill==36?"firebolt":p.Skill==38?"chargedbolt":p.Skill==39?"icebolt":p.Skill==44?"frostnova":p.Skill==47?"fireball":p.Skill==49?"lightningbolt":"nova")
            ||source.DeathNova&&(p.Skill!=48||p.Rank>15)
            ||p.Minimum256<0||p.Maximum256<p.Minimum256||p.ColdFrames!=(p.Skill==44?200+25*(p.Rank-1):p.Skill==39?150+35*(p.Rank-1):0)
            ||p.Emissions!=(p.Skill is 44 or 48?64:p.Skill==38?Math.Min(24,p.Rank+2):1)
            ||flight.Minimum256!=flight.Slow.Damage256(p.Minimum256)||flight.Maximum256!=flight.Slow.Damage256(p.Maximum256)
            ||flight.ColdFrames!=p.ColdFrames||flight.Modifiers!=new FirestormModifiers(0,0,0,0,0))
        {throw new InvalidDataException("Invalid original NPC spell flight context");}
        _=NpcItemSpellDamage.Require(p.Skill);
    }
    internal static (SkeletonGuardian Body,BaseCreature Target) RequireTarget(PlayerMobile owner,Mobile recipient,NpcItemSpellSource context,int map,DateTime at,bool copiedArea=false)
    {
        var body=World.FindMobile((Serial)context.Actor)as SkeletonGuardian;
        if(!AttachedSource(body,owner,map,context)||recipient is not BaseCreature target||!ClassCombat.IsHostile(owner,target)
            ||ClassCombat.Friendly(body,target)||!CanHarm(body,owner,target,context)||SpellHelper.IsTown(target.Location,owner))
        {throw new NotSupportedException("Original NPC spell requires its attached source and living hostile NPC recipient");}
        _=NpcElementSpellHit.Require(body,owner,target,at,"Original NPC "+context.Packet.Missile);
        if(!copiedArea&&(!SourceBlocking.TryRate(target,out int rate,out _)||rate!=0))
        {throw new NotSupportedException("Original NPC spell shield recipient needs its source defense adapter");}
        return(body,target);
    }
    internal static void Explosion(PlayerMobile owner,Point3D center,SorceressSpellFlight flight,out ulong seed)
    {
        Validate(flight);
        if(flight.Npc.Packet.Skill!=47){throw new NotSupportedException("Unqualified original NPC copied-area spell");}
        var body=World.FindMobile((Serial)flight.Npc.Actor)as SkeletonGuardian;
        if(!AttachedSource(body,owner,flight.Map,flight.Npc)){throw new NotSupportedException("Detached original NPC explosion");}
        var values=new SkillValues(SkillMechanics.For(CombatAbility.Fireball),_=>flight.Rank,_=>0,Level(body));
        var targets=SkillCombat.Targets(body,owner,center,SorceressSpellMissiles.ExplosionRadius(values))
            .Where(t=>t is{Alive:true,Deleted:false}&&!ClassCombat.Friendly(body,t)&&CanHarm(body,owner,t,flight.Npc)&&!SpellHelper.IsTown(t.Location,owner)).ToArray();
        // Qualify the complete area before RNG or HP mutation. Unlike direct
        // missiles, source copied areas do not request shield/return-fire.
        foreach(var target in targets){_=RequireTarget(owner,target,flight.Npc,flight.Map,flight.NextUtc,true);}
        int raw=SorceressSpellMissiles.Roll(flight,out seed);var packet=flight.Npc.Packet;
        foreach(var target in targets)
        {
            if(!AttachedSource(body,owner,flight.Map,flight.Npc)){break;}
            if(target is not BaseCreature{Alive:true,Deleted:false} creature||ClassAvoidance.Missile(creature)){continue;}
            bool critical=NpcEquipmentCritical.Intrinsic(new(0,0,0,NecromancerSummons.Source(body.SourceMonsterId,"Crit")),()=>NpcEquipmentRandom.Raw(body));
            var damage=NpcEquipmentCritical.ApplyIntrinsic(0,new(0,raw/256.0,0,0,0),new(0,0),critical);
            NpcElementSpellHit.Deliver(body,owner,creature,"fire",NamedElements.To256(damage.Elements.Fire),0,flight.NextUtc,
                "Original NPC Fire Ball area",hitFlags:2,hitClass:damage.HitClass,pierce:element=>element=="fire"?packet.FirePierce:0);
        }
    }
    internal static int Impact(PlayerMobile owner,Mobile recipient,SorceressSpellFlight flight,out ulong seed)
    {
        Validate(flight);return ImpactCaptured(owner,recipient,flight.Npc,flight.Map,flight.Minimum256,flight.Maximum256,flight.Seed,flight.NextUtc,out seed);
    }
    internal static int ImpactCaptured(PlayerMobile owner,Mobile recipient,NpcItemSpellSource context,int map,int minimum,int maximum,ulong initial,DateTime at,out ulong seed)
    {
        var(body,target)=RequireTarget(owner,recipient,context,map,at);var p=context.Packet;
        int raw=SorceressSpellMissiles.Roll(minimum,maximum,initial,out seed);
        SorceressEffects.MissileHit(body,target,p.Missile);
        bool avoided=ClassAvoidance.Missile(target,p.Missile);
        bool critical=NpcEquipmentCritical.Intrinsic(new(0,0,0,NecromancerSummons.Source(body.SourceMonsterId,"Crit")),()=>NpcEquipmentRandom.Raw(body));
        if(avoided){return 0;}
        var damage=NpcEquipmentCritical.ApplyIntrinsic(0,p.Element=="fire"?new(0,raw/256.0,0,0,0):p.Element=="cold"?new(0,0,raw/256.0,0,0):new(0,0,0,raw/256.0,0),new(0,0),critical);
        int? Pierce(string element)=>element switch{"fire"=>p.FirePierce,"cold"=>p.ColdPierce,"ltng"=>p.LightningPierce,_=>0};
        int before=SkillHealth.Balance256(target);
        NpcElementSpellHit.Deliver(body,owner,target,p.Element,NamedElements.To256(damage.Elements.Total),p.ColdFrames,at,"Original NPC "+p.Missile,hitClass:context.DeathNova?64:damage.HitClass,pierce:Pierce,retainedSource:context.DeathNova?source=>source==body&&AttachedSource(body,owner,map,context):null,hitClassActive:context.DeathNova);
        return Math.Max(0,before-SkillHealth.Balance256(target));
    }
}
