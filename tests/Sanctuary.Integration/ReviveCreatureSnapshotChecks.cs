// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class ReviveCreatureSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveCreatureSnapshotChecks));
    private sealed record Saved(CorpseSkillRecord[] Rows,uint[][] Loot);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-creature-snapshot-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-creature-snapshot-check",Check);Register("revive-creature-snapshot-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Original native type/active combat descriptor and save compatibility",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static byte[] Payload(CorpseSkillRecord[] rows)
    {var w=new BufferWriter(new byte[8*1024*1024],true);w.Write(0);w.Write(JsonSerializer.Serialize(rows));return w.Buffer[..(int)w.Position];}
    private static byte[] Snapshot()=>Task.Run(()=>{var w=new BufferWriter(new byte[8*1024*1024],true);CorpseSkillRules.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
    private static Corpse Body(PlayerMobile owner,int kind,MonsterDifficulty difficulty)
    {
        BaseCreature victim=kind switch{0=>new GraveyardSkeleton(),1=>new GraveyardZombie(),2=>new Dragon(),3=>new Drake(),_=>new Skeleton()};
        victim.Frozen=victim.CantWalk=victim.NoKillAwards=true;
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(victim,difficulty);}
        victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);victim.AIObject?.AITimer.Stop();
        string source=CreatureProfiles.Definition(victim).Reference;var expected=ReviveCreatureSnapshots.Capture(victim,source);
        var observed=ReviveCreatureSnapshots.Combat(expected);
        Require(observed.Type==victim.GetType().FullName&&observed.Level==CreatureProfiles.Level(victim)
            &&observed.AttackRating==CreatureProfiles.AttackRating(victim)&&observed.Defense==CreatureProfiles.Defense(victim)
            &&observed.Life==victim.HitsMax,"Capture imported a proposed variant instead of active combat values");
        int rawStrength=victim.RawStr,rawDexterity=victim.RawDex,rawIntelligence=victim.RawInt;var body=victim.Body;int hue=victim.Hue;
        Corpse corpse=null;var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>
        {
            if(actor!=victim){previous?.Invoke(actor,container);return;}
            CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;corpse?.DropItem(new Gold(1));
        };
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=previous;}
        Require(corpse is{Deleted:false},"Actual death did not create a corpse");
        var row=CorpseSkillRules.Profiles.Find(corpse);
        Require(row?.ReviveCreature==expected&&row.ReviveSnapshotIssue==null,"Death capture changed or omitted original native/active combat descriptor");
        Require(expected.RawStrength==rawStrength&&expected.RawDexterity==rawDexterity&&expected.RawIntelligence==rawIntelligence
            &&expected.Body==body.BodyID&&expected.Hue==hue&&!expected.NativeAbilityStateCaptured,"Original descriptor omitted native fields or falsely certified special state");
        victim.Delete();corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void Exercise(Corpse corpse)
    {
        var row=CorpseSkillRules.Profiles.Find(corpse);var native=row.ReviveCreature;var unit=SourceCorpseIdentity.For(corpse);
        Require(unit.OriginalCreature==native&&!native.NativeAbilityStateCaptured,"Source corpse did not retain actual descriptor or hid incomplete native abilities");
        var a=ReviveCreatureSnapshots.Combat(native);var b=ReviveCreatureSnapshots.Combat(native);
        Require(!ReferenceEquals(a.Profile,b.Profile)&&(a.Profile.SourceStats==null||!ReferenceEquals(a.Profile.SourceStats,b.Profile.SourceStats)),"Decoded profiles share mutable dictionaries");
        if(a.Profile.SourceStats is System.Collections.Generic.IDictionary<string,string> values){values["revive-test-mutation"]="1";}
        Require(ReviveCreatureSnapshots.Combat(native).Profile.SourceStats?.ContainsKey("revive-test-mutation")!=true,"Caller modified saved original combat descriptor");
        var before=Snapshot();var profiles=CorpseSkillRules.Profiles;
        try
        {
            foreach(var bad in new[]{native with{Version=99},native with{RuntimeType=""},native with{Assembly=""},
                native with{ActiveCombatJson="{"},native with{NativeAbilityStateCaptured=true},native with{DamageMinimum=9,DamageMaximum=1}})
            {
                bool refused=false;try{profiles.Deserialize(new BufferReader(Payload([row with{ReviveCreature=bad}])));}catch(InvalidDataException){refused=true;}
                Require(refused,"Invalid original creature snapshot accepted");
            }
            var node=JsonSerializer.SerializeToNode(row).AsObject();node.Remove("ReviveCreature");node.Remove("ReviveSnapshotIssue");
            var w=new BufferWriter(new byte[8*1024*1024],true);w.Write(0);w.Write("["+node.ToJsonString()+"]");profiles.Deserialize(new BufferReader(w.Buffer[..(int)w.Position]));
            var legacy=profiles.Find(corpse);Require(legacy.ReviveCreature==null&&legacy.ReviveSnapshotIssue==null&&legacy.BaseLife==row.BaseLife,"Old v0 corpse lost existing fields or fabricated reconstruction data");
            bool missing=false;try{_ = SourceCorpseIdentity.For(corpse).OriginalCreature;}catch(NotSupportedException){missing=true;}Require(missing,"Legacy corpse guessed a native creature type");
        }
        finally{profiles.Deserialize(new BufferReader(before));}
        Require(profiles.Find(corpse)==row&&SourceCorpseIdentity.For(corpse).OriginalCreature==native,"Validation/legacy controls changed retained original descriptor");
    }
    private static void Check()
    {
        var owner=new PlayerMobile{Name="ReviveCreatureSnapshotOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);
        FixtureSpace.Place(owner,new(6100,1670,0),Map.Trammel);
        var bodies=(from kind in new[]{0,1} from difficulty in Enum.GetValues<MonsterDifficulty>() select Body(owner,kind,difficulty))
            .Concat(new[]{Body(owner,2,MonsterDifficulty.Normal),Body(owner,3,MonsterDifficulty.Normal),Body(owner,4,MonsterDifficulty.Normal)}).ToArray();
        foreach(var corpse in bodies){Exercise(corpse);}
        var rows=bodies.Select(CorpseSkillRules.Profiles.Find).ToArray();
        Require(rows.Select(r=>r.ReviveCreature.RuntimeType).Distinct().Count()==5&&rows.All(r=>!r.ReviveCreature.NativeAbilityStateCaptured),"Original native types collapsed or abilities falsely certified");
        var unknown=new GraveyardLich();unknown.AIObject?.AITimer.Stop();
        try
        {
            Require(CreatureProfiles.Definition(unknown).Reference==null,"Unmapped fixture unexpectedly has a source");
            Require(ReviveCreatureSnapshots.TryCapture(unknown,null,out var issue)==null&&!string.IsNullOrWhiteSpace(issue),"Unavailable reconstruction data was silently invented");
        }
        finally{unknown.Delete();}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(rows,bodies.Select(c=>c.Items.Select(i=>i.Serial.Value).Order().ToArray()).ToArray())));owner.Delete();Report();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Rows.Length==9,"Incomplete original creature fixture");
        for(int i=0;i<saved.Rows.Length;i++)
        {
            var row=saved.Rows[i];var corpse=World.FindItem((Serial)row.Corpse) as Corpse;
            Require(corpse is{Deleted:false}&&corpse.Owner==null&&CorpseSkillRules.Profiles.Find(corpse)==row
                &&corpse.Items.Select(x=>x.Serial.Value).Order().SequenceEqual(saved.Loot[i]),"Reload changed original descriptor/history or loot");Exercise(corpse);
        }
        Report();
    }
    private static void Report()
    {
        Log.Information("Revive creature snapshots:9actual deaths across5native types/3source species/3difficulties retain concrete type,AI/combat descriptors,raw fields and loot;unmapped capture is explicit,independent decoded profiles,6invalid payloads per corpse and old-v0 compatibility pass;native ability state,in-place reconstruction and full actor lifecycle remain gated");
    }
}
