// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class WeaponLandedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WeaponLandedChecks));
    private sealed record Saved(int Generation,uint Owner,uint Target,int X,int Y,int Life256,HexPurgeTarget Mark);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-landed-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Landed weapon and prepared Purge composition",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("weapon-landed-check",false);Register("weapon-landed-reload-check",true);
    }
    private static CleaveActionTarget Enemy(PlayerMobile owner,int dx=1)
    {
        var t=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);
        t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static ulong Seed()
    {for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),next=seed;if(SourceRandom.Limited(ref next,100)==0){return seed;}}throw new InvalidOperationException("No Purge zero-roll seed");}
    private static void Exercise(PlayerMobile p)
    {
        var target=Enemy(p);var near=Enemy(p,3);var trace=new List<string>();HexPurgeBurst burst=default;int landed=0,damage=0,packet256=0;bool expectMark=true;
        var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;
        void Landed(WeaponLandedContact c)
        {
            Require(c.Source==p&&c.Target==target,"Landed source/recipient changed");trace.Add(c.Ranged?"missile":"melee");landed++;
            var registered=HexPurge.Buff(p);var plan=HexPurge.Plan(SkillMechanics.Values(p,CombatAbility.HexPurge,registered.Rank));
            var reaction=HexPurgeReactionProposal.Capture(plan,NamedElements.MasteryPercent(p,"mag"),DruidFirestorm.Modifiers(p),Seed());
            Require(HexPurgeReactionProposal.AttachPrepared(p,target,registered,plan,reaction,true,TimedEffects.Now),"Qualified landed event failed prepared attachment");
        }
        void Damage(SkillDamageContact c)
        {
            if(!expectMark){Require(HexPurge.Debuff(target)==null,"Unrelated spell attached a mark");damage++;return;}
            Require(c.Target==target&&HexPurge.Debuff(target)?.Reaction!=null,"Damage arrived before the applying hit's mark");
            trace.Add("damage");damage++;packet256=c.Damage256;burst=HexPurgeReactionProposal.OnDamagePrepared(c,TimedEffects.Now,true);
        }
        WeaponLandedEvents.BeforeDamage+=Landed;SkillDamageEvents.BeforeDirectDamage+=Damage;
        try
        {
            HexPurge.Clear(p);HexPurge.Clear(target);Require(HexPurge.CastPrepared(p,replaceGroup:true),"Prepared Purge registration");
            CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;int hp=SkillHealth.Balance256(target),other=SkillHealth.Balance256(near);
            var a=CleaveActionProposal.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);Require(a!=null,"Cleave did not start");CleaveActionProposal.Advance(a,a.EffectAt);
            Require(a.Fault==null&&a.Successful==1&&landed==1&&damage==1&&trace.SequenceEqual(new[]{"melee","damage"})&&burst.Triggered&&burst.Fault==null
                &&HexPurge.Debuff(target)?.Reaction.RemainingCharges==1&&SkillHealth.Balance256(target)<hp-packet256&&SkillHealth.Balance256(near)<other,
                "Actual Cleave attachment/reaction order,once charge or secondary HP composition failed");

            CleaveActionChecks.Ready(p);HexPurge.Clear(target);trace.Clear();landed=damage=0;CleaveActionProposal.HitRoll=()=>1;
            a=CleaveActionProposal.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);CleaveActionProposal.Advance(a,a.EffectAt);
            Require(a.Fault==null&&a.Successful==0&&landed==0&&damage==0&&HexPurge.Debuff(target)==null,"Missed Cleave attached a landed mark");CleaveActionProposal.HitRoll=()=>0;
            var prepared=PlayerWeaponContact.PrepareOrdinary(p,target,0,0,()=>true,()=>0,()=>0,_=>0,_=>0,new Random(4));
            foreach(int flags in new[]{0,16,128,256,32768})
            {PlayerWeaponContact.Release(p,target,prepared with{Flags=flags},TimedEffects.Now);}
            Require(landed==0&&damage==0&&HexPurge.Debuff(target)==null,"Miss/block/dodge packet notified a landed contact");

            // A successful, fully absorbed melee contact still attaches, but
            // its zero-life packet cannot consume a reaction charge.
            var zero=prepared with{Impact=prepared.Impact with{Parts=new(0,0,0,0,0),LifeDamage256=0,Absorbed256=0,AllocatedPoison=null}};
            PlayerWeaponContact.Release(p,target,zero,TimedEffects.Now);
            Require(landed==1&&damage==0&&HexPurge.Debuff(target)?.Reaction.RemainingCharges==2,"Zero-life landed contact failed attachment or spent a charge");
            HexPurge.Clear(target);landed=damage=0;
            using(CombatItemEvents.Suppress(p,target)){PlayerWeaponContact.Release(p,target,zero,TimedEffects.Now);}
            Require(landed==0&&damage==0,"Pair-suppressed weapon contact notified");
            PlayerWeaponContact.Release(p,target,zero with{Flags=zero.Flags|32},TimedEffects.Now);
            Require(landed==0&&damage==0,"ResultFlags32 weapon contact notified");

            // Existing ranged weapon transport shares the same before-life
            // boundary; an unrelated direct spell must not attach a mark.
            trace.Clear();hp=SkillHealth.Balance256(target);
            SkillCombat.ResolveHitParts(p,target,new(1,0,0,0,0),false,weapon:true,ranged:true,enhanceBase:false,addWeaponElements:false);
            Require(landed==1&&damage==1&&trace.SequenceEqual(new[]{"missile","damage"})&&burst.Triggered&&burst.Fault==null&&SkillHealth.Balance256(target)<hp-packet256,
                "Existing ranged weapon transport missed attachment-before-damage");
            HexPurge.Clear(target);landed=damage=0;expectMark=false;
            SkillCombat.ResolveHitParts(p,target,new(1,0,0,0,0),false,spell:true,enhanceBase:false,addWeaponElements:false);
            Require(landed==0&&damage==1&&HexPurge.Debuff(target)==null,"Unrelated spell became a landed weapon event");
        }
        finally
        {
            WeaponLandedEvents.BeforeDamage-=Landed;SkillDamageEvents.BeforeDirectDamage-=Damage;
            CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);HexPurge.Clear(p);HexPurge.Clear(target);target.Delete();near.Delete();
        }
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;CleaveActionTarget target;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=World.FindMobile((Serial)s.Owner)as PlayerMobile;target=World.FindMobile((Serial)s.Target)as CleaveActionTarget;
            Require(p!=null&&target!=null&&SkillHealth.Balance256(target)==s.Life256&&HexPurge.Debuff(target)==s.Mark,"Original spent mark/seed/wounds changed on reload");
            FixtureSpace.Place(p,new(s.X,s.Y,0),Map.Trammel);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        }
        else
        {
            p=CleaveActionChecks.Fresh();ClassSkillChecks.Train(p,"warlock.hexpurge",10);target=Enemy(p,5);
            Require(HexPurge.CastPrepared(p,replaceGroup:true),"Saved prepared registration");var v=SkillMechanics.Values(p,CombatAbility.HexPurge);var b=HexPurge.Buff(p);var plan=HexPurge.Plan(v);
            Require(HexPurgeReactionProposal.AttachPrepared(p,target,b,plan,HexPurgeReactionProposal.Capture(plan,0,new(0,0,0,0,0),Seed()),true,TimedEffects.Now),"Saved mark attachment");
            var result=HexPurgeReactionProposal.OnDamagePrepared(new(p,target,256),TimedEffects.Now,true);Require(result.Triggered&&HexPurge.Debuff(target)?.Reaction.RemainingCharges==1,"Saved mark did not actually spend a charge");
            // Persistence-only hold, not a claim about the live debuff length.
            HexPurge.StoreReactionPrepared(HexPurge.Debuff(target) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)});
        }
        CleaveActionChecks.Ready(p);Exercise(p);
        // Exercise clears the owner's registration, not the distinct saved
        // recipient's existing snapshot. Do not refill its spent charge.
        Require(!WarlockCombat.Implemented(CombatAbility.HexPurge)&&!HexPurgeReactionProposal.Approved,"Prepared composition enabled unapproved Purge");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,target.Serial.Value,p.X,p.Y,SkillHealth.Balance256(target),HexPurge.Debuff(target))));
        Log.Information("Landed weapon bridge:actual Cleave/ranged transport mark-before-damage,once reaction,miss/block/dodge/zero/suppression/spell controls pass;Purge remains gated.");
    }
}
