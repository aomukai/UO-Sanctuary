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

// Approved Sanctuary held adapter; this is not a modern D2R chronology claim.
public static class ArcticHeldInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ArcticHeldInputChecks));
    private sealed record Saved(uint Owner,int Mana256,int Life256,ArcticMissile[] Missiles,bool HeldAtFirstSave,int Generation=0,int UnresumedSaves=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"arctic-held-input-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var hits=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="ArcticHeldInput"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="ArcticHeldInput"?FixtureSpace.Retention:hits?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static ArcticMissile[] Own(PlayerMobile p)=>DruidArcticStream.Snapshot().Missiles.Where(f=>f.Owner==p.Serial.Value).ToArray();
    private static bool Press(PlayerMobile p,Point3D point,DateTime epoch)
    {
        PlayerInfernoModes.Cancel(p);
        return PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,1,CombatAbility.ArcticBlast,Serial.Zero,point,epoch);
    }
    private static bool Hold(PlayerMobile p,DateTime until)
    {
        var cast=PlayerInfernoModes.State(p);if(cast==null){return false;}
        int end=(int)((until-cast.EpochUtc).TotalMilliseconds/40);
        // Actual transport renewals are at most200ms apart. Their chunking is
        // independent of the40ms source actions and must not change payment.
        for(int frame=cast.Sequence.Frame;frame<=end;frame=Math.Min(frame+5,end))
        {
            if(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,1,CombatAbility.ArcticBlast,Serial.Zero,cast.Point,cast.EpochUtc.AddMilliseconds(frame*40L))){return false;}
            if(frame==end){return true;}
        }
        return true;
    }
    private static void Release(PlayerMobile p){PlayerInfernoModes.EndHeld(p,1);}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Name="ArcticHeldInput",Player=true,Body=0x190};}finally{Creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"druid.arcticblast");
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Candidate energy allocation");}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Candidate persisted strength for real gear");}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ArcticBlast),"Bind approved Inferno");
        FixtureSpace.Corridor(p,6360,20,3500);Require(p.ManaMax>=80,"Candidate needs its real80mana reserve");return p;
    }
    private static void Reset(PlayerMobile p,ArcticStreamSave before,int mana=80*256)
    {Release(p);DruidArcticStream.Restore(before);ClassCombat.Stop(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;NecromancerEffects.SetMana256(p,mana);}
    private static void Cases(PlayerMobile p)
    {
        var before=DruidArcticStream.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+3,p.Y,p.Z);
        try
        {
            Reset(p,before);
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ArcticBlast)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ArcticBlast),"Bind left and quick Inferno");
            foreach(int source in new[]{0,1,9})
            {
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,source,CombatAbility.ArcticBlast,Serial.Zero,point,at),"Heartbeat created a channel");
                Require(PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,source,CombatAbility.ArcticBlast,Serial.Zero,point,at),"Bound source refused press");
                string state=Json(PlayerInfernoModes.State(p));int mana=NecromancerEffects.ManaBalance256(p);
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,source,CombatAbility.ArcticBlast,Serial.Zero,point,at)&&Json(PlayerInfernoModes.State(p))==state&&NecromancerEffects.ManaBalance256(p)==mana,"Duplicate press billed/restarted");
                Require(!PlayerInfernoModes.EndHeld(p,(source+1)%14)&&PlayerInfernoModes.Active(p),"Unrelated release cancelled channel");
                Require(!PlayerInfernoModes.Advance(p,at.AddMilliseconds(750))&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Expired transport created catchup damage/payment");
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,source,CombatAbility.ArcticBlast,Serial.Zero,point,at.AddMilliseconds(800)),"Heartbeat revived expired press");
                Reset(p,before);
            }
            Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,13,CombatAbility.ArcticBlast,Serial.Zero,point,at),"Unbound quick source accepted");
            Require(!ClassCombat.ContinueCast(p,CombatAbility.ArcticBlast,null,point,Core.TickCount)&&!ClassCombat.Perform(p,CombatAbility.ArcticBlast,null,point,Core.TickCount),"Legacy one-shot/heartbeat bypassed explicit press");
            Reset(p,before,1535);Require(!Press(p,point,at)&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==1535&&Own(p).Length==0,"Candidate changed failed six-mana entry");
            foreach(int chunk in new[]{1,3,7})
            {
                Reset(p,before);Require(Press(p,point,at)&&NecromancerEffects.ManaBalance256(p)==80*256-96,"Candidate fresh fractional debit");
                for(int frame=0;frame<=50;frame+=chunk){Require(Hold(p,at.AddMilliseconds(frame*40L)),"Candidate hold stopped early");}
                Require(Hold(p,at.AddMilliseconds(2000)),"Candidate final catchup");var shots=Own(p);
                Require(shots.Length==20&&NecromancerEffects.ManaBalance256(p)==80*256-22*96,"Candidate2second payment/emission count differs: "+shots.Length+"/"+NecromancerEffects.ManaBalance256(p));
                Require(shots.OrderBy(f=>f.Id).Select(f=>(int)((f.NextUtc-at).TotalMilliseconds/40)-1).SequenceEqual(Enumerable.Range(0,20).Select(i=>12+2*i)),"Candidate lost first12/two-frame emission phase");
                string active=Json(PlayerInfernoModes.State(p));Require(Hold(p,at.AddMilliseconds(2000))&&Json(PlayerInfernoModes.State(p))==active&&Own(p).Length==20,"Duplicate input/tick billed or rewound again");
                Release(p);Require(PlayerInfernoModes.State(p)==null&&Own(p).Length==20,"Candidate release erased fired streams or kept a channel");
            }
            Reset(p,before,1536);Require(Press(p,point,at),"Candidate finite entry");Require(!Hold(p,at.AddMilliseconds(4000))&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==0&&Own(p).Length==14,"Candidate six mana did not fund16debits/14emissions before exhaustion");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(360)),"Candidate short press");Release(p);Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==80*256-96,"Early release emitted or refunded paid entry");
            Require(Press(p,point,at.AddMilliseconds(400))&&Hold(p,at.AddMilliseconds(840))&&Own(p).Length==0,"Fresh reentry skipped windup");
            Require(Hold(p,at.AddMilliseconds(880))&&Own(p).Length==1,"Fresh reentry failed its new first stream");Release(p);
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Candidate gear scene");var first=Own(p).Single();Require(first.Damage.ColdFrames==100&&first.Damage.Frames==17&&PlayerInfernoModes.State(p).Ability==CombatAbility.ArcticBlast,"Held Arctic borrowed fire packet or sequence context");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Candidate real soft-rank gear");
            try{Require(Hold(p,at.AddMilliseconds(560))&&Own(p).Length==2&&Json(Own(p).Single(f=>f.Id==first.Id))==Json(first)&&Own(p).Single(f=>f.Id!=first.Id).Rank==2&&NecromancerEffects.ManaBalance256(p)==80*256-3*96-100,"Candidate cached rank/cost or recaptured old stream");}
            finally{p.AddToBackpack(helm);helm.Delete();}
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Arctic aim setup");
            var east=Own(p).Single();var south=new Point3D(p.X,p.Y+3,p.Z);
            Require(PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,1,CombatAbility.ArcticBlast,Serial.Zero,south,at.AddMilliseconds(520))
                &&Hold(p,at.AddMilliseconds(560)),"Arctic held aim update");
            var aimed=Own(p).Single(x=>x.Id!=east.Id);
            Require(east.Vx>0&&east.Vy==0&&aimed.Vx==0&&aimed.Vy>0&&Json(Own(p).Single(x=>x.Id==east.Id))==Json(east),"Aim rewrote an old stream or failed to redirect the next");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Arctic Stop setup");
            int stopped=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,1,CombatAbility.ArcticBlast,Serial.Zero,point,at.AddMilliseconds(560))
                &&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==stopped,"Stop resumed,billed or removed fired stream");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Arctic movement setup");
            var origin=p.Location;stopped=NecromancerEffects.ManaBalance256(p);p.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),p.Map);
            Require(!PlayerInfernoModes.Advance(p,at.AddMilliseconds(560))&&PlayerInfernoModes.State(p)==null
                &&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==stopped,"Movement retained a channel or rewrote fired streams");p.MoveToWorld(origin,p.Map);
            Reset(p,before);ClassSkillChecks.Train(p,"druid.firestorm");Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Arctic rebind setup");
            stopped=NecromancerEffects.ManaBalance256(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Firestorm),"Bind alternate learned skill");
            Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,1,CombatAbility.ArcticBlast,Serial.Zero,point,at.AddMilliseconds(560))
                &&PlayerInfernoModes.State(p)==null&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==stopped,"Rebinding carried a held channel into another skill");
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ArcticBlast),"Restore Arctic binding");
            Reset(p,before);Require(CombatPotions.Apply(p,3,at),"Held Arctic potion setup");
            Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(2000))&&Own(p).Length==20&&Own(p).All(x=>x.Modifiers.Potion==0),"Potion blocked learned stream or froze its bonus");Release(p);TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);
            Reset(p,before);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(point,p.Map);foreach(var kind in Enum.GetValues<ResistanceType>()){target.SetResistance(kind,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);
            try
            {
                int hp=SkillHealth.Balance256(target);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(2000)),"Candidate real damage setup");Release(p);int mana=NecromancerEffects.ManaBalance256(p);
                for(int frame=1;frame<=100;frame++){DruidArcticStream.Tick(at.AddMilliseconds(frame*40L),true);}
                Require(SkillHealth.Balance256(target)<hp&&NecromancerEffects.ManaBalance256(p)==mana,"Released candidate streams did not deal independent damage or billed again");
            }
            finally{NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Delete();}
        }
        finally{TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);Reset(p,before);}
        Log.Information("Inferno candidate:6mana threshold/96FP press;first stream frame12,then2frame renewal;rank1steady4.6875mana/sec;chunk-independent2second22debits/20streams;6mana16debits/14streams;instant release/fresh windup/live gear and independent fired damage pass;approved held controller");
    }
    private static void Check(NetState net,bool reload)
    {
        Require(DruidCombat.Implemented(CombatAbility.ArcticBlast),"Approved Arctic Blast handler missing");
        if(!reload)
        {
            var p=Owner();FixtureNetwork.Authenticate(net,p,"arctic-candidate-");Cases(p);
            var at=TimedEffects.Now.Add(FixtureSpace.Retention*3);var point=new Point3D(p.X+3,p.Y,p.Z);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(2000)),"Candidate held save setup");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),Own(p),false)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureNetwork.Authenticate(net,p,"arctic-candidate-reload-");
            Require(saved.HeldAtFirstSave&&PlayerInfernoModes.State(p)==null&&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Missiles),"Candidate intent replayed or original wounds/mana/flights changed after restart");
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
    }
    private static void SaveConnected(NetState net)
    {StageConnected(net);World.Save();}
    private static void StageConnected(NetState net)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        // The old socket may still be awaiting its asynchronous disconnect.
        // End any preceding input explicitly before staging this save press.
        Release(p);FixtureNetwork.Authenticate(net,p,"arctic-candidate-save-");
        if(saved.Generation==0)
        {
            // The preceding command's socket has closed. Start a new, actually
            // connected press at the save boundary; retain all original flights.
            Require(Press(p,new Point3D(p.X+3,p.Y,p.Z),TimedEffects.Now.Add(FixtureSpace.Retention)),"Candidate connected save press");
            File.WriteAllText(Manifest,Json(saved with{Mana256=NecromancerEffects.ManaBalance256(p)}));
        }
    }
    private static void SaveBoundary()
    {
        if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;if(p==null){return;}
        Require(SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Missiles),"Candidate original save boundary resources/flights changed");
        bool held=PlayerInfernoModes.Active(p);Require(saved.Generation==0?held:!held,"Candidate first save lost held mode or reload restored it");
        File.WriteAllText(Manifest,Json(saved with{HeldAtFirstSave=saved.HeldAtFirstSave||held,UnresumedSaves=saved.UnresumedSaves+(saved.Generation>0?1:0)}));
    }
    private static void Wire(NetState net,int source)
    {
        string label="arctic-held-wire-"+source;var before=DruidArcticStream.Snapshot();var p=Owner();
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ArcticBlast)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ArcticBlast),"Wire bindings");
        var account=FixtureNetwork.Authenticate(net,p,"arctic-wire-");Reset(p,before);
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        foreach(var kind in Enum.GetValues<ResistanceType>()){target.SetResistance(kind,0);}
        TimedEffects.PreventHealing(target,TimedEffects.Now);int hp=SkillHealth.Balance256(target),phase=0,stoppedMana=0;DateTime stopped=default;
        long deadline=Core.TickCount+7000;
        void Cleanup(){PlayerInfernoModes.Cancel(p);DruidArcticStream.Restore(before);NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Delete();account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline&&net.Running,"Wire timeout/disconnect");var cast=PlayerInfernoModes.State(p);
                if(phase==0&&cast!=null)
                {
                    Require(cast.HeldSource==source&&NecromancerEffects.ManaBalance256(p)==80*256-96&&Own(p).Length==0,"Wire entry charged twice/emitted early/wrong source");
                    phase=1;Log.Information("{Label}: pressed",label);
                }
                if(phase==1&&SkillHealth.Balance256(target)<hp)
                {
                    Require(cast is{Sequence.Frame:>=12}&&NecromancerEffects.ManaBalance256(p)<=80*256-3*96&&cast.HeldSource==source,"Natural stream skipped windup/renewal");
                    phase=2;Log.Information("{Label}: burning",label);
                }
                if(phase==2&&cast==null){phase=3;stopped=TimedEffects.Now;stoppedMana=NecromancerEffects.ManaBalance256(p);}
                if(phase==3)
                {
                    Require(cast==null&&NecromancerEffects.ManaBalance256(p)==stoppedMana,"Released wire heartbeat restarted channel/payment");
                    if(TimedEffects.Now-stopped>=TimeSpan.FromMilliseconds(300))
                    {Cleanup();Log.Information("{Label}-result: passed",label);return;}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Log.Information("{Label}: ready ability={Ability} x={X} y={Y} z={Z}",label,(int)CombatAbility.ArcticBlast,target.X,target.Y,target.Z);Observe();
    }
    public static void Configure()
    {
        Rates();EventSink.WorldSave+=SaveBoundary;
        foreach(int source in new[]{0,1,9})
        {int input=source;string name="arctic-held-wire-"+source;ConsoleInputHandler.RegisterCommand(name+"-check","Actual held channel wire/natural timer",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name+"-result",net=>Wire(net,input))));}
        ConsoleInputHandler.RegisterCommand("arctic-held-input-save-stage-check","Stage a connected press without saving",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("arctic-held-input-save-stage-check",net=>{try{StageConnected(net);Log.Information("arctic-held-input-save-stage-check: passed");}catch(Exception e){Log.Error(e,"arctic-held-input-save-stage-check: failed");}})));
        ConsoleInputHandler.RegisterCommand("arctic-held-input-save","Connected candidate save boundary",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("arctic-held-input-save",SaveConnected)));
        foreach(string name in new[]{"arctic-held-input-check","arctic-held-input-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Approved held Arctic Blast adapter",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
}
