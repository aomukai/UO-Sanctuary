// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowPsychicChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowPsychicChecks));
    public sealed class Witness:GraveyardZombie
    {
        internal uint CasterId;internal ulong SeedAtNotification;internal int Notifications;
        public Witness(){CantWalk=true;Frozen=true;NoKillAwards=true;}public Witness(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;if(World.FindMobile((Serial)CasterId)is AssassinShadowBody b){SeedAtNotification=NpcEquipmentRandom.Require(b).Seed;}base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);Require(reader.ReadInt()==0,"Witness version");}
    }
    private sealed record Held(uint Actor,string Body,string Resources,string[] Items,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-psychic-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    public static void Configure(){Register("shadow-psychic-check",false);Register("shadow-psychic-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Own Shadow Psychic Hammer SC,typed HP/RNG/knockback and saves",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Witness Target(AssassinShadowBody b,int life=65000)
    {
        var t=new Witness{CasterId=b.Serial.Value};t.SetHits(life);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        t.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static void Shot(AssassinShadowBody b,PlayerMobile owner,bool physicalImmune,bool lethal)
    {
        var t=Target(b,lethal?1:65000);var at=TimedEffects.Now.AddMinutes(5);CombatPotions.Apply(owner,3,at);
        try
        {
            if(physicalImmune){t.SetResistance(ResistanceType.Physical,100);}
            Require(CombatPotions.BonusAt(owner,"magical-penetration",at)>0&&NamedElements.MagicResistance(t)==0,"Owner potion/zero magic-resistance fixture");
            int mana=NecromancerEffects.ManaBalance256(owner),life=SkillHealth.Balance256(t);var world=NpcWorldAllocation.Capture();var random=NpcEquipmentRandom.Require(b);ulong expected=random.Seed;
            var graph=b.EquipmentNumbers;var values=ShadowSkillCasting.Values(b,b.OwnedSkillRank(253),253);
            var raw=PsychicHammers.Damage(values,graph.Read(357),n=>(int)SourceRandom.Limited(ref expected,n));ulong damageSeed=expected;
            int chance=PsychicHammers.Chance(owner,t,values);Require(chance==100,"Ordinary target knockback fixture");SourceRandom.Roll(ref expected);
            int damage=NamedElements.To256(raw.Magic+(physicalImmune?0:raw.Physical));
            Require(ShadowActions.BeginPsychicHammer(b,t,at),"Intrinsic Hammer SC entry refused");var action=ShadowActions.State(b);
            int release=action.Plan.Events.Single(e=>e.Type==1&&e.Kind==1).Frame,end=action.Plan.Events.Single(e=>e.Type==2).Frame;
            ShadowActions.Tick(at.AddMilliseconds((release-1)*40),true,b.Serial.Value);
            Require(SkillHealth.Balance256(t)==life&&NpcEquipmentRandom.Require(b)==random&&t.Notifications==0,"Hammer damaged/drew RNG before SC marker");
            ShadowActions.Tick(at.AddMilliseconds(release*40),true,b.Serial.Value);
            Require(NpcEquipmentRandom.Require(b).Seed==expected&&t.SeedAtNotification==damageSeed,"Hammer did not use own physical/magic draws then post-damage chance,including lethal hits");
            if(lethal){Require(!t.Alive||t.Deleted,"Typed Hammer lethal return did not finish death");}
            else
            {
                Require(life-SkillHealth.Balance256(t)==damage&&SourceKnockbacks.Remaining(t)>0,"Typed mixed Hammer damage/immunity/knockback failed or borrowed owner potion");
                Require(!TimedEffects.Snapshot().Any(s=>s.Owner==t.Serial.Value&&s.PoisonMs>0),"Hammer inherited owner/weapon poison");
            }
            ShadowActions.Tick(at.AddMilliseconds((end+10)*40),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&b.OwnedSkills.Used==null&&NpcEquipmentRandom.Require(b).Seed==expected&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(owner)==mana,"Hammer end replayed,allocated missiles or borrowed owner mana");
        }
        finally{SourceKnockbacks.Clear(t);if(!t.Deleted){t.Delete();}ShadowActions.Cancel(b);}
    }
    private static void Refusals(AssassinShadowBody b,PlayerMobile owner)
    {
        var t=Target(b);var at=TimedEffects.Now.AddMinutes(5);
        try
        {
            var rng=NpcEquipmentRandom.Require(b);int mana=NecromancerEffects.ManaBalance256(owner);
            Require(!ShadowActions.BeginPsychicHammer(b,null,at)&&NpcEquipmentRandom.Require(b)==rng,"No-target Hammer cast or drew RNG");
            t.MoveToWorld(new(1438,1696,0),Map.Trammel);Require(Server.Spells.SpellHelper.IsTown(t.Location,t),"Town target fixture");
            Require(!ShadowActions.BeginPsychicHammer(b,t,at)&&NpcEquipmentRandom.Require(b)==rng,"Town target cast or drew RNG");t.MoveToWorld(new(b.X+2,b.Y,b.Z),b.Map);
            Require(ShadowActions.BeginPsychicHammer(b,t,at),"Lost-target fixture failed to start");t.Delete();ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&NpcEquipmentRandom.Require(b)==rng,"Lost target drew damage/chance RNG or replayed");
            t=Target(b);int hp=SkillHealth.Balance256(t);Require(ShadowActions.BeginPsychicHammer(b,t,at),"Park fixture failed to start");PersistentMinions.Park(b);ShadowActions.Tick(at.AddSeconds(10),true,b.Serial.Value);
            Require(ShadowActions.State(b)==null&&SkillHealth.Balance256(t)==hp&&NpcEquipmentRandom.Require(b)==rng&&NecromancerEffects.ManaBalance256(owner)==mana,"Parked Hammer released damage or cost");
        }
        finally{if(!t.Deleted){t.Delete();}ShadowActions.Cancel(b);}
    }
    private static void Warrior(AssassinShadowBody b,PlayerMobile owner)
    {
        b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);ShadowGroundMotion.Activate(b,owner,TimedEffects.Now.AddMinutes(10));TimedEffects.PreventHealing(b,TimedEffects.Now);
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(owner,false,CombatAbility.PsychicHammer),"Owner Hammer selection refused");
        var t=Target(b);t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);
        try
        {
            // Both initial left/right alternatives reach the eligible Hammer;
            // choose a seed that passes its whole-mana and budget gates.
            ulong chosen=0;
            for(int i=1;i<10000;i++){ulong seed=SourceRandom.InitLow(i),copy=seed;SourceRandom.Roll(ref copy);if(SourceRandom.Roll(ref copy)%100>70){continue;}if(SourceRandom.Roll(ref copy)%5<=SourceRandom.Roll(ref copy)%100){chosen=seed;break;}}
            Require(chosen!=0,"No Hammer AI seed");NpcEquipmentRandom.Set(b,chosen);int hp=SkillHealth.Balance256(t);
            void Through(int frame){var s=b.WorldGround.State.Controller;IronGolemWorld.Advance(b,s.NextUtc.AddMilliseconds((frame-s.Frame-1)*40L),true);}
            Through(20);var action=ShadowActions.State(b);Require(action?.Cast.Skill==253&&ShadowGroundMotion.Mode(b)==7,"Warrior did not choose/enter its own Hammer SC");
            Through(20+action.Plan.Events.Single(e=>e.Type==2).Frame);Require(SkillHealth.Balance256(t)<hp&&ShadowActions.State(b)==null,"Warrior AI Hammer did not release/end");
        }
        finally{SourceKnockbacks.Clear(t);t.Delete();PersistentMinions.Park(b);}
    }
    private static void Check(bool reload)
    {
        _=ShadowPsychicHammers.Row();PlayerMobile owner;AssassinShadowBody[] bodies;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);bodies=saved.Bodies.Select(h=>(AssassinShadowBody)World.FindMobile((Serial)h.Actor)).ToArray();
            for(int i=0;i<bodies.Length;i++){Require(Json(Capture(bodies[i]))==Json(saved.Bodies[i])&&ShadowActions.State(bodies[i])==null,"Reload changed own Hammer actor/items/wounds/RNG or replayed action");}
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);
            bodies=new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}.Select(a=>AssassinShadowBody.CreateInitialized(owner,a,TimedEffects.Now)).ToArray();
            foreach(var b in bodies)
            {
                b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(253,10);book.Assign(263,20);});GeneratedNpcLiveResources.ActivateItemEvents(b);ShadowEquipmentDefense.Activate(b);SkillHealth.SetBalance256(b,b.MaximumLife256*2/3-1);
                b.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Shot(b,owner,false,false);Shot(b,owner,true,false);Shot(b,owner,false,true);Refusals(b,owner);
            }
            Warrior(bodies[0],owner);
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow Psychic Hammer:both species use own intrinsic SC/start22/do33,physical/magic and post-damage own RNG,typed HP/death/knockback,physical immunity,no owner potion/mana/projectile,loss/town/park cancellation,actual Warrior AI and saved original actor/items/wounds/RNG;Mind Blast/other consumers/Master AI/death42/learned routes remain pending");
    }
}
