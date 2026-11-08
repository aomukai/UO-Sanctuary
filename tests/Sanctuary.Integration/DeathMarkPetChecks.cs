// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class DeathMarkPetChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkPetChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-pets-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {string name=reload?"death-mark-pets-reload-check":"death-mark-pets-check";bool again=reload;ConsoleInputHandler.RegisterCommand(name,"Current Death Mark pet callback and shared placement composition",_=>Core.LoopContext.Post(()=>
        {try{Check(again);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Remove(ref DeathMarkControlList head,DeathMarkControlList node)
    {
        DeathMarkControlList previous=null;
        for(var current=head;current!=null;current=current.Next)
        {
            if(current==node){if(previous==null){head=current.Next;}else{previous.Next=current.Next;}return;}
            previous=current;
        }
    }
    private static int[] Chain(DeathMarkControlList head)
    {var ids=new List<int>();for(;head!=null;head=head.Next){Require(ids.Count<32,"Invalid cyclic fixture");ids.Add(head.Identity);}return ids.ToArray();}
    private static void Check(bool reload)
    {
        int generation=reload?int.Parse(File.ReadAllText(Manifest))+1:0;var before=NpcWorldAllocation.Capture();int n=0;
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"death-mark-pets-cases.json")));
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");int I(string k)=>input.GetProperty(k).GetInt32();bool B(string k)=>input.GetProperty(k).GetBoolean();
            var nodes=new List<DeathMarkControlList>();
            DeathMarkControlList Make(string key)
            {
                DeathMarkControlList first=null,previous=null;
                foreach(var entry in input.GetProperty(key).EnumerateArray())
                {var node=new DeathMarkControlList(nodes.Count+1,entry[0].GetInt32(),entry[1].GetInt32());nodes.Add(node);if(previous==null){first=node;}else{previous.Next=node;}previous=node;}
                return first;
            }
            var root=new DeathMarkControlRoot(I("rootFlags"),Make("first"),Make("second"));var current=B("root")?root:null;
            var directive=B("directive")?new DeathMarkDirective{TargetType=I("targetType"),TargetId=I("targetId"),X=I("x"),Y=I("y"),Remaining=I("remaining"),ClearControl=B("clear")}:null;
            var trace=new List<long[]>();
            DeathMarkPets.Run(new(B("pet"),checked((short)I("overlay")),checked((byte)I("unitSelector")),checked((byte)I("gameSelector"))),directive,
                new(point=>{trace.Add([1,point.X,point.Y]);return I("place");},overlay=>trace.Add([2,overlay]),(type,id)=>trace.Add([3,type,id]),ticks=>trace.Add([4,ticks]),()=>current,
                    (selector,node)=>{trace.Add([5,selector,node.Identity]);node.Flags=I("afterFlags");Remove(ref root.First,node);Remove(ref root.Second,node);node.Next=null;
                        if(I("changeRoot")==1){current=null;}else if(I("changeRoot")==2){root.Flags=0;}},(selector,node)=>trace.Add([6,selector,node.Identity])));
            var wanted=row.GetProperty("output");int[] Numbers(string key)=>wanted.GetProperty(key).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var wantedTrace=wanted.GetProperty("trace").EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
            Require((directive?.Remaining??I("remaining"))==wanted.GetProperty("remaining").GetInt32()&&(current!=null)==wanted.GetProperty("root").GetBoolean()
                &&root.Flags==wanted.GetProperty("rootFlags").GetInt32()&&Chain(root.First).SequenceEqual(Numbers("first"))&&Chain(root.Second).SequenceEqual(Numbers("second"))
                &&nodes.Select(node=>node.Flags).SequenceEqual(Numbers("flags"))&&trace.Count==wantedTrace.Length&&!trace.Where((t,i)=>!t.SequenceEqual(wantedTrace[i])).Any(),"Current Death Mark pet callback differs at case "+n);n++;
        }
        Require(n==97,"Missing current pet callback cases");Commands(json.RootElement);CommandBinding();OwnedCommandMetadata();Placement();Compose(false,true);Compose(true,true);Compose(false,false);
        Require(NpcWorldAllocation.Capture()==before&&!WarlockCombat.Implemented(CombatAbility.DeathMark),"Prepared callback allocated world actors or admitted learned skill");
        File.WriteAllText(Manifest,generation.ToString());Require(generation<=2,"Unexpected callback comparison generation");
        Log.Information("Death Mark pet callback:97 callback/165 target-command current-code cases;exact placement/quota,overlay/target/idle,state21/1/11 first-chain precedence,root reloading and post-unlink/free flags. Existing monster placement binding and combined cast/pet success/failure quota/state-allocation controls pass;no world/learned admission.");
    }
    private static void Commands(JsonElement json)
    {
        int n=0;
        foreach(var row in json.GetProperty("commandCases").EnumerateArray())
        {
            var input=row.GetProperty("input");int I(string k)=>input.GetProperty(k).GetInt32();bool B(string k)=>input.GetProperty(k).GetBoolean();
            var data=new GenericPetCommandData(I("initialTarget"),I("initialType"));var unit=new GenericPetCommandUnit(I("kind"),I("classId"),checked((byte)I("selector")),B("data")?data:null);
            var trace=new List<long[]>();int result=GenericPetTargetCommand.Set(B("present")?unit:null,I("type"),I("target"),(selector,id)=>
            {trace.Add([1,selector,id]);unit.Kind=I("afterKind");unit.Data=B("afterData")?data:null;return B("row")?checked((byte)I("flags")):null;});
            var wanted=row.GetProperty("output");var expected=wanted.GetProperty("trace").EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetInt64()).ToArray()).ToArray();
            Require(result==wanted.GetProperty("result").GetInt32()&&unit.Kind==wanted.GetProperty("kind").GetInt32()&&(unit.Data!=null)==wanted.GetProperty("data").GetBoolean()
                &&data.Target48==wanted.GetProperty("target").GetInt32()&&data.Type4c==wanted.GetProperty("type").GetInt32()
                &&trace.Count==expected.Length&&!trace.Where((t,i)=>!t.SequenceEqual(expected[i])).Any(),"Current pet target command differs at case "+n);n++;
        }
        Require(n==165,"Missing target-command cases");
    }
    private static void CommandBinding()
    {
        foreach(byte flags in new byte[]{0,1})
        {
            var data=new GenericPetCommandData(-1,0);var unit=new GenericPetCommandUnit(1,742,2,data);int idle=0,overlay=0;
            var services=new DeathMarkPetServices(_=>1,_=>overlay++,(_,_)=>throw new InvalidOperationException("Target command was not rebound"),_=>idle++,()=>null,(_,_)=>{},(_,_)=>{});
            services=GenericPetTargetCommand.Bind(services,unit,(selector,id)=>{Require(selector==2&&id==742,"Command changed metadata lookup");return flags;});
            var directive=new DeathMarkDirective{X=100,Y=200,Remaining=3,TargetType=2,TargetId=77};DeathMarkPets.Run(new(true,17,2,1),directive,services);
            Require(directive.Remaining==2&&idle==1&&overlay==1&&data.Target48==(flags==1?77:-1)&&data.Type4c==(flags==1?2:0),"Command refusal wrongly rejected a successful relocation or consumed different quota");
        }
    }
    private static void OwnedCommandMetadata()
    {
        var view=GenericPetPathTargets.Bind(2);
        foreach(int id in new[]{742,743,744})
        {
            Require(view.CommandFlags(2,id)==5,"Owned demon command flags changed");
            var data=new GenericPetCommandData(-1,0);var unit=new GenericPetCommandUnit(1,id,2,data);
            Require(GenericPetTargetCommand.Set(unit,2,77,view.CommandFlags)==1&&data.Target48==77&&data.Type4c==2,"Owned command metadata did not reach the shared setter");
        }
        Require(view.CommandFlags(2,-1)==null&&view.CommandFlags(2,751)==null,"Invalid metadata ID did not refuse");
        bool refused=false;try{_=view.CommandFlags(1,742);}catch(NotSupportedException){refused=true;}Require(refused,"Unbound command selector accepted");
    }
    private static void Placement()
    {
        foreach(bool success in new[]{false,true})
        {
            var unit=new GenericPetMonsterPlacementUnit(new(742,0x21),true,0,0,0,0,19);int roomCalls=0,search=0,finished=0,overlay=0,idle=0;
            var services=new DeathMarkPetServices(_=>throw new InvalidOperationException("Placement was not rebound"),_=>overlay++,(_,_)=>throw new InvalidOperationException("Unexpected target"),ticks=>{Require(ticks==1,"Wrong idle delay");idle++;},()=>null,(_,_)=>{},(_,_)=>{});
            services=DeathMarkPets.BindPlacement(services,unit,new(()=>1,(room,x,y)=>{Require(room==0&&x==100&&y==200,"Placement changed room/point");roomCalls++;return 1;},one=>Require(one==1,"Wrong search preparation"),()=>2,()=>3,
                request=>{Require(request==new GenericPetNearbyRequest(1,100,200,0,2,0x1c09,0x1c09,0,50,1),"Shared placement changed source masks/search");search++;return new(1,100,200);},()=>{},
                (room,packed)=>{Require(room==1&&packed==(100U|(200U<<16)),"Shared path update lost coordinates");return success?1:0;},()=>finished++,zero=>Require(zero==0,"Wrong position sync")));
            var d=new DeathMarkDirective{X=100,Y=200,Remaining=3};DeathMarkPets.Run(new(true,17,2,1),d,services);
            Require(roomCalls==1&&search==1&&d.Remaining==(success?2:3)&&overlay==(success?1:0)&&idle==(success?1:0)&&finished==(success?1:0)
                &&unit.PathWord34==(success?0:19)&&unit.Pet.Flags==(success?0x10021U:0x21U),"Existing placement and callback did not compose without rollback/reset");
        }
    }
    private static void Compose(bool allFail,bool allocate)
    {
        int places=0,overlays=0,commands=0,idles=0,states=0,binds=0,auras=0;var writes=new List<(int,int)>();
        var context=new DeathMarkCastContext(1,new(0,true,100,100),true,375,1,1,-17,20,21,212,256);
        int result=DeathMarkCast.Run(context,new(()=>true,()=>true,()=>{},field=>field switch{0x194=>3,0x84=>38,0x190=>1,0x80=>125,_=>throw new InvalidOperationException("Unexpected formula")},
            ()=>new Point2D(110,120),()=>10,()=>1,(_,_)=>throw new InvalidOperationException("Unexpected placement gate"),()=>{},()=>new(77,1),_=>true,_=>10,(type,d)=>
            {
                Require(type==20,"Lost demon traversal type");
                for(int index=0;index<6;index++)
                {
                    DeathMarkPets.Run(new(true,17,2,1),d,new(point=>{Require(point==new Point2D(110,120),"Pet lost cast coordinates");int attempt=places++;return allFail||attempt is 0 or 3?0:1;},_=>overlays++,
                        (target,id)=>{Require(target==2&&id==77,"Pet lost selected enemy");commands++;},ticks=>{Require(ticks==1,"Lost idle event");idles++;},()=>null,(_,_)=>{},(_,_)=>{}));
                }
                Require(d.Remaining==(allFail?3:0),"Shared directive quota did not survive traversal");
            },r=>{Require(r.Target.Id==77&&r.State==212&&r.Duration==125,"Wrong debuff request after pet traversal");states++;return allocate;},()=>binds++,()=>auras++,(stat,value)=>writes.Add((stat,value))));
        int successes=allFail?0:3;
        Require(places==(allFail?6:5)&&overlays==successes&&commands==successes&&idles==successes&&states==(allFail?0:1)
            &&result==(!allFail&&!allocate?0:1)&&binds==(!allFail&&allocate?1:0)&&auras==binds
            &&writes.SequenceEqual(!allFail&&allocate?new[]{(350,375),(351,1),(202,-17)}:Array.Empty<(int,int)>()),"Current cast/pet composition changed failed-place quota,cap,conditional mark or post-command allocation failure");
    }
}
