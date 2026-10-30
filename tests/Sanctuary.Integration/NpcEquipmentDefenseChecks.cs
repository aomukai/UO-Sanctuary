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

public static class NpcEquipmentDefenseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentDefenseChecks));
    private sealed record Saved(uint Owner,uint Item,string Gear,FrozenEquipmentSnapshot Frozen);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-defense-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-defense-check",Check);Register("npc-equipment-defense-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Non-armor flat defense and prepared equipment graph",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static EquipmentStatValue[] Stats(params (int Id,int Value)[] rows)=>rows.Select(x=>new EquipmentStatValue(EquipmentStatKey.Pack(x.Id),x.Value)).OrderBy(x=>x.Packed).ToArray();
    private static void SourceCases()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-defense-source-cases.json")));int count=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int[] input=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var recipient=new EquipmentStatUnit(new object(),input[0]);var item=new EquipmentStatUnit(new object(),4);
            recipient.Stats=new(){Owner=recipient,OwnerKind=input[0],Flags=EquipmentStatNode.Extended,BaseValues=Stats((2,input[1]),(31,input[2])),FullValues=Stats((2,input[1]),(31,input[2]))};
            item.Stats=new(){Owner=item,OwnerKind=4,Flags=EquipmentStatNode.Extended,BaseValues=Stats((31,input[3])),FullValues=Stats((31,input[3]))};
            var properties=new EquipmentStatNode{Flags=0x40,OwnerKind=4,BaseValues=Stats((31,input[4]),(16,input[5]))};
            var values=new EquipmentStatValues(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});
            EquipmentStatGraph.Post(item,properties,true,values);EquipmentStatGraph.Merge(recipient,item,true,values);
            int Read(EquipmentStatUnit unit,int id)=>EquipmentStatValues.SourceRead(unit.Stats,EquipmentStatKey.Pack(id),EquipmentStatValues.Definition(id));
            int local=Read(item,31),percent=Read(recipient,16),basis=input[1]/4+Read(recipient,31);
            int total=basis+(basis<=0?basis*percent/-100:basis*percent/100);
            int[] expected=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            Require(expected.SequenceEqual(new[]{local,percent,total}),"Complete source equipment/defense composition case "+count);count++;
        }
        Require(count==648,"Incomplete source equipment defense contexts");
    }
    private static Item Create(PlayerMobile owner,bool identified=true)
    {
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Ribcracker");
        Require(NamedGear.Available(definition.Id),"Actual Ribcracker is unavailable");
        var item=NamedGear.Create(definition.Id,99,new Random(317),identified);owner.Backpack.DropItem(item);return item;
    }
    private static void Defense(PlayerMobile owner,Item item,int expected)
    {
        Require(ClassGear.Base(item).MaxDefense==0&&ClassGear.Defense(item)==expected,"Actual non-armor defense supplier");
        foreach(int level in new[]{1,37,99}){Require(new ItemStatContext(item,level).Defense==expected,"Explicit NPC recipient-level defense supplier");}
        Require(ClassStats.Defense(owner)==owner.Dex/4+expected,"Actual equipped player defense did not consume the shared supplier");
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="EquipmentDefenseOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        var item=Create(owner);Require(owner.EquipItem(item),"Cannot equip actual Ribcracker");ClassGear.Refresh(owner);Defense(owner,item,100);
        var weapon=(BaseWeapon)item;int durability=weapon.HitPoints;weapon.HitPoints=0;ClassGear.Refresh(owner);Defense(owner,item,0);
        weapon.HitPoints=durability;ClassGear.Refresh(owner);Defense(owner,item,100);
        var frozen=FrozenEquipmentEffects.Capture(item);
        owner.Backpack.DropItem(item);ClassGear.Refresh(owner);Require(ClassStats.Defense(owner)==owner.Dex/4,"Unequipped flat defense remained active");
        var unidentified=Create(owner,false);Require(ClassGear.Defense(unidentified)==0,"Unidentified flat defense leaked");unidentified.Delete();
        var detached=Create(owner);var copy=FrozenEquipmentEffects.Capture(detached);detached.Delete();
        Require(FrozenEquipmentEffects.Bind(copy).At(37).Defense==100,"Deleting original loot lost its frozen numeric defense");
        Require(owner.EquipItem(item),"Reequip actual Ribcracker");ClassGear.Refresh(owner);Defense(owner,item,100);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,item.Serial.Value,JsonSerializer.Serialize(ClassGear.State(item)),frozen)));
        owner.Internalize();Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var item=World.FindItem((Serial)saved.Item);
        Require(item is{Deleted:false}&&item.Parent==owner&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear,"Actual reload changed original identity/parent/rolls");
        Defense(owner,item,100);Require(FrozenEquipmentEffects.Bind(saved.Frozen).At(37).Defense==100,"Actual reload lost independent frozen defense");Report();
    }
    private static void Report()=>Log.Information("Equipment defense:648complete older-source composition contexts;actual Ribcracker flat100 reaches equip/repair/removal and recipient-level/frozen readers,with original rolls/payload persistence;full NPC consumers/events remain unfinished");
}
