// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DefilerLinkCastingChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerLinkCastingChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint Target,uint Pending,DateTime PendingAction,string Birth,int Life,NpcEquipmentSeed Random,DeathMarkSavedActor Source,GenericPetSavedPath Path);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-link-casting-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-link-casting-reload-check":"defiler-link-casting-check";
            ConsoleInputHandler.RegisterCommand(name,"Own Defiler S1 source clock/release and original unissued save",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Model();Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        ConsoleInputHandler.RegisterCommand("defiler-link-casting-save","Save an actual connected original Defiler mid-windup",_=>Core.LoopContext.Post(()=>
            FixtureNetwork.Connected("defiler-link-casting-save-result",SavePending)));
    }
    private static GenericPetActionServices Outer()=>new((_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
        (_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
        ()=>throw new InvalidOperationException(),(_,_,_,_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),
        _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_)=>throw new InvalidOperationException(),
        _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>true,_=>0);
    private static void Model()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"defiler-link-cast-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int rate=row[0].GetInt32(),speed=SourceOtherAnimation.Speed(128,rate);Require(speed==row[1].GetInt32(),"Defiler source Other-rate writer mismatch");
            var flags=new int[144];flags[12]=1;var events=SourceAnimationEvents.Initial(new(0,speed,4096,flags,[]),0).Events;
            Require(events.Length==row[2].GetArrayLength(),"Defiler source event count mismatch");
            for(int i=0;i<events.Length;i++){var e=events[i];var wanted=row[2][i];Require(e.Type==wanted[0].GetInt32()&&e.Frame==wanted[1].GetInt32()&&e.Kind==wanted[2].GetInt32()&&e.Param==wanted[3].GetInt32(),"Defiler complete source event mismatch");}count++;
        }
        Require(count==401,"Incomplete Defiler source clock cases");
    }
    private static GoatmanNpcContactChecks.Recipient Target(WarlockDefilerBody b,int offset)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(b.X+4+offset,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.summondefiler",20);ClassSkillChecks.Train(p,"warlock.demonicmastery",20);FixtureSpace.Corridor(p,6560,28,3500);
        var account=FixtureNetwork.Authenticate(network,p,"defiler-s1-");var targets=new List<GoatmanNpcContactChecks.Recipient>();WarlockDefilerBody b=null;bool kept=false;
        try
        {
            DateTime at=TimedEffects.Now;b=DefilerFlyingWorld.CreateCandidate(p,new(p.X+1,p.Y,p.Z),at.AddDays(2));NpcEquipmentRandom.Set(b,SourceRandom.InitLow(405));
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p);var random=NpcEquipmentRandom.State(b);
            for(int i=0;i<5;i++){targets.Add(Target(b,i));}var first=targets[0];
            Require(DefilerLinkCasting.Timing(b)==(24,32,128)&&b.Prepared.Snapshot.Birth.Suppliers.AttackSpeed==24,$"Defiler S1 baseline mismatch:clock={DefilerLinkCasting.Timing(b)},other={SourceOtherAnimation.Rate(b)},masteryIAS={b.Prepared.Snapshot.Birth.Suppliers.AttackSpeed}");
            var slows=ImpaleSlow.Snapshot();
            try
            {
                ImpaleSlow.Restore(slows.Append(new(b.Serial.Value,p.Serial.Value,1,40,60,30,at.AddHours(1))).ToArray());
                Require(SourceOtherAnimation.Rate(b)==70&&DefilerLinkCasting.Timing(b).Speed==89,"S1 used Impale attack/velocity instead of stored Other field");
                TimedEffects.Chill(b,50,60000,at);Require(SourceOtherAnimation.Rate(b)==20&&DefilerLinkCasting.Timing(b).Speed==25,"Cold Other field did not add independently");
            }
            finally{ImpaleSlow.Restore(slows);TimedEffects.Clear(b);}
            using(var combat=new DemonNativeCombat(b,p))
            {
                bool duplicate=false;try{using var other=new DemonNativeCombat(b,p);}catch(NotSupportedException){duplicate=true;}Require(duplicate,"Duplicate S1 frame owner admitted");
                var bound=combat.Bind(Outer(),()=>0,()=>at);bool Start()=>bound.ExecuteSkill(8,405,checked((int)first.Serial.Value),0,0,true);
                Require(Start()&&combat.Current is{Mode:8,Contacts:0}&&b.Prepared.Links==null,"S1 source action did not start unpaid/unlinked");
                var action=DefilerLinkCasting.State(b);combat.Advance(23,action.ActionUtc.AddTicks(-1));Require(b.Prepared.Links==null,"S1 released before its source event");
                var commander=ClassSkillChecks.Player(DiabloClass.Barbarian);ClassSkillChecks.Train(commander,"barbarian.battlecommand",1);
                try
                {
                    BattleCommand.Apply(commander,b,1,at);combat.Advance(24,action.ActionUtc);
                    Require(DefilerLinkCasting.State(b)is{Released:true,Linked:true,ReleasedRank:21,ReleasedSelfRank:21}&&b.Prepared.Links.Marks.Length==5
                        &&b.Prepared.Links.Marks.All(m=>m.Rank==21&&m.SelfRank==21&&m.UntilUtc==action.ActionUtc.AddSeconds(20)),"S1 did not capture own live NPC ranks at release");
                }
                finally{BattleCommand.Clear(b);commander.Delete();}
                string group=JsonSerializer.Serialize(b.Prepared.Links);combat.Advance(24,action.ActionUtc);combat.Advance(32,action.EndUtc);
                Require(combat.Current==null&&DeathMarkWorld.State(b).Mode==1&&group==JsonSerializer.Serialize(b.Prepared.Links)&&!Start(),"S1 replayed/failed completion or full-capacity cast was admitted");
                Require(targets.All(t=>SkillHealth.Balance256(t)==t.HitsMax*256)&&SkillHealth.Balance256(b)==life&&NecromancerEffects.ManaBalance256(p)==mana&&NpcEquipmentRandom.State(b)==random,"Support-only S1 damaged/spent mana/healed/rerolled");
                void Clear(){combat.Cancel();b.RestorePrepared(b.Prepared with{Links=null});DefilerHealthLink.Tick(at);}
                Clear();Require(Start(),"Movement cancellation setup");var path=DeathMarkWorld.Path(b);uint x=path.X;var point=b.Location;
                DeathMarkWorld.WalkPrepared(b,path.X+1,path.Y);Require(b.Location==point,"Fractional source displacement changed native tile");combat.Advance(1,at.AddMilliseconds(40));
                Require(combat.Current==null&&b.Prepared.Links==null,"Fractional movement did not cancel S1");DeathMarkWorld.WalkPrepared(b,x,path.Y);
                Require(Start(),"Freeze cancellation setup");b.Frozen=true;combat.Advance(1,at.AddMilliseconds(40));b.Frozen=false;Require(combat.Current==null&&b.Prepared.Links==null,"Frozen S1 released");
                Require(Start(),"Mode interruption setup");DeathMarkWorld.ChangeMode(b,3);Require(combat.Current==null,"External mode did not cancel S1");DeathMarkWorld.ChangeMode(b,1);
                Require(Start(),"Logout cancellation setup");network.Mobile=null;p.NetState=null;DefilerLinkCasting.Advance(b,at.AddSeconds(2));
                Require(combat.Current==null&&b.Prepared.Links==null,"Offline S1 released");network.Mobile=p;p.NetState=network;PersistentMinions.Resume(p);DeathMarkWorld.RebindPrepared(b,DefilerFlyingWorld.BirthPath(b));
                var lost=Target(b,7);targets.Add(lost);var late=combat.Bind(Outer(),()=>0,()=>at);
                Require(late.ExecuteSkill(8,405,checked((int)lost.Serial.Value),0,0,true),"Lost-target setup");var doomed=DefilerLinkCasting.State(b);lost.Delete();bool refused=false;
                try{combat.Advance(24,doomed.ActionUtc);}catch(NotSupportedException){refused=true;}
                Require(refused&&DefilerLinkCasting.State(b)is{Released:true,Fault:not null}&&b.Prepared.Links==null,"Lost original target invented a fallback or retried");Clear();
                foreach(var t in targets.Skip(1)){t.Delete();}
                Require(Start(),"Retained original S1 setup");action=DefilerLinkCasting.State(b);combat.Advance(24,action.ActionUtc);combat.Advance(32,action.EndUtc);
                Require(b.Prepared.Links.Marks.Length==1&&b.Prepared.Links.Marks[0].Target==first.Serial.Value,"Retained one-target original link setup");
            }
            if(retain)
            {
                // Hold only the original group's deadlines for reload checks;
                // actual20second release deadlines were verified above.
                b.RestorePrepared(b.Prepared with{Links=b.Prepared.Links with{Marks=b.Prepared.Links.Marks.Select(m=>m with{UntilUtc=at.AddHours(1)}).ToArray()},NextRecoveryUtc=at.AddDays(2)});
                PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,first.Serial.Value,0,at,b.SavedText,life,random,DeathMarkWorld.State(b),GenericPetSavedPath.Capture(DeathMarkWorld.Path(b)))));kept=true;
            }
            Require(!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Own S1 enabled unfinished AI/learned Defiler");
            Log.Information("Defiler S1:401source clocks,24/32baseline,own Other fields/live release ranks,no masteryIAS/full-cap cast/replay/mana/damage,RNG/wounds preserved;movement/freeze/mode/logout cancellation and explicit lost-target refusal");Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var t in targets){t.Delete();}b?.Delete();p.Delete();}}
    }
    private static void SavePending(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=(WarlockDefilerBody)World.FindMobile((Serial)s.Body);var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        FixtureNetwork.Authenticate(network,p,"defiler-s1-save-");PersistentMinions.Resume(p);DeathMarkWorld.RebindPrepared(b,DefilerFlyingWorld.BirthPath(b));var target=Target(b,2);
        Require(DefilerLinkCasting.Begin(b,target,TimedEffects.Now)&&DefilerLinkCasting.State(b)is{Released:false}&&DeathMarkWorld.State(b).Mode==8,"Actual connected pending S1 save setup failed");
        var pending=DefilerLinkCasting.State(b);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Pending=target.Serial.Value,PendingAction=pending.ActionUtc,Source=DeathMarkWorld.State(b),Path=GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))}));
        World.Save();Log.Information("defiler-link-casting-save-result: passed");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=(WarlockDefilerBody)World.FindMobile((Serial)s.Body);var pending=(BaseCreature)World.FindMobile((Serial)s.Pending);
        Require(s.Pending!=0&&s.Source.Mode==8&&b.Map==Map.Internal&&b.SavedText==s.Birth&&SkillHealth.Balance256(b)==s.Life&&NpcEquipmentRandom.State(b)==s.Random
            &&DeathMarkWorld.State(b)==s.Source with{Mode=1}&&GenericPetSavedPath.Capture(DeathMarkWorld.Path(b))==s.Path&&DefilerLinkCasting.State(b)==null,"Original mid-windup save lost source/body data or replayed the transient cast");
        DefilerLinkCasting.Advance(b,s.PendingAction.AddSeconds(1));Require(b.Prepared.Links.Marks.Length==1&&DefilerHealthLink.Mark(pending,TimedEffects.Now)==null,"Unissued S1 linked after reload/offline");
        pending.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Pending=0,Source=DeathMarkWorld.State(b)}));
    }
}
