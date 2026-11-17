// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells.Necromancy;
namespace Sanctuary.Integration;

public static class CorpseShatterGuardChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CorpseShatterGuardChecks));
    private sealed record BodyState(uint Corpse,CorpseSkillRecord Record,uint[] Items,int Art,int Hue);
    private sealed record Saved(uint Owner,Point3D Point,int Life,int Mana,BodyState[] Bodies,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"corpse-shatter-guard-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"corpse-shatter-guard-reload-check":"corpse-shatter-guard-check";ConsoleInputHandler.RegisterCommand(name,"Shattered/reserved corpses cannot enter native or wild-Lich animation",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static Corpse Body(PlayerMobile p,int dx,string mode)
    {
        var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true,Fame=1000};t.SetResistance(ResistanceType.Cold,0);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        if(mode=="freeze"){Require(FreezeStates.Commit(p,t,500,true,1,Core.TickCount),"Actual freeze marker");}
        if(mode=="holy"){PaladinHolyFreeze.Restore(PaladinHolyFreeze.Snapshot().Append(new HolyFreezeLease(t.Serial.Value,p.Serial.Value,1,30,30,30,true,TimedEffects.Now.AddSeconds(20))).ToArray());}
        if(mode=="chill"){ColdCorpseRules.SetRoll(t,()=>99);Require(ColdStates.Commit(p,t,500,true,-50,1,Core.TickCount),"Actual ordinary cold marker");}
        t.Kill();Require(t.Corpse is Corpse,"Corpse creation");var c=(Corpse)t.Corpse;c.BeginDecay(FixtureSpace.Retention);c.DropItem(new Gold(17));Require(CorpseSkillRules.Shattered(c)==(mode is "freeze" or "holy"),"Existing shatter capture: "+mode);
        if(mode=="reserved"){Require(CorpseSkillRules.Reserve(c),"Actual corpse reservation");}return c;
    }
    private static GraveyardLich Lich(PlayerMobile p,int dx=0)
    {var m=new GraveyardLich{CantWalk=true,NoKillAwards=true};m.MoveToWorld(new(p.X+dx,p.Y+2,p.Z),p.Map);m.AIObject.AITimer.Stop();return m;}
    private static uint[] Items(Corpse c)=>c.Items.Select(x=>x.Serial.Value).OrderBy(x=>x).ToArray();
    private static object Group(Corpse c)
    {var f=typeof(AnimateDeadSpell).GetMethod("FindGroup",BindingFlags.NonPublic|BindingFlags.Static);Require(f!=null,"Native group selector");return f.Invoke(null,new object[]{c.Owner?.GetType()??typeof(GraveyardSkeleton)});}
    private static void NativeCommit(PlayerMobile p,Corpse c)
    {var f=typeof(AnimateDeadSpell).GetMethod("SummonDelay_Callback",BindingFlags.NonPublic|BindingFlags.Static);Require(f!=null,"Actual native delayed callback");f.Invoke(null,new object[]{p,c,c.Location,c.Map,Group(c)});}
    private static void Blocked(PlayerMobile p,GraveyardLich lich,Corpse c)
    {
        var record=Json(CorpseSkillRules.Profiles.Find(c));var items=Items(c);int art=c.ItemID,hue=c.Hue,mana=NecromancerEffects.ManaBalance256(p);bool native=CorpseRecoveryHooks.CanAnimate(c),wild=lich.CanRaise(c);
        Require(!native&&!wild&&!lich.BeginRaise(c),$"Blocked corpse accepted: id={c.Serial};shattered={CorpseSkillRules.Shattered(c)};reserved={CorpseSkillRules.NoSelect(c)};native={native};wild={wild}");
        var prior=CorpseRecoveryHooks.CanAnimate;int calls=0,count=World.Mobiles.Count;
        try
        {
            CorpseRecoveryHooks.CanAnimate=body=>{if(body==c){calls++;}return prior(body);};new AnimateDeadSpell(p).Target(c);Require(calls==1,"Native initial target did not reach its hook");calls=0;NativeCommit(p,c);Require(calls==1,"Native delayed commit did not recheck its hook");
        }
        finally{CorpseRecoveryHooks.CanAnimate=prior;}
        Require(World.Mobiles.Count==count&&!c.Animated&&!c.SanctuaryConsumed&&!c.Deleted&&Json(CorpseSkillRules.Profiles.Find(c))==record&&Items(c).SequenceEqual(items)&&c.ItemID==art&&c.Hue==hue&&NecromancerEffects.ManaBalance256(p)==mana,"Denied native target/commit mutated corpse,loot,actors or mana");
    }
    private static void NativePositive(PlayerMobile p)
    {
        var c=Body(p,10,"plain");var before=World.Mobiles.Keys.ToHashSet();var items=Items(c);
        try{Require(CorpseRecoveryHooks.CanAnimate(c),"Plain native animation was disabled");NativeCommit(p,c);var born=World.Mobiles.Values.Where(x=>!before.Contains(x.Serial)).ToArray();Require(c.Animated&&c.Hue==1109&&born.Length==1&&born[0]is BaseCreature{Summoned:true}&&Items(c).SequenceEqual(items),"Native delayed positive control did not animate or lost loot");foreach(var m in born){AnimateDeadSpell.Unregister(p,m);m.Delete();}}
        finally{foreach(var m in World.Mobiles.Values.Where(x=>!before.Contains(x.Serial)).ToArray()){AnimateDeadSpell.Unregister(p,m);m.Delete();}c.Delete();}
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;Corpse[] bodies;int generation=0;
        if(!reload){p=HellslayerChecks.Owner(DiabloClass.Necromancer,3750);bodies=new[]{Body(p,1,"freeze"),Body(p,2,"holy"),Body(p,3,"reserved"),Body(p,4,"chill")};}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);bodies=s.Bodies.Select(x=>(Corpse)World.FindItem((Serial)x.Corpse)).ToArray();Require(generation<=2&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Original caster wounds/mana");
            foreach(var entry in s.Bodies){var c=(Corpse)World.FindItem((Serial)entry.Corpse);Require(c is{Deleted:false,Animated:false,SanctuaryConsumed:false}&&Json(CorpseSkillRules.Profiles.Find(c))==Json(entry.Record)&&Items(c).SequenceEqual(entry.Items)&&c.ItemID==entry.Art&&c.Hue==entry.Hue,"Original corpse flags/identity/loot changed through reload");}p.MoveToWorld(s.Point,Map.Trammel);
        }
        var lich=Lich(p);foreach(var c in bodies.Take(3)){Blocked(p,lich,c);}Require(!CorpseSkillRules.Shattered(bodies[3])&&!CorpseSkillRules.NoSelect(bodies[3])&&CorpseRecoveryHooks.CanAnimate(bodies[3])&&lich.CanRaise(bodies[3]),"Ordinary chill's nonshatter result or plain raising was disabled");lich.Delete();NativePositive(p);
        var unknown=Body(p,11,"plain");CorpseSkillRules.Profiles.Remove(unknown);Require(CorpseRecoveryHooks.CanAnimate(unknown),"Legacy missing metadata was reclassified");unknown.Delete();
        var positive=Body(p,6,"plain");var pending=Body(p,7,"plain");var a=Lich(p);var b=Lich(p,5);var prior=World.Mobiles.Keys.ToHashSet();var positiveItems=Items(positive);var pendingItems=Items(pending);
        // New wild casters retain their normal three-second startup cooldown.
        Timer.StartTimer(TimeSpan.FromMilliseconds(3100),()=>
        {
        try{Require(a.CanRaise(positive)&&a.BeginRaise(positive)&&b.CanRaise(pending)&&b.BeginRaise(pending),"Actual wild raise positive/queued controls");Require(CorpseSkillRules.Reserve(pending),"Reservation during queued raise");}
        catch(Exception e){a.Delete();b.Delete();positive.Delete();pending.Delete();Log.Error(e,"corpse-shatter-guard-result: failed");return;}
        Timer.StartTimer(TimeSpan.FromMilliseconds(1100),()=>
        {
            try
            {
                var born=World.Mobiles.Values.OfType<GraveyardUndead>().Where(x=>!prior.Contains(x.Serial)&&x.Raised&&!x.Deleted).ToArray();Require(positive.Animated&&positive.SanctuaryConsumed&&born.Length==1&&!pending.Animated&&!pending.SanctuaryConsumed&&Items(positive).SequenceEqual(positiveItems)&&Items(pending).SequenceEqual(pendingItems),"Natural wild raise/revalidation or retained loot");
                var rows=bodies.Select(c=>new BodyState(c.Serial.Value,CorpseSkillRules.Profiles.Find(c),Items(c),c.ItemID,c.Hue)).ToArray();if(!reload){SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,17);}var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),rows,generation)));Log.Information("corpse-shatter-guard-result: passed");
            }
            catch(Exception e){Log.Error(e,"corpse-shatter-guard-result: failed");}
            finally{foreach(var m in World.Mobiles.Values.OfType<GraveyardUndead>().Where(x=>!prior.Contains(x.Serial)).ToArray()){m.Delete();}a.Delete();b.Delete();positive.Delete();pending.Delete();}
        });
        });
    }
}
