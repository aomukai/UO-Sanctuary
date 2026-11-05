// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class ItemQualityChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ItemQualityChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"item-quality-fixtures.json");
    private sealed record Held(int Case,uint Item,ItemPropertyCreationState Graph,string State);
    private sealed record Saved(Held[] Items,int Generation=0);
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("item-quality-check",false);Register("item-quality-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Complete source quality/reset/finalizer and original controlled native graphs",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private sealed class Frame:ItemQualityServices
    {
        internal readonly JsonElement Input;internal readonly ItemQualityRatio[] Ratios;internal readonly ItemQualityUnique[][] Profiles;
        internal readonly ItemPropertyCreationContext Context;private ulong SeedValue;
        internal int I(int n)=>Input[n].GetInt32();private bool Bit(int n)=>(I(3)&n)!=0;
        internal readonly ulong UnitSeed;internal int Q,DQ,Fmt,File=41,Auto=51,Flags=16,RP=31,RS=32;
        internal uint Start=17,Reference;internal int[] Prefix=[11,12,13],Suffix=[21,22,23];
        private readonly Dictionary<int,int> Stats=new(){{7,1000},{72,60},{73,80}};
        internal readonly List<int[]> Trace=new();
        internal Frame(JsonElement input,ItemQualityRatio[] ratios,ItemQualityUnique[][] profiles,ItemPropertyCreationContext context=null)
        {
            Input=input;Ratios=ratios;Profiles=profiles;Context=context;SeedValue=ulong.Parse(input[16].GetString());UnitSeed=SeedValue^0x123456789ABCDEF0UL;
            Reference=(uint)SeedValue;Q=DQ=I(2);Fmt=I(1);
        }
        internal ulong Seed=>Context?.Seed??SeedValue;
        private void T(params int[] v)=>Trace.Add(v);
        private int Raw(int stat)=>Context==null?Stats.GetValueOrDefault(stat):EquipmentStatValues.Raw(Context.Unit.Stats.FullValues,EquipmentStatKey.Pack(stat));
        private void Hp(int amount)
        {if(Context==null){Stats[7]+=amount*256;}else{Context.Apply(EquipmentPropertyWriter.Plan("hp"),new(amount,amount,0));}}
        internal override bool HasItem=>!Bit(512);internal override bool IsItem=>!Bit(1024);
        internal override ItemQualityBase Basis=>Bit(2048)?null:new(42,Bit(4),I(10));
        internal override int Quality{get=>Q;set{Q=value;T(1,value);}}
        internal override int DropQuality{get=>DQ;set=>DQ=value;}
        internal override int Format=>Fmt;internal override bool CanMagic=>Bit(1);internal override bool Quest=>Bit(2);
        internal override bool MagicOrBetter=>Q is>=4 and<=9;internal override bool CanRare=>Bit(8);internal override bool UniqueColumn=>Bit(16);internal override bool CanNormal=>Bit(32);
        internal override int RollQuality()=>SourceItemQuality.Roll(new RollFrame(this));
        internal override uint LowSeed=>(uint)Seed;
        internal override void StartSeed(uint value){Start=value;T(6,unchecked((int)value));}
        internal override void InitLow(uint value){if(Context==null){SeedValue=SourceRandom.InitLow(unchecked((int)value));}else{Context.InitLow(value);}}
        internal override void AutoAffix(int id){Auto=id;T(2,id);}
        internal override void Affix(bool prefix,int slot,int id){(prefix?Prefix:Suffix)[slot]=id;T(3,prefix?1:0,slot,id);}
        internal override void RareName(bool prefix,int id){if(prefix){RP=id;}else{RS=id;}T(4,prefix?1:0,id);}
        internal override void FileIndex(int id){File=id;T(5,id);}
        private int G(int q)
        {
            int result=(I(4)&(1<<q))!=0?1:0;T(13,q,result);for(int i=0;i<q%3+1;i++){Limited(1000);}
            Prefix[0]=200+q;Suffix[0]=300+q;RS=400+q;File=500+q;Hp(q);if(I(12)==1){Q=0;}return result;
        }
        internal override int Inferior()=>G(1);internal override void Normal(bool legacy)=>G(legacy?21:20);internal override void NormalFallback()=>G(2);
        internal override int Superior()=>G(3);internal override int Rare()=>G(6);internal override int Magic()=>G(4);internal override int Craft()=>G(8);
        internal override int Tempered(bool prefix){int q=prefix?10:11;return G(q)!=0?q+600:0;}
        internal override int Set()=>G(5);internal override bool HasDurability=>Bit(64);
        internal override int FullStat(int stat)=>Raw(stat)+(stat==72?7:0);internal override int BaseStat(int stat)=>Raw(stat);
        internal override void WriteStat(int stat,int value){if(Context==null){Stats[stat]=value;}else{Context.SetBase(stat,value);}T(7,stat,value);}
        internal override bool Force=>Bit(128);internal override bool GameAllowsLadder=>Bit(256);internal override int ItemLevel=>I(14);internal override int ForcedIndex=>I(6);
        internal override int UniqueCount=>Profiles[I(5)].Length;internal override ItemQualityUnique UniqueAt(int index)=>Profiles[I(5)][index];
        internal override ItemQualityUnique FindUnique(int id){T(9,id);return id>=0&&id<UniqueCount&&(I(8)&(1<<id))!=0?UniqueAt(id):null;}
        internal override bool Dropped(int index){T(8,index);return (I(7)&(1<<index))!=0;}
        internal override bool CanDropUnique(){T(10);return I(9)!=0;}
        internal override int Limited(int bound)
        {int value;if(Context==null){value=(int)SourceRandom.Limited(ref SeedValue,bound);}else{value=bound<=0?0:Context.Roll(bound);}T(0,bound,value);return value;}
        internal override void Identify(bool value){Flags=value?Flags|16:Flags&~16;T(11,value?1:0);}
        internal override void AssignProperties(int mode,object record){T(14,mode,record is int id?id:0);if(mode==3||record!=null){Hp(mode==3?17:11);}}
        internal override void MakeEthereal(){T(15);Flags|=128;Context?.SetEthereal();if(I(12)==2){Q=7;Fmt=0;}if(I(12)==4){Q=0;}}
        internal override void FinishNormal(){T(16);if(I(12)==3){Q=9;}}
        internal override int PickAuto(int a,int b,int c,int d,int e,int f){T(17,a,b,c,d,e,f);return I(11);}
        internal override object MagicRecord(int id){T(12,id);return id>0?id:null;}
        internal int Execute()
        {
            switch(I(0))
            {
                case 0:return SourceItemQuality.Run(this);
                case 1:return RollQuality();
                case 2:return SourceItemQuality.Finish(this,Basis);
                case 3:SourceItemQuality.NormalFallback(this,Reference);return 1;
                case 4:uint reference=Reference;int result=SourceItemQuality.SuperiorFallback(this,ref reference);Reference=reference;return result;
                case 5:SourceItemQuality.Reset(this);return 1;
                default:throw new InvalidOperationException("Unknown quality source entry");
            }
        }
        internal string State(int result)=>Json(new object[]{result,Q,DQ,File,Auto,Flags,RP,RS,Prefix,Suffix,Start.ToString(),Seed.ToString(),UnitSeed.ToString(),
            new[]{new[]{7,Raw(7)},new[]{72,Raw(72)},new[]{73,Raw(73)}},Trace,Reference.ToString(),Fmt});
    }
    private sealed class RollFrame(Frame frame):ItemQualityRollServices
    {
        internal override int Format=>frame.Fmt;internal override int DropQuality=>frame.DQ;internal override bool Quest=>(frame.I(3)&4)!=0;
        internal override bool Miscellaneous=>(frame.I(3)&4096)!=0;internal override int DropLevel=>frame.I(14);internal override int BaseLevel=>frame.I(15);
        internal override bool SuperiorFlag=>(frame.I(3)&8192)!=0;internal override ItemQualityRatio Ratio=>frame.Ratios[frame.I(13)];
        internal override uint Limited(int bound)=>unchecked((uint)frame.Limited(bound));
    }
    private static EquipmentStatValues Services()=>new(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});
    private static ItemQualityRatio[] Ratios(JsonElement root)=>root.GetProperty("ratios").EnumerateArray().Select(r=>
    {var v=r.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new ItemQualityRatio(v[0],v[1],v[2],v[3],v[4],v[5],v[6],v[7],v[8],v[9],v[10],v[11]);}).ToArray();
    private static ItemQualityUnique[][] Profiles(JsonElement root)=>root.GetProperty("profiles").EnumerateArray().Select(rows=>rows.EnumerateArray().Select(r=>
        new ItemQualityUnique(r[0].GetInt32(),r[1].GetInt32()!=0,r[2].GetInt32()!=0,r[3].GetUInt32(),r[4].GetInt32(),r[5].GetInt32())).ToArray()).ToArray();
    private static void Check(bool reload)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-cases.json")));var root=doc.RootElement;var inputs=root.GetProperty("inputs");var outputs=root.GetProperty("outputs");var ratios=Ratios(root);var profiles=Profiles(root);
        for(int i=0;i<inputs.GetArrayLength();i++)
        {var f=new Frame(inputs[i],ratios,profiles);int result=f.Execute();Require(f.State(result)==outputs[i].GetRawText(),"Complete quality source state/action trace at "+i);}
        Require(inputs.GetArrayLength()==22656,"Incomplete quality source matrix");
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved([]);Require(!reload||saved.Generation<2,"Wrong quality save cohort");var rows=new List<Held>();
        if(reload)
        {
            foreach(var r in saved.Items)
            {
                var item=World.FindItem((Serial)r.Item);Require(item is{Deleted:false,Visible:false,Movable:false,Parent:null}&&item.Map==Map.Trammel,"Original quality fixture item changed");
                var context=ItemPropertyCreationContext.Restore(item,Services(),new(),false,false,false,r.Graph);Require(context.Draws==0&&Json(context.Capture())==Json(r.Graph),"Quality graph restore rerolled/lost raw state");
                var expected=outputs[r.Case];Require(r.State==expected.GetRawText()&&context.Seed==ulong.Parse(expected[11].GetString())
                    &&context.Ethereal==((expected[5].GetInt32()&128)!=0),"Original saved quality metadata/seed/ethereal contract changed");
                foreach(var stat in expected[13].EnumerateArray()){Require(EquipmentStatValues.Raw(context.Unit.Stats.FullValues,EquipmentStatKey.Pack(stat[0].GetInt32()))==stat[1].GetInt32(),"Reload lost quality graph contribution");}
                ulong seed=context.Seed;int amount=(int)SourceRandom.Limited(ref seed,5)+1;context.Apply(EquipmentPropertyWriter.Plan("hp"),new(1,5,0));
                Require(context.Seed==seed&&EquipmentStatValues.Raw(context.Capture().Properties,EquipmentStatKey.Pack(7))==EquipmentStatValues.Raw(r.Graph.Properties,EquipmentStatKey.Pack(7))+amount*256,"Saved quality item property continuation changed");rows.Add(r);
            }
        }
        else
        {
            // Original controlled item graphs,not a complete NPC item factory.
            for(int entry=0;entry<6;entry++)for(int variant=0;variant<3;variant++)
            {
                int selected=Enumerable.Range(0,inputs.GetArrayLength()).First(i=>inputs[i][0].GetInt32()==entry&&inputs[i][1].GetInt32()==(variant==0?0:100)
                    &&inputs[i][2].GetInt32()==(entry==2?2:entry==1?0:6)&&inputs[i][16].GetString()==new[]{"0",SourceRandom.InitLow(511).ToString(),ulong.MaxValue.ToString()}[variant]);
                var input=inputs[selected];var item=new Item(0x14F0){Name="Quality orchestration [controlled unattached fixture]",Visible=false,Movable=false};item.MoveToWorld(new(6030+rows.Count%6,2940+rows.Count/6,0),Map.Trammel);
                var context=new ItemPropertyCreationContext(item,Services(),new(),false,false,false,ulong.Parse(input[16].GetString()),
                    [new(EquipmentStatKey.Pack(7),1000),new(EquipmentStatKey.Pack(72),60),new(EquipmentStatKey.Pack(73),80)]);
                var f=new Frame(input,ratios,profiles,context);int result=f.Execute();Require(f.State(result)==outputs[selected].GetRawText(),"Native quality graph/source trace at "+selected);
                rows.Add(new(selected,item.Serial.Value,context.Capture(),f.State(result)));
            }
        }
        Require(rows.Count==18,"Incomplete original quality item cohort");File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),reload?saved.Generation+1:0)));
        Log.Information("Item quality:22656complete source controls/6entries/11routines;18original controlled native graphs retain exact metadata,raw HP/durability,property RNG,partial service contributions and reload continuation;full quality bodies/factory remain separate");
    }
}
