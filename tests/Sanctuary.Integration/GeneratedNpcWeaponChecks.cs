// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GeneratedNpcWeaponChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcWeaponChecks));
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads);
    private sealed record Saved(uint Owner,Held[] Holders,int Generation);
    private sealed record Unit(int Id);
    private sealed class Gates(int[] a):SourceWeaponServices<Unit>
    {
        internal readonly Unit Actor=new(0),First=new(1),Second=new(2);
        internal int Active=a[4],Calls,Trace;
        private Unit Hand(int id)=>(a[1]&(1<<(id-1)))!=0?id==1?First:Second:null;
        internal override bool Inventory(Unit u)=>true;
        internal override bool Dual(Unit u)=>SourceItemStatUpdates.CanDualWield(a[0]<3?0:a[0]<5?1:4,a[0]switch{0=>6,1=>4,3=>1,4=>2,_=>0});
        internal override Unit Equipped(Unit u)=>Hand(a[2]);internal override Unit Primary(Unit u)=>Hand(a[2]);internal override Unit Secondary(Unit u)=>Hand(3-a[2]);
        internal override Unit Composite(Unit u,int part)=>Hand(part==5?1:2);
        internal override bool Skill(Unit u)=>true;internal override bool SkillRow(Unit u)=>true;internal override int Selection(Unit u)=>a[6];
        internal override int SequenceFrame(Unit u)=>a[7]<<8;internal override uint SkillFlags(Unit u)=>0;
        internal override bool Weapon(Unit u)=>u!=null;internal override bool Equippable(Unit u)=>u!=null;
        internal override Unit Owner(Unit u)=>u!=null&&(a[3]&(1<<(u.Id-1)))!=0?Actor:null;
        internal override void Merge(Unit u,Unit item,bool damage)
        {int bit=1<<(item.Id-1);Active=damage?Active|bit:Active&~bit;Calls++;Trace=Trace*10+item.Id*2+(damage?1:0);}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        foreach(string command in new[]{"generated-npc-weapons-check","generated-npc-weapons-reload-check"})
        {ConsoleInputHandler.RegisterCommand(command,"Original generated item selected weapon damage lists",_=>Core.LoopContext.Post(()=>
            {try{Check(command.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));}
    }
    private static void SelectorCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-weapon-selector-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var s=new Gates(a);var selected=SourceWeaponSelection.Select(s,s.Actor);SourceWeaponSelection.Enter(s,s.Actor,a[5]!=0);
            Require((selected?.Id??0)==a[8]&&s.Active==a[9]&&s.Calls==a[10]&&s.Trace==a[11],"Complete source hand-selector/entry control "+count);count++;
        }
        Require(count==12288,"Missing shared kick selector controls");
    }
    private static EquipmentStatValue[] Basis(int level)=>new[]{(0,100),(1,20),(2,50),(3,30),(7,256000),(9,76800),(11,25600),(12,level),(21,7),(22,14),(31,80)}
        .Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static void Check(bool reload)
    {
        SelectorCases();using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-weapons-cases.json")));var root=doc.RootElement;
        int equippable=0;foreach(var a in root.GetProperty("equippable").EnumerateArray())
        {Require(SourceWeaponSelection.Equippable(a[0].GetInt32()!=0,a[1].GetInt32(),a[2].GetInt32()!=0,a[3].GetUInt32())==(a[4].GetInt32()!=0),"Source item equip flags "+equippable);equippable++;}Require(equippable==60,"Missing equip predicate controls");
        string manifest=Path.Join(Core.BaseDirectory,"generated-npc-weapons-fixtures.json");var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-stats-fixtures.json")));
        if(reload){var prior=JsonSerializer.Deserialize<Saved>(File.ReadAllText(manifest));Require(saved.Generation==prior.Generation+1&&saved.Owner==prior.Owner&&Json(saved.Holders)==Json(prior.Holders),"Weapon original native identity/payload witness changed");}
        var hands=root.GetProperty("hands").EnumerateArray().ToArray();var outputs=root.GetProperty("numericOutputs").EnumerateArray().ToArray();Require(hands.Length==3276&&outputs.Length==3276,"Missing hand graph compositions");
        for(int n=0;n<hands.Length;n++)
        {
            var a=hands[n];int shape=a.GetProperty("Case").GetInt32();var input=root.GetProperty("numericInputs")[shape];var held=saved.Holders[shape];var ids=held.Items;
            var actor=World.FindMobile((Serial)held.Actor)as BaseCreature;Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner,"Original weapon recipient lost");
            var native=BattleOrders.Capture(actor);var changes=new List<int[]>();
            int Id(EquipmentStatUnit unit)=>unit==null?0:unit.Entity==actor?1:unit.Entity is GeneratedNpcItem item?Array.IndexOf(ids,item.Serial.Value)+2:-1;
            var graph=new GeneratedNpcStatGraph(actor,Basis(input.GetProperty("Level").GetInt32()),_=>input.GetProperty("Clock").GetInt32(),
                (node,source,key,before,after)=>changes.Add([Id(node.Owner),Id(source),(int)key,before,after]),true,_=>{},_=>{});
            int primary=a.GetProperty("Primary").GetInt32(),secondary=a.GetProperty("Secondary").GetInt32(),selection=a.GetProperty("Selection").GetInt32(),frame=a.GetProperty("Frame").GetInt32();
            uint flags=a.GetProperty("Flags").GetUInt32();bool used=a.GetProperty("Skill").GetBoolean(),row=a.GetProperty("Row").GetBoolean(),reset=a.GetProperty("Reset").GetInt32()!=0;
            var weapon=graph.Weapons(primary==0?0:ids[primary-1],secondary==0?0:ids[secondary-1],used,row,selection,frame,flags);
            void Compare(int stage)
            {
                var expected=outputs[n][0][stage];uint selected=weapon.Selected;int expectedId=expected[0].GetInt32();
                Require((selected==0?0:Array.IndexOf(ids,selected)+2)==expectedId,"Source selected original identity "+n+"/"+stage);
                GeneratedNpcStatChecks.Compare(graph,ids,expected[1],n,stage);
            }
            Compare(0);weapon.Enter(reset);Compare(1);weapon.Enter(reset);Compare(2);weapon.Restore(reset);Compare(3);
            weapon=graph.Weapons(primary==0?0:ids[primary-1],secondary==0?0:ids[secondary-1],used,row,selection==0?1:0,frame+256,flags);weapon.Enter(reset);Compare(4);
            Require(Json(changes)==outputs[n][1].GetRawText(),"Source selected-hand callback order "+n);
            Require(BattleOrders.Capture(actor)==native&&ids.Select(id=>((GeneratedNpcItem)World.FindItem((Serial)id)).SavedText).SequenceEqual(held.Payloads),"Numeric hand context mutated native pools or original item payloads");
        }
        File.WriteAllText(manifest,Json(saved));
        Log.Information("Generated NPC weapons:3276source item/hand compositions including1728multi-item;12288shared source selector controls;60equip guards;original identities,all arrays,damage toggles,repeat/restore/swap and callback order survive two reloads;no live combat admission");
    }
}
