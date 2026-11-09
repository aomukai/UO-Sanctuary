// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SkillDamageEventChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SkillDamageEventChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,int Life256,int X,int Y);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skill-damage-event-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Direct damage notification and fractional-life preservation",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("skill-damage-event-check",false);Register("skill-damage-event-reload-check",true);
    }
    private static ShockWaveTarget Enemy(PlayerMobile owner)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        return t;
    }
    private static void Contacts(PlayerMobile owner)
    {
        var target=Enemy(owner);var other=Enemy(owner);
        var seen=new List<(SkillDamageContact Contact,int Life)>();Action<SkillDamageContact> react=null;
        void Observe(SkillDamageContact c){seen.Add((c,SkillHealth.Balance256(c.Target)));react?.Invoke(c);}
        SkillDamageEvents.BeforeDirectDamage+=Observe;
        try
        {
            int before=SkillHealth.Balance256(target);
            Require(SkillHealth.Damage(owner,target,129)==0&&SkillHealth.Balance256(target)==before-129,"Fractional contact changed HP semantics");
            Require(seen.Count==1&&seen[0].Contact==new SkillDamageContact(owner,target,129)&&seen[0].Life==before,"Fractional contact missing or dispatched after HP commit");seen.Clear();
            Require(SkillHealth.Damage(owner,target,127)==1&&SkillHealth.Balance256(target)==before-256&&seen.Count==1&&seen[0].Life==before-129,"Remainder carry or pre-hit balance changed");seen.Clear();
            SkillHealth.Damage(owner,target,0);SkillHealth.Damage(owner,target,-1);
            target.Blessed=true;SkillHealth.Damage(owner,target,256);target.Blessed=false;
            Require(seen.Count==0&&SkillHealth.Balance256(target)==before-256,"Zero/negative/invulnerable packet notified");
            foreach(var kind in new[]{SkillDamageKind.Periodic,SkillDamageKind.Return,SkillDamageKind.SelfCost}){SkillHealth.Damage(owner,target,128,kind);}
            Require(seen.Count==0&&SkillHealth.Balance256(target)==before-640,"Excluded kind lost damage or notified");
            int ownerLife=SkillHealth.Balance256(owner);SkillHealth.PayLife(owner,129);
            Require(seen.Count==0&&SkillHealth.Balance256(owner)==ownerLife-129,"Life payment changed damage or became a direct contact");
            before=SkillHealth.Balance256(target);
            using(CombatItemEvents.Suppress(owner,target))
            {
                SkillHealth.Damage(owner,target,129);
                SkillHealth.Damage(owner,other,128);
            }
            Require(seen.Count==1&&seen[0].Contact.Target==other&&SkillHealth.Balance256(target)==before-129,"Suppression leaked across recipients or discarded damage");seen.Clear();
            SkillHealth.Damage(owner,target,128);Require(seen.Count==1,"Suppression scope did not release");seen.Clear();

            before=SkillHealth.Balance256(target);
            react=c=>{using(CombatItemEvents.Suppress(c.Source,c.Target)){SkillHealth.Damage(c.Source,c.Target,129);}};
            SkillHealth.Damage(owner,target,128);react=null;
            Require(seen.Count==1&&SkillHealth.Balance256(target)==before-257,"Nested reaction recursed or overwrote its fractional damage");seen.Clear();

            before=SkillHealth.Balance256(target);
            AOS.Damage(target,owner,10,false,100,0,0,0,0,weaponDamage:true);
            Require(seen.Count==1&&seen[0].Contact.Damage256==10*256&&seen[0].Life==before&&SkillHealth.Balance256(target)==before-10*256,"Native AOS did not publish one mitigated contact");seen.Clear();
            target.SetResistance(ResistanceType.Fire,100);before=SkillHealth.Balance256(target);
            SkillCombat.FireImpact(owner,target,10*256);
            Require(seen.Count==0&&SkillHealth.Balance256(target)==before,"Fire immunity generated a positive contact");target.SetResistance(ResistanceType.Fire,0);
            SkillCombat.FireFrame(owner,target,256);
            SkillCombat.AuraElement(owner,target,256,"fire");
            ownerLife=SkillHealth.Balance256(owner);PaladinThorns.Retaliate(target,owner,256);
            Require(seen.Count==0&&SkillHealth.Balance256(target)==before-512&&SkillHealth.Balance256(owner)==ownerLife-256,"Field/aura/return classification changed damage or notified");
            SkillCombat.FireImpact(owner,target,256);
            Require(seen.Count==1&&seen[0].Contact.Damage256==256,"Discrete fire impact inherited periodic classification");seen.Clear();

            SkillHealth.SetBalance256(other,512);bool observedAlive=false;
            react=c=>{observedAlive=c.Target==other&&other.Alive&&SkillHealth.Balance256(other)==512;};
            SkillHealth.Damage(owner,other,1000);react=null;
            Require(observedAlive&&seen.Count==1&&!other.Alive,"Lethal contact was lost or notified after death");seen.Clear();
            SkillHealth.Damage(owner,other,256);Require(seen.Count==0,"Dead recipient notified");

            // A secondary reaction can finish the victim first. The pending
            // original packet must not write stale HP/fraction after death.
            react=c=>{using(CombatItemEvents.Suppress(c.Source,c.Target)){SkillHealth.Damage(c.Source,c.Target,int.MaxValue);}};
            Require(SkillHealth.Damage(owner,target,128)==0&&!target.Alive&&SkillHealth.Remainder256(target)==0&&seen.Count==1,"Reaction death replayed the original life commit");react=null;
            Require(!WarlockCombat.Implemented(CombatAbility.HexPurge),"Infrastructure silently enabled Purge");
            Log.Information("Damage event bridge:fractional/carry/native AOS/immune/direct fire/field/aura/return/self-cost/suppression/nested/lethal controls pass;Purge remains gated.");
        }
        finally{SkillDamageEvents.BeforeDirectDamage-=Observe;target.Delete();other.Delete();SkillHealth.Clear(target);SkillHealth.Clear(other);}
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;ShockWaveTarget target;int generation=0;
        if(reload)
        {
            var old=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=old.Generation+1;
            owner=World.FindMobile((Serial)old.Owner)as PlayerMobile;target=World.FindMobile((Serial)old.Target)as ShockWaveTarget;
            Require(owner!=null&&target!=null&&SkillHealth.Balance256(target)==old.Life256&&SkillHealth.Remainder256(target)==129,"Original saved fractional life changed");
            FixtureSpace.Place(owner,new(old.X,old.Y,0),Map.Trammel);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        }
        else
        {
            owner=new PlayerMobile{Player=true,Body=0x190,Name="SkillDamageEventFixture"};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(owner,99);
            FixtureSpace.Corridor(owner,6560,12,3500);target=Enemy(owner);SkillHealth.Damage(owner,target,129);
        }
        Contacts(owner);
        OrderedDamageEventChecks.Exercise(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,owner.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),owner.X,owner.Y)));
    }
}
