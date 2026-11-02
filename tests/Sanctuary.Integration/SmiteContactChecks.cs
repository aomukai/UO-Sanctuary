// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SmiteContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SmiteContactChecks));
    private sealed class NoProc:Random{public override int Next(int maxValue)=>maxValue-1;}
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications,LifeAtNotification,StunAtNotification;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;LifeAtNotification=SkillHealth.Balance256(this);StunAtNotification=CombatStuns.Remaining(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("smite-contact-check","Smite ordered shield contact and deferred target death",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("smite-contact-check: passed");}catch(Exception e){Log.Error(e,"smite-contact-check: failed");}}));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="smite-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Smite input and release payment",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();var shield=new Buckler();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));
        p.AddToBackpack(shield);Require(p.EquipItem(shield),"Smite contact Buckler");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Smite)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Smite)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Smite),"Smite contact bindings");return p;
    }
    private static T Place<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.Frozen=t.CantWalk=true;t.NoKillAwards=true;t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static bool Begin(PlayerMobile p,BaseCreature t)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.Smite,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinAuras.Clear(t);CombatStuns.Clear(t);SourceKnockbacks.Clear(t);MonsterUninterruptibles.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static void Notification()
    {
        var p=Owner();var t=Place(p,new Recipient());
        try
        {
            int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),raw=PaladinSmite.DamageRange256(p,SkillMechanics.Values(p,CombatAbility.Smite)).Min;
            Require(Begin(p,t)&&t.Notifications==0&&CombatStuns.Remaining(t)==0&&NecromancerEffects.ManaBalance256(p)==mana,"Smite allocated/paid before Do150 event");
            Require(Finish(p)&&t.Notifications==1&&t.LifeAtNotification==before&&t.StunAtNotification==0&&before-SkillHealth.Balance256(t)==raw
                &&CombatStuns.Remaining(t) is >0 and <=600&&SourceKnockbacks.Remaining(t)>0&&NativeMonsterReturn.Session(t).LastHitClass==0x65
                &&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback&&NativeMonsterReturn.Session(t).Packets==1
                &&NecromancerEffects.ManaBalance256(p)==mana-512&&t.FindDamageEntryFor(p)?.Damager==p,"Smite notification/HP/stun/knockback/hit-class/credit/payment order");
            Require(!Finish(p)&&t.Notifications==1,"Smite packet replayed");
        }
        finally{Clean(p,t);}
    }
    private static void Lethal(bool native)
    {
        var p=Owner();var t=Place(p,native?(BaseCreature)new Ogre():new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int targetLifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){targetLifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}created?.Invoke(corpse);
            };
            Require(Begin(p,t)&&Finish(p)&&!p.Alive&&!t.Alive&&targetLifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1,
                "Smite must commit target HP before retaliation and defer target death until return");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,"Smite player credit or packet consumption changed");
            if(native){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Native Smite target acquired an invented source identity");}
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void Results()
    {
        foreach(string mode in new[]{"hit","miss","block"})
        {
            var p=Owner();var t=Place(p,new Recipient());string draws="";
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                PaladinSmite.Roll=n=>{draws+="damage;";return 0;};PaladinSmite.HitRoll=()=>{draws+="hit;";return mode=="miss"?1:0;};
                SourceBlocking.BlockRoll=n=>{draws+="block;";return mode=="block"?0:n-1;};
                int before=SkillHealth.Balance256(t),raw=PaladinSmite.DamageRange256(p,SkillMechanics.Values(p,CombatAbility.Smite)).Min;
                Require(Begin(p,t)&&Finish(p)&&before-SkillHealth.Balance256(t)==raw&&CombatStuns.Remaining(t)>0&&SourceKnockbacks.Remaining(t)>0,
                    "Smite failed to force damage/stun/knockback after ordinary "+mode);
                Require(draws==(mode=="miss"?"damage;hit;":"damage;hit;block;"),"Smite source damage/result RNG order differs: "+draws);
            }
            finally{Clean(p,t);}
        }
        PaladinSmite.Roll=_=>0;PaladinSmite.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
    }
    private static void Exclusions()
    {
        var p=Owner();var t=Place(p,new Ogre());
        try
        {
            while(p.RawStr<75){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Smite gear Strength");}
            while(p.RawDex<70){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Smite gear Dexterity");}
            foreach(string skill in new[]{"paladin.smite","paladin.holyshield"})
            {while(ClassCreation.Profiles.GetRank(p.Serial,skill)<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,skill,out _),"Smite exclusion rank20");}}
            PoisonDaggerChecks.Named(p,"Hellplague");PoisonDaggerChecks.Named(p,"Highlord's Wrath");PoisonDaggerChecks.Named(p,"Greyform");PoisonDaggerChecks.Named(p,"Wormskull");
            HolyShields.Cast(p,SkillMechanics.Values(p,CombatAbility.HolyShield));
            var ordinary=GearEffects.Damage(p,0);int raw=PaladinSmite.DamageRange256(p,SkillMechanics.Values(p,CombatAbility.Smite)).Min;
            Require(ordinary.Fire>0&&ordinary.Lightning>0&&ordinary.Poison>0&&ClassGear.Bonus(p,"deadly")>0&&raw/512*ClassGear.Bonus(p,"life-steal%")/100>0,
                "Smite exclusions lack actual nonzero weapon elements/poison/deadly/leech");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p),before=SkillHealth.Balance256(t);t.SetResistance(ResistanceType.Physical,50);
            Require(Begin(p,t)&&Finish(p)&&before-SkillHealth.Balance256(t)==raw*50/100&&SkillHealth.Balance256(p)==hp
                &&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&(x.PoisonMs>0||x.ChillPercent>0))&&CombatStuns.Remaining(t)==0,
                "Smite included ordinary weapon damage/elements/poison/deadly/leech or invented native stun");
        }
        finally{Clean(p,t);}
    }
    private static void LifeTap()
    {
        var p=Owner();var t=Place(p,new GraveyardZombie());var caster=ClassSkillChecks.Player();caster.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);
        try
        {
            Require(NecromancerEffects.Apply(caster,t,CombatAbility.LifeTap,1),"Smite actual Life Tap curse");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p),raw=PaladinSmite.DamageRange256(p,SkillMechanics.Values(p,CombatAbility.Smite)).Min;
            Require(Begin(p,t)&&Finish(p)&&SkillHealth.Balance256(p)==hp+raw/2,"Smite lost or doubled fractional Life Tap while excluding ordinary leech");
        }
        finally{caster.Delete();Clean(p,t);}
    }
    private static void Recovery()
    {
        foreach(bool rewards in new[]{true,false})
        {
            var p=Owner();while(p.RawStr<106){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Smite recovery shield Strength");}
            p.AddToBackpack(HolyShields.Shield(p));PoisonDaggerChecks.Named(p,"Headhunter's Glory");PoisonDaggerChecks.Named(p,"Silkweave");PoisonDaggerChecks.Named(p,"Tyrael's Might");
            var t=Place(p,new GraveyardZombie());t.NoKillAwards=!rewards;
            try
            {
                int heal=ClassGear.Bonus(p,"heal-kill"),mana=ClassGear.Bonus(p,"mana-kill");Require(heal>0&&mana>0&&ClassGear.Bonus(p,"rip")==1,"Actual Smite recovery/RIP gear");
                SkillHealth.SetBalance256(p,2*256-17);SkillHealth.SetBalance256(t,256);NecromancerEffects.SetMana256(p,50*256-19);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,2,TimedEffects.Now.AddDays(1)));
                Require(Begin(p,t)&&Finish(p)&&p.Alive==rewards&&!t.Alive&&SkillHealth.Balance256(p)==(rewards?heal*256-17:0)
                    &&NecromancerEffects.ManaBalance256(p)==(rewards?(48+mana)*256-19:0),"Smite payment/early recovery/retaliation entitlement order");
                Require(t.Corpse is Corpse corpse&&corpse.SanctuaryRestInPeace==rewards&&!RewardLife.Process(t,corpse)&&CombatItemEvents.KillRecoveryAllowed(p,t),
                    "Smite recovery suppression escaped or repeated death/RIP rewards");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen"})
        {
            var p=Owner();var t=Place(p,new Recipient());
            try
            {
                if(mode=="paragon"){t.IsParagon=true;}
                if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);PaladinSmite.Roll=_=>throw new InvalidOperationException("Unsupported Smite target rolled damage");
                Require(!Begin(p,t)&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&t.Notifications==0&&ClassCombat.For(p).PendingAbility==CombatAbility.None,
                    "Unsupported Smite target rolled/paid/mutated: "+mode);
            }
            finally{Clean(p,t);}
        }
        PaladinSmite.Roll=_=>0;
    }
    private static void ChampionStun()
    {
        var previous=CombatStuns.Roll;
        try
        {
            foreach(int chance in new[]{89,90})
            {
                var p=Owner();var t=new EncounterCreature();t.Assign(t.Dungeon,60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Champion,[],EliteAura.None,null,new Random(173));Place(p,t);
                try
                {
                    int rolls=0;CombatStuns.Roll=n=>{Require(n==100,"Champion stun roll bound");rolls++;return chance;};
                    Require(Begin(p,t)&&Finish(p)&&rolls==1&&(CombatStuns.Remaining(t)>0)==(chance==90),"Smite champion source-unique 10percent stun decision");
                }
                finally{Clean(p,t);}
            }
        }
        finally{CombatStuns.Roll=previous;}
    }
    private static void Check()
    {
        var roll=PaladinSmite.Roll;var hit=PaladinSmite.HitRoll;var effects=PaladinSmite.Effects;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinSmite.Roll=_=>0;PaladinSmite.HitRoll=()=>0;PaladinSmite.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
            Notification();Lethal(false);Lethal(true);Results();Exclusions();LifeTap();Recovery();Refusals();ChampionStun();
            Log.Information("Smite contact:pre-HP notification/credit/hitclass0x65;source/native deferred lethal return;forced hit/miss/block and RNG order;real item weapon/leech exclusions;fractional Life Tap;real recovery/RIP entitlement;3pre-roll refusals;champion89/90stun");
        }
        finally{PaladinSmite.Roll=roll;PaladinSmite.HitRoll=hit;PaladinSmite.Effects=effects;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Place(p,new Ogre());var account=FixtureNetwork.Authenticate(network,p,"smite-input-");
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        var roll=PaladinSmite.Roll;var hit=PaladinSmite.HitRoll;int draws=0;PaladinSmite.Roll=n=>{draws++;return 0;};PaladinSmite.HitRoll=()=>1;
        int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t),raw=PaladinSmite.DamageRange256(p,SkillMechanics.Values(p,CombatAbility.Smite)).Min;
        bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){ClassCombat.Stop(p);PaladinSmite.Roll=roll;PaladinSmite.HitRoll=hit;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Smite input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Smite&&!began)
                {Require(draws==0&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(t)==before,"Wire Smite rolled/paid before event");began=true;Log.Information("{Label}: windup",label);}
                if(SkillHealth.Balance256(t)==before){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&draws==1&&before-SkillHealth.Balance256(t)==raw&&NecromancerEffects.ManaBalance256(p)==mana-512&&NativeMonsterReturn.Session(t).Packets==1,
                    "Actual Smite forced-hit/once payment/damage/contact result");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Smite,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
