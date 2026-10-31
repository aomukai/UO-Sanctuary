// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoneBarrierBirthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BoneBarrierBirthChecks));
    private sealed record Saved(uint Owner,uint Body,BarrierBirthState State,int Life,DateTime Until,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-barrier-birth-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("bone-barrier-birth-check",Check);Register("bone-barrier-birth-reload-check",Reload);Register("bone-barrier-birth-save",StageSave);
        Register("bone-wall-birth-right-check",()=>FixtureNetwork.Connected("bone-wall-birth-right-result",n=>Input(n,CombatAbility.BoneWall,false)));
        Register("bone-prison-birth-quick-check",()=>FixtureNetwork.Connected("bone-prison-birth-quick-result",n=>Input(n,CombatAbility.BonePrison,true)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Bone barrier source birth and exact resources",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player();p.Name="BoneBarrierBirthFixture";p.AddItem(new Backpack());while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Barrier fixture strength");}FixtureSpace.Corridor(p,7130,24,3500);return p;
    }
    private static BoneBarrierBody[] Bodies(PlayerMobile p)
    {var result=new List<BoneBarrierBody>();foreach(var body in p.Map.GetMobilesInRange<BoneBarrierBody>(p.Location,24)){if(body.SummonMaster==p){result.Add(body);}}return result.ToArray();}
    private static bool Tiles(Map map,Point3D point)
    {foreach(var tile in map.GetItemsInRange<BoneBarrierTile>(point,8)){return true;}return false;}
    private static void Clean(PlayerMobile p){foreach(var b in Bodies(p)){b.Delete();}p.Delete();}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BoneBarrierBody Spawn(PlayerMobile p,CombatAbility a)=> (BoneBarrierBody)BoneBarrierSegment.Spawn(p,a,new(p.X+6,p.Y,p.Z),SkillMechanics.Values(p,a),null);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bone-barrier-birth-source-cases.json")));int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();var a=V(0)==78?CombatAbility.BoneWall:CombatAbility.BonePrison;
            var n=BoneBarrierBirth.Prepare(a,V(1),V(3),V(4),(MonsterDifficulty)V(2));
            Require(n.Life256==V(6)&&V(6)==V(7)&&n.Regeneration256==V(8)&&n.Level==V(9)&&n.Defense==V(10),"Complete barrier passive/MAXHP callback "+count);
            Require(V(11)==Math.Clamp(3*V(1),1,V(5)),"Source requested equipment level");count++;
        }
        Require(count==21420,"Incomplete bone barrier source matrix");
        Require(BoneBarrierBirth.Prepare(CombatAbility.BoneWall,4,1,1,MonsterDifficulty.Nightmare) is{Life256:73382,Regeneration256:35,Level:40,Defense:174,Cold:90,Poison:200},"Nightmare wall pinned values");
        Require(BoneBarrierBirth.Prepare(CombatAbility.BonePrison,4,1,1,MonsterDifficulty.Hell) is{Life256:210741,Regeneration256:102,Level:73,Defense:334,Physical:50},"Hell prison pinned values");
    }
    private static void Recovery(BoneBarrierBody b)
    {
        long at=Core.TickCount+10000;int max=SkillHealth.Maximum256(b),rate=b.Prepared.Numbers.Regeneration256;
        SkillHealth.SetBalance256(b,max-100);b.ResetInspectedRecovery();b.InspectRecovery(at);b.InspectRecovery(at+39);
        Require(SkillHealth.Balance256(b)==max-100,"Barrier recovered before a full source frame");b.InspectRecovery(at+40);
        Require(SkillHealth.Balance256(b)==max-100+rate,"Fractional barrier recovery rate");b.InspectRecovery(at+100000);
        Require(SkillHealth.Balance256(b)<=max-100+26*rate,"Barrier repaid an inactive clock gap");
        TimedEffects.PreventHealing(b,TimedEffects.Now);int hp=SkillHealth.Balance256(b);b.InspectRecovery(at+100040);Require(SkillHealth.Balance256(b)==hp,"Barrier PMH ignored");
        TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=b.Serial.Value).ToArray());b.ResetInspectedRecovery();
        SkillHealth.SetBalance256(b,max-1);b.InspectRecovery(at+200000);b.InspectRecovery(at+200040);Require(SkillHealth.Balance256(b)==max,"Barrier healing exceeded or missed fractional maximum");
    }
    private static void Check()
    {
        Source();var p=Owner();var facets=SummonDifficulties.Facets();
        try
        {
            foreach(var a in new[]{CombatAbility.BoneWall,CombatAbility.BonePrison})
            {
                var b=Spawn(p,a);var expected=BoneBarrierBirth.Capture(SkillMechanics.Values(p,a),MonsterDifficulty.Normal);
                Require(b.Prepared==expected&&SkillHealth.Balance256(b)==expected.Numbers.Life256&&b.HitsMax==(expected.Numbers.Life256+255)/256&&b.SourceLevel==7,"Exact learned barrier birth/ceil/level");
                Recovery(b);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/2+7);int hp=SkillHealth.Balance256(b),max=SkillHealth.Maximum256(b);var birthPoint=b.Location;var map=b.Map;var until=b.ExpiresUtc;
                foreach(var d in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell,MonsterDifficulty.Normal})
                {
                    SummonDifficulties.SetFacets(facets.Where(x=>x.Map!=Map.Felucca.MapID).Append(new(Map.Felucca.MapID,d)).ToArray());
                    p.MoveToWorld(new(p.X,p.Y,p.Z),Map.Felucca);
                    var next=BoneBarrierBirth.Prepare(a,1,1,1,d);int wanted=SummonDifficulties.Life256(hp,max,next.Life256);
                    Require(b.Prepared.Difficulty==d&&b.Prepared.Numbers==next&&SkillHealth.Balance256(b)==wanted&&b.Location==birthPoint&&b.Map==map&&b.ExpiresUtc==until,"Owner facet changed wounds, lifetime or moved the barrier");
                    Require(b.SourceLevel==next.Level&&b.SourceDefense==next.Defense&&b.PhysicalResistance==next.Physical&&b.ColdResistance==next.Cold&&b.PoisonResistance==next.Poison,"Difficulty stats did not reach native barrier consumers");
                    hp=wanted;max=next.Life256;p.MoveToWorld(new(p.X,p.Y,p.Z),map);
                    // The return transition also rescales to the configured original facet.
                    hp=SkillHealth.Balance256(b);max=SkillHealth.Maximum256(b);
                }
                b.Delete();
            }
            var direct=Spawn(p,CombatAbility.BoneWall);var old=direct.Prepared;
            PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.BoneWall)==2,"Effective barrier item rank");
            var soft=BoneBarrierBirth.Capture(SkillMechanics.Values(p,CombatAbility.BoneWall),MonsterDifficulty.Normal);
            Require(soft.Rank==2&&soft.Armor==1&&soft.Other==1&&direct.Prepared==old,"Soft synergy bonus or existing recipe changed retroactively");direct.Delete();
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var point=new Point3D(p.X+6,p.Y,p.Z);
            Require(ClassCombat.Perform(p,CombatAbility.BoneWall,null,point,Core.TickCount)&&Bodies(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Barrier created/paid during windup");
            long effect=ClassCombat.For(p).EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,effect)&&Bodies(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled barrier windup created/paid");p.MoveToWorld(origin,p.Map);
        }
        finally{SummonDifficulties.SetFacets(facets);Clean(p);}
    }
    private static void StageSave()
    {
        int generation=File.Exists(Manifest)?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Generation+1:0;
        var p=Owner();var b=Spawn(p,generation%2==0?CombatAbility.BoneWall:CombatAbility.BonePrison);
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,b.Prepared,SkillHealth.Balance256(b),b.ExpiresUtc,generation)));
        World.Save();
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body) as BoneBarrierBody;
        Require(b is{Alive:true,Deleted:false}&&b.Prepared==s.State&&SkillHealth.Balance256(b)==s.Life&&b.ExpiresUtc==s.Until&&b.SummonMaster?.Serial.Value==s.Owner,"Live saved barrier recipe/wounds/deadline/owner changed");
        Require(b.Tiles.Count==1&&b.Tiles.All(t=>!t.Deleted&&t.Segment==b&&t.Map==b.Map),"Saved barrier collision link");
        b.InspectRecovery(Core.TickCount+1000);Require(SkillHealth.Balance256(b)==s.Life,"Reload granted healing despite retained PMH");
    }
    private static void Input(NetState network,CombatAbility ability,bool quick)
    {
        var p=Owner();Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Barrier input bindings");Ready(p);
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);var at=ability==CombatAbility.BoneWall?new Point3D(p.X+6,p.Y,p.Z):target.Location;
        string label=quick?"bone-prison-birth-quick":"bone-wall-birth-right";int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        var account=FixtureNetwork.Authenticate(network,p,"barrier-birth-");bool began=false,finished=false;long timeout=Core.TickCount+35000;
        void End(){target.Delete();Clean(p);account.Delete();}
        void Fail(Exception e){if(finished){return;}finished=true;End();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            if(finished){return;}
            try
            {
                Require(Core.TickCount<timeout,"Barrier input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==ability&&!began){Require(Bodies(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input created/paid during windup");began=true;Log.Information("{Label}: windup",label);}
                var parts=Bodies(p);if(parts.Length==0){Require(!began||session.PendingAbility==ability,"Cast ended without allocating a barrier");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                var body=parts[0];var expected=BoneBarrierBirth.Capture(SkillMechanics.Values(p,ability),MonsterDifficulty.Normal);
                Require(began&&parts.All(b=>b.Prepared==expected&&SkillHealth.Balance256(b)==expected.Numbers.Life256)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual input recipe/resource/once payment");ClassCombat.Stop(p);
                Require((body.ExpiresUtc-TimedEffects.Now).TotalMilliseconds is >23900 and <=24000,"Barrier source24second lifetime");var until=body.ExpiresUtc;
                void Expired()
                {
                    if(finished){return;}if(TimedEffects.Now<until.AddMilliseconds(100)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));return;}
                    try{Require(Bodies(p).Length==0&&!Tiles(p.Map,at),"Barrier expiry left bodies or collision tiles");finished=true;End();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Expired));
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,ability==CombatAbility.BoneWall?0:target.Serial.Value,at.X,at.Y,at.Z);Inspect();});
    }
}
