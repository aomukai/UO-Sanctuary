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

public static class CorpseBaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CorpseBaseChecks));
    private sealed record Saved(CorpseSkillRecord[] Rows);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"corpse-base-fixtures.json");
    public static void Configure()
    {Register("corpse-base-check",Check);Register("corpse-base-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current corpse base-life capture and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);Log.Information("corpse-base-result: passed");}catch(Exception e){Log.Error(e,"{Name}: failed",name);Log.Error("corpse-base-result: failed");}}));
    private static void Require(bool condition,string message){if(!condition){throw new InvalidOperationException(message);}}
    private static void Goldens()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"corpse-base-life-source-probe.json")));
        var ids=doc.RootElement.GetProperty("ids").EnumerateArray().Select(x=>x.GetString()).ToArray();
        var catalogue=new CorpseBaseCatalogue(BalanceCatalog.Read<CorpseBaseMonster>("corpse-base-monsters"),BalanceCatalog.Read<CorpseBaseLevel>("corpse-base-levels"));
        int count=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();
            bool success=catalogue.TryCalculate(ids[V(0)],V(1)!=0,V(2),V(3),out var result);
            Require(success==(V(4)!=0) && result.Minimum==V(5) && result.Maximum==V(6) && result.Base256==V(7),"Native catalogue differs from complete source output");count++;
        }
        Require(count==52100 && !catalogue.TryCalculate("unsupported-source",true,0,1,out _),"Catalogue coverage/unknown-source fallback");
        Log.Information("Corpse base:52100 native packaged/source comparisons passed");
    }
    private static Corpse Body(PlayerMobile owner,MonsterDifficulty difficulty)
    {
        var victim=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(victim,difficulty);}
        victim.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);victim.AIObject.AITimer.Stop();
        string source=CreatureProfiles.Definition(victim).Reference;int level=CreatureProfiles.Level(victim);uint serial=victim.Serial.Value;
        var catalogue=new CorpseBaseCatalogue(BalanceCatalog.Read<CorpseBaseMonster>("corpse-base-monsters"),BalanceCatalog.Read<CorpseBaseLevel>("corpse-base-levels"));
        Require(catalogue.TryCalculate(source,true,(int)difficulty,level,out var expected),"Fixture source profile missing");
        // Actual spawned life deliberately disagrees with the source base.
        victim.SetHits(12345);victim.Hits=victim.HitsMax;
        Require(expected.Base256!=victim.HitsMax*256,"Spawned-life rejection control lacks contrast");
        Corpse corpse=null;var prior=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,container)=>
        {
            if(actor!=victim){prior?.Invoke(actor,container);return;}
            // Exercise the real death callback/capture without random treasure
            // generation; this control makes no loot-generator claim.
            CorpseSkillRules.Capture(actor,container);corpse=container as Corpse;corpse?.DropItem(new Gold(1));
        };
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=prior;}
        Require(corpse is{Deleted:false},"Actual death did not retain a corpse");
        Require(CorpseSkillRules.TryBaseLife(corpse,out var snapshot),"Actual death did not capture source base life");
        Require(snapshot.OriginalMonsterSerial==serial && snapshot.OriginalMonsterSerial!=corpse.Serial.Value
            && snapshot.Level==level && snapshot.Difficulty==difficulty && snapshot.Base256==expected.Base256,"Source identity/level/difficulty/base-life mapping");
        var retained=CorpseSkillRules.Profiles.Find(corpse);CorpseSkillRules.Capture(victim,corpse);
        Require(CorpseSkillRules.Profiles.Find(corpse)==retained,"Repeated capture changed historical base life");
        corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static byte[] Bytes(CorpseSkillRecord[] rows)
    {var writer=new BufferWriter(new byte[1024*1024],true);writer.Write(0);writer.Write(JsonSerializer.Serialize(rows));return writer.Buffer[..(int)writer.Position];}
    private static byte[] Snapshot()=>Task.Run(()=>
    {var writer=new BufferWriter(new byte[1024*1024],true);CorpseSkillRules.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}).GetAwaiter().GetResult();
    private static void PersistenceControls(CorpseSkillRecord valid,CorpseSkillRecord legacy)
    {
        byte[] before=Snapshot();var reader=new BufferReader(before);Require(reader.ReadInt()==0,"Corpse metadata save version changed");
        var rows=JsonSerializer.Deserialize<CorpseSkillRecord[]>(reader.ReadString());
        Require(rows.Contains(valid) && rows.Contains(legacy) && legacy.BaseLife==null,"Worker snapshot dropped base/legacy records");
        try
        {
            foreach(var bad in new[]{valid with{BaseLife=valid.BaseLife with{OriginalMonsterSerial=0}},valid with{BaseLife=valid.BaseLife with{Level=0}},
                valid with{BaseLife=valid.BaseLife with{Difficulty=(MonsterDifficulty)99}},valid with{BaseLife=valid.BaseLife with{Base256=-1}},valid with{Source=null}})
            {
                bool rejected=false;try{CorpseSkillRules.Profiles.Deserialize(new BufferReader(Bytes(new[]{bad})));}catch(InvalidDataException){rejected=true;}
                Require(rejected,"Malformed corpse base snapshot was accepted");
            }
            // Old v0 JSON lacks the optional member entirely.
            var node=JsonSerializer.SerializeToNode(legacy).AsObject();node.Remove("BaseLife");
            var writer=new BufferWriter(new byte[1024*1024],true);writer.Write(0);writer.Write("["+node.ToJsonString()+"]");
            CorpseSkillRules.Profiles.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
            Require(CorpseSkillRules.Profiles.Find((Corpse)World.FindItem((Serial)legacy.Corpse))==legacy,"Legacy v0 payload did not retain default-null snapshot");
        }
        finally{CorpseSkillRules.Profiles.Deserialize(new BufferReader(before));}
        Require(CorpseSkillRules.Profiles.Find((Corpse)World.FindItem((Serial)valid.Corpse))==valid,"Worker/validation control changed retained source context");
    }
    private static void Check()
    {
        Goldens();var owner=new PlayerMobile{Body=0x190,Name="CorpseBaseFixture",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);FixtureSpace.Corridor(owner,6060,12,2000);
        var corpses=new[]{Body(owner,MonsterDifficulty.Normal),Body(owner,MonsterDifficulty.Nightmare),Body(owner,MonsterDifficulty.Hell)};
        var legacyBody=Body(owner,MonsterDifficulty.Normal);var legacy=CorpseSkillRules.Profiles.Find(legacyBody) with{BaseLife=null};CorpseSkillRules.Profiles.Store(legacy);
        Require(!CorpseSkillRules.TryBaseLife(legacyBody,out _),"Legacy source guessed historical base life");
        var retry=new GraveyardSkeleton{Frozen=true};try{CorpseSkillRules.Capture(retry,legacyBody);}finally{retry.Delete();}
        Require(!CorpseSkillRules.TryBaseLife(legacyBody,out _),"Repeated callback retrofitted a legacy corpse");
        var deleted=Body(owner,MonsterDifficulty.Normal);deleted.Delete();Require(CorpseSkillRules.Profiles.Find(deleted)==null,"Corpse deletion retained metadata");
        var rows=corpses.Select(CorpseSkillRules.Profiles.Find).Append(legacy).ToArray();PersistenceControls(rows[0],legacy);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(rows)));owner.Delete();
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Rows.Length==4,"Incomplete corpse reload fixture");
        foreach(var row in saved.Rows)
        {
            var corpse=World.FindItem((Serial)row.Corpse) as Corpse;
            Require(corpse is{Deleted:false} && CorpseSkillRules.Profiles.Find(corpse)==row,"Saved corpse lost original source snapshot");
            Require(CorpseSkillRules.TryBaseLife(corpse,out var snapshot)==(row.BaseLife!=null) && snapshot==row.BaseLife,"Reload invented or changed base life");
        }
        PersistenceControls(saved.Rows[0],saved.Rows[^1]);
    }
}
