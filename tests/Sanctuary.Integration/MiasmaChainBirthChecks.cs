// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server.Items;
namespace Sanctuary.Integration;

// Invoked by the complete Chain-world fixture;no old compiler matrix is rerun.
public static class MiasmaChainBirthChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaChainBirthChecks));
    public static void Configure()=>Server.ConsoleInputHandler.RegisterCommand("miasma-chain-birth-check","Chain-specific captured numbers and duration callback",_=>Server.Core.LoopContext.Post(()=>
    {try{Check();Log.Information("miasma-chain-birth-check: passed");}catch(Exception e){Log.Error(e,"miasma-chain-birth-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank,int bolt=0,int abyss=0,int entropy=0,int hardEntropy=0)=>new(SkillMechanics.For(CombatAbility.MiasmaChain),
        k=>k=="Miasma Chains"?rank:k=="Enhanced Entropy"?entropy:0,k=>k=="Miasma Bolt"?bolt:k=="Abyss"?abyss:k=="Enhanced Entropy"?hardEntropy:0,99);
    internal static void Check()
    {
        var one=MiasmaChainBirths.Numbers(Values(1));var twenty=MiasmaChainBirths.Numbers(Values(20));
        Require(one is{DirectMinimum256:1536,DirectMaximum256:2304,CloudMinimum256:1280,CloudMaximum256:2560,Mana256:1152,LinkFrames:125,MaximumChains:5,LinkLength:40}
            &&one.Shape is{LifetimeFrames:50,Radius:2,ServerCalculation:2},"Rank1 Chain pinned golden");
        Require(twenty is{DirectMinimum256:14336,DirectMaximum256:15104,CloudMinimum256:36096,CloudMaximum256:37376,Mana256:3584,LinkFrames:125,MaximumChains:10},"Rank20 Chain pinned golden");
        Require(MiasmaChainBirths.Numbers(Values(3)).MaximumChains==5&&MiasmaChainBirths.Numbers(Values(4)).MaximumChains==6
            &&MiasmaChainBirths.Numbers(Values(40)).MaximumChains==10,"Chain cap boundaries");
        var soft=MiasmaChainBirths.Numbers(Values(1,entropy:20));var hard=MiasmaChainBirths.Numbers(Values(1,entropy:20,hardEntropy:20));
        Require(soft.Shape is{LifetimeFrames:150,Radius:4,ServerCalculation:2}&&hard.Shape is{LifetimeFrames:150,Radius:4,ServerCalculation:4}
            &&soft.CloudMinimum256==one.CloudMinimum256&&hard.CloudMaximum256==one.CloudMaximum256,"Entropy effective shape/hard layer separation");
        var venom=new SkillValues(SkillMechanics.For(CombatAbility.Venom),_=>1,_=>0,99);
        Require(venom.Reference("skill","Venom","len")==3000&&venom.DurationFrames==10,"Aura-duration callback54 conflated with poison length");
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.miasmachain");ClassSkillChecks.Train(p,"warlock.enhancedentropy",10);
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Chain +skill helm");ClassGear.Refresh(p);
        try
        {
            var saved=MiasmaChainBirths.Capture(p);p.AddToBackpack(item);ClassGear.Refresh(p);var current=MiasmaChainBirths.Capture(p);
            Require(saved.Numbers.Rank==current.Numbers.Rank+1&&saved.Numbers.Shape.EffectiveRank==current.Numbers.Shape.EffectiveRank+1
                &&saved.Numbers.Shape.HardRank==current.Numbers.Shape.HardRank&&saved.HardBolt==current.HardBolt&&saved.HardAbyss==current.HardAbyss,"Actual source/effective/hard capture");MiasmaChainBirths.Validate(saved);
        }
        finally{item.Delete();p.Delete();}
        Require(WarlockCombat.Implemented(CombatAbility.MiasmaChain),"Approved Chain admission missing");
    }
}
