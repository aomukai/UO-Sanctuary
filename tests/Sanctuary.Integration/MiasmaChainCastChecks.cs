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

public static class MiasmaChainCastChecks
{
    private const CombatAbility Ability=CombatAbility.MiasmaChain;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaChainCastChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"miasma-chain-cast-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("MiasmaChainCast",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("MiasmaChainCast",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"miasma-chain-cast-reload-check":"miasma-chain-cast-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved learned Chain SC/bindings/lifecycle and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
        foreach(string choice in new[]{"left","right","quick"})
        {
            string mode=choice,name="miasma-chain-cast-"+choice;
            ConsoleInputHandler.RegisterCommand(name+"-check","Actual Miasma packets and automatic head/link/cloud damage",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(name+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",name+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-check");}}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(bool female,int column)
    {
        var p=new PlayerMobile{Name="MiasmaChainCastFixture",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.miasmachain");
        FixtureSpace.Corridor(p,column,24,3200);var w=new Dagger();p.AddToBackpack(w);Require(p.EquipItem(w),"Original dagger");
        ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="MiasmaChainCastTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static MiasmaChainState Cast(PlayerMobile p,Mobile target=null)
    {
        Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);var point=new Point3D(p.X+12,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,target,point,Core.TickCount),"Learned SC startup");var s=ClassCombat.For(p);var aim=target?.Location??point;
        Require(s.PendingTarget==(target?.Serial??Serial.Zero)&&s.PendingPoint==aim&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Selected target/SC clock");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Early payment");
        if(target!=null){target.MoveToWorld(new(target.X,target.Y+1,target.Z),target.Map);}
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Once-only learned release");var node=MiasmaChainWorld.For(p).Single();
        Require(node.Maker&&NecromancerEffects.ManaBalance256(p)==mana-node.Birth.Numbers.Mana256&&(target==null||node.TargetY>p.Y),"Snapshot payment/live selected-target aim");return node;
    }
    private static void Controls(PlayerMobile p)
    {
        Require(CombatTiming.For(p,Ability)==WarlockCastClock.Calculate(CombatTiming.Fcr(p))
            &&CombatTiming.Calculate(DiabloClass.Warlock,Ability,new(AttackFamily.Bow,-40),150,20,80,60)==WarlockCastClock.Calculate(20),"Approved Chain HUD FCR versus weapon/IAS/chill timing");
        Require(MiasmaChainWorld.Approved&&ClassCombat.Available(p,Ability)&&!ClassAbilities.SelfTarget(Ability)
            &&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Approved left/right/quick metadata");
        var target=Target(p,5);try{Cast(p,target);}finally{target.Delete();MiasmaWorld.Clear(p);}
        var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Cast(p);MiasmaWorld.Clear(p);Require(p.EquipItem(weapon),"Restore dagger after unarmed cast");
        var state=Cast(p);ClassCombat.Stop(p);Require(MiasmaChainWorld.For(p).Single()==state,"Stop recalled release");MiasmaWorld.Clear(p);Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p);var aim=new Point3D(p.X+12,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Cancel start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p,effect)&&MiasmaChainWorld.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Cancellation paid/emitted");
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Late mana start");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
        Require(!ClassCombat.ResolveAction(p,effect)&&MiasmaChainWorld.For(p).Length==0,"Late mana emitted");Ready(p);
        Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Insufficient mana fallback");
        state=Cast(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);
        Require(MiasmaChainWorld.For(p).Single()==state&&!ClassCombat.Available(p,Ability),"Respec rewrote release");ClassSkillChecks.Train(p,"warlock.miasmachain");MiasmaWorld.Clear(p);
        state=Cast(p);var origin=p.Location;var destination=new Point3D(p.X+16,p.Y,p.Z);var town=new GuardedRegion("MiasmaTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try
        {
            p.MoveToWorld(destination,p.Map);Ready(p);mana=NecromancerEffects.ManaBalance256(p);int hp=SkillHealth.Balance256(p);
            Require(MinionSafeAreas.Contains(p)&&!ClassCombat.Perform(p,Ability,null,new(p.X+1,p.Y,p.Z),Core.TickCount),"Safe admission");MiasmaChainWorld.TickPrepared(state.NextUtc,true,p.Serial.Value);
            Require(MiasmaChainWorld.For(p)is[{} parked]&&parked.UntilUtc==state.UntilUtc&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==hp,"Safe parking healed/refunded");
        }
        finally{p.MoveToWorld(origin,p.Map);town.Unregister();MiasmaWorld.Clear(p);}
        state=Cast(p);p.AddToBackpack(weapon);Ready(p);p.Combatant=null;
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&MiasmaChainWorld.For(p).Length==0,"Class switch retained graph");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock)&&p.EquipItem(weapon),"Restore class/weapon");
        Cast(p);p.Kill();Require(!p.Alive&&MiasmaChainWorld.For(p).Length==0,"Death retained graph");p.Resurrect();
        if(weapon.Parent!=p){p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Restore original weapon after death");}
        Ready(p);NecromancerEffects.SetMana256(p,80*256+19);
        Log.Information("Chain learned:SC/FCR/current selected aim/all bindings,unarmed,one debit,no replay/cancel/low-mana fallback,respec snapshot,Stop retention,safe/class/death cleanup pass");
    }
    private static void Scene(NetState network)
    {
        var p=Fresh(true,6600);var account=FixtureNetwork.Authenticate(network,p,"chain-learned-");bool kept=false;
        try{Controls(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);p.Name="MiasmaChainCastSaved";
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p))));kept=true;}
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){MiasmaWorld.Clear(p);p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"chain-learned-reload-");
        try
        {
            Require(p.Female&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
                &&MiasmaChainWorld.For(p).Length==0&&ClassCombat.For(p).MiasmaChain==null,"Saved identity/gear/resources/transient");var b=SkillBindings.Instance.Get(p);
            Require(b.Left==Ability&&b.Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Saved bindings");
            FixtureSpace.Corridor(p,6600,24,3200);Cast(p);MiasmaWorld.Clear(p);Ready(p);SkillHealth.SetBalance256(p,saved.Life);NecromancerEffects.SetMana256(p,saved.Mana);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Chain learned:saved Witch/original dagger/fractional wounds+mana/all bindings retain;post-load learned cast works without replay");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",6500);p.Name="MiasmaChainCastWire";
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Wire bindings");
        if(mode=="quick"){p.AddToBackpack(CombatTiming.Equipped(p));}
        var target=Target(p,4);var account=FixtureNetwork.Authenticate(network,p,"chain-wire-");Ready(p);NecromancerEffects.SetMana256(p,100*256+17);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target),cost=SkillMechanics.Values(p,Ability).Mana256;bool began=false,released=false,direct=false,done=false;int directLife=hp;long deadline=Core.TickCount+8000;string label="miasma-chain-cast-"+mode;
        void Cleanup(){MiasmaWorld.Clear(p);ClassCombat.Stop(p);target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Wire link/cloud timeout");}var s=ClassCombat.For(p);var nodes=MiasmaChainWorld.For(p);
                if(!began&&s.PendingAbility==Ability)
                {Require(s.PendingTarget==target.Serial&&s.PendingPoint==target.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Wire selected aim/unpaid windup");began=true;Log.Information("{Label}: windup",label);}
                if(nodes.Length>0&&!released){Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire one release debit");released=true;}
                if(!direct&&SkillHealth.Balance256(target)<hp&&nodes.Any(n=>n.Attached)){direct=true;directLife=SkillHealth.Balance256(target);}
                if(MiasmaWorld.For(p).Any(n=>n.Cloud?.Ability==Ability)&&MiasmaWorld.Snapshot().Leases.Any(l=>l.Target==target.Serial.Value&&nodes.Any(n=>n.Id==l.Cast)))
                {Require(released&&direct&&NecromancerEffects.ManaBalance256(p)==mana-cost&&SkillHealth.Balance256(target)<directLife,"Automatic head/cloud damage or once-only mana");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,target.Serial.Value,target.X,target.Y,target.Z);Inspect();}));
    }
}
