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

public static class ReviveRandomChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveRandomChecks));
    private sealed record Saved(int Generation,uint[] Actors,uint Target,NpcEquipmentSeed[] Streams,uint[] Next,ulong[] After);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-random-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-random-check",Check);Register("revive-random-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Revive persisted stream and loot-independent lifetime",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Actor(uint serial)=>World.FindMobile((Serial)serial)as BaseCreature;
    private static void Roll(BaseCreature actor,Mobile target)
    {
        ulong seed=NpcEquipmentRandom.State(actor).Seed;
        uint Raw()=>SourceRandom.Roll(ref seed);int Limited(int n)=>(int)(Raw()%(uint)n);
        var expected=ReviveEquipmentOffense.Roll(ReviveEquipmentOffense.Capture(actor,target),Limited,Raw);
        var actual=ReviveEquipmentOffense.Roll(actor,target);
        Require(actual==expected&&NpcEquipmentRandom.State(actor).Seed==seed,"Actor stream differed from supplied source stream or advanced twice");
    }
    private static void Save(int generation,uint[] actors,uint target)
    {
        var rows=actors.Select(id=>NpcEquipmentRandom.State(Actor(id))).ToArray();var next=new uint[rows.Length];var after=new ulong[rows.Length];
        for(int i=0;i<rows.Length;i++){ulong seed=rows[i].Seed;next[i]=SourceRandom.Roll(ref seed);after[i]=seed;}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(generation,actors,target,rows,next,after)));
    }
    private static BaseCreature Detached(PlayerMobile owner,out Corpse corpse)
    {
        var b=new Skeleton{Name="PoisonDaggerFixtureReviveRandom",NoKillAwards=true};foreach(var item in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        var gear=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Dwarf Star").Id,99,new Random(173),true);b.AddItem(gear);
        b.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);b.AIObject?.AITimer.Stop();Corpse captured=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==b){captured=item as Corpse;}};
        try{owner.DoHarmful(b,true);b.Damage(b.Hits+1,owner);CombatDamage.Finish(b,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(captured is{Deleted:false}&&ReviveOriginals.Find(captured)==b,"Random lifecycle original was not retained");
        var original=ReviveOriginals.State(captured);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveLife.Setup(owner,captured,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")};setup=ReviveEquipmentCombat.Prepare(setup,original);
        Require(ReviveNativeActivation.CommitPrepared(owner,captured,captured.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==b,"Random lifecycle activation failed: "+issue);
        Require(gear.Parent==captured,"Random lifecycle gear was not separate loot");owner.AddToBackpack(gear);gear.Delete();corpse=captured;return b;
    }
    private static void Lifecycle()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureReviveRandomOwner";MagePetBuffChecks.Train(p,"necromancer.revive",1);FixtureSpace.Corridor(p,6540,24,3400);
        var first=Detached(p,out var corpse);NpcEquipmentRandom.Set(first,SourceRandom.InitLow(173));var old=NpcEquipmentRandom.State(first);
        corpse.Delete();Require(corpse.Deleted&&first is{Alive:true,Deleted:false}&&NpcEquipmentRandom.Snapshot().Contains(old),"Deleting loot corpse invalidated original stream");
        ulong value=old.Seed;uint expected=SourceRandom.Roll(ref value);Require(NpcEquipmentRandom.Raw(first)==expected&&NpcEquipmentRandom.State(first).Seed==value,"Deleted corpse changed stream identity");
        var second=Detached(p,out var replacement);Require(first.Deleted&&ReviveNativeActivation.State(first)==null&&NpcEquipmentRandom.State(first)==null,"Replacement retained discarded original stream");
        NpcEquipmentRandom.Set(second,SourceRandom.InitLow(179));p.Kill();Require(!p.Alive,"Owner-death control failed");ReviveNativeActivation.Tick();
        Require(second.Deleted&&ReviveNativeActivation.State(second)==null&&NpcEquipmentRandom.State(second)==null,"Necromancer death retained original stream");replacement.Delete();
    }
    private static void Check()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-equipment-offense-fixtures.json")));var root=document.RootElement;
        uint[] actors=root.GetProperty("Scenes").EnumerateArray().Select(r=>r.GetProperty("Actor").GetUInt32()).ToArray();uint targetId=root.GetProperty("Target").GetUInt32();var target=World.FindMobile((Serial)targetId);
        var old=NpcEquipmentRandom.SeedLow;int initialized=0;NpcEquipmentRandom.SeedLow=()=>{initialized++;return 173;};
        try
        {
            foreach(uint id in actors)
            {
                var b=Actor(id);Require(NpcEquipmentRandom.State(b)==null,"Frozen fixture stream was initialized before use");
                ulong seed=SourceRandom.InitLow(173);uint expected=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(b)==expected,"Lazy stream initialization used another algorithm");
                var row=NpcEquipmentRandom.State(b);Require(row.Item==0&&!row.Unarmed&&row.ReviveCorpse==ReviveNativeActivation.State(b).Original.Corpse&&row.ReviveBinding is{Length:64},"Frozen stream acquired a fake live-item binding");
                for(int i=0;i<256;i++){expected=SourceRandom.Roll(ref seed);Require(NpcEquipmentRandom.Raw(b)==expected&&NpcEquipmentRandom.State(b).Seed==seed,"Frozen source RNG sequence diverged");}
                Roll(b,target);
                var before=NpcEquipmentRandom.State(b);int boundCalls=initialized;Require(NpcEquipmentRandom.Limited(b,0)==0&&NpcEquipmentRandom.Limited(b,-1)==0&&NpcEquipmentRandom.State(b)==before&&initialized==boundCalls,"Empty bound consumed or rebound stream");
                b.SetDamageType(ResistanceType.Physical,75);b.SetDamageType(ResistanceType.Fire,25);bool refused=false;
                try{_=ReviveEquipmentOffense.Roll(b,target);}catch(NotSupportedException){refused=true;}
                finally{b.SetDamageType(ResistanceType.Physical,100);b.SetDamageType(ResistanceType.Fire,0);}
                Require(refused&&NpcEquipmentRandom.State(b)==before ,"Unsupported offensive input advanced persisted stream");
            }
            Require(initialized==3,"Original stream was initialized more than once");
        }
        finally{NpcEquipmentRandom.SeedLow=old;}
        var all=NpcEquipmentRandom.Snapshot();var sample=NpcEquipmentRandom.State(Actor(actors[0]));
        foreach(var bad in new[]{sample with{ReviveCorpse=0},sample with{ReviveBinding=null},sample with{ReviveBinding=new string('g',64)},sample with{Unarmed=true},sample with{Item=sample.ReviveCorpse}})
        {
            bool refused=false;try{NpcEquipmentRandom.Restore(all.Select(r=>r.Actor==sample.Actor?bad:r).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&all.SequenceEqual(NpcEquipmentRandom.Snapshot()),"Malformed frozen stream restore was not atomic");
        }
        var actor=Actor(actors[0]);var original=ReviveNativeActivation.State(actor);var death=ReviveBeforeDeath.Decode(original.Original.BeforeDeathJson);
        old=NpcEquipmentRandom.SeedLow;int rebound=0;NpcEquipmentRandom.SeedLow=()=>{rebound++;return 181;};
        try
        {
            ReviveNativeActivation.Store(original with{Original=original.Original with{BeforeDeathJson=JsonSerializer.Serialize(death with{CapturedAtUtc=death.CapturedAtUtc.AddTicks(1)})}});
            ulong reset=SourceRandom.InitLow(181);uint expected=SourceRandom.Roll(ref reset);
            Require(NpcEquipmentRandom.Raw(actor)==expected&&rebound==1&&NpcEquipmentRandom.State(actor).ReviveCorpse==sample.ReviveCorpse
                &&NpcEquipmentRandom.State(actor).ReviveBinding!=sample.ReviveBinding,"New death snapshot reused an old same-serial stream");
        }
        finally{ReviveNativeActivation.Store(original);NpcEquipmentRandom.Restore(all);NpcEquipmentRandom.SeedLow=old;}
        Require(all.SequenceEqual(NpcEquipmentRandom.Snapshot()),"Binding-epoch probe changed existing streams");
        var legacy=new NpcEquipmentSeed(sample.Actor,sample.Owner,0,173,true);string json=JsonSerializer.Serialize(legacy);
        Require(!json.Contains("ReviveCorpse")&&!json.Contains("ReviveBinding")&&JsonSerializer.Deserialize<NpcEquipmentSeed>(json)==legacy,"Optional stream fields changed legacy row shape");
        var facets=SummonDifficulties.Facets();var owner=Actor(actors[0]).ControlMaster as PlayerMobile;
        var beforeTravel=actors.Select(id=>NpcEquipmentRandom.State(Actor(id))).ToArray();
        try
        {
            SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Hell)]);
            owner.MoveToWorld(new(6680,3400,0),Map.Trammel);ReviveFacets.Sync(owner);owner.MoveToWorld(new(6680,3400,0),Map.Felucca);ReviveFacets.Sync(owner);owner.Internalize();
            Require(beforeTravel.SequenceEqual(actors.Select(id=>NpcEquipmentRandom.State(Actor(id))))&&beforeTravel.All(r=>NpcEquipmentRandom.Snapshot().Contains(r)),"Facet travel/parking reset original stream");
        }
        finally{SummonDifficulties.SetFacets(facets);}
        Lifecycle();Save(0,actors,targetId);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Actors.Length==3&&saved.Generation is 0 or 1,"Missing original stream generation");
        var old=NpcEquipmentRandom.SeedLow;NpcEquipmentRandom.SeedLow=()=>throw new InvalidOperationException("Reload reseeded an original stream");
        try
        {
            for(int i=0;i<saved.Actors.Length;i++)
            {
                var b=Actor(saved.Actors[i]);Require(b is{Alive:true,Deleted:false}&&b.Map==Map.Internal&&NpcEquipmentRandom.State(b)==saved.Streams[i]&&NpcEquipmentRandom.Snapshot().Contains(saved.Streams[i]),"Saved original stream identity/seed changed");
                Require(NpcEquipmentRandom.Raw(b)==saved.Next[i]&&NpcEquipmentRandom.State(b).Seed==saved.After[i],"Reload did not continue the exact next source draw");Roll(b,World.FindMobile((Serial)saved.Target));
            }
        }
        finally{NpcEquipmentRandom.SeedLow=old;}
        Save(saved.Generation+1,saved.Actors,saved.Target);
    }
}
