// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BloodBoilLearnedChecks
{
    private const CombatAbility Ability=CombatAbility.BloodBoil;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodBoilLearnedChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="BloodBoilLearnedFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="BloodBoilLearnedFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(string choice in new[]{"left","right","quick"})
        {
            string mode=choice,label="blood-boil-learned-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Approved normal self cast and automatic original/bound explosions",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static string Birth(BaseCreature b)=>b is SkeletonGuardian original?JsonSerializer.Serialize(original switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()}):JsonSerializer.Serialize(BoundDemonHosts.State(b).Birth);
    private static void Input(NetState network,string mode)
    {
        string label="blood-boil-learned-"+mode;var p=new PlayerMobile{Body=mode=="quick"?0x191:0x190,Female=mode=="quick",Player=true,Name="BloodBoilLearnedFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(string key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key);}
        FixtureSpace.Corridor(p,6900,34,2650);var account=FixtureNetwork.Authenticate(network,p,"blood-boil-learned-");BaseCreature[] bodies=[];GoatmanNpcContactChecks.Recipient target=null;bool done=false;
        void Cleanup(){ClassCombat.Stop(p);target?.Delete();foreach(var b in bodies){if(!b.Deleted){b.Delete();}}p.Delete();account.Delete();}
        try
        {
            Require(!ClassCombat.Available(p,Ability)&&!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Unlearned self cast");
            ClassSkillChecks.Train(p,"warlock.bloodboil",mode=="quick"?1:19);
            Require(BloodBoilPrepared.Approved&&ClassAbilities.SelfTarget(Ability)&&ClassCombat.Available(p,Ability)
                &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned self/bindings");
            var original=WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman);WarlockGoatmanWorld.Admit(original);bodies=[original];
            if(mode!="quick")
            {
                var second=WarlockGoatmanWorld.Create(p,new(p.X+3,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(second);bodies=[original,second];
                if(mode=="right")
                {
                    ClassSkillChecks.Train(p,"warlock.binddemon");var bound=new Daemon{Name="BloodBoilLearnedFixture"};bound.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);bound.AIObject?.AITimer.Stop();bodies=[original,second,bound];
                    Require(BoundDemonHosts.BindPrepared(p,bound,out var why),"Qualified bound recipient: "+why);bound.AIObject?.AITimer.Stop();
                }
                else{var third=WarlockGoatmanWorld.Create(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonDefiler);WarlockGoatmanWorld.Admit(third);bodies=[original,second,third];}
            }
            foreach(var b in bodies){b.Name="BloodBoilLearnedFixture";b.Frozen=b.Paralyzed=true;b.Poison=Poison.Lesser;}
            Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Normal self startup");var cancelled=ClassCombat.For(p).BloodBoil;long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(cancelled.Cancelled&&!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled self cast paid");
            Ready(p);NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Underfunded self cast");Ready(p);NecromancerEffects.SetMana256(p,mana);
            if(mode=="quick"){SkillHealth.SetBalance256(original,SkillHealth.Maximum256(original)/10);}
            var health=bodies.Select(SkillHealth.Balance256).ToArray();var maximum=bodies.Select(SkillHealth.Maximum256).ToArray();var births=bodies.Select(Birth).ToArray();var seeds=bodies.Select(NpcEquipmentRandom.State).ToArray();var order=bodies.Select(BattleCommand.PetOrder).ToArray();
            target=new GoatmanNpcContactChecks.Recipient{Name="BloodBoilLearnedFixture",Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);target.Hits=target.HitsMax;target.Poison=Poison.Lesser;
            target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Fire,0);target.MoveToWorld(new(p.X+3,p.Y+1,p.Z),p.Map);target.AIObject?.AITimer.Stop();int hp=SkillHealth.Balance256(target);
            bool began=false;long deadline=Core.TickCount+8000;BloodBoilCasting.Pending pending=null;BloodBoilNumbers numbers=null;
            void Inspect()
            {
                if(done){return;}
                try
                {
                    Require(Core.TickCount<deadline,"Self input timeout");var s=ClassCombat.For(p);
                    if(!began&&s.PendingAbility==Ability)
                    {
                        Require(NecromancerEffects.ManaBalance256(p)==mana&&bodies.Select(SkillHealth.Balance256).SequenceEqual(health)&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Unpaid self windup/SC");
                        began=true;pending=s.BloodBoil;Require(pending!=null,"Missing learned self action");
                        if(mode!="quick"){PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);}numbers=BloodBoilPrepared.Numbers(SkillMechanics.Values(p,Ability));
                        Log.Information("{Label}: windup",label);
                    }
                    if(pending?.Released==true)
                    {
                        var result=pending.Result;Require(result is{Released:true,Fault:null}&&result.DemonCount==bodies.Length&&result.Bursts.Length==bodies.Length
                            &&result.Bursts.Select(b=>b.Demon).SequenceEqual(bodies.Select(b=>b.Serial.Value))&&NecromancerEffects.ManaBalance256(p)==mana-numbers.Mana256
                            &&result.Bursts.Sum(b=>b.Recipients.Length)==1&&hp-SkillHealth.Balance256(target)==result.Bursts[0].Physical256+result.Bursts[0].Fire256,"Natural self payment/ordered explosion/overlap/damage");
                        for(int i=0;i<bodies.Length;i++)
                        {
                            int cost=(int)((long)maximum[i]*BloodBoilPrepared.CostPercent(numbers,bodies.Length)/100);int remaining=Math.Max(0,health[i]-cost);if(remaining<256){remaining=0;}
                            Require(result.Bursts[i].LifePaid256==health[i]-remaining,"Life payment differs");
                            if(remaining>0){Require(SkillHealth.Balance256(bodies[i])==remaining&&Birth(bodies[i])==births[i]&&NpcEquipmentRandom.State(bodies[i])==seeds[i]&&BattleCommand.PetOrder(bodies[i])==order[i],"Surviving demon birth/HP/RNG/order changed");}
                            else{Require(bodies[i].Deleted||!bodies[i].Alive,"Lethal payment did not retire demon");}
                        }
                        Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-numbers.Mana256,"Self action replayed");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
                }
                catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
            }
            Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)Ability,(byte)DiabloClass.Warlock,p.X,p.Y,p.Z);Inspect();}));
        }
        catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
    }
}
