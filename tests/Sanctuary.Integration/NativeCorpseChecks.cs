// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class NativeCorpseChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NativeCorpseChecks));
    private sealed record Saved(int Generation,uint Owner,uint Corpse,uint Gold,CorpseSkillRecord Metadata);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"native-corpse-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="NativeCorpseFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="NativeCorpseFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"native-corpse-reload-check":"native-corpse-check";
            ConsoleInputHandler.RegisterCommand(name,"Explicit native corpse identity/base life and Necromancer consumers",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
        foreach(string choice in new[]{"ce-right","poison-quick","nightmare-ce-right","hell-poison-quick"})
        {
            string mode=choice,label="native-corpse-"+choice;
            ConsoleInputHandler.RegisterCommand(label+"-check","Actual corpse-target input using native classification",_=>Core.LoopContext.Post(()=>
            {FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",label+"-check");}));
        }
    }
    internal static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="NativeCorpseFixture",Player=true,Body=0x190};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(string id in new[]{"necromancer.raiseskeleton","necromancer.skeletalmage","necromancer.corpseexplosion","necromancer.poisonexplosion","necromancer.revive"}){ClassSkillChecks.Train(p,id);}
        FixtureSpace.Corridor(p,6700,36,2500);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    internal static Corpse Corpse(PlayerMobile p,int offset=5,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        var actor=new GraveyardLich{Name="NativeCorpseLich",NoKillAwards=true,Frozen=true,CantWalk=true};if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(actor,difficulty);}
        actor.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);actor.AIObject?.AITimer.Stop();
        var expected=difficulty switch{MonsterDifficulty.Normal=>(Level:5,Life:90),MonsterDifficulty.Nightmare=>(Level:36,Life:1102),MonsterDifficulty.Hell=>(Level:69,Life:9118),_=>throw new InvalidOperationException("Unknown native difficulty")};
        uint identity=actor.Serial.Value;Require(CreatureProfiles.Definition(actor).Reference==null&&CreatureProfiles.Level(actor)==expected.Level&&actor.HitsMax==expected.Life,"Native classification no longer matches authored Lich facts");
        SkillCombat.HitParts(p,actor,new(100000,0,0,0,0),recover:false);var corpse=actor.Corpse as Corpse;Require(corpse is{Deleted:false},"Actual native death produced no corpse");corpse.DropItem(new Gold(17));
        var row=CorpseSkillRules.Profiles.Find(corpse);
        Require(row is{Selectable:true,Owned:false,ReviveCreature:not null,ReviveSnapshotIssue:null}&&row.Source=="sanctuary.native.graveyard-lich.v1"
            &&row.BaseLife is{Seed:not null}&&row.BaseLife.Level==expected.Level&&row.BaseLife.Base256==expected.Life*256&&row.BaseLife.Difficulty==difficulty
            &&row.BaseLife.OriginalMonsterSerial==identity&&GenericPetSearchTables.SourceId(row.Source)==null,"Native corpse acquired incorrect identity/base or fake D2 species");
        CorpseSkillRules.Capture(actor,corpse);Require(CorpseSkillRules.Profiles.Find(corpse)==row,"Repeated capture rerolled native base/seed");return corpse;
    }
    internal static void Clean(PlayerMobile p)
    {NecromancerSpellMissiles.Cancel(p);foreach(var pet in p.AllFollowers?.ToArray()??Array.Empty<BaseCreature>()){pet.Delete();}p.Delete();}
    internal static Corpse KeepForReload(Corpse corpse)
    {
        // The combined class suite exceeds native monster-corpse decay (7m).
        // Only inspected saved bodies get the existing shared retention hold;
        // actual casting/consumption and ordinary world decay stay untouched.
        corpse.BeginDecay(FixtureSpace.Retention);return corpse;
    }
    internal static GraveyardSkeleton Target(PlayerMobile p)
    {
        var target=PoisonDaggerChecks.Target(p);
        // The source NPC recovery loop is separate from UO's regen-rate
        // handler. Keep one-shot damage observable without changing combat.
        TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static void RecoveryControl(PlayerMobile p)
    {
        var target=PoisonDaggerChecks.Target(p);long now=Core.TickCount;int full=SkillHealth.Balance256(target);
        target.RecoverLife(now);SkillCombat.HitParts(p,target,new(20,0,0,0,0),false);Require(SkillHealth.Balance256(target)<full,"Recovery control did not take damage");
        target.RecoverLife(now+1000);Require(SkillHealth.Balance256(target)==full,"Control no longer demonstrates independent source recovery");
        TimedEffects.PreventHealing(target,TimedEffects.Now);SkillCombat.HitParts(p,target,new(20,0,0,0,0),false);int wounded=SkillHealth.Balance256(target);
        target.RecoverLife(now+2000);Require(wounded<full&&SkillHealth.Balance256(target)==wounded,"Fixture healing block failed to preserve observed damage");target.Delete();
    }
    internal static void Cast(PlayerMobile p,CombatAbility ability,Corpse corpse)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        bool started=ability==CombatAbility.RaiseSkeleton?ClassCombat.CastSummon(p,corpse.Serial,Core.TickCount):ClassCombat.BeginCorpse(p,ability,corpse.Serial,corpse.Location,Core.TickCount);
        Require(started,$"Native corpse public start:{ability},region={NecromancerCastRegions.CorpseAllowed(p,ability,corpse)},enabled={ClassCombat.Enabled(p)},overlap={AlliedOverlap.Allowed(p)},alive={p.Alive},frozen={p.Frozen},paralyzed={p.Paralyzed},spell={p.Spell},recovery={HitRecovery.Active(p,Core.TickCount)},rank={SkeletonPets.Rank(p)},mana={p.Mana},usable={SkeletonPets.Usable(p,corpse)},canRaise={SkeletonPets.CanRaise(p,corpse)},space={p.Map.CanSpawnMobile(corpse.X,corpse.Y,corpse.Z)},loot={corpse.CanLoot(p,null)},criminal={corpse.IsCriminalAction(p)},owner={corpse.Owner?.GetType().Name},ownerAlive={corpse.Owner?.Alive},position={p.Location}/{corpse.Location},pending={ClassCombat.For(p).PendingAbility},body={corpse.OwnerWasBaseCreature}/{corpse.OwnerWasSummoned}/{corpse.IsBones},consumed={corpse.SanctuaryConsumed},row={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse))}");var s=ClassCombat.For(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Native corpse paid early");
        bool released=ClassCombat.ResolveAction(p,s.EffectAt);int after=NecromancerEffects.ManaBalance256(p);bool replay=ClassCombat.ResolveAction(p,s.EffectAt);
        Require(released&&!replay&&after==mana-cost,$"Native corpse release:{ability},released={released},replay={replay},mana={after}/{mana-cost},corpseAvailable={CorpseSkillRules.Available(corpse)},space={p.Map.CanSpawnMobile(corpse.X,corpse.Y,corpse.Z)},pendingPoint={s.PendingPoint}");
        Require(!corpse.Deleted&&corpse.SanctuaryConsumed&&corpse.Animated&&!CorpseSkillRules.BodyAvailable(corpse)&&corpse.Items.OfType<Gold>().Single().Amount==17,"Native corpse reused or protected loot lost");
    }
    private static void Check()
    {
        var p=Fresh();bool kept=false;
        try
        {
            RecoveryControl(p);
            foreach(var ability in new[]{CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage,CombatAbility.CorpseExplosion,CombatAbility.PoisonExplosion})
            {
                var corpse=Corpse(p);var target=Target(p);target.MoveToWorld(new(corpse.X+1,corpse.Y,corpse.Z),p.Map);int hp=SkillHealth.Balance256(target);
                int followers=p.AllFollowers?.Count??0;Cast(p,ability,corpse);
                if(ability is CombatAbility.RaiseSkeleton or CombatAbility.RaiseSkeletalMage){Require((p.AllFollowers?.Count??0)>followers,"Native corpse did not create the expected owned summon");}
                if(ability==CombatAbility.CorpseExplosion){Require(SkillHealth.Balance256(target)<hp,"Native corpse base failed actual explosion damage");}
                target.Delete();corpse.Delete();NecromancerSpellMissiles.Cancel(p);foreach(var pet in p.AllFollowers?.ToArray()??Array.Empty<BaseCreature>()){pet.Delete();}
            }
            var savedBody=KeepForReload(Corpse(p,8));int before=NecromancerEffects.ManaBalance256(p);
            bool planned=ReviveCasts.Plan(p,savedBody,out _,out _,out string issue);
            Require(planned==NativeReviveRules.Approved&&(planned||issue==NativeCorpseSources.ForSource(CorpseSkillRules.Profiles.Find(savedBody).Source).ReviveIssue)
                &&!CorpseSkillRules.NoSelect(savedBody)&&NecromancerEffects.ManaBalance256(p)==before,"Unqualified native Revive was invented or consumed its corpse");
            Require(NativeCorpseSources.ForSource(CorpseSkillRules.Profiles.Find(savedBody).Source).Soft==null&&!CorpseSkillRules.Soft(savedBody),"Unassigned native flesh classification was invented");
            var gold=savedBody.Items.OfType<Gold>().Single();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,savedBody.Serial.Value,gold.Serial.Value,CorpseSkillRules.Profiles.Find(savedBody))));kept=true;
            Log.Information("Native corpse:actual Lich death,authored level5/base90,no fake D2identity,once-only public Raise/Mage/CE/Poison consumption+loot and explicit Revive/flesh gaps pass");
        }
        finally{if(!kept){Clean(p);}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var corpse=World.FindItem((Serial)saved.Corpse) as Corpse;
        // Offline players load on the internal map. Restore this fixture's
        // saved world position before checking live range/visibility guards.
        Require(p is{Deleted:false,Alive:true}&&corpse is{Deleted:false},$"Saved native corpse or fixture owner missing:owner={saved.Owner}/{p?.Alive}/{p?.Deleted},corpse={saved.Corpse}/{corpse?.Deleted},gold={World.FindItem((Serial)saved.Gold)?.Parent}");
        if(p.Map==Map.Internal){p.MoveToWorld(p.Location,corpse.Map);}
        Require(corpse is{Deleted:false}&&CorpseSkillRules.Profiles.Find(corpse)==saved.Metadata&&World.FindItem((Serial)saved.Gold)is Gold{Amount:17}gold&&gold.Parent==corpse
            &&CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)&&CorpseExplosionCasts.Supported(corpse),$"Saved native corpse:player={p?.Location}/{p?.Map},alive={p?.Alive},corpse={corpse?.Location}/{corpse?.Map},deleted={corpse?.Deleted},metadata={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse))},saved={JsonSerializer.Serialize(saved.Metadata)},gold={World.FindItem((Serial)saved.Gold)?.Amount}/{World.FindItem((Serial)saved.Gold)?.Parent},body={CorpseSkillRules.BodyAvailable(corpse)},usable={CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)},supported={CorpseExplosionCasts.Supported(corpse)},see={p?.CanSee(corpse)},los={p?.InLOS(corpse)},loot={corpse?.CanLoot(p,null)},criminal={corpse?.IsCriminalAction(p)},flags={corpse?.OwnerWasBaseCreature}/{corpse?.OwnerWasSummoned}/{corpse?.Carved}/{corpse?.Animated}/{corpse?.SanctuaryHarvested}/{corpse?.SanctuaryConsumed}/{corpse?.SanctuaryRestInPeace}/{corpse?.IsBones}");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,string mode)
    {
        var difficulty=mode.StartsWith("nightmare-",StringComparison.Ordinal)?MonsterDifficulty.Nightmare:mode.StartsWith("hell-",StringComparison.Ordinal)?MonsterDifficulty.Hell:MonsterDifficulty.Normal;
        var p=Fresh();var corpse=Corpse(p,difficulty:difficulty);var target=Target(p);target.MoveToWorld(new(corpse.X+1,corpse.Y,corpse.Z),p.Map);
        var ability=mode.EndsWith("ce-right",StringComparison.Ordinal)?CombatAbility.CorpseExplosion:CombatAbility.PoisonExplosion;string label="native-corpse-"+mode;
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Native corpse bindings");var account=FixtureNetwork.Authenticate(network,p,"native-corpse-wire-");Ready(p);
        Require(SkillCombat.Targets(p,corpse.Location,1).Contains(target),$"Native corpse fixture recipient:hostile={ClassCombat.IsHostile(p,target)},enemy={target.IsEnemy(p)},see={p.CanSee(target)},harm={p.CanBeHarmful(target,false)},blessed={target.Blessed},returning={target.ReturningHome},centerLos={p.Map.LineOfSight(new Point3D(corpse.X,corpse.Y,corpse.Z+12),new Point3D(target.X,target.Y,target.Z+12))},positions={p.Location}/{corpse.Location}/{target.Location}");
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target),cost=SkillMechanics.Values(p,ability).Mana256;bool began=false,done=false;long deadline=Core.TickCount+12000;
        void Cleanup(){target.Delete();corpse.Delete();Clean(p);account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                var s=ClassCombat.For(p);
                Require(Core.TickCount<deadline,$"Native corpse automatic damage timeout:{mode},began={began},now={Core.TickCount},pending={s.PendingAbility},effect={s.EffectAt},recovery={s.RecoveryUntil},mana={NecromancerEffects.ManaBalance256(p)}/{mana-cost},corpse={corpse.Deleted}/{corpse.SanctuaryConsumed}/{CorpseSkillRules.NoSelect(corpse)}/{corpse.Location}/{corpse.Map},usable={CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)},target={target.Alive}/{target.Deleted}/{SkillHealth.Balance256(target)}/{hp}/{target.Location}/{target.Map},owner={p.Alive}/{p.Deleted}/{p.Location}/{p.Map}/{p.NetState!=null},row={JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse))}");
                if(!began&&s.PendingAbility==ability){Require(!CorpseSkillRules.NoSelect(corpse)&&NecromancerEffects.ManaBalance256(p)==mana,"Native corpse input paid/consumed during windup");began=true;Log.Information("{Label}: windup",label);}
                if(began&&CorpseSkillRules.NoSelect(corpse)&&SkillHealth.Balance256(target)<hp)
                {Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&corpse.Items.OfType<Gold>().Single().Amount==17,"Native corpse automatic damage/payment/loot mismatch");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,corpse.Serial.Value,corpse.X,corpse.Y,corpse.Z);Inspect();}));
    }
}
