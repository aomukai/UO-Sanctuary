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

public static class WeaponCrushingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponCrushingChecks));
    private sealed record Saved(uint Owner,uint Boots,uint Target,int Life);
    private sealed class Draw(int value):Random
    {public int Calls;public override int Next(int maximum){Calls++;return value;}}
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-crushing-fixtures.json");
    public static void Configure(){Freeze();Register("weapon-crushing-check",Check);Register("weapon-crushing-reload-check",Reload);}
    [CallPriority(int.MaxValue)] public static void Initialize()=>Freeze();
    private static void Freeze()
    {var hits=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=actor=>_creating || actor.Name=="WeaponCrushingTarget"?TimeSpan.FromHours(1):hits?.Invoke(actor)??Mobile.DefaultHitsRate;}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fractional shared Crushing Blow",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-crushing-source-probe.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            int hp=row[0].GetInt32(),flags=row[1].GetInt32(),boss=row[2].GetInt32(),ranged=row[3].GetInt32(),res=row[4].GetInt32(),bonus=row[5].GetInt32(),roll=row[6].GetInt32();
            int loss=roll<50?NamedCombat.CrushingLoss256(hp,boss!=0 || (flags&2)!=0?8:4,res,bonus,ranged!=0):0;
            Require(hp-loss==row[7].GetInt32(),"Compiled FP256 CB reduction differential");count++;
        }
        Require(count==1152,"Incomplete Crushing Blow source fixture");
        Require(NamedCombat.CrushingLoss256(257,4,33)==43,"Separately truncated resistance share");
        Require(NamedCombat.CrushingLoss256(257,4,-50)==64 && NamedCombat.CrushingLoss256(257,4,100)==0,"Negative resistance/immunity");
        Log.Information("Crushing Blow:1152 compiled source rows matched shared fractional arithmetic; source classification supplied explicitly");
    }
    private static BaseCreature Enemy(bool skeleton=false)
    {
        BaseCreature target;_creating=true;try{target=skeleton?new GraveyardSkeleton():new GraveyardZombie();target.Name="WeaponCrushingTarget";}finally{_creating=false;}
        target.Frozen=true;target.CantWalk=true;target.NoKillAwards=true;target.AIObject.AITimer.Stop();target.SetHits(5000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Physical,33);return target;
    }
    private static void Check()
    {
        Differential();Projectile();var owner=new PlayerMobile{Body=0x190,Name="WeaponCrushingOwner",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Paladin);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        var boots=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goblin Toe").Id,99,new Random(317),true);
        owner.Backpack.DropItem(boots);Require(owner.EquipItem(boots),"Actual Goblin Toe equip");ClassGear.Refresh(owner);Require(ClassGear.Bonus(owner,"crush")==25,"Pinned Goblin Toe CB25");
        var target=Enemy();
        SkillHealth.SetBalance256(target,1025);var draw=new Draw(24);NamedCombat.Crushing(owner,target,false,draw);
        Require(draw.Calls==1 && SkillHealth.Balance256(target)==853 && SkillHealth.Remainder256(target)==171,"Actual CB must preserve sub-HP life without an additional weapon damage event");
        int life=SkillHealth.Balance256(target);draw=new(25);NamedCombat.Crushing(owner,target,false,draw);Require(draw.Calls==1 && SkillHealth.Balance256(target)==life,"Exact chance boundary");
        owner.Backpack.DropItem(boots);ClassGear.Refresh(owner);draw=new(0);NamedCombat.Crushing(owner,target,false,draw);Require(draw.Calls==0 && SkillHealth.Balance256(target)==life,"Absent current gear must not roll");Require(owner.EquipItem(boots),"Re-equip crushing item");ClassGear.Refresh(owner);
        SkillHealth.SetBalance256(target,1025);draw=new(0);NamedCombat.Crushing(owner,target,true,draw);Require(SkillHealth.Balance256(target)==939,"Ranged halves before positive resistance subtraction");
        foreach(var grade in new[]{EncounterGrade.Ordinary,EncounterGrade.Champion,EncounterGrade.Unique,EncounterGrade.SuperUnique})
        {
            var elite=new EncounterCreature();try
            {
                elite.Assign(elite.Dungeon,60,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(317));elite.SetHits(5000);elite.Hits=elite.HitsMax;elite.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(elite,1025);
                int expected=grade==EncounterGrade.Ordinary?256:128;
                Require(NamedCombat.CrushingLoss256(elite,false)==expected,"Published grouping retained; legacy Champion/Unique discrepancy must remain explicit");
            }
            finally{elite.Delete();}
        }
        target.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(target,257);NamedCombat.Crushing(owner,target,false,new Draw(0));
        Require(target.Alive && SkillHealth.Balance256(target)==193,"Direct source CB life write must retain fractional below-one life until normal weapon damage");
        // Existing health serializer is reused, with no new gameplay persistence type.
        target.SetResistance(ResistanceType.Physical,33);SkillHealth.SetBalance256(target,1025);NamedCombat.Crushing(owner,target,false,new Draw(0));
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,boots.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target))));
    }
    private static void Projectile()
    {
        var owner=new PlayerMobile{Body=0x190,Name="WeaponCrushingOwner",Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Projectile strength");}
        owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2200;x<=2500 && !found;x+=50)for(int y=1000;y<=1300 && !found;y+=50)
        {Point3D point;try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}if(Server.Spells.SpellHelper.IsTown(point,owner)){continue;}
            bool empty=true;foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){empty=false;break;}}if(empty){owner.MoveToWorld(point,owner.Map);found=true;}}
        Require(found,"No independent crushing corridor");var boots=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goblin Toe").Id,99,new Random(317),true);owner.Backpack.DropItem(boots);Require(owner.EquipItem(boots),"Projectile Goblin Toe");ClassGear.Refresh(owner);
        var oldEffects=WeaponMissiles.ItemEffects;var oldHit=WeaponMissiles.HitRoll;var oldDamage=WeaponMissiles.DamageRoll;var oldBlock=SourceBlocking.BlockRoll;
        try
        {
            WeaponMissiles.HitRoll=()=>0;WeaponMissiles.DamageRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            foreach(string mode in new[]{"central","outer","removed","added","miss","blocked","immune","leech"})
            {
                var target=Enemy(mode=="blocked");try
                {
                    target.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);target.SetResistance(ResistanceType.Physical,mode=="immune"?100:0);SkillHealth.SetBalance256(target,1025);
                    SkillHealth.SetBalance256(owner,10*256);var draw=new Draw(0);WeaponMissiles.ItemEffects=draw;
                    if(mode=="added"){owner.Backpack.DropItem(boots);ClassGear.Refresh(owner);}
                    int physical=mode=="leech"?12800:mode=="blocked"?256:0;
                    var payload=new WeaponMissileDamage(physical,physical,0,0,0,false,new MissileSourceStats(LifeDrain:mode=="leech"?100:0));
                    long id=WeaponMissiles.Launch(owner,CombatAbility.BasicAttack,new(20,0,mode=="outer"?2080u|0x10000:2080u,1),payload,WeaponMissiles.NewCast());Require(id>0,"Projectile crushing launch");
                    if(mode=="removed"){owner.Backpack.DropItem(boots);ClassGear.Refresh(owner);}
                    if(mode=="added"){Require(owner.EquipItem(boots),"Live added crushing item");ClassGear.Refresh(owner);}
                    if(mode=="miss"){WeaponMissiles.HitRoll=()=>1;}
                    if(mode=="blocked")
                    {SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new(target.Serial.Value,CreatureProfiles.Definition(target).Reference,1)).ToArray());Require(SourceBlocking.TryRate(target,out int rate,out _) && rate>0,"Source crushing shield");SourceBlocking.BlockRoll=_=>0;}
                    for(int i=0;i<100 && WeaponMissiles.State(id) is{} flight;i++){WeaponMissiles.Tick(flight.NextUtc);}
                    int expected=mode is "outer" or "removed" or "miss" or "blocked" or "immune"?1025:897;
                    if(mode=="leech"){Require(SkillHealth.Balance256(owner)==10*256+897 && !target.Alive,"CB must reduce victim life before capped captured leech");}
                    else{Require(SkillHealth.Balance256(target)==expected,"Central/outer/live-gear/miss/immune CB projectile "+mode);}
                    Require(draw.Calls==(mode is "outer" or "removed" or "miss" or "blocked"?0:1),"Exactly one eligible central callback roll "+mode);
                    WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;if(boots.Parent!=owner){Require(owner.EquipItem(boots),"Restore crushing boots");ClassGear.Refresh(owner);}
                }
                finally{target.Corpse?.Delete();target.Delete();}
            }
        }
        finally{WeaponMissiles.ItemEffects=oldEffects;WeaponMissiles.HitRoll=oldHit;WeaponMissiles.DamageRoll=oldDamage;SourceBlocking.BlockRoll=oldBlock;WeaponMissiles.Cancel(owner);owner.Delete();}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var target=World.FindMobile((Serial)saved.Target) as BaseCreature;var boots=World.FindItem((Serial)saved.Boots);
        Require(owner is{Alive:true,Deleted:false} && target is{Alive:true,Deleted:false} && boots?.Parent==owner,"Saved CB actors/equipped item");
        target.Frozen=true;target.CantWalk=true;target.AIObject.AITimer.Stop();Require(SkillHealth.Balance256(target)==saved.Life && saved.Life==853,"Saved fractional CB life");
        ClassGear.Refresh(owner);int expected=NamedCombat.CrushingLoss256(target,true);var draw=new Draw(0);NamedCombat.Crushing(owner,target,true,draw);
        Require(draw.Calls==1 && SkillHealth.Balance256(target)==saved.Life-expected,"Shared CB after actual reload");
        SkillHealth.SetBalance256(target,saved.Life); // Inspection fixture retained for second reload.
    }
}
