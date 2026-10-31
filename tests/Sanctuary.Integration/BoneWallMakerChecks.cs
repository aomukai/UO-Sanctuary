// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class BoneWallMakerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneWallMakerChecks));
    private sealed record Saved(uint Body,uint Owner,BoneWallGrowth Growth,BarrierBirthState Birth,int Life,DateTime Until,uint[] Children);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-wall-maker-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("bone-wall-maker-check",Check);Register("bone-wall-maker-reload-check",Reload);
        Register("bone-wall-maker-save",()=>{StageSave();BoneBarrierBirthChecks.StageSave();});
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bone Wall maker state/source composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceContract()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-wall-maker-source-cases.json")));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("maker").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int ownerCalls=0,budget=V(1),allocations=0,links=0,writes=0;string trace="";
            bool Owner(){trace+="O";return (V(0)&(ownerCalls++==0?1:2))!=0;}
            bool Skill(){trace+="K";return V(3)!=0;}
            bool Summon(){trace+="I";return V(4)!=0;}
            bool Root(){trace+="R";return V(5)!=0;}
            bool Spawn(){trace+="A";allocations++;if(V(6)==0){return false;}links++;trace+="DLUPSN";return true;}
            int Update()
            {
                trace+=V(7) switch{2=>"MH",1=>"MFH",3 or 4=>"MFOCH",5=>"MFOC",_=>"MFO"};
                return V(7) switch{1 or 2 or 4=>7,3=>2,_=>1};
            }
            int result=BoneWallMakerUpdate.Run(budget,new(Owner,()=>((V(2)&8)!=0),Skill,Summon,Root,Spawn,n=>{budget=n;writes++;trace+="B";},Update));
            Require(result==V(8)&&budget==V(9)&&allocations==V(11)&&links==V(12)&&writes==V(13)&&trace==row[17].GetString(),"Complete source maker contract "+count);
            count++;
        }
        Require(count==2592,"Incomplete maker source contexts");
    }
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player();p.Name="BoneWallMakerFixture";p.AddItem(new Backpack());FixtureSpace.Corridor(p,7060,24,3500);return p;
    }
    private static BoneBarrierSegment[] Parts(BoneWallMakerBody root)
    {var list=new List<BoneBarrierSegment>();foreach(var b in root.Map.GetMobilesInRange<BoneBarrierSegment>(root.Location,16)){if(b==root||b.Root==root){list.Add(b);}}return list.ToArray();}
    private static BoneWallMakerBody Create(PlayerMobile p,int rank=1)
    {
        var point=new Point3D(p.X,p.Y+2,p.Z);
        Require(BoneBarriers.Create(p,CombatAbility.BoneWall,point,SkillMechanics.Values(p,CombatAbility.BoneWall,rank)),"Create learned maker root");
        var roots=new List<BoneWallMakerBody>();foreach(var b in p.Map.GetMobilesInRange<BoneWallMakerBody>(point,0)){if(b.SummonMaster==p){roots.Add(b);}}
        return roots.Single();
    }
    private static void Check()
    {
        SourceContract();var p=Owner();BoneWallMakerBody root=null;var original=BoneBarriers.Allocate;
        try
        {
            root=Create(p,20);int attempts=0;var failed=new List<Point3D>();
            BoneBarriers.Allocate=(owner,ability,point,values,parent)=>
            {if(parent==root&&++attempts<=2){failed.Add(point);return null;}return original(owner,ability,point,values,parent);};
            for(int i=0;i<root.Growth.Frames+2;i++){root.InspectGrowth(TimedEffects.Now);}
            Require(attempts>2&&failed.Count==2&&Parts(root).Length>1&&root.Growth.Left.Done&&root.Growth.Right.Done,"Allocation failure stopped a maker arm");
            Require(root.Growth.Left.Budget==4-Parts(root).Count(b=>b.X>root.X)&&root.Growth.Right.Budget==4-Parts(root).Count(b=>b.X<root.X),"Failed/duplicate placement consumed success budget");
            Require(Parts(root).All(b=>b is BoneBarrierBody body&&body.Prepared==root.Prepared),"Maker children lost immutable birth recipe");
            var children=Parts(root);root.Delete();Require(children.All(b=>b.Deleted),"Maker root did not delete its children");
            root=Create(p,80);
            BoneBarriers.Allocate=(owner,ability,point,values,parent)=>parent==root&&point.X<root.X+20?null:original(owner,ability,point,values,parent);
            for(int i=0;i<root.Growth.Frames+2;i++){root.InspectGrowth(TimedEffects.Now);}
            var distant=root.MadeChildren.ToArray();
            Require(distant.Length>0&&distant.All(b=>b.X>=root.X+20),"Failed placements did not allow distant later creation");
            root.Delete();Require(distant.All(b=>b.Deleted),"Distant maker minions escaped root deletion");
            BoneBarriers.Allocate=original;root=Create(p);
            var initial=root.Growth;root.InspectGrowth(TimedEffects.Now);
            Require(Parts(root).Length==1&&root.Growth.Left.Budget==4&&root.Growth.Left.Remaining==initial.Frames-1
                &&root.Growth.Left.Changed&&root.Growth.Left.X>root.X,"Maker spawned before initial path movement or lost first cell change");
            // Natural timer behavior is checked separately from inspected steps.
            root.Delete();root=Create(p);var natural=root;var until=root.ExpiresUtc;
            Timer.StartTimer(TimeSpan.FromMilliseconds(900),()=>Core.LoopContext.Post(()=>
            {
                try
                {
                    Require(natural.Growth.Left.Done&&natural.Growth.Right.Done&&Parts(natural).Length>1&&natural.ExpiresUtc==until,"Natural maker lifetime or barrier deadline changed");
                    Log.Information("bone-wall-maker-result: passed");
                }
                catch(Exception e){Log.Error(e,"bone-wall-maker-result: failed");}
                finally{natural.Delete();p.Delete();}
            }));
        }
        catch{root?.Delete();p.Delete();throw;}
        finally{BoneBarriers.Allocate=original;}
    }
    private static void StageSave()
    {
        var p=Owner();var b=Create(p);b.InspectGrowth(TimedEffects.Now);b.InspectGrowth(TimedEffects.Now);
        // Hold only this fixture's growth clock through save/boot, not the
        // natural24second barrier life. Reload verifies it before resuming.
        b.HoldInspectedGrowth(TimedEffects.Now.AddSeconds(60));
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(b.Serial.Value,p.Serial.Value,b.Growth,b.Prepared,SkillHealth.Balance256(b),b.ExpiresUtc,b.MadeChildren.Select(c=>c.Serial.Value).ToArray())));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body) as BoneWallMakerBody;
        Require(b is{Deleted:false,Alive:true}&&b.SummonMaster?.Serial.Value==s.Owner&&b.Growth==s.Growth&&b.Prepared==s.Birth
            &&b.ExpiresUtc==s.Until&&SkillHealth.Balance256(b)==s.Life,"Saved maker state, wounds, recipe, deadline or owner changed");
        Require(b.Growth.Left.Remaining<s.Growth.Frames&&b.Growth.Left.Remaining>0&&b.Tiles.All(t=>!t.Deleted&&t.Segment==b),"Missing partial maker or collision link");
        Require(s.Children.Length>0&&b.MadeChildren.Select(c=>c.Serial.Value).SequenceEqual(s.Children)
            &&b.MadeChildren.All(c=>c is{Deleted:false,Alive:true}&&c.Root==b),"Saved maker minion graph changed");
        b.HoldInspectedGrowth(TimedEffects.Now);
        Timer.StartTimer(TimeSpan.FromMilliseconds(900),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                Require(b is{Alive:true,Deleted:false}&&b.Growth.Left.Done&&b.Growth.Right.Done&&Parts(b).Length>1
                    &&b.ExpiresUtc==s.Until&&SkillHealth.Balance256(b)==s.Life,"Reload did not resume maker or granted recovery/reset");
                Log.Information("bone-wall-maker-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"bone-wall-maker-reload-result: failed");}
        }));
    }
}
