// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class VengeanceContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(VengeanceContactChecks));
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
        ConsoleInputHandler.RegisterCommand("vengeance-contact-check","Vengeance start snapshot and ordered elemental weapon contact",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("vengeance-contact-check: passed");}catch(Exception e){Log.Error(e,"vengeance-contact-check: failed");}}));
        foreach(string mode in new[]{"left","right","quick"})
        {string label="vengeance-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Vengeance snapshot and release payment",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Vengeance)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Vengeance)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Vengeance),"Vengeance contact bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinAuras.Clear(t);MonsterUninterruptibles.Clear(t);CombatStuns.Clear(t);SourceKnockbacks.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static bool Begin(PlayerMobile p,Mobile t)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.Vengeance,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Snapshot()
    {
        var p=Owner();var t=Target(p,new Ogre());var hit=PaladinVengeance.HitRoll;var element=PaladinVengeance.Roll;
        var weapon=PaladinVengeance.WeaponRoll;var item=PaladinVengeance.PhysicalRoll;string draws="";
        try
        {
            int hits=0,elements=0;PaladinVengeance.HitRoll=()=>{hits++;draws+="hit;";return 0;};PaladinVengeance.Roll=n=>{elements++;draws+="element;";return 0;};
            PaladinVengeance.WeaponRoll=n=>{draws+="physical;";return 0;};PaladinVengeance.PhysicalRoll=()=>{draws+="item;";return 0;};
            int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
            Require(Begin(p,t),"Vengeance snapshot start");
            Require(hits==1&&elements==1&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana,
                "Vengeance must allocate hit and elemental roll at windup without committing resources or HP");
            var prepared=ClassCombat.For(p).VengeanceAttack.Contact;var parts=prepared.Impact.Parts;
            Require(draws=="hit;physical;element;item;"&&NamedElements.To256(parts.Physical)==320&&NamedElements.To256(parts.Fire)==209
                &&NamedElements.To256(parts.Cold)==209&&NamedElements.To256(parts.Lightning)==209&&prepared.Impact.LifeDamage256==947,
                "Vengeance independent source rolls or ungeared four-lane golden differ");
            foreach(var resistance in Enum.GetValues<ResistanceType>()){t.SetResistance(resistance,100);}
            PaladinVengeance.HitRoll=()=>throw new InvalidOperationException("Vengeance rerolled hit at release");
            PaladinVengeance.Roll=_=>throw new InvalidOperationException("Vengeance rerolled elements at release");
            PaladinVengeance.WeaponRoll=_=>throw new InvalidOperationException("Vengeance rerolled physical at release");PaladinVengeance.PhysicalRoll=()=>throw new InvalidOperationException("Vengeance recollected items at release");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.resistfire",out _),"Vengeance changed hard synergy during windup");
            Require(Finish(p)&&SkillHealth.Balance256(t)<before&&NecromancerEffects.ManaBalance256(p)==mana-1024&&!Finish(p),
                "Vengeance discarded or re-mitigated its start packet/repeated payment");
        }
        finally{PaladinVengeance.HitRoll=hit;PaladinVengeance.Roll=element;PaladinVengeance.WeaponRoll=weapon;PaladinVengeance.PhysicalRoll=item;Clean(p,t);}
    }
    private static void CyclesAndMisses()
    {
        var p=Owner();var t=Target(p,new Recipient());
        try
        {
            string source=CreatureProfiles.Definition(t).Reference;
            SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
            foreach(var pair in new[]{("hit",32),("miss",0),("block",0),("hit",48),("cancel",64),("hit",32)})
            {
                PaladinVengeance.HitRoll=()=>pair.Item1=="miss"?1:0;SourceBlocking.BlockRoll=n=>pair.Item1=="block"?0:n-1;
                int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),notifications=t.Notifications;
                PaladinAuras.Clear(t);
                if(pair.Item1 is "miss" or "block"){PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));}
                Require(Begin(p,t)&&ClassCombat.For(p).VengeanceAttack.Contact.HitClass==pair.Item2,"Vengeance successful-allocation hit-class cycle advanced on miss/block");
                if(pair.Item1=="cancel")
                {ClassCombat.Stop(p);Require(!Finish(p)&&SkillHealth.Balance256(t)==life&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled Vengeance dealt damage/paid");continue;}
                Require(Finish(p)&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Vengeance miss/block release payment");
                if(pair.Item2==0)
                {Require(SkillHealth.Balance256(t)==life&&t.Notifications==notifications&&SkillHealth.Balance256(p)==hp-4*256,"Vengeance miss/block dealt damage or lost flat retaliation");}
                else
                {Require(t.Notifications==notifications+1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).LastHitClass==(pair.Item2|1),"Vengeance notification or source hit class changed at return");}
            }
        }
        finally{Clean(p,t);PaladinVengeance.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;}
    }
    private static void Cancellation()
    {
        foreach(string mode in new[]{"movement","weapon","stop","range","mana","held-target"})
        {
            var p=Owner();var t=Target(p,new Recipient());BaseCreature other=null;Item weapon=null;
            try
            {
                int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);Require(Begin(p,t),"Vengeance cancellation start "+mode);
                if(mode=="movement"){var where=p.Location;p.Location=new(p.X-1,p.Y,p.Z);p.Location=where;}
                if(mode=="weapon"){var dagger=new Dagger();weapon=dagger;ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);Require(p.EquipItem(dagger),"Vengeance cancellation weapon");}
                if(mode=="stop"){ClassCombat.Stop(p);}
                if(mode=="range"){t.Location=new(t.X+10,t.Y,t.Z);}
                if(mode=="mana"){NecromancerEffects.SetMana256(p,1023);mana=1023;}
                if(mode=="held-target")
                {
                    other=Target(p,new Ogre());int life=SkillHealth.Balance256(other);
                    Require(ClassCombat.ContinueCast(p,CombatAbility.Vengeance,other,other.Location,Core.TickCount),"Vengeance held cursor update");ClassCombat.ReleaseCast(p);
                    Require(Finish(p)&&SkillHealth.Balance256(other)==life&&SkillHealth.Balance256(t)<before,"Vengeance held input replaced the allocated target");
                }
                else{Require(!Finish(p)&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana,"Vengeance cancellation spent/replayed "+mode);}
                Require(!Finish(p),"Vengeance consumed/cancelled action replayed "+mode);
            }
            finally{weapon?.Delete();other?.Delete();Clean(p,t);}
        }
    }
    private static void Lethal(bool native)
    {
        var p=Owner();var t=Target(p,native?(BaseCreature)new Ogre():new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int lifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Vengeance lethal snapshot");SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1
                &&ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,"Vengeance ordered HP/retaliation/deferred death/credit");
            if(native){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Vengeance invented a native target's D2 identity");}
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void MixedItems()
    {
        var p=Owner();var t=Target(p,new Recipient());
        try
        {
            var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
            GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Vengeance Sapphire dagger");PoisonDaggerChecks.Named(p,"Wormskull");ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Cold,50);int life=SkillHealth.Balance256(t);Require(Begin(p,t),"Vengeance item packet start");var saved=ClassCombat.For(p).VengeanceAttack.Contact;
            Require(saved.ColdFrames==52&&saved.Impact.AllocatedPoison==new PreparedPoison(102,200),"Vengeance combined skill/item cold or source poison capture");
            t.SetResistance(ResistanceType.Cold,0);Require(Finish(p)&&life-SkillHealth.Balance256(t)==saved.Impact.LifeDamage256,"Vengeance item packet mitigation repeated");
            var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
            Require(state.PoisonClock?.Rate256==102&&state.PoisonMs==8000&&state.ChillPercent>0&&(state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1800 and <=2080,
                "Vengeance captured poison/cold state missing or recomputed at release");
            life=SkillHealth.Balance256(t);TimedEffects.Settle(t,state.UpdatedUtc.AddMilliseconds(40));Require(life-SkillHealth.Balance256(t)==102,"Vengeance captured poison tick differs");
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
                while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Vengeance leech Strength");}
                PoisonDaggerChecks.Named(p,"Greyform");Require(ClassGear.Bonus(p,"life-steal%")==5,"Actual Greyform leech");
                if(tap){caster=ClassSkillChecks.Player();caster.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);Require(NecromancerEffects.Apply(caster,t,CombatAbility.LifeTap,1),"Vengeance actual Life Tap curse");}
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p);Require(Begin(p,t),"Vengeance leech capture");var impact=ClassCombat.For(p).VengeanceAttack.Contact.Impact;
                int physical=NamedElements.To256(impact.Parts.Physical),expected=physical*5/100+(tap?physical/2:0);
                Require(expected>0&&Finish(p)&&SkillHealth.Balance256(p)==hp+expected,"Vengeance leech/Life Tap used elements or healed twice");
            }
            finally{caster?.Delete();Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen"})
        {
            var p=Owner();var t=Target(p,new Recipient());
            try
            {
                if(mode=="paragon"){t.IsParagon=true;}
                if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);PaladinVengeance.HitRoll=()=>throw new InvalidOperationException("Unsupported Vengeance target rolled hit");
                Require(!Begin(p,t)&&ClassCombat.For(p).VengeanceAttack==null&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&t.Notifications==0,"Unsupported Vengeance target rolled/paid/mutated "+mode);
            }
            finally{Clean(p,t);}
        }
        PaladinVengeance.HitRoll=()=>0;
    }
    private static void Check()
    {
        var hit=PaladinVengeance.HitRoll;var element=PaladinVengeance.Roll;var physical=PaladinVengeance.WeaponRoll;var item=PaladinVengeance.PhysicalRoll;var poison=PaladinVengeance.PoisonRoll;var effects=PaladinVengeance.Effects;
        var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinVengeance.HitRoll=()=>0;PaladinVengeance.Roll=_=>0;PaladinVengeance.WeaponRoll=_=>0;PaladinVengeance.PhysicalRoll=()=>0;PaladinVengeance.PoisonRoll=_=>0;PaladinVengeance.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
            Snapshot();CyclesAndMisses();Cancellation();Lethal(false);Lethal(true);MixedItems();Drains();Refusals();
            Log.Information("Vengeance contact:hit/physical/element/item windup order and four-lane golden;cached mitigation/hard synergy;successful32/48/64cycle;miss/block flat return;6cancellation/target cases;source/native deferred death and credit;real Sapphire/Wormskull;physical-only leech/Life Tap;3pre-roll refusals");
        }
        finally{PaladinVengeance.HitRoll=hit;PaladinVengeance.Roll=element;PaladinVengeance.WeaponRoll=physical;PaladinVengeance.PhysicalRoll=item;PaladinVengeance.PoisonRoll=poison;PaladinVengeance.Effects=effects;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new Ogre());var account=FixtureNetwork.Authenticate(network,p,"vengeance-input-");ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        var hit=PaladinVengeance.HitRoll;var element=PaladinVengeance.Roll;var weapon=PaladinVengeance.WeaponRoll;var item=PaladinVengeance.PhysicalRoll;var effects=PaladinVengeance.Effects;
        PaladinVengeance.HitRoll=()=>0;PaladinVengeance.Roll=_=>0;PaladinVengeance.WeaponRoll=_=>0;PaladinVengeance.PhysicalRoll=()=>0;PaladinVengeance.Effects=new NoProc();
        NecromancerEffects.SetMana256(p,1024+17);int before=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+5000;int allocated=0;
        void Cleanup(){ClassCombat.Stop(p);PaladinVengeance.HitRoll=hit;PaladinVengeance.Roll=element;PaladinVengeance.WeaponRoll=weapon;PaladinVengeance.PhysicalRoll=item;PaladinVengeance.Effects=effects;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Vengeance input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Vengeance&&!began)
                {
                    var saved=ClassCombat.For(p).VengeanceAttack;Require(saved!=null&&NecromancerEffects.ManaBalance256(p)==1041&&SkillHealth.Balance256(t)==before,"Wire Vengeance missing unpaid snapshot");
                    allocated=saved.Contact.Impact.LifeDamage256.Value;began=true;Log.Information("{Label}: windup",label);
                }
                if(SkillHealth.Balance256(t)==before){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&before-SkillHealth.Balance256(t)==allocated&&NecromancerEffects.ManaBalance256(p)==17&&NativeMonsterReturn.Session(t).Packets==1,"Actual Vengeance snapshot/damage/once fractional payment");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Vengeance,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
