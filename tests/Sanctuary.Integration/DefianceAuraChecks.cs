// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class DefianceAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DefianceAuraChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Defiance defense composition,hard/soft synergy and shared instant input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("defiance-aura-check",Check);
        foreach(string mode in new[]{"right","quick"})
        {string label="defiance-aura-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>MightAuraChecks.Input(n,label,CombatAbility.Defiance)));}
    }
    private static void Select(PlayerMobile p)=>Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Defiance),"Defiance selection");
    private static void Check()
    {
        var p=BlessedHammerDamageChecks.Owner();p.RawStr=1000;p.RawDex=400;
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.RawDex=400;ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var barbarian=ClassSkillChecks.Player(DiabloClass.Barbarian);barbarian.RawDex=400;barbarian.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        var shield=new WoodenKiteShield();ClassGear.Profiles.Convert(shield,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(shield),"Defiance real shield");
        try
        {
            var definition=SkillMechanics.For(CombatAbility.Defiance);
            Require(definition.SourceId==104&&definition.Number("srvdofunc")==65&&definition.Number("aurafilter")==73731
                &&definition.Text("aurastat1")=="skill_armor_percent"&&definition.Text("aurastate")=="defiance","Pinned Defiance route");
            int basis=ClassStats.Defense(p),allyBasis=ClassStats.Defense(ally);
            Require(basis>0&&allyBasis==100,"Defiance raw defense fixtures");
            HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield,1));
            Require(HolyShields.DefensePercent(p)==40,"Rank1 Holy Shield with one hard Defiance");
            SorceressEffects.CastArmor(ally,CombatAbility.FrozenArmor,SkillMechanics.Values(ally,CombatAbility.FrozenArmor,1));
            // Apply the existing Shout recipient callback; its cast/flight is covered by ShoutChecks.
            BarbarianShout.Apply(barbarian,ally,1,TimedEffects.Now);
            NecromancerEffects.SetMana256(p,0);var action=ClassCombat.For(p);action.NextAction=Core.TickCount+10000;long next=action.NextAction;
            Require(ClassCombat.Perform(p,CombatAbility.Defiance,null,p.Location,Core.TickCount),"Zero-mana Defiance activation");
            Require(PaladinAuras.Effect(p,CombatAbility.Defiance) is{Rank:1,Percent:70}&&PaladinAuras.DefensePercent(ally)==70
                &&ClassStats.Defense(p)==basis+basis*110/100&&ClassStats.Defense(ally)==300,"Defiance added after armor or multiplied other skill percentages");
            Require(NecromancerEffects.ManaBalance256(p)==0&&action.NextAction==next&&action.PendingAbility==CombatAbility.None
                &&ClassCombat.ActionInterval(p,CombatAbility.Defiance)==0,"Defiance reserved an action or paid mana");
            var stored=HolyShields.State(p);var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");
            Require(SkillMechanics.Rank(p,CombatAbility.Defiance)==2&&ClassCreation.Profiles.GetRank(p.Serial,"paladin.defiance")==1
                &&HolyShields.State(p)==stored&&HolyShields.DefensePercent(p)==40,"Soft Defiance entered Holy Shield hard synergy");
            Select(p);Require(PaladinAuras.Effect(ally,CombatAbility.Defiance) is{Rank:2,Percent:80},"Real Tarnhelm did not raise active Defiance");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.defiance",out _)&&HolyShields.DefensePercent(p)==55
                &&HolyShields.State(p)==stored&&PaladinAuras.Effect(ally,CombatAbility.Defiance) is{Rank:2,Percent:80},"Hard synergy or aura changed outside its own refresh phase");
            Select(p);var strong=PaladinAuras.Effect(ally,CombatAbility.Defiance);
            Require(strong is{Rank:3,Percent:90},"Hard plus soft active Defiance rank");p.AddToBackpack(helm);ClassGear.Refresh(p);Select(p);
            Require(PaladinAuras.Effect(p,CombatAbility.Defiance) is{Rank:2,Percent:80}&&PaladinAuras.Effect(ally,CombatAbility.Defiance)==strong
                &&HolyShields.DefensePercent(p)==55,"Self reselection or stronger recipient linger after gear removal");
            action.RunningStep=true;action.RunningUntil=Core.TickCount+1000;action.StepMap=p.Map;action.StepPosition=p.Location;
            Require(ClassStats.Defense(p)==0&&PaladinAuras.DefensePercent(p)==80,"Running lost aura state or retained defense");action.RunningUntil=0;
            Require(ClassStats.Defense(p)>0&&ClassStats.Defense(barbarian)>0,"Standing or Barbarian defense fixture");
            BarbarianBerserk.Begin(barbarian,SkillMechanics.Values(barbarian,CombatAbility.Berserk),Core.TickCount);
            Require(ClassStats.Defense(barbarian)==0&&PaladinAuras.DefensePercent(barbarian)==90,"Defiance escaped Berserk final override");BarbarianBerserk.Clear(barbarian);
            Require(ClassStats.Defense(barbarian)>0,"Berserk removal lost Defiance defense");
            Log.Information("Defiance:stat171 non-direct;zero mana;additive Holy Shield/Frozen Armor/Shout;real Tarnhelm soft aura versus live hard synergy;stronger recipient linger;running/Berserk final defense;shared original Defiance saves and instant wire paths retained");
        }
        finally
        {
            foreach(var actor in new[]{p,ally,barbarian}){PaladinAuras.Clear(actor);HolyShields.Clear(actor);BarbarianShout.Clear(actor);BarbarianBerserk.Clear(actor);actor.Corpse?.Delete();actor.Delete();}
        }
    }
}
