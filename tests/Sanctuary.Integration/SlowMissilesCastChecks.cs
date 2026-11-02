// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary.Integration;

public static class SlowMissilesCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SlowMissilesCastChecks));
    private sealed record Saved(uint Owner,uint Target,int Mana,SlowMissilesState State,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-cast-fixtures.json");
    private static bool _creating;
    private static void FreezeMana()
    {var previous=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="SlowCastFixture"?TimeSpan.FromHours(1):previous?.Invoke(p)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)] public static void Initialize()=>FreezeMana();
    public static void Configure()
    {FreezeMana();Register("slow-missiles-cast-check",Check);Register("slow-missiles-cast-reload-check",Reload);Register("slow-missiles-restore-check",AtomicRestore);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Slow Missiles caster search/payment and atomic state restore",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Amazon);}finally{_creating=false;}p.Name="SlowCastFixture";p.AddItem(new Backpack());
        for(int x=2200;x<2900;x+=4)for(int y=1000;y<1450;y+=4)
        {
            int z=Map.Trammel.GetAverageZ(x,y);var point=new Point3D(x,y,z);
            if(!Map.Trammel.CanSpawnMobile(x,y,z)||SpellHelper.IsTown(point,p)){continue;}
            bool occupied=false;foreach(var other in Map.Trammel.GetMobilesInRange<Mobile>(point,22)){occupied=true;break;}
            if(occupied){continue;}
            p.MoveToWorld(point,Map.Trammel);return p;
        }
        throw new InvalidOperationException("No independent Slow Missiles cast area");
    }
    private static EncounterCreature Enemy(PlayerMobile p,int dx,int dy=0)
    {
        var c=new EncounterCreature{CantWalk=true,NoKillAwards=true};c.AIObject.AITimer.Stop();c.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);return c;
    }
    private static void AtomicRestore()
    {
        var p=Owner();var target=Enemy(p,3);var original=AmazonSlowMissiles.Snapshot();
        try
        {
            Require(AmazonSlowMissiles.Apply(p,target,1,TimedEffects.Now),"Atomic restore seed");var valid=AmazonSlowMissiles.Snapshot();
            foreach(var bad in new[]{valid.Take(valid.Length-1).Append(valid[^1] with{Rank=0}).ToArray(),valid.Append(valid[^1]).ToArray(),valid.Prepend(null).ToArray()})
            {
                bool refused=false;try{AmazonSlowMissiles.Restore(bad);}catch(InvalidDataException){refused=true;}
                Require(refused&&valid.SequenceEqual(AmazonSlowMissiles.Snapshot()),"Slow Missiles restore was not atomic");
            }
            bool missing=false;try{AmazonSlowMissiles.Restore(null);}catch(InvalidDataException){missing=true;}
            Require(missing&&valid.SequenceEqual(AmazonSlowMissiles.Snapshot()),"Null restore changed Slow Missiles states");
        }
        finally{AmazonSlowMissiles.Restore(original);target.Delete();p.Delete();}
    }
    private static void Check()
    {
        var p=Owner();var target=Enemy(p,3);var outside=Enemy(p,14);DateTime at=TimedEffects.Now;
        Require(!ClassCombat.Available(p,CombatAbility.SlowMissiles),"Prepared cast enabled incomplete talent");
        Require(AmazonSlowMissilesCast.SourceAllowed(true,false)&&!AmazonSlowMissilesCast.SourceAllowed(true,true)
            &&!AmazonSlowMissilesCast.SourceAllowed(false,false),"Species-boss filter contract");
        target.MoveToWorld(new(p.X+13,p.Y+3,p.Z),p.Map);Require(AmazonSlowMissilesCast.Target(p,target,27),"Inside source circle excluded");
        target.MoveToWorld(new(p.X+13,p.Y+4,p.Z),p.Map);Require(!AmazonSlowMissilesCast.Target(p,target,27),"Outside source circle included");
        target.MoveToWorld(new(p.X+3,p.Y,p.Z+16),p.Map);Require(!AmazonSlowMissilesCast.Target(p,target,27),"Other floor included");
        target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        var unknown=new GraveyardLich{CantWalk=true,Frozen=true,NoKillAwards=true};unknown.AIObject.AITimer.Stop();unknown.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        Require(!AmazonSlowMissilesCast.Target(p,unknown,27),"Unknown source species acquired a guessed boss flag");unknown.Delete();
        NecromancerEffects.SetMana256(p,1279);Require(!AmazonSlowMissilesCast.Commit(p,at)&&NecromancerEffects.ManaBalance256(p)==1279&&AmazonSlowMissiles.State(target)==null,"Low-mana cast mutated state/payment");
        NecromancerEffects.SetMana256(p,1280);Require(AmazonSlowMissilesCast.Commit(p,at)&&NecromancerEffects.ManaBalance256(p)==0,"Exact cast payment");
        var first=AmazonSlowMissiles.State(target);
        Require(first is{Rank:1,VelocityPercent:68,DamagePercent:94}&&first.UntilUtc==at.AddSeconds(12)&&first.Creator==p.Serial.Value
            &&AmazonSlowMissiles.State(outside)==null,"Caster-centered rank1 payload/duration/filter");
        // The actual encounter launch captures the state produced by Commit.
        EliteBolts.Lightning(target);var shots=EliteBolts.Inspect(target);
        Require(shots.Length==8&&shots.All(x=>x.Slow==new SlowMissilePayload(68,94)),"Cast state did not reach actual missile factory");
        AmazonSlowMissiles.Clear(target);Require(EliteBolts.Inspect(target).All(x=>x.Slow==new SlowMissilePayload(68,94)),"Later state change mutated launched missiles");EliteBolts.Cancel(target);
        Require(AmazonSlowMissiles.Apply(p,target,20,at),"Strong recipient seed");var strong=AmazonSlowMissiles.State(target);
        NecromancerEffects.SetMana256(p,1280);Require(AmazonSlowMissilesCast.Commit(p,at.AddSeconds(1))&&NecromancerEffects.ManaBalance256(p)==0&&AmazonSlowMissiles.State(target)==strong,"Weaker cast refunded/replaced stronger state");
        AmazonSlowMissiles.Clear(target);target.Internalize();outside.Internalize();
        NecromancerEffects.SetMana256(p,1280);Require(AmazonSlowMissilesCast.Commit(p,at)&&NecromancerEffects.ManaBalance256(p)==0,"Empty cast did not pay once");
        p.Frozen=true;NecromancerEffects.SetMana256(p,1280);Require(!AmazonSlowMissilesCast.Commit(p,at)&&NecromancerEffects.ManaBalance256(p)==1280,"Invalid caster paid");p.Frozen=false;
        outside.Delete();target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        Require(AmazonSlowMissilesCast.Commit(p,TimedEffects.Now),"Saved release");
        // Inspection clock only; the ordinary rank1 duration was checked above
        // and the retained component fixture observes its natural expiration.
        var state=AmazonSlowMissiles.State(target) with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(x=>x.Target!=target.Serial.Value).Append(state).ToArray());
        p.Internalize();target.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,NecromancerEffects.ManaBalance256(p),state,0)));
        Log.Information("Slow Missiles prepared release:source circle/floor/species guard,exact/low/empty/weaker payment,captured actual8bolt launches;availability remains gated");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&AmazonSlowMissiles.State(target)==s.State&&NecromancerEffects.ManaBalance256(p)==s.Mana&&!ClassCombat.Available(p,CombatAbility.SlowMissiles),"Original release state/resources or unavailable gate changed after reload: owner="+(p!=null)+",target="+(target!=null)+",state="+AmazonSlowMissiles.State(target)+",expected="+s.State+",mana="+(p==null?-1:NecromancerEffects.ManaBalance256(p))+",expectedMana="+s.Mana);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
