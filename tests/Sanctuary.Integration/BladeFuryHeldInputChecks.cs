// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

// Approved Sanctuary held input; exact current D2R input timing remains unverified.
public static class BladeFuryHeldInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BladeFuryHeldInputChecks));
    private sealed record Saved(uint Owner,int Mana256,int Life256,WeaponFlight[] Flights,bool HeldAtFirstSave,int Generation=0,int UnresumedSaves=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-held-input-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var hits=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="BladeFuryHeldInput"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="BladeFuryHeldInput"?FixtureSpace.Retention:hits?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Ability==CombatAbility.BladeFury).ToArray();
    private static bool Press(PlayerMobile p,Point3D point,DateTime at)
    {BladeFuryModes.Cancel(p);return PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,1,CombatAbility.BladeFury,Serial.Zero,point,at);}
    private static bool Hold(PlayerMobile p,DateTime until,List<int> emissions=null)
    {
        var cast=BladeFuryModes.State(p);if(cast==null){return false;}
        int end=checked((int)((until-cast.EpochUtc).TotalMilliseconds/40));
        for(int frame=cast.Sequence.Frame;frame<=end;frame=Math.Min(frame+5,end))
        {
            var ids=Own(p).Select(f=>f.Id).ToHashSet();
            if(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,1,CombatAbility.BladeFury,Serial.Zero,cast.Point,cast.EpochUtc.AddMilliseconds(frame*40L))){return false;}
            foreach(var flight in Own(p).Where(f=>!ids.Contains(f.Id)).OrderBy(f=>f.Id))
            {emissions?.Add(checked((int)((flight.NextUtc-cast.EpochUtc).TotalMilliseconds/40)));}
            if(frame==end){return true;}
        }
        return true;
    }
    private static void Release(PlayerMobile p){BladeFuryModes.EndHeld(p,1);}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=BladeBodyChecks.Fresh(6400,30,3550);}finally{Creating=false;}
        p.Name="BladeFuryHeldInput";ClassSkillChecks.Train(p,"assassin.bladefury");
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Candidate energy allocation");}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Candidate real gear requirements");}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BladeFury),"Bind Fury right");
        Require(p.ManaMax>=80,"Candidate real mana pool");return p;
    }
    private static void Reset(PlayerMobile p,WeaponFlightSave before,int mana=80*256)
    {
        Release(p);WeaponMissiles.Restore(before);ClassCombat.Stop(p);
        var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount-1;
        NecromancerEffects.SetMana256(p,mana);
    }
    private static void Cases(PlayerMobile p)
    {
        var before=WeaponMissiles.Snapshot();var at=TimedEffects.Now.Add(FixtureSpace.Retention);var point=new Point3D(p.X+10,p.Y,p.Z);
        try
        {
            Reset(p,before);
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeFury)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BladeFury),"Bind Fury left/quick");
            foreach(int source in new[]{0,1,9})
            {
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,source,CombatAbility.BladeFury,Serial.Zero,point,at),"Heartbeat created Fury");
                Require(PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,source,CombatAbility.BladeFury,Serial.Zero,point,at),"Bound Fury source refused press");
                string state=Json(BladeFuryModes.State(p));int mana=NecromancerEffects.ManaBalance256(p);
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,source,CombatAbility.BladeFury,Serial.Zero,point,at)
                    &&Json(BladeFuryModes.State(p))==state&&NecromancerEffects.ManaBalance256(p)==mana,"Duplicate Fury press changed state/payment");
                Require(!BladeFuryModes.EndHeld(p,(source+1)%14)&&BladeFuryModes.Active(p),"Unrelated release ended Fury");
                Require(!BladeFuryModes.Advance(p,at.AddMilliseconds(750))&&BladeFuryModes.State(p)==null&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Expired lease emitted/billed catchup");
                Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelAim,source,CombatAbility.BladeFury,Serial.Zero,point,at.AddMilliseconds(800)),"Heartbeat revived Fury");Reset(p,before);
            }
            Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,13,CombatAbility.BladeFury,Serial.Zero,point,at),"Unbound quick Fury accepted");
            Require(!ClassCombat.Perform(p,CombatAbility.BladeFury,null,point,Core.TickCount)&&!ClassCombat.ContinueCast(p,CombatAbility.BladeFury,null,point,Core.TickCount)
                &&!AssassinCombat.Begin(p,CombatAbility.BladeFury,Serial.Zero,point,Core.TickCount)&&!AssassinCombat.Release(p,CombatAbility.BladeFury),"One-shot Fury bypassed held input");
            Reset(p,before,1535);Require(!Press(p,point,at)&&BladeFuryModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==1535&&Own(p).Length==0,"Candidate below-six entry changed resources");
            foreach(int chunk in new[]{1,3,7})
            {
                Reset(p,before);var frames=new List<int>();
                Require(Press(p,point,at)&&NecromancerEffects.ManaBalance256(p)==80*256,"Candidate wrongly paid upfront");
                for(int frame=0;frame<=50;frame+=chunk){Require(Hold(p,at.AddMilliseconds(frame*40L),frames),"Candidate hold ended early");}
                Require(Hold(p,at.AddMilliseconds(2000),frames),"Candidate final catchup");
                Require(frames.SequenceEqual(new[]{12,17,22,27,32,37,42,47})&&Own(p).Length==8&&NecromancerEffects.ManaBalance256(p)==72*256,
                    "Candidate phase/payment mismatch: "+string.Join(',',frames)+" mana="+NecromancerEffects.ManaBalance256(p));
                string state=Json(BladeFuryModes.State(p));
                Require(Hold(p,at.AddMilliseconds(2000))&&Json(BladeFuryModes.State(p))==state&&Own(p).Length==8&&NecromancerEffects.ManaBalance256(p)==72*256,"Duplicate hold paid/emitted/rewound");
                Release(p);Require(BladeFuryModes.State(p)==null&&Own(p).Length==8,"Release erased issued blades or kept intent");
            }
            Reset(p,before,1536);Require(Press(p,point,at)&&!Hold(p,at.AddMilliseconds(4000))&&BladeFuryModes.State(p)==null&&NecromancerEffects.ManaBalance256(p)==0&&Own(p).Length==6,"Six mana did not fund exactly six blades");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(440)),"Short press setup");Release(p);
            Require(Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==80*256,"Short press emitted or paid");
            Require(Press(p,point,at.AddMilliseconds(480))&&Hold(p,at.AddMilliseconds(920))&&Own(p).Length==0,"Fresh press reused windup");
            Require(Hold(p,at.AddMilliseconds(960))&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==79*256,"Fresh first blade/payment");Release(p);
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Live gear setup");var first=Own(p).Single();
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);
            try
            {
                Require(p.EquipItem(helm)&&SkillMechanics.Rank(p,CombatAbility.BladeFury)==2,"Candidate real soft rank");
                Require(Hold(p,at.AddMilliseconds(720))&&Own(p).Length==2&&Json(Own(p).Single(f=>f.Id==first.Id))==Json(first)
                    &&Own(p).Single(f=>f.Id!=first.Id).SkillRank==2&&NecromancerEffects.ManaBalance256(p)==80*256-256-288,"Candidate stale packet/rank/cost");
            }
            finally{p.AddToBackpack(helm);helm.Delete();}
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Aim setup");first=Own(p).Single();
            Require(BladeFuryModes.Aim(p,Serial.Zero,new(p.X,p.Y+10,p.Z))&&Hold(p,at.AddMilliseconds(720)),"Aim update");
            var second=Own(p).Single(f=>f.Id!=first.Id);
            Require(first.Vx>0&&Math.Abs(first.Vy)<0.01&&second.Vy>0&&Math.Abs(second.Vx)<0.01&&Json(Own(p).Single(f=>f.Id==first.Id))==Json(first),"Aim rewrote issued blade");Release(p);
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Fury Stop setup");
            int stopped=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);
            Require(!Hold(p,at.AddMilliseconds(720))&&Own(p).Length==1&&NecromancerEffects.ManaBalance256(p)==stopped,"Stop resumed or removed issued Fury");
            Reset(p,before);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Fury movement setup");var origin=p.Location;
            p.MoveToWorld(new(origin.X,origin.Y+1,origin.Z),p.Map);
            Require(!BladeFuryModes.Advance(p,at.AddMilliseconds(560))&&BladeFuryModes.State(p)==null&&Own(p).Length==1,"Movement retained Fury");p.MoveToWorld(origin,p.Map);
            Reset(p,before);ClassSkillChecks.Train(p,"assassin.fireblast");Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Fury rebind setup");
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireBlast),"Bind alternate Assassin skill");
            Require(!Hold(p,at.AddMilliseconds(720))&&BladeFuryModes.State(p)==null&&Own(p).Length==1,"Rebind retained Fury");
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BladeFury),"Restore Fury binding");
            Reset(p,before);var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
            var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
            try
            {
                WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
                int hp=SkillHealth.Balance256(target);Require(Press(p,point,at)&&Hold(p,at.AddMilliseconds(480)),"Independent released damage setup");Release(p);
                int mana=NecromancerEffects.ManaBalance256(p);int guard=60;
                while(Own(p).Length>0&&guard-->0){WeaponMissiles.Tick(Own(p).Single().NextUtc);}
                Require(guard>0&&SkillHealth.Balance256(target)<hp&&NecromancerEffects.ManaBalance256(p)==mana,"Released blade failed independent damage or paid twice");
            }
            finally{WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;target.Delete();}
        }
        finally{Reset(p,before);}
        Log.Information("Fury candidate:6mana threshold/no entry debit;first blade frame12,then5frames;rank1cost1mana/steady5mana per second;2second8blades/8debits;chunk-independent/duplicates/early release/fresh windup/live gear/aim and released damage pass;approved held controller");
    }
    private static void Check(NetState net,bool reload)
    {
        Require(AssassinCombat.Implemented(CombatAbility.BladeFury),"Approved Fury handler missing");
        if(!reload)
        {
            var p=Owner();FixtureNetwork.Authenticate(net,p,"fury-held-");Cases(p);
            var at=TimedEffects.Now.Add(FixtureSpace.Retention);Require(Press(p,new(p.X+10,p.Y,p.Z),at)&&Hold(p,at.AddMilliseconds(2000)),"Saved candidate setup");Release(p);
            // Future inspection deadline isolates original packets from other
            // fixtures' synthetic clocks. No packet payload or identity changes.
            var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Owner==p.Serial.Value?f with{NextUtc=at.Add(FixtureSpace.Retention)}:f).ToArray()});
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),Own(p),false)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureNetwork.Authenticate(net,p,"fury-held-reload-");
            Require(saved.HeldAtFirstSave&&BladeFuryModes.State(p)==null&&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Flights),"Original candidate wounds/mana/packets changed or held intent replayed");
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
    }
    private static void StageConnected(NetState net)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Release(p);FixtureNetwork.Authenticate(net,p,"fury-held-save-");
        if(saved.Generation==0)
        {Require(Press(p,new(p.X+10,p.Y,p.Z),TimedEffects.Now.Add(FixtureSpace.Retention*3)),"Connected Fury save press");}
    }
    private static void SaveBoundary()
    {
        if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Flights),"Fury save boundary changed original resources/packets");
        bool held=BladeFuryModes.Active(p);Require(saved.Generation==0?held:!held,"First save lost Fury input or restart replayed it");
        File.WriteAllText(Manifest,Json(saved with{HeldAtFirstSave=saved.HeldAtFirstSave||held,UnresumedSaves=saved.UnresumedSaves+(saved.Generation>0?1:0)}));
    }
    private static void Wire(NetState net,int source)
    {
        string label="fury-held-wire-"+source;var before=WeaponMissiles.Snapshot();var p=Owner();
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BladeFury)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BladeFury),"Wire bindings");
        var account=FixtureNetwork.Authenticate(net,p,"fury-wire-");Reset(p,before);
        var target=MagicArrowChecks.Target(p);target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        foreach(var kind in Enum.GetValues<ResistanceType>()){target.SetResistance(kind,0);}
        TimedEffects.PreventHealing(target,TimedEffects.Now);int hp=SkillHealth.Balance256(target),phase=0,stoppedMana=0;DateTime stopped=default;
        var hit=WeaponMissiles.HitRoll;var damage=WeaponMissiles.DamageRoll;var block=SourceBlocking.BlockRoll;
        WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
        long deadline=Core.TickCount+7000;
        void Cleanup(){WeaponMissiles.HitRoll=hit;WeaponMissiles.DamageRoll=damage;SourceBlocking.BlockRoll=block;BladeFuryModes.Cancel(p);WeaponMissiles.Restore(before);NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Delete();account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline&&net.Running,"Wire timeout/disconnect");var cast=BladeFuryModes.State(p);
                if(phase==0&&cast!=null)
                {
                    Require(cast.HeldSource==source&&NecromancerEffects.ManaBalance256(p)==80*256&&Own(p).Length==0,"Wire entry charged twice/emitted early/wrong source");
                    phase=1;Log.Information("{Label}: pressed",label);
                }
                if(phase==1&&SkillHealth.Balance256(target)<hp)
                {
                    Require(cast is{Sequence.Frame:>=12}&&NecromancerEffects.ManaBalance256(p)<=80*256-256&&cast.HeldSource==source,"Natural Fury skipped windup/payment");
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
        Log.Information("{Label}: ready ability={Ability} x={X} y={Y} z={Z}",label,(int)CombatAbility.BladeFury,target.X,target.Y,target.Z);Observe();
    }
    public static void Configure()
    {
        Rates();EventSink.WorldSave+=SaveBoundary;
        ConsoleInputHandler.RegisterCommand("fury-held-input-save-stage-check","Stage original Fury owner connected through main save",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("fury-held-input-save-stage-check",net=>
        {try{StageConnected(net);Log.Information("fury-held-input-save-stage-check: passed");}catch(Exception e){Log.Error(e,"fury-held-input-save-stage-check: failed");}})));
        foreach(int source in new[]{0,1,9})
        {int input=source;string label="fury-held-wire-"+source;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Fury held input/natural timer",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(label+"-result",net=>Wire(net,input))));}
        foreach(string name in new[]{"blade-fury-held-input-check","blade-fury-held-input-reload-check"})
        ConsoleInputHandler.RegisterCommand(name,"Approved Fury held input",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
}
