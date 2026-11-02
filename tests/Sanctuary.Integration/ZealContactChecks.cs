// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ZealContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ZealContactChecks));
    private sealed class NoProc:Random{public override int Next(int maxValue)=>maxValue-1;}
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications,LifeAtNotification;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;LifeAtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("zeal-contact-check","Zeal ordered per-event contact",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("zeal-contact-check: passed");}catch(Exception e){Log.Error(e,"zeal-contact-check: failed");}}));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="zeal-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Zeal one-payment two-contact sequence",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Zeal)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Zeal)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Zeal),"Zeal contact bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinAuras.Clear(t);MonsterUninterruptibles.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static bool Begin(PlayerMobile p,Mobile t)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.Zeal,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)
    {var s=ClassCombat.For(p);return ClassCombat.ResolveAction(p,s.Zeal?.EffectAt??s.EffectAt);}
    private static void Lethal(bool native)
    {
        var p=Owner();var t=Target(p,native?(BaseCreature)new Ogre():new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int lifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Zeal lethal sequence start");SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1,
                "Zeal must commit target HP before retaliation and defer target death until return");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,"Zeal ordered credit/packet consumption");
            if(native){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Zeal invented a native target's D2 identity");}
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void LiveEvents()
    {
        var p=Owner();var t=Target(p,new Recipient());int hits=0,physical=0,elements=0;
        try
        {
            PaladinZeal.HitRoll=()=>{hits++;return 0;};PaladinZeal.Roll=_=>{physical++;return 0;};PaladinZeal.ElementRoll=()=>{elements++;return 0;};
            NecromancerEffects.SetMana256(p,529);int life=SkillHealth.Balance256(t);
            Require(Begin(p,t)&&hits==0&&physical==0&&elements==0&&NecromancerEffects.ManaBalance256(p)==17&&SkillHealth.Balance256(t)==life,
                "Zeal rolled contact at windup or failed once-only fractional start payment");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.sacrifice",out _),"Zeal changed hard synergy before first event");
            t.SetResistance(ResistanceType.Physical,50);
            // Fractional source health changes immediately. The existing UO
            // notification adapter reports only changes to displayed whole HP.
            Require(Finish(p)&&life-SkillHealth.Balance256(t)==190&&hits==1&&physical==1&&elements==1&&t.Notifications==0
                &&NativeMonsterReturn.Session(t).LastHitClass==1,"Zeal first event lost live synergy/resistance or fractional HP boundary");
            life=SkillHealth.Balance256(t);t.SetResistance(ResistanceType.Physical,0);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.sacrifice",out _),"Zeal changed synergy between events");
            Require(Finish(p)&&life-SkillHealth.Balance256(t)==412&&hits==2&&physical==2&&elements==2&&t.Notifications==1
                &&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==2&&NecromancerEffects.ManaBalance256(p)==17
                &&!Finish(p)&&t.Notifications==1,"Zeal reused damage or paid again at a rollback event");
        }
        finally{PaladinZeal.HitRoll=()=>0;PaladinZeal.Roll=_=>0;PaladinZeal.ElementRoll=()=>0;Clean(p,t);}
    }
    private static void Attempts()
    {
        foreach(bool blocked in new[]{true,false})
        {
            var p=Owner();var t=Target(p,new Recipient());
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                SourceBlocking.BlockRoll=n=>blocked?0:n-1;PaladinZeal.HitRoll=()=>blocked?0:1;
                PaladinZeal.Roll=_=>throw new InvalidOperationException("Zeal rolled physical damage for a miss/block");
                PaladinZeal.ElementRoll=()=>throw new InvalidOperationException("Zeal rolled item elements for a miss/block");
                int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));
                Require(Begin(p,t)&&Finish(p)&&Finish(p)&&SkillHealth.Balance256(p)==hp-8*256&&SkillHealth.Balance256(t)==life
                    &&t.Notifications==0&&NativeMonsterReturn.Session(t).Packets==2&&NecromancerEffects.ManaBalance256(p)==mana-512,
                    "Zeal miss/block lost attack attempts, flat return, series count or one payment");
            }
            finally{PaladinZeal.HitRoll=()=>0;PaladinZeal.Roll=_=>0;PaladinZeal.ElementRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;Clean(p,t);}
        }
    }
    private static void InitialTarget()
    {
        var p=Owner();var a=Target(p,new Recipient());var b=Target(p,new Recipient());b.Location=new(p.X+1,p.Y+1,p.Z);
        try
        {
            int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);
            Require(Begin(p,a)&&ClassCombat.ContinueCast(p,CombatAbility.Zeal,b,b.Location,Core.TickCount),"Zeal held target change");ClassCombat.ReleaseCast(p);
            Require(ClassCombat.For(p).PendingTarget==a.Serial&&Finish(p)&&SkillHealth.Balance256(a)<ah&&SkillHealth.Balance256(b)==bh
                &&ClassCombat.For(p).Zeal.Target==b.Serial,"Zeal held cursor replaced its start37 target");
            Require(Finish(p)&&SkillHealth.Balance256(b)<bh,"Zeal failed cyclic retarget after original target contact");
        }
        finally{b.Delete();Clean(p,a);}
    }
    private static void MixedItems()
    {
        var p=Owner();var t=Target(p,new Recipient());
        try
        {
            var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
            GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Zeal Sapphire dagger");PoisonDaggerChecks.Named(p,"Wormskull");ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Cold,50);int life=SkillHealth.Balance256(t);Require(Begin(p,t)&&Finish(p),"Zeal item event");
            var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
            Require(state.PoisonClock?.Rate256==102&&state.PoisonMs==8000&&state.ChillPercent>0
                &&(state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1380 and <=1480&&SkillHealth.Balance256(t)<life,
                "Zeal source item poison/cold state differs");
            life=SkillHealth.Balance256(t);TimedEffects.Settle(t,state.UpdatedUtc.AddMilliseconds(40));Require(life-SkillHealth.Balance256(t)==102,"Zeal source poison tick differs");
        }
        finally{Clean(p,t);}
    }
    private static void Drains()
    {
        foreach(bool tap in new[]{false,true})
        {
            var p=Owner();var t=Target(p,new GraveyardZombie());PlayerMobile caster=null;
            try
            {
                while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Zeal leech Strength");}
                PoisonDaggerChecks.Named(p,"Greyform");Require(ClassGear.Bonus(p,"life-steal%")==5,"Actual Greyform leech");
                if(tap){caster=ClassSkillChecks.Player();caster.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);Require(NecromancerEffects.Apply(caster,t,CombatAbility.LifeTap,1),"Zeal actual Life Tap curse");}
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p);
                // Unarmed min1,100Strength and12hard-Sacrifice damage percent:542FP256.
                int expected=542*5/100+(tap?542/2:0);
                Require(Begin(p,t)&&Finish(p)&&SkillHealth.Balance256(p)==hp+expected,"Zeal ordinary fractional leech/Life Tap healed twice or used whole numbers");
            }
            finally{caster?.Delete();Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen","late-paragon"})
        {
            var p=Owner();var t=Target(p,new Recipient());
            try
            {
                if(mode=="late-paragon"){Require(Begin(p,t)&&Finish(p),"Zeal supported first event before late refusal");}
                if(mode is "paragon" or "late-paragon"){t.IsParagon=true;}
                if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),notifications=t.Notifications;PaladinZeal.HitRoll=()=>throw new InvalidOperationException("Unsupported Zeal target rolled hit");
                Require(!(mode=="late-paragon"?Finish(p):Begin(p,t))&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana
                    &&t.Notifications==notifications,"Unsupported Zeal target rolled/paid/mutated "+mode);
                Require(mode!="late-paragon"||ClassCombat.For(p).Zeal.Remaining==0,"Zeal continued unsupported rollback series");
            }
            finally{PaladinZeal.HitRoll=()=>0;Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PaladinZeal.HitRoll;var roll=PaladinZeal.Roll;var element=PaladinZeal.ElementRoll;var poison=PaladinZeal.PoisonRoll;var effects=PaladinZeal.Effects;
        var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinZeal.HitRoll=()=>0;PaladinZeal.Roll=_=>0;PaladinZeal.ElementRoll=()=>0;PaladinZeal.PoisonRoll=_=>0;PaladinZeal.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
            Lethal(false);Lethal(true);LiveEvents();Attempts();InitialTarget();MixedItems();Drains();Refusals();
            Log.Information("Zeal contact:source/native deferred lethal return;unrolled windup/live per-event synergy and mitigation;once fractional start payment;miss/block flat return;latched initial/cyclic next target;real Sapphire/Wormskull;fractional leech/Life Tap;3start and1late pre-roll refusals");
        }
        finally{PaladinZeal.HitRoll=hit;PaladinZeal.Roll=roll;PaladinZeal.ElementRoll=element;PaladinZeal.PoisonRoll=poison;PaladinZeal.Effects=effects;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new Ogre());var account=FixtureNetwork.Authenticate(network,p,"zeal-input-");
        var hit=PaladinZeal.HitRoll;var roll=PaladinZeal.Roll;var element=PaladinZeal.ElementRoll;var effects=PaladinZeal.Effects;
        int draws=0;PaladinZeal.HitRoll=()=>
        {
            draws++;var state=ClassCombat.For(p);var stored=state.Zeal?.Target??state.PendingTarget;var chosen=PaladinZeal.Target(p,stored);
            Log.Information("{Label}: event{Draw} stored={Stored} chosen={Chosen} original={Original} originalLife={Life} originalValid={Valid} originalPoint={Point}",
                label,draws,stored,chosen?.Serial,t.Serial,SkillHealth.Balance256(t),PaladinVengeance.ValidTarget(p,t.Serial),t.Location);return 0;
        };PaladinZeal.Roll=_=>0;PaladinZeal.ElementRoll=()=>0;PaladinZeal.Effects=new NoProc();
        NecromancerEffects.SetMana256(p,529);int before=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){ClassCombat.Stop(p);PaladinZeal.HitRoll=hit;PaladinZeal.Roll=roll;PaladinZeal.ElementRoll=element;PaladinZeal.Effects=effects;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                var live=ClassCombat.For(p);
                Require(Core.TickCount<deadline,$"Actual Zeal input timeout: pending={live.PendingAbility},remaining={live.Zeal?.Remaining},packets={NativeMonsterReturn.Session(t).Packets},draws={draws},loss={before-SkillHealth.Balance256(t)},mana={NecromancerEffects.ManaBalance256(p)},alive={p.Alive},frozen={p.Frozen},paralyzed={p.Paralyzed},spell={p.Spell},overlap={AlliedOverlap.Allowed(p)},origin={p.Location}/{live.ActionOrigin},effect={live.EffectAt-Core.TickCount},next={live.NextAction-Core.TickCount}");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Zeal&&!began)
                {
                    Require(NecromancerEffects.ManaBalance256(p)==17&&SkillHealth.Balance256(t)==before&&draws==0,"Wire Zeal windup rolled contact or missed once start payment");
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(NativeMonsterReturn.Session(t).Packets<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&draws==2&&before-SkillHealth.Balance256(t)==700&&NecromancerEffects.ManaBalance256(p)==17
                    &&NativeMonsterReturn.Session(t).Packets==2&&ClassCombat.For(p).Zeal.Remaining==0,"Actual Zeal sequence live contacts/fractional once payment");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Zeal,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
