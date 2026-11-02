// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class OffensiveAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(OffensiveAuraChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Concentration/Fanaticism real soft-rank packets and shared instant input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("offensive-aura-check",Check);
        foreach(var ability in new[]{CombatAbility.Concentration,CombatAbility.Fanaticism})foreach(string mode in new[]{"right","quick"})
        {var captured=ability;string label="offensive-aura-"+ability.ToString().ToLowerInvariant()+"-"+mode;
            Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>MightAuraChecks.Input(n,label,captured)));}
    }
    private static void Select(PlayerMobile p,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(p,false,ability),"Offensive aura selection");
    private static void Check()
    {
        var p=BlessedHammerDamageChecks.Owner();p.RawStr=1000;
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");NecromancerEffects.SetMana256(p,0);Select(p,CombatAbility.Concentration);
            var concentration=PaladinAuras.Effect(ally,CombatAbility.Concentration);
            Require(SkillMechanics.Rank(p,CombatAbility.Concentration)==2&&concentration is{Rank:2,Percent:75,UninterruptChance:20}
                &&PaladinOffense.HammerBonus(ally)==37&&NecromancerEffects.ManaBalance256(p)==0,"Real soft-rank Concentration packet or truncated Hammer bonus");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Select(p,CombatAbility.Concentration);
            Require(PaladinAuras.Effect(p,CombatAbility.Concentration) is{Rank:1,Percent:60,UninterruptChance:20}
                &&PaladinAuras.Effect(ally,CombatAbility.Concentration)==concentration,"Lower self Concentration partly rewrote stronger recipient");
            // Isolate the next packet; ordinary simultaneous-aura damage is retained in PaladinOffenseChecks.
            PaladinAuras.Clear(ally);Require(p.EquipItem(helm),"Fanaticism actual Tarnhelm");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,256);Select(p,CombatAbility.Fanaticism);
            Require(SkillMechanics.Rank(p,CombatAbility.Fanaticism)==2&&PaladinAuras.Effect(p,CombatAbility.Fanaticism) is{Rank:2,Percent:18,AttackPercent:45,DamagePercent:67}
                &&PaladinAuras.Effect(ally,CombatAbility.Fanaticism) is{Rank:2,Percent:18,AttackPercent:45,DamagePercent:33}
                &&PaladinOffense.AttackRate(ally)==18&&NecromancerEffects.ManaBalance256(p)==256,"Provisional positive-mana self/ally Fanaticism soft-rank packet");
            NecromancerEffects.SetMana256(p,0);Select(p,CombatAbility.Fanaticism);var stronger=PaladinAuras.Effect(ally,CombatAbility.Fanaticism);
            Require(PaladinAuras.Effect(p,CombatAbility.Fanaticism) is{Rank:2,Percent:18,AttackPercent:45,DamagePercent:33}
                &&stronger is{Rank:2,Percent:18,AttackPercent:45,DamagePercent:33},"Provisional zero-mana self packet altered independent rate/AR");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Select(p,CombatAbility.Fanaticism);
            Require(PaladinAuras.Effect(p,CombatAbility.Fanaticism) is{Rank:1,Percent:14,AttackPercent:40,DamagePercent:25}
                &&PaladinAuras.Effect(ally,CombatAbility.Fanaticism)==stronger,"Lower self Fanaticism partly rewrote stronger multi-stat recipient");
            Log.Information("Offensive auras:actual Tarnhelm rank2;Concentration75/chance20/Hammer37;Fanaticism18/45/33and provisional67positive-mana self;lower self rank leaves stronger recipient packet intact;existing recovery/timing/damage/reloads retained");
        }
        finally{foreach(var actor in new Mobile[]{p,ally}){PaladinAuras.Clear(actor);actor.Corpse?.Delete();actor.Delete();}}
    }
}
