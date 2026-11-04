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

public static class ReviveNativeSwingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveNativeSwingChecks));
    private sealed record Saved(uint Owner,uint Actor,int Life,int Generation=0,uint Target=0,int TargetLife=0,ulong Seed=0,DateTime EndUtc=default);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-native-swing-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("revive-native-swing-check",false);Register("revive-native-swing-reload-check",true);
        ConsoleInputHandler.RegisterCommand("revive-native-swing-save","Save a connected original in actual A1 windup",_=>Core.LoopContext.Post(()=>FixtureClock.NextLoop("revive-native-swing-save-result",()=>FixtureNetwork.Connected("revive-native-swing-save-result",Save))));
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original native OnSwing,AI pursuit and saved packet",_=>Core.LoopContext.Post(()=>FixtureClock.NextLoop(name,()=>FixtureNetwork.Connected(name,net=>
    {try{Check(net,reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}))));
    private static ReviveEventChecks.Recipient Target(PlayerMobile owner,BaseCreature source,int distance=1)
    {
        var target=new ReviveEventChecks.Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};target.SetHits(60000);foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}
        target.MoveToWorld(new(source.X+distance,source.Y,source.Z),owner.Map);target.AIObject.AITimer.Stop();return target;
    }
    internal static ulong HitSeed()
    {for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),next=seed;if(SourceRandom.Roll(ref next)%100<5){return seed;}}throw new InvalidOperationException("No native hit seed");}
    private static void Attach(NetState net,PlayerMobile owner,BaseCreature source)
    {
        var account=owner.Account as Account??new Account("revive-swing-"+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));
        account[0]=owner;net.Account=account;
        ReviveNativeActivationChecks.Attach(net,owner);owner.MoveToWorld(new(6600,3720,0),Map.Trammel);PersistentMinions.Resume(owner);
        source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();source.Combatant=null;source.FocusMob=null;
    }
    private static ReviveSwingState Start(BaseCreature actor,BaseCreature target)
    {
        NpcEquipmentRandom.Set(actor,HitSeed());var plan=ReviveSwingClock.Capture(actor);var delay=((BaseWeapon)actor.Weapon).OnSwing(actor,target);
        var state=ReviveSwings.State(actor);Require(state?.Pending!=null&&delay==TimeSpan.FromMilliseconds(plan.End*40)&&state.Plan==plan,"Actual native OnSwing did not enter its source A1 clock");return state;
    }
    private static void Controls(PlayerMobile owner,BaseCreature actor)
    {
        foreach(string scene in new[]{"hit","escape","move","freeze","recovery","park","codec"})
        {
            ReviveSwings.Cancel(actor,false);actor.Location=new(owner.X+2,owner.Y,owner.Z);var target=Target(owner,actor);
            try
            {
                int life=SkillHealth.Balance256(target);var row=Start(actor,target);var seed=NpcEquipmentRandom.State(actor).Seed;
                Require(SkillHealth.Balance256(target)==life&&ClassCombatHooks.OverlapMovement(actor)&&!actor.Frozen&&!actor.CantWalk,"Windup dealt early damage or changed native movement flags");
                _=((BaseWeapon)actor.Weapon).OnSwing(actor,target);Require(NpcEquipmentRandom.State(actor).Seed==seed,"Busy native swing rerolled its packet");
                ReviveSwings.Advance(row.ActionUtc.AddTicks(-1));Require(target.Notifications==0&&SkillHealth.Balance256(target)==life,"Native hit released before its action frame");
                if(scene=="escape"){target.Location=new(actor.X+12,actor.Y,actor.Z);}
                if(scene=="move"){actor.X++;}
                if(scene=="freeze"){actor.Frozen=true;}
                if(scene=="recovery"){Require(CreatureHitRecovery.BeginPreparedGetHit(actor,80),"Recovery control did not enter GH");}
                if(scene=="park")
                {
                    PersistentMinions.Suspend(owner);Require(actor.Map==Map.Internal&&ReviveSwings.State(actor)?.Pending==null,"Parking retained pending damage");
                    PersistentMinions.Resume(owner);actor.AIObject.AITimer.Stop();Require(ReviveSwings.State(actor)?.EndUtc==row.EndUtc,"Parking reset the busy deadline");
                }
                if(scene=="codec")
                {
                    var rows=ReviveSwings.Snapshot();string before=JsonSerializer.Serialize(rows);bool no=false;
                    try{ReviveSwings.Restore(rows.Select(r=>r.Actor==row.Actor?r with{Pending=r.Pending with{Impact=r.Pending.Impact with{Packet=r.Pending.Impact.Packet with{Total256=-1}}}}:r).ToArray());}catch(InvalidDataException){no=true;}
                    Require(no&&JsonSerializer.Serialize(ReviveSwings.Snapshot())==before&&NpcEquipmentRandom.State(actor).Seed==seed,"Malformed swing restore changed state or RNG");
                    ReviveSwings.Restore(JsonSerializer.Deserialize<ReviveSwingState[]>(before));Require(NpcEquipmentRandom.State(actor).Seed==seed,"Pending packet codec rerolled its source");
                }
                ReviveSwings.Advance(row.ActionUtc);bool hits=scene is "hit" or "codec";
                Require(hits?target.Notifications==1&&target.WoundedAtNotification&&SkillHealth.Balance256(target)<life:target.Notifications==0&&SkillHealth.Balance256(target)==life,"Native windup/contact/cancel differed: "+scene);
                int after=SkillHealth.Balance256(target);ReviveSwings.Advance(row.ActionUtc);Require(SkillHealth.Balance256(target)==after,"Native pending hit replayed");
                ReviveSwings.Advance(row.EndUtc);Require(!ReviveSwings.Busy(actor,row.EndUtc),"Native mode end retained the AI hold");
            }
            finally{actor.Frozen=false;CreatureProfiles.Session(actor).RecoveryUntil=Core.TickCount;ReviveSwings.Cancel(actor,false);target.Corpse?.Delete();target.Delete();}
        }
        var ordinary=new Skeleton{NoKillAwards=true};ordinary.MoveToWorld(new(owner.X+5,owner.Y+1,owner.Z),owner.Map);ordinary.AIObject.AITimer.Stop();
        try{Require(ClassCombatHooks.CreatureSwing(ordinary,actor)==null,"Native swing adapter leaked into an unmanaged NPC");}finally{ordinary.Delete();}
    }
    private static void Check(NetState net,bool reload)
    {
        PlayerMobile owner;BaseCreature actor;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;actor=World.FindMobile((Serial)saved.Actor)as BaseCreature;
            var target=World.FindMobile((Serial)saved.Target)as BaseCreature;var row=ReviveSwings.State(actor);
            Require(ReviveSwings.LoadedPending>0&&row is{Pending:null}&&row.EndUtc==saved.EndUtc&&actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==saved.Life
                &&SkillHealth.Balance256(target)==saved.TargetLife&&NpcEquipmentRandom.State(actor).Seed==saved.Seed,
                "Actual reload lost/cast/reseeded its saved pending packet or busy deadline: loaded="+ReviveSwings.LoadedPending+
                ", row="+JsonSerializer.Serialize(row)+", expected="+JsonSerializer.Serialize(saved)+", map="+actor.Map+
                ", life="+SkillHealth.Balance256(actor)+", targetLife="+SkillHealth.Balance256(target)+", seed="+NpcEquipmentRandom.State(actor).Seed);
            target.Delete();generation=saved.Generation+1;
        }
        else
        {
            owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveNativeSwingOwner";MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6600,26,3720);
            actor=ReviveMeleeGeometryChecks.Create(owner);actor.Name="PoisonDaggerFixtureReviveNativeSwing";
            var state=ReviveNativeActivation.State(actor);var setup=ReviveSwings.Prepare(state.Setup,state.Original);
            ReviveNativeActivation.Restore(ReviveNativeActivation.Snapshot().Select(r=>r.Actor==state.Actor?r with{Setup=setup}:r).ToArray());
        }
        ReviveSwings.Cancel(actor,false);Attach(net,owner,actor);Controls(owner,actor);actor.Location=new(owner.X+2,owner.Y,owner.Z);
        var enemy=Target(owner,actor,6);int initial=SkillHealth.Balance256(enemy);var original=actor;var ai=actor.AI;var origin=actor.Location;DateTime deadline=TimedEffects.Now.AddSeconds(20);ReviveSwingState captured=null;
        NpcEquipmentRandom.Set(actor,HitSeed());actor.IssueOrder(OrderType.Guard,null);actor.AIObject.AITimer.Start();
        Log.Information("revive-native-swing-live: ready");
        void Inspect()
        {
            try
            {
                Require(net.Running&&actor==original&&actor.AI==ai&&actor.Alive&&TimedEffects.Now<deadline,"Native guard did not acquire/pursue/release its original A1: "+actor.Location+"/"+actor.Combatant);
                var row=ReviveSwings.State(actor);if(row?.Pending!=null){captured??=row;Require(actor.Location==row.Origin&&SkillHealth.Balance256(enemy)==initial,"Native AI moved or dealt damage during windup");}
                if(SkillHealth.Balance256(enemy)<initial)
                {
                    Require(captured!=null&&origin!=captured.Origin&&actor.Location==captured.Origin&&enemy.Notifications==1&&actor.Combatant==enemy,"Natural damage did not follow source windup/pursuit or repeated native damage");
                    actor.AIObject.AITimer.Stop();actor.Combatant=null;actor.FocusMob=null;enemy.Delete();owner.Internalize();actor.Internalize();
                    File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,SkillHealth.Balance256(actor),generation)));
                    Log.Information("revive-native-swing-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){actor.AIObject.AITimer.Stop();enemy.Delete();Log.Error(e,"revive-native-swing-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(40),()=>Core.LoopContext.Post(Inspect));
    }
    private static void Save(NetState net)
    {
        try
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var actor=World.FindMobile((Serial)saved.Actor)as BaseCreature;
            ReviveSwings.Cancel(actor,false);Attach(net,owner,actor);var target=Target(owner,actor);NpcEquipmentRandom.Set(actor,HitSeed());
            // Inject a future UTC epoch so a real save/reload can test a remaining
            // busy deadline despite loading taking longer than a normal A1.
            _=ReviveSwings.Begin(actor,target,TimedEffects.Now.AddHours(1));var row=ReviveSwings.State(actor);Require(row?.Pending!=null,"Save control lacks a captured pending packet");
            SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)*2/3+17);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(actor),Target=target.Serial.Value,TargetLife=SkillHealth.Balance256(target),Seed=NpcEquipmentRandom.State(actor).Seed,EndUtc=row.EndUtc}));
            Log.Information("revive-native-swing-save-result: passed");
            if(HolyShieldCastChecks.HasSavedFixture)
            {HolyShieldCastChecks.StageSave();Log.Information("holy-shield-cast-save: passed");}
            else{World.Save();}
        }
        catch(Exception e){Log.Error(e,"revive-native-swing-save-result: failed");}
    }
}
