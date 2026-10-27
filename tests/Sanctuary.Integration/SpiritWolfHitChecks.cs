// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SpiritWolfHitChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfHitChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("spirit-wolf-hit-check","Ordinary wolf cold damage and defenses",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("spirit-wolf-hit-check: passed");}catch(Exception e){Log.Error(e,"spirit-wolf-hit-check: failed");}}));
    private static void Numeric()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"spirit-wolf-hit-source-probe.json")));
        int[][] ranges=[[2,6,31],[343,359,875],[5,5,31]];int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();if(N(6)!=1 || N(8)!=1){continue;}
            var bounds=new List<int>();int Limited(int bound){bounds.Add(bound);return N(4)==1?bound-1:0;}uint Raw(){bounds.Add(-1);return(uint)N(5);}
            var range=ranges[N(0)];var packet=DruidSpiritWolfHit.Packet(range[0]*256,range[1]*256,range[2],0,0,0,0,N(3),5,Limited,Raw);
            Require(packet.Cold256==N(10) && packet.ColdFrames==N(11) && bounds.Count==N(12) && packet.Critical==(N(5)<5)
                && packet.Fire256==0 && packet.Lightning256==0 && bounds.SequenceEqual(row[16].EnumerateArray().Select(x=>x.GetInt32())),"Wolf full-source cold packet/RNG mismatch");count++;
        }
        Require(count==384,"Wolf skip-physical source inventory");
        var ordered=new List<int>();int Low(int bound){ordered.Add(bound);return 0;}
        var combined=DruidSpiritWolfHit.Packet(512,1536,31,256,512,256,768,0,5,Low,()=>{ordered.Add(-1);return 0;});
        Require(ordered.SequenceEqual(new[]{256,512,1024,-1}) && combined is{Fire256:512,Lightning256:512,Cold256:1024,ColdFrames:31,Critical:true},"Typed channel order or all-channel monster critical");
    }
    private static ulong Seed(DruidSpiritWolfBody wolf,BaseCreature target,bool hit,bool critical=false)
    {
        int chance=DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target);
        for(ulong initial=1;initial<10000;initial++)
        {
            ulong seed=initial;if(AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref seed))!=hit){continue;}
            if(!hit){return initial;}var birth=wolf.Prepared.Snapshot.Birth;
            if(birth.ColdMaximum>birth.ColdMinimum){SourceRandom.Limited(ref seed,(birth.ColdMaximum-birth.ColdMinimum)*256);}
            if((SourceRandom.Roll(ref seed)%100<5)==critical){return initial;}
        }
        throw new InvalidOperationException("No deterministic wolf seed");
    }
    private static void Shield(BaseCreature target,int choice)=>SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value)
        .Append(new SourceShield(target.Serial.Value,CreatureProfiles.Definition(target).Reference,choice)).ToArray());
    private static void Check()
    {
        Numeric();var p=SpiritWolfWorldChecks.Fresh();var at=TimedEffects.Now.AddHours(1);
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,1);
        var target=new GraveyardSkeleton{CantWalk=true,NoKillAwards=true};target.SetHits(10000);target.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        var original=SourceBlocking.BlockRoll;
        SpiritWolfHitResult Resolve(ref ulong seed)=>DruidSpiritWolfHit.Resolve(wolf,p,wolf.Prepared.Controller,target,ref seed);
        try
        {
            target.SetResistance(ResistanceType.Cold,0);Shield(target,0);SourceBlocking.BlockRoll=_=>99;
            DruidSpiritWolfWorld.Advance(wolf,wolf.Prepared.Controller.NextUtc.AddMilliseconds(24*40),true);
            Require(wolf.Prepared.Controller is{Mode:4,AttackPacket:{Rank:1,Attack:125}},"Direct hit fixture did not enter source A1");
            int chance=DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target);wolf.Dex=200;p.Dex=400;
            Require(DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target)==chance,"Native/owner Dexterity leaked into source monster attack");
            ulong seed=Seed(wolf,target,false),beforeSeed=seed;int life=SkillHealth.Balance256(target);
            var miss=Resolve(ref seed);Require(miss.Outcome==SpiritWolfHitOutcome.Miss && seed!=beforeSeed && SkillHealth.Balance256(target)==life && GearEffects.ChillPercent(target)==0,"Miss changed health/state");
            Shield(target,1);Require(SourceBlocking.TryRate(target,out int block,out _) && block==10,"Wolf shield target supplier");SourceBlocking.BlockRoll=_=>0;
            seed=Seed(wolf,target,true);var blocked=Resolve(ref seed);
            Require(blocked.Outcome==SpiritWolfHitOutcome.Blocked && blocked.Cold256==0 && SkillHealth.Balance256(target)==life && GearEffects.ChillPercent(target)==0,"Cold-only ordinary melee bypassed shield block");
            SourceBlocking.BlockRoll=_=>99;seed=Seed(wolf,target,true);var cold=Resolve(ref seed);
            Require(cold is{Outcome:SpiritWolfHitOutcome.Hit,Fire256:0,Lightning256:0,Critical:false} && cold.Cold256>=wolf.Prepared.Snapshot.Birth.ColdMinimum*256
                && cold.Cold256<wolf.Prepared.Snapshot.Birth.ColdMaximum*256 && SkillHealth.Balance256(target)==life-cold.Cold256 && GearEffects.ChillPercent(target)>0,"Cold FP256 hit or state did not commit");
            target.SetResistance(ResistanceType.Cold,100);life=SkillHealth.Balance256(target);seed=Seed(wolf,target,true,true);var immune=Resolve(ref seed);
            Require(immune is{Outcome:SpiritWolfHitOutcome.Hit,Critical:true} && SkillHealth.Balance256(target)==life,"Provisional cold-only packet leaked physical floor through immunity");
            target.SetResistance(ResistanceType.Cold,0);SorceressEffectProfiles effects=SorceressEffects.Profiles;
            effects.Store(wolf,new(Enchantment:new(at.AddHours(1),1,1024,1024,100,p.Serial.Value)));
            Require(DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target)>=chance,"Live Enchant AR was ignored");seed=Seed(wolf,target,true);var enchanted=Resolve(ref seed);
            Require(enchanted.Outcome==SpiritWolfHitOutcome.Hit && enchanted.Fire256==(enchanted.Critical?2048:1024),"Live actor Enchant fire missing or double counted");effects.Clear(wolf);
            target.Location=new(p.X+3,p.Y+1,p.Z);
            Require(DruidWolfGeometry.MeleeDistance(DruidSpiritWolfWorld.Cell(wolf.Prepared.Controller),PetOwnerRoutes.Cell(target),2,CreatureProfiles.For(target).Traits.Size.Value)>2
                && DruidSpiritWolfHit.InRange(wolf,wolf.Prepared.Controller,target),"Ordinary impact lost extra source reach");
            target.Location=new(p.X+10,p.Y,p.Z);seed=1;var distant=Resolve(ref seed);
            Require(distant.Outcome==SpiritWolfHitOutcome.Invalid && seed==1,"Lost-range impact consumed RNG");
            var state=wolf.Prepared;wolf.RestorePrepared(state with{Controller=state.Controller with{Profile="source-ai-native-ground-fit-pending-hit-v1"}});
            Require(wolf.Prepared.Controller.Profile=="source-ai-native-ground-fit-mode-attack-v3" && wolf.Prepared.Controller.Seed==state.Controller.Seed,"Old prepared movement profile migration");
            target.Location=new(p.X+2,p.Y,p.Z);SkillHealth.SetBalance256(target,256);seed=Seed(wolf,target,true);
            var lethal=Resolve(ref seed);Require(lethal.Outcome==SpiritWolfHitOutcome.Hit && !target.Alive && target.LastKiller==wolf,"Lethal wolf packet lost killer identity");
            Log.Information("Spirit Wolf melee:384complete source cold packet/RNG comparisons; actual miss/block/FP256 cold/chill/immunity/critical/Enchant/reach and profile migration pass; physical floor and modern full parity remain provisional");
        }
        finally{SourceBlocking.BlockRoll=original;target.Delete();SpiritWolfWorldChecks.End(p);}
    }
}
