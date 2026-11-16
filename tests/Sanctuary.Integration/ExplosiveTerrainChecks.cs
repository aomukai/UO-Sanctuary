// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ExplosiveTerrainChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ExplosiveTerrainChecks));
    private sealed record Saved(uint Owner,uint Near,uint Behind,uint Wall,Point3D Point,int Mana,int NearLife,int BehindLife,SorceressSpellFlight Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"explosive-terrain-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"explosive-terrain-reload-check":"explosive-terrain-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared shared Fire Ball/Glacial terrain adapter",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
    }
    private static PlayerMobile Owner(CombatAbility ability)
    {
        var p=new PlayerMobile{Name="ExplosiveTerrainFixture",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Sorceress);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);FixtureSpace.Corridor(p,6700,40,1000);
        NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x,int y)
    {var t=FireBoltChecks.Target(p,0);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);return t;}
    private static SorceressSpellFlight Flight(PlayerMobile p,CombatAbility ability,int dx=1,int dy=0)
        =>SorceressSpellMissiles.Prepare(p,new(p.X+10*dx,p.Y+10*dy,p.Z),SkillMechanics.Values(p,ability),TimedEffects.Now)
            with{Minimum256=1280,Maximum256=1280};
    private static void Finish(long id)
    {FireBoltChecks.Through(id,60);Require(FireBoltChecks.State(id)==null,"Prepared explosive did not terminate");}
    private static void Wall(CombatAbility ability,int dx,int dy)
    {
        var p=Owner(ability);GraveyardSkeleton near=null,behind=null;var wall=new Static(0x80);
        try
        {
            wall.MoveToWorld(new(p.X+4*dx,p.Y+4*dy,p.Z),p.Map);
            Require(LobProjectiles.Barrier(p.Map,wall.Location),"Shared lob wall metadata control");
            long id=SorceressSpellMissiles.Commit(Flight(p,ability,dx,dy));
            (double X,double Y,bool Landed) impact=default;
            for(int frame=0;frame<50;frame++)
            {
                var flight=FireBoltChecks.State(id);Require(flight!=null,"Empty flight ended before test wall");
                impact=LobProjectiles.Advance(p.Map,flight.X,flight.Y,flight.Z,flight.Vx,flight.Vy,int.MaxValue);
                if(impact.Landed){break;}
                SorceressSpellMissiles.Tick(flight.NextUtc,true);
            }
            Require(impact.Landed,"Shared adapter never reached test wall");
            // Introduce recipients only for the terminal update: they cannot
            // turn this terrain test into an earlier direct-monster collision.
            near=Enemy(p,(int)impact.X-p.X,(int)impact.Y-p.Y);behind=Enemy(p,5*dx,5*dy);
            int a=SkillHealth.Balance256(near),b=SkillHealth.Balance256(behind),mana=NecromancerEffects.ManaBalance256(p);
            Finish(id);
            Require(SkillHealth.Balance256(near)<a&&SkillHealth.Balance256(behind)==b,
                "Prepared explosion crossed wall or missed visible side: "+ability+" direction="+dx+","+dy+" nearLoss="+(a-SkillHealth.Balance256(near))+" behindLoss="+(b-SkillHealth.Balance256(behind)));
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Terrain callback charged mana");
            Require(ability==CombatAbility.GlacialSpike?NamedControl.State(near)?.Freeze is{Removed:false}:NamedControl.State(near)?.Freeze==null,"Terrain changed source freeze semantics");
            Require(NamedControl.State(behind)?.Freeze==null,"Terrain froze wall-occluded target");
        }
        finally{wall.Delete();FireBoltChecks.Clean(p,new[]{near,behind}.Where(t=>t!=null).Cast<Mobile>().ToArray());}
    }
    private static void Open(CombatAbility ability,bool expiry)
    {
        var p=Owner(ability);int distance=expiry?(ability==CombatAbility.Fireball?23:14):6;
        var near=Enemy(p,distance,expiry?1:0);var peer=Enemy(p,distance,expiry?-1:0);
        try
        {
            int a=SkillHealth.Balance256(near),b=SkillHealth.Balance256(peer);long id=SorceressSpellMissiles.Commit(Flight(p,ability));Finish(id);
            Require(a-SkillHealth.Balance256(near)==1280&&b-SkillHealth.Balance256(peer)==1280,"Prepared open contact/expiry changed copied packet: "+ability+" expiry="+expiry+" firstLoss="+(a-SkillHealth.Balance256(near))+" peerLoss="+(b-SkillHealth.Balance256(peer)));
        }
        finally{FireBoltChecks.Clean(p,near,peer);}
    }
    private static Saved Save(CombatAbility ability)
    {
        var p=Owner(ability);var near=Enemy(p,3,1);var behind=Enemy(p,5,0);var wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        long id=SorceressSpellMissiles.Commit(Flight(p,ability));FireBoltChecks.Through(id,3);
        var held=FireBoltChecks.State(id) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        var pool=SorceressSpellMissiles.Snapshot();SorceressSpellMissiles.Restore(pool with{Flights=pool.Flights.Select(f=>f.Id==id?held:f).ToArray()});
        var saved=new Saved(p.Serial.Value,near.Serial.Value,behind.Serial.Value,wall.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(near),SkillHealth.Balance256(behind),held,0);
        p.Internalize();return saved;
    }
    private static Saved Reload(Saved s)
    {
        var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var near=(GraveyardSkeleton)World.FindMobile((Serial)s.Near);var behind=(GraveyardSkeleton)World.FindMobile((Serial)s.Behind);var wall=World.FindItem((Serial)s.Wall);
        Require(s.Generation<2&&FireBoltChecks.State(s.Flight.Id)==s.Flight&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&SkillHealth.Balance256(near)==s.NearLife&&SkillHealth.Balance256(behind)==s.BehindLife&&wall is{Deleted:false},"Original terrain flight/actors/resources/wall did not retain");
        p.MoveToWorld(s.Point,Map.Trammel);
        var a=Enemy(p,3,-1);var b=Enemy(p,5,0);near.Internalize();behind.Internalize();
        try
        {
            int life=SkillHealth.Balance256(a),hidden=SkillHealth.Balance256(b);var pool=SorceressSpellMissiles.Snapshot();
            var copy=s.Flight with{Id=pool.NextId+1,NextUtc=TimedEffects.Now};SorceressSpellMissiles.Commit(copy);Finish(copy.Id);
            Require(SkillHealth.Balance256(a)<life&&SkillHealth.Balance256(b)==hidden&&FireBoltChecks.State(s.Flight.Id)==s.Flight,"Resumed copy crossed wall or rewrote original held flight");
            Require(NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(near)==s.NearLife&&SkillHealth.Balance256(behind)==s.BehindLife,"Resumed copy healed/reset original resources");
        }
        finally{a.Delete();b.Delete();near.MoveToWorld(new(p.X+3,p.Y+1,p.Z),p.Map);behind.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);p.Internalize();}
        return s with{Generation=s.Generation+1};
    }
    private static void Check(bool reload)
    {
        Require(!SorceressSpellMissiles.PreparedExplosiveTerrain,"Prepared terrain proposal leaked into normal routes");
        SorceressSpellMissiles.PreparedExplosiveTerrain=true;
        try
        {
            Saved[] saved;
            if(reload){saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)).Select(Reload).ToArray();}
            else
            {
                foreach(var ability in new[]{CombatAbility.Fireball,CombatAbility.GlacialSpike})
                {
                    foreach(var direction in new[]{(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)}){Wall(ability,direction.Item1,direction.Item2);}
                    Open(ability,false);Open(ability,true);
                }
                saved=[Save(CombatAbility.Fireball),Save(CombatAbility.GlacialSpike)];
            }
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
            Log.Information("Prepared explosive terrain:shared lob wall/LOS/last-clear adapter;16 directional walls,open copied contacts/expiry,Fire Ball versus Glacial freeze,original held flights/resources and resumed copies;default routes remain unchanged");
        }
        finally{SorceressSpellMissiles.PreparedExplosiveTerrain=false;}
    }
}
