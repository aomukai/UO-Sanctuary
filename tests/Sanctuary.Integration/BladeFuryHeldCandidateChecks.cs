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

// Review-only input policy. No production input, handler or client is changed.
public static class BladeFuryHeldCandidateChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BladeFuryHeldCandidateChecks));
    private sealed record Saved(uint Owner,int Mana256,int Life256,WeaponFlight[] Flights,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-held-candidate-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var hits=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="BladeFuryHeldCandidate"?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="BladeFuryHeldCandidate"?FixtureSpace.Retention:hits?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static WeaponFlight[] Own(PlayerMobile p)=>WeaponMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Ability==CombatAbility.BladeFury).ToArray();
    private static bool Press(PlayerMobile p,Point3D point,DateTime at)
    {
        // A new physical press discards the unresolved released/future-shot
        // reentry path. This is a proposed adaptation, not reference evidence.
        BladeFuryModes.Cancel(p);return BladeFuryModes.BeginPrepared(p,Serial.Zero,point,at);
    }
    private static bool Hold(PlayerMobile p,DateTime until,List<int> emissions=null)
    {
        var cast=BladeFuryModes.State(p);if(cast==null){return false;}
        int end=checked((int)((until-cast.EpochUtc).TotalMilliseconds/40));
        for(int frame=cast.Sequence.Frame;frame<=end;frame++)
        {
            cast=BladeFuryModes.State(p);if(cast==null){return false;}
            int count=Own(p).Length;var phase=cast.EpochUtc.AddMilliseconds(frame*40L);
            bool action=cast.Sequence.Animation.Events.Any(e=>e.Type==SourceAnimationEvents.ModeChange&&e.Frame==frame&&e.Kind is 1 or 2);
            // The older start wrapper reports failure at exact NextShot
            // equality. On that frame, advance the existing do event only;
            // it keeps the source strict-less-than emission test and rewind.
            // This acceptance policy must be explicitly approved for gameplay.
            if(action&&cast.Sequence.Channel.NextShot!=frame&&!BladeFuryModes.StartCallback(p,phase))
            {BladeFuryModes.Cancel(p);return false;}
            BladeFuryModes.Advance(p,phase);
            if(Own(p).Length>count){emissions?.Add(frame);}
            if(BladeFuryModes.State(p)?.Sequence.UsedSkill!=true){BladeFuryModes.Cancel(p);return false;}
        }
        return true;
    }
    private static void Release(PlayerMobile p)=>BladeFuryModes.Cancel(p);
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=BladeBodyChecks.Fresh(6380,30,3550);}finally{Creating=false;}
        p.Name="BladeFuryHeldCandidate";ClassSkillChecks.Train(p,"assassin.bladefury");
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Candidate energy allocation");}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Candidate real gear requirements");}
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
        Log.Information("Fury candidate:6mana threshold/no entry debit;first blade frame12,then5frames;rank1cost1mana/steady5mana per second;2second8blades/8debits;chunk-independent/duplicates/early release/fresh windup/live gear/aim and released damage pass;not enabled");
    }
    private static void Check(NetState net,bool reload)
    {
        Require(AssassinCombat.Implemented(CombatAbility.BladeFury),"Approved Fury handler missing");
        if(!reload)
        {
            var p=Owner();FixtureNetwork.Authenticate(net,p,"fury-candidate-");Cases(p);
            var at=TimedEffects.Now.Add(FixtureSpace.Retention);Require(Press(p,new(p.X+10,p.Y,p.Z),at)&&Hold(p,at.AddMilliseconds(2000)),"Saved candidate setup");Release(p);
            // Future inspection deadline isolates original packets from other
            // fixtures' synthetic clocks. No packet payload or identity changes.
            var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with{Flights=all.Flights.Select(f=>f.Owner==p.Serial.Value?f with{NextUtc=at.Add(FixtureSpace.Retention)}:f).ToArray()});
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),Own(p))));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);FixtureNetwork.Authenticate(net,p,"fury-candidate-reload-");
            Require(BladeFuryModes.State(p)==null&&SkillHealth.Balance256(p)==saved.Life256&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&Json(Own(p))==Json(saved.Flights),"Original candidate wounds/mana/packets changed or held intent replayed");
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
    }
    public static void Configure()
    {
        Rates();foreach(string name in new[]{"blade-fury-held-candidate-check","blade-fury-held-candidate-reload-check"})
        ConsoleInputHandler.RegisterCommand(name,"Review-only Fury hold policy; no learned input",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
}
