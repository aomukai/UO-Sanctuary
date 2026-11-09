// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PlayerCurrentReachChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlayerCurrentReachChecks));
    private sealed record Saved(uint Owner,uint Weapon,bool Female,int Base,int Reach);
    private sealed record Manifest(int Generation,Saved[] Owners);
    private static string FileName=>Path.Join(Core.BaseDirectory,"player-current-reach-fixtures.json");
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {bool again=reload;string name=again?"player-current-reach-reload-check":"player-current-reach-check";ConsoleInputHandler.RegisterCommand(name,"Current player reach and actual Warlock basic-attack boundary",_=>Core.LoopContext.Post(()=>
        {try{Check(again);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"player-current-reach-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");int I(string k)=>p.GetProperty(k).GetInt32();bool B(string k)=>p.GetProperty(k).GetBoolean();
            var trace=new List<string>();int result;
            try{result=SourcePlayerMeleeReach.Read(I("class"),()=>{trace.Add("inventory");trace.Add("select");
                if(B("found")&&B("item")&&I("kind")==4){trace.Add("row");}
                return new(B("found"),B("item")?I("kind"):null,B("row")?I("range"):null);});}
            catch(NotSupportedException){result=-1;}
            var expected=row.GetProperty("output");Require(result==expected.GetProperty("result").GetInt32()&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(r=>r.GetString())),"Current player reach differs at"+count);count++;
        }
        Require(count==60,"Missing current reach controls");
        Log.Information("Current player reach:60current contexts/401bytes;all eight classes,failed/null/nonitem selection,item-byte endpoints and captured class7bonus match. Native inventory eligibility remains supplied.");
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static (PlayerMobile Owner,Broadsword Weapon) Fresh(DiabloClass cls,bool female=false,int x=7040)
    {
        var p=ClassSkillChecks.Player(cls);p.Female=female;p.Body=female?0x191:0x190;p.AddItem(new Backpack());FixtureSpace.Corridor(p,x,24,3650);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Reach Strength");}
        while(p.RawDex<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Reach Dexterity");}
        var weapon=new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Reach weapon equip");ClassGear.Refresh(p);Ready(p);return(p,weapon);
    }
    private static void Boundary(PlayerMobile p,Broadsword weapon)
    {
        var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();enemy.SetHits(10000);SkillHealth.SetBalance256(enemy,enemy.HitsMax*256);
        try
        {
            int basis=PlayerMeleeGeometry.ForCode(ClassGear.Base(weapon).Code);bool found=false;var boundaries=new List<string>();
            foreach(var offset in Enumerable.Range(1,5).SelectMany(x=>Enumerable.Range(0,4),(x,y)=>new Point2D(x,y)).OrderBy(v=>v.X+v.Y))
            {
                enemy.MoveToWorld(new(p.X+offset.X,p.Y+offset.Y,p.Z),p.Map);
                bool legacy=PlayerMeleeGeometry.Within(p,enemy,1,basis);
                bool current=PlayerMeleeGeometry.Within(p,enemy,1);boundaries.Add($"{offset.X},{offset.Y}:{legacy}/{current}/{p.InLOS(enemy)}");
                if(!legacy&&current&&p.InLOS(enemy)){found=true;break;}
            }
            Require(found,"No native boundary distinguishes Warlock bonus:base="+basis+" reach="+PlayerMeleeGeometry.Reach(p)+" cells="+string.Join(",",boundaries));
            int hp=SkillHealth.Balance256(enemy);int attempts=0;
            while(SkillHealth.Balance256(enemy)==hp&&attempts++<16)
            {
                Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,enemy,enemy.Location,Core.TickCount),"Extended Warlock basic start");
                Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Extended Warlock basic release");
            }
            Require(SkillHealth.Balance256(enemy)<hp,"Actual basic attack never reached the current class-bonus boundary");
            enemy.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);Require(!PlayerMeleeGeometry.Within(p,enemy,1),"Far range control is not outside reach");hp=SkillHealth.Balance256(enemy);
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,enemy,enemy.Location,Core.TickCount),"Far whiff start");ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
            Require(SkillHealth.Balance256(enemy)==hp,"Far Warlock basic attack ignored exact release range");Ready(p);
            Log.Information("Current reach original:actual Warlock/Witch basic attack damages at a footprint boundary outside base reach;far release remains harmless. Existing source footprint/LOS and coarse input search are reused.");
        }
        finally{enemy.Delete();}
    }
    private static void Check(bool reload)
    {
        Source();Manifest manifest;
        if(!reload)
        {
            var saved=new List<Saved>();foreach(bool female in new[]{false,true})
            {
                var(p,weapon)=Fresh(DiabloClass.Warlock,female,female?7080:7040);int basis=PlayerMeleeGeometry.ForCode(ClassGear.Base(weapon).Code);
                Require(PlayerMeleeGeometry.Reach(p)==basis+2&&CombatTiming.Range(p,CombatAbility.BasicAttack)==PlayerMeleeGeometry.SearchRange(p),"Warlock class bonus missing in live adapter/input bound");
                Boundary(p,weapon);int wear=weapon.HitPoints;weapon.HitPoints=0;Require(PlayerMeleeGeometry.Reach(p)==0&&!PlayerMeleeGeometry.WarlockMelee(p),"Broken weapon gained intrinsic reach");weapon.HitPoints=wear;
                p.AddToBackpack(weapon);Require(PlayerMeleeGeometry.Reach(p)==0&&!PlayerMeleeGeometry.WarlockMelee(p),"Unarmed native adapter changed");Require(p.EquipItem(weapon),"Original sword reequip");
                var bow=new Bow();p.AddToBackpack(weapon);p.AddToBackpack(bow);Require(p.EquipItem(bow)&&!PlayerMeleeGeometry.WarlockMelee(p)&&CombatTiming.Range(p,CombatAbility.BasicAttack)==bow.MaxRange,"Ranged weapon entered melee override");
                p.AddToBackpack(bow);bow.Delete();Require(p.EquipItem(weapon),"Sword restore after bow");Ready(p);saved.Add(new(p.Serial.Value,weapon.Serial.Value,female,basis,basis+2));
            }
            var(classic,oldWeapon)=Fresh(DiabloClass.Paladin,x:7120);Require(PlayerMeleeGeometry.Reach(classic)==PlayerMeleeGeometry.ForCode(ClassGear.Base(oldWeapon).Code)
                &&CombatTiming.Range(classic,CombatAbility.BasicAttack)==oldWeapon.MaxRange&&!PlayerMeleeGeometry.WarlockMelee(classic),"Classic melee adapter/range changed");oldWeapon.Delete();classic.Delete();manifest=new(0,saved.ToArray());
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Unexpected reach reload");
            foreach(var s in old.Owners)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var weapon=(Broadsword)World.FindItem((Serial)s.Weapon);
                Require(p is{Alive:true,Deleted:false}&&p.Female==s.Female&&weapon.Parent==p&&PlayerMeleeGeometry.ForCode(ClassGear.Base(weapon).Code)==s.Base&&PlayerMeleeGeometry.Reach(p)==s.Reach,"Original equipped reach changed after reload");
                Log.Information("Current reach reload:original map={Map},position={Position};explicit fixture world reactivation precedes combat.",p.Map,p.Location);
                FixtureSpace.Corridor(p,s.Female?7080:7040,24,3650);Ready(p);Boundary(p,weapon);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Require(!WarlockCombat.Implemented(CombatAbility.Cleave),"Reach fix admitted unfinished Cleave");
    }
}
