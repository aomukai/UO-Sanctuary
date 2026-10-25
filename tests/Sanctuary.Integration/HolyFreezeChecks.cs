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

public static class HolyFreezeChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(HolyFreezeChecks));
    private sealed record Saved(uint Owner,uint Target,uint Corpse,Point3D Origin,HolyFreezeLease Lease,PaladinAuraSave Aura);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-freeze-checks.json");
    public static void Configure(){Register("holy-freeze-check",Check);Register("holy-freeze-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Freeze independent slow, cold weapon/pulse and retained-loot shatter checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
        {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Values(int rank,int hard=0)=>new(SkillMechanics.For(CombatAbility.HolyFreeze),
        k=>k=="Holy Freeze"?rank:40,k=>k=="Holy Freeze"?rank:hard,99);
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx,int dy=0,MonsterDifficulty difficulty=MonsterDifficulty.Normal)
    {
        var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};
        if(difficulty!=MonsterDifficulty.Normal){MonsterVariants.Assign(target,difficulty);}
        target.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);target.AIObject.AITimer.Stop();
        target.SetHits(20000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Physical,0);target.SetResistance(ResistanceType.Cold,0);
        target.Poison=Poison.Lesser; // Prevent unrelated native life regeneration during measurements.
        return target;
    }
    private static Point3D Place(PlayerMobile owner)
    {
        for(int x=2700;x<=3100;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D p;try{p=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(p,20)){if(actor!=owner){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(p,owner)){return p;}
        }
        throw new InvalidOperationException("No independent Holy Freeze corridor");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.RawStr=owner.RawDex=1000;
        owner.MoveToWorld(Place(owner),owner.Map);
        var one=Values(1);var twenty=Values(20,20);
        Require(one.Mana256==0 && one.Damage256(false)==512 && one.Damage256(true)==768
            && PaladinElements.WeaponRange(owner,CombatAbility.HolyFreeze,one)==(10,15)
            && one.Eval("aurastatcalc1")==-30 && twenty.Eval("aurastatcalc1")==-54,"Pinned rank1/20 Holy Freeze endpoints/slow");
        Require(twenty.Eval("EDmgSymPerCalc")==440 && twenty.Damage256(false)==51148 && twenty.Damage256(true)==52531
            && PaladinElements.WeaponRange(owner,CombatAbility.HolyFreeze,twenty)==(998,1025),"Hard synergies/fixed-point weapon rounding");
        var target=Enemy(owner,1);var immune=Enemy(owner,-1);immune.SetResistance(ResistanceType.Cold,100);
        var far=Enemy(owner,5);var ally=ClassSkillChecks.Player(DiabloClass.Necromancer);ally.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        var unknown=new Ogre{Frozen=true};unknown.MoveToWorld(new(owner.X,owner.Y-1,owner.Z),owner.Map);unknown.AIObject.AITimer.Stop();int unknownHp=unknown.Hits;
        int hp=target.Hits,ihp=immune.Hits,fhp=far.Hits;owner.Mana=0;
        var roll=PaladinElements.Roll;var shatter=PaladinHolyFreeze.RollPercent;PaladinElements.Roll=_=>0;PaladinHolyFreeze.RollPercent=()=>19;
        Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.HolyFreeze),"Right-slot Holy Freeze assignment");
        Require(PaladinHolyFreeze.Effect(target)==null && target.Hits==hp && !PaladinElements.WeaponActive(owner),"Holy Freeze incorrectly immediate");
        Later(2200,"holy-freeze-result",()=>
        {
            try
            {
                Require(target.Hits<hp && immune.Hits==ihp && PaladinHolyFreeze.VelocitySlow(immune)==30
                    && GearEffects.ChillPercent(immune)==0,"Cold immunity suppressed aura slow or ordinary chill substituted");
                Require(far.Hits==fhp && PaladinHolyFreeze.Effect(far)==null && PaladinHolyFreeze.Effect(ally)==null
                    && PaladinHolyFreeze.Effect(unknown)==null && unknown.Hits==unknownHp,"Range/friendly/unknown-source gates");
                Require(PaladinAuras.Effect(owner,CombatAbility.HolyFreeze) is{WeaponMin:12,WeaponMax:18},"Cold weapon packet endpoints");
                Require(CreatureHitRecovery.Remaining(target)==0 && PaladinHolyFreeze.Shattered(target),"Soft hit or20percent marker boundary");
                owner.Mana=0;PaladinElements.Pulse(owner,CombatAbility.HolyFreeze,SkillMechanics.Values(owner,CombatAbility.HolyFreeze),TimedEffects.Now);
                Require(owner.Mana==0 && PaladinElements.WeaponActive(owner),"Zero-mana pulse disabled or charged");
                Require(PaladinHolyFreeze.AttackSlow(target)==30 && PaladinHolyFreeze.OtherSlow(target)==30
                    && GearEffects.AttackSlow(target)==30 && GearEffects.MovementSlow(target)==30
                    && ClassGearHooks.MovementDelay(target,1)>1 && PaladinOffense.CreatureCycle(target,1000)>1000,"Native movement/attack consumers");
                PaladinHolyFreeze.RollPercent=()=>20;PaladinHolyFreeze.RollShatter(target);Require(!PaladinHolyFreeze.Shattered(target),"Shatter20percent exclusive boundary");
                foreach(var difficulty in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
                {
                    var variant=Enemy(owner,7,0,difficulty);PaladinHolyFreeze.Slow(owner,variant,twenty,TimedEffects.Now);
                    int expected=difficulty==MonsterDifficulty.Normal?50:difficulty==MonsterDifficulty.Nightmare?40:25;
                    Require(PaladinHolyFreeze.AttackSlow(variant)==expected,"Difficulty ColdEffect cap");variant.Delete();
                }
                target.Hits=target.HitsMax;SkillHealth.Clear(target);target.SetResistance(ResistanceType.Physical,100);
                Require(GearEffects.Strike(owner,target,.5,new Random(6),false)==15,"Class cold weapon addition");
                target.Hits=target.HitsMax;SkillHealth.Clear(target);int before=target.Hits;
                AOS.Damage(target,owner,100,false,100,0,0,0,0,weaponDamage:true);
                Require(target.Hits<before && target.Hits>=before-18,"Native typed cold weapon addition");
                var victim=Enemy(owner,2,2);
                PaladinHolyFreeze.Slow(owner,victim,one,TimedEffects.Now);PaladinHolyFreeze.RollPercent=()=>0;PaladinHolyFreeze.RollShatter(victim);
                AOS.Damage(victim,owner,100000,false,100,0,0,0,0,weaponDamage:false);
                var corpse=victim.Corpse as Corpse;Require(corpse!=null,"Shatter victim left no corpse; life="+victim.Hits);
                // Keep this saved-state inspection fixture beyond the growing
                // three-boot suite; ordinary corpse decay remains unchanged.
                corpse.BeginDecay(TimeSpan.FromHours(1));
                Require(CorpseSkillRules.Shattered(corpse),"Shatter marker absent from captured corpse: "+JsonSerializer.Serialize(CorpseSkillRules.Profiles.Find(corpse)));
                // Ordinary reward cleanup intentionally removes native backpack drops. Seed retained loot after that existing policy.
                var gold=new Gold(777);corpse.DropItem(gold);
                Require(gold.IsChildOf(corpse) && !corpse.SanctuaryConsumed && !corpse.Animated,"Shatter consumed loot or changed ordinary corpse flags; gold="+gold.Parent);
                Require(!CorpseSkillRules.Usable(owner,corpse,20) && !SkeletonPets.Usable(owner,corpse),"Shatter corpse remained usable");
                PaladinHolyFreeze.RollShatter(target);var original=PaladinHolyFreeze.Effect(target);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Might),"Holy Freeze replacement");
                Require(PaladinHolyFreeze.Effect(target)==original && PaladinAuras.Effect(owner,CombatAbility.HolyFreeze)==null,"Replacement removed lingering target lease or retained weapon stats");
                Later(2200,"holy-freeze-result",()=>
                {
                    Require(PaladinHolyFreeze.Effect(target)==null && GearEffects.MovementSlow(target)==0 && PaladinHolyFreeze.Shattered(target),"Natural51-frame slow expiry must not remove independent target SHATTER");
                    Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.HolyFreeze),"Reselect Holy Freeze");
                    PaladinElements.Pulse(owner,CombatAbility.HolyFreeze,one,TimedEffects.Now);Seed(owner,target,corpse);_log.Information("holy-freeze-result: passed");
                });
            }
            finally{PaladinElements.Roll=roll;PaladinHolyFreeze.RollPercent=shatter;}
        });
    }
    private static void Seed(PlayerMobile owner,Mobile target,Corpse corpse)
    {
        var deadline=TimedEffects.Now.Add(FixtureSpace.Retention);var lease=PaladinHolyFreeze.Effect(target) with{UntilUtc=deadline,Shatter=true};
        PaladinHolyFreeze.Restore(PaladinHolyFreeze.Snapshot().Select(x=>x.Target==target.Serial.Value?lease:x).ToArray());
        var aura=PaladinAuras.Snapshot();aura=aura with{Emitters=aura.Emitters.Select(x=>x.Owner==owner.Serial.Value?x with{NextUtc=deadline}:x).ToArray(),
            Leases=aura.Leases.Select(x=>x.Source==owner.Serial.Value?x with{UntilUtc=deadline}:x).ToArray()};PaladinAuras.Restore(aura);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,corpse.Serial.Value,owner.Location,lease,aura)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);owner.MoveToWorld(saved.Origin,Map.Trammel);target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(PaladinHolyFreeze.Effect(target)==saved.Lease && PaladinHolyFreeze.Shattered(target),"Saved independent slow/source/rank/shatter/deadline changed");
        var corpse=World.FindItem((Serial)saved.Corpse) as Corpse;Require(CorpseSkillRules.Shattered(corpse) && corpse.Items.Count>0
            && !CorpseSkillRules.Usable(owner,corpse,20) && !SkeletonPets.Usable(owner,corpse),$"Saved shatter corpse/loot changed: corpse={corpse?.Serial},deleted={corpse?.Deleted},items={corpse?.Items.Count},shattered={CorpseSkillRules.Shattered(corpse)}");
        target.SetResistance(ResistanceType.Cold,0);int hp=target.Hits;
        var state=PaladinAuras.Snapshot();PaladinAuras.Restore(state with{Emitters=state.Emitters.Select(x=>x.Owner==saved.Owner?x with{NextUtc=TimedEffects.Now}:x).ToArray()});
        Later(150,"holy-freeze-reload-result",()=>
        {
            Require(target.Hits<hp && PaladinHolyFreeze.Effect(target).UntilUtc<TimedEffects.Now.AddSeconds(3)
                && PaladinAuras.Effect(owner,CombatAbility.HolyFreeze)!=null,"Restored cold pulse/slow failed to resume");
            PaladinHolyFreeze.Restore(PaladinHolyFreeze.Snapshot().Select(x=>x.Target==saved.Target?saved.Lease:x).ToArray());PaladinAuras.Restore(state);
            _log.Information("holy-freeze-reload-result: passed");
        });
    }
}
