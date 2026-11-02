// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class BlessedAimAuraChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlessedAimAuraChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Blessed Aim active/passive rank and shared hit consumers",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("blessed-aim-aura-check",Check);
        foreach(string mode in new[]{"right","quick"})
        {string label="blessed-aim-aura-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>MightAuraChecks.Input(n,label,CombatAbility.BlessedAim)));}
    }
    private static void Select(PlayerMobile p,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(p,false,ability),"Blessed Aim selection");
    private static void Check()
    {
        var p=BlessedHammerDamageChecks.Owner();p.RawStr=1000;p.RawDex=200;
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        var enemy=new Ogre{NoKillAwards=true,RawDex=40000,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            var definition=SkillMechanics.For(CombatAbility.BlessedAim);
            Require(definition.SourceId==108&&definition.Number("srvdofunc")==65&&definition.Text("aurastat1")=="item_tohit_percent"
                &&definition.Text("passivecalc1")=="skill('Blessed Aim'.blvl) * par8"&&definition.Parameter(8)==5,"Pinned active/passive Blessed Aim route");
            int basis=ClassStats.PlayerAttackBase(p);
            Require(PaladinAuras.AttackPercent(p)==5&&ClassStats.AttackRating(p)==basis+basis*5/100,"Inactive hard1 passive");
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");
            Require(SkillMechanics.Rank(p,CombatAbility.BlessedAim)==2&&ClassCreation.Profiles.GetRank(p.Serial,"paladin.blessedaim")==1
                &&PaladinAuras.AttackPercent(p)==5,"Soft Blessed Aim raised hard-point passive");
            NecromancerEffects.SetMana256(p,0);var action=ClassCombat.For(p);action.NextAction=Core.TickCount+10000;long next=action.NextAction;
            Require(ClassCombat.Perform(p,CombatAbility.BlessedAim,null,p.Location,Core.TickCount)&&PaladinAuras.Effect(p,CombatAbility.BlessedAim) is{Rank:2,Percent:90}
                &&PaladinAuras.AttackPercent(p)==90&&ClassStats.AttackRating(p)==basis+basis*90/100,"Active effective rank added inactive passive or multiplied rating");
            Require(NecromancerEffects.ManaBalance256(p)==0&&action.PendingAbility==CombatAbility.None&&action.NextAction==next
                &&ClassCombat.ActionInterval(p,CombatAbility.BlessedAim)==0,"Blessed Aim paid mana or reserved an action");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.blessedaim",out _)&&PaladinAuras.AttackPercent(p)==90,"Hard rank changed active lease outside refresh");
            Select(p,CombatAbility.BlessedAim);var strong=PaladinAuras.Effect(ally,CombatAbility.BlessedAim);
            Require(strong is{Rank:3,Percent:105}&&PaladinAuras.AttackPercent(p)==105,"Hard2 plus soft1 active rank");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Select(p,CombatAbility.HolyBolt);
            Require(PaladinAuras.Effect(p,CombatAbility.BlessedAim)==null&&PaladinAuras.AttackPercent(p)==10&&ClassStats.AttackRating(p)==basis+basis*10/100
                &&PaladinAuras.Effect(ally,CombatAbility.BlessedAim)==strong,"Inactive hard2 passive or independent recipient linger");
            int defense=NamedCombat.DefenseFor(p,enemy,ClassStats.Defense(enemy));
            double plain=ClassStats.HitChance(ClassStats.AttackRatingAgainst(p,enemy),defense,ClassStats.Level(p),ClassStats.Level(enemy));
            Select(p,CombatAbility.BlessedAim);Require(PaladinAuras.AttackPercent(p)==90&&PaladinAuras.Effect(ally,CombatAbility.BlessedAim)==strong,"Lower self selection extended/replaced stronger recipient");
            double enhanced=ClassStats.HitChance(ClassStats.AttackRatingAgainst(p,enemy),defense,ClassStats.Level(p),ClassStats.Level(enemy));
            Require(enhanced>plain+.01,"Blessed Aim native hit-boundary contrast");double boundary=(plain+enhanced)/2;
            Require(ClassStats.CheckHit(p,enemy,0,()=>boundary)==true&&ClassStats.CheckHit(p,enemy,0,()=>boundary,projectile:true)==true,"Active Blessed Aim melee/projectile hit consumer");
            Select(p,CombatAbility.HolyBolt);Require(ClassStats.CheckHit(p,enemy,0,()=>boundary)==false&&ClassStats.CheckHit(p,enemy,0,()=>boundary,projectile:true)==false,"Inactive passive retained active hit chance");
            Log.Information("Blessed Aim:hard-only inactive passive;real Tarnhelm active effective rank;no passive double count;rank refresh/stronger recipient linger;zero-mana instant selection;native melee/projectile hit boundary;Amazon Penetrate/Enchant/Fanaticism coexistence reused separately");
        }
        finally{foreach(var actor in new Mobile[]{p,ally,enemy}){PaladinAuras.Clear(actor);actor.Corpse?.Delete();actor.Delete();}}
    }
}
