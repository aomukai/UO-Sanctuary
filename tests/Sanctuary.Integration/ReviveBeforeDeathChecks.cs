// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class ReviveBeforeDeathChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveBeforeDeathChecks));
    private sealed record Saved(ReviveOriginalState Original,int Hits,int Mana,int Stamina,FrozenEquipmentSnapshot[] Equipment,uint[] Loot);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-before-death-fixtures.json");
    private static readonly string[] Codes=["str","dex","enr","vit","hp","mana","stam","att","att%","ac","ac%","dmg%","dmg-min","dmg-max","dmg-max%","res-all","res-fire","res-cold","res-ltng","res-pois","res-physical","swing1","swing2","swing3","cast1","move1","balance1","lifesteal","manasteal","allskills","hp%","mana%","fire-min","fire-max","cold-min","cold-max","ltng-min","ltng-max","pois-min","pois-max","regen","regen-mana","red-dmg","red-mag","block"];
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    public static void Configure(){Register("revive-before-death-check",Check);Register("revive-before-death-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Accepted-death resources,independent gear effects and native ability clocks",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string key,int seed)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(seed),true);
    private static void Compare(Item item,FrozenEquipmentSnapshot saved)
    {
        var frozen=FrozenEquipmentEffects.Bind(saved);
        for(int level=1;level<=99;level++)
        {
            var live=new ItemStatContext(item,level);var damage=live.WeaponDamage;var values=frozen.At(level);
            Require(values.Defense==live.Defense&&values.Minimum==damage.Min&&values.Maximum==damage.Max&&values.Life256==live.LifeBonus256&&values.Mana256==live.ManaBonus256,"Frozen local/resource supplier differs:"+level);
            foreach(string code in Codes){Require(frozen.Bonus(code,level)==live.Bonus(code),"Frozen numeric bonus differs:"+code+"/"+level);}
        }
        var gear=FrozenEquipmentEffects.Gear(saved);
        if(gear?.NamedProperties.Length>0)
        {var original=gear.NamedProperties[0];gear.NamedProperties[0]=original with{Value=original.Value+1};Require(FrozenEquipmentEffects.Gear(saved).NamedProperties[0]==original,"Decoded gear shares mutable saved property rolls");}
        foreach(int level in new[]{0,100}){bool refused=false;try{_=frozen.At(level);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Frozen recipient level silently clamped");}
    }
    private static Corpse Kill(PlayerMobile owner,BaseCreature actor)
    {
        Corpse corpse=null;var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(victim,container)=>{previous?.Invoke(victim,container);if(victim==actor){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}finally{ClassLootHooks.CreatureDied=previous;}
        Require(corpse is{Deleted:false},"Accepted death lost corpse");corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void AssertSaved(Saved saved)
    {
        var corpse=World.FindItem((Serial)saved.Original.Corpse) as Corpse;var actor=ReviveOriginals.Find(corpse);
        Require(actor is{Alive:false,Deleted:false}&&ReviveOriginals.Snapshot().Single(r=>r.Corpse==saved.Original.Corpse)==saved.Original,"Saved accepted-death record changed");
        var before=ReviveBeforeDeath.Decode(saved.Original.BeforeDeathJson);ReviveBeforeDeath.Validate(saved.Original.BeforeDeathJson,saved.Original.RuntimeType);
        Require(before.Mana==saved.Mana&&before.Stamina==saved.Stamina&&before.Hits==saved.Hits&&actor.Mana==0&&actor.Stam==0,$"Accepted-death boundary: snapshot={before.Hits}/{before.Mana}/{before.Stamina},observed={saved.Hits}/{saved.Mana}/{saved.Stamina},stored={actor.Hits}/{actor.Mana}/{actor.Stam}");
        var items=ReviveBeforeDeath.Equipment(before);Require(items.SequenceEqual(saved.Equipment),"Frozen original equipment changed");
        Require(corpse.Items.Select(i=>i.Serial.Value).Order().SequenceEqual(saved.Loot),"NPC state capture moved or duplicated loot");
        if(actor is Dragon)
        {
            var breath=actor.GetAbility(MonsterAbilityType.FireBreath);
            Require(ReviveBeforeDeath.Clocks(before).Any(c=>c.AbilityType==(int)MonsterAbilityType.FireBreath&&c.RemainingMilliseconds>0&&c.RemainingMilliseconds<=45000)
                &&breath.RemainingTriggerCooldown(actor)==0,"Accepted-death ability clock was lost or native death clearing changed");
        }
    }
    private static void Validate(Saved saved)
    {
        var before=ReviveOriginals.Snapshot();var row=saved.Original;var state=ReviveBeforeDeath.Decode(row.BeforeDeathJson);
        var badState=state with{Mana=state.MaximumMana+1};
        foreach(var bad in new[]{row with{BeforeDeathJson="{"},row with{BeforeDeathJson=JsonSerializer.Serialize(state with{Version=99})},
            row with{BeforeDeathJson=JsonSerializer.Serialize(badState)},row with{BeforeDeathIssue="conflict"},row with{BeforeDeathJson=JsonSerializer.Serialize(state with{EquipmentJson="null"})},
            row with{BeforeDeathJson=JsonSerializer.Serialize(state with{AbilityClocksJson="[{\"Path\":\"0\",\"RuntimeType\":\"x\",\"AbilityType\":1,\"RemainingMilliseconds\":-1}]"})}})
        {
            bool refused=false;try{ReviveOriginals.Restore([bad]);}catch(InvalidDataException){refused=true;}Require(refused&&ReviveOriginals.Snapshot().SequenceEqual(before),"Invalid accepted-death restore was not atomic");
        }
        foreach(var bad in new[]{saved.Equipment[0] with{Version=99},saved.Equipment[0] with{OriginalItem=0},saved.Equipment[0] with{LevelsJson="[]"},saved.Equipment[0] with{PropertiesJson="{"}})
        {bool refused=false;try{FrozenEquipmentEffects.Validate(bad);}catch(InvalidDataException){refused=true;}Require(refused,"Invalid frozen item effects accepted");}
        // Old v0 retained originals cannot invent pre-cleanup state.
        var legacy=row with{BeforeDeathJson=null,BeforeDeathIssue=null};ReviveOriginals.Restore([legacy]);Require(ReviveOriginals.Snapshot().Single().BeforeDeathJson==null,"Legacy retained original fabricated accepted-death state");ReviveOriginals.Restore(before);
    }
    private static void Check()
    {
        var owner=new PlayerMobile{Name="BeforeDeathOwner",Player=true,Body=0x190};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);FixtureSpace.Place(owner,new(6100,1690,0),Map.Trammel);
        // This negative control must have no source classification now that
        // classified Skeletons participate in automatic gameplay capture.
        var ordinary=new Cow();var ordinaryGear=new Broadsword();ClassGear.Profiles.FromMonster(ordinaryGear,99,GearRarity.Normal,new Random(99));ordinary.AddItem(ordinaryGear);ordinary.NoKillAwards=true;ordinary.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ordinary.AIObject?.AITimer.Stop();
        var ordinaryCorpse=Kill(owner,ordinary);Require(ordinary.Deleted&&ordinaryGear.Deleted&&ReviveOriginals.Find(ordinaryCorpse)==null,"Default noncaptured native loot cleanup changed");ordinaryCorpse.Delete();
        var saved=new List<Saved>();var beforeHook=ClassLootHooks.BeforeDeath;var retainHook=ClassLootHooks.RetainOriginal;
        var observed=new Dictionary<uint,(int Hits,int Mana,int Stam)>();
        ClassLootHooks.BeforeDeath=actor=>{observed[actor.Serial.Value]=(actor.Hits,actor.Mana,actor.Stam);ReviveBeforeDeath.Capture(actor);};ClassLootHooks.RetainOriginal=ReviveOriginals.Capture;
        try
        {
            for(int kind=0;kind<3;kind++)
            {
                BaseCreature actor=kind switch{0=>new Skeleton(),1=>new Dragon(),_=>new GraveyardZombie()};
                foreach(var item in actor.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
                var weapon=new Broadsword();var magic=ClassGear.Profiles.FromMonster(weapon,99,GearRarity.Magic,new Random(100+kind));ClassGear.Profiles.Store(weapon,magic with{Identified=true});
                if(kind==2)
                {
                    var state=ClassGear.State(weapon);var family=SocketGems.Family("Ruby");
                    ClassGear.Profiles.Store(weapon,state with{TinkerSockets=1,Gems=[new("Ruby",GemQuality.Standard,family.Effects[(int)GemQuality.Standard])]});
                }
                var armor=Named(kind==1?"Spiritforge":"Duriel's Shell",200+kind);
                actor.AddItem(weapon);actor.AddItem(armor);actor.NoKillAwards=true;actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject?.AITimer.Stop();
                actor.Mana=Math.Max(1,actor.ManaMax/2);actor.Stam=Math.Max(1,actor.StamMax/2);int mana=actor.Mana,stamina=actor.Stam;Require(mana>0&&stamina>0,"Native resource fixture unavailable");
                if(actor is Dragon){actor.GetAbility(MonsterAbilityType.FireBreath).Trigger(MonsterAbilityTrigger.CombatAction,actor,owner);}
                var expected=new[]{FrozenEquipmentEffects.Capture(weapon),FrozenEquipmentEffects.Capture(armor)};
                Compare(weapon,expected[0]);Compare(armor,expected[1]);
                var corpse=Kill(owner,actor);var row=ReviveOriginals.Snapshot().Single(r=>r.Actor==actor.Serial.Value);Require(row.BeforeDeathJson!=null&&row.BeforeDeathIssue==null,"Accepted-death capture unavailable:"+row.BeforeDeathIssue);
                var actual=ReviveBeforeDeath.Equipment(ReviveBeforeDeath.Decode(row.BeforeDeathJson));Require(actual.Length==2&&actual.OrderBy(i=>i.OriginalItem).SequenceEqual(expected.OrderBy(i=>i.OriginalItem)),"Death capture changed original item effects");
                Require(weapon.Parent==corpse&&armor.Parent==corpse,$"Native loot placement: kind={kind},weapon={weapon.Parent?.GetType().FullName},armor={armor.Parent?.GetType().FullName},weaponDeleted={weapon.Deleted},armorDeleted={armor.Deleted},weaponMovable={weapon.Movable},armorMovable={armor.Movable},weaponLoot={weapon.LootType},armorLoot={armor.LootType},keeps={actor.KeepsItemsOnDeath},items={corpse.Items.Count}");
                // Loot can be acquired and destroyed independently of captured effects.
                owner.AddToBackpack(weapon);var frozen=FrozenEquipmentEffects.Bind(expected[0]);var values=frozen.At(37);weapon.Delete();
                Require(frozen.At(37)==values&&FrozenEquipmentEffects.Bind(expected[0]).At(37)==values,"Destroyed original loot invalidated captured combat effects");
                var accepted=observed[actor.Serial.Value];var record=new Saved(row,accepted.Hits,accepted.Mana,accepted.Stam,actual,corpse.Items.Select(i=>i.Serial.Value).Order().ToArray());AssertSaved(record);Validate(record);saved.Add(record);
            }
        }
        finally{ClassLootHooks.BeforeDeath=beforeHook;ClassLootHooks.RetainOriginal=retainHook;}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));owner.Delete();Report();
    }
    private static void Reload()
    {var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(saved.Length==3,"Missing accepted-death fixtures");foreach(var row in saved){AssertSaved(row);Validate(row);}Report();}
    private static void Report()
    {
        Log.Information("Revive before death:3actual deaths/6equipped items retain pre-cleanup mana/stamina,independent gear rolls and native ability clock data;99recipient levels/45bonus codes match existing suppliers,loot stays lootable and deleted loot cannot alter frozen effects;10invalid payloads per corpse and old-v0 compatibility pass;complete procs/custom-timer parity is outside this capture scope");
    }
}
