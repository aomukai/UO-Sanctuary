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

public static class ReviveMeleeGeometryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveMeleeGeometryChecks));
    private sealed record Saved(uint Owner,uint Actor,int Life,ulong Seed,int Generation=0);
    public sealed class Unmapped:BaseCreature
    {
        [Constructible] public Unmapped():base(AIType.AI_Melee,FightMode.None,10){}
        public Unmapped(Serial serial):base(serial){}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-melee-geometry-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-melee-geometry-check",Check);Register("revive-melee-geometry-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original Revive live geometry/result and shared contact",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Item Named(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    internal static BaseCreature Create(PlayerMobile owner,Item[] equipment=null)
    {
        var body=new Skeleton{Name="PoisonDaggerFixtureReviveMeleeGeometry",NoKillAwards=true};
        foreach(var item in body.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){item.Delete();}
        var gear=equipment??new[]{"The Gnasher","Swordback Hold","Goblin Toe"}.Select(Named).ToArray();foreach(var item in gear){body.AddItem(item);}
        Require(gear.All(i=>i.Parent==body),"Original melee fixture slots collided");
        body.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);body.AIObject.AITimer.Stop();Corpse corpse=null;var died=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{died?.Invoke(who,item);if(who==body){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{owner.DoHarmful(body,true);body.Damage(body.Hits+1,owner);CombatDamage.Finish(body,owner);}finally{ClassLootHooks.CreatureDied=died;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==body,"Original melee body was not retained");corpse.BeginDecay(FixtureSpace.Retention);
        var original=ReviveOriginals.State(corpse);var before=ReviveBeforeDeath.Decode(original.BeforeDeathJson);
        var setup=ReviveEquipmentGraph.Prepare(ReviveLife.Setup(owner,corpse,before.Mana,before.Stamina)with{RegenerationRate=ReviveRegeneration.Rate("skeleton1")},original);
        Require(ReviveNativeActivation.CommitPrepared(owner,corpse,corpse.Location,before.AiStatus.Value,setup,out var actual,out var issue)&&actual==body,"Original melee activation failed: "+issue);
        foreach(var item in gear){Require(item.Parent==corpse,"Combat gear stopped being separate loot");owner.AddToBackpack(item);item.Delete();}
        return body;
    }
    private static ulong Seed(bool hit)
    {
        for(int low=0;low<10000;low++)
        {ulong seed=SourceRandom.InitLow(low),next=seed;uint roll=SourceRandom.Roll(ref next)%100;if(hit?roll<5:roll>=95){return seed;}}
        throw new InvalidOperationException("No deterministic result seed");
    }
    private static ReviveEventChecks.Recipient Target(PlayerMobile owner,BaseCreature body)
    {
        var target=new ReviveEventChecks.Recipient{NoKillAwards=true};target.SetHits(40000);
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}
        target.MoveToWorld(new(body.X+1,body.Y,body.Z),owner.Map);target.AIObject.AITimer.Stop();return target;
    }
    private static void Exercise(PlayerMobile owner,BaseCreature source)
    {
        owner.MoveToWorld(new(6600,3640,0),Map.Trammel);source.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);source.AIObject.AITimer.Stop();
        var unit=ReviveMeleeGeometry.Unit(source);Require(unit.BaseId==0&&unit.Size==2&&unit.RawRange==0&&unit.Cell==PetOwnerRoutes.Cell(source),"Original melee identity/footprint did not use retained source");
        foreach(var row in BalanceCatalog.Read<NpcMeleeGeometryProfile>("revive-melee-geometry"))
        {Require(ReviveMeleeGeometry.Profile(row.Key)==row&&NativeMonsterReturn.Identity(row.Key).Class==row.BaseId,"Melee and return source catalogs disagree");}
        foreach(var scene in new[]{"hit","miss","range","facet","height","friendly"})
        {
            var target=Target(owner,source);try
            {
                if(scene=="range"){target.Location=new(source.X+12,source.Y,source.Z);}
                if(scene=="facet"){target.MoveToWorld(target.Location,Map.Felucca);}
                if(scene=="height"){target.Z+=16;}
                if(scene=="friendly"){Require(target.SetControlMaster(owner),"Friendly result control failed");}
                ulong seed=Seed(scene!="miss");NpcEquipmentRandom.Set(source,seed);ulong after=seed;
                bool draws=scene is "hit" or "miss";if(draws){SourceRandom.Roll(ref after);}
                int flags=ReviveMeleeGeometry.Result(source,owner,target);
                Require(flags==(scene=="hit"?5:0)&&NpcEquipmentRandom.State(source).Seed==after,"Live range/hostility/hit result or exact hit-stream draw differs: "+scene);
            }
            finally{target.Corpse?.Delete();target.Delete();}
        }
        var blocker=new GraveyardSkeleton{NoKillAwards=true};blocker.SetHits(40000);blocker.MoveToWorld(new(source.X+1,source.Y,source.Z),owner.Map);blocker.AIObject.AITimer.Stop();
        var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;
        try
        {
            var rule=BalanceCatalog.Read<SourceBlockRule>("source-block-rules").Single(r=>r.Source=="skeleton1");int shield=Array.FindIndex(rule.ShieldItems,x=>x);
            Require(shield>=0,"Missing actual source shield control");SourceBlocking.ChoiceRoll=_=>shield;SourceBlocking.BlockRoll=_=>0;
            Require(SourceBlocking.TryRate(blocker,out int rate,out _)&&rate>0,"Source shield control has zero block chance");
            NpcEquipmentRandom.Set(source,Seed(true));Require(ReviveMeleeGeometry.Result(source,owner,blocker)==16,"Live recipient blocking did not clear success/set source block flag");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;blocker.Delete();}
        var rayTarget=Target(owner,source);rayTarget.Location=new(source.X+6,source.Y,source.Z);
        int wallTile=Enumerable.Range(1,0x200).First(id=>(TileData.ItemTable[id].Flags&(TileFlag.Window|TileFlag.NoShoot))!=0
            &&(TileData.ItemTable[id].Flags&TileFlag.Door)==0&&TileData.ItemTable[id].CalcHeight>=16);
        var wall=new Static(wallTile);
        try
        {
            // The explicit large range bonus reaches beyond overlapping footprints,
            // forcing the actual shrunk-endpoint native ray service to execute.
            Require(ReviveMeleeGeometry.Within(source,rayTarget,100),"Clear native endpoint ray was blocked");
            wall.MoveToWorld(new(source.X+3,source.Y,source.Z),source.Map);
            Require(!source.Map.LineOfSight(new Point3D(source.X+1,source.Y,source.Z+14),new Point3D(rayTarget.X-1,rayTarget.Y,rayTarget.Z+14))
                &&!ReviveMeleeGeometry.Within(source,rayTarget,100),"Native wall did not block the requested source endpoint ray");
            wall.Delete();Require(ReviveMeleeGeometry.Within(source,rayTarget,100),"Removing native wall did not restore endpoint ray");
        }
        finally{wall.Delete();rayTarget.Delete();}
        foreach(bool escapes in new[]{false,true})
        {
            var target=Target(owner,source);try
            {
                NpcEquipmentRandom.Set(source,Seed(true));int before=SkillHealth.Balance256(target);
                var packet=ReviveMeleeGeometry.Prepare(source,owner,target);Require((packet.Impact.Packet.ResultFlags&1)!=0,"Actual geometry/result did not prepare a hit");
                if(escapes){target.Location=new(source.X+12,source.Y,source.Z);}
                var result=ReviveMeleeGeometry.Release(source,owner,target,packet,TimedEffects.Now);
                Require(packet.Consumed&&(escapes?result.Exit==MonsterContactExit.OutOfRangeFreed&&target.Notifications==0&&SkillHealth.Balance256(target)==before
                    :result.Exit==MonsterContactExit.Finished&&result.Commit!=null&&target.Notifications==1&&target.WoundedAtNotification&&SkillHealth.Balance256(target)<before),"Actual original geometry/result contact lost range/events/HP ordering");
                bool refused=false;try{_=ReviveMeleeGeometry.Release(source,owner,target,packet,TimedEffects.Now);}catch(InvalidOperationException){refused=true;}Require(refused,"Actual original contact replayed");
            }
            finally{target.Corpse?.Delete();target.Delete();}
        }
        var unknown=new Unmapped{NoKillAwards=true};unknown.MoveToWorld(source.Location,owner.Map);
        try{ulong before=NpcEquipmentRandom.State(source).Seed;bool refused=false;try{_=ReviveMeleeGeometry.Prepare(source,owner,unknown);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(source).Seed==before,"Unknown native identity consumed RNG or guessed geometry");}
        finally{unknown.Delete();}
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)*2/3+17);owner.Internalize();source.Internalize();
        Require(!ReviveMeleeGeometry.Within(source,source,0),"Internal source unexpectedly admitted");
    }
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveMeleeGeometryOwner";
        MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6600,24,3640);
        var actor=Create(owner);Exercise(owner,actor);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,SkillHealth.Balance256(actor),NpcEquipmentRandom.State(actor).Seed)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var actor=World.FindMobile((Serial)saved.Actor)as BaseCreature;
        Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==saved.Life&&NpcEquipmentRandom.State(actor)?.Seed==saved.Seed,"Original geometry fixture lost wounds/stream/identity on reload");
        ulong next=saved.Seed;uint expected=SourceRandom.Roll(ref next);Require(NpcEquipmentRandom.Raw(actor)==expected&&NpcEquipmentRandom.State(actor).Seed==next,"Restored original result stream reseeded");
        Exercise(owner,actor);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(actor),Seed=NpcEquipmentRandom.State(actor).Seed,Generation=saved.Generation+1}));
    }
}
