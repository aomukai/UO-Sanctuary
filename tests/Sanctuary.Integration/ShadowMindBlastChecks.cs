// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowMindBlastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowMindBlastChecks));
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life,uint Target,ConversionAllegiance Allegiance,int TargetLife);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-mindblast-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-mindblast-check",false);Register("shadow-mindblast-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Mind Blast area/SC,allegiance,stun and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static BaseCreature Target(AssassinShadowBody b,bool hell=false)
    {
        BaseCreature t=hell?new GraveyardZombie():new ShadowPsychicChecks.Witness{CasterId=b.Serial.Value};
        if(hell){MonsterVariants.Assign(t,MonsterDifficulty.Hell);}
        t.CantWalk=t.Frozen=true;t.NoKillAwards=true;t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
        t.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static (ulong Start,ulong End,GearDamage Raw,int Frames) Seed(SkillValues v,bool convert)
    {
        for(int low=1;low<10000;low++)
        {
            ulong seed=SourceRandom.InitLow(low),copy=seed;var raw=MindBlasts.Damage(v,n=>(int)SourceRandom.Limited(ref copy,n));
            int roll=(int)SourceRandom.Limited(ref copy,100);
            // Exercise the inclusive boundary itself,not only a zero roll.
            if(roll!=(convert?MindBlasts.Chance(v):MindBlasts.Chance(v)+1)){continue;}
            int frames=convert?v.Param(3)+(int)SourceRandom.Limited(ref copy,v.Param(4)):0;return(seed,copy,raw,frames);
        }
        throw new InvalidOperationException("Missing own conversion boundary seed");
    }
    private static void Protection(AssassinShadowBody b,PlayerMobile owner,BaseCreature t)
    {
        int hp=SkillHealth.Balance256(t);
        Require(PaladinConversion.Active(t)&&ClassCombat.Friendly(b,t)&&!ClassCombat.IsHostile(owner,t)&&PaladinConversion.DamageMaster(t,b)==owner,"Own NPC allegiance/owner credit lost");
        Require(SkillCombat.Hit(owner,t,100,"fire")==0&&SkillHealth.Balance256(t)==hp,"Owner damaged converted ally");
    }
    private static void Expire(BaseCreature target)
    {
        var state=PaladinConversion.Snapshot().Single(s=>s.Target==target.Serial.Value);
        int restored=state.Life==null?SkillHealth.Balance256(target):PaladinConversion.RestoredLife256(state.Life,SkillHealth.Maximum256(target),SkillHealth.Balance256(target));
        PaladinConversion.Restore(PaladinConversion.Snapshot().Select(s=>s.Target==state.Target?s with{UntilUtc=TimedEffects.Now.AddSeconds(-1)}:s).ToArray());PaladinConversion.Prune();
        Require(!PaladinConversion.Active(target)&&SkillHealth.Balance256(target)==restored,"NPC conversion expiry changed source life restoration");
    }
    private static BaseCreature Shot(AssassinShadowBody b,PlayerMobile owner,bool convert,bool immune=false,bool lethal=false,bool hell=false)
    {
        var t=Target(b,hell);if(lethal){SkillHealth.SetBalance256(t,256);}if(immune){t.SetResistance(ResistanceType.Physical,100);}
        var at=TimedEffects.Now.AddMinutes(5);CombatPotions.Apply(owner,3,at);var v=ShadowSkillCasting.Values(b,b.OwnedSkillRank(273),273);var seed=Seed(v,convert);
        Require(PaladinConversion.MindBlastEligible(b,owner,t),"Ordinary NPC conversion fixture is not eligible");
        int hp=SkillHealth.Balance256(t),max=SkillHealth.Maximum256(t),level=ClassStats.Level(t),casterLevel=ClassStats.Level(b);var world=NpcWorldAllocation.Capture();
        Require(casterLevel==OriginalNpcEquipment.Level(b),"Conversion used a foreign caster level");
        NpcEquipmentRandom.Set(b,seed.Start);int mana=NecromancerEffects.ManaBalance256(owner);ClassCombat.Stop(owner);
        if(convert&&!hell)
        {
            ClassCombat.For(owner).NextAction=ClassCombat.For(owner).RecoveryUntil=Core.TickCount;
            Require(ClassCombat.Perform(owner,CombatAbility.FireBlast,t,t.Location,Core.TickCount),"Independent owner cast fixture");
        }
        var ownerAction=ClassCombat.For(owner).PendingAbility;long ownerAt=ClassCombat.For(owner).EffectAt;
        try
        {
            Require(ShadowActions.BeginMindBlast(b,t,t.Location,at),"Own Mind Blast entry refused");var action=ShadowActions.State(b);
            int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b).Seed==seed.Start&&!PaladinConversion.Active(t),"Mind Blast released before SC marker");
            ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);
            int damage=immune?0:NamedElements.To256(seed.Raw.Physical);
            if(lethal){Require(t.Deleted||!t.Alive,"Mind Blast typed lethal return failed");}
            else if(convert)
            {
                int expected=hp-damage;ConversionLife life=null;
                if(level>casterLevel){life=PaladinConversion.Scale(level,max>>8,casterLevel,out expected,expected>>8);}
                var state=PaladinConversion.Snapshot().Single(s=>s.Target==t.Serial.Value);
                Require(state.Source==b.Serial.Value&&state.UntilUtc==at.AddMilliseconds(release*40+seed.Frames*40L)&&state.Life==life&&SkillHealth.Balance256(t)==expected,"NPC conversion damage/source/level/timer mismatch");
                Require(CombatStuns.Remaining(t)==0&&SourceKnockbacks.Remaining(t)==0,"Successful conversion applied stun or knockback");Protection(b,owner,t);
                Require(ClassCombat.For(owner).PendingAbility==ownerAction&&ClassCombat.For(owner).EffectAt==ownerAt,"NPC conversion canceled owner's independent action");
            }
            else
            {
                Require(SkillHealth.Balance256(t)==hp-damage&&!PaladinConversion.Active(t)&&CombatStuns.Remaining(t)>0&&SourceKnockbacks.Remaining(t)>0,"Failed conversion lost own damage/stun/knockback or immunity");
            }
            Require(NpcEquipmentRandom.Require(b).Seed==seed.End,"Mind Blast did not use one own area draw followed by own inclusive chance/duration draws");
            if(t is ShadowPsychicChecks.Witness witness)
            {Require(witness.Notifications==(immune?0:1)&&(immune||witness.SeedAtNotification==seed.End),"Conversion chance/duration notification mismatch: "+Json(new{convert,immune,lethal,witness.Notifications,witness.SeedAtNotification,Expected=seed.End}));}
            ShadowActions.Tick(at.AddMilliseconds((end+10)*40),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b).Seed==seed.End&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(owner)==mana,"Mind Blast end replayed,allocated a projectile or borrowed owner mana");
            if(convert&&!lethal){return t;}
        }
        finally{ClassCombat.Stop(owner);ShadowActions.Cancel(b);if(!convert||lethal){CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Corpse?.Delete();t.Delete();}}
        return null;
    }
    private static void Area(AssassinShadowBody b)
    {
        var first=Target(b);var second=Target(b);second.MoveToWorld(new(first.X,first.Y+1,first.Z),b.Map);
        var outside=Target(b);outside.MoveToWorld(new(first.X+8,first.Y,first.Z),b.Map);var at=TimedEffects.Now.AddMinutes(5);var v=ShadowSkillCasting.Values(b,b.OwnedSkillRank(273),273);
        try
        {
            ulong start=SourceRandom.InitLow(42),expected=start;var raw=MindBlasts.Damage(v,n=>(int)SourceRandom.Limited(ref expected,n));var victims=new[]{first,second}.OrderBy(t=>t.Serial).ToArray();
            var conversions=new bool[2];var notification=new ulong[2];
            for(int i=0;i<2;i++){conversions[i]=SourceRandom.Limited(ref expected,100)<=MindBlasts.Chance(v);if(conversions[i]){SourceRandom.Limited(ref expected,v.Param(4));}notification[i]=expected;}
            int[] hp=victims.Select(SkillHealth.Balance256).ToArray();int untouched=SkillHealth.Balance256(outside);NpcEquipmentRandom.Set(b,start);
            Require(ShadowActions.BeginMindBlast(b,null,first.Location,at),"Ground area cast refused");ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b).Seed==expected&&SkillHealth.Balance256(outside)==untouched,"Area shared draw/order/radius failed");
            for(int i=0;i<2;i++)
            {Require(hp[i]-SkillHealth.Balance256(victims[i])==NamedElements.To256(raw.Physical)&&PaladinConversion.Active(victims[i])==conversions[i]&&((ShadowPsychicChecks.Witness)victims[i]).SeedAtNotification==notification[i],"Area did not reuse one physical packet in serial order");}
            NpcEquipmentRandom.Set(b,start);expected=start;MindBlasts.Damage(v,n=>(int)SourceRandom.Limited(ref expected,n));
            Require(ShadowActions.BeginMindBlast(b,null,new(b.X-6,b.Y,b.Z),at),"Empty ground refused");ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b).Seed==expected,"Empty ground did not roll exactly one area packet");
        }
        finally{foreach(var t in new[]{first,second,outside}){CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Delete();}PaladinConversion.Prune();ShadowActions.Cancel(b);}
    }
    private static void Refusals(AssassinShadowBody b)
    {
        var t=Target(b);var at=TimedEffects.Now.AddMinutes(5);var rng=NpcEquipmentRandom.Require(b);int hp=SkillHealth.Balance256(t);
        try
        {
            Require(!ShadowActions.BeginMindBlast(b,null,new(1438,1696,0),at)&&NpcEquipmentRandom.Require(b)==rng,"Town area drew RNG");
            Require(ShadowActions.BeginMindBlast(b,t,t.Location,at),"Park cancellation entry");PersistentMinions.Park(b);ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng,"Parked Mind Blast released");
        }
        finally{t.Delete();ShadowActions.Cancel(b);}
    }
    private static void Unique(AssassinShadowBody b)
    {
        foreach(var grade in new[]{EncounterGrade.Unique,EncounterGrade.Champion})foreach(int selected in grade==EncounterGrade.Unique?new[]{1,99}:new[]{99})
        {
            var t=new EncounterCreature();var profile=EncounterProfiles.All.First(p=>p.Archetype==MonsterArchetype.Tank);
            t.Assign(profile,60,MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,null,new Random(1));
            t.Frozen=t.CantWalk=true;t.NoKillAwards=true;t.SetHits(65000);foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}
            t.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);
            try
            {
                var at=TimedEffects.Now.AddMinutes(5);var v=ShadowSkillCasting.Values(b,b.OwnedSkillRank(273),273);ulong start=0,expected=0;GearDamage raw=default;
                bool eligible=PaladinConversion.MindBlastEligible(b,(PlayerMobile)b.ControlMaster,t);
                for(int low=1;low<10000;low++){ulong copy=SourceRandom.InitLow(low);var damage=MindBlasts.Damage(v,n=>(int)SourceRandom.Limited(ref copy,n));if(eligible&&SourceRandom.Limited(ref copy,100)<=MindBlasts.Chance(v)){continue;}if(grade==EncounterGrade.Unique&&SourceRandom.Limited(ref copy,100)!=(uint)selected){continue;}start=SourceRandom.InitLow(low);expected=copy;raw=damage;break;}
                bool mapped=CombatStuns.Rules(t,out bool boss,out int velocity);Require(start!=0&&mapped&&(grade!=EncounterGrade.Champion||!boss&&velocity>0),"Unique/champion source-stun fixture");int duration=(grade!=EncounterGrade.Unique||selected>=90)&&!boss&&velocity!=0?Math.Min(v.DurationFrames,250):0;
                int hp=SkillHealth.Balance256(t);NpcEquipmentRandom.Set(b,start);Require(ShadowActions.BeginMindBlast(b,t,t.Location,at),"Unique Mind Blast entry");var action=ShadowActions.State(b);int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame;
                ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
                Require(!PaladinConversion.Active(t)&&hp-SkillHealth.Balance256(t)==NamedElements.To256(raw.Physical)&&NpcEquipmentRandom.Require(b).Seed==expected,"Elite conversion/stun/damage stream mismatch: "+Json(new{grade,eligible,selected,Damage=hp-SkillHealth.Balance256(t),ExpectedDamage=NamedElements.To256(raw.Physical),Seed=NpcEquipmentRandom.Require(b).Seed,ExpectedSeed=expected}));
                var state=CombatStuns.Snapshot().SingleOrDefault(s=>s.Target==t.Serial.Value);
                Require(duration==0?state==null:state?.UntilUtc==at.AddMilliseconds((release+duration)*40L),"Unique stun acceptance/duration did not use its actual source metadata");
                if(grade==EncounterGrade.Champion)
                {
                    CombatStuns.Clear(t);SourceKnockbacks.Clear(t);var stunRoll=CombatStuns.Roll;var chanceRoll=MindBlasts.Roll;
                    try
                    {
                        CombatStuns.Roll=_=>throw new InvalidOperationException("Champion consumed a Unique-only stun roll");MindBlasts.Roll=n=>n-1;
                        Require(MindBlasts.Impact((PlayerMobile)b.ControlMaster,t,v,raw)&&CombatStuns.Remaining(t)>0,"Player Mind Blast did not share corrected champion stun eligibility");
                    }
                    finally{CombatStuns.Roll=stunRoll;MindBlasts.Roll=chanceRoll;}
                }
            }
            finally{CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Delete();ShadowActions.Cancel(b);}
        }
    }
    private static void Warrior(AssassinShadowBody b,PlayerMobile owner)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(10));TimedEffects.PreventHealing(b,TimedEffects.Now);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.MindBlast),"Owner Mind Blast selection refused");var t=Target(b);t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);
        try
        {
            ulong chosen=0;for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100>70){continue;}if(SourceRandom.Roll(ref copy)%5<=SourceRandom.Roll(ref copy)%100){chosen=seed;break;}}
            Require(chosen!=0,"No Mind Blast AI seed");NpcEquipmentRandom.Set(b,chosen);int hp=SkillHealth.Balance256(t);
            void Through(int frame){var s=b.WorldGround.State.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);}
            Through(20);var action=ShadowActions.State(b);Require(action?.Cast.Skill==273&&ShadowGroundMotion.Mode(b)==7,"Warrior did not select own Mind Blast SC");
            Through(20+action.Plan.Events.Single(e=>e.Type==2).Frame);Require(SkillHealth.Balance256(t)<hp&&ShadowActions.State(b)==null,"Warrior Mind Blast did not release/end");
        }
        finally{CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Delete();PaladinConversion.Prune();PersistentMinions.Park(b);}
    }
    private static Held Capture(AssassinShadowBody b,BaseCreature target)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b),target.Serial.Value,PaladinConversion.Snapshot().Single(s=>s.Target==target.Serial.Value),SkillHealth.Balance256(target));
    private static void HigherLevel()
    {
        var p=new PlayerMobile{Name="ShadowOwnLevelFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,30);FixtureSpace.Corridor(p,6260,24,3576);ClassSkillChecks.Train(p,"assassin.shadowmaster",1);
        AssassinShadowBody b=null;BaseCreature target=null;
        try
        {
            b=AssassinShadowBody.CreateInitialized(p,CombatAbility.ShadowMaster,TimedEffects.Now);b.ChangeSkills(book=>book.Assign(273,10));GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            ClassCreation.Profiles.AdvanceLevel(p,99);Require(ClassStats.Level(b)==30,"Shadow level followed live owner");target=Shot(b,p,true,hell:true);
            Require(PaladinConversion.Life(target)is{Level:30,OriginalLevel:>30},"Higher-level NPC target was scaled with live owner instead of original Shadow");Expire(target);Require(ClassCombat.IsHostile(p,target),"Conversion expiry added friendly grace");
        }
        finally{target?.Delete();b?.Delete();p.Delete();PaladinConversion.Prune();}
    }
    private static void Check(bool reload)
    {
        _=ShadowMindBlasts.Row();PlayerMobile owner;AssassinShadowBody[] bodies;BaseCreature[] targets;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();targets=saved.Bodies.Select(h=>(BaseCreature)World.FindMobile((Serial)h.Target)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i],targets[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload lost own Mind Blast source/items/wounds/RNG/allegiance or replayed action");Protection(bodies[i],owner,targets[i]);}
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.fireblast",1);ClassSkillChecks.Train(owner,"assassin.mindblast",1);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();targets=new BaseCreature[2];
            for(int i=0;i<bodies.Length;i++)
            {
                var b=bodies[i];b.ChangeSkills(book=>book.Assign(273,10));GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
                Shot(b,owner,false);Shot(b,owner,false,immune:true);Shot(b,owner,false,lethal:true);Area(b);Unique(b);var expire=Shot(b,owner,true);Expire(expire);expire.Delete();targets[i]=Shot(b,owner,true);Refusals(b);
            }
            HigherLevel();Warrior(bodies[0],owner);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select((b,i)=>Capture(b,targets[i])).ToArray(),generation)));
        Log.Information("Shadow Mind Blast:both species own SC/do51,one physical area packet,inclusive own chance/duration,damage before conversion/no stun or knockback,ordinary/immunity/lethal stun returns,own source level/credit,owner action and potion/mana exclusion,ground area/radius/empty/town/park,original actors/items/wounds/RNG/allegiance through two reloads;other consumers/Master AI/death/learned routes pending");
    }
}
