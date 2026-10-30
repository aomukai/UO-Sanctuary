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
public static class SourceCorpseIdentityChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SourceCorpseIdentityChecks));
    private sealed record Saved(CorpseSkillRecord[] Rows,uint[] Loot);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"source-corpse-identity-fixtures.json");
    public static void Configure(){Register("source-corpse-identity-check",Check);Register("source-corpse-identity-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Historical corpse GUID/species lookup and index persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Refuses(Action action,string why)
    {bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    private static void Catalogue()
    {
        int count=0;
        foreach(var r in BalanceCatalog.Read<GenericPetSearchMonster>("generic-pet-search-monsters"))
        {Require(GenericPetSearchTables.SourceId(r.Name)==r.Id&&GenericPetSearchTables.SourceId(r.Name.ToUpperInvariant())==r.Id,"Current species ID mismatch");count+=2;}
        Require(count==1502&&GenericPetSearchTables.SourceId(null)==null&&GenericPetSearchTables.SourceId("unsupported-source")==null,"Incomplete species identity scope");
    }
    private static Corpse Body(PlayerMobile owner)
    {
        var corpse=DeathSentryExplosionChecks.Body(owner,new(owner.X+1,owner.Y,owner.Z));
        corpse.Owner?.Delete();Require(corpse.Owner is not{Deleted:false},"Orphan fixture still has live native owner");return corpse;
    }
    private static void Exercise(Corpse corpse)
    {
        var profiles=CorpseSkillRules.Profiles;var original=profiles.Find(corpse);int id=checked((int)original.BaseLife.OriginalMonsterSerial);
        var registry=new GenericPetWorldIdentity(719);var unit=SourceCorpseIdentity.For(corpse);
        Require(unit.Id==id&&unit.Id!=corpse.Serial.Value&&unit.Body==corpse&&unit.Snapshot==original&&unit.ClassId==GenericPetSearchTables.SourceId(original.Source),"Historical corpse identity/species substituted native item or live owner");
        Require(SourceCorpseIdentity.Find(id)==unit&&registry.Resolve(719,1,id)==id&&registry.Resolve(719,0,id)==0,"Orphan source monster registry lookup failed");
        Refuses(()=>registry.Resolve(719,4,checked((int)corpse.Serial.Value)),"Corpse exposed as source loot");
        Refuses(()=>registry.Resolve(719,1,checked((int)corpse.Serial.Value)),"Corpse item serial silently used as source monster GUID");
        var copy=profiles.ForOriginal((uint)id);Require(copy.Length==1&&copy[0]==original,"Original ID index incomplete");copy[0]=null;
        Require(profiles.ForOriginal((uint)id).Single()==original,"Caller changed original ID index");
        try
        {
            profiles.Store(original with{BaseLife=null});Require(profiles.ForOriginal((uint)id).Length==0,"Replacing row left stale original ID");
            Refuses(()=>SourceCorpseIdentity.For(corpse),"Legacy corpse fabricated historical identity");
            profiles.Store(original with{Source="unsupported-source"});Refuses(()=>SourceCorpseIdentity.Find(id),"Unmapped saved species silently accepted");
            profiles.Store(original with{Source=null});Refuses(()=>SourceCorpseIdentity.For(corpse),"Missing saved species reconstructed");
            profiles.Store(original);Require(CorpseSkillRules.Reserve(corpse),"Could not exercise reservation update");
            Require(SourceCorpseIdentity.Find(id)?.Id==id&&registry.Resolve(719,1,id)==id&&profiles.ForOriginal((uint)id).Length==1,"Reservation changed GUID or duplicated the index");
            profiles.Store(original with{Shattered=true,NoSelect=true});Require(SourceCorpseIdentity.Find(id)?.Id==id,"State/eligibility flags removed source identity");
        }
        finally{profiles.Store(original);}
        Require(profiles.Find(corpse)==original&&SourceCorpseIdentity.Find(id)==unit,"Identity controls failed to restore original row");
    }
    private static void RoundTrip(Corpse[] bodies)
    {
        var rows=bodies.Select(CorpseSkillRules.Profiles.Find).ToArray();
        byte[] bytes=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);CorpseSkillRules.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0,"Corpse persistence version changed");
        var serialized=JsonSerializer.Deserialize<CorpseSkillRecord[]>(reader.ReadString());Require(rows.All(serialized.Contains),"Worker snapshot omitted corpse source records");
        CorpseSkillRules.Profiles.Deserialize(new BufferReader(bytes));
        foreach(var row in rows){Require(CorpseSkillRules.Profiles.ForOriginal(row.BaseLife.OriginalMonsterSerial).Contains(row),"Deserialize did not rebuild reverse identity index");}
    }
    private static void Check()
    {
        Catalogue();var owner=new PlayerMobile{Name="SourceCorpseIdentityOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);
        FixtureSpace.Place(owner,new(6100,1650,0),Map.Trammel);var bodies=new[]{Body(owner),Body(owner)};
        foreach(var corpse in bodies){Exercise(corpse);}
        var first=CorpseSkillRules.Profiles.Find(bodies[0]);
        var duplicate=new Corpse(bodies[0].Owner,[]);duplicate.MoveToWorld(bodies[0].Location,bodies[0].Map);
        try
        {
            CorpseSkillRules.Profiles.Store(first with{Corpse=duplicate.Serial.Value});
            Require(CorpseSkillRules.Profiles.ForOriginal(first.BaseLife.OriginalMonsterSerial).Length==2,"Ambiguous history was silently deduplicated");
            bool ambiguous=false;
            try{SourceCorpseIdentity.Find(checked((int)first.BaseLife.OriginalMonsterSerial));}
            catch(NotSupportedException e){ambiguous=e.Message.Contains("Multiple live native corpses");}
            Require(ambiguous,"Duplicate corpse control did not reach the actual ambiguity boundary");
        }
        finally{duplicate.Delete();}
        var deleted=Body(owner);uint old=CorpseSkillRules.Profiles.Find(deleted).BaseLife.OriginalMonsterSerial;deleted.Delete();
        Require(CorpseSkillRules.Profiles.ForOriginal(old).Length==0&&SourceCorpseIdentity.Find(checked((int)old))==null,"Corpse deletion left an indexed source unit");
        RoundTrip(bodies);var saved=new Saved(bodies.Select(CorpseSkillRules.Profiles.Find).ToArray(),bodies.Select(c=>c.Items.Single().Serial.Value).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));owner.Delete();Report();
    }
    private static void Reload()
    {
        Catalogue();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var bodies=saved.Rows.Select(r=>World.FindItem((Serial)r.Corpse) as Corpse).ToArray();
        for(int i=0;i<bodies.Length;i++)
        {
            var c=bodies[i];Require(c is{Deleted:false}&&c.Owner==null&&CorpseSkillRules.Profiles.Find(c)==saved.Rows[i]&&c.Items.Single().Serial.Value==saved.Loot[i],"Reload changed source identity,ownerless history or original loot");Exercise(c);
        }
        RoundTrip(bodies);Report();
    }
    private static void Report()
    {
        Log.Information("Source corpse identity:751species/1502case-folded IDs,original monster GUID versus item serial,ownerless registry lookup,legacy/unmapped/ambiguous boundaries,reservation/state-independent identity,index replacement/deletion and unchanged-v0 rebuild pass;Revive combat/special abilities and full corpse state projection remain gated");
    }
}
