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

// A reviewable proposed input policy, confined to integration fixtures.
// This does not register a learned handler or claim D2R input chronology.
public static class InfernoHeldCandidateChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(InfernoHeldCandidateChecks));
    private sealed record Saved(uint Owner,int Mana256,int Life256,SorceressSpellFlight[] Flights,bool HeldAtFirstSave,int Generation=0,int UnresumedSaves=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"inferno-held-candidate-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var hits=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="InfernoHeldCandidate"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="InfernoHeldCandidate"?FixtureSpace.Retention:hits?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static SorceressSpellFlight[] Own(PlayerMobile p)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).ToArray();
    private static bool Press(PlayerMobile p,Point3D point,DateTime epoch)
    {
        // Proposed rule: a new press after release has a fresh windup/lease.
        PlayerInfernoModes.Cancel(p);
        return PlayerInfernoModes.BeginPrepared(p,CombatAbility.Inferno,Serial.Zero,point,epoch);
    }
    private static bool Hold(PlayerMobile p,DateTime until)
    {
        var cast=PlayerInfernoModes.State(p);if(cast==null){return false;}
        int end=(int)((until-cast.EpochUtc).TotalMilliseconds/40);
        for(int frame=cast.Sequence.Frame;frame<=end;frame++)
        {
            cast=PlayerInfernoModes.State(p);if(cast==null){return false;}
            var phase=cast.EpochUtc.AddMilliseconds(frame*40L);
            bool action=cast.Sequence.Animation.Events.Any(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Frame==frame&&e.Kind is 1 or 2);
            // Input renewal is the proposed adaptation, not a finding about
            // when the modern D2R client/server accepts a start callback.
            if(action&&!PlayerInfernoModes.StartCallback(p,phase)){PlayerInfernoModes.Cancel(p);return false;}
            PlayerInfernoModes.Advance(p,phase);
            if(PlayerInfernoModes.State(p)?.Sequence.UsedSkill!=true){PlayerInfernoModes.Cancel(p);return false;}
        }
        return true;
    }
    private static void Release(PlayerMobile p)=>PlayerInfernoModes.Cancel(p);
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=new PlayerMobile{Name="InfernoHeldCandidate",Player=true,Body=0x190};}finally{Creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"sorceress.inferno");
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Candidate energy allocation");}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Candidate persisted strength for real gear");}
        FixtureSpace.Corridor(p,6280,20,3500);Require(p.ManaMax>=80,"Candidate needs its real80mana reserve");return p;
    }
    private static void Reset(PlayerMobile p,SorceressSpellSave before,int mana=80*256)
    {Release(p);SorceressSpellMissiles.Restore(before);ClassCombat.Stop(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount-1;NecromancerEffects.SetMana256(p,mana);}
    private static void Cases(PlayerMobile p)
    {
        var before=SorceressSpellMissiles.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+3,p.Y,p.Z);
        try
        {
            Reset(p,before,1535);Require(!Press(p,point,at)&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==1535&&Own(p).Length==0,"Candidate changed failed six-mana entry");
            foreach(int chunk in new[]{1,3,7})
            {
                Reset(p,before);Require(Press(p,point,at)&&NecromancerEffects.ManaBalance256(p)==80*256-96,"Candidate fresh fractional debit");
                for(int frame=0;frame<=50;frame+=chunk){Require(Hold(p,at.AddMilliseconds(frame*40L)),"Candidate hold stopped early");}
                Require(Hold(p,at.AddMilliseconds(2000)),"Candidate final catchup");var shots=Own(p);
                Require(shots.Length==20&&NecromancerEffects.ManaBalance256(p)==80*256-22*96,"Candidate2second payment/emission count differs: "+shots.Length+"/"+NecromancerEffects.ManaBalance256(p));
                Require(shots.OrderBy(f=>f.Id).Select(f=>(int)((f.NextUtc-at).TotalMilliseconds/40)-1).SequenceEqual(Enumerable.Range(0,20).Select(i=>12+2*i)),"Candidate lost first12/two-frame emission phase");
                string active=Json(PlayerInfernoModes.State(p));Require(Hold(p,at.AddMilliseconds(2000))&&Json(PlayerInfernoModes.State(p))==active&&Own(p).Length==20,"Duplicate input/tick billed or rewound again");
                Release(p);Require(PlayerInfernoModes.State(p)==null&&Own(p).Length==20,"Candidate release erased fired flames or kept a channel");
            }
            Reset(p,before,1536);Require(Press(p,point,at),"Candidate finite entry");Require(!Hold(p,at.AddMilliseconds(4000))&&PlayerInfernoModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==0&&Own(p).Length==14,"Candidate six mana did not fund16debits/14emissions before exhaustion");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(360)),"Candidate short press");Release(p);Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==80*256-96,"Early release emitted or refunded paid entry");
            Require(Press(p,point,at.AddMilliseconds(400))&&Hold(p,at.AddMilliseconds(840))&&Own(p).Length==0,"Fresh reentry skipped windup");
            Require(Hold(p,at.AddMilliseconds(880))&&Own(p).Length==1,"Fresh reentry failed its new first flame");Release(p);
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Candidate gear scene");var first=Own(p).Single();
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Candidate real soft-rank gear");
            try{Require(Hold(p,at.AddMilliseconds(560))&&Own(p).Length==2&&Json(Own(p).Single(f=>f.Id==first.Id))==Json(first)&&Own(p).Single(f=>f.Id!=first.Id).Rank==2&&NecromancerEffects.ManaBalance256(p)==80*256-3*96-100,"Candidate cached rank/cost or recaptured old flame");}
            finally{p.AddToBackpack(helm);helm.Delete();}
            Reset(p,before);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(point,p.Map);foreach(var kind in Enum.GetValues<ResistanceType>()){target.SetResistance(kind,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);
            try
            {
                int hp=SkillHealth.Balance256(target);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(2000)),"Candidate real damage setup");Release(p);int mana=NecromancerEffects.ManaBalance256(p);
                for(int frame=1;frame<=100;frame++){SorceressSpellMissiles.Tick(at.AddMilliseconds(frame*40L),true);}
                Require(SkillHealth.Balance256(target)<hp&&NecromancerEffects.ManaBalance256(p)==mana,"Released candidate flames did not deal independent damage or billed again");
            }
            finally{NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Delete();}
        }
        finally{Reset(p,before);}
        Log.Information("Inferno candidate:6mana threshold/96FP press;first flame frame12,then2frame renewal;rank1steady4.6875mana/sec;chunk-independent2second22debits/20flames;6mana16debits/14flames;instant release/fresh windup/live gear and independent fired damage pass;not enabled");
    }
    private static void Check(NetState net,bool reload)
    {
        Require(!SorceressCombat.Implemented(CombatAbility.Inferno),"Unapproved candidate enabled learned Inferno");
        if(!reload)
        {
            var p=Owner();FixtureNetwork.Authenticate(net,p,"inferno-candidate-");Cases(p);
            var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+3,p.Y,p.Z);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(2000)),"Candidate held save setup");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),Own(p),false)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureNetwork.Authenticate(net,p,"inferno-candidate-reload-");
            Require(saved.HeldAtFirstSave&&PlayerInfernoModes.State(p)==null&&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Flights),"Candidate intent replayed or original wounds/mana/flights changed after restart");
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
    }
    private static void SaveConnected(NetState net)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        // The old socket may still be awaiting its asynchronous disconnect.
        // End any preceding input explicitly before staging this save press.
        Release(p);FixtureNetwork.Authenticate(net,p,"inferno-candidate-save-");
        if(saved.Generation==0)
        {
            // The preceding command's socket has closed. Start a new, actually
            // connected press at the save boundary; retain all original flights.
            Require(Press(p,new Point3D(p.X+3,p.Y,p.Z),TimedEffects.Now.Add(FixtureSpace.Retention)),"Candidate connected save press");
            File.WriteAllText(Manifest,Json(saved with{Mana256=NecromancerEffects.ManaBalance256(p)}));
        }
        World.Save();
    }
    private static void SaveBoundary()
    {
        if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;if(p==null){return;}
        Require(SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Flights),"Candidate original save boundary resources/flights changed");
        bool held=PlayerInfernoModes.Active(p);Require(saved.Generation==0?held:!held,"Candidate first save lost held mode or reload restored it");
        File.WriteAllText(Manifest,Json(saved with{HeldAtFirstSave=saved.HeldAtFirstSave||held,UnresumedSaves=saved.UnresumedSaves+(saved.Generation>0?1:0)}));
    }
    public static void Configure()
    {
        Rates();EventSink.WorldSave+=SaveBoundary;
        ConsoleInputHandler.RegisterCommand("inferno-held-candidate-save","Connected candidate save boundary",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("inferno-held-candidate-save",SaveConnected)));
        foreach(string name in new[]{"inferno-held-candidate-check","inferno-held-candidate-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Review-only held Inferno input policy",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
}
