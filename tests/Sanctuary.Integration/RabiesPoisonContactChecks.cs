// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class RabiesPoisonContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RabiesPoisonContactChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class NoEffects:Random{public override int Next(int maximum)=>maximum-1;}
    public static void Configure()
    {
        foreach(string name in new[]{"rabies-poison-contact-check","rabies-poison-contact-reload-check"})
        {var label=name;ConsoleInputHandler.RegisterCommand(label,"Separate forced Rabies poison contact through real melee events",_=>Core.LoopContext.Post(()=>
            {try{Check();Log.Information("{Label}: passed",label);}catch(Exception e){Log.Error(e,"{Label}: failed",label);}}));}
    }
    private static void Clear(PlayerMobile p)
    {var s=DruidRabiesInfections.Snapshot();DruidRabiesInfections.Restore(s with{Infections=s.Infections.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Contagions=s.Contagions.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Markers=s.Markers.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Seeds=s.Seeds.Where(x=>x.Caster!=p.Serial.Value).ToArray()});}
    private static void Check()
    {
        var hit=DruidRabies.HitRoll;var poison=DruidRabies.PoisonRoll;var effects=DruidRabies.Effects;var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;var observed=PlayerItemProcWorld.Observed;
        var p=DruidBasicProcChecks.Owner(CombatAbility.Werewolf,2800,out var item);ClassSkillChecks.Train(p,"druid.rabies");var visits=new List<PlayerProcWorldVisit>();
        try
        {
            using var candidate=PlayerItemProcWorld.Candidate(p);DruidRabies.Effects=new NoEffects();DruidRabies.PoisonRoll=_=>0;
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            foreach(string scenario in new[]{"hit","miss","block","immune","stronger","repeat","lethal","cancel"})
            {
                DruidRabies.HitRoll=()=>0;SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=n=>n-1;ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
                NecromancerEffects.SetMana256(p,p.ManaMax*256);SkillHealth.SetBalance256(p,p.HitsMax*128);int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
                var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}t.Frozen=true;
                try
                {
                    if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}
                    if(scenario=="stronger"){PoisonJavelins.Poison(p,t,1000,250,TimedEffects.Now);}
                    if(scenario=="repeat"){DruidRabiesInfections.Mark(p,t,1,100,TimedEffects.Now);}
                    var marker=DruidRabiesInfections.Infection(t);int packets=NativeMonsterReturn.Session(t).Packets;visits.Clear();
                    Require(ClassCombat.Perform(p,CombatAbility.Rabies,t,t.Location,Core.TickCount),"Forced poison fixture start");long at=ClassCombat.For(p).EffectAt;
                    Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-2560,"Rabies start emitted contact or wrong mana");
                    if(scenario=="cancel"){ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==0&&NativeMonsterReturn.Session(t).Packets==packets&&DruidRabiesInfections.Infection(t)==null,"Cancelled Rabies committed poison contact");continue;}
                    if(scenario is "miss" or "lethal"){DruidRabies.HitRoll=()=>1;}
                    if(scenario=="block"){SourceBlocking.BlockRoll=_=>0;}
                    if(scenario=="lethal"){SkillHealth.SetBalance256(t,256);}
                    int before=SkillHealth.Balance256(t);Require(ClassCombat.ResolveAction(p,at),"Forced poison release");
                    bool ordinary=scenario is not("miss" or "block" or "lethal");int rate=scenario=="immune"?0:48;
                    Require(visits.Count==(ordinary?4:3)&&NativeMonsterReturn.Session(t).Packets==packets+2,"Rabies did not drain two independent contacts");
                    var second=visits[^2];Require(second.Phase=="domeleedamage"&&second.Packet.ResultFlags==1&&second.Packet.HitFlags==32&&second.Packet.Total256==rate&&second.Packet.Poison256==rate
                        &&second.Packet.Physical256==0&&second.Packet.Magic256==0&&second.Packet.Fire256==0&&second.Packet.Lightning256==0&&second.Packet.Cold256==0&&second.Packet.ColdFrames==0&&second.Packet.StunFrames==0,"Forced packet repeated weapon stats,hit recovery or omitted initial poison");
                    Require(visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==32&&NecromancerEffects.ManaBalance256(p)==mana-2560&&SkillHealth.Balance256(p)==life,"Second contact lost attack event,repaid mana or leeched poison");
                    if(scenario=="lethal"){Require(!t.Alive&&SkillHealth.Balance256(t)==0,"Initial poison rate failed to kill at fractional HP boundary");}
                    else
                    {
                        Require(before-SkillHealth.Balance256(t)==visits.Where(v=>v.Phase=="domeleedamage").Sum(v=>v.Packet.Total256),"Initial poison rate missing or damage duplicated");
                        var state=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);
                        Require(scenario=="immune"?state==null:state?.PoisonClock?.Rate256==(scenario=="stronger"?1000:48),"Strongest poison clock or immunity changed");
                        if(marker!=null){Require(DruidRabiesInfections.Infection(t)==marker,"Repeat contact refreshed independent infection marker");}
                    }
                    int count=visits.Count;Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==count,"Forced poison contact replayed");
                }
                finally{ClassCombat.Stop(p);Clear(p);SorceressSpellMissiles.Cancel(p);t.Corpse?.Delete();t.Delete();}
            }
        }
        finally{DruidRabies.HitRoll=hit;DruidRabies.PoisonRoll=poison;DruidRabies.Effects=effects;SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;PlayerItemProcWorld.Observed=observed;Clear(p);DruidPlayerForms.Clear(p);p.Delete();}
    }
}
