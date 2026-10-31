// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PoisonDaggerContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonDaggerContactChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("poison-dagger-contact-check","Live Poison Dagger notification,retaliation,deferred death and kill recovery",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("poison-dagger-contact-check: passed");}catch(Exception e){Log.Error(e,"poison-dagger-contact-check: failed");}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications,AtNotification;
        [Constructible] public Recipient(){CantWalk=Frozen=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;AtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static T Place<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.CantWalk=t.Frozen=true;t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(20000);t.Hits=t.HitsMax;
        foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,0);}return t;
    }
    private static bool Begin(PlayerMobile p,BaseCreature t)
    {PoisonDaggerChecks.Ready(p);return ClassCombat.Perform(p,CombatAbility.PoisonDagger,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Clean(PlayerMobile p,BaseCreature t)
    {MonsterUninterruptibles.Clear(t);PaladinAuras.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();PoisonDaggerChecks.Drop(p,t);}
    private static void Notification()
    {
        var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Dagger(p);var t=Place(p,new Recipient());
        try
        {
            int before=SkillHealth.Balance256(t);Require(Begin(p,t),"Actual ordered start");var impact=ClassCombat.For(p).PoisonDaggerAttack.Impact;
            Require(Finish(p)&&t.Notifications==1&&t.AtNotification==before&&SkillHealth.Balance256(t)==before-impact.LifeDamage256
                &&NativeMonsterReturn.Session(t).Packets==1&&NativeMonsterReturn.Session(t).LastHitClass==0x56
                &&t.FindDamageEntryFor(p)?.Damager==p,"Notification/HP/direct-player credit/knife hit class/free differs");
            int after=SkillHealth.Balance256(t);Require(!Finish(p)&&t.Notifications==1&&SkillHealth.Balance256(t)==after,"Released contact replayed");
        }
        finally{Clean(p,t);}
    }
    private static void Cold()
    {
        var p=PoisonDaggerChecks.Owner();var dagger=PoisonDaggerChecks.Dagger(p);
        p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
        GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Equip cold dagger");ClassGear.Refresh(p);
        var t=Place(p,new Recipient());
        try
        {
            t.SetResistance(ResistanceType.Cold,50);Require(Begin(p,t),"Cold capture start");var prepared=ClassCombat.For(p).PoisonDaggerAttack;
            Require(prepared.ColdFrames==37&&prepared.Impact.Parts.Cold>0,"Actual Sapphire cold length/mitigation");
            t.SetResistance(ResistanceType.Cold,0);Require(Finish(p)&&TimedEffects.TryActor(t,out var actor)&&actor.Cold?.Source==p.Serial.Value
                &&actor.ChillPercent>0,"Captured cold state missing after ordered HP");
            var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
            Require((state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1200 and <=1480,"Cold length recalculated at release");
        }
        finally{Clean(p,t);}
    }
    private static void Attempts()
    {
        foreach(bool block in new[]{false,true})
        {
            var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Dagger(p);var t=Place(p,new Recipient());
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                PoisonDaggerPackets.HitRoll=()=>block?0:1;SourceBlocking.BlockRoll=_=>0;
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1),DamagePercent:250));
                int hp=SkillHealth.Balance256(p),before=SkillHealth.Balance256(t);Require(Begin(p,t)&&Finish(p),"Actual miss/block release");
                Require(SkillHealth.Balance256(p)==hp-4*256&&SkillHealth.Balance256(t)==before&&t.Notifications==0
                    &&NativeMonsterReturn.Session(t).Packets==1,"Miss/block lost or duplicated flat ATD,or dealt health damage");
            }
            finally{Clean(p,t);}
        }
        PoisonDaggerPackets.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;
    }
    private static void Lethal()
    {
        var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Dagger(p);var t=Place(p,new GraveyardZombie());
        var created=CorpseRecoveryHooks.Created;int lifeAtSourceDeath=-1,deaths=0;bool aliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Lethal start");SkillHealth.SetBalance256(t,256);SkillHealth.SetBalance256(p,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);aliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}
                created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&aliveAtSourceDeath&&deaths==1&&sourceDeadAtTargetDeath,
                "Player HP-before-retaliation-before-deferred recipient death differs");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&t.Corpse is Corpse corpse&&!RewardLife.Process(t,corpse)
                &&NativeMonsterReturn.Session(t).Packets==1,"Direct player credit,once-only death reward or packet free differs");
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void Recovery()
    {
        foreach(bool rewards in new[]{true,false})
        {
            var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Dagger(p);
            while(p.RawStr<106){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Shield strength");}
            PoisonDaggerChecks.Named(p,"Headhunter's Glory");PoisonDaggerChecks.Named(p,"Silkweave");PoisonDaggerChecks.Named(p,"Tyrael's Might");
            var t=Place(p,new GraveyardZombie{NoKillAwards=!rewards});
            try
            {
                int heal=ClassGear.Bonus(p,"heal-kill"),mana=ClassGear.Bonus(p,"mana-kill");
                Require(heal>0&&mana>0&&ClassGear.Bonus(p,"rip")==1,"Real active recovery/RIP equipment");
                Require(Begin(p,t),"Recovery start");SkillHealth.SetBalance256(t,256);SkillHealth.SetBalance256(p,2*256-17);NecromancerEffects.SetMana256(p,50*256-19);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,2,TimedEffects.Now.AddDays(1)));
                bool finished=Finish(p);int life=SkillHealth.Balance256(p),mp=NecromancerEffects.ManaBalance256(p);
                int expectedLife=rewards?heal*256-17:0,expectedMana=rewards?(50+mana)*256-19:0;
                Require(finished&&p.Alive==rewards&&!t.Alive&&life==expectedLife&&mp==expectedMana,
                    $"Early kill recovery:rewards{rewards},finished{finished},playerAlive{p.Alive},targetAlive{t.Alive},life{life}/expected{expectedLife},mana{mp}/expected{expectedMana}");
                Require(t.Corpse is Corpse corpse&&corpse.SanctuaryRestInPeace==rewards&&!RewardLife.Process(t,corpse),"Recovery suppression also suppressed RIP or reward repeated");
                Require(CombatItemEvents.KillRecoveryAllowed(p,t),"Recovery lease escaped actual contact");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string reason in new[]{"uninterruptible","paragon","evilomen"})
        {
            var p=PoisonDaggerChecks.Owner();PoisonDaggerChecks.Dagger(p);var t=Place(p,new Recipient());
            try
            {
                if(reason=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(reason=="paragon"){t.IsParagon=true;}
                if(reason=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);PoisonDaggerPackets.HitRoll=()=>throw new InvalidOperationException("Refused actor rolled hit");
                Require(!Begin(p,t)&&ClassCombat.For(p).PoisonDaggerAttack==null&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&t.Notifications==0,
                    "Unsupported incoming actor rolled,paid or damaged: "+reason);
            }
            finally{Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PoisonDaggerPackets.HitRoll;var poison=PoisonDaggerPackets.PoisonRoll;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PoisonDaggerPackets.HitRoll=()=>0;PoisonDaggerPackets.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            Notification();Cold();Attempts();Lethal();Recovery();Refusals();
            Log.Information("Poison Dagger contact:real pre-HP notification/direct player credit/knife hit class;real Sapphire captured cold;miss/block flat ATD;lethal retaliation before recipient death;2real item recovery/RIP entitlement cases;3pre-payment capability refusals pass");
        }
        finally{PoisonDaggerPackets.HitRoll=hit;PoisonDaggerPackets.PoisonRoll=poison;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
}
