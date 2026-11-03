// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GolemMasteryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GolemMasteryChecks));
    private sealed record View(int Rank,int Mastery,int Maximum,int Attack,int Velocity,int Minimum,int MaximumDamage,int Percent);
    private sealed record Saved(uint Owner,uint Pet,uint Helm,uint Original,CombatAbility Kind,View Birth,int Life,int Mana,int OwnerLife);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"golem-mastery-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("golem-mastery-check",Check);Register("golem-mastery-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Golem Mastery learned creation,captured ranks,gear,respec and original reloads",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Train(PlayerMobile p,CombatAbility kind,int mastery)
    {MagePetBuffChecks.Train(p,SkillMechanics.For(kind).TalentId,1);MagePetBuffChecks.Train(p,"necromancer.golemmastery",mastery);}
    private static SkillValues Values(SkeletonGuardian b)=>b is IronGolemBody iron?iron.BirthValues:((NecromancerMinion)b).CapturedValues;
    private static View Inspect(SkeletonGuardian b)
    {
        var v=Values(b);var damage=b.SourceWeaponBase;
        return new(v.Rank,(int)v.Formula("skill('Golem Mastery'.lvl)"),SkillHealth.Maximum256(b),b.CombatProfile.AttackRating,
            (int)v.Eval("passivecalc1"),damage.Min,damage.Max,damage.Percent);
    }
    private static SkeletonGuardian Cast(PlayerMobile p,CombatAbility kind)
    {
        var point=new Point3D(p.X+2,p.Y,p.Z);Item original=null;
        // A replacement must use legal free ground;the previous golem still
        // occupies its cell until the new candidate commits successfully.
        Require(GroundSummonPlacement.Find(p.Map,point,c=>p.Map.CanSpawnMobile(c),out point),"No free replacement placement");
        if(kind==CombatAbility.IronGolem){original=new Broadsword();ClassGear.Profiles.Ensure(original);original.MoveToWorld(point,p.Map);}
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p);int cost=SkillMechanics.Values(p,kind).Mana256;
        PoisonDaggerChecks.Ready(p);Require(NecromancerCombat.Begin(p,kind,original?.Serial??Serial.Zero,point,Core.TickCount),"Learned golem start "+kind);
        long at=ClassCombat.For(p).EffectAt;
        Require(NecromancerEffects.ManaBalance256(p)==before&&!ClassCombat.ResolveAction(p,at-1),"Passive golem benefit caused early payment/release");
        Require(ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==before-cost,"Learned once-only golem payment "+kind);
        Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==before-cost,"Golem cast replay");
        var b=NecromancerGolems.For(p).Single();b.SuspendActions();b.AIObject?.AITimer.Stop();
        if(original!=null){Require(original.Parent==b&&IronGolemItems.State((IronGolemBody)b).Item==original.Serial.Value,"Original Iron material lost");}
        return b;
    }
    private static Saved Scene(CombatAbility kind,int row)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureGolemMastery";FixtureSpace.Corridor(p,6800,24,row);
        SkeletonGuardian body=null;
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);
            Require(SkillMechanics.Rank(p,CombatAbility.GolemMastery)==0&&!ClassCombat.Available(p,CombatAbility.GolemMastery)
                &&!SkillBindings.Instance.Assign(p,false,CombatAbility.GolemMastery)&&!SkillBindings.Instance.AssignQuick(p,6,CombatAbility.GolemMastery)
                &&NecromancerEffects.ManaBalance256(p)==mana,"Untrained/passive Mastery became an active cast");
            Train(p,kind,1);body=Cast(p,kind);var low=Inspect(body);Require(low.Mastery==1&&low.Rank==1,"Hard rank1 captured supplier");
            SkillHealth.SetBalance256(body,low.Maximum-257);int wounded=SkillHealth.Balance256(body);
            Train(p,kind,20);Require(SkillMechanics.Rank(p,CombatAbility.GolemMastery)==20&&Inspect(body)==low&&SkillHealth.Balance256(body)==wounded,"Hard training recaptured/healed original golem");
            var prior=body;body=Cast(p,kind);var high=Inspect(body);
            Require(body!=prior&&NecromancerGolems.For(p).Single()==body&&high.Mastery==20&&high.Rank==1
                &&high.Maximum>low.Maximum&&high.Attack>low.Attack&&high.Velocity>low.Velocity
                &&high.Minimum-low.Minimum==(kind==CombatAbility.BloodGolem?57:kind==CombatAbility.FireGolem?76:38)
                &&high.MaximumDamage-low.MaximumDamage==(kind==CombatAbility.BloodGolem?57:kind==CombatAbility.FireGolem?76:38),"Hard Mastery failed life/AR/movement/approved damage or one-golem replacement "+kind);
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.GolemMastery)==21&&Inspect(body)==high,"Soft gear recaptured existing golem");
            body=Cast(p,kind);var captured=Inspect(body);Require(captured.Mastery==21&&captured.Rank==2&&captured.Maximum>high.Maximum,"Soft effective ranks failed fresh golem "+kind);
            p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.GolemMastery)==20&&Inspect(body)==captured,"Gear removal rewrote captured birth");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Train(p,kind,1);
            Require(!body.Deleted&&SkillMechanics.Rank(p,CombatAbility.GolemMastery)==1&&Inspect(body)==captured,"Respec rewrote old golem");
            body.SuspendActions();body.Internalize();SkillHealth.SetBalance256(body,captured.Maximum-257);TimedEffects.PreventHealing(body,TimedEffects.Now);
            NecromancerEffects.SetMana256(p,22784);SkillHealth.SetBalance256(p,37632);p.Internalize();
            uint original=body is IronGolemBody i?IronGolemItems.State(i).Item:0;
            return new(p.Serial.Value,body.Serial.Value,helm.Serial.Value,original,kind,captured,SkillHealth.Balance256(body),22784,37632);
        }
        catch{body?.Delete();p.Delete();throw;}
    }
    private static void Check()
    {
        var rows=new List<Saved>();int y=2800;
        foreach(var kind in new[]{CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.IronGolem,CombatAbility.FireGolem}){rows.Add(Scene(kind,y));y+=30;}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(rows));
    }
    private static void Reload()
    {
        var rows=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(rows.Length==4,"Missing Mastery recipient");
        foreach(var s in rows)
        {
            var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;var b=World.FindMobile((Serial)s.Pet)as SkeletonGuardian;var helm=World.FindItem((Serial)s.Helm);
            Require(p!=null&&b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&b.Map==Map.Internal&&helm?.Parent==p.Backpack
                &&SkillMechanics.Rank(p,CombatAbility.GolemMastery)==1&&Inspect(b)==s.Birth&&Values(b).Ability==s.Kind
                &&SkillHealth.Balance256(b)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.OwnerLife,"Original Mastery capture/item/wounds/resources changed on reload "+s.Kind);
            if(s.Original!=0){Require(World.FindItem((Serial)s.Original)?.Parent==b&&IronGolemItems.State((IronGolemBody)b).Item==s.Original,"Saved original Iron material lost");}
        }
    }
}
