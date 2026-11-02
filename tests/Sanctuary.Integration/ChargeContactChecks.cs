// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ChargeContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ChargeContactChecks));
    private sealed class NoProc:Random{public override int Next(int maxValue)=>maxValue-1;}
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("charge-contact-check","Charge ordered impact and source return",_=>Core.LoopContext.Post(()=>
        {try{Check();Log.Information("charge-contact-check: passed");}catch(Exception e){Log.Error(e,"charge-contact-check: failed");}}));
        foreach(string prefix in new[]{"charge-contact-","charge-ground-"})foreach(string mode in new[]{"left","right","quick"})
        {string label=prefix+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Actual Charge target/ground input",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Charge)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Charge)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Charge),"Charge contact bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}return t;
    }
    private static void Clean(PlayerMobile p,BaseCreature t)
    {PaladinCharge.Cancel(p);SourceKnockbacks.Clear(t);PaladinAuras.Clear(t);MonsterUninterruptibles.Clear(t);t.Corpse?.Delete();p.Corpse?.Delete();t.Delete();p.Delete();}
    private static bool Begin(PlayerMobile p,BaseCreature t,bool ground=false)
    {
        PaladinCharge.Cancel(p);ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        return ClassCombat.Perform(p,CombatAbility.Charge,ground?null:t,ground?new(t.X-1,t.Y,t.Z):t.Location,Core.TickCount);
    }
    private static void Arrive(PlayerMobile p)
    {
        var state=ClassCombat.For(p);for(int i=0;state.Charge is{Phase:PaladinCharge.Phase.Moving};i++)
        {Require(i<30&&ClassCombat.ResolveAction(p,state.Charge.NextFrame),"Charge contact travel failed");}
        Require(state.Charge is{Phase:PaladinCharge.Phase.Impact},"Charge contact did not arrive");
    }
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).Charge.NextFrame);
    private static void Lethal(bool native)
    {
        var p=Owner();var t=Target(p,native?(BaseCreature)new Ogre():new GraveyardZombie());var created=CorpseRecoveryHooks.Created;
        int lifeAtSourceDeath=-1,deaths=0;bool targetAliveAtSourceDeath=false,sourceDeadAtTargetDeath=false;
        try
        {
            Require(Begin(p,t),"Charge lethal start");Arrive(p);SkillHealth.SetBalance256(p,2*256);SkillHealth.SetBalance256(t,256);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            CorpseRecoveryHooks.Created=corpse=>
            {
                if(corpse.Owner==p){lifeAtSourceDeath=SkillHealth.Balance256(t);targetAliveAtSourceDeath=t.Alive&&t.Corpse==null;}
                if(corpse.Owner==t){deaths++;sourceDeadAtTargetDeath=!p.Alive;}created?.Invoke(corpse);
            };
            Require(Finish(p)&&!p.Alive&&!t.Alive&&lifeAtSourceDeath==0&&targetAliveAtSourceDeath&&sourceDeadAtTargetDeath&&deaths==1,
                "Charge must commit target HP before retaliation and defer target death until return");
            Require(ClassLoot.Killer(t)==p&&t.FindDamageEntryFor(p)?.Damager==p&&NativeMonsterReturn.Session(t).Packets==1,"Charge ordered credit/packet consumption");
            if(native){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Unclassified Charge target acquired a source identity");}
        }
        finally{CorpseRecoveryHooks.Created=created;Clean(p,t);}
    }
    private static void Allocation()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());int hits=0,physical=0,elements=0;
        try
        {
            PaladinCharge.HitRoll=()=>{hits++;return 0;};PaladinCharge.Roll=_=>{physical++;return 0;};PaladinCharge.ElementRoll=()=>{elements++;return 0;};
            NecromancerEffects.SetMana256(p,2321);int life=SkillHealth.Balance256(t);Require(Begin(p,t),"Charge allocation start");
            Require(hits==0&&physical==0&&elements==0&&NecromancerEffects.ManaBalance256(p)==17,"Charge allocated before impact or charged wrong mana");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.charge",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.might",out _),"Charge live impact ranks");
            // Base1, Strength25 and impact ED185 add: floor(256*310/100)/2 =396.
            t.SetResistance(ResistanceType.Physical,50);Arrive(p);
            Require(hits==0&&physical==0&&elements==0&&SkillHealth.Balance256(t)==life,"Charge allocated during travel");
            Require(Finish(p)&&life-SkillHealth.Balance256(t)==396&&hits==1&&physical==1&&elements==1&&NecromancerEffects.ManaBalance256(p)==17
                &&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).LastHitClass==0x71
                &&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback,$"Charge live impact allocation: loss={life-SkillHealth.Balance256(t)}, hits={hits}, physical={physical}, elements={elements}, mana={NecromancerEffects.ManaBalance256(p)}, notifications={t.Notifications}, before={t.LifeAtNotification}/{life}, hitclass={NativeMonsterReturn.Session(t).LastHitClass}, mode={NativeMonsterReturn.Session(t).LastMode}");
            Require(!Finish(p)&&NativeMonsterReturn.Session(t).Packets==1,"Charge replayed impact");
        }
        finally{PaladinCharge.HitRoll=()=>0;PaladinCharge.Roll=_=>0;PaladinCharge.ElementRoll=()=>0;Clean(p,t);}
    }
    private static void Attempts()
    {
        foreach(string mode in new[]{"hit","miss","block"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
            try
            {
                SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,CreatureProfiles.Definition(t).Reference,1)).ToArray());
                SourceBlocking.BlockRoll=n=>mode=="block"?0:n-1;PaladinCharge.HitRoll=()=>mode=="miss"?1:0;
                PaladinCharge.Roll=_=>mode=="hit"?0:throw new InvalidOperationException("Charge rolled miss/block weapon damage");
                PaladinCharge.ElementRoll=()=>mode=="hit"?0:throw new InvalidOperationException("Charge rolled miss/block elements");
                int life=SkillHealth.Balance256(t),hp=SkillHealth.Balance256(p);
                PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,4,TimedEffects.Now.AddDays(1)));
                Require(Begin(p,t),"Charge attempt start "+mode);Arrive(p);
                Require(Finish(p)&&life-SkillHealth.Balance256(t)==(mode=="hit"?678:0)&&hp-SkillHealth.Balance256(p)==4*256
                    &&t.Notifications==(mode=="hit"?1:0)&&NativeMonsterReturn.Session(t).Packets==1
                    &&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback,"Charge hit/miss/block damage/flat return/forced knockback "+mode);
                var state=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);
                Require(state.PauseFrozen&&!state.Recovering,"Charge did not use prepared knockback");
                SourceKnockbacks.Tick(state.NextUtc);Require(SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value).Travel16==0,"Frozen Charge recipient travelled");
                t.Frozen=false;for(int i=0;i<30;i++)
                {state=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);if(state.Recovering){break;}SourceKnockbacks.Tick(state.NextUtc);}
                state=SourceKnockbacks.Snapshot().Single(x=>x.Target==t.Serial.Value);
                Require(state.Recovering&&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.GetHit&&NativeMonsterReturn.Session(t).LastHitClass==160,"Charge knockback did not finish in get-hit "+mode);
            }
            finally{PaladinCharge.HitRoll=()=>0;PaladinCharge.Roll=_=>0;PaladinCharge.ElementRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;Clean(p,t);}
        }
    }
    private static void MixedItems()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            var dagger=new Dagger();ClassGear.Profiles.Convert(dagger,99,GearRarity.Normal,new Random(173));p.AddToBackpack(dagger);ClassGear.Profiles.Store(dagger,ClassGear.State(dagger) with{TinkerSockets=1});
            GemActivationChecks.Install(p,GemActivationChecks.Gem(p,"Sapphire"),dagger);Require(p.EquipItem(dagger),"Charge actual Sapphire weapon");PoisonDaggerChecks.Named(p,"Wormskull");ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Cold,50);Require(Begin(p,t),"Charge mixed item start");Arrive(p);int life=SkillHealth.Balance256(t);
            Require(Finish(p)&&life>SkillHealth.Balance256(t),"Charge mixed item impact");var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);
            Require(state.PoisonClock?.Rate256==102&&state.PoisonMs==8000&&state.ChillPercent>0&&(state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds is >1380 and <=1480,
                "Charge lost ordinary weapon poison/cold");
            Require(NativeMonsterReturn.Session(t).LastHitClass==0x76,"Charge dagger hit class differs from source0x70 plus weapon");
            life=SkillHealth.Balance256(t);TimedEffects.Settle(t,state.UpdatedUtc.AddMilliseconds(40));Require(life-SkillHealth.Balance256(t)==102,"Charge source poison tick differs");
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
                while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Charge leech Strength");}
                PoisonDaggerChecks.Named(p,"Greyform");Require(ClassGear.Bonus(p,"life-steal%")==5,"Charge actual Greyform leech");
                if(tap){caster=ClassSkillChecks.Player();caster.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);Require(NecromancerEffects.Apply(caster,t,CombatAbility.LifeTap,1),"Charge Life Tap");}
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p);
                Require(Begin(p,t),"Charge drain start");Arrive(p);
                Require(Finish(p)&&SkillHealth.Balance256(p)==hp+870*5/100+(tap?870/2:0),"Charge fractional leech/Life Tap missing or duplicated");
            }
            finally{caster?.Delete();Clean(p,t);}
        }
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen","mana","late-paragon"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
            try
            {
                if(mode=="paragon"){t.IsParagon=true;}
                if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                if(mode=="mana"){NecromancerEffects.SetMana256(p,2303);}
                int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
                PaladinCharge.HitRoll=()=>throw new InvalidOperationException("Unsupported Charge rolled hit");
                if(mode=="late-paragon")
                {Require(Begin(p,t),"Charge late refusal start");Arrive(p);t.IsParagon=true;life=SkillHealth.Balance256(t);Require(!Finish(p)&&NecromancerEffects.ManaBalance256(p)==mana-2304,"Charge late refusal did not retain paid start");}
                else{Require(!Begin(p,t)&&NecromancerEffects.ManaBalance256(p)==mana,"Unsupported/unpaid Charge paid or started "+mode);}
                Require(SkillHealth.Balance256(t)==life&&NativeMonsterReturn.Session(t).Packets==0,"Unsupported Charge mutated target "+mode);
            }
            finally{PaladinCharge.HitRoll=()=>0;Clean(p,t);}
        }
    }
    private static void Check()
    {
        var hit=PaladinCharge.HitRoll;var roll=PaladinCharge.Roll;var element=PaladinCharge.ElementRoll;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();
        try
        {
            PaladinCharge.HitRoll=()=>0;PaladinCharge.Roll=_=>0;PaladinCharge.ElementRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
            // First regression also runs against the parent content DLL.
            Lethal(false);Lethal(true);Details();
            Log.Information("Charge contact:ordered mapped/native lethal return/credit;396FP live allocation;hit/miss/block forced knockback and frozen pause/get-hit;real Sapphire/Wormskull;870FP leech/Life Tap;5pre-roll refusals");
        }
        finally{PaladinCharge.HitRoll=hit;PaladinCharge.Roll=roll;PaladinCharge.ElementRoll=element;SourceBlocking.BlockRoll=block;SourceBlocking.Restore(shields);}
    }
    private static void Details()
    {
        var poison=PaladinCharge.PoisonRoll;var effects=PaladinCharge.Effects;
        try{PaladinCharge.PoisonRoll=_=>0;PaladinCharge.Effects=new NoProc();Allocation();Attempts();MixedItems();Drains();Refusals();}
        finally{PaladinCharge.PoisonRoll=poison;PaladinCharge.Effects=effects;}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());TimedEffects.PreventHealing(t,TimedEffects.Now);var account=FixtureNetwork.Authenticate(network,p,"charge-input-");bool ground=label.StartsWith("charge-ground-",StringComparison.Ordinal);
        var hit=PaladinCharge.HitRoll;var roll=PaladinCharge.Roll;var element=PaladinCharge.ElementRoll;var effects=PaladinCharge.Effects;var block=SourceBlocking.BlockRoll;var shields=SourceBlocking.Snapshot();int hits=0;
        PaladinCharge.HitRoll=()=>{hits++;return 0;};PaladinCharge.Roll=_=>0;PaladinCharge.ElementRoll=()=>0;PaladinCharge.Effects=new NoProc();SourceBlocking.BlockRoll=n=>n-1;
        NecromancerEffects.SetMana256(p,2321);int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+5000;
        void Cleanup(){PaladinCharge.HitRoll=hit;PaladinCharge.Roll=roll;PaladinCharge.ElementRoll=element;PaladinCharge.Effects=effects;SourceBlocking.BlockRoll=block;Clean(p,t);SourceBlocking.Restore(shields);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Charge input timeout");
                if(ClassCombat.For(p).Charge is{Phase:PaladinCharge.Phase.Moving}&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==17&&SkillHealth.Balance256(t)==life&&hits==0,"Wire Charge did not pay once before travel");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&hits==1&&NecromancerEffects.ManaBalance256(p)==17&&life-SkillHealth.Balance256(t)==678
                    &&t.Notifications==1&&NativeMonsterReturn.Session(t).Packets==1&&NativeMonsterReturn.Session(t).LastHitClass==0x71
                    &&NativeMonsterReturn.Session(t).LastMode==PreparedMonsterReturn.Knockback,"Actual Charge payment/ordered impact/knockback failed");
                Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>
        {
            if(ground){Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.Charge,(int)DiabloClass.Paladin,t.X-1,t.Y,t.Z);}
            else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Charge,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);}
            Observe();
        });
    }
}
