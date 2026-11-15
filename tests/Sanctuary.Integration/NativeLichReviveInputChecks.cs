// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class NativeLichReviveInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NativeLichReviveInputChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(string choice in new[]{"normal-right","normal-quick","nightmare-right","hell-quick"})
        {
            string mode=choice,label="native-lich-revive-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Approved native Lich learned corpse-target input",_=>Core.LoopContext.Post(()=>
            {FixtureNetwork.Connected(label+"-result",network=>Input(network,mode));Log.Information("{Label}: passed",label+"-check");}));
        }
    }
    private static void Input(NetState network,string mode)
    {
        string label="native-lich-revive-"+mode;var facets=SummonDifficulties.Facets();
        var difficulty=mode.StartsWith("nightmare-",StringComparison.Ordinal)?MonsterDifficulty.Nightmare:mode.StartsWith("hell-",StringComparison.Ordinal)?MonsterDifficulty.Hell:MonsterDifficulty.Normal;
        var p=NativeCorpseChecks.Fresh();SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);
        var corpse=NativeCorpseChecks.Corpse(p,difficulty:difficulty);var original=ReviveOriginals.Find(corpse)as GraveyardLich;uint serial=original?.Serial.Value??0;
        Require(original is{Alive:false,Deleted:false}&&NativeReviveRules.Approved,"Approved native input original/gate");
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Revive)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Revive),"Native Revive input bindings");
        var account=FixtureNetwork.Authenticate(network,p,"native-lich-learned-");ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Revive).Mana256;
        long deadline=Core.TickCount+12000;bool began=false,done=false;
        void Cleanup(){corpse.Delete();NativeCorpseChecks.Clean(p);account.Delete();SummonDifficulties.SetFacets(facets);}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,$"Native Revive input timeout:{mode},began={began},pending={s.PendingAbility},alive={original.Alive},mana={NecromancerEffects.ManaBalance256(p)}/{mana-cost}");
                if(!began&&s.PendingAbility==CombatAbility.Revive)
                {
                    Require(!original.Alive&&!CorpseSkillRules.NoSelect(corpse)&&NecromancerEffects.ManaBalance256(p)==mana,"Native Revive input paid/activated before release");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(began&&original.Alive)
                {
                    Require(original.Serial.Value==serial&&original.ControlMaster==p&&original.Map==p.Map&&!original.Summoned&&original.ControlSlots==0
                        &&MonsterVariants.Difficulty(original)==difficulty&&ReviveNativeActivation.State(original)is{} state&&state.Setup.LifePlan.Source=="sanctuary.native.graveyard-lich.v1"
                        &&state.Setup.RegenerationRate==0&&PersistentMinions.Profiles.Find(original).ExpiresUtc==null&&NecromancerEffects.ManaBalance256(p)==mana-cost
                        &&CorpseSkillRules.Profiles.Find(corpse)is{UnitReactivated:true,NoSelect:true}&&corpse.SanctuaryConsumed&&corpse.Items.OfType<Gold>().Single().Amount==17
                        &&ClassCombat.Friendly(original,p)&&!original.CanBeHarmful(p,false),"Native learned release lost identity,ownership,life context,payment or loot");
                    done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Revive,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();}));
    }
}
