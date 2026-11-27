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

public static class NativeLichReviveChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NativeLichReviveChecks));
    private sealed record Saved(int Generation,uint Owner,uint Actor,uint Corpse,CorpseSkillRecord Inactive,int Life256,ReviveNativeState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"native-lich-revive-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string label=reload?"native-lich-revive-reload-check":"native-lich-revive-check";
            ConsoleInputHandler.RegisterCommand(label,"Prepared native original Lich Revive world",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Label}: passed",label);}catch(Exception e){Log.Error(e,"{Label}: failed",label);}}));
        }
        ConsoleInputHandler.RegisterCommand("native-lich-revive-live-check","Original Lich offense, support restrictions and facet projection",_=>Core.LoopContext.Post(()=>
        {FixtureClock.NextLoop("native-lich-revive-live-result",()=>FixtureNetwork.Connected("native-lich-revive-live-result",Live));Log.Information("native-lich-revive-live-check: passed");}));
    }
    private static GraveyardLich Release(PlayerMobile p,Corpse corpse)
    {
        var original=ReviveOriginals.Find(corpse)as GraveyardLich;Require(original is{Alive:false,Deleted:false}&&original.Map==Map.Internal,"Native original was not retained");
        var unit=SourceCorpseIdentity.For(corpse);Require(unit.Snapshot.Source=="sanctuary.native.graveyard-lich.v1"&&unit.ClassId==GenericPetSearchTables.SourceId("unraveler1")
            &&GenericPetSearchTables.SourceId(unit.Snapshot.Source)==null,"Native source identity was replaced by its proposed AI analogue");
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Revive).Mana256;
        Require(ReviveCasts.Plan(p,corpse,out _,out _,out _)==NativeReviveRules.Approved&&NecromancerEffects.ManaBalance256(p)==mana,"Native admission does not match its approval gate");
        Require(ReviveCasts.Plan(p,corpse,out var setup,out _,out string issue,true),"Prepared native plan:"+issue);
        Require(setup.RegenerationRate==0&&setup.LifePlan.Source==unit.Snapshot.Source,"Native resources invented a D2 regeneration rate or source");
        Require(ReviveCasts.Release(p,corpse,true)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Prepared native release/payment");
        Require(original.Alive&&original.ControlMaster==p&&ReviveNativeActivation.State(original)!=null&&!original.Summoned&&original.ControlSlots==0
            &&CorpseSkillRules.Profiles.Find(corpse)is{UnitReactivated:true,NoSelect:true}&&corpse.Items.OfType<Gold>().Single().Amount==17,"Native activation cloned/lost original identity,ownership or loot");
        return original;
    }
    private static void Check()
    {
        var p=NativeCorpseChecks.Fresh();var corpse=NativeCorpseChecks.Corpse(p);var actor=Release(p,corpse);
        int life=SkillHealth.Maximum256(actor)-257;SkillHealth.SetBalance256(actor,life);
        ReviveRegeneration.Recover(actor,true,Core.TickCount);ReviveRegeneration.Recover(actor,true,Core.TickCount+10000);
        Require(SkillHealth.Balance256(actor)==life&&actor.Map==Map.Internal,"Native no-regen/offline parking reset wounds");
        NativeCorpseChecks.KeepForReload(corpse);
        var inactive=NativeCorpseChecks.KeepForReload(NativeCorpseChecks.Corpse(p,7));var row=CorpseSkillRules.Profiles.Find(inactive);
        Require(ReviveOriginals.Find(inactive) is GraveyardLich{Alive:false,Deleted:false},"Saved dead native original missing");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,actor.Serial.Value,corpse.Serial.Value,row,life,ReviveNativeActivation.State(actor))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var actor=World.FindMobile((Serial)saved.Actor)as GraveyardLich;
        var corpse=World.FindItem((Serial)saved.Corpse)as Corpse;var inactive=World.FindItem((Serial)saved.Inactive.Corpse)as Corpse;
        Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&actor.ControlMaster==p&&SkillHealth.Balance256(actor)==saved.Life256
            &&ReviveNativeActivation.State(actor)==saved.State&&PersistentMinions.Profiles.Find(actor).ExpiresUtc==null,"Saved active native original/owner/wounds/setup changed");
        Require(corpse is{SanctuaryConsumed:true}&&corpse.Items.OfType<Gold>().Single().Amount==17&&CorpseSkillRules.Profiles.Find(corpse).UnitReactivated
            &&CorpseSkillRules.Profiles.Find(inactive)==saved.Inactive&&ReviveOriginals.Find(inactive)is GraveyardLich{Alive:false,Deleted:false},"Saved active loot or inactive native original changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static Corpse RaiseBody(PlayerMobile p)
    {
        var victim=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};victim.MoveToWorld(new(p.X+7,p.Y,p.Z),p.Map);victim.AIObject.AITimer.Stop();
        SkillCombat.HitParts(p,victim,new(100000,0,0,0,0),recover:false);var corpse=(Corpse)victim.Corpse;corpse.DropItem(new Gold(17));return corpse;
    }
    private static void Live(NetState network)
    {
        var p=NativeCorpseChecks.Fresh();var account=FixtureNetwork.Authenticate(network,p,"native-lich-revive-wire-");var corpse=NativeCorpseChecks.Corpse(p);var actor=Release(p,corpse);actor.AIObject.AITimer.Stop();
        var target=NativeCorpseChecks.Target(p);target.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);var body=RaiseBody(p);int hp=SkillHealth.Balance256(target),ownerLife=SkillHealth.Balance256(p);
        Require(ReviveNativeActivation.CanAct(actor),"Revived Lich is not active for support checks");
        Require(!actor.CanRaise(body)&&!actor.BeginRaise(body),"Revived Lich can raise another monster");
        Require(!new Server.Spells.First.HealSpell(actor).Cast()&&!new Server.Spells.Eighth.ResurrectionSpell(actor).Cast()
            &&!new Server.Spells.Eighth.SummonDaemonSpell(actor).Cast(),"Revived Lich can cast a native support spell");
        var summon=new Skeleton();
        Require(!BaseCreature.Summon(summon,actor,actor.Location,0,TimeSpan.FromSeconds(10))&&summon.Deleted,"Revived Lich can spawn a helper");
        long deadline=Core.TickCount+15000;bool done=false,bolt=false;
        void Cleanup(){target.Delete();body.Delete();corpse.Delete();NativeCorpseChecks.Clean(p);account.Delete();}
        void Inspect()
        {
            try
            {
                if(done){return;}Require(Core.TickCount<deadline,$"Native Lich live timeout:bolt={bolt},actor={actor.Location}/{actor.Map}/{actor.Alive}/{ReviveNativeActivation.CanAct(actor)},body={body.SanctuaryConsumed},rows={RaisedMinions.Snapshot().Length}");
                actor.AIObject.AITimer.Stop();actor.Combatant=target;actor.OnThink();bolt|=SkillHealth.Balance256(target)<hp;
                var child=RaisedMinions.Snapshot().Where(row=>row.Caster==actor.Serial.Value).Select(row=>World.FindMobile((Serial)row.Actor)).OfType<GraveyardUndead>().SingleOrDefault();
                Require(child==null&&!body.SanctuaryConsumed&&body.Items.OfType<Gold>().Single().Amount==17,"Revived Lich created a helper or consumed its corpse");
                if(bolt)
                {
                    Require(ClassCombat.Friendly(actor,p)&&!actor.CanBeHarmful(p,false)
                        &&SkillHealth.Balance256(p)==ownerLife,"Original caster ability lost allegiance or damaged its owner");
                    var facets=SummonDifficulties.Facets();var home=p.Location;var homeMap=p.Map;
                    int floor=Enumerable.Range(0x500,0x100).First(id=>TileData.ItemTable[id].Surface&&!TileData.ItemTable[id].Impassable&&TileData.ItemTable[id].CalcHeight==0);
                    for(int dx=-4;dx<=24;dx++)for(int dy=-4;dy<=4;dy++){new Static(floor).MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),Map.Felucca);}
                    try
                    {
                        foreach(var difficulty in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
                        {
                            int maximum=SkillHealth.Maximum256(actor),life=maximum-257;SkillHealth.SetBalance256(actor,life);
                            var nextMap=difficulty==MonsterDifficulty.Nightmare?Map.Felucca:Map.Trammel;
                            SummonDifficulties.SetFacets([new(Map.Felucca.MapID,MonsterDifficulty.Nightmare),new(Map.Trammel.MapID,MonsterDifficulty.Hell)]);
                            p.MoveToWorld(new(p.X+2,p.Y,p.Z),nextMap);
                            Require(ReviveFacets.Issue(actor)==null&&MonsterVariants.Difficulty(actor)==difficulty
                                &&actor.Map==nextMap
                                &&SkillHealth.Balance256(actor)==SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(actor)),$"Native facet:expected={difficulty}/{nextMap},owner={p.Map},actor={actor.Map}/{MonsterVariants.Difficulty(actor)},issue={ReviveFacets.Issue(actor)},life={SkillHealth.Balance256(actor)}/{SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(actor))},max={SkillHealth.Maximum256(actor)}/{maximum}");
                        }
                    }
                    finally{SummonDifficulties.SetFacets(facets);p.MoveToWorld(home,homeMap);SummonDifficulties.Sync(p);}
                    // Forced blind qualifies the existing native punch fallback,
                    // avoiding a fabricated D2 animation for this UO host.
                    actor.AIObject.AITimer.Stop();actor.StopCombatActions();target.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);
                    var old=NecromancerEffects.Profiles.Find(actor)??new();NecromancerEffects.Profiles.Store(actor,old with{Curse=new(CombatAbility.DimVision,p.Serial.Value,1,TimedEffects.Now.AddSeconds(10),actor.X,actor.Y)});
                    var hit=ClassCombatHooks.ClassHitCheck;
                    try
                    {
                        Require(NamedControl.Blind(actor)&&ClassCombatHooks.CreatureSwing(actor,target)==null,"Native blind did not choose original weapon swing");
                        ClassCombatHooks.ClassHitCheck=(a,b)=>a==actor?true:hit?.Invoke(a,b);int before=SkillHealth.Balance256(target);
                        ((BaseWeapon)actor.Weapon).OnSwing(actor,target);Require(SkillHealth.Balance256(target)<before,"Native blind punch did not deal damage");
                    }
                    finally{ClassCombatHooks.ClassHitCheck=hit;NecromancerEffects.Profiles.Store(actor,old);}
                    int wounded=SkillHealth.Maximum256(actor)-257;SkillHealth.SetBalance256(actor,wounded);PersistentMinions.Suspend(p);Require(actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==wounded,"Parking healed or left actor active");
                    PersistentMinions.Resume(p);Require(actor.Map==p.Map&&SkillHealth.Balance256(actor)==wounded,"Resuming reset wounds");
                    actor.Delete();done=true;Cleanup();Log.Information("native-lich-revive-live-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"native-lich-revive-live-result: failed");}
        }
        Inspect();
    }
}
