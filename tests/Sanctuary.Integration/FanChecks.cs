// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary.Integration;

public static class FanChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FanChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("fan-check","Compiled do8 differential and actual Teeth cursor fan",_=>Core.LoopContext.Post(()=>
    {try{Differential();Teeth();Log.Information("fan-check: passed");}catch(Exception ex){Log.Error(ex,"fan-check: failed");}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Differential()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fan-source-probe.json")));
        int count=0,rays=0;
        foreach(var row in source.RootElement.GetProperty("fanCases").EnumerateArray())
        {
            var actual=FanGeometry.Create(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32(),row[3].GetInt32(),row[4].GetInt32());
            var expected=row[7];Require(actual.Length==expected.GetArrayLength(),"Source fan count");
            for(int i=0;i<actual.Length;i++)
            {
                var ray=actual[i];var golden=expected[i];
                Require(ray.X==golden[0].GetInt32() && ray.Y==golden[1].GetInt32() && ray.Flags==golden[2].GetUInt32()
                    && ray.ActivationFrame==golden[4].GetInt32(),$"Source fan aim/flags/activation at{count}:{i}");rays++;
            }
            count++;
        }
        Require(count==3468,"Incomplete source fan fixture");
        var self=FanGeometry.Create(0,0,24);
        foreach(var ray in self){Require(ray.X==0 && ray.Y==0 && FanGeometry.Velocity(ray,16)==MissileVectors.Velocity(1,1,16),"Zero-target factory fallback");}
        var teeth=SkillMechanics.For(CombatAbility.Teeth);var multi=SkillMechanics.For(CombatAbility.MultipleShot);
        for(int rank=1;rank<=40;rank++)
        {
            foreach(var row in new[]{teeth,multi})
            {
                var values=new SkillValues(row,_=>rank,_=>0,99);
                var plan=FanGeometry.Create(20,0,(int)values.Eval("calc1"),(int)values.Eval("calc3"),(int)values.Eval("calc2"));
                int central=0;foreach(var ray in plan){if((ray.Flags&0x10000)==0){central++;}}
                Require(plan.Length==Math.Min(rank+1,24) && central==(row==multi?2:plan.Length)
                    && plan[0].ActivationFrame==(row==multi?1:0),"Pinned skill fan count/cap/central/activation");
            }
        }
        Log.Information("Shared do8 C# differential: {Cases} source cases / {Rays} emitted aims; rank1..40 Teeth/Multiple Shot fields",count,rays);
    }
    private static void Teeth()
    {
        var owner=ClassSkillChecks.Player();owner.Name="FanFixture";
        while(ClassCreation.Profiles.GetRank(owner.Serial,"necromancer.teeth")<20)
        {Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.teeth",out _),"Train Teeth20");}
        bool found=false;
        for(int x=2200;x<2300 && !found;x++)for(int y=1000;y<1100 && !found;y++)
        {
            var point=new Point3D(x,y,owner.Map.GetAverageZ(x,y));if(SpellHelper.IsTown(point,owner)){continue;}
            bool clear=true;
            for(int a=0;a<=10 && clear;a++)for(int b=0;b<=6 && clear;b++)
            {
                var cell=new Point3D(x+a,y+b,point.Z);
                clear=owner.Map.CanSpawnMobile(cell) && owner.Map.LineOfSight(new Point3D(x,y,point.Z+12),new Point3D(cell.X,cell.Y,point.Z+12));
            }
            if(clear){foreach(var other in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(other!=owner){clear=false;break;}}}
            if(clear){owner.MoveToWorld(point,owner.Map);found=true;}
        }
        if(!found){owner.Delete();throw new InvalidOperationException("No independent open fan terrain");}
        var enemy=new GraveyardSkeleton{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.AIObject.AITimer.Stop();enemy.SetHits(50000);
        enemy.MoveToWorld(new(owner.X+1,owner.Y+4,owner.Z),owner.Map);int before=enemy.Hits;
        void Cast(int distance)
        {
            owner.Mana=owner.ManaMax;int mana=NecromancerEffects.ManaBalance256(owner);
            var values=SkillMechanics.Values(owner,CombatAbility.Teeth);
            Require(NecromancerCombat.Release(owner,CombatAbility.Teeth,Serial.Zero,new(owner.X+distance,owner.Y,owner.Z)),"Teeth release");
            Require(NecromancerEffects.ManaBalance256(owner)==mana-values.Mana256,"Fan paid more than once");
        }
        try{Cast(10);}catch{owner.Delete();enemy.Delete();throw;}
        Timer.StartTimer(TimeSpan.FromSeconds(2.2),()=>
        {
            try
            {
                Require(enemy.Hits==before,"Far cursor fan incorrectly reached steep off-axis target");Cast(1);
                Timer.StartTimer(TimeSpan.FromSeconds(2.2),()=>
                {
                    try{Require(enemy.Hits<before,"Near cursor Teeth fan failed actual off-axis collision/damage");Log.Information("fan-result: passed");}
                    catch(Exception ex){Log.Error(ex,"fan-result: failed");}
                    finally{owner.Delete();enemy.Delete();}
                });
            }
            catch(Exception ex){Log.Error(ex,"fan-result: failed");owner.Delete();enemy.Delete();}
        });
    }
}
