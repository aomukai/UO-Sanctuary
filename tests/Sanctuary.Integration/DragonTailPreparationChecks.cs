// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DragonTailPreparationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonTailPreparationChecks));
    private sealed record Saved(uint Owner,uint Target,MartialCharge Charge,int Mana256,int Life256,ActionTiming Ordinary,ActionTiming Tail);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-tail-preparation-fixtures.json");
    public static void Configure(){Register("dragon-tail-preparation-check",Check);Register("dragon-tail-preparation-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Dragon Tail approved accuracy and isolated KK clock suppliers; separate from learned action",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Hold(MartialCharge charge)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=charge.Owner||x.Ability!=charge.Ability).Append(charge).ToArray());
    private static void Differential()
    {
        using var clocks=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-tail-clock-source-probe.json")));
        int count=0;
        foreach(var row in clocks.RootElement.EnumerateArray())
        {
            if(row[1].GetInt32()==0){continue;}
            var clock=DragonTailPreparation.Timing(row[1].GetInt32());
            Require(clock.EffectFrame==row[2].GetInt32()&&clock.Frames==row[3].GetInt32()&&row[4].GetInt32()==1,"Complete KK scheduler differential");count++;
        }
        Require(count==4480,"Incomplete KK clocks");
        using var rates=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"dragon-tail-rate-source-probe.json")));count=0;
        foreach(var row in rates.RootElement.EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();
            int advance=CombatTiming.FrameAdvance(new(AttackFamily.Swing,V(1)),V(3)+V(5),V(7),V(6),rateAdjustment:-40);
            Require(advance==V(11),"Original current-family Tail primary-hand/rate differential");count++;
        }
        Require(count==4608,"Incomplete rate vectors");
    }
    private static BaseCreature Target(PlayerMobile owner)
    {
        var target=new CobraPassiveTarget{Name="DragonTailPreparationTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
        target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject?.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;return target;
    }
    private static void Check()
    {
        Differential();var owner=BladeBodyChecks.Fresh(6660,10,3500);owner.Name="DragonTailPreparationFixture";
        var weapon=new Dagger();ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(173));Require(owner.EquipItem(weapon),"Actual dagger");ClassGear.Refresh(owner);
        var target=Target(owner);int mana=NecromancerEffects.ManaBalance256(owner),life=SkillHealth.Balance256(target),calls=0;
        int Miss(){calls++;return 1;}
        int flags=DragonTailPreparation.ResultFlags(owner,target,()=>true,()=>Miss());
        Require((flags&1)==0&&calls==1,"Uncharged Tail must roll ordinary accuracy");
        var charge=new MartialCharge(owner.Serial.Value,CombatAbility.TigerStrike,3,1,150,TimedEffects.Now.AddHours(1));Hold(charge);
        flags=DragonTailPreparation.ResultFlags(owner,target,()=>true,()=>throw new InvalidOperationException("Charged accuracy rolled"));
        Require((flags&SourceMeleeResult.Successful)!=0,"Charged Tail failed unblocked accuracy");
        Require(DragonTailPreparation.ResultFlags(owner,target,()=>false,()=>throw new InvalidOperationException("Out-of-range hit roll"))==0,"Guaranteed accuracy bypassed range");
        var blocker=new GraveyardSkeleton{Name="DragonTailPreparationTarget",Frozen=true,CantWalk=true,NoKillAwards=true};
        blocker.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);blocker.AIObject?.AITimer.Stop();blocker.SetHits(50000);blocker.Hits=blocker.HitsMax;
        var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
        try
        {
            SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;
            Require(SourceBlocking.TryRate(blocker,out int rate,out _)&&rate>0,"Mapped skeleton shield");
            flags=DragonTailPreparation.ResultFlags(owner,blocker,()=>true,()=>throw new InvalidOperationException("Charged shield accuracy rolled"));
            Require((flags&SourceMeleeResult.Block)!=0&&(flags&SourceMeleeResult.Successful)==0,"Charged Tail bypassed mapped shield");
            SourceBlocking.BlockRoll=_=>99;
            Require((DragonTailPreparation.ResultFlags(owner,blocker,()=>true,()=>1)&1)!=0,"Unblocked charged Tail failed");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
        var ordinary=CombatTiming.For(owner,CombatAbility.BasicAttack);var tail=DragonTailPreparation.Timing(owner);
        Require(tail==DragonTailPreparation.Timing(CombatTiming.FrameAdvance(CombatTiming.Weapon(weapon),CombatTiming.Ias(owner),GearEffects.AttackSlowTotal(owner),CombatTiming.SkillAttackRate(owner),-40)),"Actual primary gear clock");
        Require(CombatTiming.For(owner,CombatAbility.BasicAttack)==ordinary&&AssassinCharges.State(owner)==charge,"Prepared clock/hit altered ordinary timing or consumed charges");
        Require(NecromancerEffects.ManaBalance256(owner)==mana&&SkillHealth.Balance256(target)==life,"Preparing a hit paid or damaged before contact");
        Require(AssassinCombat.Implemented(CombatAbility.DragonTail),"Learned Tail no longer reuses the prepared suppliers");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,charge,mana,life,ordinary,tail)));
        owner.MoveToWorld(owner.Location,Map.Internal);target.MoveToWorld(target.Location,Map.Internal);
        Log.Information("Dragon Tail preparation: 4480 complete KK clocks,4608 original rate vectors,charged/uncharged/mapped-shield/range controls; no debit/damage/consumption in supplier-only calls");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(owner!=null&&target!=null&&AssassinCharges.State(owner)==saved.Charge,"Original prepared charge did not survive actual reload");
        Require(CombatTiming.For(owner,CombatAbility.BasicAttack)==saved.Ordinary&&DragonTailPreparation.Timing(owner)==saved.Tail,"Prepared -40 leaked or changed after reload");
        // Offline timers may naturally age; preparation creates no persistence
        // list or pending action and cannot reinterpret an existing charge.
        Require(ClassCombat.For(owner).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(owner)>=saved.Mana256&&SkillHealth.Balance256(target)==saved.Life256,"Prepared action leaked a pending cast,spent mana or damaged target");
    }
}
