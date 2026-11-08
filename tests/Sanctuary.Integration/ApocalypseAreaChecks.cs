// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ApocalypseAreaChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypseAreaChecks));
    private sealed record Saved(uint Owner,uint[] Targets,uint[] Walls,Point3D Center,Point3D OwnerPoint,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-area-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("apocalypse-area-check",Check);Register("apocalypse-area-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Apocalypse ground-centered area,LOS and actual HP",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"apocalypse-area-cases.json")));int count=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");var result=row.GetProperty("output");int radius=i.GetProperty("radius").GetInt32(),x=result.GetProperty("center")[0].GetInt32(),y=result.GetProperty("center")[1].GetInt32();
            foreach(var hit in result.GetProperty("hits").EnumerateArray())
            {
                var target=i.GetProperty("targets").EnumerateArray().Single(t=>t.GetProperty("id").GetInt32()==hit[0].GetInt32());
                Require(DruidTornado.InPulse(x,y,target.GetProperty("x").GetInt32(),target.GetProperty("y").GetInt32(),radius),"Current source accepted outside native circle");
            }
            var outside=i.GetProperty("targets")[2];Require(!DruidTornado.InPulse(x,y,outside.GetProperty("x").GetInt32(),outside.GetProperty("y").GetInt32(),radius),"Exclusive exterior boundary");count++;
        }
        Require(count==6,"Incomplete current area/filter fixture");
        for(int rank=1;rank<=255;rank++){Require(ApocalypseArea.Radius(rank)==(rank>=20?17:rank>=10?15:13),"Pinned radius rank "+rank);}
    }
    private static void Area(PlayerMobile owner,BaseCreature[] targets,Point3D center)
    {
        int map=owner.Map.MapID;var first=targets[0];var hidden=targets[4];
        Require(!SourceDamageAreas.Recipient(owner,first,map,center.X*2,center.Y*2,center.Z,13,true)
            &&ApocalypseArea.Recipient(owner,first,owner.Map,center,1),"Explosion-side recipient still traces from caster");
        Require(SourceDamageAreas.Recipient(owner,hidden,map,center.X*2,center.Y*2,center.Z,13,true)
            &&!ApocalypseArea.Recipient(owner,hidden,owner.Map,center,1),"Explosion wall exclusion used caster LOS");
        Require(ApocalypseArea.Center(owner,new(0,center.Y,center.Z))==owner.Location&&ApocalypseArea.Center(owner,center)==center,"Current center coordinate fallback");
        foreach(int rank in new[]{1,9,10,19,20,255})
        {
            ulong seed=SourceRandom.InitLow(rank);var packet=ApocalypsePackets.CapturePrepared(owner,rank,25600,25600,ref seed);var before=targets.Select(SkillHealth.Balance256).ToArray();
            int expected=rank>=20?4:rank>=10?3:2;
            Require(ApocalypseArea.ReleasePrepared(owner,owner.Map,center,packet,0)==expected,"Area recipient count at rank "+rank);
            for(int index=0;index<targets.Length;index++)
            {
                int lost=before[index]-SkillHealth.Balance256(targets[index]);
                Require(index<expected?lost>0&&lost==before[0]-SkillHealth.Balance256(first):lost==0,"Radius/LOS/ally/vertical exclusion at "+rank+"/"+index);
            }
            Require(packet.SeedAfter==seed,"Area changed the shared packet seed");
        }
        ulong original=SourceRandom.InitLow(42);var suppliedRank=ApocalypsePackets.Capture(owner,ref original,10);
        Require(suppliedRank.Rank==10&&SkillMechanics.Rank(owner,CombatAbility.Apocalypse)==1,"Explosion did not retain supplied launch rank");
        var life=targets.Select(SkillHealth.Balance256).ToArray();
        Require(ApocalypseArea.ReleasePrepared(owner,Map.Felucca,center,suppliedRank)==0&&targets.Select(SkillHealth.Balance256).SequenceEqual(life),"Facet refusal still damaged");
    }
    private static void Check()
    {
        Source();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,45,2800);var center=new Point3D(owner.X+10,owner.Y,owner.Z);
        int[] offsets=[14,16,17,18,6,11,12,1];var targets=offsets.Select(x=>FireBoltChecks.Target(owner,x)).Cast<BaseCreature>().ToArray();
        targets[5].Controlled=true;targets[5].ControlMaster=owner;Require(!ClassCombat.IsHostile(owner,targets[5]),"Friendly exclusion fixture");
        targets[6].MoveToWorld(new(targets[6].X,targets[6].Y,center.Z+16),owner.Map);
        var walls=Enumerable.Range(-8,17).Select(dy=>{var wall=new Static(0x80);wall.MoveToWorld(new(center.X-2,center.Y+dy,center.Z),owner.Map);return wall;}).ToArray();
        Area(owner,targets,center);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,targets.Select(t=>t.Serial.Value).ToArray(),walls.Select(w=>w.Serial.Value).ToArray(),center,owner.Location,owner.Map.MapID,0)));
        Log.Information("Apocalypse area:6 current enumeration/filter contexts,255 radii;actual ground-origin LOS opposite caster LOS,13/15/17subtile thresholds,uniform shared HP damage,ally/height/facet guards and persistent original fixtures. Native map/hostility/collision projection reused;D2 room-list order not certified.");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.OwnerPoint,Map.Maps[saved.Map]);
        Require(saved.Generation<2&&saved.Walls.All(id=>World.FindItem((Serial)id)is{Deleted:false}),"Area fixture walls/reload count");
        Area(owner,saved.Targets.Select(id=>(BaseCreature)World.FindMobile((Serial)id)).ToArray(),saved.Center);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
