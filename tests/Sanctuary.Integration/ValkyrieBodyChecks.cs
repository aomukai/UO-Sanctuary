// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class ValkyrieBodyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieBodyChecks));
    private sealed record Held(uint Owner,uint Body,uint Helm,string State,int Life,int Maximum,int Mana,long Order,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-body-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-body-check",Check);Register("valkyrie-body-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Valkyrie supplied birth/snapshot/body and original save checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(bool female)
    {
        var p=new PlayerMobile{Name="ValkyrieBodyFixture",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"amazon.valkyrie",20);ClassSkillChecks.Train(p,"amazon.decoy",10);
        foreach(string skill in new[]{"dodge","avoid","evade","criticalstrike"}){ClassSkillChecks.Train(p,"amazon."+skill);}
        FixtureSpace.Corridor(p,6260,20,female?3440:3360);PoisonDaggerChecks.Named(p,"Tarnhelm");p.Mana=p.ManaMax;return p;
    }
    private static void Numeric(AmazonValkyrieBody b)
    {
        var s=b.Prepared;var n=b.BirthStats;var raw=s.Birth;
        Require(CreatureProfiles.Definition(b).Reference=="valkyrie"&&CreatureProfiles.For(b).Traits is{Family:"Valkyrie",Undead:false,Demon:false,Size:2},"Valkyrie borrowed skeleton identity/traits");
        int expectedLife=raw.SpawnLife256+SourcePercentage.Calculate(raw.SpawnLife256,20*(raw.Rank-1)+20*raw.Decoy,100);
        Require(n.Life256==expectedLife&&b.MaximumLife256==expectedLife&&SkillHealth.Maximum256(b)==expectedLife&&ClassStats.Level(b)==n.Level,"Captured source maximum/level");
        Require(b.SourceDefensePercent==10*(raw.Rank-1)&&ClassStats.Defense(b)==ClassStats.ScaleDefense(n.Defense,b.SourceDefensePercent)
            &&ClassStats.AttackRating(b)==n.Attack&&n.Attack==ValkyrieCreation.Base(raw.OwnerLevel,raw.Rank,(int)s.Difficulty,raw.Expansion,new(raw.SpawnLife256,NecromancerSummons.Source("valkyrie","AC",s.Difficulty),0)).Attack,"Source AC/AR/passive projection");
        int resist=Math.Min(85,2*(raw.Rank+raw.Decoy));
        Require(b.RawStr==25*raw.Rank&&b.RawDex==12*raw.Rank&&b.PhysicalResistance==0&&NamedElements.MagicResistance(b)==0
            &&b.FireResistance==resist&&b.EnergyResistance==resist&&b.ColdResistance==resist&&b.PoisonResistance==resist,"Captured attributes/resistances");
        Require(n.Skills.Length==4&&n.Skills.All(v=>v.Value==1)&&n.Passives.Length==7&&b.SourceRegeneration256==(raw.SpawnLife256*12>>12),"Captured hard-only skills/seven slots/base regen");
        Require(b.SkillWeaponDamage==(0,0)&&b.CombatProfile.MinDamage==0&&b.CombatProfile.MaxDamage==0&&!b.UsesNativeSummonTimer
            &&!b.CanRegenHits&&!b.CanRegenMana&&!b.CanRegenStam&&!b.BleedImmune&&b.PoisonImmune==null&&!b.Blessed,"Prepared body borrowed skeleton offense/immunity/timers");
        n.Passives[0]=new("tamper",999);Require(b.BirthStats.Passives[0].Name=="item_armor_percent","Mutable stat array escaped birth body");
        bool refused=false;try{b.BeginMelee(b.ControlMaster);}catch(NotSupportedException){refused=true;}Require(refused,"Unfinished combat silently used skeleton melee");
    }
    private static Held Scene(bool female)
    {
        var p=Owner(female);var facets=SummonDifficulties.Facets();AmazonValkyrieBody b=null;
        try
        {
            DateTime at=TimedEffects.Now.AddDays(2);int mana=NecromancerEffects.ManaBalance256(p);
            int spawn=(female?480:400)*256;var captured=ValkyrieBirthNumbers.Capture(p,spawn);
            int count=World.Mobiles.Count;
            foreach(int bad in new[]{0,399*256,481*256,400*256+1})
            {bool refused=false;try{AmazonValkyrieBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),bad,at);}catch(InvalidDataException){refused=true;}Require(refused&&World.Mobiles.Count==count,"Unsupported birth allocated a body");}
            b=AmazonValkyrieBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),spawn,at);
            Require(b.ControlMaster==p&&b.SummonClass==DiabloClass.Amazon&&b.Permitted(p)&&b.ControlSlots==0&&b.Summoned&&b.Prepared.Birth==captured
                &&SkillHealth.Full(b)&&NecromancerEffects.ManaBalance256(p)==mana,"Prepared ownership/fill/payment/public gate");Numeric(b);
            int max=b.MaximumLife256;SkillHealth.SetBalance256(b,max-2000);b.AdvanceRecovery(at.AddMilliseconds(39),true);Require(SkillHealth.Balance256(b)==max-2000,"Early recovery");
            b.AdvanceRecovery(at.AddMilliseconds(40),true);Require(SkillHealth.Balance256(b)==max-2000+b.SourceRegeneration256,"Explicit source40ms recovery");
            int life=SkillHealth.Balance256(b);b.AdvanceRecovery(at.AddMilliseconds(80),false);Require(SkillHealth.Balance256(b)==life&&b.Map==Map.Internal,"Offline recovery refilled or retained a body");PersistentMinions.Resume(p);
            var helm=p.FindItemOnLayer(Layer.Helm);p.Backpack.DropItem(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"amazon.decoy",11);
            Require(b.Prepared.Birth==captured,"Existing body recaptured gear/hard-point change");
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
            {
                int oldLife=SkillHealth.Balance256(b),oldMax=SkillHealth.Maximum256(b);p.MoveToWorld(new(6260,female?3440:3360,0),map);SummonDifficulties.Sync(p);Numeric(b);
                Require(b.Prepared.Birth==captured&&b.Prepared.Difficulty==SummonDifficulties.For(map)&&SkillHealth.Balance256(b)==(long)oldLife*SkillHealth.Maximum256(b)/oldMax,"Difficulty rerolled birth or changed relative wounds");
            }
            Require(p.EquipItem(helm),"Restore saved helm");ClassGear.Refresh(p);
            var state=b.Prepared;string text=b.SavedText;int before=SkillHealth.Balance256(b);bool invalid=false;
            try{b.RestorePrepared(state with{Birth=state.Birth with{ColdSunder=1}});}catch(InvalidDataException){invalid=true;}
            Require(invalid&&b.SavedText==text&&SkillHealth.Balance256(b)==before,"Unadmitted sunder partially mutated body");
            var enemy=new GraveyardZombie{Frozen=true,CantWalk=true};enemy.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);enemy.AIObject?.AITimer.Stop();
            try{Require(b.CanBeDamaged(),"Prepared body is invulnerable");SkillHealth.Damage(enemy,b,257);Require(SkillHealth.Balance256(b)==before-257,"Actual fractional damage commitment");}finally{enemy.Delete();}
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
            return new(p.Serial.Value,b.Serial.Value,helm.Serial.Value,b.SavedText,SkillHealth.Balance256(b),b.MaximumLife256,mana,BattleCommand.PetOrder(b));
        }
        catch{b?.Delete();p.Delete();throw;}
        finally{SummonDifficulties.SetFacets(facets);}
    }
    private static void ClassParking()
    {
        var p=Owner(false);AmazonValkyrieBody b=null;
        try
        {
            DateTime at=TimedEffects.Now.AddDays(2);b=AmazonValkyrieBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),400*256,at);
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b);
            ClassCreation.Profiles.SelectHistory(p,DiabloClass.Warlock);
            b.AdvanceRecovery(at.AddMilliseconds(40),true);
            Require(b.Map==Map.Internal&&SkillHealth.Balance256(b)==life,"Shared recovery parked the wrong class or healed inactive Valkyrie");
            ClassCreation.Profiles.SelectHistory(p,DiabloClass.Amazon);PersistentMinions.Resume(p);
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==life,"Class return refilled or lost Valkyrie");
        }
        finally{b?.Delete();p.Delete();}
    }
    private static void Check(){ClassParking();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        var rows=JsonSerializer.Deserialize<Held[]>(File.ReadAllText(Manifest));
        foreach(var s in rows)
        {
            var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;var b=World.FindMobile((Serial)s.Body)as AmazonValkyrieBody;var helm=World.FindItem((Serial)s.Helm);
            Require(s.Generation<2&&p!=null&&b!=null&&b.ControlMaster==p&&helm?.Parent==p&&b.SavedText==s.State&&SkillHealth.Balance256(b)==s.Life
                &&b.MaximumLife256==s.Maximum&&NecromancerEffects.ManaBalance256(p)==s.Mana&&BattleCommand.PetOrder(b)==s.Order,"Original owner/body/gear/wounds/snapshot/public gate changed");Numeric(b);
            int before=SkillHealth.Balance256(b);b.AdvanceRecovery(TimedEffects.Now,false);Require(SkillHealth.Balance256(b)==before,"Reload healed before future deadline");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(rows.Select(s=>s with{Generation=s.Generation+1}).ToArray()));
    }
}
