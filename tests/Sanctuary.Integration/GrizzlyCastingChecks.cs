// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class GrizzlyCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrizzlyCastingChecks));
    private sealed record Saved(uint Owner,uint[] Bears,long[] Orders,string[] States,uint Pending,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grizzly-casting-fixtures.json");
    private static bool _creating;private static int _scene;
    [CallPriority(int.MaxValue)]public static void Initialize()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("GrizzlyCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Register("grizzly-casting-check",Check);Register("grizzly-casting-reload-check",Reload);
        Register("grizzly-right-check",()=>FixtureNetwork.Connected("grizzly-right-result",n=>Input(n,false)));
        Register("grizzly-quick-check",()=>FixtureNetwork.Connected("grizzly-quick-result",n=>Input(n,true)));
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Learned Grizzly casting and input",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id)
    {
        TalentCatalog.TryGet(DiabloClass.Druid,id,out var def);
        foreach(var prior in def.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior);}}
        Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);
    }
    private static PlayerMobile Fresh(bool trained=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="GrizzlyCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        if(trained){Train(p,"druid.summongrizzly");}
        FixtureSpace.Corridor(p,6200,20,1200+32*_scene++);Mana(p);return p;
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static DruidGrizzlyBody[] Bears(PlayerMobile p)=>DruidGrizzlies.For(p).OrderBy(BattleCommand.PetOrder).ToArray();
    private static void Drop(PlayerMobile p){PersistentMinions.Dismiss(p);p.Delete();}
    private static DruidGrizzlyBody Cast(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);long order=Bears(p).Select(BattleCommand.PetOrder).DefaultIfEmpty().Max();
        Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,target,aim??target?.Location??new(p.X+2,p.Y,p.Z),Core.TickCount),"Grizzly start");
        var s=ClassCombat.For(p);Require(s.PendingAbility==CombatAbility.SummonGrizzly && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Unpaid SC windup");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early release");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-40*256,"Exactly40mana per release");
        return Bears(p).Single(x=>BattleCommand.PetOrder(x)>order);
    }
    private static void Cancellation()
    {
        var p=Fresh(false);
        try
        {
            Require(!ClassCombat.Available(p,CombatAbility.SummonGrizzly) && !ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,p.Location,Core.TickCount),"Unlearned cast");
            Train(p,"druid.summongrizzly");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.SummonGrizzly) && SkillBindings.Instance.Assign(p,false,CombatAbility.SummonGrizzly)
                && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonGrizzly),"Right/quick identity");
            Ready(p);NecromancerEffects.SetMana256(p,10239);Require(!ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Fractionally insufficient mana");
            for(int mode=0;mode<6;mode++)
            {
                Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
                Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Cancellation start");long when=ClassCombat.For(p).EffectAt;
                if(mode==0){ClassCombat.Stop(p);}
                if(mode==1){var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location==origin,"Return step");}
                if(mode==2){NecromancerEffects.SetMana256(p,10239);}
                if(mode==3){Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery cancellation");}
                if(mode==4){p.Frozen=true;}
                if(mode==5){var old=p.Map;p.MoveToWorld(p.Location,Map.Felucca);p.MoveToWorld(p.Location,old);}
                Require(!ClassCombat.ResolveAction(p,when) && Bears(p).Length==0 && NecromancerEffects.ManaBalance256(p)==(mode==2?10239:mana),"Cancelled cast paid or spawned: "+mode);
            }
            Ready(p);Mana(p);Require(!ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,new(65530,65530,0),Core.TickCount),"Invalid point");
            NecromancerEffects.SetMana256(p,10240);Cast(p);Require(NecromancerEffects.ManaBalance256(p)==0,"Exact low mana");
        }
        finally{Drop(p);}
    }
    private static void CapsAndTargets()
    {
        var p=Fresh();var a=new GraveyardZombie{CantWalk=true,NoKillAwards=true};var b=new GraveyardZombie{CantWalk=true,NoKillAwards=true};
        PlayerMobile supplier=null;
        try
        {
            a.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);a.AIObject.AITimer.Stop();b.AIObject.AITimer.Stop();
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Tarnhelm equip");ClassGear.Refresh(p);
            var first=Cast(p,a);Require(first.Prepared is{LearnedCast:true,Snapshot.Birth.Rank:2}&&first.Map==p.Map&&first.InRange(a,2),"Target adjacent ground/soft rank");
            var second=Cast(p,b);Require(first.Deleted&&Bears(p).Length==1&&second.InRange(b,2),"Second paid bear did not replace cap1oldest");
            Ready(p);Mana(p);Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,b,b.Location,Core.TickCount),"Moving target start");
            b.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Moving target release");
            Require(second.Deleted&&Bears(p).Single().InRange(b,2),"Live target/cap1replacement");
            var held=Bears(p).Select(x=>x.Serial).ToArray();Ready(p);NecromancerEffects.SetMana256(p,10240);
            Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,a,a.Location,Core.TickCount),"Lost-mana start");NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&Bears(p).Select(x=>x.Serial).SequenceEqual(held),"Failed cast replaced paid bear");
            var raven=DruidRavenBody.CreatePrepared(p,new(p.X+3,p.Y,p.Z),TimedEffects.Now,123,enforce:false);
            supplier=new PlayerMobile{Body=0x190,Player=true,Name="GrizzlyCastFixtureSupplier"};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            BattleCommand.Apply(supplier,p,1,TimedEffects.Now);Mana(p);var newest=Cast(p,a);
            Require(Bears(p).Length==1&&newest.Prepared.Snapshot.Birth.Rank==3&&!raven.Deleted,"Command changed cap1or coexistence");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);DruidGrizzlies.Tick(TimedEffects.Now,false);
            BattleCommand.Clear(p);DruidGrizzlies.Tick(TimedEffects.Now,false);
            Require(Bears(p).Length==1&&Bears(p)[0]==newest&&newest.Prepared.Snapshot.Birth.Rank==3&&!raven.Deleted,"Gear/Command loss changed cap1or copied birth");
            Ready(p);p.Kill();Require(newest.Deleted,"Owner death retained learned bear");p.Corpse?.Delete();
        }
        finally{a.Delete();b.Delete();if(supplier!=null){Drop(supplier);}Drop(p);}
    }
    private static void Placement()
    {
        var p=Fresh();var blocks=new List<Item>();
        try
        {
            var aim=new Point3D(p.X+8,p.Y,p.Z);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,null,aim,Core.TickCount),"Placement windup");
            int wall=Enumerable.Range(1,0x2000).First(id=>TileData.ItemTable[id].Impassable && TileData.ItemTable[id].CalcHeight>=16);
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){var item=new Static(wall);item.MoveToWorld(new(aim.X+x,aim.Y+y,aim.Z),p.Map);blocks.Add(item);}
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Bears(p).Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Failed placement paid/spawned");
            foreach(var item in blocks){item.Delete();}blocks.Clear();
            var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(aim,p.Map);enemy.AIObject.AITimer.Stop();
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.SummonGrizzly,enemy,aim,Core.TickCount),"Deleted target windup");enemy.Delete();
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Bears(p).Single().InRange(aim,2),"Deleted target fallback");
            foreach(var body in Bears(p)){body.Delete();}RecognitionChecks.Place(p);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p),"Town fixture");Mana(p);Cast(p,target:p,aim:p.Location);
        }
        finally{foreach(var item in blocks){item.Delete();}Drop(p);}
    }
    private static void Transactions()
    {
        var p=Fresh();var original=DruidGrizzlyCasting.Spawn;
        try
        {
            var first=Cast(p);var before=Bears(p).Select(x=>(x.Serial,BattleCommand.PetOrder(x))).ToArray();
            foreach(int mode in new[]{0,1,2,3})
            {
                Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);DruidGrizzlyBody allocated=null;
                DruidGrizzlyCasting.Spawn=(owner,point,at,seed)=>mode switch
                {
                    0=>null,
                    1=>throw new ArgumentException("Supplied failed allocation"),
                    2=>throw new NotSupportedException("Supplied unsupported summon context"),
                    _=>PaymentFailure(owner,point,at,seed)
                };
                DruidGrizzlyBody PaymentFailure(PlayerMobile owner,Point3D point,DateTime at,ulong seed)
                {allocated=original(owner,point,at,seed);NecromancerEffects.SetMana256(owner,0);return allocated;}
                Require(!DruidGrizzlyCasting.Commit(p,Serial.Zero,new(p.X+4,p.Y,p.Z),TimedEffects.Now,out var body) && body==null
                    && !first.Deleted && Bears(p).Select(x=>(x.Serial,BattleCommand.PetOrder(x))).SequenceEqual(before)
                    && NecromancerEffects.ManaBalance256(p)==(mode==3?0:mana) && (allocated==null || allocated.Deleted),"Allocation/payment failure replaced a paid wolf or leaked a body: "+mode);
            }
        }
        finally{DruidGrizzlyCasting.Spawn=original;Drop(p);}
    }
    private static void MaximumAndFamilies()
    {
        var p=Fresh();Train(p,"druid.summongrizzly");Train(p,"druid.summongrizzly");Train(p,"druid.oaksage");Train(p,"druid.poisoncreeper");
        try
        {
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Rank4actual helm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.SummonGrizzly)==4,"Hard3/soft1scene");
            Ready(p);Mana(p);Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,new(p.X+4,p.Y,p.Z),Core.TickCount)
                &&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Learned Spirit Wolf coexistence");
            var spiritWolf=DruidSpiritWolves.For(p).Single();Mana(p);
            Require(DruidDireWolfCasting.Commit(p,Serial.Zero,new(p.X+5,p.Y,p.Z),TimedEffects.Now,out var dire),"Learned Dire Wolf coexistence");
            var raven=DruidRavenBody.CreatePrepared(p,new(p.X+6,p.Y,p.Z),TimedEffects.Now,123,enforce:false);
            var creeper=DruidCreeperBody.CreatePrepared(p,new(p.X+7,p.Y,p.Z),TimedEffects.Now);
            Mana(p);Require(DruidOakCasting.Commit(p,Serial.Zero,new(p.X+8,p.Y,p.Z),TimedEffects.Now,out var oak),"Oak coexistence");
            var captured=spiritWolf.Prepared.Snapshot;var direBirth=dire.Prepared.Snapshot;Mana(p);var first=Cast(p);DruidGrizzlyBody newest=first;
            for(int i=0;i<3;i++){Mana(p);newest=Cast(p);}
            Require(first.Deleted&&Bears(p).Length==1&&newest.Prepared.Snapshot.Birth.Rank==4
                &&!spiritWolf.Deleted&&spiritWolf.Prepared.Snapshot==captured&&!dire.Deleted&&dire.Prepared.Snapshot==direBirth
                &&!raven.Deleted&&!creeper.Deleted&&!oak.Deleted,"Cap1at rank4or distinct summon-family coexistence");
            DruidGrizzlies.Tick(TimedEffects.Now,false);Require(Bears(p).Single()==newest,"Steady cap1removed newest");
        }
        finally{Drop(p);}
    }
    private static void OfflinePending()
    {
        var p=Fresh();
        try
        {
            var bear=Cast(p);var before=bear.Prepared.Controller;int life=SkillHealth.Balance256(bear);
            DruidGrizzlyWorld.Advance(bear,before.NextUtc.AddMilliseconds(500*40L),false);
            Require(bear.Map==Map.Internal&&bear.Prepared.Controller is{Mode:1,Sequence:null}
                &&bear.Prepared.Controller.Seed==before.Seed&&SkillHealth.Balance256(bear)==life,"Offline learned bear replayed actions,healing or RNG");
        }
        finally{Drop(p);}
    }
    private static void Write(PlayerMobile p,PlayerMobile pending)
    {
        foreach(var body in Bears(p)){var due=TimedEffects.Now.AddDays(2);body.RestorePrepared(body.Prepared with{NextRecoveryUtc=due,Controller=body.Prepared.Controller with{NextUtc=due}});}
        PersistentMinions.Suspend(p);var wolves=Bears(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,wolves.Select(x=>x.Serial.Value).ToArray(),wolves.Select(BattleCommand.PetOrder).ToArray(),wolves.Select(x=>x.SavedText).ToArray(),pending.Serial.Value,NecromancerEffects.ManaBalance256(pending),p.Map.MapID,p.Location)));
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonGrizzly),"Learned route missing");Cancellation();CapsAndTargets();Placement();Transactions();MaximumAndFamilies();OfflinePending();
        var p=Fresh();Train(p,"druid.summongrizzly");Mana(p);Cast(p);Mana(p);Cast(p,aim:new(p.X+4,p.Y,p.Z));
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.SummonGrizzly) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonGrizzly),"Saved bindings");
        var pending=Fresh();Ready(pending);Require(ClassCombat.Perform(pending,CombatAbility.SummonGrizzly,null,new(pending.X+2,pending.Y,pending.Z),Core.TickCount),"Saved unpaid windup");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Write(p,pending);
        Log.Information("Learned Grizzly:40mana once-only SC,cancellation,allocation/payment rollback,live/ground/self/town/deleted targets,actual gear/Command cap1,learned Spirit/Dire Wolf,Raven/vine/spirit coexistence,offline skip and saved bindings pass; ground search and remaining event/geometry/item-only limits stay explicit");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        var wolves=Bears(p);Require(wolves.Select(x=>x.Serial.Value).SequenceEqual(saved.Bears) && wolves.Select(BattleCommand.PetOrder).SequenceEqual(saved.Orders)
            && wolves.Select(x=>x.SavedText).SequenceEqual(saved.States) && wolves.All(x=>x.Map==Map.Internal && x.Prepared.LearnedCast),"Saved learned bear/FIFO/parking");
        Require(Bears(pending).Length==0 && ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==saved.Mana,"Unpaid windup replayed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.SummonGrizzly && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.SummonGrizzly,"Saved bindings");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}PersistentMinions.Resume(p);var oldest=wolves[0];Mana(p);Cast(p,aim:new(p.X+6,p.Y,p.Z));
        Require(oldest.Deleted && Bears(p).Length==1,"Cap1replacement changed after reload");Write(p,pending);
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"grizzly-quick":"grizzly-right";var p=Fresh();Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");
        field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.SummonGrizzly) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonGrizzly),"Input bindings");
        bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Grizzly input timeout");var wolves=Bears(p);
                if(ClassCombat.For(p).PendingAbility==CombatAbility.SummonGrizzly)
                {
                    Require(wolves.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Input unpaid windup");
                    if(!began){Require(quick || ClassCombat.For(p).HeldAbility==CombatAbility.SummonGrizzly,"Right hold");began=true;Log.Information("{Label}: windup",label);}
                }
                if(wolves.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began && (quick || ClassCombat.For(p).HeldAbility==CombatAbility.None) && wolves.Length==1 && NecromancerEffects.ManaBalance256(p)==mana-10240
                    && wolves[0].Location==new Point3D(p.X+2,p.Y,p.Z),"Real input payment/placement");Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.SummonGrizzly,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Inspect();});
    }
}
