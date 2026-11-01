// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class StormPulseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(StormPulseChecks));
    private sealed record Saved(uint Owner,uint Target,uint Orb,int Map,Point3D Point,int Mana,int Life,ThunderStormState Storm,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"storm-pulse-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="StormPulseSaved"&&prior?.Invoke(p)!=false;
        Register("storm-pulse-check",Check);Register("storm-pulse-reload-check",()=>FixtureNetwork.Connected("storm-pulse-reload-result",Reload));
        foreach(bool quick in new[]{false,true}){bool q=quick;string label=q?"storm-pulse-quick":"storm-pulse-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q)));}
        ConsoleInputHandler.RegisterCommand("storm-pulse-offline","Actual Storm disconnect/no burst/expiry",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("storm-pulse-offline-result",Offline)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Thunder Storm source/live suppliers/input/saved scheduling",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.ChargedBolt,5980);
        foreach(string skill in new[]{"staticfield","nova","lightning","chainlightning","thunderstorm"}){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress."+skill,out _),"Storm prerequisite "+skill);}
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ThunderStorm)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ThunderStorm),"Storm right/quick bindings");return p;
    }
    private static ThunderStormState State(PlayerMobile p)=>ThunderStorm.Active(p);
    private static void Set(PlayerMobile p,ThunderStormState state)
    {SorceressEffects.Profiles.Store(p,(SorceressEffects.Profiles.Find(p)??new()) with{Storm=state});ThunderStorm.Schedule(p.Serial,state);}
    private static void Clean(PlayerMobile p,params Mobile[] targets){SorceressEffects.Profiles.Clear(p);FireBoltChecks.Clean(p,targets);}
    private static int Pulse(PlayerMobile p,Mobile target,bool actions=true)
    {
        var at=TimedEffects.Now;Set(p,State(p) with{NextStrikeUtc=at});int hp=SkillHealth.Balance256(target);ThunderStorm.Tick(p.Serial,at,actions);return hp-SkillHealth.Balance256(target);
    }
    private static void Check(){Goldens();Delivery();AdmissionAndLifecycle();Save();Log.Information("Thunder Storm:765independent rank/hard goldens,live retained-rank gear/mastery/slow/typed potion,serial cycling/radius,expiry/cancellation and saved original gear/resources/wounds pass");}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ThunderStorm);var at=TimedEffects.Now;
        for(int rank=1;rank<=255;rank++)foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:200,k=>k=="Static Field"?hard:0,99);
            int extra=Math.Min(15,rank-1)*10+Math.Max(0,rank-16)*11;
            int min=(1+extra)*256*(100+7*hard)/100;
            Require(v.Damage256(false)==min&&v.Damage256(true)==(100+extra)*256*(100+7*hard)/100&&v.Mana256==4864
                &&v.Eval("perdelay")==125-Math.Min(100,110*rank/(rank+6))&&v.Eval("auralencalc")==3600+600*(rank-1),"Independent Storm curve "+rank+"/"+hard);
            var prepared=ThunderStorm.Prepare(v,at);Require(prepared.Rank==rank&&prepared.RadiusSubtiles==17&&prepared.IntervalFrames==v.Eval("perdelay")&&ThunderStorm.Valid(prepared),"Prepared Storm source state");
        }
        foreach(var bad in new[]{new ThunderStormState(at,at,24,17,1),new(at,at,111,17,1),new(at,at,110,18,1),new(at,at,110,17,256),new(at,DateTime.SpecifyKind(at,DateTimeKind.Unspecified),110,17,1)}){Require(!ThunderStorm.Valid(bad),"Malformed Storm state accepted");}
    }
    private static void Delivery()
    {
        var p=Owner();var t=FireBoltChecks.Target(p,2);var old=SorceressSpellMissiles.SeedLow;SorceressSpellMissiles.SeedLow=()=>0;try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(SorceressCombat.Release(p,CombatAbility.ThunderStorm,Serial.Zero,p.Location),"Actual Storm release");var initial=State(p);
            Require(initial is{Rank:1,IntervalFrames:110,RadiusSubtiles:17}&&NecromancerEffects.ManaBalance256(p)==mana-4864,"Storm payment/initial state");
            Require(Pulse(p,t)==939,"Live Storm FP source roll");Require(CombatPotions.Apply(p,3),"Storm typed potion");Require(Pulse(p,t)==1311,"Four separate Storm potion lanes");
            t.SetResistance(ResistanceType.Energy,100);Require(Pulse(p,t)==279,"Storm potion other elements fail lightning immunity");TimedEffects.Clear(p);Require(Pulse(p,t)==0,"Storm ignored immunity");t.SetResistance(ResistanceType.Energy,0);
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());Require(Pulse(p,t)==802,"Supported CanSlow0 Storm damage payload");AmazonSlowMissiles.Clear(p);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightningmastery",out _),"Live Storm mastery");Require(Pulse(p,t)==1075,"Storm did not refresh positive mastery");
            for(int i=1;i<20;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.thunderstorm",out _),"Storm hard rank while active");}
            Require(State(p).Rank==1&&State(p).IntervalFrames==110,"Later hard rank rewrote active Storm timing/rank");
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(Pulse(p,t)>1075&&State(p).Rank==1,"Live actual gear or cast-rank retention");p.Backpack.DropItem(orb);ClassGear.Refresh(p);Require(Pulse(p,t)==1075,"Removed gear retained Storm enhancement");
            for(int i=1;i<20;i++){Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.staticfield",out _),"Live hard Static synergy");}
            Require(Pulse(p,t)==1587&&State(p).Rank==1,"Storm did not refresh hard synergy at retained rank");
            var second=FireBoltChecks.Target(p,4);var outside=FireBoltChecks.Target(p,10);try
            {
                int hp=SkillHealth.Balance256(t),b=SkillHealth.Balance256(second),c=SkillHealth.Balance256(outside);Require(State(p).LastTarget==t.Serial.Value,"Storm target memory");Pulse(p,second);Require(SkillHealth.Balance256(second)<b&&SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(outside)==c,"Storm greater-ID/radius");Pulse(p,t);Require(SkillHealth.Balance256(t)<hp&&SkillHealth.Balance256(outside)==c,"Storm wrap/radius");
                long until=State(p).UntilUtc.Ticks;var memory=State(p).LastTarget;int life=SkillHealth.Balance256(t);Pulse(p,t,false);Require(SkillHealth.Balance256(t)==life&&State(p).LastTarget==memory&&State(p).UntilUtc.Ticks==until,"Suspended Storm attacked/reset memory/expiry");
                Require(SorceressCombat.Release(p,CombatAbility.ThunderStorm,Serial.Zero,p.Location)&&State(p).Rank==20&&State(p).IntervalFrames==41&&State(p).LastTarget==0,"Storm recast timing/memory reset");
            }finally{second.Delete();outside.Delete();}
        }finally{SorceressSpellMissiles.SeedLow=old;Clean(p,t);}
    }
    private static void AdmissionAndLifecycle()
    {
        foreach(string mode in new[]{"respec","wardrobe","death","expire"})
        {
            var p=Owner();try
            {
                ThunderStorm.Cast(p,SkillMechanics.Values(p,CombatAbility.ThunderStorm));
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Storm class change");}
                if(mode=="death"){p.Kill();}
                if(mode=="expire"){var at=TimedEffects.Now;Set(p,State(p) with{UntilUtc=at.AddMilliseconds(-1)});ThunderStorm.Tick(p.Serial,at,true);}
                Require(State(p)==null,"Storm retained invalid lifecycle: "+mode);
            }finally{Clean(p);}
        }
        var owner=Owner();try
        {
            int mana=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.ThunderStorm,null,owner.Location,Core.TickCount),"Storm cancel windup");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);Require(!ClassCombat.ResolveAction(owner,effect)&&NecromancerEffects.ManaBalance256(owner)==mana&&State(owner)==null,"Cancelled Storm paid/cast");
            NecromancerEffects.SetMana256(owner,4863);Require(!SorceressCombat.Release(owner,CombatAbility.ThunderStorm,Serial.Zero,owner.Location)&&State(owner)==null&&NecromancerEffects.ManaBalance256(owner)==4863,"Insufficient Storm mana");
            owner.MoveToWorld(new(1497,1628,10),Map.Trammel);Require(!SorceressCombat.Release(owner,CombatAbility.ThunderStorm,Serial.Zero,owner.Location),"Town Storm cast");owner.Internalize();Require(!SorceressCombat.Release(owner,CombatAbility.ThunderStorm,Serial.Zero,owner.Location),"Internal Storm cast");
        }finally{Clean(owner);}
    }
    private static void Save()
    {
        var p=Owner();p.Name="StormPulseSaved";var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var t=FireBoltChecks.Target(p,2);SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t)-257);t.Internalize();
        Require(SorceressCombat.Release(p,CombatAbility.ThunderStorm,Serial.Zero,p.Location),"Saved Storm actual cast");var hold=TimedEffects.Now.Add(FixtureSpace.Retention);var state=State(p) with{UntilUtc=hold.AddSeconds(1),NextStrikeUtc=hold,LastTarget=t.Serial.Value};Set(p,state);
        var s=new Saved(p.Serial.Value,t.Serial.Value,orb.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),state,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var held=(Mobile)World.FindMobile((Serial)s.Target);
        Log.Information("Storm reload:state={State}/{Expected} mana={Mana}/{ExpectedMana} life={Life}/{ExpectedLife} orb={Orb}",State(p),s.Storm,NecromancerEffects.ManaBalance256(p),s.Mana,SkillHealth.Balance256(held),s.Life,World.FindItem((Serial)s.Orb)?.Parent==p);
        Require(s.Generation<2&&State(p)==s.Storm&&SkillHealth.Balance256(held)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&World.FindItem((Serial)s.Orb)?.Parent==p,"Saved Storm rank/phase/memory/original gear/mana/wounds");
        var account=FixtureNetwork.Authenticate(network,p,"storm-pulse-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);var target=FireBoltChecks.Target(p,2);int hp=SkillHealth.Balance256(target);Set(p,s.Storm with{NextStrikeUtc=TimedEffects.Now.AddMilliseconds(100)});var deadline=TimedEffects.Now.AddSeconds(3);
        void Observe()
        {
            if(SkillHealth.Balance256(target)==hp&&TimedEffects.Now<deadline){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Observe));return;}
            try
            {
                Require(SkillHealth.Balance256(target)<hp&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(held)==s.Life&&State(p).Rank==s.Storm.Rank&&State(p).LastTarget==target.Serial.Value,"Real saved Storm pulse/resources/wounds");
                var at=TimedEffects.Now.Add(FixtureSpace.Retention);var inspected=State(p) with{NextStrikeUtc=at,UntilUtc=at.AddSeconds(1)};Set(p,inspected);target.Internalize();p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Target=target.Serial.Value,Life=SkillHealth.Balance256(target),Storm=inspected,Generation=s.Generation+1}));Log.Information("storm-pulse-reload-result: passed");
            }catch(Exception e){Log.Error(e,"storm-pulse-reload-result: failed");}
        }
        FixtureClock.NextLoop("storm-pulse-reload-result",Observe);
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"storm-pulse-input-");int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;string label=quick?"storm-pulse-quick":"storm-pulse-right";
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Storm input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.ThunderStorm&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&State(p)==null,"Storm paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(State(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-4864&&State(p) is{Rank:1,IntervalFrames:110},"Actual Storm once-only cost/state");Clean(p);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){Clean(p);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.ThunderStorm,(int)DiabloClass.Sorceress,p.X,p.Y,p.Z);Observe();});
    }
    private static void Offline(NetState network)
    {
        var p=Owner();var t=FireBoltChecks.Target(p,2);var account=FixtureNetwork.Authenticate(network,p,"storm-pulse-offline-");ThunderStorm.Cast(p,SkillMechanics.Values(p,CombatAbility.ThunderStorm));var now=TimedEffects.Now;var state=State(p) with{NextStrikeUtc=now.AddMilliseconds(200),UntilUtc=now.AddMilliseconds(700),LastTarget=t.Serial.Value};Set(p,state);int hp=SkillHealth.Balance256(t);Log.Information("storm-pulse-offline: ready");
        Timer.StartTimer(TimeSpan.FromMilliseconds(70),()=>
        {
            try
            {
#pragma warning disable CS0618
                network.Dispose();
#pragma warning restore CS0618
                Require(p.NetState==null,"Storm actual disconnect retained session");
                Timer.StartTimer(TimeSpan.FromMilliseconds(300),()=>
                {
                    try
                    {
                        Require(State(p) is{} next&&next.NextStrikeUtc>state.NextStrikeUtc&&next.LastTarget==state.LastTarget&&next.UntilUtc==state.UntilUtc&&SkillHealth.Balance256(t)==hp,"Offline Storm froze timer/attacked/reset memory");
                        Timer.StartTimer(TimeSpan.FromMilliseconds(500),()=>{try{Require(State(p)==null&&SorceressEffects.Profiles.Find(p)?.Storm==null&&SkillHealth.Balance256(t)==hp,"Offline Storm failed expiry/no replay");Log.Information("storm-pulse-offline-result: passed");}catch(Exception e){Log.Error(e,"storm-pulse-offline-result: failed");}finally{Clean(p,t);account.Delete();}});
                    }catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"storm-pulse-offline-result: failed");}
                });
            }catch(Exception e){Clean(p,t);account.Delete();Log.Error(e,"storm-pulse-offline-result: failed");}
        });
    }
}
