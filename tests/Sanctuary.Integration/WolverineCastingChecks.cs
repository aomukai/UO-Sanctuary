// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class WolverineCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WolverineCastingChecks));
    private sealed record Saved(uint Owner,uint Body,uint Wolf,uint Raven,OakBodyState State,int Mana,uint Pending,int PendingMana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"wolverine-casting-fixtures.json");private static bool _creating;private static int _scene;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("wolverine-casting-check",Check);Register("wolverine-casting-reload-check",Reload);
        Register("wolverine-right-check",()=>FixtureNetwork.Connected("wolverine-right-result",n=>Input(n,false)));
        Register("wolverine-quick-check",()=>FixtureNetwork.Connected("wolverine-quick-result",n=>Input(n,true)));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var prior=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateMana=p=>!(_creating||p.Name?.StartsWith("WolverineCastFixture",StringComparison.Ordinal)==true)&&prior?.Invoke(p)!=false;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Learned Wolverine casting/payment/shared-spirit/input",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh(bool train=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="WolverineCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.oaksage",out _),"Heart prerequisite Oak");
        if(train){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.heartofwolverine",out _),"Train Heart");}
        FixtureSpace.Corridor(p,6600,24,1000+32*_scene++);Full(p);return p;
    }
    private static void Full(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void End(PlayerMobile p)
    {foreach(var pet in p.AllFollowers??[]){DruidWolverineAuras.Clear(pet);DruidOakAuras.Clear(pet);}DruidWolverineAuras.Clear(p);DruidOakAuras.Clear(p);PersistentMinions.Dismiss(p);p.Corpse?.Delete();p.Delete();}
    private static bool Commit(PlayerMobile p,Serial selected,Point3D cached,DateTime at,out DruidOakBody body)
        =>DruidOakCasting.Commit(p,selected,cached,at,out body,CombatAbility.HeartOfWolverine);
    private static DruidOakBody Cast(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.HeartOfWolverine).Mana256;
        Require(ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,target,aim??target?.Location??new(p.X+3,p.Y,p.Z),Core.TickCount),"Wolverine begin");
        var action=ClassCombat.For(p);Require(action.Animation==16&&NecromancerEffects.ManaBalance256(p)==mana,"Unpaid SC windup");
        Require(!ClassCombat.ResolveAction(p,action.EffectAt-1),"Early Wolverine release");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,action.EffectAt)&&!ClassCombat.ResolveAction(p,action.EffectAt),"Once-only Wolverine release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&DruidSpirits.Hearts(p).Length==1,"Wolverine payment/cap");return DruidSpirits.Hearts(p).Single();
    }
    private static void CancellationAndAtomicity()
    {
        var p=Fresh(false);var spawn=DruidOakCasting.WolverineSpawn;
        try
        {
            Require(!ClassCombat.Available(p,CombatAbility.HeartOfWolverine),"Unlearned Wolverine available");Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.heartofwolverine",out _),"Train cancellation fixture");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.HeartOfWolverine)&&SkillBindings.Instance.Assign(p,false,CombatAbility.HeartOfWolverine)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HeartOfWolverine),"Wolverine right/quick bindings");
            for(int mode=0;mode<6;mode++)
            {
                Ready(p);Full(p);int mana=NecromancerEffects.ManaBalance256(p);
                Require(ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Cancellation begin");long when=ClassCombat.For(p).EffectAt;
                if(mode==0){ClassCombat.Stop(p);}if(mode==1){var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location==origin,"Return step");}
                if(mode==2){NecromancerEffects.SetMana256(p,5119);}if(mode==3){Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery interrupt");}
                if(mode==4){p.Frozen=true;}if(mode==5){var map=p.Map;p.MoveToWorld(p.Location,Map.Felucca);p.MoveToWorld(p.Location,map);}
                Require(!ClassCombat.ResolveAction(p,when)&&DruidSpirits.Hearts(p).Length==0&&NecromancerEffects.ManaBalance256(p)==(mode==2?5119:mana),"Cancelled Wolverine paid/spawned: "+mode);
            }
            Ready(p);NecromancerEffects.SetMana256(p,5119);Require(!ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,null,p.Location,Core.TickCount),"Fractional low mana start");
            NecromancerEffects.SetMana256(p,5120);var old=Cast(p);Require(NecromancerEffects.ManaBalance256(p)==0,"Exact20mana");SkillHealth.SetBalance256(old,1201);Full(p);int held=NecromancerEffects.ManaBalance256(p);
            DruidOakCasting.WolverineSpawn=(_,_,_,_)=>null;
            Require(!Commit(p,Serial.Zero,new(p.X+4,p.Y,p.Z),TimedEffects.Now,out _)&&!old.Deleted&&SkillHealth.Balance256(old)==1201&&NecromancerEffects.ManaBalance256(p)==held,"Failed allocation paid/replaced/healed");
            DruidOakBody failed=null;DruidOakCasting.WolverineSpawn=(owner,point,at,seed)=>{failed=spawn(owner,point,at,seed);NecromancerEffects.SetMana256(owner,5119);return failed;};
            Require(!Commit(p,Serial.Zero,new(p.X+4,p.Y,p.Z),TimedEffects.Now,out _)&&failed is{Deleted:true}&&!old.Deleted&&SkillHealth.Balance256(old)==1201,"Failed payment replaced old spirit");
            DruidOakCasting.WolverineSpawn=spawn;Full(p);var next=Cast(p);Require(old.Deleted&&!next.Deleted,"Successful paid replacement failed");
            Ready(p);held=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,null,new(65530,65530,0),Core.TickCount)&&!next.Deleted&&NecromancerEffects.ManaBalance256(p)==held,"Invalid aim changed existing spirit");
        }
        finally{DruidOakCasting.WolverineSpawn=spawn;End(p);}
    }
    private static void TargetsGearAndTown()
    {
        var p=Fresh();var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Wolverine Tarnhelm");ClassGear.Refresh(p);
            int mana=NecromancerEffects.ManaBalance256(p);var first=Cast(p,enemy);Require(first.Prepared.Birth is{Rank:2,Life256:43520,AuraRank:2}&&first.InRange(enemy,2)&&NecromancerEffects.ManaBalance256(p)==mana-5376,"Soft rank cost/adjacent placement");
            Full(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,enemy,enemy.Location,Core.TickCount),"Moving target begin");enemy.X++;
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&first.Deleted&&DruidSpirits.Hearts(p).Single().InRange(enemy,2),"Live target/replacement");
            var last=enemy.Location;Full(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.HeartOfWolverine,enemy,last,Core.TickCount),"Deleted-target begin");enemy.Delete();
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&DruidSpirits.Hearts(p).Single().InRange(last,2),"Deleted target fallback");
            Require(DruidSpirits.Source("oaksage")&&DruidSpirits.Source(SkillMechanics.For(CombatAbility.HeartOfWolverine).Text("summon"))
                &&DruidSpirits.Source(SkillMechanics.For(CombatAbility.SpiritOfBarbs).Text("summon"))&&!DruidSpirits.Source("spiritwolf"),"Shared totem source category");
            p.MoveToWorld(new(1420,1698,0),Map.Trammel);p.Z=p.Map.GetAverageZ(p.X,p.Y);Require(SpellHelper.IsTown(p.Location,p),"Town fixture");Full(p);
            var town=Cast(p,aim:p.Location);Require(town.Map==p.Map&&SpellHelper.IsTown(town.Location,p),"Current InTown1cast refused");
        }
        finally{enemy.Delete();End(p);}
    }
    private static void Write(PlayerMobile p,DruidOakBody body,BaseCreature wolf,BaseCreature raven,PlayerMobile pending)
    {
        PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,wolf.Serial.Value,raven.Serial.Value,body.Prepared,NecromancerEffects.ManaBalance256(p),pending.Serial.Value,NecromancerEffects.ManaBalance256(pending),p.Map.MapID,p.Location)));
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.HeartOfWolverine),"Learned Wolverine missing");CancellationAndAtomicity();TargetsGearAndTown();
        var p=Fresh();Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"druid.summonspiritwolf",out _),"Coexisting family ranks");
        var at=TimedEffects.Now.AddDays(2);var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+6,p.Y,p.Z),at,177);var raven=DruidRavenBody.CreatePrepared(p,new(p.X+7,p.Y,p.Z),at,178,enforce:false);
        Full(p);Require(DruidOakCasting.Commit(p,Serial.Zero,new(p.X+3,p.Y,p.Z),TimedEffects.Now,out var oldOak),"Initial shared-category Oak");
        Full(p);var first=Cast(p);Require(oldOak.Deleted&&DruidSpirits.Oaks(p).Length==0,"Heart did not replace Oak");
        Full(p);Require(DruidOakCasting.Commit(p,Serial.Zero,new(p.X+3,p.Y,p.Z),TimedEffects.Now,out var newOak)&&first.Deleted&&DruidSpirits.Hearts(p).Length==0,"Oak did not replace Heart");
        Full(p);var second=Cast(p,aim:new(p.X+4,p.Y,p.Z));Require(newOak.Deleted&&!wolf.Deleted&&!raven.Deleted,"Spirit replacement deleted another family");
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.HeartOfWolverine)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HeartOfWolverine),"Saved learned bindings");
        // Park the paid actor's inspection clock without changing its birth or seed.
        var c=second.Prepared.Controller;var next=TimedEffects.Now.AddDays(2);second.RestorePrepared(second.Prepared with{Controller=c with{NextUtc=next},NextRecoveryUtc=next});
        var pending=Fresh();Ready(pending);Require(ClassCombat.Perform(pending,CombatAbility.HeartOfWolverine,null,new(pending.X+3,pending.Y,pending.Z),Core.TickCount),"Saved unpaid cast");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Write(p,second,wolf,raven,pending);
        Log.Information("Learned Wolverine:20+rank-1mana once-only SC, cancellation, allocation/payment rollback, current/deleted target placement, soft gear rank, shared spirit replacement, coexistence/town and saved unpaid windup controls pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var old=World.FindMobile((Serial)saved.Body) as DruidOakBody;var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(old is{Alive:true,Deleted:false}&&old.ControlMaster==p&&old.Map==Map.Internal&&old.SavedText==JsonSerializer.Serialize(saved.State)&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Saved paid Heart/owner/mana changed");
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None&&DruidSpirits.Hearts(pending).Length==0&&NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Unpaid windup replayed/paid");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.HeartOfWolverine&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.HeartOfWolverine,"Saved right/quick bindings");
        Require(World.FindMobile((Serial)saved.Wolf) is{Alive:true,Deleted:false}&&World.FindMobile((Serial)saved.Raven) is{Alive:true,Deleted:false},"Saved coexisting family removed");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}Full(p);var body=Cast(p);Require(old.Deleted,"Paid post-reload replacement failed");
        var next=TimedEffects.Now.AddDays(2);body.RestorePrepared(body.Prepared with{Controller=body.Prepared.Controller with{NextUtc=next},NextRecoveryUtc=next});
        Write(p,body,(BaseCreature)World.FindMobile((Serial)saved.Wolf),(BaseCreature)World.FindMobile((Serial)saved.Raven),pending);
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"wolverine-quick":"wolverine-right";var p=Fresh();Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Input protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.HeartOfWolverine)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HeartOfWolverine),"Input bindings");
        bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Wolverine input timeout");var bodies=DruidSpirits.Hearts(p);
                if(ClassCombat.For(p).PendingAbility==CombatAbility.HeartOfWolverine)
                {Require(bodies.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid before release");if(!began){began=true;Log.Information("{Label}: windup",label);}}
                if(bodies.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&(quick||ClassCombat.For(p).HeldAbility==CombatAbility.None)&&bodies.Length==1&&NecromancerEffects.ManaBalance256(p)==mana-5120
                    &&bodies[0].Location==new Point3D(p.X+3,p.Y,p.Z),"Real Wolverine input payment/placement");End(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.HeartOfWolverine,(int)DiabloClass.Druid,p.X+3,p.Y,p.Z);Inspect();});
    }
}
