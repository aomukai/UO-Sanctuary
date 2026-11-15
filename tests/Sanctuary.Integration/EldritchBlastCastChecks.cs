// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class EldritchBlastCastChecks
{
    private const CombatAbility Ability=CombatAbility.EldritchBlast;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EldritchBlastCastChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"eldritch-blast-cast-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>m.Name?.StartsWith("EldritchCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>m.Name?.StartsWith("EldritchCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"eldritch-blast-cast-reload-check":"eldritch-blast-cast-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved learned Eldritch cast/lifecycle/bindings and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        foreach(string choice in new[]{"left","right","quick"})
        {
            string mode=choice,name="eldritch-blast-cast-"+choice;
            ConsoleInputHandler.RegisterCommand(name+"-check","Real Eldritch packet and automatic first pulse",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(name+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",name+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-check");}}));
        }
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(bool female,int column)
    {
        var p=new PlayerMobile{Name="EldritchCastFixture",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.eldritchblast");ClassSkillChecks.Train(p,"warlock.hexbane");
        FixtureSpace.Corridor(p,column,24,3400);var w=new Dagger();p.AddToBackpack(w);Require(p.EquipItem(w),"Fixture dagger");
        ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static EldritchBlastState Cast(PlayerMobile p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p,Ability,p,new(-20,-20,0),Core.TickCount),"Learned self start");var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"SC/FCR or self normalization");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Early payment");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"One learned release");
        Require(EldritchBlastWorld.State(p)is{} next&&NecromancerEffects.ManaBalance256(p)==mana-next.Numbers.Mana256,"One fractional release debit");
        return EldritchBlastWorld.State(p);
    }
    private static void Controls(PlayerMobile p)
    {
        Require(EldritchBlastWorld.Approved&&ClassCombat.Available(p,Ability)&&ClassAbilities.SelfTarget(Ability)
            &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Approved self/left/right/quick metadata");
        var state=Cast(p);var same=Cast(p);Require(EldritchBlastWorld.Snapshot().Count(s=>s.Owner==p.Serial.Value)==1&&EldritchBlastWorld.State(p)==same,"Recast stacked states");
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Cancel windup start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p,effect)&&EldritchBlastWorld.State(p)==same&&NecromancerEffects.ManaBalance256(p)==mana,"Cancellation paid/replaced");
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Late mana start");effect=ClassCombat.For(p).EffectAt;
        NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);Require(!ClassCombat.ResolveAction(p,effect)&&EldritchBlastWorld.State(p)==same,"Late mana replaced old state");
        Ready(p);Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Underfunded start accepted");
        NecromancerEffects.SetMana256(p,100*256);Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Movement windup start");effect=ClassCombat.For(p).EffectAt;
        var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&EldritchBlastWorld.State(p)==same,"Moved windup released");p.MoveToWorld(origin,p.Map);
        state=Cast(p);var destination=new Point3D(p.X+16,p.Y,p.Z);var town=new GuardedRegion("EldritchTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try
        {
            p.MoveToWorld(destination,p.Map);Require(MinionSafeAreas.Contains(p),"Actual safe-area control");mana=NecromancerEffects.ManaBalance256(p);int hp=SkillHealth.Balance256(p);Ready(p);
            Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Safe-area cast admitted");var pulse=EldritchBlastWorld.TickPrepared(p,state.NextUtc,true);
            Require(pulse is{Due:true,Refreshed:0,Damaged:0}&&EldritchBlastWorld.State(p).UntilUtc==state.UntilUtc
                &&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==hp,"Safe-area state healed/reset/combat continued");
        }
        finally{p.MoveToWorld(origin,p.Map);town.Unregister();}
        state=Cast(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(EldritchBlastWorld.State(p)==null&&!ClassCombat.Available(p,Ability),"Respec retained effect/availability");ClassSkillChecks.Train(p,"warlock.eldritchblast");ClassSkillChecks.Train(p,"warlock.hexbane");
        state=Cast(p);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Ready(p);p.Combatant=null;
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&EldritchBlastWorld.State(p)==null,"Class switch retained state");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock)&&p.EquipItem(weapon),"Restore original class/weapon");
        EldritchBlastWorld.Clear(p);Ready(p);NecromancerEffects.SetMana256(p,80*256+19);
        Log.Information("Eldritch learned:SC/FCR/self and all bindings,one fractional payment/recast,cancel/low mana/movement,safe-area no recovery/reset,respec/class-switch cleanup pass");
    }
    private static void Scene(NetState network)
    {
        var p=Fresh(true,6600);var account=FixtureNetwork.Authenticate(network,p,"eldritch-learned-");bool kept=false;
        try
        {
            Controls(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);p.Name="EldritchCastSaved";
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p))));kept=true;
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"eldritch-learned-reload-");
        try
        {
            Require(p.Female&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
                &&EldritchBlastWorld.State(p)==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Saved owner/gear/resources or replay");
            var b=SkillBindings.Instance.Get(p);Require(b.Left==Ability&&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Saved bindings");
            Cast(p);EldritchBlastWorld.Clear(p);Ready(p);SkillHealth.SetBalance256(p,saved.Life);NecromancerEffects.SetMana256(p,saved.Mana);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Eldritch learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",6500);p.Name="EldritchCastWire";Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Wire bindings");
        var target=new GoatmanNpcContactChecks.Recipient{Name="EldritchCastTarget",Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(30000);target.Hits=target.HitsMax;target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
        Require(HexBane.CastPrepared(p,true)&&HexBane.ApplyPrepared(p,target,HexBane.Buff(p),true,TimedEffects.Now,true),"Wire Hex setup");
        var account=FixtureNetwork.Authenticate(network,p,"eldritch-wire-");Ready(p);NecromancerEffects.SetMana256(p,100*256+17);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-13);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);bool began=false,released=false,done=false;long deadline=Core.TickCount+8000;string label="eldritch-blast-cast-"+mode;
        void Cleanup(){EldritchBlastWorld.Clear(p);ClassCombat.Stop(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Wire cast/pulse timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability)
                {Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Wire self/unpaid windup");began=true;Log.Information("{Label}: windup",label);}
                if(EldritchBlastWorld.State(p)is{} state)
                {
                    if(!released){Require(began&&NecromancerEffects.ManaBalance256(p)==mana-state.Numbers.Mana256&&SkillHealth.Balance256(target)==life,"Wire one debit or immediate pulse");released=true;}
                    if(SkillHealth.Balance256(target)<life)
                    {Require(released&&state.NextUtc>state.StartedUtc.AddMilliseconds(1200),"Automatic shared timer did not advance");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,p.Serial.Value,p.X+1,p.Y,p.Z);Inspect();}));
    }
}
