// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MoltenBoulderChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MoltenBoulderChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,BoulderSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"molten-boulder-fixtures.json");
    private static int _scene;
    public static void Configure()
    {
        Register("molten-boulder-check",Check);Register("molten-boulder-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared native Molten Boulder actors",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="BoulderFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,6860,25,3400+32*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var reason),"Train: "+reason);}
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx)
    {
        var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(10000);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static string Text()=>JsonSerializer.Serialize(DruidMoltenBoulder.Snapshot());
    private static BoulderMissile Launch(PlayerMobile p,DateTime at)
    {long id=DruidMoltenBoulder.Create(p,new(p.X+12,p.Y,p.Z),1,at);return DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Id==id);}
    private static BoulderMissile Rolling(PlayerMobile p,DateTime at)
    {
        Launch(p,at);DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);
        return DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Missile==452 && x.Owner==p.Serial.Value);
    }
    private static void DamageSource()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"molten-boulder-damage-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int kind=row[0].GetInt32(),rank=row[1].GetInt32(),firestorm=row[2].GetInt32(),volcano=row[3].GetInt32(),mastery=row[4].GetInt32();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.MoltenBoulder),_=>rank,key=>key=="Firestorm"?firestorm:key=="Volcano"?volcano:0,99);
            var actual=DruidMoltenBoulder.Endpoints(values,kind==0?453:kind==1?452:455,mastery,SlowMissilePayload.Normal);
            Require(actual==new BoulderEndpoints(row[6].GetInt32(),row[7].GetInt32(),row[8].GetInt32(),row[9].GetInt32()),"Complete source damage capture differs at kind/rank "+kind+"/"+rank);
            Require(values.Mana256==2560+(rank-1)*128,"Half-mana rank growth");count++;
        }
        Require(count==12240,"Incomplete source damage coverage");Log.Information("Molten Boulder:12240complete source damage capture comparisons/all255ranks and mana growth pass");
    }
    private static void Motion()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);int mana=NecromancerEffects.ManaBalance256(p);
        var initial=Launch(p,at);Require(initial.Missile==453 && initial.Remaining==5 && initial.Velocity256==576 && initial.PhysicalMax256==0 && initial.FireMax256==0,"Emergence factory/lifetime/empty packet");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(160),true);var fourth=DruidMoltenBoulder.Snapshot().Missiles.Single();
        Require(fourth.Missile==453 && fourth.Remaining==1,"InitSteps introduced extra server updates");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);var rolling=DruidMoltenBoulder.Snapshot().Missiles.Single();
        Require(rolling.Missile==452 && rolling.Remaining==50 && rolling.Velocity256==1536 && rolling.X>initial.X
            && rolling.AimX==initial.AimX && rolling.AimY==initial.AimY && rolling.PhysicalMin256==1536 && rolling.PhysicalMax256==3072,"Fresh rolling child capture");
        var v=SkillMechanics.Values(p,CombatAbility.MoltenBoulder,1);Require(rolling.FireMin256==v.Damage256(false) && rolling.FireMax256==v.Damage256(true),"Hard Firestorm synergy capture");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(240),true);Require(!DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Missile==455),"Trail emitted on new movement rather than previous movement flag");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(480),true);var fields=DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455).ToArray();
        Require(fields.Length>0 && fields.All(x=>x.Remaining is >=1 and <=37 && x.Velocity256==0 && x.PhysicalMax256==0),"Independent stationary trail fields");
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared actor charged cast mana");DruidMoltenBoulder.Cancel(p);p.Delete();
    }
    private static void Contacts()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Rolling(p,at);var t=Target(p,2);
        int last=SkillHealth.Balance256(t),changes=0;
        for(int i=6;i<=16;i++)
        {
            DruidMoltenBoulder.Tick(at.AddMilliseconds(i*40),true);int life=SkillHealth.Balance256(t);
            if(life<last){changes++;}last=life;
        }
        Require(changes>=2 && DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Missile==452),"Non-LARGE repeated contacts/continuation");
        Require(DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455).All(x=>x.Remaining<=37),"Trail lifetime");
        DruidMoltenBoulder.Cancel(p);t.Delete();p.Delete();
        // A declared LARGE species input exercises native area/disc services;
        // it does not relabel the production skeleton species as LARGE.
        p=Fresh();Train(p,CombatAbility.MoltenBoulder);Rolling(p,at);t=Target(p,2);var prior=DruidMoltenBoulder.Model;
        try
        {
            DruidMoltenBoulder.Model=c=>SourceKnockbacks.Rule(c) with{Large=true};
            int before=SkillHealth.Balance256(t);bool exploded=false;
            for(int i=6;i<=12;i++)
            {
                DruidMoltenBoulder.Tick(at.AddMilliseconds(i*40),true);
                if(!DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value && x.Missile==452))
                {
                    var disc=DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455 && x.Remaining==7).ToArray();
                    Require(disc.Length==18 && SkillHealth.Balance256(t)<before,"LARGE area damage/18 shortened children");exploded=true;break;
                }
            }
            Require(exploded,"LARGE contact never exploded");
        }
        finally{DruidMoltenBoulder.Model=prior;DruidMoltenBoulder.Cancel(p);t.Delete();p.Delete();}
    }
    private static void Offline()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Launch(p,at);
        DruidMoltenBoulder.Tick(at.AddSeconds(1),false);Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0,"Offline emergence spawned a child instead of expiring");
        var state=Rolling(p,at);var target=Target(p,2);int life=SkillHealth.Balance256(target);
        DruidMoltenBoulder.Tick(at.AddSeconds(4),false);Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0 && SkillHealth.Balance256(target)==life,"Offline rolling fought/emitted or stopped expiring");target.Delete();
        Launch(p,at);var saved=DruidMoltenBoulder.Snapshot();string text=Text();
        Task.Run(()=>{for(int i=0;i<20;i++){Require(Text()==text,"Worker snapshot mutation");}}).GetAwaiter().GetResult();
        var broken=saved with{Missiles=saved.Missiles.Concat(new[]{saved.Missiles[0]}).ToArray()};bool refused=false;
        try{DruidMoltenBoulder.Restore(broken);}catch(InvalidDataException){refused=true;}
        Require(refused && Text()==text,"Failed restore modified current state");
        var copy=DruidMoltenBoulder.Snapshot();copy.Missiles[0].Steps.Cells[0]=new(1,1);Require(Text()==text,"Snapshot exposed mutable saved steps");
        DruidMoltenBoulder.Cancel(p);p.Delete();
    }
    private static void SlowCapture()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);var original=AmazonSlowMissiles.Snapshot();
        try
        {
            AmazonSlowMissiles.Restore(original.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddDays(2))).ToArray());
            var emergence=Launch(p,at);Require(emergence.Velocity256==288,"Emergence Slow Missiles velocity");
            DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);var rolling=DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value);
            var values=SkillMechanics.Values(p,CombatAbility.MoltenBoulder,1);
            Require(rolling.Missile==452 && rolling.Velocity256==768 && rolling.PhysicalMin256==values.PhysicalDamage256(false)
                && rolling.PhysicalMax256==values.PhysicalDamage256(true) && rolling.FireMin256==values.Damage256(false)/2
                && rolling.FireMax256==values.Damage256(true)/2,"Slow elemental capture must preserve physical endpoints");
        }
        finally{AmazonSlowMissiles.Restore(original);DruidMoltenBoulder.Cancel(p);p.Delete();}
    }
    private static void Check()
    {
        Require(!DruidCombat.Implemented(CombatAbility.MoltenBoulder),"Prepared actor accidentally enabled learned casting");
        var block=SourceBlocking.BlockRoll;var seed=DruidMoltenBoulder.SeedLow;
        try{SourceBlocking.BlockRoll=_=>99;DruidMoltenBoulder.SeedLow=()=>123;DamageSource();Motion();Contacts();Offline();SlowCapture();}
        finally{SourceBlocking.BlockRoll=block;DruidMoltenBoulder.SeedLow=seed;}
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Rolling(p,at);var t=Target(p,2);
        DruidMoltenBoulder.Tick(at.AddMilliseconds(280),true);var saved=DruidMoltenBoulder.Snapshot();
        // Hold the checkpoint clock after separate normal motion/lifetime tests.
        DruidMoltenBoulder.Restore(saved with{Missiles=saved.Missiles.Select(x=>x with{NextUtc=TimedEffects.Now.AddDays(2)}).ToArray()});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,SkillHealth.Balance256(t),DruidMoltenBoulder.Snapshot())));
        Log.Information("Molten Boulder: prepared native emergence/rolling/trail/direct/area/disc/offline/atomic-worker checks pass; learned handler remains gated");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(Text()==JsonSerializer.Serialize(saved.State),"Native reload lost exact Boulder position/steps/damage/seed/clock");
        Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile && World.FindMobile((Serial)saved.Target) is GraveyardSkeleton t && SkillHealth.Balance256(t)==saved.Life256,"Saved owner/target/life changed");
        Log.Information("Molten Boulder: exact prepared native state survives actual reload");
    }
}
