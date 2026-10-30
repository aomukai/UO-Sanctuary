// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipmentDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentDamageChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,NpcPhysicalStats Stats,int First,int Last);
    private sealed record Saved(uint Owner,Stored[] Actors);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-damage-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-damage-check",Check);Register("npc-equipment-damage-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bound NPC physical damage and source composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-damage-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray(),o=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var stats=new NpcPhysicalStats(o[0],o[1],2,o[3],i[5],10,i[0]!=0,100,0,o[7],o[2]);
            int bound=0;int Roll(int size){bound=size;return i[8]==0?0:size-1;}
            Require(NpcEquipmentDamage.Roll256(stats,Roll,i[7])==o[5]&&bound==o[6],"Full source damage/roll bound differs at "+count);count++;
        }
        Require(count==3888,"Missing source item/damage compositions");
    }
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name).Id,99,new Random(317),true);
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var pet=new SkeletonGuardian{Name="NPC damage holder [prepared]"};pet.Initialize(owner,7,2);pet.ControlSlots=0;pet.NoKillAwards=true;
        Require(pet.SetControlMaster(owner),"Prepared ownership failed");pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(pet);pet.AIObject.AITimer.Stop();return pet;
    }
    private static Stored Bind(PlayerMobile owner,string name,bool two,bool weapon,int strength)
    {
        var actor=Pet(owner);var item=Named(name);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original binding failed: "+why);
        var stats=NpcEquipmentDamage.Capture(actor).Value;var raw=actor.SourceWeaponBase;
        Require(stats.Strength==strength&&stats.Weapon==weapon,"Actual item attribute or weapon route differs: "+name);
        var local=ClassGear.Damage(item,CreatureProfiles.Level(actor),two,true);
        if(weapon){Require(stats.Minimum==(two?0:raw.Min)+local.Min&&stats.Maximum==(two?0:raw.Max)+local.Max,"Weapon local endpoints or hand basis applied twice: "+name);}
        if(name=="The Scalper")
        {
            var thrown=ClassGear.Damage(item,CreatureProfiles.Level(actor),false,false);
            Require(local!=thrown&&stats.Minimum==raw.Min+local.Min,"NPC swing used the throwing weapon's missile damage");
        }
        int first=NpcEquipmentDamage.Roll256(stats,_=>0),last=NpcEquipmentDamage.Roll256(stats,n=>n-1);
        Require(first>0&&last>=first,"Invalid actual NPC physical range");
        var row=new Stored(actor.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),stats,first,last);
        var target=new Rat{Name="Equipment damage recipient",NoKillAwards=true};target.SetHits(20000);target.SetResistance(ResistanceType.Physical,0);
        target.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);target.AIObject.AITimer.Stop();
        try
        {
            int before=SkillHealth.Balance256(target);
            Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0)&&before-SkillHealth.Balance256(target)==first,"Bound equipment did not reach actual FP256 life loss");
            before=SkillHealth.Balance256(target);
            typeof(SkeletonGuardian).GetMethod("Strike",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(actor,[target]);
            int loss=before-SkillHealth.Balance256(target);Require(loss>=first&&loss<=last,"Actual summon Strike bypassed equipped physical damage");
        }
        finally{target.Delete();}
        if(weapon&&ClassItemTypes.Is(ClassGear.Base(item).Type,"blun"))
        {
            var undead=new Skeleton{NoKillAwards=true};undead.SetHits(20000);undead.SetResistance(ResistanceType.Physical,0);
            undead.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);undead.AIObject.AITimer.Stop();
            try
            {
                var againstUndead=NpcEquipmentDamage.Capture(actor,undead).Value;
                Require(againstUndead.DamagePercent==stats.DamagePercent+100,"Bonesnap needs source blunt50 plus its separate item undead50 bonus");
                int expected=NpcEquipmentDamage.Roll256(againstUndead,_=>0),before=SkillHealth.Balance256(undead);
                Require(NpcEquipmentDamage.TryResolve(actor,undead,out _,_=>0)&&before-SkillHealth.Balance256(undead)==expected,"NPC undead damage bonus did not reach actual health loss");
            }
            finally{undead.Delete();}
        }
        return row;
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentDamageOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var rows=new[]{Bind(owner,"The Atlantian",false,true,16),Bind(owner,"The Diggler",false,true,0),Bind(owner,"Bonesob",true,true,0),Bind(owner,"Duriel's Shell",false,false,15),Bind(owner,"The Scalper",false,true,0)};
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows)));PersistentMinions.Suspend(owner);owner.Internalize();Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&actor.ControlMaster?.Serial.Value==saved.Owner&&item.Parent==actor
                &&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original identity/rolls/parent");
            var stats=NpcEquipmentDamage.Capture(actor).Value;
            Require(stats==row.Stats&&NpcEquipmentDamage.Roll256(stats,_=>0)==row.First&&NpcEquipmentDamage.Roll256(stats,n=>n-1)==row.Last,"Reload changed exact item physical packet");
        }
        Report();
    }
    private static void Report()=>Log.Information("NPC equipment physical:3888complete older source compositions;actual one/two-handed and armor Strength routes,FP256 life loss and summon Strike;two reloads preserve original serial/rolls/parent/packet;elements,critical and procs remain unfinished");
}
