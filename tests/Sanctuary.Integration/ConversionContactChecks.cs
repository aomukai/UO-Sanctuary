// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ConversionContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ConversionContactChecks));
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
        ConsoleInputHandler.RegisterCommand("conversion-contact-check","Conversion failed-chance ordered contact",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("conversion-contact-check: passed");}catch(Exception e){Log.Error(e,"conversion-contact-check: failed");}}));
        foreach(string prefix in new[]{"conversion-contact-","conversion-allegiance-"})foreach(string mode in new[]{"left","right","quick"})
        {string label=prefix+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Conversion success bypass/failed contact",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Conversion)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Conversion)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Conversion),"Conversion contact bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinAuras.Clear(t);MonsterUninterruptibles.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();PaladinConversion.Prune();}
    private static bool Begin(PlayerMobile p,Mobile t)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.Conversion,t,t.Location,Core.TickCount);}
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void Lethal()
    {
        var p=Owner();var t=Target(p,new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int lifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Conversion lethal start");SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1,
                "Failed Conversion must commit target HP before retaliation and defer target death until return");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,"Failed Conversion ordered credit/packet consumption");
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void Snapshot()
    {
        var p=Owner();var t=Target(p,new Recipient());int hits=0,physical=0,elements=0,chances=0;
        try
        {
            PaladinConversion.HitRoll=()=>{hits++;return 0;};PaladinConversion.PhysicalRoll=_=>{physical++;return 0;};PaladinConversion.ElementRoll=()=>{elements++;return 0;};
            PaladinConversion.ChanceRoll=_=>{chances++;return 99;};NecromancerEffects.SetMana256(p,1041);
            int life=SkillHealth.Balance256(t);Require(Begin(p,t),"Conversion snapshot start");var saved=ClassCombat.For(p).Conversion.Contact;
            Require(hits==1&&physical==1&&elements==1&&chances==0&&SkillHealth.Balance256(t)==life&&NecromancerEffects.ManaBalance256(p)==17
                &&NamedElements.To256(saved.Impact.Parts.Physical)==353&&saved.Impact.LifeDamage256==353,"Conversion start32 snapshot/golden/payment/chance order");
            t.SetResistance(ResistanceType.Physical,100);Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.conversion",out _),"Conversion rank changed during windup");
            PaladinConversion.HitRoll=()=>throw new InvalidOperationException("Conversion rerolled hit at release");PaladinConversion.PhysicalRoll=_=>throw new InvalidOperationException("Conversion rerolled weapon at release");
            PaladinConversion.ElementRoll=()=>throw new InvalidOperationException("Conversion rerolled items at release");
            Require(Finish(p)&&life-SkillHealth.Balance256(t)==353&&t.Notifications==1&&t.LifeAtNotification==life&&chances==1
                &&NecromancerEffects.ManaBalance256(p)==17&&NativeMonsterReturn.Session(t).Packets==1&&!Finish(p),"Conversion re-mitigated/rerolled/repaid or lost ordered credit");
        }
        finally{PaladinConversion.HitRoll=()=>0;PaladinConversion.PhysicalRoll=_=>0;PaladinConversion.ElementRoll=()=>0;PaladinConversion.ChanceRoll=_=>99;Clean(p,t);}
    }
    private static void SuccessfulBypass()
    {
        foreach(string mode in new[]{"hit","miss","block"})
        {
            var p=Owner();var t=Target(p,new Recipient());BaseWeapon weapon=null;
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                SourceBlocking.BlockRoll=n=>mode=="block"?0:n-1;PaladinConversion.HitRoll=()=>mode=="miss"?1:0;PaladinConversion.ChanceRoll=_=>0;
                if(mode=="hit")
                {
                    var dagger=new Dagger();weapon=dagger;ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);
                    ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);
                    Require(p.EquipItem(dagger),"Conversion success actual weapon");PoisonDaggerChecks.Named(p,"Wormskull");PoisonDaggerChecks.Named(p,"Greyform");ClassGear.Refresh(p);
                }
                SkillHealth.SetBalance256(t,256);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-10*256-17);int hp=SkillHealth.Balance256(p),life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),wear=weapon?.HitPoints??0;
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));
                Require(PaladinThorns.Active(t),"Conversion aura fixture missing Thorns");
                Require(Begin(p,t),"Successful Conversion start "+mode);var packet=ClassCombat.For(p).Conversion.Contact;
                Require(mode=="hit"?(packet.Flags&1)!=0:mode=="block"?(packet.Flags&16)!=0:(packet.Flags&1)==0,"Conversion result setup "+mode);
                Require(Finish(p)&&PaladinConversion.Active(t)&&ClassCombat.Friendly(p,t)&&SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(p)==hp
                    &&t.Notifications==0&&NativeMonsterReturn.Session(t).Packets==0&&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value)
                    &&(weapon?.HitPoints??0)==wear&&NecromancerEffects.ManaBalance256(p)==mana-1024&&!Finish(p),"Successful Conversion delivered damage/status/leech/return/wear or required a hit "+mode);
                Require(!PaladinThorns.Active(t),"Successful Conversion retained an aura state");
            }
            finally{SourceBlocking.BlockRoll=n=>n-1;PaladinConversion.HitRoll=()=>0;PaladinConversion.ChanceRoll=_=>99;Clean(p,t);}
        }
    }
    private static void AuraBoundary()
    {
        var p=Owner();var t=Target(p,new Recipient());var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);var sorc=ClassSkillChecks.Player(DiabloClass.Sorceress);
        try
        {
            supplier.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);sorc.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
            SorceressEffects.CastEnchant(sorc,t,SkillMechanics.Values(sorc,CombatAbility.Enchant));var enchant=SorceressEffects.Enchantment(t);
            BattleOrders.Apply(supplier,t,1,TimedEffects.Now);BarbarianShout.Apply(supplier,t,1,TimedEffects.Now);BattleCommand.Apply(supplier,t,1,TimedEffects.Now);
            Require(BattleOrders.Percent(t)>0&&BarbarianShout.State(t)!=null&&BattleCommand.State(t)!=null&&enchant!=null,"Conversion aura/non-aura suppliers");
            PaladinConversion.ChanceRoll=_=>0;
            Require(Begin(p,t)&&Finish(p)&&PaladinConversion.Active(t)&&BattleOrders.Percent(t)==0&&BarbarianShout.State(t)==null
                &&BattleCommand.State(t)==null&&SorceressEffects.Enchantment(t)==enchant,"Conversion onset retained aura states or removed ordinary Enchant");
        }
        finally{PaladinConversion.ChanceRoll=_=>99;supplier.Delete();sorc.Delete();Clean(p,t);}
    }
    private static void FailedAttempts()
    {
        foreach(bool block in new[]{false,true})
        {
            var p=Owner();var t=Target(p,new Recipient());
            try
            {
                string source=CreatureProfiles.Definition(t).Reference;
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,source,1)).ToArray());
                SourceBlocking.BlockRoll=n=>block?0:n-1;PaladinConversion.HitRoll=()=>block?0:1;
                PaladinConversion.PhysicalRoll=_=>throw new InvalidOperationException("Conversion rolled weapon damage on miss/block");
                PaladinConversion.ElementRoll=()=>throw new InvalidOperationException("Conversion rolled item elements on miss/block");
                int life=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));
                Require(Begin(p,t)&&Finish(p)&&!PaladinConversion.Active(t)&&SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(p)==hp-4*256
                    &&t.Notifications==0&&NativeMonsterReturn.Session(t).Packets==1&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Failed Conversion miss/block lost attempt/flat return/payment");
            }
            finally{SourceBlocking.BlockRoll=n=>n-1;PaladinConversion.HitRoll=()=>0;PaladinConversion.PhysicalRoll=_=>0;PaladinConversion.ElementRoll=()=>0;Clean(p,t);}
        }
    }
    private static void Cancellation()
    {
        foreach(string mode in new[]{"movement","weapon","failed-equip","stop","range","held-target","late-paragon"})
        {
            var p=Owner();var t=Target(p,new Recipient());BaseCreature other=null;Item weapon=null;
            try
            {
                int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);Require(Begin(p,t),"Conversion cancellation start "+mode);
                if(mode=="movement"){var at=p.Location;p.Location=new(p.X-1,p.Y,p.Z);p.Location=at;}
                if(mode=="weapon")
                {var dagger=new Dagger();weapon=dagger;ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);Require(p.EquipItem(dagger),"Conversion cancellation weapon");p.AddToBackpack(dagger);}
                if(mode=="failed-equip")
                {var sword=new Longsword();weapon=sword;ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));p.AddToBackpack(sword);Require(!p.EquipItem(sword),"Conversion failed-equip fixture unexpectedly met requirements");}
                if(mode=="stop"){ClassCombat.Stop(p);}
                if(mode=="range"){t.Location=new(t.X+10,t.Y,t.Z);}
                if(mode=="late-paragon"){t.IsParagon=true;life=SkillHealth.Balance256(t);}
                if(mode is "movement" or "weapon" or "stop"){Require(ClassCombat.For(p).Conversion==null,"Conversion did not immediately cancel: "+mode);}
                PaladinConversion.ChanceRoll=_=>throw new InvalidOperationException("Cancelled Conversion rolled chance: "+mode);
                if(mode is "held-target" or "failed-equip")
                {
                    PaladinConversion.ChanceRoll=_=>99;int otherLife=0;
                    if(mode=="held-target")
                    {other=Target(p,new GraveyardZombie());otherLife=SkillHealth.Balance256(other);Require(ClassCombat.ContinueCast(p,CombatAbility.Conversion,other,other.Location,Core.TickCount),"Conversion held update");ClassCombat.ReleaseCast(p);}
                    Require(ClassCombat.For(p).Conversion!=null&&Finish(p)&&SkillHealth.Balance256(t)<life&&(other==null||SkillHealth.Balance256(other)==otherLife),"Conversion target changed or rejected equip cancelled its prepared action");
                }
                else{Require(!Finish(p)&&SkillHealth.Balance256(t)==life&&NativeMonsterReturn.Session(t).Packets==0,"Cancelled Conversion delivered a packet "+mode);}
                Require(ClassCombat.For(p).Conversion==null&&NecromancerEffects.ManaBalance256(p)==mana-1024&&!Finish(p),"Conversion cancelled packet replay/refund/payment "+mode);
            }
            finally{PaladinConversion.ChanceRoll=_=>99;weapon?.Delete();other?.Delete();Clean(p,t);}
        }
    }
    private static void PredecessorEquipment()
    {
        foreach(var ability in new[]{CombatAbility.Sacrifice,CombatAbility.Vengeance})
        {
            var p=Owner();var t=Target(p,new Recipient());var dagger=new Dagger();
            try
            {
                int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
                Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Predecessor equipment start "+ability);
                ClassGear.Profiles.Convert(dagger,1,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);Require(p.EquipItem(dagger),"Predecessor equipment commit");p.AddToBackpack(dagger);
                var action=ClassCombat.For(p);
                Require(action.PendingAbility==CombatAbility.None&&action.SacrificeAttack==null&&action.VengeanceAttack==null
                    &&!ClassCombat.ResolveAction(p,action.EffectAt)&&SkillHealth.Balance256(t)==life&&NecromancerEffects.ManaBalance256(p)==mana,
                    "Committed weapon round trip retained predecessor attack "+ability);
            }
            finally{dagger.Delete();Clean(p,t);}
        }
    }
    private static void MixedItems()
    {
        var p=Owner();var t=Target(p,new Recipient());
        try
        {
            var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
            GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Conversion Sapphire dagger");PoisonDaggerChecks.Named(p,"Wormskull");ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Cold,50);int life=SkillHealth.Balance256(t);Require(Begin(p,t),"Conversion item snapshot");var saved=ClassCombat.For(p).Conversion.Contact;
            Require(saved.ColdFrames==37&&saved.Impact.AllocatedPoison==new PreparedPoison(102,200),"Conversion source cold/poison capture");
            t.SetResistance(ResistanceType.Cold,0);Require(Finish(p)&&life-SkillHealth.Balance256(t)==saved.Impact.LifeDamage256,"Conversion repeated item mitigation");
            var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
            Require(state.PoisonClock?.Rate256==102&&state.PoisonMs==8000&&state.ChillPercent>0&&(state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1380 and <=1480,
                "Conversion changed captured item status");
            life=SkillHealth.Balance256(t);TimedEffects.Settle(t,state.UpdatedUtc.AddMilliseconds(40));Require(life-SkillHealth.Balance256(t)==102,"Conversion source poison tick differs");
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
                while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Conversion leech Strength");}
                PoisonDaggerChecks.Named(p,"Greyform");Require(ClassGear.Bonus(p,"life-steal%")==5,"Conversion actual Greyform leech");
                if(tap){caster=ClassSkillChecks.Player();caster.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);Require(NecromancerEffects.Apply(caster,t,CombatAbility.LifeTap,1),"Conversion actual Life Tap curse");}
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p);
                Require(Begin(p,t),"Conversion leech capture");int physical=NamedElements.To256(ClassCombat.For(p).Conversion.Contact.Impact.Parts.Physical);
                Require(physical==545&&Finish(p)&&SkillHealth.Balance256(p)==hp+545*5/100+(tap?545/2:0),"Conversion fractional physical leech/Life Tap differed or ran twice");
            }
            finally{caster?.Delete();Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen","unmapped","mana"})
        {
            var p=Owner();var t=Target(p,mode=="unmapped"?(BaseCreature)new Ogre():new Recipient());
            try
            {
                if(mode=="paragon"){t.IsParagon=true;}
                if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                if(mode=="mana"){NecromancerEffects.SetMana256(p,1023);}
                int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
                PaladinConversion.HitRoll=()=>throw new InvalidOperationException("Unsupported/unpaid Conversion rolled hit");PaladinConversion.ChanceRoll=_=>throw new InvalidOperationException("Unsupported/unpaid Conversion rolled chance");
                Require(!Begin(p,t)&&ClassCombat.For(p).Conversion==null&&SkillHealth.Balance256(t)==life&&NecromancerEffects.ManaBalance256(p)==mana,
                    "Unsupported/unpaid Conversion rolled/paid/mutated "+mode);
            }
            finally{PaladinConversion.HitRoll=()=>0;PaladinConversion.ChanceRoll=_=>99;Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PaladinConversion.HitRoll;var roll=PaladinConversion.PhysicalRoll;var element=PaladinConversion.ElementRoll;var chance=PaladinConversion.ChanceRoll;var poison=PaladinConversion.PoisonRoll;var effects=PaladinConversion.Effects;
        var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinConversion.HitRoll=()=>0;PaladinConversion.PhysicalRoll=_=>0;PaladinConversion.ElementRoll=()=>0;PaladinConversion.ChanceRoll=_=>99;
            PaladinConversion.PoisonRoll=_=>0;PaladinConversion.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
            Lethal();Snapshot();SuccessfulBypass();AuraBoundary();FailedAttempts();Cancellation();PredecessorEquipment();MixedItems();Drains();Refusals();
            Log.Information("Conversion contact:ordered failed-chance lethal return/credit;353FP windup snapshot;hit/miss/block successful bypass;aura removal/Enchant preservation;failed miss/block flat return;7cancellation/target cases;2predecessor weapon round trips;real Sapphire/Wormskull;545FP fractional leech/Life Tap;5pre-roll refusals");
        }
        finally{PaladinConversion.HitRoll=hit;PaladinConversion.PhysicalRoll=roll;PaladinConversion.ElementRoll=element;PaladinConversion.ChanceRoll=chance;PaladinConversion.PoisonRoll=poison;PaladinConversion.Effects=effects;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new Recipient());TimedEffects.PreventHealing(t,TimedEffects.Now);var account=FixtureNetwork.Authenticate(network,p,"conversion-input-");bool success=label.StartsWith("conversion-allegiance-",StringComparison.Ordinal);
        var hit=PaladinConversion.HitRoll;var roll=PaladinConversion.PhysicalRoll;var element=PaladinConversion.ElementRoll;var chance=PaladinConversion.ChanceRoll;var effects=PaladinConversion.Effects;
        var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();int hits=0,chances=0;
        PaladinConversion.HitRoll=()=>{hits++;return 0;};PaladinConversion.PhysicalRoll=_=>0;PaladinConversion.ElementRoll=()=>0;PaladinConversion.ChanceRoll=_=>{chances++;return success?0:99;};PaladinConversion.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
        NecromancerEffects.SetMana256(p,1041);int life=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){ClassCombat.Stop(p);PaladinConversion.HitRoll=hit;PaladinConversion.PhysicalRoll=roll;PaladinConversion.ElementRoll=element;PaladinConversion.ChanceRoll=chance;PaladinConversion.Effects=effects;SourceBlocking.BlockRoll=block;Clean(p,t);SourceBlocking.Restore(shields);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Conversion input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.Conversion&&!began)
                {
                    var saved=ClassCombat.For(p).Conversion;Require(saved!=null&&saved.Contact.Impact.LifeDamage256==353&&NecromancerEffects.ManaBalance256(p)==17
                        &&SkillHealth.Balance256(t)==life&&hits==1&&chances==0,"Wire Conversion missing paid windup snapshot");began=true;Log.Information("{Label}: windup",label);
                }
                if(success?!PaladinConversion.Active(t):NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&hits==1&&chances==1&&NecromancerEffects.ManaBalance256(p)==17&&ClassCombat.For(p).Conversion==null
                    &&(success?SkillHealth.Balance256(t)==life&&SkillHealth.Balance256(p)==hp&&t.Notifications==0&&NativeMonsterReturn.Session(t).Packets==0&&ClassCombat.Friendly(p,t)
                        :life-SkillHealth.Balance256(t)==353&&t.Notifications==1&&NativeMonsterReturn.Session(t).Packets==1),$"Actual Conversion result: success={success}, began={began}, hits={hits}, chances={chances}, mana={NecromancerEffects.ManaBalance256(p)}, pending={ClassCombat.For(p).Conversion!=null}, loss={life-SkillHealth.Balance256(t)}, sourceHP={hp}/{SkillHealth.Balance256(p)}, notices={t.Notifications}, packets={NativeMonsterReturn.Session(t).Packets}, friendly={ClassCombat.Friendly(p,t)}, eligible={PaladinConversion.Eligible(p,t)}, levels={ClassStats.Level(p)}/{ClassStats.Level(t)}");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Conversion,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
