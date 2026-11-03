// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GoatmanBodyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GoatmanBodyChecks));
    private sealed record Actor(uint Owner,uint Body,uint Helm,bool Female,string State,int Life,int Maximum,int Mana,long Order);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-body-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("goatman-body-check",Check);Register("goatman-body-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared saved Warlock/Witch Goatman life/owner/difficulty",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(bool female)
    {
        var p=new PlayerMobile{Name="GoatmanBodyFixture",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman");ClassSkillChecks.Train(p,"warlock.demonicmastery",16);ClassSkillChecks.Train(p,"warlock.bloodoath",5);
        FixtureSpace.Corridor(p,6100,24,female?3720:3680);PoisonDaggerChecks.Named(p,"Tarnhelm");NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Numeric(WarlockGoatmanBody b)
    {
        var birth=b.Prepared.Snapshot.Birth;var r=b.BodyResistances;
        Require(CreatureProfiles.Definition(b).Reference=="wargoatman"&&CreatureProfiles.For(b).Traits is{Family:"Demon",Demon:true,Undead:false,Size:2}
            &&ClassStats.Level(b)==birth.PetLevel&&ClassStats.AttackRating(b)==birth.ProjectedAttack&&ClassStats.Defense(b)==birth.ProjectedArmor,"Live source identity/footprint/level/AR/defense");
        Require(b.MaximumLife256==birth.MaximumLife256&&SkillHealth.Maximum256(b)==birth.MaximumLife256
            &&b.PhysicalResistance==r.Physical&&b.FireResistance==20&&b.ColdResistance==50&&b.EnergyResistance==20&&b.PoisonResistance==20
            &&NamedElements.MagicResistance(b)==0,"Actual Goatman fractional maximum/resistances");
        Require(b.SourceRegeneration256==(birth.BaseLife256*3>>12)&&b.CombatProfile.MinDamage==0&&b.CombatProfile.MaxDamage==0
            &&b.SkillWeaponDamage==(0,0)&&!b.UsesNativeSummonTimer&&!b.CanRegenHits&&!b.CanRegenMana&&!b.BleedImmune&&b.PoisonImmune==null&&!b.Blessed,"Body borrowed skeleton offense/immunities/recovery");
        bool refused=false;try{b.BeginMelee(b.ControlMaster);}catch(NotSupportedException){refused=true;}Require(refused,"Unfinished combat silently substituted skeleton melee");
    }
    private static Actor Scene(bool female)
    {
        var p=Owner(female);var facets=SummonDifficulties.Facets();WarlockGoatmanBody b=null;
        try
        {
            var before=GoatmanPrepared.Capture(p);int mana=NecromancerEffects.ManaBalance256(p);DateTime at=TimedEffects.Now.AddDays(2);
            b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);Require(b.ControlMaster==p&&b.Permitted(p)&&b.SummonClass==DiabloClass.Warlock&&b.ControlSlots==0&&b.Summoned&&b.Prepared.Snapshot==before
                &&SkillHealth.Full(b)&&NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.Available(p,CombatAbility.SummonGoatman),"Body ownership/fill/capture or accidental learned cast/payment");Numeric(b);
            int maximum=b.MaximumLife256;SkillHealth.SetBalance256(b,maximum-257);b.AdvanceRecovery(at.AddMilliseconds(39),true);Require(SkillHealth.Balance256(b)==maximum-257,"Early recovery");
            b.AdvanceRecovery(at.AddMilliseconds(40),true);Require(SkillHealth.Balance256(b)==maximum-257+b.SourceRegeneration256,"Source40ms base-life recovery");
            int life=SkillHealth.Balance256(b);b.AdvanceRecovery(at.AddMilliseconds(80),false);Require(SkillHealth.Balance256(b)==life&&b.Map==Map.Internal,"Offline frame healed or retained world body");
            // The original snapshot, not changed live skill gear/ranks, supplies
            // subsequent difficulty recomposition.
            var helm=p.FindItemOnLayer(Layer.Helm);p.Backpack.DropItem(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.summongoatman",2);
            Require(b.Prepared.Snapshot==before,"Gear/training recaptured existing birth");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int oldLife=SkillHealth.Balance256(b),oldMax=SkillHealth.Maximum256(b);p.MoveToWorld(new(6100,female?3720:3680,0),map);SummonDifficulties.Sync(p);Numeric(b);
                Require(b.Prepared.Snapshot==GoatmanPrepared.Rescale(before,SummonDifficulties.For(map))&&SkillHealth.Balance256(b)==(long)oldLife*SkillHealth.Maximum256(b)/oldMax,"Difficulty recaptured birth or lost relative wounds");
            }
            Require(p.EquipItem(helm),"Restore actual saved helm");ClassGear.Refresh(p);
            var state=b.Prepared;string text=b.SavedText;int beforeInvalid=SkillHealth.Balance256(b);bool invalid=false;
            try{b.RestorePrepared(state with{Snapshot=state.Snapshot with{Birth=state.Snapshot.Birth with{Suppliers=state.Snapshot.Birth.Suppliers with{InheritedPhysical5=1,InheritedPhysical7=1}}}});}catch(InvalidDataException){invalid=true;}
            Require(invalid&&b.SavedText==text&&SkillHealth.Balance256(b)==beforeInvalid,"Unsupported inherited sunder partially replaced state");
            // Reset one controlled fractional wound only for retained inspection.
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
            var saved=new Actor(p.Serial.Value,b.Serial.Value,helm.Serial.Value,female,b.SavedText,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),NecromancerEffects.ManaBalance256(p),BattleCommand.PetOrder(b));
            b=null;p=null;return saved;
        }
        finally{SummonDifficulties.SetFacets(facets);b?.Delete();p?.Delete();}
    }
    private static void IncomingHealth()
    {
        var p=Owner(false);var b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));
        var enemy=new GraveyardZombie{NoKillAwards=true,CantWalk=true};enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        try
        {
            Require(b.CanBeDamaged(),"Prepared Goatman is accidentally invulnerable");int before=SkillHealth.Balance256(b);
            SkillHealth.Damage(enemy,b,257);Require(b.Alive&&SkillHealth.Balance256(b)==before-257,"Actual native health commitment lost the fractional Goatman wound");
            Require(SkillHealth.Heal(b,1)==1&&SkillHealth.Balance256(b)==before-256,"Fractional Goatman healing transport");
        }
        finally{enemy.Delete();b.Delete();p.Delete();}
    }
    private static void Check(){IncomingHealth();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        foreach(var s in JsonSerializer.Deserialize<Actor[]>(File.ReadAllText(Manifest)))
        {
            var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var b=World.FindMobile((Serial)s.Body) as WarlockGoatmanBody;var helm=World.FindItem((Serial)s.Helm);
            Require(p!=null&&b!=null&&b.ControlMaster==p&&p.Female==s.Female&&helm?.Parent==p&&b.SavedText==s.State&&SkillHealth.Balance256(b)==s.Life
                &&SkillHealth.Maximum256(b)==s.Maximum&&NecromancerEffects.ManaBalance256(p)==s.Mana&&BattleCommand.PetOrder(b)==s.Order&&!ClassCombat.Available(p,CombatAbility.SummonGoatman),"Original Goatman/owner/gear/wounds/snapshot/order/public gate changed");
            Numeric(b);int before=SkillHealth.Balance256(b);b.AdvanceRecovery(TimedEffects.Now,false);Require(SkillHealth.Balance256(b)==before,"Reload healed before retained future deadline");
        }
    }
}
