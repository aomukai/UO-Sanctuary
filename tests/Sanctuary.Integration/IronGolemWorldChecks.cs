// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class IronGolemWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemWorldChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Item,string Gear,IronGolemWorldState State,ulong Seed,int Life,int Maximum,Point3D Point,int Map);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-world-fixtures.json");
    public static void Configure()
    {
        Register("iron-golem-world-check",Check);Register("iron-golem-world-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("iron-golem-world-live","Connected natural40ms prepared Iron Golem controller",_=>Core.LoopContext.Post(()=>
            FixtureNetwork.Connected("iron-golem-world-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Iron Golem motion/equipment clock;learned cast disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string talent)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Necromancer,talent,out var row),"Missing Necromancer talent");
        foreach(string prior in row.Prerequisites){Train(owner,prior);}
        if(ClassCreation.Profiles.GetRank(owner.Serial,talent)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,talent,out _),"Train "+talent);}
    }
    private static PlayerMobile Fresh()
    {
        var owner=new PlayerMobile{Name="IronWorldOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Train(owner,"necromancer.irongolem");Train(owner,"necromancer.golemmastery");
        FixtureSpace.Corridor(owner,6100,24);return owner;
    }
    private static void End(PlayerMobile owner)
    {foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}owner.Delete();}
    private static Item Original(PlayerMobile owner,string name=null,int dx=1)
    {
        Item item=name==null?new Broadsword():NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name||x.SourceKey=="The "+name).Id,99,new Random(7041),true);
        if(name==null){ClassGear.Profiles.Ensure(item);}item.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return item;
    }
    private static GraveyardZombie Target(PlayerMobile owner,IronGolemBody body)
    {
        var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.SetHits(20000);target.MoveToWorld(body.Location,owner.Map);target.AIObject.AITimer.Stop();
        target.SetResistance(ResistanceType.Physical,0);TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static void Through(IronGolemBody body,int frame,bool actions=true)
    {
        var s=body.Prepared.Controller;Require(frame>s.Frame,"Reverse Iron Golem clock");
        IronGolemWorld.Advance(body,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),actions);
        Require(!body.Deleted&&body.Prepared.Controller.Fault==null,"Iron Golem deleted or faulted");
    }
    private static ulong AttackSeed(IronGolemBody body,BaseCreature target)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(body),ClassStats.Defense(target),ClassStats.Level(body),ClassStats.Level(target));
        for(ulong initial=1;initial<10000;initial++)
        {ulong seed=initial;SourceRandom.Roll(ref seed);if(SourceRandom.Roll(ref seed)%100<80&&SourceRandom.Roll(ref seed)%100/100.0<chance){return initial;}}
        throw new InvalidOperationException("No deterministic melee seed");
    }
    private static void Combat()
    {
        foreach(string name in new string[]{null,"Battlebranch"})
        {
            var owner=Fresh();var original=Original(owner,name);var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),1);
            var target=Target(owner,body);
            try
            {
                ulong seed=AttackSeed(body,target);NpcEquipmentRandom.Set(body,seed);int life=SkillHealth.Balance256(target);
                Through(body,25);var state=body.Prepared.Controller;
                int ias=NpcEquipmentCombat.Bonus(body,"swing1")+NpcEquipmentCombat.Bonus(body,"swing2")+NpcEquipmentCombat.Bonus(body,"swing3");
                int speed=SourceAttackRate.Speed(256,ias,SourceAttackRate.WolfRate(body)-ClassGear.Base(original).Speed);var clock=DruidWolfGeometry.Clock("A1",speed,"irongolem-animation");
                Require(state.Mode==4&&state.ActionAt==25+clock.Action&&state.EndAt==25+clock.End&&state.AttackSpeed==speed
                    &&state.Ai.RememberedOwner==(int)owner.Serial.Value&&state.Attempts==0&&SkillHealth.Balance256(target)==life,
                    "Iron Golem AI/A1 entry/IAS: "+JsonSerializer.Serialize(state));
                ulong expected=seed;SourceRandom.Roll(ref expected);SourceRandom.Roll(ref expected);
                Require(NpcEquipmentRandom.State(body).Seed==expected,"AI used an independent or extra RNG stream");
                if(name==null){Require(speed==256&&state.ActionAt==35&&state.EndAt==41,"Unmodified G4 animation clock");}
                else{Require(ias==30&&ClassGear.Base(original).Speed==10&&speed==291,"Original Battlebranch IAS/base speed not used");}
                Through(body,state.ActionAt-1);Require(SkillHealth.Balance256(target)==life,"Contact before G4 action flag");
                Through(body,state.ActionAt);Require(body.Prepared.Controller is{Attempts:1,Hits:1}&&SkillHealth.Balance256(target)<life
                    &&NpcEquipmentRandom.State(body).Seed!=expected,"Source equipment attack failed at action frame");
                int after=SkillHealth.Balance256(target);Through(body,state.EndAt);
                Require(body.Prepared.Controller is{Mode:1,Attempts:1}&&SkillHealth.Balance256(target)==after,"Repeated attack at animation end");
            }
            finally{target.Delete();End(owner);}
        }
    }
    private static void MotionAndRecovery()
    {
        var owner=Fresh();var original=Original(owner,dx:16);var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),23);
        try
        {
            var start=body.Location;Through(body,150);Require(body.Location!=start&&owner.InRange(body,20),"NecroPet owner-follow did not move");
            body.RestoreController(IronGolemWorld.Park(body.Prepared.Controller) with{NextThink=body.Prepared.Controller.Frame+1000});
            int maximum=SkillHealth.Maximum256(body);SkillHealth.SetBalance256(body,maximum/3+17);int life=SkillHealth.Balance256(body);
            int delta=BattleOrders.Regeneration256(body)+NpcEquipmentRegeneration.ItemRate(body);int frame=body.Prepared.Controller.Frame;
            Through(body,frame+10);Require(SkillHealth.Balance256(body)==life+10*delta,"World recovery omitted or doubled source/item rate");
            var before=body.Prepared;string text=JsonSerializer.Serialize(before);bool rejected=false;
            try{body.RestoreController(before.Controller with{Mode=4,Target=0,EndAt=0});}catch(InvalidDataException){rejected=true;}
            Require(rejected&&JsonSerializer.Serialize(body.Prepared)==text,"Malformed controller restore mutated actor");
            var point=body.Location;life=SkillHealth.Balance256(body);ulong seed=NpcEquipmentRandom.State(body).Seed;
            Through(body,frame+100,false);Require(body.Map==Map.Internal&&body.Prepared.Controller.Mode==1
                &&SkillHealth.Balance256(body)==life&&NpcEquipmentRandom.State(body).Seed==seed,"Offline world healed,acted or rolled");
            Require(point!=Point3D.Zero,"Invalid test pose");
        }
        finally{End(owner);}
    }
    private static void MovementItems()
    {
        foreach(string name in new[]{"Shaftstop","Gorerider"})
        {
            var owner=Fresh();var item=Original(owner,name,16);var body=IronGolemWorld.Create(owner,item,item.Location,TimedEffects.Now.AddHours(1),23);
            try
            {
                Through(body,25);var state=body.Prepared.Controller;
                int faster=NpcEquipmentCombat.Bonus(body,"move1")+NpcEquipmentCombat.Bonus(body,"move2")+NpcEquipmentCombat.Bonus(body,"move3");
                int percent=100+(int)body.BirthValues.Eval("passivecalc1")-ClassGear.Base(item).Speed;
                int expected=SourceActorMotion.Speed(9,112,percent,faster).Velocity;
                Require(state.Mode==2&&state.Motion.Velocity==expected,"Original armor speed/faster-move transport: "+JsonSerializer.Serialize(state));
                Require(name=="Shaftstop"?ClassGear.Base(item).Speed==5:faster==30,"Missing nonzero original movement supplier: "+name+" speed "+ClassGear.Base(item).Speed+" faster "+faster);
                var profile=CreatureProfiles.For(body);Require(profile.Traits.Size==2&&profile.Traits.ChillEffectiveness==50
                    &&profile.Traits.BlockChance==41&&!profile.HitRecovery.Enabled,"Iron Golem inherited a generic size/recovery profile");
            }
            finally{End(owner);}
        }
    }
    private static void Cancellation()
    {
        foreach(bool offline in new[]{false,true})
        {
            var owner=Fresh();var item=Original(owner);var body=IronGolemWorld.Create(owner,item,item.Location,TimedEffects.Now.AddHours(1),1);var target=Target(owner,body);
            try
            {
                NpcEquipmentRandom.Set(body,AttackSeed(body,target));int life=SkillHealth.Balance256(target);Through(body,25);
                if(offline){Through(body,40,false);}
                else{body.InterruptForRecovery();body.Frozen=true;Through(body,40);body.Frozen=false;}
                Require(SkillHealth.Balance256(target)==life&&body.Prepared.Controller is{Mode:1,Attempts:0},"Canceled pending contact still struck");
            }
            finally{target.Delete();End(owner);}
        }
    }
    private static void Stage()
    {
        var owner=Fresh();var item=Original(owner,"Battlebranch");var body=IronGolemWorld.Create(owner,item,item.Location,TimedEffects.Now.AddDays(2),24601);
        SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/3+17);var point=owner.Location;int map=owner.Map.MapID;
        PersistentMinions.Suspend(owner);owner.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,body.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),
            body.Prepared.Controller,NpcEquipmentRandom.State(body).Seed,SkillHealth.Balance256(body),SkillHealth.Maximum256(body),point,map)));
    }
    private static void Check()
    {

        Combat();MotionAndRecovery();MovementItems();Cancellation();Stage();
        Log.Information("Prepared Iron Golem world:G4 action/end clock,original Battlebranch IAS,shared AI/item RNG,ordinary item contact,follow motion,exact recovery,offline/interruption cancellation,atomic restore and saved original/controller/seed pass;complete current attack/events and learned cast remain unfinished");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var body=(IronGolemBody)World.FindMobile((Serial)saved.Pet);var item=World.FindItem((Serial)saved.Item);
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==owner&&body.Map==Map.Internal&&body.Prepared.Controller.Mode==1
            &&body.Prepared.Controller.Attempts==0&&NpcEquipmentRandom.State(body).Seed==saved.Seed&&item.Parent==body
            &&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear&&SkillHealth.Balance256(body)==saved.Life&&SkillHealth.Maximum256(body)==saved.Maximum,
            "Saved Iron Golem world/original/seed/resources changed");
        owner.MoveToWorld(saved.Point,Map.Maps[saved.Map]);IronGolemWorld.Advance(body,body.Prepared.Controller.NextUtc.AddHours(2),false);
        Require(SkillHealth.Balance256(body)==saved.Life&&NpcEquipmentRandom.State(body).Seed==saved.Seed&&body.Prepared.Controller.Attempts==0,"Loaded backlog healed or acted");
        End(owner);Combat();Stage();
    }
    private static void Live(NetState network)
    {
        var owner=Fresh();network.Seeded=true;network.SentFirstPacket=true;network.CompressionEnabled=false;
        var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(field!=null,"Connected Iron Golem protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));
        var account=new Account("iron-world-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));account[0]=owner;
        network.Account=account;network.Mobile=owner;owner.NetState=network;network.ResetMovementState();
        var item=Original(owner);var body=IronGolemWorld.Create(owner,item,item.Location,TimedEffects.Now,1);var target=Target(owner,body);
        NpcEquipmentRandom.Set(body,AttackSeed(body,target));int life=SkillHealth.Balance256(target);long deadline=Core.TickCount+12000;
        void Inspect()
        {
            try
            {
                var state=body.Prepared.Controller;Require(network.Running&&!body.Deleted&&state.Fault==null,"Natural Iron Golem session/controller fault");
                Require(Core.TickCount<deadline,"Natural Iron Golem contact timeout: "+JsonSerializer.Serialize(state));
                if(state.Hits==0){Timer.StartTimer(TimeSpan.FromMilliseconds(40),Inspect);return;}
                Require(SkillHealth.Balance256(target)<life&&state.Attempts>0&&state.Ai.RememberedOwner==(int)owner.Serial.Value,"Natural controller did not deliver item attack");
                target.Delete();End(owner);account.Delete();Log.Information("iron-golem-world-live-result: passed");
            }
            catch(Exception e){target.Delete();End(owner);account.Delete();Log.Error(e,"iron-golem-world-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>{Log.Information("iron-golem-world-live: ready");Inspect();});
    }
}
