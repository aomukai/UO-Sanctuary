// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SkeletonRosterChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SkeletonRosterChecks));
    private static readonly CombatAbility[] Kinds=[CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage];
    private sealed record Pet(uint Serial,CombatAbility Kind,long Order,int Rank,int Mastery,int Life256);
    private sealed record Saved(uint Owner,Point3D Point,int Map,Pet[] Pets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"skeleton-roster-fixtures.json");
    public static void Configure(){Register("skeleton-roster-check",Check);Register("skeleton-roster-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Skeleton/Mage FIFO,cap isolation and actual learned casts",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int ranks=1)
    {
        TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,prior);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<ranks){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Owner()
    {var p=PoisonDaggerChecks.Owner();p.Name="SkeletonRosterFixture";foreach(var kind in Kinds){Train(p,SkillMechanics.For(kind).TalentId,2);}Train(p,SkillMechanics.For(CombatAbility.ClayGolem).TalentId);return p;}
    private static Corpse Corpse(PlayerMobile p)
    {
        var mob=new GraveyardZombie{NoKillAwards=true};mob.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);mob.Damage(10000,p);if(mob.Alive){mob.Kill();}
        var corpse=mob.Corpse as Corpse;Require(SkeletonPets.Usable(p,corpse),"Real corpse unavailable");return corpse;
    }
    private static bool Release(PlayerMobile p,CombatAbility kind,Corpse c)=>kind==CombatAbility.RaiseSkeleton?SkeletonPets.Raise(p,c):NecromancerCombat.Release(p,kind,c.Serial,c.Location);
    private static SkeletonGuardian Cast(PlayerMobile p,CombatAbility kind)
    {
        p.Mana=p.ManaMax;var c=Corpse(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,kind).Mana256;
        Require(Release(p,kind,c),"Learned summon refused: "+kind);var body=NecromancerSkeletons.For(p,kind).Last();
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&(c.Deleted||c.SanctuaryConsumed),"Actual cast lost exact payment/corpse "+kind);
        body.AIObject.AITimer.Stop();body.MoveToWorld(new(p.X+6,p.Y+3,p.Z),p.Map);body.SuspendActions();c.Delete();return body;
    }
    private static void End(PlayerMobile p){foreach(var pet in(p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}p.Delete();}
    private static void Swap(SkeletonGuardian a,SkeletonGuardian b)
    {
        long first=BattleCommand.PetOrder(a),second=BattleCommand.PetOrder(b);Require(first>0&&second>first,"Expected original birth ordering");
        var state=BattleCommand.Snapshot();BattleCommand.Restore(state with{Pets=state.Pets.Select(x=>x.Pet==a.Serial.Value?x with{Order=second}:x.Pet==b.Serial.Value?x with{Order=first}:x).ToArray()});
    }
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"skeleton-roster-source-cases.json")));int count=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            int rank=row[0].GetInt32(),maximum=row[2].GetInt32();
            foreach(var kind in Kinds)
            {var definition=SkillMechanics.For(kind);var v=new SkillValues(definition,key=>key==definition.SourceKey?rank:0,_=>0,99);Require(v.Eval("petmax")==maximum&&SkeletonPets.Limit(rank)==maximum,"Current cap differs from source case");}
            int n=row[1].GetInt32(),remove=Math.Max(0,n+1-maximum);var ids=Enumerable.Range(0,n).Select(i=>1000-i).ToArray();
            Require(row[3].EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(ids.Take(remove))&&row[4].EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(ids.Skip(remove).Append(99999)),"Source reversed-GUID FIFO trace");count++;
        }
        Require(count==1530,"Missing source roster contexts");
    }
    private static void Creation()
    {
        var p=Owner();var other=Owner();try
        {
            var external=Cast(other,CombatAbility.RaiseSkeleton);
            Require(NecromancerSummons.Create(p,CombatAbility.ClayGolem,Serial.Zero,new(p.X+4,p.Y,p.Z)),"Unrelated golem");var golem=NecromancerGolems.For(p).Single();golem.AIObject.AITimer.Stop();
            foreach(var kind in Kinds)
            {
                var first=Cast(p,kind);var second=Cast(p,kind);Swap(first,second);int life=SkillHealth.Balance256(first);
                var separate=NecromancerSkeletons.For(p,kind==Kinds[0]?Kinds[1]:Kinds[0]);var newest=Cast(p,kind);
                Require(second.Deleted&&!first.Deleted&&!newest.Deleted&&NecromancerSkeletons.For(p,kind).SequenceEqual(new[]{first,newest})&&SkillHealth.Balance256(first)==life,"Creation used serial instead of saved FIFO");
                Require(!golem.Deleted&&!external.Deleted&&separate.All(x=>!x.Deleted),"Creation crossed pet family/owner");
                var corpse=Corpse(p);p.Mana=0;var before=NecromancerSkeletons.For(p,kind);
                Require(!Release(p,kind,corpse)&&!corpse.SanctuaryConsumed&&before.SequenceEqual(NecromancerSkeletons.For(p,kind)),"Insufficient mana consumed corpse/pruned roster");corpse.Delete();
            }
        }
        finally{End(p);End(other);}
    }
    private static void Historical()
    {
        var p=Owner();var supplier=new PlayerMobile{Player=true,Body=0x190};ClassCreation.Profiles.Assign(supplier,DiabloClass.Barbarian);
        try
        {
            foreach(var kind in Kinds)
            {
                var first=Cast(p,kind);var second=Cast(p,kind);BattleCommand.Apply(supplier,p,1,TimedEffects.Now.AddHours(1));var newest=Cast(p,kind);
                var state=BattleCommand.Snapshot();BattleCommand.Restore(state with{Pets=state.Pets.Where(x=>x.Pet!=first.Serial.Value).ToArray()});PersistentMinions.Suspend(p);
                BattleCommand.Clear(p);
                Require(first.Deleted&&!second.Deleted&&!newest.Deleted&&NecromancerSkeletons.For(p,kind).Length==2,"Historical missing-order member was promoted above recorded birth");
                Require(second.Map==Map.Internal&&newest.Map==Map.Internal,"Cap refresh moved parked pets");
                foreach(var pet in NecromancerSkeletons.For(p,kind)){pet.Delete();}
                // Restore a world owner for the next family's learned casts.
                p.MoveToWorld(p.Location,p.Map);
            }
        }
        finally{BattleCommand.Clear(p);supplier.Delete();End(p);}
    }
    private static Pet Snapshot(SkeletonGuardian p)=>new(p.Serial.Value,NecromancerSkeletons.Kind(p),BattleCommand.PetOrder(p),p.Rank,p.Mastery,SkillHealth.Balance256(p));
    private static void Save()
    {
        var p=Owner();foreach(var kind in Kinds){var first=Cast(p,kind);var second=Cast(p,kind);Swap(first,second);}
        var pets=Kinds.SelectMany(kind=>NecromancerSkeletons.For(p,kind)).ToArray();foreach(var pet in pets){SkillHealth.SetBalance256(pet,1237);}
        var saved=new Saved(p.Serial.Value,p.Location,p.Map.MapID,pets.Select(Snapshot).ToArray(),0);PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Check()
    {Source();Creation();Historical();Save();Log.Information("Skeleton roster:1530source contexts;both learned casts preserve FIFO over serial,independent caps/owners,failed payment and parked historical cap refresh pass");}
    private static void Reload()
    {
        Source();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        foreach(var row in s.Pets)
        {var body=(SkeletonGuardian)World.FindMobile((Serial)row.Serial);Require(body.ControlMaster==p&&body.Map==Map.Internal&&Snapshot(body)==row,"Reload changed roster identity/order/captured ranks/injury");}
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);p.ClaimAutoStabledPets();
        foreach(var kind in Kinds)
        {
            var before=NecromancerSkeletons.For(p,kind);Require(before.Length==2,"Restored family cap");var created=Cast(p,kind);
            Require(before[0].Deleted&&!before[1].Deleted&&!created.Deleted&&NecromancerSkeletons.For(p,kind).SequenceEqual(new[]{before[1],created}),"Reloaded creation ignored persisted birth FIFO");
        }
        var pets=Kinds.SelectMany(kind=>NecromancerSkeletons.For(p,kind)).ToArray();PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Pets=pets.Select(Snapshot).ToArray(),Generation=s.Generation+1}));
    }
}
