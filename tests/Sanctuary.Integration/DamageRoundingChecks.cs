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

public static class DamageRoundingChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(DamageRoundingChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Absorber,Point3D Origin,int Map,int EnemyLife,int AbsorberLife);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"damage-rounding-checks.json");
    public static void Configure(){Register("damage-rounding-check",Check);Register("damage-rounding-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"FP256 channels, absorb healing, lethal boundaries and corpse persistence",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static int Life(Mobile target)=>target.Alive && !target.Deleted?target.Hits*256-SkillHealth.Remainder256(target):0;
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static Ogre Enemy(PlayerMobile owner,int offset=1)
    {
        var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(owner.X+offset,owner.Y,owner.Z),owner.Map);
        enemy.AIObject.AITimer.Stop();Freeze(enemy);return enemy;
    }
    private static void Resist(BaseCreature enemy,int element=50,int physical=100)
    {
        enemy.SetResistance(ResistanceType.Physical,physical);enemy.SetResistance(ResistanceType.Fire,element);
        enemy.SetResistance(ResistanceType.Cold,element);enemy.SetResistance(ResistanceType.Energy,element);
    }
    private static void Probe(PlayerMobile owner)
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT") ?? throw new InvalidOperationException("Missing source probe root");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/damage-rounding-engine-evidence.json")));
        int compared=0,negative=0;
        foreach(var row in doc.RootElement.GetProperty("probe").GetProperty("data").GetProperty("reduction").EnumerateArray())
        {
            int value=row[0].GetInt32(),resist=row[1].GetInt32(),reduction=row[2].GetInt32(),pct=row[3].GetInt32(),flat=row[4].GetInt32();
            bool monster=row[5].GetInt32()==1,bypass=row[6].GetInt32()==1;
            if(!bypass && value>0 && value<reduction){negative++;continue;} // Legacy MDR overflow is retained as an open modern question.
            if(!monster){resist=Math.Min(resist,75);}resist=Math.Max(resist,-100);
            var actual=NamedElements.Reduce256(value,bypass?Math.Min(resist,0):resist,bypass?0:reduction,bypass?0:pct,bypass?0:flat*256);
            Require(actual.Damage256==row[7].GetInt32() && actual.Absorbed256==row[8].GetInt32(),"Fixed-point reducer differs from compiled original source");compared++;
        }
        Require(compared==1260 && negative==180,"Source reducer domain coverage changed");
        foreach(var row in doc.RootElement.GetProperty("probe").GetProperty("data").GetProperty("life").EnumerateArray())
        {
            int before=row[0].GetInt32(),hit=row[1].GetInt32(),expected=row[2].GetInt32();var enemy=Enemy(owner,3);
            enemy.Hits=(before+255)/256;SkillHealth.Profiles.Remainders[enemy.Serial]=(enemy.Hits*256-before,enemy.Hits);
            SkillHealth.Damage(owner,enemy,hit);
            Require(Life(enemy)==expected,"Native life subtraction differs at the one-point boundary");
            if(expected==0){Require(SkillHealth.Remainder256(enemy)==0 && enemy.Corpse is{Deleted:false},"Lethal fractional damage lost corpse or retained remainder");}
            enemy.Corpse?.Delete();enemy.Delete();SkillHealth.Clear(enemy);
        }
        var protectedEnemy=Enemy(owner,3);protectedEnemy.Blessed=true;int hp=protectedEnemy.Hits;
        Require(SkillHealth.Damage(owner,protectedEnemy,128)==0 && protectedEnemy.Hits==hp && SkillHealth.Remainder256(protectedEnemy)==0,"Undamageable target acquired a health remainder");protectedEnemy.Delete();
    }
    private static void Packets(PlayerMobile owner,BaseCreature enemy)
    {
        var roll=PaladinVengeance.Roll;var hit=PaladinVengeance.HitRoll;var physical=PaladinVengeance.PhysicalRoll;
        try
        {
            PaladinVengeance.Roll=_=>0;PaladinVengeance.HitRoll=()=>0;PaladinVengeance.PhysicalRoll=()=>0;
            Resist(enemy);int hp=Life(enemy);var bonus=PaladinVengeance.Elements(owner,SkillMechanics.Values(owner,CombatAbility.Vengeance));
            Require(bonus.Base256==256 && bonus.Fire256==209 && bonus.Cold256==209 && bonus.Lightning256==209,"Native three-channel rounding fixture base changed");
            PaladinVengeance.Strike(owner,enemy,SkillMechanics.Values(owner,CombatAbility.Vengeance));
            Require(hp-Life(enemy)==312,"Three source channels were rounded after summing instead of individually");
            Resist(enemy,element:100,physical:50);hp=Life(enemy);GearEffects.Strike(owner,enemy,0);
            Require(hp-Life(enemy)==128,"Ordinary weapon discarded or raised half-point physical damage");
            GearEffects.Strike(owner,enemy,0);Require(hp-Life(enemy)==256,"Repeated ordinary weapon fractions did not settle exactly");
            Resist(enemy);hp=Life(enemy);SkillCombat.ResolveHitParts(owner,enemy,new(0,209/256.0,209/256.0,209/256.0,0),false);
            Require(hp-Life(enemy)==312,"General class hit packet lost per-channel source truncation");
            hp=Life(enemy);AOS.Damage(enemy,owner,1,false,0,100,0,0,0,spellDamage:true);
            Require(hp-Life(enemy)==128,"Native AOS early return discarded a sub-point class packet");
            AOS.Damage(enemy,owner,1,false,0,100,0,0,0,spellDamage:true);
            Require(hp-Life(enemy)==256,"Native AOS fractional packets failed to settle");
        }
        finally{PaladinVengeance.Roll=roll;PaladinVengeance.HitRoll=hit;PaladinVengeance.PhysicalRoll=physical;}
    }
    private static void Absorb(PlayerMobile owner,PlayerMobile absorber)
    {
        absorber.Hits=absorber.HitsMax-20;SkillHealth.Clear(absorber);int hp=Life(absorber);
        var parts=SkillCombat.MitigatePrepared(owner,absorber,new(0,129/256.0,209/256.0,0,0));
        Require(NamedElements.MasteryPercent(absorber,"fire")==0 && ClassGear.Bonus(absorber,"abs-cold%")==20
            && ClassGear.Bonus(absorber,"abs-fire")>=1,"Real absorb gear fixture changed");
        Require(Life(absorber)-hp==170 && parts.Fire==0 && NamedElements.To256(parts.Cold)==168,"Combined fractional absorb heal or source percent/flat order changed");
        SkillHealth.Damage(owner,absorber,NamedElements.To256(parts.Total));Require(Life(absorber)==hp+2,"Absorb healing/damage lost their shared fractional life balance");
        var nativeSource=Enemy(owner,3);absorber.Hits=Math.Min(100,absorber.HitsMax-20);SkillHealth.Clear(absorber);hp=Life(absorber);
        AOS.Damage(absorber,nativeSource,1,false,0,0,100,0,0,spellDamage:true);
        Require(hp-Life(absorber)==154,"Native percent absorb mixed fractional healing with whole damage");nativeSource.Delete();
        Require(SkillHealth.Heal(absorber,10000000)>0 && absorber.Hits==absorber.HitsMax && SkillHealth.Remainder256(absorber)==0,"Full heal retained a stale health remainder");
        // Keep the ledger fixture below the existing native login HP clamp; investigate full-pool restoration separately.
        absorber.Hits=Math.Min(100,absorber.HitsMax-10);SkillHealth.Clear(absorber);SkillHealth.Damage(owner,absorber,17);
    }
    private static Item Equip(PlayerMobile owner,string key)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Actual absorb gear equip: "+key);return item;
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5890,1800),owner.Map);Freeze(owner);
        Probe(owner);var enemy=Enemy(owner);enemy.Hits-=20;Packets(owner,enemy);
        var absorber=ClassSkillChecks.Player(DiabloClass.Paladin);absorber.AddItem(new Backpack());absorber.RawStr=1000;
        absorber.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Freeze(absorber);Equip(absorber,"Raven Frost");Equip(absorber,"Steelshade");Absorb(owner,absorber);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,absorber.Serial.Value,owner.Location,owner.Map.MapID,Life(enemy),Life(absorber))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Freeze(owner);
        var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);enemy.Frozen=enemy.CantWalk=true;enemy.AIObject.AITimer.Stop();Freeze(enemy);
        var absorber=(PlayerMobile)World.FindMobile((Serial)saved.Absorber);absorber.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);absorber.RawStr=1000;Freeze(absorber);
        Require(Life(enemy)==saved.EnemyLife && Life(absorber)==saved.AbsorberLife && SkillHealth.Remainder256(absorber)==17,
            $"Saved class/ordinary/absorb health remainder changed: enemy{Life(enemy)}/{saved.EnemyLife}, absorber{Life(absorber)}/{saved.AbsorberLife}, fraction{SkillHealth.Remainder256(absorber)}/17");
        Packets(owner,enemy);Absorb(owner,absorber);
        var lethal=Enemy(owner,3);lethal.Hits=2;SkillHealth.Damage(owner,lethal,256);Require(lethal.Alive && Life(lethal)==256,"Exactly one life should remain alive");
        SkillHealth.Damage(owner,lethal,1);Require(!lethal.Alive && lethal.Corpse is{Deleted:false} && SkillHealth.Remainder256(lethal)==0,"Reloaded one-point boundary did not become lethal below one life");lethal.Corpse?.Delete();lethal.Delete();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{EnemyLife=Life(enemy),AbsorberLife=Life(absorber)}));
    }
}
