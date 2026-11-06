// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ValkyrieWeaponChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieWeaponChecks));
    private sealed record Held(uint Body,string Text,uint[] Items,string[] Payloads,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-weapons-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure(){Register("valkyrie-weapons-check",false);Register("valkyrie-weapons-reload-check",true);}
    private static void Register(string command,bool reload)=>ConsoleInputHandler.RegisterCommand(command,"Original Valkyrie hand identity and persistent explicit attack stream",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"inventory-weapon-identity-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var rows=c.GetProperty("Items").EnumerateArray().Select(r=>r[0].GetInt32()==0?null:new SourceInventoryWeapon(r[1].GetUInt32(),r[2].GetInt32(),r[3].GetInt32()!=0,r[4].GetInt32()!=0,r[5].GetInt32(),r[6].GetInt32()!=0,r[7].GetUInt32())).ToArray();
            var a=c.GetProperty("Inventory");var slots=new SourceInventoryWeapon[13];slots[4]=rows[0];slots[5]=rows[1];
            var inv=new SourceInventoryWeapons(a[0].GetInt32()!=0,a[1].GetInt32()!=0,a[2].GetInt32()!=0,a[3].GetUInt32(),slots);
            uint[] Current()=>[inv.Present?inv.Guid:SourceInventoryWeapons.Invalid,inv.Primary()?.Guid??0,inv.Secondary()?.Guid??0];
            void Inspect(int stage)=>Require(Current().SequenceEqual(c.GetProperty("Stages")[stage].EnumerateArray().Select(v=>v.GetUInt32())),"Complete source hand identity mismatch case="+count+" stage="+stage);
            var action=a[4].GetInt32()<0?null:rows[a[4].GetInt32()];Inspect(0);inv.Insert(action);Inspect(1);inv.Remove(action);Inspect(2);count++;
        }
        Require(count==8192,"Missing full source hand identity controls");
    }
    private static AmazonValkyrieBody Body(PlayerMobile p,JsonElement c)
    {
        int rank=c.GetProperty("Rank").GetInt32();var difficulty=(MonsterDifficulty)c.GetProperty("Difficulty").GetInt32();var b=new AmazonValkyrieBody();
        var state=new ValkyrieBodyState(p.Serial.Value,ValkyrieBirthNumbers.Capture(p,400*256)with{Rank=rank},difficulty,TimedEffects.Now.AddDays(2));b.RestorePrepared(state);
        var facets=SummonDifficulties.Facets();try{SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());b.Initialize(p,rank,0);}finally{SummonDifficulties.SetFacets(facets);}
        b.RestorePrepared(state);b.SetStr(rank*25);b.SetDex(rank*12);Require(b.SetControlMaster(p),"Original weapon owner refused");PersistentMinions.Profiles.Track(b);
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(b,b.MaximumLife256-257);
        ValkyrieEquipment.Materialize(b,new(SourceRandom.InitLow(100+rank)),new(SourceRandom.InitLow(900+(int)difficulty)));ValkyrieEquipmentStats.Activate(b);return b;
    }
    private static Held Store(AmazonValkyrieBody b)
    {var items=ValkyrieEquipment.Originals(b);return new(b.Serial.Value,b.SavedText,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b));}
    private static void Draw(AmazonValkyrieBody b)
    {
        var before=NpcEquipmentRandom.State(b);Require(before!=null,"Missing original attack RNG");ulong seed=before.Seed;
        Require(NpcEquipmentRandom.Limited(b,0)==0&&NpcEquipmentRandom.Limited(b,-1)==0&&NpcEquipmentRandom.State(b)==before,"Nonpositive random bounds consumed the source stream");
        for(int i=0;i<64;i++)
        {if(i%2==0){Require(NpcEquipmentRandom.Raw(b)==SourceRandom.Roll(ref seed),"Stored source raw continuation differs");}else{int bound=i+1;Require(NpcEquipmentRandom.Limited(b,bound)==SourceRandom.Limited(ref seed,bound),"Stored source limited continuation differs");}}
        Require(NpcEquipmentRandom.State(b)==before with{Seed=seed},"Attack stream lost its original identity or continuation");
    }
    private static void Refusals(AmazonValkyrieBody b)
    {
        var old=Store(b);bool refused=false;try{ValkyrieEquipmentWeapons.Activate(b,123);}catch(NotSupportedException){refused=true;}
        Require(refused&&Json(Store(b))==Json(old),"Repeated weapon activation reseeded or rebound the original");
        var all=NpcEquipmentRandom.Snapshot();var corrupt=all.Select(s=>s.Actor==b.Serial.Value?s with{Unarmed=true}:s).ToArray();refused=false;
        try{NpcEquipmentRandom.Restore(corrupt);}catch(InvalidDataException){refused=true;}Require(refused&&Json(NpcEquipmentRandom.Snapshot())==Json(all),"Mixed-mode corrupt RNG restore partially replaced saved streams");
        var low=NpcEquipmentRandom.SeedLow;NpcEquipmentRandom.SeedLow=()=>throw new InvalidOperationException("Valkyrie attempted implicit reseeding");
        try
        {
            NpcEquipmentRandom.Forget(b);refused=false;try{_=NpcEquipmentRandom.Raw(b);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.State(b)==null,"Missing explicit seed silently auto-created a new stream");NpcEquipmentRandom.Restore(all);
            NpcEquipmentRandom.Restore(all.Select(s=>s.Actor==b.Serial.Value?s with{ValkyrieBinding=new string('0',64)}:s).ToArray());var wrong=NpcEquipmentRandom.State(b);refused=false;
            try{_=NpcEquipmentRandom.Raw(b);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(b)==wrong,"Stale original binding advanced or reseeded RNG");NpcEquipmentRandom.Restore(all);
            var item=ValkyrieEquipment.Originals(b)[^1];((PlayerMobile)b.ControlMaster).Backpack.DropItem(item);refused=false;
            try{_=NpcEquipmentRandom.Raw(b);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(b)==old.Random,"Transferred original left a usable attack stream");
            b.AddItem(item);b.RestorePrepared(b.Prepared);Require(ValkyrieEquipmentWeapons.Frame(b).Selected==b.Prepared.EquipmentWeapons.Primary,"Returned original failed identity validation");
        }
        finally{NpcEquipmentRandom.SeedLow=low;NpcEquipmentRandom.Restore(all);}
        Require(Json(Store(b))==Json(old),"Refusal controls changed exact original payloads/wounds/binding/stream");
        ValkyrieEquipmentResources.Activate(b,0,0,0,0,0);uint primary=b.Prepared.EquipmentWeapons.Primary;GeneratedNpcLiveResources.SetItemActive(b,primary,false);var seedBefore=NpcEquipmentRandom.State(b);refused=false;
        try{_=ValkyrieEquipmentWeapons.Frame(b);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(b)==seedBefore,"Detached original primary silently became an unarmed damage frame");GeneratedNpcLiveResources.SetItemActive(b,primary,true);
    }
    private static void Check(bool reload)
    {
        SourceCases();using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-numeric-cases.json")));var cases=doc.RootElement.EnumerateArray().ToArray();Require(cases.Length==57,"Original loadout matrix changed");
        PlayerMobile p;var bodies=new List<AmazonValkyrieBody>();var held=new List<Held>();int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Weapon reload count");generation=saved.Generation+1;p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(p is{Deleted:false},"Original weapon owner missing");
            foreach(var h in saved.Bodies){var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Store(b))==Json(h),"Saved original primary/payload/wound/RNG changed on reload");bodies.Add(b);}
        }
        else
        {
            // Old optional fields must serialize byte-for-byte unchanged.
            const string old="{\"Actor\":1,\"Owner\":2,\"Item\":1073741825,\"Seed\":123}";Require(Json(JsonSerializer.Deserialize<NpcEquipmentSeed>(old))==old,"Legacy RNG JSON gained new fields");
            p=new PlayerMobile{Name="ValkyrieWeaponFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6380,20,3540);
            foreach(var c in cases)
            {
                var b=Body(p,c);string oldText=b.SavedText;Require(!oldText.Contains("EquipmentWeapons")&&Json(JsonSerializer.Deserialize<ValkyrieBodyState>(oldText))==oldText,"Legacy prepared body JSON changed");
                ValkyrieEquipmentWeapons.Activate(b,b.Prepared.Equipment.PetAfter);var items=ValkyrieEquipment.Originals(b);var source=c.GetProperty("Items");
                for(int i=0;i<items.Length;i++){var s=items[i].Snapshot;Require(Json(s.Graph)==source[i].GetProperty("Graph").GetRawText()&&Json(s.Metadata)==source[i].GetProperty("Metadata").GetRawText(),"Weapon admission filtered/rerolled original item layers");}
                bodies.Add(b);
            }
            Refusals(bodies[0]);
        }
        foreach(var b in bodies)
        {
            var binding=ValkyrieEquipmentWeapons.Binding(b);var items=ValkyrieEquipment.Originals(b);Require(items.Single(i=>i.Snapshot.Equipment.Body==4).Serial.Value==binding.Primary&&binding.Secondary==0&&ValkyrieEquipmentWeapons.Frame(b).Selected==binding.Primary,"Original source spear was replaced by slot fallback/secondary");
            Draw(b);Require(!ClassCombat.Available(p,CombatAbility.Valkyrie)&&b.SkillWeaponDamage==(0,0),"Hand/RNG delivery enabled unfinished contact or learned cast");
            b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
        }
        // Gear refresh and real difficulty notifications must not reseed or
        // rewrite the original captured birth, recipe and item witnesses.
        var prior=bodies.Select(b=>b.Prepared.EquipmentWeapons).ToArray();var seeds=bodies.Select(NpcEquipmentRandom.State).ToArray();var facets=SummonDifficulties.Facets();
        try
        {
            if(!reload){PoisonDaggerChecks.Named(p,"Tarnhelm");}else{Require(p.FindItemOnLayer(Layer.Helm)!=null,"Original owner gear missing after reload");}
            ClassGear.Refresh(p);SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
            foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel}){p.MoveToWorld(new(6380,3540,0),map);SummonDifficulties.Sync(p);for(int i=0;i<bodies.Count;i++){Require(ValkyrieEquipmentWeapons.Frame(bodies[i]).Selected==prior[i].Primary&&ValkyrieEquipmentWeapons.Binding(bodies[i])==prior[i]&&NpcEquipmentRandom.State(bodies[i])==seeds[i],"Owner gear/facet change recaptured original weapon or reseeded attack stream");}}
        }
        finally{SummonDifficulties.SetFacets(facets);}
        PersistentMinions.Suspend(p);p.Internalize();foreach(var b in bodies){held.Add(Store(b));}File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held.ToArray(),generation)));
        Log.Information("Valkyrie weapons:8192source contexts/24576stages;57unfiltered original loadouts/330items;explicit persistent hand-bound RNG,64draws per actor per boot,actual owner facets,parking,legacy JSON and atomic refusal;current world seed,skills,procs,AI,contact and learned cast remain gated");
    }
}
