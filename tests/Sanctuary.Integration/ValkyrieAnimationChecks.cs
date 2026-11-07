// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieAnimationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieAnimationChecks));
    private sealed record Held(uint Body,string Text,string[] Items,NpcEquipmentSeed Random,int Life,string Stats);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-animation-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-animation-check",false);Register("valkyrie-animation-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original Valkyrie transformed animation/rate preparation",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Store(AmazonValkyrieBody body)=>new(body.Serial.Value,body.SavedText,ValkyrieEquipment.Originals(body).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(body),SkillHealth.Balance256(body),Json(body.EquipmentNumbers.Stats));
    private static void Inspect(AmazonValkyrieBody body,JsonElement[] cases)
    {
        var before=Json(Store(body));
        foreach(var c in cases)
        {
            int mode=c.GetProperty("Mode").GetInt32();var stats=c.GetProperty("Stats").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            Require(new[]{93,68,67,96}.Select(i=>body.EquipmentNumbers.Read(i)).SequenceEqual(stats),"Original full-graph timing stats differ from independent source reader");
            foreach(int frame in new[]{0,997})
            {
                var plan=ValkyrieEquipmentAnimation.Plan(body,mode,c.GetProperty("BaseAttack").GetInt32(),c.GetProperty("BaseMove").GetInt32(),frame);
                var expected=c.GetProperty("Expected");
                Require(expected[0].GetInt32()==0&&expected[1].GetInt32()==0&&plan.ActorMode==mode&&plan.GraphicsMode==expected[2].GetInt32()
                    &&plan.FrameBonus==expected[3].GetInt32()&&plan.Speed==expected[4].GetInt32()&&plan.Velocity==expected[5].GetInt32(),"Original transformed mode/rate/frame bonus differs from full source bodies");
                var events=expected[6].EnumerateArray().Select(e=>new[]{e[0].GetInt32(),e[1].GetInt32()+frame,e[2].GetInt32(),e[3].GetInt32()}).ToArray();
                Require(Json(plan.Events.Select(e=>new[]{e.Type,e.Frame,e.Kind,e.Param}).ToArray())==Json(events),"Transformed spear initial action/end events differ from full source scheduler");
                Require(plan.Code==(mode==4?"AMA12HT":mode==2?"AMWL2HT":"AMRN2HT"),"Bare VK animation selected instead of Amazon2HT");
            }
        }
        foreach(int mode in new[]{-1,0,1,3,5,8,16})
        {bool refused=false;try{ValkyrieEquipmentAnimation.Plan(body,mode,100,100);}catch(NotSupportedException){refused=true;}Require(refused,"Unsupported source mode accepted");}
        foreach(int frame in new[]{-1,int.MaxValue})
        {bool refused=false;try{ValkyrieEquipmentAnimation.Plan(body,4,100,100,frame);}catch(ArgumentOutOfRangeException){refused=true;}Require(refused,"Unsupported clock accepted");}
        Require(Json(Store(body))==before,"Read-only timing mutated original gear/birth/wounds/stats or consumed attack RNG");
        Require(body.CantWalk,"Prepared clock enabled unfinished world/controller or learned cast");
    }
    private static void Check(bool reload)
    {
        using var clock=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-animation-cases.json")));var cases=clock.RootElement.EnumerateArray().ToArray();Require(cases.Length==1368,"Full source animation corpus");
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var original=damage.RootElement.EnumerateArray().Where(c=>c.GetProperty("Critical").GetInt32()==1).ToArray();Require(original.Length==57,"Original loadout corpus");
        PlayerMobile owner;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Animation reload count");generation=saved.Generation+1;
            owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Original animation owner lost");
            foreach(var held in saved.Bodies){var b=World.FindMobile((Serial)held.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&Json(Store(b))==Json(held),"Original animation context changed on actual reload");bodies.Add(b);}
        }
        else
        {
            owner=new PlayerMobile{Name="ValkyrieAnimationFixture",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);ClassSkillChecks.Train(owner,"amazon.valkyrie",20);FixtureSpace.Corridor(owner,6380,20,3640);
            foreach(var c in original){bodies.Add(ValkyrieDamageChecks.Body(owner,c));}
        }
        for(int i=0;i<bodies.Count;i++){Inspect(bodies[i],cases.Where(c=>c.GetProperty("Case").GetInt32()==i).ToArray());}
        if(!reload){PoisonDaggerChecks.Named(owner,"Tarnhelm");ClassGear.Refresh(owner);}
        var helds=bodies.Select(Store).ToArray();PersistentMinions.Suspend(owner);owner.Internalize();
        for(int i=0;i<bodies.Count;i++){Require(Json(Store(bodies[i]))==Json(helds[i]),"Parking changed prepared timing ingredients");}
        File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,helds,generation)));
        Log.Information("Valkyrie animation:1368complete source contexts/2736epoch comparisons per boot;57original loadouts;Amazon2HT frame bonus2;read-only preparation only");
    }
}
