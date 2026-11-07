// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class ValkyrieEquipmentChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieEquipmentChecks));
    private sealed record Held(uint Body,string Text,uint[] Items,string[] Payloads,int Life,int Mana);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-equipment-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-equipment-check",false);Register("valkyrie-equipment-reload-check",true);}
    private static void Register(string n,bool reload)=>ConsoleInputHandler.RegisterCommand(n,"Valkyrie actual MonEquip loadouts and original inventory saves",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    private sealed class FaultBody:AmazonValkyrieBody
    {
        private int Added;
        public FaultBody(){}
        // Deleted fixture types still appear in the native type index.
        public FaultBody(Serial serial):base(serial){}
        public override void OnItemAdded(Item item){base.OnItemAdded(item);if(item is GeneratedNpcItem&&++Added==2){throw new NotSupportedException("Injected unavailable native item consumer");}}
    }
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="ValkyrieEquipmentFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);
        FixtureSpace.Corridor(p,6380,20,3300);p.Mana=p.ManaMax;return p;
    }
    private static AmazonValkyrieBody Body(PlayerMobile p,int rank,MonsterDifficulty difficulty,bool fault=false)
    {
        var b=fault?new FaultBody():new AmazonValkyrieBody();
        // Controlled captured effective ranks include >20 gear contexts. This
        // tests the backend,not a claim of learned rank40 input acceptance.
        var state=new ValkyrieBodyState(p.Serial.Value,ValkyrieBirthNumbers.Capture(p,400*256)with{Rank=rank},difficulty,TimedEffects.Now.AddDays(2));
        b.RestorePrepared(state);var facets=SummonDifficulties.Facets();
        try{SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());b.Initialize(p,rank,0);}
        finally{SummonDifficulties.SetFacets(facets);}
        b.RestorePrepared(state);
        var n=b.BirthStats;b.SetHits((n.Life256+255)/256);b.SetStr(n.Auras.Single(s=>s.Name=="strength").Value);b.SetDex(n.Auras.Single(s=>s.Name=="dexterity").Value);
        foreach(var pair in new[]{(ResistanceType.Fire,"fireresist"),(ResistanceType.Cold,"coldresist"),(ResistanceType.Energy,"lightresist"),(ResistanceType.Poison,"poisonresist")}){b.SetResistance(pair.Item1,n.Auras.Single(s=>s.Name==pair.Item2).Value);}
        Require(b.SetControlMaster(p),"Loadout owner refused");PersistentMinions.Profiles.Track(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(b,b.MaximumLife256);return b;
    }
    private static void Rollback(PlayerMobile p)
    {
        var b=Body(p,27,MonsterDifficulty.Normal,true);try
        {
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b),count=World.Items.Count;string before=b.SavedText;
            var pet=new SourceSeedStream(SourceRandom.InitLow(123));var game=new SourceSeedStream(SourceRandom.InitLow(456));ulong expectedPet=pet.Value,expectedGame=game.Value;
            for(int i=0;i<2;i++){SourceRandom.Limited(ref expectedPet,1);NpcItemAllocations.Take(ref expectedGame);}
            bool refused=false;try{ValkyrieEquipment.Materialize(b,pet,game);}catch(NotSupportedException){refused=true;}
            Require(refused&&GeneratedNpcEquipment.Items(b).Length==0&&b.Prepared.Equipment==null&&b.SavedText==before&&SkillHealth.Balance256(b)==life
                &&pet.Value==expectedPet&&game.Value==expectedGame&&World.Items.Count==count,"Unsupported native delivery leaked items/state,wounds or rewound seed words");
        }
        finally{b.Delete();}
    }
    private static void Inspect(AmazonValkyrieBody b)
    {
        var s=b.Prepared.Equipment;var items=ValkyrieEquipment.Originals(b);Require(items.Length>0&&items.Length<=10&&s.Attempts.Where(a=>a.Success).Select(a=>a.Item).SequenceEqual(items.Select(i=>i.Serial.Value)),"Original source item order/count");
        foreach(var item in items)
        {
            var state=item.Snapshot;var graph=item.RestoreGraph(GeneratedNpcItem.CreationValues());
            Require(item.Parent==b&&!item.Visible&&!item.Movable&&item.Layer==Layer.Invalid&&state.Metadata.Flags==0x82010&&state.Equipment.Flags==0x1000
                &&!state.Graph.Ethereal&&graph.BaseValue(72)==EquipmentStatValues.SourceRead(graph.Unit.Stats,EquipmentStatKey.Pack(73),EquipmentStatValues.Definition(73))
                &&EquipmentStatValues.SourceRead(graph.Unit.Stats,EquipmentStatKey.Pack(194),EquipmentStatValues.Definition(194))==0,"Private original flags/durability/socket/ethereal context");
        }
        s.Attempts[0]=s.Attempts[0]with{Item=0};Require(b.Prepared.Equipment.Attempts[0].Item!=0,"Mutable loadout array escaped body state");
    }
    private static void Check(bool reload)
    {
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Loadout reload count");var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
            foreach(var row in saved.Bodies)
            {
                var b=World.FindMobile((Serial)row.Body)as AmazonValkyrieBody;Require(b!=null&&b.ControlMaster==p&&b.SavedText==row.Text&&SkillHealth.Balance256(b)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana,"Original equipped Valkyrie identity/payload/wounds/resources changed");Inspect(b);
                var items=ValkyrieEquipment.Originals(b);Require(items.Select(i=>i.Serial.Value).SequenceEqual(row.Items)&&items.Select(i=>i.SavedText).SequenceEqual(row.Payloads),"Actual item serials or property graphs rerolled on reload");
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));return;
        }
        using(var old=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-legacy-body-fixtures.json"))))
        {foreach(var row in old.RootElement.EnumerateArray()){string text=row.GetProperty("State").GetString();var state=JsonSerializer.Deserialize<ValkyrieBodyState>(text);AmazonValkyrieBody.Validate(state);Require(state.Equipment==null&&Json(state)==text,"Old supplied-body JSON changed under optional loadout extension");}}
        var owner=Owner();Rollback(owner);var held=new System.Collections.Generic.List<Held>();
        foreach(int rank in new[]{1,2,4,5,7,8,10,11,13,14,16,17,19,20,21,23,25,27,40})foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
        {
            var body=Body(owner,rank,difficulty);var pet=new SourceSeedStream(SourceRandom.InitLow(100+rank));var game=new SourceSeedStream(SourceRandom.InitLow(900+(int)difficulty));
            var alias=new SourceSeedStream(123);bool refused=false;try{ValkyrieEquipment.Materialize(body,alias,alias);}catch(NotSupportedException){refused=true;}Require(refused&&alias.Value==123,"Aliased pet/game streams accepted or advanced");
            int mana=NecromancerEffects.ManaBalance256(owner);SkillHealth.SetBalance256(body,body.MaximumLife256-257);int life=SkillHealth.Balance256(body);
            var state=ValkyrieEquipment.Materialize(body,pet,game);Inspect(body);Require(state.PetAfter==pet.Value&&state.GameAfter==game.Value&&SkillHealth.Balance256(body)==life&&NecromancerEffects.ManaBalance256(owner)==mana,"Equipment changed pools or lost stream continuation");
            ulong beforePet=pet.Value,beforeGame=game.Value;refused=false;try{ValkyrieEquipment.Materialize(body,pet,game);}catch(NotSupportedException){refused=true;}Require(refused&&pet.Value==beforePet&&game.Value==beforeGame,"Existing loadout rerolled");
            var originals=ValkyrieEquipment.Originals(body);string text=body.SavedText;refused=false;
            try{body.RestorePrepared(body.Prepared with{Equipment=state with{GameAfter=state.GameAfter^1}});}catch(InvalidDataException){refused=true;}Require(refused&&body.SavedText==text,"Bad continuation partially changed body");
            var first=originals[0];owner.Backpack.DropItem(first);refused=false;try{ValkyrieEquipment.Originals(body);}catch(NotSupportedException){refused=true;}Require(refused,"Transferred item fabricated a replacement");body.AddItem(first);Inspect(body);
            PersistentMinions.Suspend(owner);held.Add(new(body.Serial.Value,body.SavedText,originals.Select(i=>i.Serial.Value).ToArray(),originals.Select(i=>i.SavedText).ToArray(),life,mana));
        }
        Require(held.Count==57,"Rank/difficulty loadout coverage");owner.Internalize();File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,held.ToArray())));
        Log.Information("Valkyrie equipment:57actual captured-rank/difficulty loadouts,19rank boundaries;full MonEquip order,independent source streams,private original item flags/durability,atomic failure/no seed rewind,alias/reentry/transfer refusal and original saves checked;live stat/passive/proc/combat admission remains separate");
    }
}
