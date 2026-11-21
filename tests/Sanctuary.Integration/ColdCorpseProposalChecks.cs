// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ColdCorpseProposalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ColdCorpseProposalChecks));
    private sealed record Body(uint Corpse,CorpseSkillRecord Record,uint[] Items);
    private sealed record Saved(int Generation,uint Owner,Point3D Point,int Life,int Mana,Body[] Bodies);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cold-corpse-proposal-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"cold-corpse-proposal-reload-check":"cold-corpse-proposal-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved ordinary-chill corpse rule, shared guards and original saves",_=>Core.LoopContext.Post(()=>
            {try{Run(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static uint[] Items(Corpse c)=>c.Items.Select(i=>i.Serial.Value).OrderBy(i=>i).ToArray();
    private static Corpse Make(PlayerMobile p,int roll,string state="cold",bool supplied=true)
    {
        var m=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};m.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);m.AIObject?.AITimer.Stop();m.SetResistance(ResistanceType.Cold,0);
        int calls=0;if(supplied){ColdCorpseRules.SetRoll(m,()=>{calls++;return roll;});}
        if(state is "cold" or "expired" or "removed" or "freeze" or "holy")
        {Require(ColdStates.Commit(p,m,state=="expired"?1:500,true,-50,1,Core.TickCount-(state=="expired"?1000:0)),"Cold setup:"+state);}
        if(state=="removed"){Require(ColdStates.Remove(m,TimedEffects.Now),"Remove cold");}
        if(state=="freeze"){Require(FreezeStates.Commit(p,m,500,true,1,Core.TickCount),"Frozen control");}
        if(state=="holy"){PaladinHolyFreeze.Restore(PaladinHolyFreeze.Snapshot().Append(new HolyFreezeLease(m.Serial.Value,p.Serial.Value,1,30,30,30,true,TimedEffects.Now.AddSeconds(20))).ToArray());}
        SkillCombat.HitParts(p,m,new(100000,0,0,0,0),recover:false);
        var c=m.Corpse as Corpse;Require(c is{Deleted:false},"Actual cold corpse creation");c.DropItem(new Gold(17));c.BeginDecay(FixtureSpace.Retention);
        bool existing=state is "freeze" or "holy",eligible=state=="cold";
        Require((!supplied||CorpseSkillRules.Shattered(c)==(existing||eligible&&roll<20))&&calls==(eligible&&supplied?1:0),$"Approved corpse result:{state},roll={roll},supplied={supplied},calls={calls}");
        var record=CorpseSkillRules.Profiles.Find(c);CorpseSkillRules.Capture(m,c);
        Require(CorpseSkillRules.Profiles.Find(c)==record&&calls==(eligible&&supplied?1:0)&&c.Items.OfType<Gold>().Any(g=>g.Amount==17),"Duplicate capture rerolled or loot changed");return c;
    }
    private static void Denied(PlayerMobile p,Corpse c)
    {
        int mana=NecromancerEffects.ManaBalance256(p);var items=Items(c);var row=CorpseSkillRules.Profiles.Find(c);
        var lich=new GraveyardLich{CantWalk=true};lich.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);lich.AIObject?.AITimer.Stop();
        try
        {
            Require(CorpseSkillRules.Shattered(c)&&!CorpseSkillRules.Available(c)&&!CorpseSkillRules.Usable(p,c,12)
                &&!CorpseRecoveryHooks.CanAnimate(c)&&!lich.CanRaise(c),"Shattered corpse entered shared/native/Lich consumers");
            foreach(var a in new[]{CombatAbility.CorpseExplosion,CombatAbility.PoisonExplosion,CombatAbility.Revive})
            {
                ClassCombat.Stop(p);var action=ClassCombat.For(p);action.NextAction=action.RecoveryUntil=Core.TickCount;
                bool started=ClassCombat.BeginCorpse(p,a,c.Serial,c.Location,Core.TickCount);
                Require(!started&&NecromancerEffects.ManaBalance256(p)==mana&&!c.SanctuaryConsumed&&!c.Animated&&Items(c).SequenceEqual(items)&&CorpseSkillRules.Profiles.Find(c)==row,"Shattered corpse public cast/payment/loot:"+a);
            }
        }
        finally{lich.Delete();}
    }
    private static void Run(bool reload)
    {
        PlayerMobile p;Corpse[] retained;int generation=0;
        if(!reload){p=NativeCorpseChecks.Fresh();retained=[Make(p,0),Make(p,20)];}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
            Require(generation<=2&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Original owner wounds/resources");p.MoveToWorld(s.Point,Map.Trammel);
            retained=s.Bodies.Select(b=>
            {var c=World.FindItem((Serial)b.Corpse) as Corpse;Require(c is{Deleted:false}&&JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(c))==JsonSerializer.Serialize(b.Record)&&Items(c).SequenceEqual(b.Items),"Original saved corpse decision/identity/loot changed");return c;}).ToArray();
        }
        Denied(p,retained[0]);Require(CorpseSkillRules.Available(retained[1])&&CorpseRecoveryHooks.CanAnimate(retained[1]),"Nonshattered original corpse was disabled");
        int shattered=0;
        for(int roll=0;roll<100;roll++){var c=Make(p,roll);if(CorpseSkillRules.Shattered(c)){shattered++;}c.Delete();}
        Require(shattered==20,"Candidate must accept exactly20 of100 roll values");
        foreach(string state in new[]{"plain","expired","removed","freeze","holy"}){var c=Make(p,0,state);c.Delete();}
        var ordinary=Make(p,0,supplied:false);Require(CorpseSkillRules.Available(ordinary)!=CorpseSkillRules.Shattered(ordinary),"Default native-RNG corpse availability disagrees with its stored decision");ordinary.Delete();
        MageKills();
        var positive=Make(p,99);NativeCorpseChecks.Cast(p,CombatAbility.CorpseExplosion,positive);positive.Delete();
        ClassCombat.Stop(p);var point=p.Location;SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,17);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,p.Serial.Value,point,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),retained.Select(c=>new Body(c.Serial.Value,CorpseSkillRules.Profiles.Find(c),Items(c))).ToArray())));
    }
    private static void MageKills()
    {
        var owner=MagePetBuffChecks.Owner(1);
        try
        {
            foreach(int roll in new[]{0,20})
            {
                var mage=MagePetBuffChecks.Pet(owner,1);var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
                target.MoveToWorld(new(mage.X+5,mage.Y,mage.Z),mage.Map);target.AIObject?.AITimer.Stop();target.SetResistance(ResistanceType.Cold,0);TimedEffects.PreventHealing(target,TimedEffects.Now);SkillHealth.SetBalance256(target,1);
                int calls=0;ColdCorpseRules.SetRoll(target,()=>{calls++;return roll;});int mana=NecromancerEffects.ManaBalance256(owner);long id=NecromancerMageMissiles.Launch(mage,target,TimedEffects.Now);
                try
                {
                    Require(id>0&&NecromancerMageMissiles.Snapshot().Bolts.Single(b=>b.Id==id).Packet.Spell==CombatAbility.IceBolt,"Actual Mage Ice Bolt release");
                    for(int frame=0;frame<100&&NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(b=>b.Id==id)is{} shot;frame++){NecromancerMageMissiles.Tick(shot.NextUtc,true,id);}
                    Require(!target.Alive&&target.Corpse is Corpse,"Actual Mage cold kill created no corpse");var c=(Corpse)target.Corpse;c.DropItem(new Gold(17));
                    Require(calls==1&&CorpseSkillRules.Shattered(c)==(roll<20)&&CorpseSkillRules.Available(c)==(roll>=20)&&NecromancerEffects.ManaBalance256(owner)==mana&&c.Items.OfType<Gold>().Any(g=>g.Amount==17),"Mage Ice Bolt cold-kill decision,availability,loot or owner mana");c.Delete();
                }
                finally{mage.Delete();target.Delete();}
            }
        }
        finally{owner.Delete();}
    }
}
