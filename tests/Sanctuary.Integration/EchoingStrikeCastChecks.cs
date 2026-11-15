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

public static class EchoingStrikeCastChecks
{
    private const CombatAbility Ability=CombatAbility.EchoingStrike;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EchoingStrikeCastChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"echoing-strike-cast-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        // A regeneration timer may already be scheduled before fixture naming.
        // Gate its first callback too;changing the next interval is insufficient.
        var canHp=ClassGearHooks.CanRegenerateHits;var canMp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("EchoingCast",StringComparison.Ordinal)==true)&&canHp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("EchoingCast",StringComparison.Ordinal)==true)&&canMp?.Invoke(m)!=false;
        Mobile.HitsRegenRateHandler=m=>m.Name?.StartsWith("EchoingCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>m.Name?.StartsWith("EchoingCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"echoing-strike-cast-reload-check":"echoing-strike-cast-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved learned Echoing input/lifecycle and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        foreach(string choice in new[]{"left","right","quick"})
        {
            string mode=choice,name="echoing-strike-cast-"+choice;
            ConsoleInputHandler.RegisterCommand(name+"-check","Real Echoing packet and automatic missile contacts",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(name+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",name+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-check");}}));
        }
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(bool female,int column)
    {
        var p=new PlayerMobile{Name="EchoingCastFixture",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.echoingstrike");
        FixtureSpace.Corridor(p,column,24,3400);var w=new Dagger();p.AddToBackpack(w);Require(p.EquipItem(w),"Fixture dagger");
        ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static EchoingFlight Cast(PlayerMobile p,Mobile target=null)
    {
        Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);var point=new Point3D(p.X+12,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,target,point,Core.TickCount),"Learned start");var s=ClassCombat.For(p);var aim=target?.Location??point;
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==aim&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"SC/FCR or locked point");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Early payment");
        if(target!=null){target.MoveToWorld(new(target.X+1,target.Y,target.Z),target.Map);Require(s.PendingPoint==aim,"Aim followed target during windup");}
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"One learned release");
        var flights=EchoingStrikeWorld.For(p);Require(flights.Length==1&&NecromancerEffects.ManaBalance256(p)==mana-flights[0].Packet.Input.Skill.Mana256,"Once-only release debit");
        return flights[0];
    }
    private static void Controls(PlayerMobile p)
    {
        Require(EchoingStrikeWorld.Approved&&ClassCombat.Available(p,Ability)&&!ClassAbilities.SelfTarget(Ability)
            &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Approved left/right/quick metadata");
        var target=Target(p,5);try{Cast(p,target);}finally{target.Delete();EchoingStrikeWorld.Clear(p);}
        var state=Cast(p);ClassCombat.Stop(p);Require(EchoingStrikeWorld.For(p).Single()==state,"Stop recalled released echo");
        EchoingStrikeWorld.Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var aim=new Point3D(p.X+12,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Cancel start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p,effect)&&EchoingStrikeWorld.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Cancel paid or emitted");
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Late mana start");effect=ClassCombat.For(p).EffectAt;
        NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
        Require(!ClassCombat.ResolveAction(p,effect)&&EchoingStrikeWorld.For(p).Length==0,"Late mana emitted");Ready(p);
        Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Underfunded start/fallback");
        state=Cast(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(EchoingStrikeWorld.For(p).Single()==state&&!ClassCombat.Available(p,Ability),"Respec rewrote a released snapshot");
        ClassSkillChecks.Train(p,"warlock.echoingstrike");EchoingStrikeWorld.Clear(p);
        state=Cast(p);var origin=p.Location;var destination=new Point3D(p.X+16,p.Y,p.Z);
        var town=new GuardedRegion("EchoingTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try
        {
            p.MoveToWorld(destination,p.Map);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int hp=SkillHealth.Balance256(p);
            Require(MinionSafeAreas.Contains(p)&&!ClassCombat.Perform(p,Ability,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Safe-area admission");
            EchoingStrikeWorld.TickPrepared(state.NextUtc,true,p.Serial.Value);
            Require(EchoingStrikeWorld.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==hp,"Safe-area retirement healed or refunded");
        }
        finally{p.MoveToWorld(origin,p.Map);town.Unregister();}
        state=Cast(p);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Ready(p);p.Combatant=null;
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&EchoingStrikeWorld.For(p).Length==0,"Class switch retained flight");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock)&&p.EquipItem(weapon),"Restore class/weapon");
        state=Cast(p);p.Kill();Require(!p.Alive&&EchoingStrikeWorld.For(p).Length==0,"Actual death retained flight");p.Resurrect();
        if(weapon.Parent!=p){p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Restore original weapon after death");}
        Ready(p);NecromancerEffects.SetMana256(p,80*256+19);
        Log.Information("Echoing learned:SC/FCR/locked aim/all bindings,one payment,early/cancel/late-low mana,no fallback,respec snapshot,Stop retention,safe-area/class/death cleanup pass");
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="EchoingCastTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static void Scene(NetState network)
    {
        var p=Fresh(true,6600);var account=FixtureNetwork.Authenticate(network,p,"echoing-learned-");bool kept=false;
        try
        {
            Controls(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);p.Name="EchoingCastSaved";
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p))));kept=true;
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){EchoingStrikeWorld.Clear(p);p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"echoing-learned-reload-");
        try
        {
            Require(p.Female&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
                &&EchoingStrikeWorld.For(p).Length==0&&ClassCombat.For(p).Echoing==null,"Saved owner/gear/resources or transient replay");
            var b=SkillBindings.Instance.Get(p);Require(b.Left==Ability&&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Saved bindings");
            // Saved players are internalized at boot;activate in a legal fixture corridor.
            FixtureSpace.Corridor(p,6600,24,3400);Cast(p);EchoingStrikeWorld.Clear(p);Ready(p);SkillHealth.SetBalance256(p,saved.Life);NecromancerEffects.SetMana256(p,saved.Mana);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Echoing learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",6500);p.Name="EchoingCastWire";
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Wire bindings");
        var target=Target(p,4);var account=FixtureNetwork.Authenticate(network,p,"echoing-wire-");Ready(p);NecromancerEffects.SetMana256(p,100*256+17);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);bool began=false,released=false,done=false;long deadline=Core.TickCount+8000;string label="echoing-strike-cast-"+mode;
        void Cleanup(){EchoingStrikeWorld.Clear(p);ClassCombat.Stop(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Wire cast/contact timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability)
                {Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==target.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Wire aim/unpaid windup");began=true;Log.Information("{Label}: windup",label);}
                if(EchoingStrikeWorld.For(p).FirstOrDefault()is{} state)
                {
                    if(!released){Require(began&&NecromancerEffects.ManaBalance256(p)==mana-state.Packet.Input.Skill.Mana256,"Wire release debit");released=true;}
                    if(SkillHealth.Balance256(target)<life)
                    {Require(released&&state.NextUtc>state.CreatedUtc.AddMilliseconds(40),"Automatic shared missile clock did not advance");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,target.Serial.Value,target.X,target.Y,target.Z);Inspect();}));
    }
}
