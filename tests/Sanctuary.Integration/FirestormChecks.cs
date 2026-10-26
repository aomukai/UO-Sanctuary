// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using Server.Network;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class FirestormChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FirestormChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,FirestormSave State,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"firestorm-fixtures.json");
    private static int _scene;private static uint _pending;private static int _pendingMana;private static bool _creating;
    private static void Rates(){var prior=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=x=>x is PlayerMobile && (_creating || x.Name?.StartsWith("FirestormFixture",StringComparison.Ordinal)==true)?TimeSpan.FromHours(1):prior?.Invoke(x)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure(){Rates();Register("firestorm-check",Check);Register("firestorm-reload-check",Reload);Register("firestorm-live-check",Live);Register("firestorm-input-check",Input);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Druid Firestorm travelling emitters and burning fields",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="FirestormFixture"};}finally{_creating=false;}p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Corridor(p,6860,25,2840+32*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){bool success=ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var reason);Require(success,"Train: "+reason);}
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=1)
    {
        var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(2000);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Cold,0);target.SetResistance(ResistanceType.Energy,0);target.SetResistance(ResistanceType.Poison,0);
        target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();return target;
    }
    private static string Text(FirestormSave saved)=>JsonSerializer.Serialize(saved);
    private static DateTime Next()=>DruidFirestorm.Snapshot().Emitters.Select(x=>x.NextUtc).Concat(DruidFirestorm.Snapshot().Patches.Select(x=>x.NextUtc)).Min();
    private static long[] Launch(PlayerMobile p,DateTime at,int rank=1)=>DruidFirestorm.Create(p,new(p.X+10,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Firestorm,rank),at);
    private static void Formula()
    {
        var d=SkillMechanics.For(CombatAbility.Firestorm);Require(d.SourceId==225 && d.Number("localdelay")==15 && DruidCombat.Implemented(CombatAbility.Firestorm),"Learned Firestorm identity");int count=0;
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})foreach(int mastery in new[]{0,20})
        {
            var v=new SkillValues(d,_=>rank,key=>key is "Molten Boulder" or "Eruption"?hard:0,99);var ends=DruidFirestorm.Endpoints(v,mastery,SlowMissilePayload.Normal);
            int Expected(bool max){long basis=SkillValues.Tier(d.Number(max?"EMax":"EMin"),rank,i=>d.Number((max?"EMaxLev":"EMinLev")+i))<<2;basis+=basis*(46*hard)/100;return (int)(basis+basis*mastery/100);}
            Require(ends.Min==Expected(false) && ends.Max==Expected(true) && v.Mana256==1024,"Per-frame skill synergy/mastery endpoint");count++;
        }
        var mods=new FirestormModifiers(10,20,30,40,50);Require(DruidFirestorm.Packet(mods,2560)==new GearDamage(0,11.2,1.3,1.4,1.5),"Approved mastered spell-potion components");
        Log.Information("Firestorm:1530current endpoint/mana cases and approved potion packet pass");
    }
    private static void Ready(PlayerMobile p,bool delay=true)
    {
        ClassCombat.Stop(p);var state=ClassCombat.For(p);state.NextAction=state.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;
        // Windup scenarios are independent; the separate cooldown check keeps
        // the real delay intact rather than resetting it before its assertion.
        if(delay){var saved=FireGround.Snapshot();FireGround.Restore(saved with{Cooldowns=saved.Cooldowns.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.Firestorm).ToArray()});}
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static bool Start(PlayerMobile p,Mobile target=null,Point3D? ground=null,bool held=false)
    {
        var point=ground??target?.Location??new Point3D(p.X+8,p.Y,p.Z);
        bool started=held?ClassCombat.ContinueCast(p,CombatAbility.Firestorm,target,point,Core.TickCount):ClassCombat.Perform(p,CombatAbility.Firestorm,target,point,Core.TickCount);
        ClassCombat.ReleaseCast(p);return started;
    }
    private static void Casting()
    {
        var p=Fresh();Require(!ClassCombat.Available(p,CombatAbility.Firestorm),"Unlearned Firestorm available");Train(p,CombatAbility.Firestorm);Mana(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Firestorm) && SkillBindings.Instance.Assign(p,false,CombatAbility.Firestorm) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.Firestorm),"Left/right/quick Firestorm bindings");
        Ready(p);NecromancerEffects.SetMana256(p,1023);Require(!Start(p) && NecromancerEffects.ManaBalance256(p)==1023 && DruidFirestorm.Snapshot().Emitters.Length==0,"Low mana started/paid Firestorm");Mana(p);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(Start(p),"Firestorm windup");var state=ClassCombat.For(p);long at=state.EffectAt;
        Require(state.Animation==16 && !ClassCombat.ResolveAction(p,at-1) && NecromancerEffects.ManaBalance256(p)==mana,"SC timing/premature payment");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled windup paid");
        Ready(p);Require(Start(p),"Movement windup");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward move");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Out/back restored cancelled cast");
        Ready(p);Require(Start(p),"Recovery windup");at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Recovery failed to cancel");
        Ready(p);Require(Start(p),"Mana-loss windup");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at) && DruidFirestorm.Snapshot().Emitters.Length==0 && FireGround.Ready(p,CombatAbility.Firestorm),"Failed release paid/emitted/delayed");Mana(p);
        Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(Start(p,held:true),"Held ground windup");at=ClassCombat.For(p).EffectAt;Require(ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-1024 && DruidFirestorm.Snapshot().Emitters.Length==3,"Once-only ground release/payment");
        var delay=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.Firestorm);Require(delay.UntilUtc>TimedEffects.Now && delay.UntilUtc<=TimedEffects.Now.AddMilliseconds(600) && !FireGround.Ready(p,CombatAbility.Firestorm),"15-frame local delay");
        Ready(p,false);Require(!Start(p) && NecromancerEffects.ManaBalance256(p)==mana-1024,"Cooldown repeated a paid cast");Train(p,CombatAbility.CycloneArmor);Require(ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Firestorm delay became global");ClassCombat.Stop(p);DruidFirestorm.Cancel(p);
        Ready(p);Mana(p);var target=Target(p,6);Require(Start(p,target),"Moving target windup");at=ClassCombat.For(p).EffectAt;target.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);
        Require(ClassCombat.ResolveAction(p,at) && DruidFirestorm.Snapshot().Emitters.Single(x=>!x.Charged) is{Vx:0,Vy:>0},"Release ignored live target coordinates");DruidFirestorm.Cancel(p);target.Delete();
        Ready(p);Mana(p);target=Target(p,6);Require(Start(p,target),"Deleted target windup");at=ClassCombat.For(p).EffectAt;target.Delete();mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.ResolveAction(p,at) && DruidFirestorm.Snapshot().Emitters.Length==3 && NecromancerEffects.ManaBalance256(p)==mana-1024,"Deleted target lost cached ground cast");DruidFirestorm.Cancel(p);
        Ready(p);Mana(p);Require(Start(p,ground:p.Location),"Self ground aim");at=ClassCombat.For(p).EffectAt;Require(ClassCombat.ResolveAction(p,at) && DruidFirestorm.Snapshot().Emitters.Length==3,"Self-coordinate factory fallback");DruidFirestorm.Cancel(p);p.Delete();
    }
    private static void MotionAndCapture()
    {
        var p=Fresh();Train(p,CombatAbility.Firestorm);var at=TimedEffects.Now.AddDays(1);int mana=NecromancerEffects.ManaBalance256(p);var ids=Launch(p,at);var initial=DruidFirestorm.Snapshot();
        Require(ids.Length==3 && initial.Emitters.Length==3 && initial.Emitters.Count(x=>!x.Charged)==1 && initial.Emitters.Where(x=>x.Charged).Select(x=>x.Ordinal).Order().SequenceEqual(new[]{0,1}) && initial.Emitters.All(x=>x.VelocityFp8==1536),"One straight/two seeded emitters");
        DruidFirestorm.Tick(at.AddMilliseconds(40),true);Require(DruidFirestorm.Snapshot().Patches.Length==0 && DruidFirestorm.Snapshot().Emitters.All(x=>x.Frame==1),"Subcell move emitted fire");
        DruidFirestorm.Tick(at.AddMilliseconds(80),true);Require(DruidFirestorm.Snapshot().Patches.Length==0,"Maker emitted before previous movement callback");DruidFirestorm.Tick(at.AddMilliseconds(120),true);var early=DruidFirestorm.Snapshot().Patches;Require(early.Length>0 && early.All(x=>x.Min256==12 && x.Max256==24 && x.Rank==1),"First fresh source fields");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);Train(p,CombatAbility.MoltenBoulder);
        Require(SkillMechanics.Rank(p,CombatAbility.Firestorm)==2,"Actual soft-rank control");
        DruidFirestorm.Tick(at.AddMilliseconds(240),true);var captured=DruidFirestorm.Snapshot();
        Require(captured.Patches.Any(x=>x.Min256==14 && x.Max256==29 && x.Rank==1) && early.All(x=>captured.Patches.Any(y=>x.Id==y.Id && x.Min256==y.Min256 && x.Max256==y.Max256)),"New live hard synergy/captured rank or retroactive field damage");
        DruidFirestorm.Tick(at.AddMilliseconds(40*40),true);var released=DruidFirestorm.Snapshot();Require(released.Emitters.Length==0 && released.Patches.Length>0,"Independent released fire lifetime");
        DruidFirestorm.Tick(at.AddMilliseconds(55*40),true);Require(DruidFirestorm.Snapshot().Patches.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Natural field expiry/prepared launch payment");
        // Explicit terminal-frame control keeps do23 child-before-removal order.
        Launch(p,at);var straight=DruidFirestorm.Snapshot().Emitters.Single(x=>!x.Charged);straight=straight with{Frame=39,X=p.X+.5,CellX=p.X*2+1,CellChanged=true,NextUtc=at.AddSeconds(5)};
        DruidFirestorm.Restore(new([straight],[],straight.Id));DruidFirestorm.Tick(straight.NextUtc,true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:1},"Terminal callback discarded previously changed-cell child");
        DruidFirestorm.Cancel(p);p.Delete();
    }
    private static void FieldsAndDefense()
    {
        var p=Fresh();var a=Target(p);var b=Target(p);var at=TimedEffects.Now.AddDays(1);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(120),true);var source=DruidFirestorm.Snapshot().Patches.First();
        var field=source with{X=a.X*2,Y=a.Y*2,Min256=256,Max256=512};DruidFirestorm.Restore(new([], [field],field.Id));
        int before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);ulong seed=field.Seed;int expected=0;
        for(int i=0;i<14;i++){expected+=WeaponMissileDamage.RollValue(256,512,0,bound=>(int)SourceRandom.Limited(ref seed,bound));}
        DruidFirestorm.Tick(field.UntilUtc.AddMilliseconds(-40),true);var after=DruidFirestorm.Snapshot().Patches.Single();
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==expected && after.Seed==seed && (SkillHealth.Full(a) || SkillHealth.Full(b)),"One collider/14exclusive seeded fire rolls or extra field callback");
        // State at next==expiry is valid between the final hit and removal.
        var saved=DruidFirestorm.Snapshot();DruidFirestorm.Restore(JsonSerializer.Deserialize<FirestormSave>(Text(saved)));DruidFirestorm.Tick(field.UntilUtc,true);Require(DruidFirestorm.Snapshot().Patches.Length==0,"Damage at zero remaining lifetime");
        var constant=field with{Min256=256,Max256=256,NextUtc=at.AddMinutes(1),UntilUtc=at.AddMinutes(1).AddMilliseconds(14*40)};
        var other=constant with{Id=constant.Id+1};DruidFirestorm.Restore(new([], [constant,other],other.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(constant.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==512,"Independent overlapping fields were throttled");
        a.SetResistance(ResistanceType.Fire,100);b.SetResistance(ResistanceType.Fire,100);Require(a.FireResistance==100 && b.FireResistance==100,"Real fire immunity controls");
        constant=constant with{NextUtc=at.AddMinutes(2),UntilUtc=at.AddMinutes(2).AddMilliseconds(14*40)};DruidFirestorm.Restore(new([], [constant],constant.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(constant.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==0,"Pure fire bypassed immunity");
        Require(CombatPotions.Apply(p,3),"Actual spell potion");var mods=DruidFirestorm.Modifiers(p);Require(mods.Potion==10,"Actual spell potion capture");
        var potion=constant with{Min256=2560,Max256=2560,Modifiers=mods,NextUtc=at.AddMinutes(3),UntilUtc=at.AddMinutes(3).AddMilliseconds(14*40)};DruidFirestorm.Restore(new([], [potion],potion.Id));before=SkillHealth.Balance256(a)+SkillHealth.Balance256(b);DruidFirestorm.Tick(potion.NextUtc,true);
        Require(before-SkillHealth.Balance256(a)-SkillHealth.Balance256(b)==3*256,"Approved nonfire potion components lost against fire immunity");
        DruidFirestorm.Cancel(p);a.Delete();b.Delete();p.Delete();
    }
    private static void WallsAndReductions()
    {
        var p=Fresh();var target=Target(p,6);var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        try
        {
            var at=TimedEffects.Now.AddDays(1);Launch(p,at);var straight=DruidFirestorm.Snapshot().Emitters.Single(x=>!x.Charged);DruidFirestorm.Restore(new([straight],[],straight.Id));
            int life=SkillHealth.Balance256(target);DruidFirestorm.Tick(at.AddSeconds(3),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0} && SkillHealth.Balance256(target)==life,"Firestorm crossed wall");
        }
        finally{wall.Delete();target.Delete();}
        var ring=NamedGear.Create("unique/274",99,new Random(173),true);Require(p.EquipItem(ring),"Real Dwarf Star MDR");ClassGear.Refresh(p);Require(GemCombat.Reduction(p)>0,"Actual MDR supplier");
        var raw=new GearDamage(0,40,40,40,0);var legacy=SkillCombat.MitigatePrepared(p,p,raw);var explicitZero=SkillCombat.MitigatePrepared(p,p,raw,damageRate:0);var field=SkillCombat.MitigatePrepared(p,p,raw,damageRate:41);
        Require(legacy==explicitZero && field.Cold>legacy.Cold && field.Lightning>legacy.Lightning && field.Fire>legacy.Fire,"Per-frame MDR rate/default backward compatibility");p.Delete();
    }
    private static void Live()=>FixtureNetwork.Connected("firestorm-live-result",Live);
    private static void Live(NetState network)
    {
        var held=DruidFirestorm.Snapshot();var p=Fresh();var target=Target(p);network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int life=SkillHealth.Balance256(target);Launch(p,TimedEffects.Now);bool moved=false,burned=false,damaged=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural emitter/field timeout");var state=DruidFirestorm.Snapshot();var emitters=state.Emitters.Where(x=>x.Owner==p.Serial.Value).ToArray();var patches=state.Patches.Where(x=>x.Owner==p.Serial.Value).ToArray();
                moved|=emitters.Any(x=>x.Frame>0 && (x.X!=p.X || x.Y!=p.Y));burned|=patches.Length>0;damaged|=SkillHealth.Balance256(target)<life;
                if(emitters.Length>0 || patches.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(moved && burned && damaged,"Natural timer did not move/burn/damage");DruidFirestorm.Cancel(p);target.Delete();p.Delete();var current=DruidFirestorm.Snapshot();Require(Text(current with{NextId=held.NextId})==Text(held),"Natural fixture changed held owner state");
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var heldOwner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var heldTarget=World.FindMobile((Serial)saved.Target);Require(SkillHealth.Balance256(heldTarget)==saved.Life256,"Natural fixture changed held target life");Save(heldOwner,heldTarget);Log.Information("firestorm-live-result: passed");
            }
            catch(Exception e){DruidFirestorm.Cancel(p);target.Delete();p.Delete();Log.Error(e,"firestorm-live-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);
    }
    private static void OfflineAndLifecycle()
    {
        var p=Fresh();var a=Target(p);var at=TimedEffects.Now.AddDays(1);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(120),true);int life=SkillHealth.Balance256(a);DruidFirestorm.Tick(at.AddSeconds(3),false);
        Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0} && SkillHealth.Balance256(a)==life,"Offline damage/lifetime replay");
        Launch(p,at);p.MoveToWorld(new(400,400,0),Map.Felucca);DruidFirestorm.Tick(at.AddSeconds(3),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0},"Cross-facet player projectiles retained");a.Delete();p.Delete();
    }
    private static void HeldCheckpoint(FirestormSave held)
    {
        var current=DruidFirestorm.Snapshot();Require(Text(current with{NextId=held.NextId})==Text(held),"Input fixture changed held owner state");
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);Require(SkillHealth.Balance256(target)==saved.Life256,"Input fixture changed held target life");Save(owner,target);
    }
    private static void Input()=>FixtureNetwork.Connected("firestorm-input-result",Input);
    private static void Input(NetState network)
    {
        var held=DruidFirestorm.Snapshot();var p=Fresh();Train(p,CombatAbility.Firestorm);Mana(p);Ready(p);var target=Target(p,4);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Firestorm) && SkillBindings.Instance.Assign(p,false,CombatAbility.Firestorm) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.Firestorm),"Connected Firestorm bindings");
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Input protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);long deadline=Core.TickCount+7000;bool started=false,released=false,damaged=false;
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Firestorm quick cast timeout");var state=DruidFirestorm.Snapshot();var emitters=state.Emitters.Where(x=>x.Owner==p.Serial.Value).ToArray();var patches=state.Patches.Where(x=>x.Owner==p.Serial.Value).ToArray();
                if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.Firestorm){started=true;Require(emitters.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Input windup emitted/paid early");}
                if(!released && emitters.Length>0){released=true;Require(started && emitters.Length==3 && NecromancerEffects.ManaBalance256(p)==mana-1024 && !FireGround.Ready(p,CombatAbility.Firestorm),"Natural quick release/count/payment/delay");}
                damaged|=SkillHealth.Balance256(target)<life;
                if(!released || emitters.Length>0 || patches.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(damaged && NecromancerEffects.ManaBalance256(p)==mana-1024 && FireGround.Ready(p,CombatAbility.Firestorm),"Natural damage/once payment/delay expiry");DruidFirestorm.Cancel(p);target.Delete();p.Delete();HeldCheckpoint(held);Log.Information("firestorm-input-result: passed");
            }
            catch(Exception e){DruidFirestorm.Cancel(p);target.Delete();p.Delete();Log.Error(e,"firestorm-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("firestorm-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.Firestorm,p.X+10,p.Y,p.Z);Wait();});
    }
    private static void WorkerAndMalformed()
    {
        var before=DruidFirestorm.Snapshot();string text=Text(before);var data=Task.Run(()=>{var writer=new BufferWriter(new byte[2*1024*1024],true);DruidFirestorm.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}).GetAwaiter().GetResult();var reader=new BufferReader(data);
        Require(reader.ReadInt()==0 && reader.ReadString()==text && Text(DruidFirestorm.Snapshot())==text,"Worker mutated Firestorm state");
        var e=before.Emitters.First();foreach(var bad in new[]{e with{Frame=40},e with{X=double.NaN},e with{Rank=0},e with{CellX=e.CellX+1}})
        {bool rejected=false;try{DruidFirestorm.Restore(before with{Emitters=before.Emitters.Where(x=>x.Id!=e.Id).Append(bad).ToArray()});}catch(InvalidDataException){rejected=true;}Require(rejected && Text(DruidFirestorm.Snapshot())==text,"Invalid restore changed live state");}
    }
    private static void Save(PlayerMobile p,Mobile target)
    {File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),DruidFirestorm.Snapshot(),_pending,_pendingMana)));}
    private static void Check()
    {
        Require(DruidFirestorm.Snapshot() is{Emitters.Length:0,Patches.Length:0},"Unexpected existing Firestorm fixtures");Formula();Casting();MotionAndCapture();FieldsAndDefense();WallsAndReductions();OfflineAndLifecycle();
        var p=Fresh();Train(p,CombatAbility.Firestorm);var target=Target(p);var at=TimedEffects.Now.AddDays(2);Launch(p,at);DruidFirestorm.Tick(at.AddMilliseconds(6*40),true);Require(DruidFirestorm.Snapshot() is{Emitters.Length:3,Patches.Length:>0},"Saved active emitter/patch setup");WorkerAndMalformed();Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Firestorm) && SkillBindings.Instance.Assign(p,false,CombatAbility.Firestorm) && SkillBindings.Instance.AssignQuick(p,4,CombatAbility.Firestorm),"Saved Firestorm bindings");
        var pending=Fresh();Train(pending,CombatAbility.Firestorm);Mana(pending);Ready(pending);_pending=pending.Serial.Value;_pendingMana=NecromancerEffects.ManaBalance256(pending);Require(Start(pending),"Saved unpaid Firestorm windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Save(p,target);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var target=World.FindMobile((Serial)saved.Target);
        Require(p!=null && target!=null && Text(DruidFirestorm.Snapshot())==Text(saved.State) && SkillHealth.Balance256(target)==saved.Life256,"Exact world emitter/patch/seed/life reload");WorkerAndMalformed();
        _pending=saved.Pending;_pendingMana=saved.PendingMana;var pending=(PlayerMobile)World.FindMobile((Serial)_pending);Require(pending!=null && NecromancerEffects.ManaBalance256(pending)==_pendingMana && ClassCombat.For(pending).PendingAbility==CombatAbility.None,"Unpaid cast replayed/paid on load");Require(SkillBindings.Instance.Get(p)==(CombatAbility.Firestorm,CombatAbility.Firestorm) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,4)==CombatAbility.Firestorm,"Saved Firestorm left/right/quick bindings");
        var before=DruidFirestorm.Snapshot();DruidFirestorm.Tick(Next(),true);var after=DruidFirestorm.Snapshot();Require(after.Emitters.Length==3 && after.Emitters.All(e=>e.Frame==before.Emitters.Single(x=>x.Id==e.Id).Frame+1),"Saved path did not resume");
        Save(p,target);
    }
}
