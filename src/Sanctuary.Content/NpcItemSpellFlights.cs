// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record NpcItemSpellSource(uint Actor,string Binding,NpcItemSpellPacket Packet);

// Explicit original-NPC emission context for the shared Sorceress flight
// engine. Original item callbacks and their child-seed allocation are separate.
internal static class NpcItemSpellFlights
{
    internal static bool Attached(AmazonValkyrieBody body,PlayerMobile owner,int map,string binding=null)
    {
        if(body is not{Alive:true,Deleted:false}||owner is not{Alive:true,Deleted:false}||body.ControlMaster!=owner
            ||body.Map==null||body.Map==Map.Internal||body.Map.MapID!=map||owner.Map!=body.Map
            ||!PersistentMinions.Active(owner,body)||!body.Permitted(owner)||SpellHelper.IsTown(body.Location,owner)) {return false;}
        var current=ValkyrieEquipmentWeapons.Binding(body);return binding==null||current.Identity==binding;
    }
    internal static void Validate(SorceressSpellFlight flight)
    {
        var source=flight.Npc;var p=source?.Packet;
        if(source==null||!((Serial)source.Actor).IsMobile||source.Actor==flight.Owner||source.Binding is not{Length:64}
            ||source.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))||p==null||p.Skill is not(36 or 38 or 44 or 48)
            ||p.Rank!=flight.Rank||p.Rank is<1 or>63||NpcItemSpellDamage.Ability(p.Skill)!=flight.Ability||p.Element!=(p.Skill==36?"fire":p.Skill==44?"cold":"ltng")
            ||p.Missile!=(p.Skill==36?"firebolt":p.Skill==38?"chargedbolt":p.Skill==44?"frostnova":"nova")
            ||p.Minimum256<0||p.Maximum256<p.Minimum256||p.ColdFrames!=(p.Skill==44?200+25*(p.Rank-1):0)
            ||p.Emissions!=(p.Skill is 44 or 48?64:p.Skill==38?Math.Min(24,p.Rank+2):1)
            ||flight.Minimum256!=flight.Slow.Damage256(p.Minimum256)||flight.Maximum256!=flight.Slow.Damage256(p.Maximum256)
            ||flight.ColdFrames!=p.ColdFrames||flight.Modifiers!=new FirestormModifiers(0,0,0,0,0))
        {throw new InvalidDataException("Invalid original NPC spell flight context");}
        _=NpcItemSpellDamage.Require(p.Skill);
    }
    internal static int Impact(PlayerMobile owner,Mobile recipient,SorceressSpellFlight flight,out ulong seed)
    {
        Validate(flight);var context=flight.Npc;var p=context.Packet;
        var body=World.FindMobile((Serial)context.Actor)as AmazonValkyrieBody;
        if(!Attached(body,owner,flight.Map,context.Binding)||recipient is not BaseCreature target||!ClassCombat.IsHostile(owner,target)
            ||ClassCombat.Friendly(body,target)||!body.CanBeHarmful(target,false)||SpellHelper.IsTown(target.Location,owner))
        {throw new NotSupportedException("Original NPC spell requires its attached source and living hostile NPC recipient");}
        _=NpcElementSpellHit.Require(body,owner,target,flight.NextUtc,"Original NPC "+p.Missile);
        // This new route does not silently inherit the older player/Mage
        // omission of ordinary NPC shield rolls. Admit zero-block recipients
        // until the full spell-defense phase has its independent source proof.
        if(!SourceBlocking.TryRate(target,out int rate,out _)||rate!=0)
        {throw new NotSupportedException("Original NPC spell shield recipient needs its source defense adapter");}
        int raw=SorceressSpellMissiles.Roll(flight,out seed);
        SorceressEffects.MissileHit(body,target,p.Missile);
        bool avoided=ClassAvoidance.Missile(target,p.Missile);
        bool critical=NpcEquipmentCritical.Intrinsic(new(0,0,0,NecromancerSummons.Source("valkyrie","Crit")),()=>NpcEquipmentRandom.Raw(body));
        if(avoided){return 0;}
        var damage=NpcEquipmentCritical.ApplyIntrinsic(0,p.Element=="fire"?new(0,raw/256.0,0,0,0):p.Element=="cold"?new(0,0,raw/256.0,0,0):new(0,0,0,raw/256.0,0),new(0,0),critical);
        int? Pierce(string element)=>element switch{"fire"=>p.FirePierce,"cold"=>p.ColdPierce,"ltng"=>p.LightningPierce,_=>0};
        int before=SkillHealth.Balance256(target);
        NpcElementSpellHit.Deliver(body,owner,target,p.Element,NamedElements.To256(damage.Elements.Total),p.ColdFrames,flight.NextUtc,"Original NPC "+p.Missile,hitClass:damage.HitClass,pierce:Pierce);
        return Math.Max(0,before-SkillHealth.Balance256(target));
    }
}
