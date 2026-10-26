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

public static class SpiritWolfCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfCastingChecks));
    private sealed record Saved(uint Owner,uint[] Wolves,long[] Orders,string[] States,uint Pending,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spirit-wolf-casting-fixtures.json");
    private static bool _creating;private static int _scene;
    [CallPriority(int.MaxValue)]public static void Initialize()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("WolfCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Register("spirit-wolf-casting-check",Check);Register("spirit-wolf-casting-reload-check",Reload);
        Register("spirit-wolf-right-check",()=>FixtureNetwork.Connected("spirit-wolf-right-result",n=>Input(n,false)));
        Register("spirit-wolf-quick-check",()=>FixtureNetwork.Connected("spirit-wolf-quick-result",n=>Input(n,true)));
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Learned Spirit Wolf casting and input",_=>Core.LoopContext.Post(()=>
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
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="WolfCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        if(trained){Train(p,"druid.summonspiritwolf");}
        FixtureSpace.Corridor(p,7040,20,1200+32*_scene++);Mana(p);return p;
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static DruidSpiritWolfBody[] Wolves(PlayerMobile p)=>DruidSpiritWolves.For(p).OrderBy(BattleCommand.PetOrder).ToArray();
    private static void Drop(PlayerMobile p){foreach(var b in Wolves(p)){b.Delete();}foreach(var b in DruidRavens.For(p)){b.Delete();}p.Delete();}
    private static DruidSpiritWolfBody Cast(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);long order=Wolves(p).Select(BattleCommand.PetOrder).DefaultIfEmpty().Max();
        Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,target,aim??target?.Location??new(p.X+2,p.Y,p.Z),Core.TickCount),"Wolf start");
        var s=ClassCombat.For(p);Require(s.PendingAbility==CombatAbility.SummonSpiritWolf && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Unpaid SC windup");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early release");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-15*256,"Exactly15mana per release");
        return Wolves(p).Single(x=>BattleCommand.PetOrder(x)>order);
    }
    private static void Cancellation()
    {
        var p=Fresh(false);
        try
        {
            Require(!ClassCombat.Available(p,CombatAbility.SummonSpiritWolf) && !ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,p.Location,Core.TickCount),"Unlearned cast");
            Train(p,"druid.summonspiritwolf");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.SummonSpiritWolf) && SkillBindings.Instance.Assign(p,false,CombatAbility.SummonSpiritWolf)
                && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonSpiritWolf),"Right/quick identity");
            Ready(p);NecromancerEffects.SetMana256(p,3839);Require(!ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Fractionally insufficient mana");
            for(int mode=0;mode<6;mode++)
            {
                Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
                Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Cancellation start");long when=ClassCombat.For(p).EffectAt;
                if(mode==0){ClassCombat.Stop(p);}
                if(mode==1){var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location==origin,"Return step");}
                if(mode==2){NecromancerEffects.SetMana256(p,3839);}
                if(mode==3){Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery cancellation");}
                if(mode==4){p.Frozen=true;}
                if(mode==5){var old=p.Map;p.MoveToWorld(p.Location,Map.Felucca);p.MoveToWorld(p.Location,old);}
                Require(!ClassCombat.ResolveAction(p,when) && Wolves(p).Length==0 && NecromancerEffects.ManaBalance256(p)==(mode==2?3839:mana),"Cancelled cast paid or spawned: "+mode);
            }
            Ready(p);Mana(p);Require(!ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,new(65530,65530,0),Core.TickCount),"Invalid point");
            NecromancerEffects.SetMana256(p,3840);Cast(p);Require(NecromancerEffects.ManaBalance256(p)==0,"Exact low mana");
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
            var first=Cast(p,a);Require(first.Prepared is{LearnedCast:true,Snapshot.Birth.Rank:2} && first.Map==p.Map && first.InRange(a,2),"Target adjacent ground/soft rank");
            first.RestorePrepared(first.Prepared with{Controller=first.Prepared.Controller with{Target=a.Serial.Value}});
            var second=Cast(p,b);Require(!first.Deleted && first.Prepared.Controller.Target==a.Serial.Value && Wolves(p).Length==2,"New cast redirected existing wolf");
            Ready(p);Mana(p);Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,b,b.Location,Core.TickCount),"Moving target start");
            b.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Moving target release");
            Require(first.Deleted && Wolves(p).Last().InRange(b,2),"Live target and FIFO");
            var held=Wolves(p).Select(x=>x.Serial).ToArray();Ready(p);NecromancerEffects.SetMana256(p,3840);
            Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,a,a.Location,Core.TickCount),"Lost-mana start");NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Wolves(p).Select(x=>x.Serial).SequenceEqual(held),"Failed cast replaced wolves");
            var raven=DruidRavenBody.CreatePrepared(p,new(p.X+3,p.Y,p.Z),TimedEffects.Now,123,enforce:false);
            supplier=new PlayerMobile{Body=0x190,Player=true,Name="WolfCastFixtureSupplier"};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);
            BattleCommand.Apply(supplier,p,1,TimedEffects.Now);Mana(p);var newest=Cast(p,a);
            Require(Wolves(p).Length==3 && newest.Prepared.Snapshot.Birth.Rank==3 && !raven.Deleted,"Command cap/coexistence");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);DruidSpiritWolves.Tick(TimedEffects.Now,false);Require(Wolves(p).Length==2,"Gear-loss cap");
            BattleCommand.Clear(p);DruidSpiritWolves.Tick(TimedEffects.Now,false);
            Require(Wolves(p).Length==1 && Wolves(p)[0]==newest && newest.Prepared.Snapshot.Birth.Rank==3 && !raven.Deleted,"Command-loss FIFO/retained birth/coexistence");
            Ready(p);p.Kill();Require(newest.Deleted,"Owner death kept wolf");p.Corpse?.Delete();
        }
        finally{a.Delete();b.Delete();if(supplier!=null){Drop(supplier);}Drop(p);}
    }
    private static void Placement()
    {
        var p=Fresh();var blocks=new List<Item>();
        try
        {
            var aim=new Point3D(p.X+8,p.Y,p.Z);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,null,aim,Core.TickCount),"Placement windup");
            int wall=Enumerable.Range(1,0x2000).First(id=>TileData.ItemTable[id].Impassable && TileData.ItemTable[id].CalcHeight>=16);
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){var item=new Static(wall);item.MoveToWorld(new(aim.X+x,aim.Y+y,aim.Z),p.Map);blocks.Add(item);}
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Wolves(p).Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Failed placement paid/spawned");
            foreach(var item in blocks){item.Delete();}blocks.Clear();
            var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(aim,p.Map);enemy.AIObject.AITimer.Stop();
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.SummonSpiritWolf,enemy,aim,Core.TickCount),"Deleted target windup");enemy.Delete();
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && Wolves(p).Single().InRange(aim,2),"Deleted target fallback");
            foreach(var body in Wolves(p)){body.Delete();}RecognitionChecks.Place(p);
            Require(Server.Spells.SpellHelper.IsTown(p.Location,p),"Town fixture");Mana(p);Cast(p,aim:p.Location);
        }
        finally{foreach(var item in blocks){item.Delete();}Drop(p);}
    }
    private static void Write(PlayerMobile p,PlayerMobile pending)
    {
        foreach(var body in Wolves(p)){body.RestorePrepared(body.Prepared with{Controller=body.Prepared.Controller with{NextUtc=TimedEffects.Now.AddDays(2)}});}
        PersistentMinions.Suspend(p);var wolves=Wolves(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,wolves.Select(x=>x.Serial.Value).ToArray(),wolves.Select(BattleCommand.PetOrder).ToArray(),wolves.Select(x=>x.SavedText).ToArray(),pending.Serial.Value,NecromancerEffects.ManaBalance256(pending),p.Map.MapID,p.Location)));
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.SummonSpiritWolf),"Learned route missing");Cancellation();CapsAndTargets();Placement();
        var p=Fresh();Train(p,"druid.summonspiritwolf");Mana(p);Cast(p);Mana(p);Cast(p,aim:new(p.X+4,p.Y,p.Z));
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.SummonSpiritWolf) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonSpiritWolf),"Saved bindings");
        var pending=Fresh();Ready(pending);Require(ClassCombat.Perform(pending,CombatAbility.SummonSpiritWolf,null,new(pending.X+2,pending.Y,pending.Z),Core.TickCount),"Saved unpaid windup");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Write(p,pending);
        Log.Information("Learned Spirit Wolf:15mana once-only SC, cancellation, live target placement, independent targets, actual gear/Command FIFO and coexistence pass; ground search is an explicit native adapter");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        var wolves=Wolves(p);Require(wolves.Select(x=>x.Serial.Value).SequenceEqual(saved.Wolves) && wolves.Select(BattleCommand.PetOrder).SequenceEqual(saved.Orders)
            && wolves.Select(x=>x.SavedText).SequenceEqual(saved.States) && wolves.All(x=>x.Map==Map.Internal && x.Prepared.LearnedCast),"Saved learned wolf/FIFO/parking");
        Require(Wolves(pending).Length==0 && ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==saved.Mana,"Unpaid windup replayed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.SummonSpiritWolf && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.SummonSpiritWolf,"Saved bindings");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}PersistentMinions.Resume(p);var oldest=wolves[0];Mana(p);Cast(p,aim:new(p.X+6,p.Y,p.Z));
        Require(oldest.Deleted && Wolves(p).Length==2,"FIFO changed after reload");Write(p,pending);
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"spirit-wolf-quick":"spirit-wolf-right";var p=Fresh();Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");
        field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.SummonSpiritWolf) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.SummonSpiritWolf),"Input bindings");
        bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Wolf input timeout");var wolves=Wolves(p);
                if(ClassCombat.For(p).PendingAbility==CombatAbility.SummonSpiritWolf)
                {
                    Require(wolves.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Input unpaid windup");
                    if(!began){Require(quick || ClassCombat.For(p).HeldAbility==CombatAbility.SummonSpiritWolf,"Right hold");began=true;Log.Information("{Label}: windup",label);}
                }
                if(wolves.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began && (quick || ClassCombat.For(p).HeldAbility==CombatAbility.None) && wolves.Length==1 && NecromancerEffects.ManaBalance256(p)==mana-3840
                    && wolves[0].Location==new Point3D(p.X+2,p.Y,p.Z),"Real input payment/placement");Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.SummonSpiritWolf,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Inspect();});
    }
}
