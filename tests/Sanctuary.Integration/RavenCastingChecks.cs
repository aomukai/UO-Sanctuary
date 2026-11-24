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

public static class RavenCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RavenCastingChecks));
    private sealed record Saved(uint Owner,uint Body,string State,uint Pending,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"raven-casting-fixtures.json");
    private static bool _creating;private static int _scene;
    private static void Rates()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("RavenCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();Register("raven-casting-check",Check);Register("raven-casting-reload-check",Reload);
        Register("raven-right-check",()=>FixtureNetwork.Connected("raven-right-result",n=>Input(n,false)));
        Register("raven-quick-check",()=>FixtureNetwork.Connected("raven-quick-result",n=>Input(n,true)));
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Learned Raven casting and input",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(bool trained=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="RavenCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        if(trained){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _),"Train Raven");}
        FixtureSpace.Corridor(p,7090,20,1200+32*_scene++);Mana(p);return p;
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Drop(PlayerMobile p){foreach(var body in DruidRavens.For(p)){body.Delete();}p.Delete();}
    private static GraveyardZombie Enemy(PlayerMobile p,int dx)
    {var t=new GraveyardZombie{CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;}
    private static DruidRavenBody Cast(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,CombatAbility.Raven,target,aim??target?.Location??p.Location,Core.TickCount),"Raven start");
        var s=ClassCombat.For(p);Require(s.PendingAbility==CombatAbility.Raven && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Unpaid SC windup");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early release");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-1536,"Exactly six mana per release");
        return DruidRavens.For(p).Single();
    }
    private static void Cancellation()
    {
        var p=Fresh(false);
        try
        {
            Require(!ClassCombat.Available(p,CombatAbility.Raven) && !ClassCombat.Perform(p,CombatAbility.Raven,null,p.Location,Core.TickCount),"Unlearned cast");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _),"Learn after refusal");
            Require(!ClassAbilities.SelfTarget(CombatAbility.Raven) && !SkillBindings.Instance.Assign(p,true,CombatAbility.Raven)
                && SkillBindings.Instance.Assign(p,false,CombatAbility.Raven) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Raven),"Right/quick identity");
            Ready(p);NecromancerEffects.SetMana256(p,1535);Require(!ClassCombat.Perform(p,CombatAbility.Raven,null,p.Location,Core.TickCount),"Fractionally insufficient mana");
            for(int mode=0;mode<6;mode++)
            {
                Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
                Require(ClassCombat.Perform(p,CombatAbility.Raven,null,p.Location,Core.TickCount),"Cancelled start");long when=ClassCombat.For(p).EffectAt;
                if(mode==0){ClassCombat.Stop(p);}
                if(mode==1){var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location==origin,"Return step");}
                if(mode==2){NecromancerEffects.SetMana256(p,1535);}
                if(mode==3){Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery cancellation");}
                if(mode==4){p.Frozen=true;}
                if(mode==5){var old=p.Map;p.MoveToWorld(p.Location,Map.Felucca);p.MoveToWorld(p.Location,old);}
                Require(!ClassCombat.ResolveAction(p,when) && DruidRavens.For(p).Length==0
                    && NecromancerEffects.ManaBalance256(p)==(mode==2?1535:mana),"Cancelled/failed cast spent mana or spawned Raven: mode="+mode+" mana="+NecromancerEffects.ManaBalance256(p)+" expected="+(mode==2?1535:mana)+" birds="+DruidRavens.For(p).Length);
            }
            Ready(p);Mana(p);Require(!ClassCombat.Perform(p,CombatAbility.Raven,null,new(65530,65530,0),Core.TickCount),"Invalid world point accepted");
            NecromancerEffects.SetMana256(p,1536);var b=Cast(p);Require(NecromancerEffects.ManaBalance256(p)==0,"Exact low-mana payment");b.Delete();
            Ready(p);Mana(p);int friendlyMana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.ContinueCast(p,CombatAbility.Raven,p,p.Location,Core.TickCount),"Held Raven self/ally point refused");
            long friendlyAt=ClassCombat.For(p).EffectAt;ClassCombat.ReleaseCast(p);
            Require(ClassCombat.ResolveAction(p,friendlyAt) && NecromancerEffects.ManaBalance256(p)==friendlyMana-1536,"Released held cast cancelled or repeated its paid summon");
        }
        finally{Drop(p);}
    }
    private static void TargetsAndCaps()
    {
        var p=Fresh();var t=Enemy(p,2);var second=Enemy(p,3);PlayerMobile supplier=null;
        try
        {
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
            Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);
            var first=Cast(p,t);Require(first.InRange(p,4) && first.Prepared.Focus==t.Serial.Value && first.FlockSize==2,"Explicit-target birth near caster/focus/gear");
            var next=Cast(p,second);Require(next==first && next.Prepared.Focus==second.Serial.Value,"Paid recast did not retarget the same flock");
            Ready(p);Mana(p);Require(ClassCombat.Perform(p,CombatAbility.Raven,t,t.Location,Core.TickCount),"Live target start");
            t.MoveToWorld(new(t.X+2,t.Y,t.Z),t.Map);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Live target release");
            var moved=DruidRavens.For(p).Single();Require(moved==next && moved.Prepared.Focus==t.Serial.Value,"Live target recast did not retarget the same flock");
            Ready(p);Mana(p);Require(ClassCombat.Perform(p,CombatAbility.Raven,second,second.Location,Core.TickCount),"Deleted target start");second.Delete();
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && DruidRavens.For(p).Single().Prepared.Focus==t.Serial.Value,"Deleted target cached ground fallback");
            var held=moved.SavedText;Ready(p);NecromancerEffects.SetMana256(p,1536);
            Require(ClassCombat.Perform(p,CombatAbility.Raven,t,t.Location,Core.TickCount),"Lost mana at cap start");NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && DruidRavens.For(p).Single()==moved
                && moved.SavedText==held,"Failed recast changed the active flock");
            supplier=new PlayerMobile{Body=0x190,Player=true,Name="RavenCastFixtureSupplier"};supplier.AddItem(new Backpack());ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(supplier,99);BattleCommand.Apply(supplier,p,1,TimedEffects.Now);
            Ready(p);Mana(p);var commanded=Cast(p,t);Require(DruidRavens.For(p).Single()==commanded && commanded.FlockSize==3,"Battle Command at cast sets flock size");
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);DruidRavens.Tick(TimedEffects.Now.AddMilliseconds(80),true);Require(commanded.FlockSize==3,"Gear loss shrank an existing flock");
            BattleCommand.Clear(p);DruidRavens.Tick(TimedEffects.Now.AddMilliseconds(160),true);
            Require(DruidRavens.For(p).Single()==commanded && commanded.FlockSize==3,"Command loss shrank or replaced the flock");
            Ready(p);p.Kill();Require(!p.Alive && commanded.Deleted,"Owner death retained Raven");p.Corpse?.Delete();
        }
        finally{t.Delete();second.Delete();if(supplier!=null){Drop(supplier);}Drop(p);}
    }
    private static void FlockFocusesHavenMonster()
    {
        var p=Fresh();
        var target=new HavenEncounter(HavenEncounter.Bogling,3){CantWalk=true,NoKillAwards=true};
        var other=Enemy(p,2);
        try
        {
            for(int i=1;i<6;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"druid.raven",out _),"Train flock rank");}
            target.SetHits(10000);target.SetResistance(ResistanceType.Physical,0);
            target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            var flock=Cast(p,target);
            Require(flock.FlockSize==5 && flock.Body.BodyID==RavenAppearance.FirstBody+4 && flock.Name=="Raven Flock"
                && flock.Prepared.Focus==target.Serial.Value,"Five-bird appearance or Haven focus");
            var ground=Cast(p,aim:p.Location);
            Require(ground==flock && ground.InRange(p,4) && ground.Prepared.Focus==other.Serial.Value,
                "Ground cast at caster did not lock the nearest monster");

            other.Delete();
            var perch=new Point3D(p.X+6,p.Y,p.Z);
            target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);
            ground.MoveToWorld(perch,p.Map);
            target.Home=target.Location;target.HomeMap=target.Map;
            var ai=(LeashedMeleeAI)target.AIObject;
            long now=Core.TickCount;
            target.Combatant=p;
            Require(target.Combatant==p&&!ai.UpdateLeash(now),"Bogling did not engage");
            Mana(p);Require(Cast(p,target)==flock,"Direct cast replaced the active flock");
            int budget=flock.Prepared.Remaining;
            flock.Advance(flock.Prepared.NextFrameUtc.AddMilliseconds(160),true);
            var first=flock.Prepared;
            Require(first.Attempts==1&&first.Remaining==budget-1,"Flock did not strike before recast");
            target.Combatant=null;
            var home=target.Home;
            target.Home=new(target.X-target.HomeLeashRadius-1,target.Y,target.Z);
            Require(ai.UpdateLeash(now+1)&&flock.Eligible(p,target),"Returning bogling stopped being targetable");

            Mana(p);var renewed=Cast(p,aim:p.Location);var state=renewed.Prepared;
            Require(renewed==flock && renewed.Location==perch && state.Focus==target.Serial.Value
                && state.Remaining==budget && state.NextAttack==first.NextAttack,"Ground recast lost the flock's position, focus, budget or cooldown");
            renewed.Advance(state.NextFrameUtc.AddMilliseconds(1600),true);
            for(int i=0;i<4&&renewed.Prepared.Attempts==0;i++){renewed.Advance(renewed.Prepared.NextFrameUtc,true);}
            Require(renewed.Location==perch && renewed.Prepared.Attempts>0 && renewed.Prepared.Hits==renewed.Prepared.Attempts,
                "Refreshed flock stopped hitting its original bogling during its return");
            target.Home=home;
            Require(ai.UpdateLeash(now+2)&&!ai.UpdateLeash(now+3003),"Being hit stopped the bogling from finishing its return");
        }
        finally{target.Delete();other.Delete();Drop(p);}
    }
    private static void Town()
    {
        var p=Fresh();try
        {RecognitionChecks.Place(p);Require(SpellHelper.IsTown(p.Location,p),"Town fixture");Ready(p);Mana(p);Require(Cast(p).Map==p.Map,"Pinned InTown1 refused");}
        finally{Drop(p);}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Raven),"Learned Raven not routed");Cancellation();TargetsAndCaps();Town();FlockFocusesHavenMonster();
        var p=Fresh();var body=Cast(p);var s=body.Prepared;body.RestorePrepared(s with{NextFrameUtc=TimedEffects.Now.AddDays(2)});PersistentMinions.Suspend(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Raven) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Raven),"Saved Raven binding");
        var pending=Fresh();Ready(pending);Mana(pending);Require(ClassCombat.Perform(pending,CombatAbility.Raven,null,pending.Location,Core.TickCount),"Saved unpaid Raven windup");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,body.SavedText,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending),p.Map.MapID,p.Location)));
        Log.Information("Learned Raven: six mana, cancellation, in-place refresh and retargeting, town casting, and a ground recast through a bogling leash reset pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var body=(DruidRavenBody)World.FindMobile((Serial)saved.Body);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(body is{Deleted:false} && body.ControlMaster==p && body.Map==Map.Internal && body.SavedText==saved.State,"Learned Raven saved owner/birth/state/parking");
        Require(DruidRavens.For(pending).Length==0 && ClassCombat.For(pending).PendingAbility==CombatAbility.None
            && NecromancerEffects.ManaBalance256(pending)==saved.Mana,"Unpaid windup replayed or charged after reload");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Raven && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Raven,"Saved Raven bindings");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        PersistentMinions.Resume(p);Require(body.Map==p.Map,"Learned Raven saved body resume");PersistentMinions.Suspend(p);
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"raven-quick":"raven-right";var p=Fresh();Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");
        field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Raven) && SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Raven),"Input binding");
        bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Raven input timeout");var bodies=DruidRavens.For(p);
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Raven)
                {
                    Require(bodies.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Input unpaid windup");
                    if(!began)
                    {
                        Require(quick || ClassCombat.For(p).HeldAbility==CombatAbility.Raven,"Right hold identity");began=true;
                        Log.Information("{Label}: windup",label);
                    }
                }
                if(bodies.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began && (quick || ClassCombat.For(p).HeldAbility==CombatAbility.None) && bodies.Length==1 && NecromancerEffects.ManaBalance256(p)==mana-1536 && bodies[0].InRange(p,4),"Real input cast/payment/placement");
                Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.Raven,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Inspect();});
    }
}
