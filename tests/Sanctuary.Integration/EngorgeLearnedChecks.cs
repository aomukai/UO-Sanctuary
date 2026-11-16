// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class EngorgeLearnedChecks
{
    private const CombatAbility Ability=CombatAbility.Engorge;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EngorgeLearnedChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="EngorgeLearnedFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="EngorgeLearnedFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(string mode in new[]{"left","right","quick","ground"})
        {
            string choice=mode,label="engorge-learned-"+mode;
            ConsoleInputHandler.RegisterCommand(label+"-check","Normal learned Engorge corpse packets,release and protected loot",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,choice));}));
        }
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static void Input(NetState network,string mode)
    {
        string label="engorge-learned-"+mode;
        var p=new PlayerMobile{Body=mode=="ground"?0x191:0x190,Female=mode=="ground",Player=true,Name="EngorgeLearnedFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(string key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key);}
        ClassSkillChecks.Train(p,"warlock.demonicmastery",10);FixtureSpace.Corridor(p,6900,34,2750);
        var account=FixtureNetwork.Authenticate(network,p,"engorge-learned-");BaseCreature body=null;Corpse corpse=null;bool done=false;
        void Cleanup(){ClassCombat.Stop(p);body?.Delete();corpse?.Delete();p.Delete();account.Delete();}
        try
        {
            corpse=DeathSentryExplosionChecks.Body(p,new(p.X+12,p.Y,p.Z));var loot=corpse.Items.Select(i=>i.Serial).ToArray();
            Require(!ClassCombat.Available(p,Ability)&&!ClassCombat.BeginCorpse(p,Ability,corpse.Serial,corpse.Location,Core.TickCount),"Unlearned Engorge started");
            ClassSkillChecks.Train(p,"warlock.engorge",19);
            Require(ClassCombat.Available(p,Ability)&&EngorgeEffects.Approved&&ClassCombat.CorpseCast(Ability)
                &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned bindings/corpse metadata");
            if(mode=="ground")
            {
                ClassSkillChecks.Train(p,"warlock.binddemon");var native=new Daemon{Name="EngorgeLearnedFixture"};body=native;
                native.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);native.AIObject?.AITimer.Stop();
                Require(BoundDemonHosts.BindPrepared(p,native,out var why),"Qualified original bind: "+why);native.AIObject?.AITimer.Stop();
            }
            else
            {
                var species=mode=="left"?CombatAbility.SummonGoatman:mode=="right"?CombatAbility.SummonTainted:CombatAbility.SummonDefiler;
                var original=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,species);body=original;
                WarlockGoatmanWorld.Admit(original);
            }
            body.Name="EngorgeLearnedFixture";
            body.Poison=Poison.Lesser; // Suppress unrelated passive recovery during this short direct-heal measurement.
            Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);
            Require(EngorgeCasting.Acquire(p,new(corpse.X,corpse.Y+2,corpse.Z))==corpse
                &&EngorgeCasting.Acquire(p,new(corpse.X,corpse.Y+3,corpse.Z))==null,"Two-tile corpse targeting boundary");
            Require(ClassCombat.BeginCorpse(p,Ability,corpse.Serial,corpse.Location,Core.TickCount),"Normal corpse dispatcher startup");
            var cancelled=ClassCombat.For(p).Engorge;long release=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(cancelled.Cancelled&&!ClassCombat.ResolveAction(p,release)&&!corpse.SanctuaryConsumed&&NecromancerEffects.ManaBalance256(p)==mana,"Normal cancellation consumed corpse/mana");
            Ready(p);NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
            Require(!ClassCombat.BeginCorpse(p,Ability,corpse.Serial,corpse.Location,Core.TickCount)&&!corpse.SanctuaryConsumed,"Underfunded normal action started");
            Ready(p);NecromancerEffects.SetMana256(p,mana);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/5);
            string birth=body is SkeletonGuardian originalBody?JsonSerializer.Serialize(originalBody switch
            {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()}):JsonSerializer.Serialize(BoundDemonHosts.State(body).Birth);
            var seed=NpcEquipmentRandom.State(body);long order=BattleCommand.PetOrder(body);int maximum=SkillHealth.Maximum256(body);
            int before=SkillHealth.Balance256(body);bool began=false;long deadline=Core.TickCount+8000;EngorgeCasting.Pending pending=null;EngorgeSuppliers capture=null;
            void Inspect()
            {
                if(done){return;}
                try
                {
                    Require(Core.TickCount<deadline,"Learned corpse input timeout");var s=ClassCombat.For(p);
                    if(!began&&s.PendingAbility==Ability)
                    {
                        Require(s.PendingTarget==corpse.Serial&&s.PendingPoint==corpse.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))
                            &&NecromancerEffects.ManaBalance256(p)==mana&&!corpse.SanctuaryConsumed,"Selected corpse/unpaid SC windup");
                        began=true;pending=s.Engorge;Require(pending!=null,"Missing normal Engorge action");
                        // Ordinary item change during windup changes release rank,never pet birth.
                        PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);capture=EngorgePrepared.Capture(p);
                        Require(capture.Rank==20,"Effective release rank did not include original gear");Log.Information("{Label}: windup",label);
                    }
                    if(corpse.SanctuaryConsumed)
                    {
                        var effect=EngorgeEffects.Saved(body);
                        Require(began&&pending.Released&&pending.Result==new EngorgeRelease(true,1,capture.Mana256)
                            &&NecromancerEffects.ManaBalance256(p)==mana-capture.Mana256&&effect.Suppliers==capture
                            &&SkillHealth.Balance256(body)==before+EngorgePrepared.Healing256(capture,maximum,before)
                            &&SkillHealth.Maximum256(body)==maximum&&BattleCommand.PetOrder(body)==order&&NpcEquipmentRandom.State(body)==seed
                            &&!corpse.Deleted&&loot.All(id=>corpse.Items.Any(i=>i.Serial==id)),"Natural release/heal/snapshot/payment/RNG/order/loot mismatch: began="+began+" result="+pending?.Result+" mana="+NecromancerEffects.ManaBalance256(p)+" expectedMana="+(mana-capture.Mana256)+" life="+SkillHealth.Balance256(body)+" expectedLife="+(before+EngorgePrepared.Healing256(capture,maximum,before))+" capture="+(effect?.Suppliers==capture)+" seed="+(NpcEquipmentRandom.State(body)==seed)+" order="+(BattleCommand.PetOrder(body)==order));
                        string after=body is SkeletonGuardian originalAfter?JsonSerializer.Serialize(originalAfter switch
                        {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()}):JsonSerializer.Serialize(BoundDemonHosts.State(body).Birth);
                        Require(after==birth&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-capture.Mana256,"Release changed birth/replayed");
                        done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
                }
                catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
            {
                if(mode=="ground"){Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Warlock,corpse.X,corpse.Y+2,corpse.Z);}
                else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Warlock,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);}Inspect();
            }));
        }
        catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
    }
}
