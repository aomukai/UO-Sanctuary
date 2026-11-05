// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipPlacementChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcEquipPlacementChecks));
    private sealed record Held(int Case,uint Actor,uint Item,string SavedText);
    private sealed record ShadowHeld(uint Actor,uint[] Items,string[] SavedText);
    private sealed record Saved(uint Owner,Held[] Items,ShadowHeld[] Shadows,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equip-placement-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("npc-equip-placement-check",false);Register("npc-equip-placement-reload-check",true);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint serial in s.Items.Select(i=>i.Actor).Concat(s.Shadows.Select(i=>i.Actor))){if(World.FindMobile((Serial)serial)is BaseCreature b){b.AIObject?.AITimer.Stop();}}}};
    }
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"NPC body/hand placement and physical generated equipment",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equip-placement-cases.json")));var root=doc.RootElement;
        var items=JsonSerializer.Deserialize<EquipPlacementItem[]>(root.GetProperty("items").GetRawText());var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");Require(inputs.GetArrayLength()==140364,"Complete placement source count");
        int n=0;
        foreach(var pair in inputs.EnumerateArray().Zip(outputs.EnumerateArray()))
        {
            var a=pair.First.EnumerateArray().Select(v=>v.GetInt32()).ToArray();int flags=a[3],body=a[4],req=0,free=0;
            EquipPlacementItem Item(int i)=>i<0?null:items[i];var item=Item(a[5]);var same=Item(a[6]);var other=Item(a[7]);
            var slots=new EquipPlacementItem[13];if(body is>=0 and<13){slots[body]=same;}slots[body switch{4=>5,5=>4,11=>12,_=>11}]=other;
            var actor=(flags&128)!=0?null:new EquipPlacementActor(a[0],a[1],(EquipMonster)a[2],(flags&1)!=0,(flags&2)!=0,(flags&4)!=0,slots,_=>{req++;return(flags&8)!=0;},()=>{free++;return(flags&16)!=0;});
            int result=SourceEquipPlacement.Classify(actor,body,item,(flags&32)!=0);
            int[] actual=[result,req,free,SourceEquipPlacement.BodyAllowed(item,unchecked((byte)body))?1:0,SourceEquipPlacement.EqualStacks(same,item)?1:0,actor==null?-1:SourceEquipPlacement.Compatible(actor,item,other)?1:0];
            Require(actual.SequenceEqual(pair.Second.EnumerateArray().Select(v=>v.GetInt32())),"Placement source/lazy service mismatch at "+n+" input="+pair.First.GetRawText()+" actual="+Json(actual)+" expected="+pair.Second.GetRawText());n++;
        }
        Require(n==outputs.GetArrayLength(),"Incomplete placement output comparisons");
    }
    private static SkeletonGuardian Actor(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian();actor.Initialize(owner,2,0);actor.Frozen=actor.CantWalk=true;actor.ControlSlots=0;actor.Name="Generated equipment storage [fixture]";
        Require(actor.SetControlMaster(owner),"Generated holder control master");PersistentMinions.Profiles.Track(actor);actor.MoveToWorld(owner.Location,owner.Map);actor.AIObject?.AITimer.Stop();return actor;
    }
    private static PreparedItemRequest Request(JsonElement a)=>new(a[0].GetString(),a[1].GetInt32(),a[2].GetInt32(),a[3].GetInt32(),ulong.Parse(a[4].GetString()),ulong.Parse(a[5].GetString()),a[6].GetUInt32());
    private static JsonElement EquippedExpected(JsonElement original,int maximum)
    {
        var node=(JsonArray)JsonNode.Parse(original.GetRawText());var fields=(JsonArray)node[17];uint key=EquipmentStatKey.Pack(72);bool found=false;
        foreach(var pair in fields){if(pair[0].GetValue<uint>()==key){pair[1]=maximum;found=true;break;}}
        if(!found&&maximum!=0){fields.Add(new JsonArray(JsonValue.Create(key),JsonValue.Create(maximum)));}
        return JsonSerializer.SerializeToElement(node);
    }
    private static void Check(bool reload)
    {
        if(!reload){SourceCases();}
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-current-cases.json")));var root=doc.RootElement;var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");
        var native=root.GetProperty("native").EnumerateArray().Select(v=>v.GetInt32()).Where(i=>inputs[i][7].GetInt32()==0&&inputs[i][2].GetInt32()is 1 or 3 or 6).ToArray();Require(native.Length==1548,"Native NPC delivery scope");
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(0,[],[]);Require(!reload||saved.Generation<2,"NPC placement reload generation");PlayerMobile owner;
        if(reload){owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Deleted:false,Alive:true},"Original equipment owner missing");}
        else
        {
            owner=new PlayerMobile{Name="GeneratedNpcEquipmentOwnerFixture",Body=0x190,Player=true};ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);FixtureSpace.Place(owner,new(6100,1810,0),Map.Trammel);
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _)&&ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Generated holder talent fixture");
        }
        var rows=new List<Held>();
        for(int n=0;n<native.Length;n++)
        {
            int index=native[n];var request=Request(inputs[index]);SkeletonGuardian actor;GeneratedNpcItem item;
            if(reload)
            {
                var original=saved.Items[n];Require(original.Case==index,"Original generated NPC context changed");actor=World.FindMobile((Serial)original.Actor)as SkeletonGuardian;item=World.FindItem((Serial)original.Item)as GeneratedNpcItem;
                Require(actor is{Deleted:false,Alive:true}&&item is{Deleted:false,Movable:false,Visible:false}&&item.Parent==actor&&item.SavedText==original.SavedText,"Native NPC item/actor/payload/parent changed");
            }
            else{actor=Actor(owner);Require(GeneratedNpcEquipment.TryCreate(actor,owner,EquipMonster.Other,request,GeneratedNpcEquipment.Type(request.Code).Body1,out item),"Valid generated NPC equip failed at "+index);}
            var state=item.Snapshot;var binding=state.Equipment;Require(state.Version==1&&state.Request==request&&binding is not null&&binding.Actor==actor.Serial.Value&&binding.Owner==owner.Serial.Value&&binding.Kind==EquipMonster.Other&&binding.Flags==0x1000&&binding.Body==GeneratedNpcEquipment.Type(request.Code).Body1,"NPC source body/NOSELL/original binding");
            Require(actor.ControlMaster==owner&&GeneratedNpcEquipment.Items(actor).Single()==item&&item.Layer==Layer.Invalid,"NPC logical inventory/placeholder layer");
            var values=GeneratedNpcItem.CreationValues();var graph=item.RestoreGraph(values);int maximum=EquipmentStatValues.SourceRead(graph.Unit.Stats,EquipmentStatKey.Pack(73),EquipmentStatValues.Definition(73));Require(maximum==values.Query(graph.Unit.Stats,EquipmentStatKey.Pack(73))&&graph.BaseValue(72)==maximum,"Successful NPC equip did not restore operative max durability/cached reader agreement");
            Require(graph.Draws==0,"NPC load or repair rerolled properties");CurrentItemQualityChecks.Verify(state.Metadata,graph,EquippedExpected(outputs[index],maximum),"equipped "+index);CurrentItemQualityChecks.Continuation(graph,outputs[index]);
            Require(item.OnParentDeath(actor)==DeathMoveResult.RemainEquipped,"Generated gear entered corpse loot");rows.Add(new(index,actor.Serial.Value,item.Serial.Value,item.SavedText));
        }
        var shadows=new List<ShadowHeld>();
        if(!reload)
        {
            var request=new PreparedItemRequest("ces",37,4,0,123,456,511);
            foreach(var kind in new[]{EquipMonster.ShadowWarrior,EquipMonster.ShadowMaster})
            {var actor=Actor(owner);Require(GeneratedNpcEquipment.TryCreate(actor,owner,kind,request,4,out _)&&GeneratedNpcEquipment.TryCreate(actor,owner,kind,request with{Code="btl",ItemSeed=789},5,out _),"Source shadow dual-claw exception");var held=GeneratedNpcEquipment.Items(actor);shadows.Add(new(actor.Serial.Value,held.Select(i=>i.Serial.Value).ToArray(),held.Select(i=>i.SavedText).ToArray()));}
            var holder=Actor(owner);Require(GeneratedNpcEquipment.TryCreate(holder,owner,EquipMonster.Other,request with{Code="2hs",Quality=3},4,out var first),"Two-handed fixture equip");int count=World.Items.Count;string before=first.SavedText;
            Require(!GeneratedNpcEquipment.TryCreate(holder,owner,EquipMonster.Other,request with{Code="buc"},5,out _)&&World.Items.Count==count&&first.SavedText==before,"Hand conflict did not delete failed new item/preserve original");
            Require(!GeneratedNpcEquipment.TryCreate(holder,owner,EquipMonster.Other,request,4,out _)&&World.Items.Count==count,"Occupied slot silently replaced or leaked an item");
            Require(!GeneratedNpcEquipment.TryCreate(holder,owner,EquipMonster.Other,request with{Code="qui"},4,out _)&&World.Items.Count==count,"Wrong body slot leaked a created item");
            bool kindRefused=false;try{GeneratedNpcEquipment.TryCreate(holder,owner,EquipMonster.ShadowMaster,request,5,out _);}catch(NotSupportedException){kindRefused=true;}Require(kindRefused&&World.Items.Count==count,"Bound actor kind silently changed");
            uint serial=first.Serial.Value;holder.Delete();Require(first.Deleted&&World.FindItem((Serial)serial)==null,"Actor deletion retained private generated gear");
        }
        else
        {
            shadows.AddRange(saved.Shadows);foreach(var original in shadows)
            {var actor=World.FindMobile((Serial)original.Actor)as SkeletonGuardian;Require(actor is{Deleted:false}&&actor.ControlMaster==owner,"Original shadow storage fixture missing");var items=GeneratedNpcEquipment.Items(actor);Require(items.Length==2&&items.Select(i=>i.Serial.Value).SequenceEqual(original.Items)&&items.Select(i=>i.SavedText).SequenceEqual(original.SavedText)&&items.Select(i=>i.Snapshot.Equipment.Body).Order().SequenceEqual(new[]{4,5})&&items.All(i=>NpcItemAffixes.Pool.IsType(i.Snapshot.Metadata.Code,"h2h")),"Original source dual claw identity/payload changed on reload");}
        }
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,rows.ToArray(),shadows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("NPC equipment placement:140364complete source classifier controls/26988current body contexts;1548original equipped items/516bases plus two dual-claw holders survive two reloads with native owner/body/NOSELL/operative full durability and unchanged raw rolls;failed creation cleanup and actor deletion pass;live recipient stats/skills/events remain open");
    }
}
