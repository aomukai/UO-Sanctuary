// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class ConsumeLearnedChecks
{
    private const CombatAbility Ability=CombatAbility.Consume;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ConsumeLearnedChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="ConsumeLearnedFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="ConsumeLearnedFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(string choice in new[]{"right","quick","defiler","menu"})
        {
            string mode=choice,label="consume-learned-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Approved normal Consume input,original retirement and town selection",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static void Input(NetState network,string mode)
    {
        string label="consume-learned-"+mode;
        var p=new PlayerMobile{Body=mode=="menu"?0x191:0x190,Female=mode=="menu",Player=true,Name="ConsumeLearnedFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(string key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key);}
        FixtureSpace.Corridor(p,6900,34,2550);var account=FixtureNetwork.Authenticate(network,p,"consume-learned-");BaseCreature body=null;GuardedRegion town=null;bool done=false;ConsumeRosterGump selectedMenu=null;int button=0;
        void Cleanup(){ClassCombat.Stop(p);ConsumeEffects.Clear(p);town?.Unregister();if(body is{Deleted:false}){body.Delete();}p.Delete();account.Delete();}
        try
        {
            Require(!ClassCombat.Available(p,Ability)&&!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Unlearned Consume opened roster");ClassSkillChecks.Train(p,"warlock.consume",19);
            Require(ConsumeEffects.Approved&&ClassCombat.Available(p,Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)
                &&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned right/quick metadata");
            if(mode=="menu")
            {
                ClassSkillChecks.Train(p,"warlock.binddemon");var native=new Daemon{Name="ConsumeLearnedFixture"};body=native;native.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);native.AIObject?.AITimer.Stop();
                Require(BoundDemonHosts.BindPrepared(p,native,out var why),"Qualified Consume bind: "+why);native.AIObject?.AITimer.Stop();
            }
            else
            {
                var species=mode=="right"?CombatAbility.SummonGoatman:mode=="quick"?CombatAbility.SummonTainted:CombatAbility.SummonDefiler;
                var original=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,species);body=original;WarlockGoatmanWorld.Admit(original);
            }
            body.Name="ConsumeLearnedFixture";body.Poison=Poison.Lesser;body.Frozen=true;
            Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,Ability,body,body.Location,Core.TickCount),"Normal selected startup");var cancelled=ClassCombat.For(p).Consume;long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(cancelled.Cancelled&&!ClassCombat.ResolveAction(p,effect)&&!body.Deleted&&ConsumeEffects.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled input consumed/paid");
            Ready(p);NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);Require(!ClassCombat.Perform(p,Ability,body,body.Location,Core.TickCount)&&!body.Deleted,"Underfunded Consume retired a demon");Ready(p);NecromancerEffects.SetMana256(p,mana);
            long order=BattleCommand.PetOrder(body);uint originalId=body.Serial.Value;SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/3-13);int wound=SkillHealth.Balance256(body);
            if(mode=="menu")
            {
                town=new GuardedRegion("ConsumeLearnedTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(p.X+18,p.Y-2,6,5));town.Register();p.Location=new(p.X+20,p.Y,p.Z);Ready(p);
                Require(MinionSafeAreas.Contains(p)&&body.Map==p.Map&&!MinionSafeAreas.Parked(body)&&SkillHealth.Balance256(body)==wound,"Town did not retain same injured body");
            }
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/3-17);int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p);bool began=false;long deadline=Core.TickCount+8000;ConsumeCasting.Pending pending=null;ConsumeSuppliers capture=null;
            void Inspect()
            {
                if(done){return;}
                try
                {
                    Require(Core.TickCount<deadline,"Consume input timeout");var s=ClassCombat.For(p);
                    if(mode=="menu"&&selectedMenu==null&&p.FindGump<ConsumeRosterGump>()is{} menu)
                    {
                        Require(s.Consume==null&&!body.Deleted&&NecromancerEffects.ManaBalance256(p)==mana,"Opening roster consumed resources");selectedMenu=menu;button=Array.IndexOf(ConsumeCasting.Roster(p),body)+1;
                        Require(button>0,"Actual sent town menu omitted bound demon");menu.OnResponse(network,new RelayInfo(button,[],[],[],[]));
                    }
                    if(!began&&s.PendingAbility==Ability)
                    {
                        Require(s.PendingTarget==body.Serial&&s.Consume is{Released:false}&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))
                            &&!body.Deleted&&ConsumeEffects.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Selected identity/unpaid windup");
                        began=true;pending=s.Consume;PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);capture=ConsumePrepared.Capture(p,ConsumeCasting.Monster(body),SummonDifficulties.For(p.Map));
                        Require(capture.Rank==20,"Release effective rank did not include original gear");Log.Information("{Label}: windup",label);
                    }
                    if(pending?.Released==true)
                    {
                        var state=ConsumeEffects.State(p);
                        Require(pending.Result is{Consumed:true,Fault:null}&&body.Deleted&&state==pending.Result.Effect&&state.Suppliers==capture
                            &&state.OriginalPet==originalId&&state.Order==order&&NecromancerEffects.ManaBalance256(p)==mana-capture.Mana256
                            &&Math.Abs(SkillHealth.Balance256(p)-(long)life*SkillHealth.Maximum256(p)/maximum)<=1
                            &&WarlockDemonRecipients.Roster(p).Length==0&&!ClassCombat.ResolveAction(p,s.EffectAt),"Natural retirement/slot/capture/payment/relative-life mismatch");
                        if(selectedMenu!=null){selectedMenu.OnResponse(network,new RelayInfo(button,[],[],[],[]));Require(s.Consume==null&&ConsumeEffects.State(p)==state,"Stale roster revived consumed original");}
                        done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
                }
                catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
            {
                if(mode=="menu"){Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Warlock,p.X,p.Y,p.Z);}
                else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Warlock,body.Serial.Value,body.X,body.Y,body.Z);}Inspect();
            }));
        }
        catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
    }
}
