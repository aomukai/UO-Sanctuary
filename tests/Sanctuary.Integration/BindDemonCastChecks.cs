// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class BindDemonCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonCastChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("bind-demon-cast-check","Current do158 ordering with mandatory binding services",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("bind-demon-cast-check: passed");}catch(Exception e){Log.Error(e,"bind-demon-cast-check: failed");}}));
    private static void Check()
    {
        var allocated=NpcWorldAllocation.Capture();var row=SkillMechanics.For(CombatAbility.BindDemon);
        Require(row.SourceId==382&&row.Number("srvdofunc")==158&&row.Number("srvstfunc")==64&&row.Text("auratargetstate")=="bind_demon_underling","Owned Bind Demon route changed");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bind-demon-cast-cases.json")));int cases=0;
        foreach(var item in doc.RootElement.EnumerateArray())
        {
            var p=item.GetProperty("input");int I(string key)=>p.GetProperty(key).GetInt32();uint U(string key)=>p.GetProperty(key).GetUInt32();bool B(string key)=>p.GetProperty(key).GetBoolean();
            var trace=new List<long[]>();var freed=new List<int>();var path=new GenericPetPathTarget(B("selfTarget")?1:B("target")?2:0,1,17);
            var control=B("control")?new BindDemonControl((ushort)I("aiFlags"),new(B("owner")?71:0,I("ownerKind"),I("ownerId"),0)):null;
            var data=new BindDemonData((ushort)I("dataFlags"),I("index"),control);
            var caster=new BindDemonUnit(I("kind"),U("casterFlags"),I("context"),B("path")?path:null,null,0);
            var target=new BindDemonUnit(I("targetKind"),U("targetFlags"),0,null,B("data")?data:null,I("events")>0?1:0);
            var other=new BindDemonUnit(I("resolvedKind"),0,0,null,null,0);var units=new Dictionary<int,BindDemonUnit>{{1,caster},{2,target},{3,other}};
            var counter=new BindDemonCounter(I("count"));var next=Enumerable.Range(1,I("events")).ToDictionary(n=>n,n=>n<I("events")?n+1:0);
            void Stop(int id,int which){Require(id==2,"Bind Demon AI stop lost target");trace.Add([which]);}
            var services=new BindDemonCastServices(game=>{Require(game==71,"Bind game changed");return new(0,0,(byte)I("selector"),0);},(selector,skill)=>
                {Require(selector==I("selector")&&skill==382,"Bind row context changed");trace.Add([1,selector,skill]);return B("row")?new((short)I("state")):null;},
                id=>units[id],(game,id)=>{Require(game==71&&id==(B("caster")?1:0),"Bind refresh context changed");trace.Add([2,id]);if(B("refreshClears")){path.Target=0;}},_=>I("stateCount"),
                (game,source,to,skill)=>{Require((game,source,to,skill)==(71,1,2,382),"Bind gate arguments changed");trace.Add([3,1,2,382]);return B("eligible");},
                id=>Stop(id,4),id=>Stop(id,5),(game,index)=>{Require(game==71&&index==I("index"),"Bind counter index changed");return B("counter")?counter:null;},
                id=>{Require(id==2,"Bind value target changed");trace.Add([6]);return I("value");},
                (game,to,source)=>{Require((game,to,source)==(71,2,1),"Bind transfer context changed");trace.Add([7]);caster.Field12c=I("lateContext");if(B("transferClearsData")){target.Data=null;}},
                id=>next[id],id=>{trace.Add([8,id]);freed.Add(id);if(B("freeMutatesNext")&&id==1){next[1]=0;}},
                (game,kind,id)=>{Require((game,kind,id)==(71,I("ownerKind"),I("ownerId")),"Bind registry arguments changed");trace.Add([9,game,kind,id]);return I("resolved") switch{0=>0,1=>2,2=>3,_=>throw new InvalidOperationException()};},
                id=>Stop(id,10),(game,source,to,skill,rank,context,flag)=>
                {Require((game,source,to,skill,rank,context,flag)==(71,1,2,382,I("rank"),I("lateContext"),true),"Bind final callback context changed");trace.Add([11,skill,rank,context,flag?1:0]);return B("bind");},
                id=>{Require(id==2,"Bind room update target changed");trace.Add([12,target.Flags124,target.Data?.Flags30??-1]);});
            int result;try{result=BindDemonCast.Run(B("game")?71:0,B("caster")?1:0,382,I("rank"),services);}catch(NotSupportedException){result=-1;}
            var expected=item.GetProperty("output");
            var wanted=expected.GetProperty("trace").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
            Require(result==expected.GetProperty("result").GetInt32()&&caster.Flags124==expected.GetProperty("casterFlags").GetUInt32()
                &&target.Flags124==expected.GetProperty("targetFlags").GetUInt32()&&data.Flags30==expected.GetProperty("dataFlags").GetInt32()
                &&counter.Value==expected.GetProperty("count").GetInt32()&&target.FirstC0==expected.GetProperty("first").GetInt32()
                &&freed.SequenceEqual(expected.GetProperty("freed").EnumerateArray().Select(v=>v.GetInt32()))
                &&trace.Count==wanted.Length&&!trace.Where((v,i)=>!v.SequenceEqual(wanted[i])).Any(),$"Current Bind Demon return/flags/mutations/order differ at{cases}");cases++;
        }
        Require(cases==69&&NpcWorldAllocation.Capture()==allocated&&!WarlockCombat.Implemented(CombatAbility.BindDemon),"Incomplete Bind Demon allocated actors or opened learned route");
        Log.Information("Bind Demon:69current contexts/709bytes;target refresh,guards,AI stop branches,cached counter,event-next capture,owner resolution,current caster context and pre-bind mutations match. Failed eligibility or bind still returns1;no payment rule is inferred. Mandatory protected helpers remain explicit,no learned admission.");
    }
}
