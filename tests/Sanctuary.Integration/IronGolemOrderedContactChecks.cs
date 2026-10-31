// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class IronGolemOrderedContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemOrderedContactChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("iron-golem-ordered-contact-check","Live original-item allocation/contact/HP/retaliation/death",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("iron-golem-ordered-contact-check: passed");}catch(Exception e){Log.Error(e,"iron-golem-ordered-contact-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string id)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){Train(owner,prior);}if(ClassCreation.Profiles.GetRank(owner.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static PlayerMobile Owner(int level=99)
    {
        var owner=new PlayerMobile{Name="Ordered Iron owner [fixture]",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,level);Train(owner,"necromancer.irongolem");
        FixtureSpace.Corridor(owner,6100,24,2000);return owner;
    }
    private static Item Original(PlayerMobile owner,string name)
    {
        Item item=name==null?new Broadsword():NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name||x.SourceKey=="The "+name).Id,99,new Random(173),true);
        if(name==null){ClassGear.Profiles.Ensure(item);}item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);return item;
    }
    public sealed class Recipient:GraveyardZombie
    {
        internal Mobile Source;internal int Notifications,AtNotification,DeathCalls;internal bool SourceDeletedAtDeath;
        [Constructible] public Recipient(){CantWalk=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;AtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override bool OnBeforeDeath(){DeathCalls++;SourceDeletedAtDeath=Source?.Deleted==true;return base.OnBeforeDeath();}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static Recipient Target(PlayerMobile owner,IronGolemBody source)
    {
        var target=new Recipient{Source=source,NoKillAwards=true};target.SetHits(20000);target.MoveToWorld(source.Location,owner.Map);target.AIObject.AITimer.Stop();
        foreach(var resistance in Enum.GetValues<ResistanceType>()){target.SetResistance(resistance,0);}TimedEffects.PreventHealing(target,TimedEffects.Now);return target;
    }
    private static ulong HitSeed(IronGolemBody body,BaseCreature target,bool hit=true)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(body),ClassStats.Defense(target),ClassStats.Level(body),ClassStats.Level(target));
        for(ulong seed=1;seed<10000;seed++){ulong next=seed;if((SourceRandom.Roll(ref next)%100/100.0<chance)==hit){return seed;}}
        throw new InvalidOperationException("No deterministic contact seed");
    }
    private static IronGolemWorldState Ready(IronGolemBody body,BaseCreature target)
    {
        var s=body.Prepared.Controller with{Mode=4,Target=target.Serial.Value,NextThink=0,ActionAt=1,EndAt=20,AttackSpeed=256};
        body.RestoreController(s);NpcEquipmentRandom.Set(body,HitSeed(body,target));return s;
    }
    private static void Clean(PlayerMobile owner,BaseCreature target)
    {
        MonsterUninterruptibles.Clear(target);PaladinAuras.Clear(target);NecromancerEffects.Profiles.Clear(target);target.Corpse?.Delete();target.Delete();
        foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Corpse?.Delete();pet.Delete();}owner.Delete();
    }
    private static void Originals()
    {
        int cases=0;
        foreach(string name in new string[]{null,"Battlebranch","Lance of Yaggai","Rakescar","Gleamscythe","Shaftstop"})
        {
            var owner=Owner();var original=Original(owner,name);var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),1);var target=Target(owner,body);
            try
            {
                var state=Ready(body,target);ulong seed=NpcEquipmentRandom.State(body).Seed;
                // A separately prepared reference packet uses the same real
                // result/allocation consumer; restore only its offensive seed.
                int flags=IronGolemWorld.Result(body,owner,state,target);var impact=NpcEquipmentImpact.Prepare(body,target,flags);
                ulong afterRoll=NpcEquipmentRandom.State(body).Seed;NpcEquipmentRandom.Set(body,seed);
                int life=SkillHealth.Balance256(target);IronGolemWorld.Advance(body,state.NextUtc,true);
                var session=NativeMonsterReturn.Session(target);
                Require(body.Prepared.Controller is{Attempts:1,Hits:1,Fault:null}&&SkillHealth.Balance256(target)==life-impact.Packet.Total256
                    &&target.Notifications==1&&target.AtNotification==life&&session.Packets==1&&session.LastHitClass!=0
                    &&NpcEquipmentRandom.State(body).Seed==afterRoll,"Live packet/HP/notification/free/RNG differs: "+name);
                if(impact.Poison.Rate256>0&&impact.Poison.Frames>0)
                {Require(TimedEffects.TryActor(target,out var actor)&&actor.PoisonClock?.Rate256==impact.Poison.Rate256,"Captured live poison state missing");}
                if(impact.Packet.ColdFrames>0)
                {Require(TimedEffects.TryActor(target,out var actor)&&actor.Cold!=null,"Captured live cold state missing");}
                int after=SkillHealth.Balance256(target);IronGolemWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
                Require(target.Notifications==1&&NativeMonsterReturn.Session(target).Packets==1&&SkillHealth.Balance256(target)==after,"Contact replayed next frame");cases++;
            }
            finally{Clean(owner,target);}
        }
        Require(cases==6,"Incomplete live original controls");
    }
    private static void Retaliation()
    {
        var owner=Owner(47);var original=Original(owner,null);var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),1);
        // Reward policies deliberately name real creature types, not fixture
        // subclasses. Exercise the actual zombie's entitlement and callbacks.
        var target=new GraveyardZombie{CantWalk=true};target.SetHits(20000);target.MoveToWorld(body.Location,owner.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Physical,0);
        var created=CorpseRecoveryHooks.Created;int targetLifeAtSourceDeath=-1,targetDeaths=0;bool targetAliveAtSourceDeath=false,sourceDeletedAtTargetDeath=false;
        try
        {
            target.NoKillAwards=false;SkillHealth.SetBalance256(target,256);SkillHealth.SetBalance256(body,256);
            PaladinAuras.ElementalWeapon(new(target.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==body){targetLifeAtSourceDeath=SkillHealth.Balance256(target);targetAliveAtSourceDeath=target.Alive&&target.Corpse==null;}
                if(corpse.Owner==target){targetDeaths++;sourceDeletedAtTargetDeath=body.Deleted;}
                created?.Invoke(corpse);
            };
            var state=Ready(body,target);ClassCreation.Profiles.TryGet(owner.Serial,out var before);
            int xp=MonsterExperience.Award(CreatureProfiles.Experience(target),CreatureProfiles.Level(target),before.Level);
            Require(xp>0,"Retaliation owner must be below the level99 no-XP ceiling");
            IronGolemWorld.Advance(body,state.NextUtc,true);ClassCreation.Profiles.TryGet(owner.Serial,out var after);
            Require(body.Deleted&&original.Deleted&&targetLifeAtSourceDeath==0&&targetAliveAtSourceDeath&&targetDeaths==1&&sourceDeletedAtTargetDeath,
                "Incoming HP/retaliation/deferred death order differs");
            Require(ClassLoot.Killer(target)==owner&&target.FindDamageEntryFor(body)?.Responsible?.Any(e=>e.Damager==owner)==true
                &&after.Experience==before.Experience+xp&&xp>0,"Retaliation reward: killer="+ClassLoot.Killer(target)?.Serial+", owner="+owner.Serial
                +", fallback="+PreparedMonsterCredit.Owner(target,body)?.Serial+", responsible="+(target.FindDamageEntryFor(body)?.Responsible?.Any(e=>e.Damager==owner)==true)
                +", XP="+before.Experience+" -> "+after.Experience+", expected="+xp+", alive="+owner.Alive+", corpse="+(target.Corpse!=null));
            Require(target.Corpse is Corpse corpse&&!RewardLife.Process(target,corpse),"Cached death reward replayed");
        }
        finally{CorpseRecoveryHooks.Created=created;body.Corpse?.Delete();Clean(owner,target);}
    }
    private static void Refusals()
    {
        int cases=0;
        foreach(string condition in new[]{"noheal","crush","ironmaiden","uninterruptible","evilomen"})
        {
            var owner=Owner();var original=Original(owner,condition=="noheal"?"Blacktongue":condition=="crush"?"Goblin Toe":null);
            var body=IronGolemWorld.Create(owner,original,original.Location,TimedEffects.Now.AddHours(1),1);var target=Target(owner,body);
            try
            {
                if(condition=="ironmaiden"){NecromancerEffects.Profiles.Store(body,new(new(CombatAbility.IronMaiden,owner.Serial.Value,1,TimedEffects.Now.AddDays(1),owner.X,owner.Y)));}
                if(condition=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(target.Serial.Value,true,false,0)).ToArray());}
                if(condition=="evilomen"){target.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                var state=Ready(body,target);ulong seed=NpcEquipmentRandom.State(body).Seed;int life=SkillHealth.Balance256(target);bool refused=false;string why="";
                try{IronGolemContact.Release(body,owner,state,target,state.NextUtc);}catch(NotSupportedException e){refused=true;why=e.Message;}
                Require(refused&&why.Length>0&&NpcEquipmentRandom.State(body).Seed==seed&&SkillHealth.Balance256(target)==life&&target.Notifications==0
                    &&NativeMonsterReturn.Session(target).Packets==0,"Unsupported capability rolled or delivered damage: "+condition);cases++;
            }
            finally{NecromancerEffects.Profiles.Clear(body);Clean(owner,target);}
        }
        Require(cases==5,"Incomplete refusal controls");
        // Baranar's current cold property has no explicit parameter and its
        // independent fallback duration was never stored in NamedPropertyRoll.
        // Keep that original untouched; do not invent a duration or reroll it.
        foreach(var item in new[]{("Baranar's Star","independent zero-parameter range fallback"),("Hellplague","fireskill")})
        {
            var birthOwner=Owner();var unsupported=Original(birthOwner,item.Item1);
            try
            {
                var point=unsupported.Location;var map=unsupported.Map;bool refused=false;
                try{IronGolemWorld.Create(birthOwner,unsupported,point,TimedEffects.Now.AddHours(1),1);}
                catch(NotSupportedException e){refused=e.Message.Contains(item.Item2,StringComparison.Ordinal);}
                Require(refused&&!unsupported.Deleted&&unsupported.Parent==null&&unsupported.Map==map&&unsupported.Location==point
                    &&!(birthOwner.AllFollowers??[]).OfType<IronGolemBody>().Any(),"Unrepresented original was invented/rerolled/transferred: "+item.Item1);
            }
            finally{unsupported.Delete();birthOwner.Delete();}
        }
    }
    private static void Check()
    {
        Originals();Retaliation();Refusals();Require(!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Live prepared combat enabled unfinished learned casting");
        Log.Information("Ordered Iron contact:6real original packets/once-only HP-native notifications/cold-poison/free/offensive streams;actual HP-before-lethal retaliation-before-deferred recipient death with removed-pet owner XP;5pre-roll capability/2unrepresented birth refusals pass;learned casting remains disabled");
    }
}
