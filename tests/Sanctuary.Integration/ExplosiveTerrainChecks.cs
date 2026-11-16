// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

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
            ConsoleInputHandler.RegisterCommand(name,"Approved shared Fire Ball/Glacial terrain adapter",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
        foreach(var ability in new[]{CombatAbility.Fireball,CombatAbility.GlacialSpike})
        foreach(bool quick in new[]{false,true})
        {
            var selected=ability;bool q=quick;string label="explosive-terrain-"+(ability==CombatAbility.Fireball?"fireball":"glacial")+(quick?"-quick":"-right");
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual approved explosive ground input and native wall",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,selected,q,label));Log.Information("{Name}: passed",label+"-check");}catch(Exception e){Log.Error(e,"{Name}: failed",label+"-check");}}));
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
    private static void Door(CombatAbility ability,bool closes)
    {
        var p=Owner(ability);var near=Enemy(p,3,1);var behind=Enemy(p,6,0);var door=new MetalDoor(DoorFacing.NorthCW);
        try
        {
            var point=new Point3D(p.X+4,p.Y,p.Z);door.MoveToWorld(point,p.Map);
            Require(!door.Open&&LobProjectiles.Barrier(p.Map,point),"Native closed-door barrier metadata");
            door.Open=closes;long id=SorceressSpellMissiles.Commit(Flight(p,ability));FireBoltChecks.Through(id,3);
            Require(FireBoltChecks.State(id)!=null,"Flight ended before dynamic door transition");
            int a=SkillHealth.Balance256(near),b=SkillHealth.Balance256(behind);door.Open=!closes;Finish(id);
            Require(closes?SkillHealth.Balance256(near)<a&&SkillHealth.Balance256(behind)==b:SkillHealth.Balance256(behind)<b,
                "Approved terrain ignored current door state: "+ability+" closes="+closes);
        }
        finally{door.Delete();FireBoltChecks.Clean(p,near,behind);}
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
        Require(SorceressSpellMissiles.ExplosiveTerrainApproved,"Approved terrain mapping is disabled");
        Saved[] saved;
        if(reload){saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)).Select(Reload).ToArray();}
        else
        {
            foreach(var ability in new[]{CombatAbility.Fireball,CombatAbility.GlacialSpike})
            {
                foreach(var direction in new[]{(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)}){Wall(ability,direction.Item1,direction.Item2);}
                Open(ability,false);Open(ability,true);Door(ability,false);Door(ability,true);
            }
            saved=[Save(CombatAbility.Fireball),Save(CombatAbility.GlacialSpike)];
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Approved explosive terrain:shared lob wall/LOS/last-clear adapter;16 directional walls,4dynamic door transitions,open copied contacts/expiry,Fire Ball versus Glacial freeze,original held flights/resources and resumed copies;ordinary routes use the approved mapping");
    }
    private static void Input(NetState network,CombatAbility ability,bool quick,string label)
    {
        var p=Owner(ability);p.Female=quick;p.Body=quick?0x191:0x190;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Approved terrain input bindings");
        var near=Enemy(p,3,1);var behind=Enemy(p,5,0);var wall=new Static(0x80);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        var account=FixtureNetwork.Authenticate(network,p,"explosive-terrain-wire-");
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        int a=SkillHealth.Balance256(near),b=SkillHealth.Balance256(behind);bool began=false,done=false;long deadline=Core.TickCount+8000;
        void Cleanup(){wall.Delete();ClassCombat.Stop(p);FireBoltChecks.Clean(p,near,behind);account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Approved terrain input timeout: "+label);
                if(!began&&ClassCombat.For(p).PendingAbility==ability)
                {Require(NecromancerEffects.ManaBalance256(p)==mana,"Terrain wire paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(near)<a)
                {
                    Require(began&&SkillHealth.Balance256(behind)==b&&NecromancerEffects.ManaBalance256(p)==mana-cost
                        &&!SorceressSpellMissiles.Snapshot().Flights.Any(f=>f.Owner==p.Serial.Value),"Terrain wire lost wall occlusion,once-only debit or termination");
                    Require(ability==CombatAbility.GlacialSpike?NamedControl.State(near)?.Freeze is{Removed:false}:NamedControl.State(near)?.Freeze==null,"Terrain wire changed freeze semantics");
                    Require(NamedControl.State(behind)?.Freeze==null,"Terrain wire froze occluded enemy");
                    done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Sorceress,p.X+3,p.Y,p.Z);Inspect();}));
    }
}
