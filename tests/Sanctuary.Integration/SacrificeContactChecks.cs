// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SacrificeContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SacrificeContactChecks));
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications,LifeAtNotification,SourceLifeAtNotification;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;LifeAtNotification=SkillHealth.Balance256(this);SourceLifeAtNotification=SkillHealth.Balance256(from);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("sacrifice-contact-check","Sacrifice ordered target life,retaliation and deferred death",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("sacrifice-contact-check: passed");}catch(Exception e){Log.Error(e,"sacrifice-contact-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static PlayerMobile Owner()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Paladin);p.Name="SacrificeAllocationFixture";p.AddItem(new Backpack());
        p.MoveToWorld(ClassSkillChecks.Corridor(p,5750,1800),p.Map);return p;
    }
    private static T Place<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.Frozen=t.CantWalk=true;t.NoKillAwards=true;t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static bool Begin(PlayerMobile p,BaseCreature t)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.Sacrifice,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinAuras.Clear(t);MonsterUninterruptibles.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static void Notification(bool weapon)
    {
        var p=Owner();var t=Place(p,new Recipient());
        try
        {
            if(weapon)
            {
                while(p.RawStr<55){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Longsword Strength");}
                while(p.RawDex<39){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Longsword Dexterity");}
                var sword=new Longsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));p.AddToBackpack(sword);Require(p.EquipItem(sword),"Source hit-class longsword");
            }
            int before=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p);Require(Begin(p,t),"Sacrifice notification start");
            var impact=ClassCombat.For(p).SacrificeAttack.Impact;int cost=PaladinSacrifice.Cost256(NamedElements.To256(impact.Parts.Physical),before,8);
            Require(Finish(p)&&t.Notifications==1&&t.LifeAtNotification==before&&t.SourceLifeAtNotification==hp-cost
                &&SkillHealth.Balance256(t)==before-impact.LifeDamage256&&NativeMonsterReturn.Session(t).Packets==1
                &&NativeMonsterReturn.Session(t).LastHitClass==(weapon?3:1)&&t.FindDamageEntryFor(p)?.Damager==p,
                "Sacrifice self-cost/pre-HP notification/typed life/player credit/unarmed hit class/free order");
            Require(!Finish(p)&&t.Notifications==1,"Sacrifice notification replayed");
        }
        finally{Clean(p,t);}
    }
    private static void Lethal(bool native)
    {
        var p=Owner();var t=Place(p,native?(BaseCreature)new Ogre():new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int lifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Sacrifice lethal windup");SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}
                created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1,
                "Sacrifice must commit target HP before retaliation and defer target death until return");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,
                "Sacrifice lost responsible player credit or packet consumption");
            if(native){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Native Ogre was assigned an invented D2 return identity");}
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void Recovery()
    {
        foreach(bool rewards in new[]{true,false})
        {
            var p=Owner();while(p.RawStr<106){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Recovery shield Strength");}
            PoisonDaggerChecks.Named(p,"Headhunter's Glory");PoisonDaggerChecks.Named(p,"Silkweave");PoisonDaggerChecks.Named(p,"Tyrael's Might");
            var t=Place(p,new GraveyardZombie());t.NoKillAwards=!rewards;
            try
            {
                int heal=ClassGear.Bonus(p,"heal-kill"),mana=ClassGear.Bonus(p,"mana-kill");
                Require(heal>0&&mana>0&&ClassGear.Bonus(p,"rip")==1,"Actual Sacrifice recovery/RIP equipment");
                Require(Begin(p,t),"Sacrifice recovery start");SkillHealth.SetBalance256(p,2*256-17);SkillHealth.SetBalance256(t,256);NecromancerEffects.SetMana256(p,50*256-19);
                int cost=PaladinSacrifice.Cost256(NamedElements.To256(ClassCombat.For(p).SacrificeAttack.Impact.Parts.Physical),256,8);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,2,TimedEffects.Now.AddDays(1)));
                Require(Finish(p)&&p.Alive==rewards&&!t.Alive&&SkillHealth.Balance256(p)==(rewards?heal*256-17-cost:0)
                    &&NecromancerEffects.ManaBalance256(p)==(rewards?(50+mana)*256-19:0),"Sacrifice self-cost/early kill recovery/retaliation order");
                Require(t.Corpse is Corpse corpse&&corpse.SanctuaryRestInPeace==rewards&&!RewardLife.Process(t,corpse)
                    &&CombatItemEvents.KillRecoveryAllowed(p,t),"Sacrifice recovery suppression escaped or changed RIP/once-only rewards");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Attempts()
    {
        foreach(bool blocked in new[]{true,false})
        {
            var p=Owner();var t=Place(p,new Recipient());
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                PaladinSacrifice.HitRoll=()=>blocked?0:1;SourceBlocking.BlockRoll=_=>0;
                int hp=SkillHealth.Balance256(p),before=SkillHealth.Balance256(t);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));
                Require(Begin(p,t)&&!ClassCombat.For(p).SacrificeAttack.Hit&&Finish(p)&&SkillHealth.Balance256(t)==before&&t.Notifications==0
                    &&SkillHealth.Balance256(p)==hp-4*256&&NativeMonsterReturn.Session(t).Packets==1,"Sacrifice miss/block lost flat retaliation or charged self-cost");
            }
            finally{Clean(p,t);}
        }
        PaladinSacrifice.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;
    }
    private static void Elements()
    {
        foreach(bool cold in new[]{false,true})
        {
            var p=Owner();var t=Place(p,new Recipient());
            try
            {
                if(cold)
                {
                    var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));
                    p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
                    GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Sacrifice cold dagger");ClassGear.Refresh(p);t.SetResistance(ResistanceType.Cold,50);
                }
                else{PoisonDaggerChecks.Named(p,"Wormskull");}
                int before=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p);Require(Begin(p,t),"Sacrifice element allocation");var saved=ClassCombat.For(p).SacrificeAttack;
                if(cold){Require(saved.Contact.ColdFrames==37,"Captured Sacrifice cold length");t.SetResistance(ResistanceType.Cold,0);}
                else{Require(saved.Impact.AllocatedPoison==new PreparedPoison(102,200),"Actual Wormskull source poison rate/frames");}
                Require(Finish(p)&&before-SkillHealth.Balance256(t)==saved.Impact.LifeDamage256
                    &&hp-SkillHealth.Balance256(p)==PaladinSacrifice.Cost256(NamedElements.To256(saved.Impact.Parts.Physical),before,8),"Sacrifice elemental packet altered self-cost or repeated mitigation");
                var effect=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
                if(cold){Require(effect.ChillPercent>0&&(effect.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1200 and <=1480,"Sacrifice cold state recaptured at release");}
                else
                {
                    Require(effect.PoisonClock?.Rate256==102&&effect.PoisonMs==8000,"Sacrifice merged poison state missing");
                    int life=SkillHealth.Balance256(t);TimedEffects.Settle(t,effect.UpdatedUtc.AddMilliseconds(40));
                    Require(life-SkillHealth.Balance256(t)==102,"Sacrifice resumed poison tick rate");
                }
            }
            finally{Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string reason in new[]{"paragon","uninterruptible","evilomen"})
        {
            var p=Owner();var t=Place(p,new Recipient());
            try
            {
                if(reason=="paragon"){t.IsParagon=true;}
                if(reason=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(reason=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t);PaladinSacrifice.HitRoll=()=>throw new InvalidOperationException("Unsupported Sacrifice target rolled hit");
                Require(!Begin(p,t)&&ClassCombat.For(p).SacrificeAttack==null&&SkillHealth.Balance256(p)==hp&&SkillHealth.Balance256(t)==life&&t.Notifications==0,
                    "Unsupported target mutated the action or resources: "+reason);
            }
            finally{Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PaladinSacrifice.HitRoll;var roll=PaladinSacrifice.Roll;var element=PaladinSacrifice.ElementRoll;var poison=PaladinSacrifice.PoisonRoll;
        var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinSacrifice.HitRoll=()=>0;PaladinSacrifice.Roll=_=>0;PaladinSacrifice.ElementRoll=()=>0;PaladinSacrifice.PoisonRoll=_=>0;SourceBlocking.BlockRoll=_=>99;
            Notification(false);Notification(true);Lethal(false);Lethal(true);Recovery();Attempts();Elements();Refusals();
            Log.Information("Sacrifice contact:pre-HP notification after self-cost;source and unclassified-native lethal retaliation before target death;real recovery/RIP entitlement;miss/block flat return;captured Sapphire cold/Wormskull poison;3pre-roll refusals");
        }
        finally{PaladinSacrifice.HitRoll=hit;PaladinSacrifice.Roll=roll;PaladinSacrifice.ElementRoll=element;PaladinSacrifice.PoisonRoll=poison;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
}
