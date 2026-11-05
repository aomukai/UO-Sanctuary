// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class ItemStaffmodChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ItemStaffmodChecks));
    private sealed record Held(int Case,uint Item,ItemPropertyCreationState Graph);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-staffmods-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-staffmods-check",false);Register("item-staffmods-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Source staffmods,current original graphs and saved continuation",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Frame:ItemStaffmodServices
    {
        private readonly JsonElement Input;
        private readonly Dictionary<int,StaffmodSkill> Records;
        private readonly int[][][] Tiers;
        private readonly Dictionary<int,int> Skills=new();
        private readonly List<int[]> Trace=new();
        private ulong Seed;
        private bool HasList;
        private int I(int n)=>Input[n].GetInt32();
        internal Frame(JsonElement input,Dictionary<int,StaffmodSkill> records,int[][][] tiers)
        {Input=input;Records=records;Tiers=tiers;Seed=ulong.Parse(input[8].GetString());HasList=I(9)==0;if(HasList){Skills[I(6)]=2;}}
        internal override int ClassId=>I(1);
        internal override int SkillCount(int c)=>I(7);
        internal override int FirstSkill(int c)=>I(6);
        internal override int Format=>I(3);
        internal override int Quality=>I(4);
        internal override int ItemLevel=>I(2);
        internal override uint DropFlags=>Input[5].GetUInt32();
        internal override uint Random(){uint value=SourceRandom.Roll(ref Seed);Trace.Add([0,unchecked((int)value)]);return value;}
        internal override StaffmodSkill Skill(int id)=>I(10)switch{1=>null,2=>new(0),3=>new(999),4 when id%2!=0=>new(999),_=>Records.GetValueOrDefault(id)};
        internal override bool IsType(int type)
        {
            string code=Input[13].GetString();if(code.Length==0){return I(11)!=0;}
            return CurrentItemFinishing.Catalogue.Types.FirstOrDefault(t=>t.Ordinal==type)is{} t&&NpcItemAffixes.Pool.IsType(code,t.Code);
        }
        internal override bool List()
        {
            Trace.Add([1,HasList?1:0]);if(HasList){return true;}
            bool ok=I(9)!=2;Trace.Add([2,ok?1:0]);Trace.Add([3,ok?1:0]);HasList=ok;return ok;
        }
        internal override void Write(int skill,int value){Skills[skill]=value;Trace.Add([4,skill,value]);}
        internal string Execute()
        {
            switch(I(0)){case 0:SourceItemStaffmods.Apply(this);break;case 1:SourceItemStaffmods.Modern(this,ItemLevel,I(6),I(7),I(12));break;case 2:SourceItemStaffmods.Legacy(this,ItemLevel,I(6));break;case 3:SourceItemStaffmods.ModernByTier(this,ItemLevel,(DropFlags&32)!=0?ItemLevel:0,(tier,draw)=>{var ids=Tiers[ClassId][tier-1];return ids[draw%(uint)ids.Length];});break;}
            ulong next=Seed;int hp=1+(int)SourceRandom.Limited(ref next,5);
            return Json(new object[]{Seed.ToString(),HasList,Skills.OrderBy(r=>r.Key).Select(r=>new[]{r.Key,r.Value}),Trace,next.ToString(),hp});
        }
    }
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static ItemFinishingParameters Parameters(JsonElement input)=>new(input[13].GetString(),input[3].GetInt32(),input[4].GetInt32(),input[2].GetInt32(),0,0,input[5].GetUInt32()|8,511,99);
    private static string Label(string code)
    {string type=NpcItemAffixes.Pool.Basis(code).Type;return CurrentItemFinishing.Catalogue.Types.Single(t=>t.Code==type).StaffMods;}
    private static void Verify(ItemPropertyCreationContext c,JsonElement input,JsonElement expected)
    {
        var s=c.Capture();Require(s.Seed==ulong.Parse(expected[0].GetString())&&s.HasProperties==expected[1].GetBoolean(),"Staffmod property RNG/list presence");
        var actual=s.Properties.Where(p=>p.Stat==107).OrderBy(p=>p.Packed).Select(p=>new[]{(int)(p.Packed&0xFFFF),p.Value});
        Require(Json(actual)==expected[2].GetRawText(),"Actual current staffmod raw skill keys/overwrites");
        Require(s.Properties.All(p=>p.Stat==107||p.Stat==7)&&EquipmentStatValues.Raw(s.Properties,EquipmentStatKey.Pack(7))==(input[9].GetInt32()==0?256:0),"Staffmods changed unrelated contributions");
    }
    private static void Continuation(ItemPropertyCreationContext c,JsonElement input,JsonElement expected)
    {
        c.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));
        Require(c.Seed==ulong.Parse(expected[4].GetString())&&EquipmentStatValues.Raw(c.Capture().Properties,EquipmentStatKey.Pack(7))==256*(expected[5].GetInt32()+(input[9].GetInt32()==0?1:0)),"Staffmod exact subsequent numeric property continuation");
    }
    private static void Check(bool reload)
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-staffmods-cases.json")));var root=document.RootElement;
        var restrictions=root.GetProperty("restrictions").EnumerateObject().ToDictionary(p=>int.Parse(p.Name),p=>new StaffmodSkill(p.Value.GetInt32()));
        var tiers=new[]{"ama","sor","nec","pal","bar","dru","ass","war"}.Select(c=>root.GetProperty("tiers").GetProperty(c).EnumerateArray().Select(t=>t.EnumerateArray().Select(id=>id.GetInt32()).ToArray()).ToArray()).ToArray();
        var controls=root.GetProperty("inputs");var expectedControls=root.GetProperty("outputs");
        for(int i=0;i<controls.GetArrayLength();i++){Require(new Frame(controls[i],restrictions,tiers).Execute()==expectedControls[i].GetRawText(),"Complete staffmod source state/ordered trace at "+i);}
        var approved=root.GetProperty("adaptationInputs");var approvedOutputs=root.GetProperty("adaptationOutputs");Require(approved.GetArrayLength()==960,"Approved tier context count");
        for(int i=0;i<approved.GetArrayLength();i++){Require(new Frame(approved[i],restrictions,tiers).Execute()==approvedOutputs[i].GetRawText(),"Approved required-level tier source composition at "+i);}
        var inputs=root.GetProperty("nativeInputs");var outputs=root.GetProperty("nativeOutputs");Require(inputs.GetArrayLength()==414,"Current staffmod context count");
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Staffmod reload cohort");var held=new List<Held>();
        for(int i=0;i<inputs.GetArrayLength();i++)
        {
            var input=inputs[i];var expected=outputs[i];string code=input[13].GetString();var d=CurrentItemInitialization.For(code);Item item;ItemPropertyCreationContext c;
            if(reload)
            {
                var original=saved.Items[i];Require(original.Case==i,"Original staffmod context changed");item=World.FindItem((Serial)original.Item);
                Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Original staffmod item identity changed");
                c=ItemPropertyCreationContext.Restore(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,original.Graph);
                Require(c.Draws==0&&Json(c.Capture())==Json(original.Graph),"Staffmod graph restore lost data or rerolled");
            }
            else
            {
                item=new Item(0x14F0){Name="Staffmods [unattached source fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6080+i%12,2940+i/12,0),Map.Trammel);
                c=new(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,ulong.Parse(input[8].GetString()),[new(EquipmentStatKey.Pack(73),100)]);
                if(input[9].GetInt32()==0){c.GetOrCreateList(0,0x40);c.SetList(7,256,0);c.SetList(107,2,input[6].GetInt32());}
                new CurrentItemStaffmods(c,Parameters(input),Label(code)).Apply();
            }
            Verify(c,input,expected);held.Add(new(i,item.Serial.Value,c.Capture()));Continuation(c,input,expected);
        }
        if(!reload)
        {
            var original=held[0];var item=World.FindItem((Serial)original.Item);var d=CurrentItemInitialization.For(inputs[0][13].GetString());
            var c=ItemPropertyCreationContext.Restore(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,original.Graph);string before=Json(c.Capture());
            foreach(string code in new[]{"dgr","7bl"})
            {
                // Unsupported legacy format refuses before current Reign mutation.
                bool refused=false;try{_=new CurrentItemFinishing(item,c,Services(),new(code,0,3,37,0,0,8,511,99));}catch(NotSupportedException){refused=true;}
                Require(refused&&Json(c.Capture())==before,"Unsupported current staffmod format changed graph");
            }
            // Actual qualified normal finishing must call the same
            // staffmod generator after its own existing operations. Compare
            // the subsequent graph with an independently prepared staffmod
            // oracle from this proof,not a guessed higher-level factory.
            var source=inputs[0];var expected=outputs[0];var paramsNormal=Parameters(source);
            var normal=new ItemPropertyCreationContext(item,Services(),d.PropertyBase,d.Weapon,d.Armor,d.Throwable,ulong.Parse(source[8].GetString()),[new(EquipmentStatKey.Pack(73),100)]);
            SourceItemFinishing.Normal(new CurrentItemFinishing(item,normal,Services(),paramsNormal));Verify(normal,source,expected);
        }
        Require(held.Count==414,"Incomplete staffmod original cohort");File.WriteAllText(Manifest,Json(new Saved(held.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Item staffmods:{Count}complete source controls/6routines;960approved tier controls;414current contexts/138bases on414original graphs;skill keys,RNG,refusals and two-reload continuation;all eight classes;full factory remains open",controls.GetArrayLength());
    }
}
