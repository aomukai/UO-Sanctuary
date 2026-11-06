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
public static class ValkyrieResourceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieResourceChecks));
    private sealed record Held(int Case,uint Body,string Text,string Resource,string Stats,uint[] Items,string[] Payloads,int[] Pools);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-resources-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static string Pairs(EquipmentStatValue[] fields)=>Json(fields.Select(v=>new long[]{v.Packed,v.Value}).ToArray());
    private static string Operative(GeneratedNpcStatGraph g)
    {
        // One validated immutable frame;call the exact production source
        // reader for every scalar/occupied layer without re-decoding the
        // same original inventory 368times within this atomic console action.
        var keys=Enumerable.Range(0,368).Select(i=>EquipmentStatKey.Pack(i)).Concat(g.Stats.Select(v=>v.Packed)).Distinct().OrderBy(k=>k);
        return Pairs(keys.Select(k=>new EquipmentStatValue(k,EquipmentStatValues.SourceRead(g.Actor.Stats,k,EquipmentStatValues.Definition(EquipmentStatKey.Stat(k))))).Where(v=>v.Value!=0).ToArray());
    }
    private static int[] Pools(AmazonValkyrieBody b)=>[SkillHealth.Balance256(b),SkillHealth.Maximum256(b),NecromancerEffects.ManaBalance256(b),NecromancerEffects.ManaMaximum256(b),BattleOrders.Stamina256(b),BattleOrders.StaminaMaximum256(b)];
    public static void Configure(){Register("valkyrie-resources-check",false);Register("valkyrie-resources-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Full original Valkyrie resource integration",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="ValkyrieResourceFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);
        FixtureSpace.Corridor(p,6380,20,3460);p.Mana=p.ManaMax;return p;
    }
    private static AmazonValkyrieBody Body(PlayerMobile p,JsonElement c)
    {
        int rank=c.GetProperty("Rank").GetInt32();var difficulty=(MonsterDifficulty)c.GetProperty("Difficulty").GetInt32();var b=new AmazonValkyrieBody();
        var state=new ValkyrieBodyState(p.Serial.Value,ValkyrieBirthNumbers.Capture(p,400*256)with{Rank=rank},difficulty,TimedEffects.Now.AddDays(2));b.RestorePrepared(state);
        var facets=SummonDifficulties.Facets();try{SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());b.Initialize(p,rank,0);}finally{SummonDifficulties.SetFacets(facets);}
        b.RestorePrepared(state);b.SetStr(rank*25);b.SetDex(rank*12);b.ApplyEquipmentResistances();Require(b.SetControlMaster(p),"Resource owner refused");
        PersistentMinions.Profiles.Track(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(b,c.GetProperty("Life").GetInt32());
        ValkyrieEquipment.Materialize(b,new(SourceRandom.InitLow(100+rank)),new(SourceRandom.InitLow(900+(int)difficulty)));ValkyrieEquipmentStats.Activate(b);return b;
    }
    private static void Inspect(AmazonValkyrieBody b,JsonElement c,int stage)
    {
        var expected=c.GetProperty("Stages")[stage];var g=b.EquipmentNumbers;
        Require(Operative(g)==expected[2].GetRawText(),"Full source resource reader mismatch case="+c.GetProperty("Case")+" variant="+c.GetProperty("Variant")+" stage="+stage+" actual="+Operative(g)+" expected="+expected[2].GetRawText());
        int Read(int stat)=>g.Read(stat);
        Require(Pools(b).SequenceEqual(new[]{Read(6),Read(7),Read(8),Read(9),Read(10),Read(11)})&&b.HitsMax==(Read(7)+255)/256&&b.ManaMax==(Read(9)+255)/256&&b.StamMax==(Read(11)+255)/256,"Native FP256 pools/maxima differ from source graph");
        Require(GeneratedNpcLiveResources.RegenerationBase(b)==expected[1].GetInt32()&&NpcEquipmentRegeneration.ItemRate(b)==Read(74)-expected[1].GetInt32(),"Source regeneration base/item contribution was omitted or doubled");
        Require(!ClassCombat.Available((PlayerMobile)b.ControlMaster,CombatAbility.Valkyrie)&&b.SkillWeaponDamage==(0,0),"Resources enabled an unfinished learned cast/weapon");
    }
    private static Held Store(AmazonValkyrieBody b,int index)
    {
        var items=ValkyrieEquipment.Originals(b);return new(index,b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),Operative(b.EquipmentNumbers),items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),Pools(b));
    }
    private static void Refusals(AmazonValkyrieBody b)
    {
        var before=Store(b,-1);bool refused=false;
        try{GeneratedNpcLiveResources.ChangeBase(b,7,99999);}catch(NotSupportedException){refused=true;}Require(refused&&Json(Store(b,-1))==Json(before),"Captured resource bases could be replaced by an unrelated live writer");
        refused=false;try{ValkyrieEquipmentResources.Activate(b,0,0,0,0,0);}catch(NotSupportedException){refused=true;}Require(refused&&Json(Store(b,-1))==Json(before),"Existing resource admission was replayed/refilled");
        var all=GeneratedNpcLiveResources.Snapshot();var corrupt=all.Select(s=>s.Actor==b.Serial.Value?s with{DamageRegeneration=13}:s).ToArray();refused=false;
        try{GeneratedNpcLiveResources.Restore(corrupt);}catch(NotSupportedException){refused=true;}Require(refused&&Json(GeneratedNpcLiveResources.Snapshot())==Json(all)&&Json(Store(b,-1))==Json(before),"Live corrupt profile restore partially replaced the source ledger");
        refused=false;try{GeneratedNpcLiveResources.SetItemActive(b,0x4FFFFFFF,true);}catch(NotSupportedException){refused=true;}Require(refused&&Json(Store(b,-1))==Json(before),"Foreign item activation changed pools/source state");
    }
    private static void Recovery(AmazonValkyrieBody b,PlayerMobile p)
    {
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)/3+17);int life=SkillHealth.Balance256(b);
        var at=b.Prepared.NextRecoveryUtc;int rate=GeneratedNpcLiveResources.RegenerationBase(b).Value+NpcEquipmentRegeneration.ItemRate(b);b.AdvanceRecovery(at,true);
        Require(SkillHealth.Balance256(b)==Math.Min(SkillHealth.Maximum256(b),life+rate),"Permanent summon recovery omitted/doubled full original item regeneration");
    }
    private static void StorePools(AmazonValkyrieBody b)
    {
        int mana=NecromancerEffects.ManaMaximum256(b),stamina=BattleOrders.StaminaMaximum256(b);
        NecromancerEffects.SetMana256(b,mana==0?0:mana/2+11);BattleOrders.SetStamina256(b,stamina==0?0:stamina/2+31);
        b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
    }
    private static void Check(bool reload)
    {
        using var input=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-resources-cases.json")));var cases=input.RootElement.EnumerateArray().ToArray();Require(cases.Length==228,"Source resource matrix");
        using var original=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-numeric-cases.json")));
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Resource reload count");var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(p!=null,"Original resource owner missing");
            foreach(var h in saved.Bodies)
            {var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Store(b,h.Case))==Json(h),"Original resource context/items/FP256 wounds changed on reload");}
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        else
        {
            // Optional JSON field preserves the earlier resource-only ledger.
            using(var old=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-legacy-resource-fixtures.json"))))
            {foreach(var row in old.RootElement.GetProperty("Actors").EnumerateArray()){string text=row.GetProperty("State").GetString();var state=JsonSerializer.Deserialize<GeneratedNpcResourceState>(text);Require(state.Valkyrie==null&&Json(state)==text,"Old resource-only JSON changed");}}
            var p=Owner();var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);var bodies=new List<AmazonValkyrieBody>();var facets=SummonDifficulties.Facets();
            try
            {
                foreach(var c in cases)
                {
                    var b=Body(p,c);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(b);string body=b.SavedText;bool refused=false;
                    try{ValkyrieEquipmentResources.Activate(b,0,-1,0,0,0);}catch(InvalidDataException){refused=true;}Require(refused&&b.SavedText==body&&SkillHealth.Balance256(b)==life&&!GeneratedNpcLiveResources.Handles(b),"Invalid supplied source pools mutated the original body");
                    ValkyrieEquipmentResources.Activate(b,c.GetProperty("Mana").GetInt32(),c.GetProperty("ManaMaximum").GetInt32(),c.GetProperty("Stamina").GetInt32(),c.GetProperty("StaminaMaximum").GetInt32(),c.GetProperty("RegenerationBase").GetInt32());
                    var raw=original.RootElement[c.GetProperty("Case").GetInt32()].GetProperty("Items");var items=ValkyrieEquipment.Originals(b);Require(items.Length==raw.GetArrayLength(),"Unfiltered original item count");
                    for(int i=0;i<items.Length;i++){var s=items[i].Snapshot;Require(Json(s.Graph)==raw[i].GetProperty("Graph").GetRawText()&&Json(s.Metadata)==raw[i].GetProperty("Metadata").GetRawText()&&Json(s.Request)==raw[i].GetProperty("Request").GetRawText()&&Json(s.Allocation)==raw[i].GetProperty("Allocation").GetRawText(),"Original item graph/request/seed data changed");}
                    Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared NPC resources spent owner mana");Inspect(b,c,0);
                    BattleOrders.Apply(supplier,b,1,TimedEffects.Now);Require(BattleOrders.Percent(b)==35,"Pinned Orders1 source context");Inspect(b,c,1);
                    BattleOrders.Clear(b);Inspect(b,c,2);uint item=ValkyrieEquipment.Originals(b)[c.GetProperty("Toggle").GetInt32()].Serial.Value;
                    GeneratedNpcLiveResources.SetItemActive(b,item,false);Inspect(b,c,3);GeneratedNpcLiveResources.SetItemActive(b,item,true);Inspect(b,c,4);
                    if(bodies.Count==0){Refusals(b);}bodies.Add(b);
                    if(bodies.Count%57==0){Log.Information("Valkyrie resources: completed {Count}/228 birth,buff and item contexts",bodies.Count);}
                }
                PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);
                SummonDifficulties.SetFacets([new(Map.Trammel.MapID,MonsterDifficulty.Normal),new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Ilshenar.MapID,MonsterDifficulty.Hell)]);
                int phase=5;foreach(var map in new[]{Map.Felucca,Map.Ilshenar,Map.Trammel})
                {
                    p.MoveToWorld(new(6380,3460,0),map);SummonDifficulties.Sync(p);
                    for(int i=0;i<bodies.Count;i++){Require(bodies[i].Prepared.Difficulty==SummonDifficulties.For(map)&&GeneratedNpcLiveResources.State(bodies[i]).Valkyrie.Birth==bodies[i].Prepared.Birth,"Facet notification recaptured birth/owner gear");Inspect(bodies[i],cases[i],phase);}phase++;
                }
                foreach(var b in bodies){Recovery(b,p);}
                // actions=false suspends the whole owner roster. Run that
                // operation once and inspect every original recipient.
                var wounded=bodies.Select(SkillHealth.Balance256).ToArray();bodies[0].AdvanceRecovery(bodies[0].Prepared.NextRecoveryUtc,false);
                var held=new List<Held>();for(int i=0;i<bodies.Count;i++)
                {Require(bodies[i].Map==Map.Internal&&SkillHealth.Balance256(bodies[i])==wounded[i],"Offline/parked source resource roster recovered");StorePools(bodies[i]);held.Add(Store(bodies[i],i));}
                PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held.ToArray())));
            }
            finally{SummonDifficulties.SetFacets(facets);supplier.Delete();}
        }
        Log.Information("Valkyrie resources:228supplied contexts/1320original items;1824complete-source stages;native fractional pools,maxima,buff/item/actualfacet transitions,shared regeneration,offline parking,atomic refusal,unfiltered item layers and original reloads;modern initializer/weapon/skill/proc/AI/cast remain gated");
    }
}
