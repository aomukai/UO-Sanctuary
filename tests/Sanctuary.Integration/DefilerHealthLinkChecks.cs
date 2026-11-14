// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DefilerHealthLinkChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerHealthLinkChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint[] Targets,string Text,int Life,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-health-link-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-health-link-reload-check":"defiler-health-link-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved Health Link groups/typed damage/lifecycle on actual native NPCs",_=>Core.LoopContext.Post(()=>
            {try{Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>{if(reload){Reload(n);}Scene(n,!reload,name+"-result");});}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="Defiler approved link fixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.summondefiler",20);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);FixtureSpace.Corridor(p,6560,30,3500);
        var account=FixtureNetwork.Authenticate(network,p,"defiler-link-");WarlockDefilerBody b=null;var targets=new GoatmanNpcContactChecks.Recipient[6];bool kept=false;
        try
        {
            DateTime at=TimedEffects.Now;b=WarlockDefilerBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at.AddHours(2));NpcEquipmentRandom.Set(b,SourceRandom.InitLow(405));
            Require(!b.SavedText.Contains("Links"),"Legacy Defiler JSON gained an empty link field");
            for(int i=0;i<targets.Length;i++)
            {
                var t=targets[i]=new(){NoKillAwards=true,CantWalk=true,Frozen=true};t.SetHits(50000);t.Hits=t.HitsMax;
                foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}t.MoveToWorld(new(b.X+4+i%3,b.Y+i/3,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            }
            Require(DefilerHealthLink.Cast(b,targets[0],at)&&b.Prepared.Links.Marks.Length==5&&b.Prepared.Links.Marks[0].Target==targets[0].Serial.Value,"Approved target-first own group/cap selection failed");
            var group=b.Prepared.Links;int pct=group.Marks[0].Percent;var linked=group.Marks.Select(m=>(GoatmanNpcContactChecks.Recipient)World.FindMobile((Serial)m.Target)).ToArray();
            Require(!DefilerHealthLink.Cast(b,targets[5],at.AddMilliseconds(1))&&b.Prepared.Links==group,"Full group refreshed/replaced old marks");
            var other=WarlockDefilerBody.CreatePrepared(p,new(p.X,p.Y+1,p.Z),at.AddHours(2));
            try
            {
                Require(DefilerHealthLink.Cast(other,targets[5],at)&&other.Prepared.Links.Marks.Length==1
                    &&other.Prepared.Links.Marks[0].Target==targets[5].Serial.Value&&b.Prepared.Links==group,
                    "Separate Defiler overwrote another group's marks");
            }
            finally{other.Delete();}
            var mixed=new DirectDamageParts(10000,20000,30000,40000,50000);var victim=linked[1];victim.SetResistance(ResistanceType.Physical,25);victim.SetResistance(ResistanceType.Fire,50);victim.SetResistance(ResistanceType.Energy,100);
            int[] before=linked.Select(SkillHealth.Balance256).ToArray();var split=mixed.Fraction(pct);
            SkillHealth.TypedDamage(p,linked[0],checked((int)mixed.Total),mixed);
            int expected=split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256;
            Require(before[0]-SkillHealth.Balance256(linked[0])==mixed.Total&&before[1]-SkillHealth.Balance256(victim)==expected
                &&before[2]-SkillHealth.Balance256(linked[2])==split.Total,"Typed share lost original hit,FP8/recipient defense or recursed");
            Require(MonsterUninterruptibles.State(victim)?.LastAttacker==b.Serial.Value&&PreparedMonsterCredit.Owner(victim,b)==p
                &&b.Prepared.Links.Fault==null,"Shared NPC source/owner credit or typed compatibility failed");
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillHealth.TypedDamage(p,linked[0],1000,new(1000,0,0,0,0),SkillDamageKind.Periodic);
            Require(SkillHealth.Balance256(victim)==before[1],"Periodic poison/other ticks initiated link sharing");
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillCombat.FireImpact(p,linked[0],25600);
            Require(before[0]-SkillHealth.Balance256(linked[0])==25600&&before[1]-SkillHealth.Balance256(victim)==(25600*pct/100)/2,"Actual elemental direct route did not publish settled typed damage");
            victim.SetResistance(ResistanceType.Physical,0);victim.SetResistance(ResistanceType.Fire,0);victim.SetResistance(ResistanceType.Energy,0);
            victim.MoveToWorld(new(b.X+20,b.Y,b.Z),b.Map);before=linked.Select(SkillHealth.Balance256).ToArray();SkillCombat.FireImpact(p,linked[0],25600);
            Require(before[1]-SkillHealth.Balance256(victim)==25600*pct/100&&DefilerHealthLink.Mark(victim,at)!=null,"Leaving application radius broke unexpired link");
            before=linked.Select(SkillHealth.Balance256).ToArray();network.Mobile=null;p.NetState=null;SkillHealth.TypedDamage(p,linked[0],1000,new(1000,0,0,0,0));
            Require(SkillHealth.Balance256(victim)==before[1]&&b.Prepared.Links==group,"Offline sharing occurred or mark duration/reset changed");network.Mobile=p;p.NetState=network;PersistentMinions.Resume(p);
            Require(b.Map==p.Map&&b.Prepared.Links==group,"Reconnect did not reactivate the unchanged link group");
            var state=b.Prepared;string text=b.SavedText;bool refused=false;
            try{b.RestorePrepared(state with{Links=group with{Owner=p.Serial.Value+1}});}catch(InvalidDataException){refused=true;}
            Require(refused&&b.SavedText==text,"Malformed link ownership partially restored");
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillHealth.Damage(p,linked[0],257);
            Require(before[0]-SkillHealth.Balance256(linked[0])==257&&before[1]==SkillHealth.Balance256(victim)
                &&b.Prepared.Links.Fault=="Direct damage lacks settled typed components", $"Untyped damage mismatch:primary={before[0]-SkillHealth.Balance256(linked[0])},secondary={before[1]-SkillHealth.Balance256(victim)},fault={b.Prepared.Links.Fault},bodyMap={b.Map},ownerMap={p.Map}");
            b.RestorePrepared(state); // Fixture restores its exact pre-fault state for remaining independent cases.
            var doomed=linked[4];SkillHealth.SetBalance256(doomed,256);int directEvents=0;
            void Observe(SkillDamageContact c){directEvents++;}
            SkillDamageEvents.BeforeDirectDamage+=Observe;
            try
            {
                SkillCombat.FireImpact(p,linked[0],25600);
                Require(!doomed.Alive&&doomed.LastKiller==b&&PreparedMonsterCredit.Owner(doomed,b)==p&&directEvents==1,
                    "Shared lethal damage lost Defiler/owner credit or dispatched secondary direct/proc events");
            }
            finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            DefilerHealthLink.Tick(at.AddSeconds(21));Require(b.Prepared.Links==null&&DefilerHealthLink.Mark(victim,at.AddSeconds(21))==null,"Original mark expiry did not remove group");
            foreach(var t in targets){if(t!=null&&!t.Deleted){t.MoveToWorld(new(b.X+4+Array.IndexOf(targets,t)%3,b.Y+Array.IndexOf(targets,t)/3,b.Z),b.Map);}}
            Require(DefilerHealthLink.Cast(b,targets[0],at),"Held-clock saved group setup failed");
            b.RestorePrepared(b.Prepared with{Links=b.Prepared.Links with{Marks=b.Prepared.Links.Marks.Select(m=>m with{UntilUtc=at.AddHours(1)}).ToArray()}});
            if(retain)
            {
                // Explicit held expiry/recovery is for original-body load
                // inspection;the separate21second check establishes expiry.
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);
                b.RestorePrepared(b.Prepared with{NextRecoveryUtc=at.AddHours(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,targets.Select(t=>t.Serial.Value).ToArray(),b.SavedText,SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))));kept=true;
            }
            Require(WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Learned Defiler route missing after source host integration");
            Log.Information("Defiler links:own target/cap/expiry,no refresh,typed FP8 sharing/recipient defenses/no recursion,NPC owner credit,actual fire route,range retention,offline suppression and atomic malformed ownership pass");Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){foreach(var t in targets){t?.Delete();}b?.Delete();p.Delete();}
        }
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockDefilerBody;
        var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.Map==Map.Internal&&b.SavedText==s.Text&&SkillHealth.Balance256(b)==s.Life
            &&NpcEquipmentRandom.State(b)==s.Random&&b.Prepared.Links.Marks.All(m=>m.UntilUtc>TimedEffects.Now),$"Original held link mismatch:body={b!=null},owner={b?.ControlMaster?.Serial.Value},map={b?.Map},text={b?.SavedText==s.Text},life={b?.Hits},rng={NpcEquipmentRandom.State(b)==s.Random},marks={b?.Prepared.Links?.Marks.Length}");
        var account=FixtureNetwork.Authenticate(network,p,"defiler-link-reload-");
        try
        {
            PersistentMinions.Resume(p);
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==s.Life,"Original link body did not resume without healing");
            var marks=b.Prepared.Links.Marks;var first=(BaseCreature)World.FindMobile((Serial)marks[0].Target);
            var next=(BaseCreature)World.FindMobile((Serial)marks[1].Target);int life=SkillHealth.Balance256(next);
            SkillCombat.FireImpact(p,first,25600);
            Require(life-SkillHealth.Balance256(next)==25600*marks[0].Percent/100&&b.Prepared.Links.Marks.SequenceEqual(marks)
                &&SkillHealth.Balance256(b)==s.Life,"Original saved link group failed actual damage after login or reset its deadline");
            PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Text=b.SavedText,Random=NpcEquipmentRandom.State(b)}));
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
