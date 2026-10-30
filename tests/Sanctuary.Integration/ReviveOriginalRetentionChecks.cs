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
public static class ReviveOriginalRetentionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveOriginalRetentionChecks));
    private sealed record Native(uint Corpse,uint Actor,string Type,int Strength,int Dexterity,int Intelligence,int MinDamage,int MaxDamage,int Loyalty,int Slots,int Physical,int Fire,int Cold,string[] Abilities,uint[] Loot);
    private sealed class OtherDeath:BaseCreature
    {
        public OtherDeath():base(AIType.AI_Melee,FightMode.Closest,10,1){}
        public override void OnDeath(Container c)=>base.OnDeath(c);
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-original-retention-fixtures.json");
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static string[] Abilities(BaseCreature actor)=>actor.GetMonsterAbilities()?.Select(a=>a.GetType().FullName).ToArray()??[];
    public static void Configure(){Register("revive-original-retention-check",Check);Register("revive-original-retention-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Actual original NPC storage,dead state and native save identity",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Corpse Kill(PlayerMobile owner,BaseCreature actor)
    {
        actor.NoKillAwards=true;actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject?.AITimer.Stop();
        Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,container)=>{died?.Invoke(who,container);if(who==actor){corpse=container as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(actor,true);actor.Damage(actor.Hits+1,owner);CombatDamage.Finish(actor,owner);}
        finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false},"Death did not retain its actual corpse");corpse.BeginDecay(TimeSpan.FromHours(1));return corpse;
    }
    private static void AssertNative(Native saved)
    {
        var corpse=World.FindItem((Serial)saved.Corpse) as Corpse;var actor=ReviveOriginals.Find(corpse);
        Require(actor!=null&&World.FindMobile((Serial)saved.Actor)==actor&&corpse.Owner==actor&&actor.GetType().FullName==saved.Type,"Original actor was deleted,changed type or replaced");
        Require(!actor.Alive&&!actor.Deleted&&actor.Map==Map.Internal&&actor.Combatant==null&&!actor.Warmode&&actor.Blessed&&actor.Hidden&&actor.Frozen&&actor.CantWalk,"Retained native actor is active or exposed");
        Require(actor.RawStr==saved.Strength&&actor.RawDex==saved.Dexterity&&actor.RawInt==saved.Intelligence&&actor.DamageMin==saved.MinDamage&&actor.DamageMax==saved.MaxDamage
            &&actor.Loyalty==saved.Loyalty&&actor.ControlSlots==saved.Slots&&actor.PhysicalDamage==saved.Physical&&actor.FireDamage==saved.Fire&&actor.ColdDamage==saved.Cold
            &&Abilities(actor).SequenceEqual(saved.Abilities),"Native original fields or ability definitions changed");
        Require(corpse.Items.Select(i=>i.Serial.Value).Order().SequenceEqual(saved.Loot),"Retained original duplicated or lost corpse loot");
        var identity=SourceCorpseIdentity.For(corpse);Require(identity.Id==saved.Actor&&SourceCorpseIdentity.Find(identity.Id)?.Body==corpse,"Historical identity no longer resolves its original actor/corpse");
        var registry=new GenericPetWorldIdentity(37);Require(registry.Resolve(37,1,identity.Id)==identity.Id,"Retained original is not available through source identity");
    }
    private static byte[] Payload(ReviveOriginalState[] rows,int version=0)
    {var w=new BufferWriter(new byte[1024*1024],true);w.Write(version);w.Write(JsonSerializer.Serialize(rows));return w.Buffer[..(int)w.Position];}
    private static void Validate()
    {
        var before=ReviveOriginals.Snapshot();Require(before.Length==9,"Incomplete original storage fixture");var row=before[0];
        foreach(var bad in new[]{new[]{row with{Corpse=0}},new[]{row with{Actor=0}},new[]{row with{RuntimeType=""}},new[]{row,row},new[]{row,before[1] with{Actor=row.Actor}},new[]{row with{Actor=row.Corpse}}})
        {
            bool refused=false;try{ReviveOriginals.Profiles.Deserialize(new BufferReader(Payload(bad)));}catch(InvalidDataException){refused=true;}
            Require(refused&&ReviveOriginals.Snapshot().SequenceEqual(before),"Malformed retained-original restore was not atomic");
        }
        bool version=false;try{ReviveOriginals.Profiles.Deserialize(new BufferReader(Payload(before,99)));}catch(InvalidDataException){version=true;}Require(version,"Unknown retained-original version accepted");
        var bytes=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);ReviveOriginals.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        ReviveOriginals.Profiles.Deserialize(new BufferReader(bytes));Require(ReviveOriginals.Snapshot().SequenceEqual(before),"Native original metadata roundtrip changed rows");
        Require(!NecromancerCombat.Implemented(CombatAbility.Revive),"Original storage enabled unfinished learned Revive");
    }
    private static void Check()
    {
        var owner=new PlayerMobile{Name="OriginalRetentionOwner",Player=true,Body=0x190};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);
        FixtureSpace.Place(owner,new(6100,1680,0),Map.Trammel);
        // Default gameplay capture is still gated. Normal NPC death remains unchanged.
        var control=new Skeleton();var oldCorpse=Kill(owner,control);Require(control.Deleted&&ReviveOriginals.Find(oldCorpse)==null,"Disabled learned route retained an ordinary death");oldCorpse.Delete();
        Require(!ReviveOriginals.SupportedDeathPath(typeof(OtherDeath)),"Unknown derived death side effects were silently accepted");
        var capture=ClassLootHooks.RetainOriginal;ClassLootHooks.RetainOriginal=ReviveOriginals.Capture;
        var rows=new System.Collections.Generic.List<Native>();var breathTarget=new PlayerMobile{Name="RetentionBreathTarget",Player=true,Body=0x190};breathTarget.RawStr=10000;breathTarget.Hits=breathTarget.HitsMax;breathTarget.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        try
        {
            for(int kind=0;kind<5;kind++)
            foreach(var difficulty in kind<2?Enum.GetValues<MonsterDifficulty>():new[]{MonsterDifficulty.Normal})
            {
                BaseCreature actor=kind switch{0=>new GraveyardSkeleton(),1=>new GraveyardZombie(),2=>new Dragon(),3=>new Drake(),_=>new Skeleton()};
                if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}
                actor.Loyalty=77;actor.ControlSlots=3;actor.PhysicalDamage=31;actor.FireDamage=37;actor.ColdDamage=32;
                actor.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);actor.AIObject?.AITimer.Stop();
                if(actor is Dragon)
                {
                    var breath=actor.GetAbility(MonsterAbilityType.FireBreath) as FireBreath;Require(breath!=null&&breath.CanFireBreathTarget(actor,breathTarget),"Dragon breath precondition unavailable");
                    breath.Trigger(MonsterAbilityTrigger.CombatAction,actor,breathTarget);
                }
                var corpse=Kill(owner,actor);
                var saved=new Native(corpse.Serial.Value,actor.Serial.Value,actor.GetType().FullName,actor.RawStr,actor.RawDex,actor.RawInt,actor.DamageMin,actor.DamageMax,actor.Loyalty,actor.ControlSlots,actor.PhysicalDamage,actor.FireDamage,actor.ColdDamage,Abilities(actor),corpse.Items.Select(i=>i.Serial.Value).Order().ToArray());
                AssertNative(saved);rows.Add(saved);
            }
            var disposable=new Skeleton();var gone=Kill(owner,disposable);Require(ReviveOriginals.Find(gone)==disposable,"Cleanup fixture did not retain actor");gone.Delete();Require(disposable.Deleted&&!ReviveOriginals.IsRetained(disposable),"Corpse deletion leaked its retained actor");
            var lost=new Skeleton();var stillLoot=Kill(owner,lost);lost.Delete();ReviveOriginals.Prune();Require(ReviveOriginals.Find(stillLoot)==null&&stillLoot is{Deleted:false}&&stillLoot.Items.Count>0,"External actor deletion removed corpse loot or kept stale storage");stillLoot.Delete();
        }
        finally{ClassLootHooks.RetainOriginal=capture;}
        Require(rows.Count==9,"Incomplete retained actor matrix");Validate();File.WriteAllText(Manifest,JsonSerializer.Serialize(rows));owner.Delete();
        int health=breathTarget.Hits;Timer.StartTimer(TimeSpan.FromSeconds(3),()=>
        {
            try{Require(breathTarget.Hits==health,"Dead retained dragon delivered queued breath damage");foreach(var row in rows){AssertNative(row);}breathTarget.Delete();Log.Information("revive-original-retention-result: passed");Report();}
            catch(Exception e){Log.Error(e,"revive-original-retention-result: failed");}
        });
    }
    private static void Reload()
    {
        var rows=JsonSerializer.Deserialize<Native[]>(File.ReadAllText(Manifest));Require(rows.Length==9,"Retained original fixture lost rows");
        foreach(var row in rows){AssertNative(row);}Validate();Report();
    }
    private static void Report()=>Log.Information("Retained originals:9actual deaths/5native types/3difficulties preserve original serials,native fields,ability definitions and corpse loot;dead hidden internal actors,queued breath refusal,corpse/actor deletion cleanup,7atomic invalid restores and actual reloads pass;pre-death state,equipment,full ability timers,activation and learned Revive remain unfinished");
}
