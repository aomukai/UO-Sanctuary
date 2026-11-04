// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ChainChildSearchChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChainChildSearchChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("chain-child-search-check","Shared player/NPC chain corner acquisition and segment terrain",_=>Core.LoopContext.Post(()=>
    {try{foreach(bool npc in new[]{false,true})foreach(bool obstruct in new[]{false,true}){Corner(npc,obstruct);}Log.Information("chain-child-search-check: passed");}catch(Exception e){Log.Error(e,"chain-child-search-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Square(PlayerMobile p,int column)
    {
        int floor=Enumerable.Range(0x500,0x100).First(id=>TileData.ItemTable[id].Surface&&!TileData.ItemTable[id].Impassable&&TileData.ItemTable[id].CalcHeight==0);
        for(int y=3500;y<3800;y+=16)
        {
            bool clear=true;for(int x=column-2;x<=column+12;x++)for(int dy=-2;dy<=10;dy++){if(!Map.Trammel.CanFit(x,y+dy,0,16,true,true,false)){clear=false;}}
            if(!clear){continue;}
            for(int x=column-2;x<=column+12;x++)for(int dy=-2;dy<=10;dy++){new Static(floor).MoveToWorld(new(x,y+dy,0),Map.Trammel);}
            p.MoveToWorld(new(column,y,0),Map.Trammel);return;
        }
        throw new InvalidOperationException("No clear chain corner square");
    }
    private static BaseCreature Enemy(PlayerMobile p,Point3D point)
    {var t=ChargedStrikeChecks.Enemy(p);t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(point,p.Map);return t;}
    private static bool Los(Mobile from,Mobile to)=>from.Map.LineOfSight(new Point3D(from.X,from.Y,from.Z+12),new Point3D(to.X,to.Y,to.Z+12));
    private static void Corner(bool npc,bool obstruct)
    {
        var p=npc?MagePetBuffChecks.Owner(10,6500,3500):FireBoltChecks.Owner(CombatAbility.ChargedBolt,6400);Square(p,npc?6500:6400);
        MageWorldBody mage=null;Mobile caster=p;
        if(npc){mage=MagePetBuffChecks.Pet(p,3);caster=mage;}
        else{Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.lightning",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.chainlightning",out _),"Chain corner training");}
        var first=Enemy(p,new(caster.X+4,caster.Y,caster.Z));var second=Enemy(p,new(caster.X+4,caster.Y+6,caster.Z));
        var corner=new Static(0x80);corner.MoveToWorld(new(caster.X+1,caster.Y+2,caster.Z),p.Map);var segments=new System.Collections.Generic.List<Static>();
        try
        {
            Require(!Los(caster,second)&&Los(caster,first)&&Los(first,second),"Corner fixture must block caster LOS while leaving both segments clear");
            if(obstruct){for(int x=first.X-3;x<=first.X+2;x++){var segment=new Static(0x80);segment.MoveToWorld(new(x,first.Y+3,first.Z),p.Map);segments.Add(segment);}Require(!Los(first,second),"Segment obstruction fixture");}
            long id;
            if(npc)
            {
                id=ChainLightning.CastNpc(mage,first.Location,MagePetBuffs.Values(CombatAbility.ChainLightning,10,mage.Level),TimedEffects.Now);
                var f=ChainLightning.Snapshot().Single(x=>x.Cast==id);ChainLightning.Restore(ChainLightning.Snapshot().Where(x=>x.Cast!=id).Append(f with{Remaining=2}).ToArray());
            }
            else{id=ChainLightning.CommitPlayer(ChainLightning.PreparePlayer(p,first.Location,SkillMechanics.Values(p,CombatAbility.ChainLightning),TimedEffects.Now)with{Remaining=2});}
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(second);bool selected=false;var trace=new System.Collections.Generic.List<string>();
            for(int i=0;i<70;i++)
            {
                var f=ChainLightning.Snapshot().SingleOrDefault(x=>x.Cast==id);if(f==null){break;}
                trace.Add($"{f.Frame}:{f.X:0.00},{f.Y:0.00}/left{f.Remaining}/aim{f.AimTarget}");ChainLightning.Tick(f.NextUtc,true,id);var child=ChainLightning.Snapshot().SingleOrDefault(x=>x.Cast==id);
                if(child?.AimTarget==second.Serial.Value){selected=true;}
            }
            Require(SkillHealth.Balance256(first)<a&&selected,"Shared child incorrectly required caster LOS instead of selecting a nearby corner target");
            Require(obstruct?SkillHealth.Balance256(second)==b:SkillHealth.Balance256(second)<b,$"Child terrain npc={npc},obstruct={obstruct},firstLoss={a-SkillHealth.Balance256(first)},secondLoss={b-SkillHealth.Balance256(second)},caster={caster.Location},first={first.Location},second={second.Location},trace={string.Join(";",trace)}");
            Require(!ChainLightning.Snapshot().Any(x=>x.Cast==id),"Corner chain exceeded finite count/lifetime");
        }
        finally{foreach(var segment in segments){segment.Delete();}corner.Delete();ChainLightning.Cancel(p);mage?.Delete();first.Delete();second.Delete();p.Delete();}
    }
}
