// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SacrificeAllocationChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SacrificeAllocationChecks));
    public static void Configure()
    {
        foreach(string command in new[]{"sacrifice-allocation-check","sacrifice-allocation-reload-check"})
        {string name=command;ConsoleInputHandler.RegisterCommand(name,"Sacrifice start allocation and release-time life cap",_=>Core.LoopContext.Post(()=>
            {try{Check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"left","right","quick"})
        {string label="sacrifice-allocation-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Sacrifice wire input",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="SacrificeAllocationFixture"&&hp?.Invoke(p)!=false;
        var mp=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="SacrificeAllocationFixture"&&mp?.Invoke(p)!=false;
    }
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Paladin);p.Name="SacrificeAllocationFixture";p.AddItem(new Backpack());
        p.MoveToWorld(ClassSkillChecks.Corridor(p,5750,1800),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Sacrifice)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Sacrifice)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Sacrifice),"Sacrifice bindings");
        return p;
    }
    private static Ogre Target(PlayerMobile p)
    {
        var t=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,0);}return t;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static bool Start(PlayerMobile p,Mobile t){Ready(p);return ClassCombat.Perform(p,CombatAbility.Sacrifice,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Clean(PlayerMobile p,Ogre t){t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static void Snapshot()
    {
        var p=Owner();var t=Target(p);int hits=0,damageRolls=0;
        try
        {
            PaladinSacrifice.HitRoll=()=>{hits++;return 0;};PaladinSacrifice.Roll=n=>{damageRolls++;return 0;};PaladinSacrifice.ElementRoll=()=>0;
            int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t);var values=SkillMechanics.Values(p,CombatAbility.Sacrifice);
            int physical=ClassStats.SkillPhysicalRange256(p,(int)values.Eval("calc1"),0).Min;
            Require(Start(p,t),"Sacrifice allocation start");
            Require(hits==1&&damageRolls==1&&SkillHealth.Balance256(p)==hp&&SkillHealth.Balance256(t)==life,
                "Sacrifice must allocate hit and physical damage at start without committing either life");
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt-1),"Early Sacrifice release");
            PaladinSacrifice.HitRoll=()=>throw new InvalidOperationException("Sacrifice rerolled hit at release");
            PaladinSacrifice.Roll=_=>throw new InvalidOperationException("Sacrifice rerolled physical at release");
            PaladinSacrifice.ElementRoll=()=>throw new InvalidOperationException("Sacrifice rerolled elements at release");
            t.SetResistance(ResistanceType.Physical,100);
            Require(Finish(p)&&life-SkillHealth.Balance256(t)==physical&&hp-SkillHealth.Balance256(p)==PaladinSacrifice.Cost256(physical,life,8),
                "Sacrifice recalculated allocated damage/mitigation at release");
            Require(!Finish(p),"Consumed Sacrifice allocation replayed");
        }
        finally{Clean(p,t);}
    }
    private static void LiveCap()
    {
        var p=Owner();var t=Target(p);
        try
        {
            PaladinSacrifice.HitRoll=()=>0;PaladinSacrifice.Roll=_=>0;PaladinSacrifice.ElementRoll=()=>0;
            Require(Start(p,t),"Sacrifice current-life cap start");int hp=SkillHealth.Balance256(p);SkillHealth.SetBalance256(t,2*256-17);
            Require(Finish(p)&&!t.Alive&&hp-SkillHealth.Balance256(p)==PaladinSacrifice.Cost256(int.MaxValue,2*256-17,8),"Sacrifice used allocation-time life cap");
        }
        finally{Clean(p,t);}
    }
    private static void Miss()
    {
        var p=Owner();var t=Target(p);
        try
        {
            PaladinSacrifice.HitRoll=()=>1;PaladinSacrifice.Roll=_=>throw new InvalidOperationException("Miss rolled damage");
            int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t);Require(Start(p,t),"Miss allocation start");
            PaladinSacrifice.HitRoll=()=>throw new InvalidOperationException("Miss rerolled at release");
            Require(Finish(p)&&SkillHealth.Balance256(p)==hp&&SkillHealth.Balance256(t)==life,"Allocated miss dealt damage or self-cost");
        }
        finally{Clean(p,t);}
    }
    private static void ContactAndCancellation()
    {
        foreach(string mode in new[]{"out-of-range","movement","weapon","stop","held-target"})
        {
            var p=Owner();var t=Target(p);Ogre other=null;Dagger weapon=null;
            try
            {
                PaladinSacrifice.HitRoll=()=>0;PaladinSacrifice.Roll=_=>0;PaladinSacrifice.ElementRoll=()=>0;
                int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t);
                int physical=ClassStats.SkillPhysicalRange256(p,(int)SkillMechanics.Values(p,CombatAbility.Sacrifice).Eval("calc1"),0).Min;
                Require(Start(p,t),"Sacrifice cancellation start "+mode);
                if(mode=="out-of-range"){t.Location=new(t.X+10,t.Y,t.Z);}
                if(mode=="movement"){var original=p.Location;p.Location=new(p.X-1,p.Y,p.Z);p.Location=original;}
                if(mode=="weapon"){weapon=new Dagger();ClassGear.Profiles.Convert(weapon,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(weapon),"Cancellation weapon");}
                if(mode=="stop"){ClassCombat.Stop(p);}
                if(mode=="held-target")
                {
                    other=Target(p);int otherLife=SkillHealth.Balance256(other);
                    Require(ClassCombat.ContinueCast(p,CombatAbility.Sacrifice,other,other.Location,Core.TickCount),"Held Sacrifice cursor update");ClassCombat.ReleaseCast(p);
                    Require(Finish(p)&&SkillHealth.Balance256(other)==otherLife&&life-SkillHealth.Balance256(t)==physical,"Held target replaced original allocated contact");
                }
                else
                {
                    bool finished=Finish(p);
                    int expected=mode=="out-of-range"?PaladinSacrifice.Cost256(physical,life,8):0;
                    Require(finished==(mode=="out-of-range")&&SkillHealth.Balance256(t)==life&&hp-SkillHealth.Balance256(p)==expected,
                        "Sacrifice contact/cancellation ordering "+mode);
                }
                Require(!Finish(p),"Cancelled/released allocation replayed "+mode);
            }
            finally{weapon?.Delete();other?.Delete();Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PaladinSacrifice.HitRoll;var damage=PaladinSacrifice.Roll;var element=PaladinSacrifice.ElementRoll;
        try{Snapshot();LiveCap();Miss();ContactAndCancellation();Log.Information("Sacrifice allocation:hit/damage/mitigation captured before impact;no release reroll;current fractional life cap;miss record;out-of-range cost;movement/weapon/stop cancellation;held target identity and once consumption");}
        finally{PaladinSacrifice.HitRoll=hit;PaladinSacrifice.Roll=damage;PaladinSacrifice.ElementRoll=element;}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p);var account=FixtureNetwork.Authenticate(network,p,"sacrifice-input-");Ready(p);
        var hit=PaladinSacrifice.HitRoll;var roll=PaladinSacrifice.Roll;var element=PaladinSacrifice.ElementRoll;
        PaladinSacrifice.HitRoll=()=>0;PaladinSacrifice.Roll=_=>0;PaladinSacrifice.ElementRoll=()=>0;
        NecromancerEffects.SetMana256(p,0);int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t);
        int physical=ClassStats.SkillPhysicalRange256(p,(int)SkillMechanics.Values(p,CombatAbility.Sacrifice).Eval("calc1"),0).Min;
        bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){ClassCombat.Stop(p);PaladinSacrifice.HitRoll=hit;PaladinSacrifice.Roll=roll;PaladinSacrifice.ElementRoll=element;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Sacrifice input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Sacrifice&&!began)
                {
                    Require(ClassCombat.For(p).SacrificeAttack is{Hit:true}&&SkillHealth.Balance256(p)==hp&&SkillHealth.Balance256(t)==life,"Wire Sacrifice allocation/life timing");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&life-SkillHealth.Balance256(t)==physical&&hp-SkillHealth.Balance256(p)==PaladinSacrifice.Cost256(physical,life,8)
                    &&NecromancerEffects.ManaBalance256(p)==0,"Actual Sacrifice once damage/self-cost/zero-mana result");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Sacrifice,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
