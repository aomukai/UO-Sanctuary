// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcItemChainRootChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemChainRootChecks));
    private sealed record Held(long Cast,uint Target,uint Peer,int TargetLife,int PeerLife);
    private sealed record Saved(uint Owner,uint Actor,Point3D Point,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources,
        Held[] Held,ChainFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-chain-root-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static ChainFlight[] Own(AmazonValkyrieBody b)=>ChainLightning.Snapshot().Where(f=>f.OriginalNpc?.Source.Actor==b.Serial.Value).ToArray();
    private static string[] Items(AmazonValkyrieBody b)=>ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray();
    public static void Configure(){Register("npc-item-chain-root-check",false);Register("npc-item-chain-root-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Actual item do26/151 root origins,budget,per-hop allocation and saves",commandLine=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static NpcItemSpellEmission Emit(AmazonValkyrieBody b,NpcItemTarget target,Point3D path,int rank,int flag,DateTime at,bool failed=false)
    {
        string before=Json(Own(b)),body=b.SavedText,items=Json(Items(b)),resources=Json(GeneratedNpcLiveResources.State(b));var random=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b),calls=0;
        var point=target?.Point(b.Map);var outcome=ChainLightning.EmitValkyrieItem(b,target,path,rank,flag,at,r=>
        {
            Require(++calls==1&&r.Skill==53&&r.Rank==rank&&r.Missile==93&&r.Flags==(33|(b.EquipmentNumbers.Read(19)!=0?0x1000:0))&&r.AttackRating==b.EquipmentNumbers.Read(19),"Item Chain allocator lost own metadata/AR");
            var origin=flag==1&&point is{} p?p:b.Location;Require(r.X==origin.X*2&&r.Y==origin.Y*2&&Json(Own(b))==before,"Item Chain root published early or used ordinary origin");return new(!failed,SourceRandom.InitLow(7100));
        });
        Require(outcome.Result==(failed?0:1)&&outcome.Attempts==1&&calls==1&&outcome.Flights.Length==(failed?0:1),"Item Chain return/allocation count differs");
        Require(b.SavedText==body&&Json(Items(b))==items&&Json(GeneratedNpcLiveResources.State(b))==resources&&NpcEquipmentRandom.State(b)==random&&SkillHealth.Balance256(b)==life,"Item Chain creation mutated original caster");
        if(!failed)
        {
            var root=Own(b).Single(f=>f.Cast==outcome.Flights[0]);var origin=flag==1&&point is{} p?p:b.Location;
            Require(root.X==origin.X&&root.Y==origin.Y&&root.Remaining==(rank+25)/5&&root.OriginalNpc.Ordinal==0&&root.OriginalNpc.Seed==SourceRandom.InitLow(7100),"Item Chain budget/seed/origin was not applied to actual root");
            if(flag==1&&point!=null){Require(root.Vx>0&&root.Vy>0&&Math.Abs(root.Vx-root.Vy)<1e-10,"Item Chain lost source factory zero-vector correction");}
        }
        return outcome;
    }
    private static NpcChainAllocation Child(NpcChainRequest r,List<NpcItemChainChecks.Contact> contacts,bool failed=false)
    {
        Require(r.Ordinal>=1&&r.Skill==53&&r.Rank==3&&r.Remaining==5-r.Ordinal&&r.Flags==33&&contacts.Count==r.Ordinal-1,"Item Chain child lost retained budget or allocated after HP");
        return new(!failed,SourceRandom.InitLow(7100+r.Ordinal));
    }
    private static void Controls(AmazonValkyrieBody b,PlayerMobile p)
    {
        foreach(int flag in new[]{0,1})foreach(int mode in new[]{0,1,2,3})
        {
            NpcItemChainChecks.Place(b,p);var first=NpcItemChainChecks.Target(b,8);var peer=mode==0?null:NpcItemChainChecks.Target(b,14);var targets=peer==null?new[]{first}:new[]{first,peer};
            try
            {
                var made=Emit(b,NpcItemTarget.Capture(first),first.Location,3,flag,TimedEffects.Now.AddMinutes(10),mode==3);var seed=NpcEquipmentRandom.State(b);var hp=targets.Select(SkillHealth.Balance256).ToArray();var contacts=new List<NpcItemChainChecks.Contact>();
                if(mode==3){Require(Own(b).Length==0,"Failed item root emitted");continue;}
                b.MoveToWorld(new(b.X,b.Y+3,b.Z),b.Map);NpcItemChainChecks.Through(b,made.Flights[0],contacts,r=>Child(r,contacts,mode==2));
                int expected=mode==1?5:1;Require(contacts.Count==expected&&Own(b).Length==0&&contacts[0].Target==first.Serial.Value,"Item Chain incoming/outgoing lone/bounce/failure contacts differ");
                NpcItemChainChecks.Verify(b,seed,contacts,targets,hp);
            }
            finally{ChainLightning.Cancel(b);foreach(var t in targets){t.Delete();}}
        }
        NpcItemChainChecks.Place(b,p);var target=NpcItemChainChecks.Target(b,8);
        try
        {
            foreach(int rank in Enumerable.Range(1,63))foreach(int flag in new[]{0,1})
            {Emit(b,NpcItemTarget.Capture(target),target.Location,rank,flag,TimedEffects.Now.AddMinutes(10));ChainLightning.Cancel(b);}
            Emit(b,null,new(b.X+12,b.Y,b.Z),3,1,TimedEffects.Now.AddMinutes(10));var root=Own(b).Single();Require(root.X==b.X&&root.Y==b.Y&&root.Vx>0&&root.Vy==0,"Targetless item Chain invented a target origin");ChainLightning.Cancel(b);
            int allocations=0;var rng=NpcEquipmentRandom.State(b);var empty=ChainLightning.EmitValkyrieItem(b,null,Point3D.Zero,3,1,TimedEffects.Now,_=>{allocations++;return new(true,1);});
            Require(empty.Result==0&&empty.Attempts==0&&allocations==0&&Own(b).Length==0,"Zero-path Chain allocated or claimed success");
            var distant=ChainLightning.EmitValkyrieItem(b,null,new(b.X+60,b.Y,b.Z),3,0,TimedEffects.Now,_=>{allocations++;return new(true,1);});
            Require(distant.Result==0&&distant.Attempts==1&&allocations==0&&NpcEquipmentRandom.State(b)==rng,"Chain source factory range guard drew RNG");
        }
        finally{ChainLightning.Cancel(b);target.Delete();}
    }
    private static Held Hold(AmazonValkyrieBody b,int ordinal,DateTime at)
    {
        var first=NpcItemChainChecks.Target(b,8);var peer=NpcItemChainChecks.Target(b,14);var contacts=new List<NpcItemChainChecks.Contact>();
        var made=Emit(b,NpcItemTarget.Capture(first),first.Location,3,1,at);long cast=made.Flights[0];
        if(ordinal==1)
        {for(int i=0;i<25&&Own(b).Single(f=>f.Cast==cast).OriginalNpc.Ordinal==0;i++){NpcItemChainChecks.Step(b,cast,contacts,r=>Child(r,contacts));}Require(contacts.Count==1,"No actual item Chain child to save");}
        first.Internalize();peer.Internalize();NpcItemChainChecks.Step(b,cast,contacts,r=>Child(r,contacts));
        var saved=Own(b).Single(f=>f.Cast==cast);Require(saved.OriginalNpc.Ordinal==ordinal&&saved.Frame==1,"Partial item root/child phase differs");
        return new(cast,first.Serial.Value,peer.Serial.Value,SkillHealth.Balance256(first),SkillHealth.Balance256(peer));
    }
    private static void Check(bool reload)
    {
        PlayerMobile p;AmazonValkyrieBody b;Point3D point;int generation=0;
        if(reload)
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra item Chain reload");p=World.FindMobile((Serial)s.Owner)as PlayerMobile;b=World.FindMobile((Serial)s.Actor)as AmazonValkyrieBody;point=s.Point;
            Require(p is{Alive:true,Deleted:false}&&b is{Alive:true,Deleted:false}&&b.SavedText==s.Body&&Json(Items(b))==Json(s.Items)&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life&&Json(GeneratedNpcLiveResources.State(b))==s.Resources&&Json(Own(b))==Json(s.Flights),"Item Chain actual root/child/seed/items/resources/wounds changed on reload");
            p.MoveToWorld(point,Map.Trammel);NpcItemChainChecks.Place(b,p);
            foreach(var h in s.Held)
            {
                var first=(NpcItemChainChecks.Recipient)World.FindMobile((Serial)h.Target);var peer=(NpcItemChainChecks.Recipient)World.FindMobile((Serial)h.Peer);var contacts=new List<NpcItemChainChecks.Contact>();var old=Own(b).Single(f=>f.Cast==h.Cast);var seed=NpcEquipmentRandom.State(b);
                NpcItemChainChecks.Step(b,h.Cast,contacts,r=>Child(r,contacts));var aged=Own(b).Single(f=>f.Cast==h.Cast);
                Require(aged.X==old.X&&aged.Y==old.Y&&aged.Frame==old.Frame+1&&contacts.Count==0&&NpcEquipmentRandom.State(b)==seed,"Loaded item Chain replayed old travel/HP/RNG");
                var nextPoint=old.OriginalNpc.Ordinal==0?new Point3D((int)Math.Round(aged.X+aged.Vx),(int)Math.Round(aged.Y+aged.Vy),b.Z):new(b.X+8,b.Y,b.Z);
                first.MoveToWorld(nextPoint,b.Map);peer.MoveToWorld(new(nextPoint.X+6,nextPoint.Y,b.Z),b.Map);Require(SkillHealth.Balance256(first)==h.TargetLife&&SkillHealth.Balance256(peer)==h.PeerLife,"Held item target wounds changed");
                int ordinal=old.OriginalNpc.Ordinal;NpcItemChainChecks.Through(b,h.Cast,contacts,r=>
                {Require(contacts.Count==r.Ordinal-ordinal-1,"Loaded child allocation happened after HP");return new(true,SourceRandom.InitLow(7100+r.Ordinal));});
                Require(contacts.Count==5-ordinal,"Saved item Chain did not finish retained budget");NpcItemChainChecks.Verify(b,seed,contacts,[first,peer],[h.TargetLife,h.PeerLife]);first.Delete();peer.Delete();
            }
        }
        else
        {
            p=new PlayerMobile{Name="NpcItemChainRootFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6800,24,3370);point=p.Location;
            using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var row=source.RootElement.EnumerateArray().First(x=>x.GetProperty("Critical").GetInt32()==1&&x.GetProperty("Rank").GetInt32()==20);
            b=ValkyrieDamageChecks.Body(p,row);ValkyrieEquipmentResources.Activate(b,0,0,0,0,300);NpcItemChainChecks.Place(b,p);
        }
        Controls(b,p);NpcItemChainChecks.Place(b,p);var at=TimedEffects.Now.AddMinutes(10);var held=new[]{Hold(b,0,at),Hold(b,1,at)};var flights=Own(b);Require(flights.Length==2,"Saved item Chain root/child count");
        var random=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);string resources=Json(GeneratedNpcLiveResources.State(b));PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,point,b.SavedText,Items(b),random,life,resources,held,flights,generation)));
        Require(!ClassCombat.Available(p,CombatAbility.Valkyrie),"Item Chain enabled learned casting");
        Log.Information("NPC item Chain roots:do26/151 actual factory origins/ownAR/zero-vector,budget after successful handle,126rank/flag roots,lone/five-hop/failure and targetless/range guards;shared per-hop beforeHP/exact HP/RNG;partial actual item root+child/original items/resources/wounds through two reloads;full wrapper/proc/game stream remain separate");
    }
}
