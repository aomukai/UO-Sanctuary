// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class GrimWardCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimWardCastChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool quick in new[]{false,true})
        {
            bool captured=quick;string label=quick?"grim-ward-quick":"grim-ward-right";
            ConsoleInputHandler.RegisterCommand(label+"-check","Learned Ward paid input and natural startup",_=>Core.LoopContext.Post(()=>
            {
                try{Log.Information("{Name}: passed",label+"-check");FixtureNetwork.Connected(label+"-result",n=>Input(n,captured));}
                catch(Exception e){Log.Error(e,"{Name}: failed",label+"-check");}
            }));
        }
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"grim-ward-quick":"grim-ward-right";
        var p=DoubleSwingChecks.Owner(3,ability:CombatAbility.GrimWard);if(quick){PoisonDaggerChecks.Named(p,"Tarnhelm");}
        var account=FixtureNetwork.Authenticate(network,p,"WardCast");var corpse=FindPotionChecks.Body(p);var loot=corpse.Items.ToArray();
        DoubleSwingChecks.Ready(p);NecromancerEffects.SetMana256(p,50*256+17);int mana=NecromancerEffects.ManaBalance256(p),rank=SkillMechanics.Rank(p,CombatAbility.GrimWard);
        bool began=false,finished=false;long deadline=Core.TickCount+10000;
        void Cleanup(){ClassCombat.Stop(p);GrimWardActors.Clear(p);corpse.Delete();p.Delete();account.Delete();}
        try
        {
            Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.GrimWard)&&SkillBindings.Instance.Assign(p,false,CombatAbility.GrimWard)
                &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.GrimWard),"Pinned right-only and quick Ward bindings");
            void Inspect()
            {
                if(finished){return;}
                try
                {
                    Require(Core.TickCount<deadline,"Ward input timeout");var action=ClassCombat.For(p);
                    if(action.PendingAbility==CombatAbility.GrimWard&&!began)
                    {
                        Require(action.GrimWardTarget==corpse.Serial&&NecromancerEffects.ManaBalance256(p)==mana-1024
                            &&!CorpseSkillRules.NoSelect(corpse)&&action.Timing==BarbarianCastClock.Calculate(0),"Actual start33 token/payment/SC timing");
                        began=true;Log.Information("{Label}: windup",label);
                    }
                    var ward=GrimWardActors.Snapshot().SingleOrDefault(x=>x.Owner==p.Serial.Value);
                    if(ward is{Phase:WardPhase.Body})
                    {
                        Require(began&&action.GrimWardTarget==Serial.Zero&&ward.Rank==rank&&ward.FindPotion==1&&ward.LastPulse==0
                            &&NecromancerEffects.ManaBalance256(p)==mana-1024&&CorpseSkillRules.NoSelect(corpse)
                            &&loot.All(x=>!x.Deleted&&x.Parent==corpse),"Natural child/startup,release snapshot,duplicate payment or loot");
                        finished=true;Cleanup();Log.Information("{Result}: passed",label+"-result");return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
                }
                catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Result}: failed",label+"-result");}
            }
            Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)CombatAbility.GrimWard,(byte)DiabloClass.Barbarian,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);
            FixtureClock.NextLoop(label+"-result",Inspect);
        }
        catch(Exception e){Cleanup();Log.Error(e,"{Result}: failed",label+"-result");}
    }
}
