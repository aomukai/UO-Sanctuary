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

public static class RavenStatsChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RavenStatsChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("raven-stats-check","Raven stats and signed summon blind components",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("raven-stats-check: passed");}catch(Exception e){Log.Error(e,"raven-stats-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int level,int rank,int spirit=0,int dire=0,int grizzly=0)=>new(SkillMechanics.For(CombatAbility.Raven),
        key=>key=="Raven"?rank:255,key=>key=="Summon Spirit Wolf"?spirit:key=="Summon Fenris"?dire:key=="Summon Grizzly"?grizzly:0,level);
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Raven),"Learned Raven route missing");
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"raven-components-source-probe.json")));int damage=0,birth=0,blind=0;
        foreach(var row in data.RootElement.GetProperty("damage").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var actual=DruidRavenStats.Attack(Values(99,N(0),N(1),N(2),N(3)));
            Require(actual.Rank==N(0) && actual.Minimum256==N(5) && actual.Maximum256==N(6) && actual.AttackBonus==N(7),"Complete physical/toht source comparison");damage++;
        }
        foreach(var row in data.RootElement.GetProperty("birth").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();var values=Values(N(0),N(1));var actual=DruidRavenStats.Birth(values,(MonsterDifficulty)N(2),N(3)!=0,_=>0);
            Require(N(4)==1 && actual.Level==N(5) && actual.Defense==N(6) && actual.Attack==N(7) && actual.Rank==N(1)
                && actual.Life256==26*256 && actual.Pierce==0 && actual.Blind==-1,"Explicit source level/lookup/difficulty/birth composition");
            Require(DruidRavenStats.InitialBudget(values)==11+N(1) && DruidRavenStats.Limit(values)==Math.Min(5,N(1)),"Current hit budget/petmax formulas");birth++;
        }
        foreach(var row in data.RootElement.GetProperty("blind").EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();int level=N(2)==0?0:NamedControl.BlindLevel(N(0),N(1),N(2),N(3)==6,N(4));
            Require(level==N(6) && (level>0?1:0)==N(5),"Complete signed event09 chance/rank/ranged comparison");blind++;
        }
        Require(damage==13770 && birth==4590 && blind==12800,"Incomplete Raven component source capture");
        bool refused=false;try{DruidRavenStats.Birth(Values(99,29),MonsterDifficulty.Hell,true,_=>17);}catch(NotSupportedException){refused=true;}
        Require(refused,"Unsupported inherited physical sunder accepted");
        Actual();Log.Information("Raven stats:13770source damage/flatAR,4590explicit-level births,12800signed blind cases and actual NPC blind controls pass; learned casting checked separately");
    }
    private static void Actual()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="RavenComponentFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(owner,99);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"druid.raven",out _),"Train Raven for component capture");
        FixtureSpace.Corridor(owner,7020,12,3300);
        var captured=DruidRavenStats.Capture(owner);Require(captured.Rank==1 && captured.Level==98,"Real owner/rank explicit pet level");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(owner.EquipItem(helm),"Real Tarnhelm equip");ClassGear.Refresh(owner);var changed=DruidRavenStats.Capture(owner);
        Require(changed.Rank==2 && changed.Level==99 && captured.Rank==1 && captured.Level==98,"Real gear and immutable birth capture");
        var source=new SkeletonGuardian{Name="RavenComponentProxy"};source.Initialize(owner,1,0);Require(source.SetControlMaster(owner),"Proxy summon control master");
        source.MoveToWorld(owner.Location,owner.Map);source.AIObject.AITimer.Stop();
        var target=new GraveyardLich{CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        int draws=0;var at=TimedEffects.Now;int life=SkillHealth.Balance256(target);
        Require(NamedControl.ApplySummonBlind(source,target,-1,false,()=>{draws++;return 98;}) && draws==1
            && NamedControl.Blind(target) && !NamedControl.SpecialAllowed(target) && SkillHealth.Balance256(target)==life,"Actual signed NPC blind/control/no health damage");
        var state=NamedControl.State(target);Require(state.BlindUntil>=at.AddSeconds(7) && state.BlindUntil<=TimedEffects.Now.AddSeconds(7.1),"Signed rank1 blind reused duration consumer");
        Require(!NamedControl.ApplySummonBlind(source,target,0,false,()=>{draws++;return 0;}) && draws==1,"Zero stat consumed draw");
        Require(!NamedControl.ApplyBlind(owner,target,-1,false,new Random(1)),"Player positive-only contract changed");
        var protectedPet=new SkeletonGuardian();protectedPet.Initialize(owner,1,0);Require(protectedPet.SetControlMaster(owner),"Protected pet ownership");protectedPet.MoveToWorld(owner.Location,owner.Map);protectedPet.AIObject.AITimer.Stop();
        Require(!NamedControl.ApplySummonBlind(source,protectedPet,-1,false,()=>{draws++;return 0;}) && draws==2,"Source draw before downstream protected-target refusal");
        protectedPet.Delete();target.Delete();source.Delete();owner.Delete();
    }
}
