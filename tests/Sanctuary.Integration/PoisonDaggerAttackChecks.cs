// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PoisonDaggerAttackChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonDaggerAttackChecks));
    private sealed record Saved(uint Owner,uint Target,int PaidMana,int Balance);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-dagger-attack-fixtures.json");
    public static void Configure(){Register("poison-dagger-attack-check",Check);Register("poison-dagger-attack-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Poison Dagger hit, shield, payment and interruption",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static (PlayerMobile Owner,BaseWeapon Weapon,GraveyardSkeleton Target) Scene(bool helm=true)
    {
        var p=PoisonDaggerChecks.Owner();var weapon=PoisonDaggerChecks.Dagger(p);
        if(helm){PoisonDaggerChecks.Named(p,"Wormskull");}
        return(p,weapon,PoisonDaggerChecks.Target(p));
    }
    private static bool Begin(PlayerMobile p,Mobile t)
    {PoisonDaggerChecks.Ready(p);return ClassCombat.Perform(p,CombatAbility.PoisonDagger,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static bool Poisoned(Mobile t)=>TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);
    private static void Results()
    {
        foreach(bool hit in new[]{false,true})foreach(bool block in new[]{false,true})
        {
            var(p,weapon,t)=Scene();int hitCalls=0,blockCalls=0,poisonCalls=0;
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                Require(SourceBlocking.TryRate(t,out int rate,out _)&&rate>0,"Real shield component control");
                PoisonDaggerPackets.HitRoll=()=>{hitCalls++;return hit?0:1;};
                SourceBlocking.BlockRoll=n=>{Require(n==100,"Block roll domain");blockCalls++;return block?0:99;};
                PoisonDaggerPackets.PoisonRoll=_=>{poisonCalls++;return 0;};
                int balance=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.PoisonDagger).Mana256;
                Require(Begin(p,t),"Prepare hit result");var prepared=ClassCombat.For(p).PoisonDaggerAttack;
                int expected=hit?(block?SourceMeleeResult.Block:SourceMeleeResult.Successful|SourceMeleeResult.GetHit):0;
                Require(prepared?.Flags==expected&&NecromancerEffects.ManaBalance256(p)==mana-cost&&SkillHealth.Balance256(t)==balance&&!Poisoned(t),"Result flags/start payment or premature damage");
                Require(hitCalls==1&&blockCalls==(hit?1:0)&&poisonCalls==(hit&&!block?1:0),"Miss/block consumed damage rolls or skipped shield");
                PoisonDaggerPackets.HitRoll=()=>throw new InvalidOperationException("Release rerolled hit");
                SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Release rerolled block");
                PoisonDaggerPackets.PoisonRoll=_=>throw new InvalidOperationException("Release rerolled poison");
                Require(Finish(p)&&ClassCombat.For(p).PoisonDaggerAttack==null&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Prepared result release/repeated payment");
                Require((SkillHealth.Balance256(t)<balance)==(hit&&!block)&&Poisoned(t)==(hit&&!block),"Prepared miss/block delivered damage or poison");
            }
            finally{PoisonDaggerChecks.Drop(p,t);}
        }
    }
    private static void Captured()
    {
        PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        var(p,weapon,t)=Scene();
        try
        {
            // A supported real item AR pool distinguishes adding the skill
            // percentage from multiplying the already-enhanced final rating.
            PoisonDaggerChecks.Named(p,"The Mahim-Oak Curio");
            int percent=(int)SkillMechanics.Values(p,CombatAbility.PoisonDagger).Formula("toht");
            int basis=ClassStats.PlayerAttackBase(p),items=ClassStats.PlayerItemAttackPercent(p);
            Require(items>0&&ClassStats.AttackRating(p,percent)==basis*(100+items+percent)/100
                &&ClassStats.AttackRating(p,percent)!=ClassStats.AttackRating(p)*(100+percent)/100,"Skill AR was multiplied after existing percentages");
            t.SetResistance(ResistanceType.Poison,50);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);
            Require(Begin(p,t),"Capture mitigation");var impact=ClassCombat.For(p).PoisonDaggerAttack.Impact;
            Require(impact.AllocatedPoison==new PreparedPoison(79,260)&&NecromancerEffects.ManaBalance256(p)==mana-832
                &&impact.LifeDamage256==NamedElements.To256(impact.Parts.Total)+79,"Initial poison mitigation,immediate source total or fractional mana");
            t.SetResistance(ResistanceType.Poison,0);var helm=p.HeadArmor;Require(helm!=null,"Missing real Wormskull");
            p.AddToBackpack(helm);ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,0);
            Require(Finish(p)&&PoisonDaggerChecks.Poison(t).PoisonClock?.Rate256==79&&PoisonDaggerChecks.Poison(t).PoisonMs==10400
                &&NecromancerEffects.ManaBalance256(p)==0&&life-SkillHealth.Balance256(t)==impact.LifeDamage256,"Release recalculated gear/resistance,initial total or required a second mana payment");
        }
        finally{PoisonDaggerChecks.Drop(p,t);}
        var(pure,_,immune)=Scene();try
        {
            immune.SetResistance(ResistanceType.Physical,100);int before=SkillHealth.Balance256(immune);
            Require(Begin(pure,immune),"Pure poison initial hit");var impact=ClassCombat.For(pure).PoisonDaggerAttack.Impact;
            Require(impact.Parts.Total==0&&impact.LifeDamage256==158&&Finish(pure)
                &&before-SkillHealth.Balance256(immune)==158&&PoisonDaggerChecks.Poison(immune).PoisonClock?.Rate256==158
                &&CreatureHitRecovery.Remaining(immune)==0,"Pure poison initial amount,state or unwanted hit recovery");
        }
        finally{PoisonDaggerChecks.Drop(pure,immune);}
    }
    private static void Interruptions()
    {
        foreach(string action in new[]{"move","stop","weapon","respec","death"})
        {
            PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            var(p,weapon,t)=Scene();try
            {
                int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
                Require(Begin(p,t),"Interruptible start "+action);long at=ClassCombat.For(p).EffectAt;
                switch(action)
                {
                    case "move":var point=p.Location;p.MoveToWorld(new(point.X,point.Y+1,point.Z),p.Map);p.MoveToWorld(point,p.Map);break;
                    case "stop":ClassCombat.Stop(p);break;
                    case "weapon":p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Reequip");break;
                    case "respec":ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);break;
                    case "death":p.Kill();break;
                }
                Require(!ClassCombat.ResolveAction(p,at)&&ClassCombat.For(p).PoisonDaggerAttack==null&&SkillHealth.Balance256(t)==before&&!Poisoned(t),"Interrupted packet survived "+action);
                if(action is "move" or "stop" or "weapon"){Require(NecromancerEffects.ManaBalance256(p)==mana-832,"Interrupted paid start refunded "+action);}
            }
            finally{p.Corpse?.Delete();PoisonDaggerChecks.Drop(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PoisonDaggerPackets.HitRoll;var poison=PoisonDaggerPackets.PoisonRoll;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            Results();Captured();Interruptions();
            var(low,_,enemy)=Scene();NecromancerEffects.SetMana256(low,0);
            Require(Begin(low,enemy)&&ClassCombat.For(low).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(low).PoisonDaggerAttack==null&&NecromancerEffects.ManaBalance256(low)==0,"AttackNoMana did not use basic attack");ClassCombat.Stop(low);PoisonDaggerChecks.Drop(low,enemy);
            PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            var(p,_,t)=Scene();Require(Begin(p,t),"Saved paid pending start");ClassCombat.For(p).EffectAt=Core.TickCount+FixtureSpace.RetentionMilliseconds;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t))));
            Log.Information("Prepared Poison Dagger:4miss/hit/shield cases;additive AR,captured mitigation/gear,zero-mana paid release,5interruptions,AttackNoMana and saved pending controls pass");
        }
        finally{PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=poison;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=World.FindMobile((Serial)s.Target);
        Require(ClassCombat.For(p).PendingAbility==CombatAbility.None&&ClassCombat.For(p).PoisonDaggerAttack==null&&NecromancerEffects.ManaBalance256(p)==s.PaidMana
            &&SkillHealth.Balance256(t)==s.Balance&&!Poisoned(t),"Saved paid attack replayed or payment changed");
    }
}
