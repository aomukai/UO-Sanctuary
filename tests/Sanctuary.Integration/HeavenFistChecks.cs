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

public static class HeavenFistChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(HeavenFistChecks));
    private sealed record Saved(uint Owner,uint Primary,uint Undead,uint Demon,uint Ally,uint Pet,HeavenStrike Strike,HolyBoltFlight Bolt,DateTime Cooldown,HeavenStrike CorpseStrike=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"heaven-fist-checks.json");
    public static void Configure(){Register("heaven-fist-check",Check);Register("heaven-fist-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fist of the Heavens primary, secondary and persistence checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string result,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",result);}});
    private static void Freeze(Mobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static T Place<T>(PlayerMobile owner,T target,int x,int y=0) where T:BaseCreature
    {
        target.NoKillAwards=target.Frozen=target.CantWalk=true;target.MoveToWorld(new(owner.X+x,owner.Y+y,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        target.SetHits(20000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Energy,0);Freeze(target);return target;
    }
    private static Point3D Space(PlayerMobile owner)
    {
        for(int x=2200;x<2300;x++)
        for(int y=1000;y<1100;y++)
        {
            int z=owner.Map.GetAverageZ(x,y);
            if(Enumerable.Range(0,15).All(i=>owner.Map.CanSpawnMobile(x+i,y,z)) && owner.Map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+14,y,z+12)))
            {
                bool clear=true;foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(new Point3D(x+7,y,z),24))
                {if(actor!=owner && !actor.Deleted){clear=false;break;}}
                if(clear){return new(x,y,z);}
            }
        }
        throw new InvalidOperationException("No FoH corridor");
    }
    private static SkillValues Golden(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.FistOfTheHeavens),
        key=>key=="Fist of the Heavens"?rank:40,key=>key=="Fist of the Heavens"?rank:hard,99);
    private static void Replace(HeavenStrike strike)=>HeavenFists.Restore(HeavenFists.Snapshot().Where(x=>x.Id!=strike.Id).Append(strike).ToArray());
    private static void Replace(HolyBoltFlight bolt)=>HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Id!=bolt.Id).Append(bolt).ToArray());
    private static void ClearBolts(PlayerMobile owner)=>HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=owner.Serial.Value).ToArray());
    private static void ClearInput(PlayerMobile owner)
    {
        ClassCombat.Stop(owner);var session=ClassCombat.For(owner);session.NextAction=session.RecoveryUntil=Core.TickCount;
        var ground=FireGround.Snapshot();FireGround.Restore(ground with{Cooldowns=ground.Cooldowns.Where(x=>x.Owner!=owner.Serial.Value || x.Ability!=CombatAbility.FistOfTheHeavens).ToArray()});
        HeavenFists.Restore(HeavenFists.Snapshot().Where(x=>x.Owner!=owner.Serial.Value).ToArray());ClearBolts(owner);
        owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());
    }
    private static void Automatic(PlayerMobile owner)
    {
        var origin=owner.Location;owner.MoveToWorld(new(origin.X+7,origin.Y,origin.Z),owner.Map);
        var near=Place(owner,new Ogre(),3);var far=Place(owner,new Ogre(),6);var behind=Place(owner,new Ogre(),-3);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.FistOfTheHeavens) && SkillBindings.Instance.Assign(owner,false,CombatAbility.FistOfTheHeavens)
            && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.FistOfTheHeavens),"FoH automatic input bindings");
        var cursor=new Point3D(owner.X+7,owner.Y,owner.Z);
        Require(HeavenFists.Acquire(owner,cursor)==far && HeavenFists.Acquire(owner,new(owner.X+2,owner.Y,owner.Z))==near,"FoH did not choose nearest cursor over nearest caster");
        behind.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);near.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        Require(HeavenFists.Acquire(owner,new(owner.X+1,owner.Y,owner.Z))==near,"FoH selected a closer enemy behind the click-facing half-circle");
        near.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);
        foreach(int input in new[]{0,1,2})
        {
            ClearInput(owner);owner.Direction=Direction.West;long now=Core.TickCount;int mana=NecromancerEffects.ManaBalance256(owner);
            Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.FistOfTheHeavens,null,cursor,now)
                :input==1?ClassCombat.ContinueCast(owner,CombatAbility.FistOfTheHeavens,null,cursor,now)
                :ClassCombat.Perform(owner,CombatAbility.FistOfTheHeavens,null,cursor,now),"FoH empty-ground input rejected");
            var session=ClassCombat.For(owner);var timing=CombatTiming.For(owner,CombatAbility.FistOfTheHeavens);
            Require(session.PendingAbility==CombatAbility.FistOfTheHeavens && session.PendingTarget==far.Serial && session.Timing==timing
                && session.EffectAt==now+timing.EffectMs && (owner.Direction&Direction.Mask)==Direction.East
                && NecromancerEffects.ManaBalance256(owner)==mana,"FoH acquisition added turn delay, changed timing or paid early");
            if(input!=2)
            {
                Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.FistOfTheHeavens,null,cursor,now+1)
                    :ClassCombat.ContinueCast(owner,CombatAbility.FistOfTheHeavens,null,cursor,now+1),"FoH held ground refresh");
                Require(session.PendingTarget==far.Serial && session.CastTarget==Serial.Zero,"Held ground erased acquired FoH primary");
            }
            Require(!ClassCombat.ResolveAction(owner,session.EffectAt-1) && HeavenFists.Snapshot().All(x=>x.Owner!=owner.Serial.Value),"Automatic FoH fired early");
            Require(ClassCombat.ResolveAction(owner,session.EffectAt) && mana-NecromancerEffects.ManaBalance256(owner)==6400
                && HeavenFists.Snapshot().Single(x=>x.Owner==owner.Serial.Value).Target==far.Serial.Value,"Automatic FoH lost primary or event payment");
        }
        ClearInput(owner);owner.Direction=Direction.East;
        Require(ClassCombat.Perform(owner,CombatAbility.FistOfTheHeavens,null,new(owner.X-2,owner.Y,owner.Z),Core.TickCount)
            && ClassCombat.For(owner).PendingTarget==behind.Serial && (owner.Direction&Direction.Mask)==Direction.West,"FoH used previous facing instead of turning toward the click");
        ClearInput(owner);far.MoveToWorld(near.Location,owner.Map);int before=NecromancerEffects.ManaBalance256(owner);
        Require(HeavenFists.Acquire(owner,cursor)==null && !ClassCombat.Perform(owner,CombatAbility.FistOfTheHeavens,null,cursor,Core.TickCount)
            && NecromancerEffects.ManaBalance256(owner)==before,"Unverified equal-distance tie invented priority or spent mana");
        Require(HeavenFists.Acquire(owner,owner.Location)==null && HeavenFists.Acquire(owner,new(owner.X+CombatProtocol.TeethRange+1,owner.Y,owner.Z))==null,"Unsupported own-tile/out-of-range ground click accepted");
        near.Delete();far.Delete();behind.Delete();ClearInput(owner);owner.MoveToWorld(origin,owner.Map);
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.MoveToWorld(Space(owner),owner.Map);
        Automatic(owner);
        var one=Golden(1);var high=Golden(20,20);
        Require(one.Damage256(false)==38400 && one.Damage256(true)==51200 && one.Mana256==6400 && one.Eval("calc4")==6
            && high.Damage256(false)==414720 && high.Damage256(true)==445440 && high.Eval("calc4")==25
            && HolyBolts.SecondaryDamage(owner,one)==(10240,12800) && HolyBolts.SecondaryDamage(owner,high)==(231424,241664),"Pinned primary/secondary tiers/hard synergies/count/cost");
        Require(NecromancerSummons.Source("fistoftheheavensbolt","Holy")==3 && NecromancerSummons.Source("fistoftheheavensbolt","NextHit")==0
            && NecromancerSummons.Source("fistoftheheavensbolt","NextDelay")==4 && NecromancerSummons.Source("fistoftheheavensbolt","Activate")==2
            && SkillMechanics.For(CombatAbility.FistOfTheHeavens).Number("TargetAlly")==0,"Distinct secondary holy/activation/NHD/heal eligibility");
        var primary=Place(owner,new Ogre(),6);var undead=new LichLord();MonsterVariants.Assign(undead,MonsterDifficulty.Hell);Place(owner,undead,10);
        var demon=new DemonKnight();MonsterVariants.Assign(demon,MonsterDifficulty.Hell);Place(owner,demon,12);var beast=Place(owner,new Ogre(),14);
        Require(HolyBolts.FullDamage(undead) && NamedElements.MagicResistance(undead)==100 && NamedCombat.Demon(demon) && NamedElements.MagicResistance(demon)==25,"Real magic-resistant undead/demon");
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);Freeze(ally);ally.Hits-=30;
        var pet=Place(owner,new GraveyardSkeleton(),8,2);Require(pet.SetControlMaster(ally),"FoH ally pet");pet.Hits-=30;
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.FistOfTheHeavens) && SkillBindings.Instance.Assign(owner,false,CombatAbility.FistOfTheHeavens)
            && PaladinAuras.Selected(owner)==CombatAbility.None,"FoH slots or aura classification");
        Require(PaladinCombat.ValidTarget(owner,CombatAbility.FistOfTheHeavens,Serial.Zero,primary.Location)
            && !PaladinCombat.ValidTarget(owner,CombatAbility.FistOfTheHeavens,ally.Serial,ally.Location),"Unknown automatic acquisition or allied primary silently substituted");
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());Require(!ClassCombat.Perform(owner,CombatAbility.FistOfTheHeavens,primary,primary.Location,Core.TickCount),"Mana failure fell back to an attack");
        owner.Mana=owner.ManaMax;var action=ClassCombat.For(owner);action.NextAction=Core.TickCount;int mana=NecromancerEffects.ManaBalance256(owner);
        Require(ClassCombat.Perform(owner,CombatAbility.FistOfTheHeavens,primary,primary.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==mana,"Pre-event debit");
        Require(ClassCombat.ResolveAction(owner,action.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana-6400,"Primary release/cost");
        Require(!FireGround.Ready(owner,CombatAbility.FistOfTheHeavens) && FireGround.Ready(owner,CombatAbility.HolyBolt)
            && FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==owner.Serial.Value && x.Ability==CombatAbility.FistOfTheHeavens).UntilUtc<TimedEffects.Now.AddMilliseconds(450),"Independent ten-frame local cooldown");
        var strike=HeavenFists.Snapshot().Single(x=>x.Owner==owner.Serial.Value);
        Require(strike.X==owner.X+6 && strike.Min256==41088 && strike.Max256==54784 && strike.ImpactUtc>TimedEffects.Now.AddMilliseconds(300),"Held primary rank/damage/origin/delay");
        primary.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.holyshock",out _) && ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.holybolt",out _)
            && ClassCreation.Profiles.TryTrain(owner.Serial,"paladin.fistoftheheavens",out _),"Later rank/synergy fixture");
        int hp=primary.Hits,uh=undead.Hits,dh=demon.Hits,bh=beast.Hits,ah=ally.Hits,ph=pet.Hits;
        var parentRoll=HeavenFists.Roll;var boltRoll=HolyBolts.Roll;HeavenFists.Roll=HolyBolts.Roll=_=>0;
        Later(250,"heaven-fist-result",()=>Require(primary.Hits==hp && !HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Strike/children before source delay"));
        Later(450,"heaven-fist-result",()=>
        {
            Require(primary.Hits==hp-160 && SkillHealth.Remainder256(primary)==128,"Primary moving target, held launch damage, lightning immunity or fraction");
            var bolts=HolyBolts.Snapshot().Where(x=>x.Owner==owner.Serial.Value).ToArray();
            Require(bolts.Length==4 && bolts.All(x=>x.Ability==CombatAbility.FistOfTheHeavens && x.Rank==1 && x.Min256==13312 && x.Max256==16640)
                && bolts.Count(x=>x.Vx>0)==3 && bolts.Count(x=>x.Vx<0)==1,"Fresh secondary synergy, original rank/origin, enemy-only search, cap or velocity");
            var flight=bolts[0];HolyBolts.Impact(owner,pet,flight);
            Require(pet.Hits==ph,"Missile heal flag bypassed parent TargetPet=0");
        });
        Later(2700,"heaven-fist-result",()=>
        {
            try
            {
                Require(undead.Hits==uh-156 && demon.Hits==dh-156 && beast.Hits==bh && primary.Hits==hp-160
                    && ally.Hits==ah && pet.Hits==ph,$"Concurrent children or holy bypass/no healing/beast rules: UD{uh-undead.Hits}, D{dh-demon.Hits}, beast{bh-beast.Hits}, primary{hp-primary.Hits}, ally{ally.Hits-ah}, pet{pet.Hits-ph}");
                Require(!HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value) && !HeavenFists.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Source flight/strike expiry");
                Cap(owner,primary);Lifecycle(owner,primary,ally,dead=>
                {Seed(owner,primary,undead,demon,ally,pet,dead);_log.Information("heaven-fist-result: passed");});
            }
            finally{HeavenFists.Roll=parentRoll;HolyBolts.Roll=boltRoll;}
        });
    }
    private static void Lifecycle(PlayerMobile owner,BaseCreature primary,PlayerMobile ally,Action<HeavenStrike> finish)
    {
        HeavenFists.Cast(owner,primary,SkillMechanics.Values(owner,CombatAbility.FistOfTheHeavens,1));
        var converted=HeavenFists.Snapshot().Single(x=>x.Owner==owner.Serial.Value);
        HeavenFists.Restore(HeavenFists.Snapshot().Where(x=>x.Id!=converted.Id).ToArray());
        int hp=primary.Hits;Require(primary.SetControlMaster(ally),"Converted primary fixture");
        try
        {
            HeavenFists.Impact(owner,converted);
            Require(primary.Hits==hp && HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Converted primary was harmed or canceled burst");
        }
        finally{primary.SetControlMaster(null);ClearBolts(owner);}
        var victim=Place(owner,new GraveyardSkeleton(),6,3);Corpse corpse=null;
        HeavenFists.Cast(owner,victim,SkillMechanics.Values(owner,CombatAbility.FistOfTheHeavens,1));
        var previous=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(actor,body)=>{previous?.Invoke(actor,body);if(actor==victim){corpse=body as Corpse;corpse?.DropItem(new Gold(7));}};
        try{owner.DoHarmful(victim,true);victim.Damage(victim.Hits+1,owner);CombatDamage.Finish(victim,owner);}
        finally{ClassLootHooks.CreatureDied=previous;}
        var dead=HeavenFists.Snapshot().Single(x=>x.Target==victim.Serial.Value);
        Require(victim.Deleted && corpse is{Deleted:false} && ClassLoot.HasContents(corpse) && dead.Corpse==corpse.Serial.Value,"Deleted target corpse identity/loot capture");
        Replace(dead with{ImpactUtc=TimedEffects.Now.AddMilliseconds(40)});
        Later(120,"heaven-fist-result",()=>
        {
            Require(!HeavenFists.Snapshot().Any(x=>x.Id==dead.Id) && HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value)
                && !corpse.Deleted && ClassLoot.HasContents(corpse),"Dead primary canceled burst or consumed corpse/loot");
            ClearBolts(owner);finish(dead);
        });
    }
    private static void Cap(PlayerMobile owner,BaseCreature primary)
    {
        BaseCreature[] extras=Enumerable.Range(0,7).Select(i=>(BaseCreature)Place(owner,new GraveyardSkeleton(),4+i,3)).ToArray();
        try
        {
            HeavenFists.Impact(owner,new(999,owner.Serial.Value,primary.Serial.Value,owner.Map.MapID,1,owner.X+6,owner.Y,owner.Z,0,0,TimedEffects.Now));
            Require(HolyBolts.Snapshot().Count(x=>x.Owner==owner.Serial.Value)==6,"Original-rank calc4 cap did not bound source callback count");
        }
        finally{ClearBolts(owner);foreach(var extra in extras){extra.Delete();}}
    }
    private static void Seed(PlayerMobile owner,BaseCreature primary,BaseCreature undead,BaseCreature demon,PlayerMobile ally,BaseCreature pet,HeavenStrike dead)
    {
        // Snapshot inspection hold only; ordinary duration/pulse checks remain separate.
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);HeavenFists.Cast(owner,primary,SkillMechanics.Values(owner,CombatAbility.FistOfTheHeavens,1));
        var strike=HeavenFists.Snapshot().Single(x=>x.Owner==owner.Serial.Value) with{ImpactUtc=at,Min256=2560,Max256=2560};Replace(strike);
        dead=dead with{ImpactUtc=at.AddSeconds(1)};Replace(dead);
        // Keep the real corpse for serialization inspection beyond the full
        // three-boot suite; production NPC corpse decay remains seven minutes.
        Require(World.FindItem((Serial)dead.Corpse) is Corpse{Deleted:false},"Missing corpse before inspection hold");
        ((Corpse)World.FindItem((Serial)dead.Corpse)).BeginDecay(TimeSpan.FromHours(1));
        HolyBolts.CastSecondary(owner,new(owner.X+11,owner.Y,owner.Z),demon.Location,SkillMechanics.Values(owner,CombatAbility.FistOfTheHeavens,1),at);
        var bolt=HolyBolts.Snapshot().Single(x=>x.Owner==owner.Serial.Value) with{Frame=2,Min256=2560,Max256=2560,LastCollision=undead.Serial.Value};Replace(bolt);
        var state=FireGround.Snapshot();FireGround.Restore(state with{Cooldowns=state.Cooldowns.Where(x=>x.Owner!=owner.Serial.Value || x.Ability!=CombatAbility.FistOfTheHeavens).Append(new(owner.Serial.Value,CombatAbility.FistOfTheHeavens,at)).ToArray()});
        var legacy=JsonSerializer.SerializeToNode(bolt with{Ability=CombatAbility.HolyBolt}).AsObject();legacy.Remove("Ability");
        Require(legacy.Deserialize<HolyBoltFlight>().Ability==CombatAbility.HolyBolt,"Previous holy-bolt JSON default changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,primary.Serial.Value,undead.Serial.Value,demon.Serial.Value,ally.Serial.Value,pet.Serial.Value,strike,bolt,at,dead)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var strike=HeavenFists.Snapshot().Single(x=>x.Id==saved.Strike.Id);var bolt=HolyBolts.Snapshot().Single(x=>x.Id==saved.Bolt.Id);
        Require(JsonSerializer.Serialize(strike)==JsonSerializer.Serialize(saved.Strike),"Saved primary strike changed");
        Require(JsonSerializer.Serialize(bolt)==JsonSerializer.Serialize(saved.Bolt),"Saved secondary bolt changed");
        Require(JsonSerializer.Serialize(HeavenFists.Snapshot().Single(x=>x.Id==saved.CorpseStrike.Id))==JsonSerializer.Serialize(saved.CorpseStrike),"Saved corpse strike changed");
        Require(World.FindItem((Serial)saved.CorpseStrike.Corpse) is Corpse{Deleted:false},"Saved inspection corpse expired or disappeared");
        Require(FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==saved.Owner && x.Ability==CombatAbility.FistOfTheHeavens).UntilUtc==saved.Cooldown,"Saved independent local cooldown changed");
        owner.MoveToWorld(new(strike.X-5,strike.Y,strike.Z),Map.Maps[strike.Map]);
        var primary=(BaseCreature)World.FindMobile((Serial)saved.Primary);primary.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);primary.SetResistance(ResistanceType.Energy,0);Freeze(primary);
        var demon=(BaseCreature)World.FindMobile((Serial)saved.Demon);demon.MoveToWorld(new(owner.X+12,owner.Y,owner.Z),owner.Map);Freeze(demon);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);ally.ClaimAutoStabledPets();
        var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);Require(pet.Controlled && pet.ControlMaster==ally,"FoH native pet claim");
        int hp=primary.Hits,dh=demon.Hits;var parentRoll=HeavenFists.Roll;var boltRoll=HolyBolts.Roll;HeavenFists.Roll=HolyBolts.Roll=_=>0;
        Replace(strike with{ImpactUtc=TimedEffects.Now.AddMilliseconds(40)});
        Replace(saved.CorpseStrike with{ImpactUtc=TimedEffects.Now.AddMilliseconds(40)});
        Later(120,"heaven-fist-reload-result",()=>
        {
            Require(primary.Hits==hp-10 && HolyBolts.Snapshot().Any(x=>x.Owner==saved.Owner && x.Id!=saved.Bolt.Id && x.Min256==13312),"Restored primary or fresh secondary source values");
            ClearBolts(owner);Replace(bolt with{NextUtc=TimedEffects.Now,UntilUtc=TimedEffects.Now.AddSeconds(2)});
            Later(120,"heaven-fist-reload-result",()=>
            {
                try
                {
                    Require(demon.Hits==dh-10 && !FireGround.Ready(owner,CombatAbility.FistOfTheHeavens) && FireGround.Ready(owner,CombatAbility.HolyBolt),"Restored child demon bypass or independent local cooldown");
                    ClearBolts(owner);Replace(saved.Bolt);Replace(saved.Strike);Replace(saved.CorpseStrike);_log.Information("heaven-fist-reload-result: passed");
                }
                finally{HeavenFists.Roll=parentRoll;HolyBolts.Roll=boltRoll;}
            });
        });
    }
}
