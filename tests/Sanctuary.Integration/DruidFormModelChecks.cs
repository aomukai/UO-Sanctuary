// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;

public static class DruidFormModelChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormModelChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("druid-form-model-check","Pure player forms; world/casting not enabled",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("druid-form-model-check: passed");}catch(Exception e){Log.Error(e,"druid-form-model-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(CombatAbility ability,int rank,int lycan)
    {
        var definition=SkillMechanics.For(ability);
        return new(definition,key=>key==definition.SourceKey?rank:key=="Shape Shifting"?lycan:0,_=>0,99);
    }
    private static void Reject(Action action,string why)
    {bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Require(rejected,why);}
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.Werewolf)&&!DruidCombat.Implemented(CombatAbility.Werebear)
            &&!DruidCombat.Implemented(CombatAbility.Lycanthropy),"Pure forms must not claim a learned handler");
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-forms-source-probe.json")));
        int compared=0,entries=0,unshifts=0,failures=0,expiry=0;
        foreach(var row in fixture.RootElement.EnumerateArray())
        {
            int N(int index)=>row[index].GetInt32();
            // The production helper supports valid player rows and active
            // forms. Monster suppliers, corrupt table rows and orphaned
            // inactive source lists are not represented as valid live states.
            int scene=N(4);if(N(3)!=0||scene is 3 or 4 or 6){continue;}
            var ability=N(0)==0?CombatAbility.Werewolf:CombatAbility.Werebear;
            var values=Values(ability,N(1),N(2));
            DruidFormState current=null;
            if(scene is 1 or 2 or 7)
            {
                var prior=scene==2?(ability==CombatAbility.Werewolf?CombatAbility.Werebear:CombatAbility.Werewolf):ability;
                current=DruidFormModel.Transform(null,Values(prior,N(1),N(2)),0).State;
            }
            bool canStart=scene!=9&&DruidFormModel.CanStart(current,values,0);
            Require(canStart==(N(5)!=0),"Source zero-mana shifted guard mismatch");
            int allocations=0;
            var actual=DruidFormModel.Transform(current,values,100,scene!=8,until=>
                {allocations++;Require(until==100+DruidFormModel.Capture(values).DurationFrames,"Allocation expiry");return scene!=5;});
            Require(actual.NeedsMana==(N(6)!=0),"do116 result/new-form payment boundary");
            Require((actual.SkillMode??7)==N(10),"Source player skill mode mismatch");
            if(actual.State is{} state)
            {
                entries++;DruidFormModel.Validate(state);
                Require(allocations==1&&state.UntilFrame==N(11),"Source entry allocation/lifetime");
                var m=state.Modifiers;var stats=row[12];int S(int i)=>stats[i].GetInt32();
                Require(ability==CombatAbility.Werewolf
                    ?m.StateId==139&&m.Model==430&&m.StaminaPercent==S(0)&&m.AttackRate==S(1)
                        &&m.OtherAnimationRate==S(6)&&m.AttackPercent==S(2)&&m.LifePercent==S(3)&&m.ArmorPercent==0&&m.DamagePercent==0&&m.Concentration==0
                    :m.StateId==140&&m.Model==431&&m.DamagePercent==S(0)&&m.ArmorPercent==S(4)&&m.LifePercent==S(2)
                        &&m.Concentration==S(3)&&m.StaminaPercent==0&&m.AttackRate==0&&m.OtherAnimationRate==0&&m.AttackPercent==0,
                    "Complete source form stat channels");
                Require(state.Rank==S(8)&&S(7)==SkillMechanics.For(ability).SourceId,"Captured modifier skill/rank");
                var copied=JsonSerializer.Deserialize<DruidFormState>(JsonSerializer.Serialize(state));
                DruidFormModel.Validate(copied);Require(copied==state,"Pure captured state JSON roundtrip");
                Require(DruidFormModel.Expire(state,state.UntilFrame-1).State==state,"Early expiry removed form");
                var ended=DruidFormModel.Expire(state,state.UntilFrame,scene!=8);
                Require(ended.Outcome==DruidFormOutcome.Expired&&ended.State==null&&!ended.NeedsMana
                    &&ended.SkillMode==(scene==8?(int?)null:7)&&ended.DelayFrames==0,"Actual removal callback contract");expiry++;
            }
            else if(actual.Outcome==DruidFormOutcome.Unshifted)
            {
                Require(allocations==0&&actual.DelayFrames==25&&row[13].EnumerateArray().Any(e=>e[0].GetInt32()==7&&e[1].GetInt32()==125),"Group removal/delay before allocation");unshifts++;
            }
            else{Require(actual.Outcome==DruidFormOutcome.Refused&&scene==5&&allocations==1&&actual.DelayFrames==0,"Failed allocation boundary");failures++;}
            compared++;
        }
        Require(compared==350&&entries==150&&unshifts==150&&failures==50&&expiry==150,"Incomplete supported player source contexts");
        var one=Values(CombatAbility.Werewolf,1,0);var plain=DruidFormModel.Transform(null,one,0).State;
        Require(plain.Modifiers.DurationFrames==1000&&plain.Modifiers.LifePercent==25&&plain.Modifiers.StaminaPercent==25,"Absent Lycanthropy donated a baseline");
        var trained=DruidFormModel.Transform(null,Values(CombatAbility.Werebear,1,1),0).State;
        Require(trained.Modifiers.DurationFrames==2000&&trained.Modifiers.LifePercent==95&&trained.Modifiers.DamagePercent==55&&trained.Modifiers.ArmorPercent==40&&trained.Modifiers.Concentration==100,"Rank-one bear/Lycanthropy channels");
        Require(!DruidFormModel.CanStart(null,one,15*256-1)&&DruidFormModel.CanStart(null,one,15*256)
            &&DruidFormModel.CanStart(plain,one,0),"Fractional entering mana versus free unshift");
        Reject(()=>DruidFormModel.Capture(Values(CombatAbility.Werewolf,0,0)),"Rank0form accepted");
        Reject(()=>DruidFormModel.Transform(null,Values(CombatAbility.Werebear,1,256),0),"Unmapped Lycanthropy rank accepted");
        Reject(()=>DruidFormModel.Validate(plain with{UntilFrame=plain.UntilFrame+1}),"Corrupt form lifetime accepted");
        Reject(()=>DruidFormModel.Validate(plain with{Modifiers=plain.Modifiers with{LifePercent=999}}),"Corrupt captured modifiers accepted");
        Reject(()=>DruidFormModel.Expire(plain,-1),"Negative frame accepted");
        Log.Information("Druid forms:350native player lifecycle/stat contexts match complete source;150entries/150unshifts/50allocation failures/150expiry+JSON controls. No world,HP,rate,outer payment,casting or learned-handler acceptance claimed");
    }
}
