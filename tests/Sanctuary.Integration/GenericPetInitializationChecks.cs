// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetInitializationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetInitializationChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure(){Register("pet-initialization-check",Check);Register("pet-initialization-reload-check",Root);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current initialization/command consumption/owner survey/root composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Trace(List<int[]> actual,JsonElement expected)
    {var rows=expected.EnumerateArray().Select(Ints).ToArray();Require(actual.Count==rows.Length&&actual.Where((a,i)=>!a.SequenceEqual(rows[i])).Any()==false,"Current composed service order/arguments differ");}
    private static int[] Parameters(GenericPetParameters p)=>[p.LeashSquared,p.WarpSquared,p.CloseRaw,p.WanderRaw,p.SwarmRaw,p.Flags,p.GatherType,p.AllowSkillBonuses?1:0,p.ChooseType,p.ModeAttack,p.ModeAiType,p.ModeBonus,p.MeleeMinSquared,p.MeleeMaxSquared,p.MeleeBonus,p.RangedMinSquared,p.RangedMaxSquared,p.RangedBonus,p.RandomBonus,p.ProgressiveBonus];
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-initialization-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<int[]>();
            var units=Enumerable.Range(1,6).ToDictionary(i=>i,i=>new GenericPetCommandUnit(i==2?N(p,"ownerKind"):1,500+i,checked((byte)N(p,"unitSelector")),new(104,2)));
            var command=units[1];var data=command.Data;data.Target48=N(p,"commandTarget");data.Type4c=N(p,"commandType");
            var memory=Ints(p.GetProperty("memory"));var control=new GenericPetDecisionMemory{Target=999,Value0=memory[0],Value1=memory[1],Value2=memory[2]};
            var paths=Enumerable.Range(1,6).ToDictionary(i=>i,i=>new GenericPetPathTarget(4,1,104));
            var buckets=Enumerable.Range(0,2).ToDictionary(k=>k,k=>units.Where(u=>u.Value.Kind==k).Select(u=>u.Key).ToArray());
            var next=buckets.Values.SelectMany(nodes=>nodes.Select((id,i)=>(id,after:i+1<nodes.Length?nodes[i+1]:0))).ToDictionary(x=>x.id,x=>x.after);
            var registry=new GenericPetRegistryServices((_,kind,id)=>{trace.Add([4,kind,id]);return buckets[kind].FirstOrDefault();},_=>throw new InvalidOperationException(),id=>100+id,id=>next[id]);
            int Resolve(int game,int kind,int id)=>GenericPetRegistry.Resolve(game,kind,id,registry);
            int validIndex=0;var validity=Ints(p.GetProperty("valid"));
            var survey=new GenericPetOwnerSurveyServices(id=>new(units[id].Kind,100+id,true,true,true,paths[id]),
                (selector,id)=>{trace.Add([0,selector,id]);return B(p,"row");},_=>throw new InvalidOperationException("Unbound survey getter"),
                (unit,target,mode)=>{int result=validity[Math.Min(validIndex++,validity.Length-1)];trace.Add([2,unit,target,mode,result]);return result;},
                id=>trace.Add([3,id]));
            survey=GenericPetTargetCommand.Bind(survey,1,id=>units[id],Resolve);
            var parameters=p.GetProperty("parameters").ValueKind==JsonValueKind.Null?null:GenericPetParameters.Decode(Ints(p.GetProperty("parameters")).Select(x=>checked((short)x)).ToArray());
            var services=new GenericPetInitializationServices(Resolve,id=>
            {
                int value=p.GetProperty("properties")[id==1?0:1].GetInt32();trace.Add([5,id,value,data.Target48,data.Type4c]);
                if(id==1&&B(p,"mutateControl")){control.Value0=99;control.Value1=98;control.Value2=97;}
                if(id==1&&B(p,"mutateCommand")){data.Target48=105;data.Type4c=2;}
                if(id==2&&p.GetProperty("afterOwnerKind").ValueKind!=JsonValueKind.Null){units[2].Kind=N(p,"afterOwnerKind");}
                return value;
            },id=>units[id].Kind,(owner,visit)=>{trace.Add([6,owner,data.Target48,data.Type4c]);foreach(int id in Ints(p.GetProperty("visits"))){visit(id);}},survey);
            var state=GenericPetInitialization.Run(new(1,checked((byte)N(p,"selector")),1,B(p,"owner")?2:0,command,control,parameters),services);
            var initial=expected.GetProperty("initialized");var frame=state.Decision;
            Require(Parameters(frame.Parameters).SequenceEqual(Ints(initial.GetProperty("parameters"))),"Composed default/decoded parameters differ");
            Require(frame.Memory.Target==N(initial,"target")&&new[]{frame.Memory.Value0,frame.Memory.Value1,frame.Memory.Value2}.SequenceEqual(Ints(initial.GetProperty("memory"))),"Command cache/control snapshot order differs");
            Require(new[]{state.PetLifePercent,state.OwnerLifePercent}.SequenceEqual(Ints(initial.GetProperty("properties")))&&state.Gather.Value108==N(initial,"gather"),"Property/default scratch differs");
            Require(state.Survey.Visits==N(initial,"visits")&&state.Survey.Targets.SequenceEqual(initial.GetProperty("swarm").EnumerateArray().Select(x=>new GenericPetSearchSwarm(x[0].GetInt32(),x[1].GetInt32()))),"Actual owner callback output differs");
            Require(new[]{data.Target48,data.Type4c}.SequenceEqual(Ints(initial.GetProperty("command"))),"Initial command consumption/property side effect differs");Trace(trace,initial.GetProperty("trace"));trace.Clear();
            int selected=N(p,"finishTarget");(int Kind,int Id)? target=selected==0?null:(N(p,"finishKind"),100+selected);
            int result=GenericPetTargetCommand.Finish(command,target,(selector,id)=>
            {
                trace.Add([0,selector,id]);if(p.GetProperty("finishAfterKind").ValueKind!=JsonValueKind.Null){command.Kind=N(p,"finishAfterKind");}
                if(!B(p,"finishAfterData")){command.Data=null;}
                return B(p,"finishRow")?checked((byte)N(p,"finishFlags")):null;
            });
            var finish=expected.GetProperty("finished");
            Require(result==N(finish,"result")&&command.Kind==N(finish,"kind")&&(command.Data!=null)==B(finish,"hasData")&&new[]{data.Target48,data.Type4c}.SequenceEqual(Ints(finish.GetProperty("command"))),"Current finish clear/restore/capability guards differ");
            Trace(trace,finish.GetProperty("trace"));count++;
        }
        Require(count==29,"Missing composed initialization controls");Root();
        Log.Information("Initialization:29composed current-code contexts;cached command before consumption,control before property side effects,actual owner callback/default reset and source finishing match. Existing root consumes produced frame and finishes before publishing memory;automatic AI remains gated.");
    }
    private static void Root()
    {
        var pet=new GenericPetDecisionUnit(1,true,20,30);var owner=new GenericPetDecisionUnit(0,true,20,30);var command=new GenericPetCommandUnit(1,501,1,new(104,2));
        var control=new GenericPetDecisionMemory{Value0=7,Value1=8,Value2=9};var trace=new List<string>();GenericPetInitializedFrame prepared=null;
        var survey=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException(),(_,_)=>false,_=>0,(_,_,_)=>0,_=>{});
        var services=new GenericPetInitializationServices((_,_,id)=>id==104?4:0,id=>id+10,_=>0,(_,visit)=>{visit(1);visit(3);},survey);
        var root=new GenericPetRootServices(()=>owner,_=>throw new InvalidOperationException("Unbound initializer"),i=>trace.Add("remove"+i),(_,_,_)=>0,_=>2,
            (_,_)=>throw new InvalidOperationException("Unexpected follow"),(_,frame)=>
            {
                Require(ReferenceEquals(frame,prepared.Decision)&&frame.Memory.Target==4&&command.Data.Target48==0&&command.Data.Type4c==0,"Root used detached/unconsumed frame");
                frame.Memory.Target=5;frame.Memory.Value0=11;frame.Memory.Value1=12;frame.Memory.Value2=13;trace.Add("combat");return true;
            },_=>throw new InvalidOperationException("Unexpected idle"),memory=>
            {Require(memory.Target==5&&GenericPetTargetCommand.Finish(command,(1,105),(_,_)=>1)==1,"Root finish selection");trace.Add("finish");},
            (a,b,c)=>{Require(command.Data is{Target48:105,Type4c:2},"Memory published before selected command restored");control.Value0=a;control.Value1=b;control.Value2=c;trace.Add("persist");});
        root=GenericPetInitialization.Bind(root,resolved=>{Require(ReferenceEquals(resolved,owner),"Resolved owner changed");return new(1,1,1,2,command,control,null);},services,state=>{prepared=state;trace.Add("prepared");});
        GenericPetDecisions.Think(new(pet,true,true,true,new byte[]{2,1,2}),root);
        Require(trace.SequenceEqual(new[]{"prepared","remove0","remove2","combat","finish","persist"})&&prepared.Survey.Visits==2&&prepared.Gather.Value108==0
            &&new[]{control.Value0,control.Value1,control.Value2}.SequenceEqual(new[]{11,12,13}),"Root initialization/event/finish/publication order");
    }
    internal static void Originals(PlayerMobile[] owners,BaseCreature target,SkeletonGuardian[] bodies)
    {
        var world=new GenericPetWorldIdentity(71);
        foreach(var body in bodies)
        {
            int id=checked((int)body.Serial.Value),owner=checked((int)body.GetMaster().Serial.Value);var before=DeathMarkWorld.State(body);
            var raw=new GenericPetOwnerSurveyServices(_=>throw new InvalidOperationException("Swarm0 unit"),(_,_)=>throw new InvalidOperationException("Swarm0 row"),
                _=>throw new InvalidOperationException("Unbound command"),(_,_,_)=>throw new InvalidOperationException("Swarm0 valid"),_=>throw new InvalidOperationException("Swarm0 path"));
            var control=new GenericPetDecisionMemory{Value0=31,Value1=32,Value2=33};
            var leaves=new GenericPetInitializationServices((_,_,_)=>throw new InvalidOperationException("Unbound registry"),_=>throw new InvalidOperationException("Native life property not bound"),
                unit=>World.FindMobile((Serial)(uint)unit) is PlayerMobile?0:1,(_,_)=>throw new InvalidOperationException("Unbound player pet store"),raw);
            leaves=GenericPetOwnerIteratorChecks.Originals(leaves,owner,bodies,world);
            var frame=DeathMarkWorld.InitializePrepared(body,owner,control,world,leaves);
            int expected=before.Type4c==1?before.Target48:checked((int)target.Serial.Value);
            Require(frame.Decision.Memory.Target==expected&&frame.Decision.Memory.Value0==31&&frame.Survey.Visits==bodies.Count(b=>checked((int)b.GetMaster().Serial.Value)==owner)
                &&frame.PetLifePercent==GenericPetLifePercent.For(id)&&frame.OwnerLifePercent==GenericPetLifePercent.For(owner)&&frame.Gather.Value108==0,"Original actor initializer did not consume supplied source context");
            Require(DeathMarkWorld.State(body) is{Target48:0,Type4c:0},"Original registered command not consumed");
            Require(DeathMarkWorld.FinishPrepared(body,(before.Type4c-1,before.Target48))==1&&DeathMarkWorld.State(body)==before,"Original selected command not restored without resetting controller");
        }
    }
}
