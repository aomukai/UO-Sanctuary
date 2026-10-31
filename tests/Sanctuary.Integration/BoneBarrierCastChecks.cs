// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
namespace Sanctuary.Integration;

public static class BoneBarrierCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneBarrierCastChecks));
    private sealed record Saved(uint Owner,int Mana,CombatAbility Right);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-barrier-cast-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("bone-barrier-cast-check",Check);Register("bone-barrier-cast-reload-check",Reload);
        Register("bone-wall-target-right-check",()=>FixtureNetwork.Connected("bone-wall-target-right-result",n=>BoneBarrierBirthChecks.Input(n,CombatAbility.BoneWall,false,"bone-wall-target-right",selectedWall:true)));
        Register("bone-prison-self-quick-check",()=>FixtureNetwork.Connected("bone-prison-self-quick-result",n=>BoneBarrierBirthChecks.Input(n,CombatAbility.BonePrison,true,"bone-prison-self-quick",selfPrison:true)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Barrier allocation/target/payment callbacks",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BoneBarrierSegment[] Own(PlayerMobile p)
    {var list=new List<BoneBarrierSegment>();foreach(var b in p.Map.GetMobilesInRange<BoneBarrierSegment>(p.Location,24)){if(b.SummonMaster==p){list.Add(b);}}return list.OrderBy(x=>x.Serial.Value).ToArray();}
    private static void Clear(PlayerMobile p){foreach(var b in Own(p)){b.Delete();}}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Allocation(PlayerMobile p,GraveyardSkeleton target)
    {
        var original=BoneBarriers.Allocate;int checkedCases=0;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-barrier-cast-source-cases.json")));
        try
        {
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int V(int i)=>r[i].GetInt32();if(V(1)!=1||V(2)!=1||V(3)!=0||V(5)!=1||V(4) is not(-1 or 0 or 2 or 24)){continue;}
                var ability=V(0)==78?CombatAbility.BoneWall:CombatAbility.BonePrison;int calls=0,fail=V(4);
                BoneBarriers.Allocate=(owner,a,point,values,root)=>++calls<=fail||fail<0?null:original(owner,a,point,values,root);
                Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                var center=ability==CombatAbility.BoneWall?new Point3D(p.X+6,p.Y,p.Z):target.Location;
                bool result=NecromancerCombat.Release(p,ability,ability==CombatAbility.BoneWall?Serial.Zero:target.Serial,center);var bodies=Own(p);
                Require(result==(V(7)!=0)&&bodies.Length==V(9)&&calls==V(8)&&NecromancerEffects.ManaBalance256(p)==mana-(result?cost:0),"Source callback allocation/payment outcome "+checkedCases);
                var positions=r[13].EnumerateArray().ToArray();
                for(int i=0;i<bodies.Length;i++)
                {Require(bodies[i].X==center.X+BoneBarriers.Round(SkillGeometry.Tiles(positions[i][0].GetInt32()))&&bodies[i].Y==center.Y+BoneBarriers.Round(SkillGeometry.Tiles(positions[i][1].GetInt32())),$"Source creation order/root position case={checkedCases} i={i} actual={bodies[i].Location} offset={positions[i]} center={center}");}
                if(bodies.Length>0){bodies[0].Delete();Require(bodies.All(b=>b.Deleted)&&Own(p).Length==0,"First successful root did not remove its children");}
                Clear(p);checkedCases++;
            }
            Require(checkedCases==8,"Incomplete native even-failure callback controls");
        }
        finally{BoneBarriers.Allocate=original;Clear(p);}
    }
    private static void Check()
    {
        Require(ClassAbilities.AllyTarget(CombatAbility.BoneWall)&&ClassAbilities.AllyTarget(CombatAbility.BonePrison),"Shared client/server placement-target metadata");
        var p=ClassSkillChecks.Player();p.Name="PoisonDaggerFixtureBarrierCast";p.AddItem(new Backpack());FixtureSpace.Corridor(p,7130,24,3500);
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);
        try
        {
            Allocation(p,target);
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.ValidTarget(p,CombatAbility.BoneWall,target.Serial,target.Location)&&NecromancerCombat.Release(p,CombatAbility.BoneWall,target.Serial,target.Location),"Occupied Wall target did not retry placement");
            var root=Own(p).Single();Require(root.Location!=target.Location&&root.GetDistanceToSqrt(target)<=3&&NecromancerEffects.ManaBalance256(p)==mana-17*256,"Occupied root placement/payment");Clear(p);
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BonePrison),"Prison right binding");
            foreach(Mobile aim in new Mobile[]{p,target})
            {
                Ready(p);mana=NecromancerEffects.ManaBalance256(p);
                Require(NecromancerCombat.ValidTarget(p,CombatAbility.BonePrison,aim.Serial,aim.Location)&&ClassCombat.ContinueCast(p,CombatAbility.BonePrison,aim,aim.Location,Core.TickCount),"Living target held admission");
                // ContinueCast requires the selected right skill.
                ClassCombat.Stop(p);Require(NecromancerCombat.Release(p,CombatAbility.BonePrison,aim.Serial,aim.Location)&&Own(p).Length>0&&NecromancerEffects.ManaBalance256(p)==mana-27*256,"Self/hostile Prison release/payment");Clear(p);
            }
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(!NecromancerCombat.Begin(p,CombatAbility.BonePrison,Serial.Zero,p.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana,"Prison without a primary spent mana");
            var town=new GuardedRegion("BarrierCastFixtureTown",p.Map,1000,new Rectangle2D(p.X-1,p.Y-1,3,3));town.Register();
            try{Require(!NecromancerCombat.ValidTarget(p,CombatAbility.BoneWall,target.Serial,target.Location)&&!NecromancerCombat.ValidTarget(p,CombatAbility.BonePrison,target.Serial,target.Location),"Caster town gate ignored");}
            finally{town.Unregister();}
            // Exact/occupied/fully blocked checks exercise the extracted service
            // through the existing Wolf consumer as well as the new barrier path.
            Require(DruidSpiritWolfCasting.Placement(p,target.Location,out var wolf)&&wolf!=target.Location,"Shared Wolf fallback changed");
            var walls=new List<Static>();
            try
            {
                var aim=new Point3D(p.X+14,p.Y,p.Z);
                for(int dx=-2;dx<=2;dx++)for(int dy=-2;dy<=2;dy++){var wall=new Static(0x80);wall.MoveToWorld(new(aim.X+dx,aim.Y+dy,p.Z),p.Map);walls.Add(wall);}
                Require(!DruidSpiritWolfCasting.Placement(p,aim,out _),"Fully blocked shared placement succeeded");
            }
            finally{foreach(var wall in walls){wall.Delete();}}
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BonePrison),"Saved Prison binding");Ready(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),CombatAbility.BonePrison)));p.Internalize();
        }
        finally{Clear(p);target.Delete();}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        Require(p!=null&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillBindings.Instance.Get(p).Right==s.Right,"Saved barrier cast capability/mana changed");
    }
}
