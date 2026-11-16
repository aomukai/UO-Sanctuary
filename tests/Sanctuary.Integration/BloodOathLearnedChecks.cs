// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BloodOathLearnedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodOathLearnedChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="BloodOathLearnedFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="BloodOathLearnedFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("blood-oath-learned-check","Automatic learned passive with no admission lease",_=>Core.LoopContext.Post(()=>
    {Log.Information("blood-oath-learned-check: passed");FixtureNetwork.Connected("blood-oath-learned-result",Check);}));
    private static void Check(NetState network)
    {
        var p=new PlayerMobile{Body=0x191,Female=true,Player=true,Name="BloodOathLearnedFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.summongoatman");ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        FixtureSpace.Corridor(p,6900,34,2850);var account=FixtureNetwork.Authenticate(network,p,"blood-oath-learned-");
        WarlockGoatmanBody body=null;var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.MoveToWorld(new(p.X+22,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        try
        {
            body=(WarlockGoatmanBody)WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman);WarlockGoatmanWorld.Admit(body);
            string birth=JsonSerializer.Serialize(body.Prepared.Snapshot);int maximum=SkillHealth.Maximum256(body);var seed=NpcEquipmentRandom.State(body);
            int pet=SkillHealth.Balance256(body),owner=SkillHealth.Balance256(p);
            Require(SkillMechanics.Rank(p,CombatAbility.BloodOath)==0&&!BloodOathDamageProposal.ManagedPrepared(p),"Fresh Witch acquired an unlearned passive");
            SkillHealth.Damage(enemy,p,1000);Require(SkillHealth.Balance256(p)==owner-1000&&SkillHealth.Balance256(body)==pet,"Unlearned Oath gave cover");
            ClassSkillChecks.Train(p,"warlock.bloodoath");NecromancerEffects.SetMana256(p,0);owner=SkillHealth.Balance256(p);pet=SkillHealth.Balance256(body);
            Require(BloodOathDamageProposal.ManagedPrepared(p)&&WarlockCombat.Implemented(CombatAbility.BloodOath),"Trained passive failed ordinary admission");
            SkillHealth.Damage(enemy,p,1000);
            Require(SkillHealth.Balance256(p)==owner-970&&SkillHealth.Balance256(body)==pet-30&&NecromancerEffects.ManaBalance256(p)==0,"Rank1 automatic reaction split/payment differs");
            Require(JsonSerializer.Serialize(body.Prepared.Snapshot)==birth&&SkillHealth.Maximum256(body)==maximum&&NpcEquipmentRandom.State(body)==seed,"Learning Oath rewrote original birth/maximum/RNG");
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.BloodOath)&&!SkillBindings.Instance.Assign(p,false,CombatAbility.BloodOath)
                &&!SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BloodOath),"Passive acquired an active binding");
            uint sequence=ClassCombat.For(p).Sequence;owner=SkillHealth.Balance256(p);pet=SkillHealth.Balance256(body);
            SkillHealth.Damage(enemy,p,1000,SkillDamageKind.Periodic);
            Require(SkillHealth.Balance256(p)==owner-1000&&SkillHealth.Balance256(body)==pet&&ClassCombat.For(p).Sequence==sequence,"Periodic damage redirected or passive created an action");
            Log.Information("blood-oath-learned-result: passed");
        }
        catch(Exception e){Log.Error(e,"blood-oath-learned-result: failed");}
        finally
        {
            body?.Delete();enemy.Delete();foreach(var pet in(p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}
            network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();
        }
    }
}
