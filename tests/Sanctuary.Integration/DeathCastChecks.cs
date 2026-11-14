// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathCastChecks));
    private sealed record Saved(uint Owner,uint Marker,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-cast-fixtures.json");
    private static CombatAbility Ability=>CombatAbility.SigilDeath;
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Death cast,resources,original world and input",_=>Core.LoopContext.Post(()=>{try{Log.Information("{Name}: passed",name);run();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true}){bool again=reload;string name=reload?"death-cast-reload-check":"death-cast-check";Register(name,()=>FixtureNetwork.Connected(name+"-result",n=>{try{if(again){Reload(n);}else{Check(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}}));}
        foreach(bool quick in new[]{false,true}){bool q=quick;string name=q?"death-cast-quick":"death-cast-right";Register(name+"-check",()=>FixtureNetwork.Connected(name+"-result",n=>Input(n,q)));}
        Register("death-cast-left-check",()=>FixtureNetwork.Connected("death-cast-left-result",n=>Input(n,false,true)));
    }
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.sigildeath",1);
        FixtureSpace.Corridor(p,6560,20,3500);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static SigilActor[] For(PlayerMobile p)=>SigilActors.Snapshot().Actors.Where(s=>s.Owner==p.Serial.Value).ToArray();
    private static void Check(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"SigilCast");var point=new Point3D(p.X+3,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);var before=SigilActors.Snapshot();
        Require(ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned availability/mouse/quick binding mismatch");
        Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Learned ordinary SC refused");var s=ClassCombat.For(p);long effect=s.EffectAt;
        Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&For(p).Length==0,"SC windup paid or allocated early");
        ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Canceled windup paid or created a marker");
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Paid ordinary start refused");effect=ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana-2560&&For(p).Single()is{Rank:1,Frames:125},"Once-only10mana/marker release failed");
        var first=For(p).Single();var original=JsonSerializer.Serialize(SigilActors.Snapshot());Ready(p);NecromancerEffects.SetMana256(p,9*256);
        Require(!ClassCombat.Perform(p,Ability,null,point,Core.TickCount)&&JsonSerializer.Serialize(SigilActors.Snapshot())==original,"Underfunded start replaced the old marker");
        NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(ClassCombat.Perform(p,Ability,null,new(point.X+2,point.Y,point.Z),Core.TickCount),"Late-mana control start refused");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
        Require(!ClassCombat.ResolveAction(p,effect)&&JsonSerializer.Serialize(SigilActors.Snapshot())==original,"Late mana failure changed old roster");
        NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);var blocked=new Point3D(point.X+2,point.Y,point.Z);
        Require(ClassCombat.Perform(p,Ability,null,blocked,Core.TickCount),"Late-terrain control start refused");effect=ClassCombat.For(p).EffectAt;
        int id=Enumerable.Range(0,0x4000).First(i=>TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].Height>=16);var wall=new Static(id);wall.MoveToWorld(blocked,p.Map);mana=NecromancerEffects.ManaBalance256(p);
        try{Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&JsonSerializer.Serialize(SigilActors.Snapshot())==original,"Failed landing paid or replaced old roster");}finally{wall.Delete();}
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(point,p.Map);Ready(p);
        Require(ClassCombat.Perform(p,Ability,target,target.Location,Core.TickCount),"Targeted-ground SC start refused");effect=ClassCombat.For(p).EffectAt;target.MoveToWorld(new(point.X+1,point.Y,point.Z),p.Map);
        Require(ClassCombat.ResolveAction(p,effect)&&For(p).Single(a=>!a.Retiring).Origin==target.Location,"Release did not resolve current selected target coordinates");
        SigilActors.Tick(TimedEffects.Now);Require(SigilDeathProposal.State(target)!=null&&For(p).Length==1,"Learned marker did not pulse actual recipient or retire original marker");
        SigilDeathProposal.Clear(target);target.Delete();SigilActors.Clear(p);
        var q=Owner();try{var v=SkillMechanics.Values(q,Ability);var a=SigilActors.CreatePrepared(q,v,new(q.X+3,q.Y,q.Z),TimedEffects.Now);q.Kill();SigilActors.Tick(TimedEffects.Now);Require(a!=null&&!For(q).Any()&&!SigilActors.Snapshot().Owners.Any(o=>o.Owner==q.Serial.Value),"Approved owner-death cleanup failed");}finally{q.Corpse?.Delete();SigilActors.Clear(q);q.Delete();}
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Saved original learned marker failed");var saved=For(p).Single();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,saved.Marker)));
        Log.Information("Learned Death:shared Warlock SC,mana10once at successful candidate commit,unpaid cancellation,underfunded/late-mana/late-terrain preservation,live target coordinates,actual recipient pulse,approved death cleanup and bindings pass. Normal5second marker will expire across reload,without replay.");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(p!=null&&saved.Generation<2,"Saved learned owner missing");
        SigilActors.Tick(TimedEffects.Now);Require(World.FindItem((Serial)saved.Marker)==null&&For(p).Length==0&&ClassCombat.For(p).PendingAbility==CombatAbility.None
            &&SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Expired learned marker replayed or bindings lost");
        FixtureNetwork.Authenticate(network,p,"SigilCastReload");File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick,bool left=false)
    {
        var p=Owner();p.Female=quick;
        if(quick)
        {
            while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Witch fixture Strength");}
            var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Witch FCR gear refused");ClassGear.Refresh(p);
        }
        Require(SkillBindings.Instance.Assign(p,left,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Death socket bindings");var account=FixtureNetwork.Authenticate(network,p,"DeathWire");Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        var point=new Point3D(p.X+3,p.Y,p.Z);var target=CleaveActionChecks.Enemy(p,3);CleaveActionTarget near=null;Corpse corpse=null;
        var prior=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,body)=>{prior?.Invoke(actor,body);if(actor==target&&body is Corpse c){c.DropItem(new Gold(1));corpse=c;}};
        var values=SkillMechanics.Values(p,Ability);int mana=NecromancerEffects.ManaBalance256(p),nearLife=0;string label=quick?"death-cast-quick":left?"death-cast-left":"death-cast-right";
        long deadline=Core.TickCount+8000;bool began=false,killed=false,finished=false;
        void Cleanup()
        {
            ClassLootHooks.CreatureDied=prior;ClassCombat.Stop(p);SigilDeathProposal.Clear(target);if(near!=null){SigilDeathProposal.Clear(near);near.Delete();}
            corpse?.Delete();if(!target.Deleted){target.Delete();}SigilActors.Clear(p);p.Delete();account.Delete();
        }
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException($"Death input/explosion timeout began={began} killed={killed} actors={For(p).Length} pending={ClassCombat.For(p).PendingAbility} alive={target.Alive} mark={SigilDeathProposal.State(target)} corpse={corpse?.Serial} consumed={corpse?.SanctuaryConsumed} hooks={string.Join(",",prior.GetInvocationList().Select(d=>d.Method.DeclaringType.Name))}");}var session=ClassCombat.For(p);
                if(session.PendingAbility==Ability&&!began){Require(session.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&For(p).Length==0,"Death wire windup paid/allocated early");began=true;Log.Information("{Label}: windup",label);}
                var actor=For(p).SingleOrDefault();
                if(!killed&&actor!=null&&SigilDeathProposal.State(target)is{} mark)
                {
                    Require(began&&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256&&mark.Creator==p.Serial.Value&&mark.Rank==values.Rank&&mark.Modifiers==actor.DeathModifiers,"Death wire release/snapshot/once mana");
                    near=CleaveActionChecks.Enemy(p,4);nearLife=SkillHealth.Balance256(near);killed=true;target.LastKiller=p;target.Kill();
                }
                if(killed&&corpse is{Deleted:false,SanctuaryConsumed:true})
                {
                    Require(SkillHealth.Balance256(near)<nearLife&&corpse.Items.OfType<Gold>().Single().Amount==1&&SigilDeathProposal.State(target)==null&&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256,"Death wire queued burst/loot/claim/one debit");
                    finished=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,point.X,point.Y,point.Z);Inspect();}));
    }
}
