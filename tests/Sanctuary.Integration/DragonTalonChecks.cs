// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DragonTalonChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonTalonChecks));
    private const CombatAbility Ability=CombatAbility.DragonTalon;
    private sealed record Saved(uint Owner,uint Target,uint Helm,uint Boots,Point3D Origin,int Mana,int Health,MartialCharge Charge,uint PendingOwner,uint PendingTarget,int PendingMana,int PendingHealth);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-talon-fixtures.json");
    private sealed class Fixed:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed class Control:IDisposable
    {
        private readonly Func<double> Hit=DragonTalons.HitRoll,Element=DragonTalons.ElementRoll;
        private readonly Func<int,int> Physical=DragonTalons.PhysicalRoll,Poison=DragonTalons.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=DragonTalons.Effects,Knockback=DragonTalons.KnockbackRoll;
        internal Control(){DragonTalons.HitRoll=DragonTalons.ElementRoll=()=>0;DragonTalons.PhysicalRoll=DragonTalons.PoisonRoll=_=>0;DragonTalons.Effects=DragonTalons.KnockbackRoll=new Fixed();SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;}
        public void Dispose(){DragonTalons.HitRoll=Hit;DragonTalons.ElementRoll=Element;DragonTalons.PhysicalRoll=Physical;DragonTalons.PoisonRoll=Poison;DragonTalons.Effects=Effects;DragonTalons.KnockbackRoll=Knockback;SourceBlocking.ChoiceRoll=Choice;SourceBlocking.BlockRoll=Block;}
    }
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var life=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonTalonFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonTalonFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:life?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    public static void Configure()
    {
        Rates();Register("dragon-talon-check",Save);Register("dragon-talon-reload-check",Reload);Register("dragon-talon-mechanics-check",Mechanics);
        foreach(string mode in new[]{"left","right","quick"}){string label="dragon-talon-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        Rates();if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(uint id in new[]{saved.Target,saved.PendingTarget}){if(World.FindMobile((Serial)id) is BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.AIObject?.AITimer.Stop();}}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved fixed-target Dragon Talon action/contact/resources/charge/lifecycle",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(int column=6700,int row=3500)
    {
        Creating=true;try{var p=BladeBodyChecks.Fresh(column,30,row);p.Name="DragonTalonFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}var w=new Dagger();ClassGear.Profiles.Convert(w,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(w),"Actual dagger");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,100*256+17);return p;}finally{Creating=false;}
    }
    private static BaseCreature Target(PlayerMobile p,int dx=1)
    {
        var t=new CobraPassiveTarget{Name="DragonTalonFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(r,0);}return t;
    }
    private static Item Named(PlayerMobile p,string source)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Fixed(),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual named "+source);ClassGear.Refresh(p);return item;}
    private static void Hold(PlayerMobile p,CombatAbility ability,int count)
    {
        int rank=SkillMechanics.Rank(p,ability);var v=SkillMechanics.Values(p,ability);var charge=new MartialCharge(p.Serial.Value,ability,count,rank,count*(int)v.Eval("aurastatcalc2"),TimedEffects.Now.Add(FixtureSpace.Retention));
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Ability!=ability).Append(charge).ToArray());
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static DragonTalons.Series Begin(PlayerMobile p,BaseCreature t)
    {Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount),"Learned Talon begin");return ClassCombat.For(p).Talon;}
    private static bool Kick(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).Talon.EffectAt);
    private static void End(PlayerMobile p)
    {while(ClassCombat.For(p).Talon is{} s){Require(ClassCombat.ResolveAction(p,s.Released<s.Total?s.EffectAt:s.Until),"Talon event/recovery");}}
    private static void Mechanics()
    {
        using var control=new Control();var p=Fresh();var t=Target(p);var other=Target(p);
        try
        {
            ClassSkillChecks.Train(p,"assassin.dragontalon",20);
            Require(AssassinCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned bindings");
            Hold(p,CombatAbility.TigerStrike,3);var original=AssassinCharges.State(p);int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t),untouched=SkillHealth.Balance256(other);
            var s=Begin(p,t);long start=s.EffectAt-ClassCombat.For(p).Timing.EffectMs;var plan=DragonTalonTiming.Plan(p);
            Require(s.Total==4&&s.Rank==20&&s.Released==0&&NecromancerEffects.ManaBalance256(p)==mana-1536&&s.Kick.Contact.HitFlags==3&&s.Kick.Contact.SkillKnockback==false,"Single entry payment/count/initial contact");
            for(int i=0;i<4;i++)
            {
                s=ClassCombat.For(p).Talon;Require(s.EffectAt==start+plan.Contacts[i]*40L&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Approved contact schedule");
                int before=SkillHealth.Balance256(t),physical=NamedElements.To256(s.Kick.Kick.Impact.Parts.Physical);Require(Kick(p)&&before-SkillHealth.Balance256(t)==physical,"Per-kick boot/Tiger contact");
                Require(NecromancerEffects.ManaBalance256(p)==mana-1536&&SkillHealth.Balance256(other)==untouched,"Repeated mana or retarget");
                if(i<2){Require(AssassinCharges.State(p) is{} charge&&charge.Count==2-i&&charge.UntilUtc==original.UntilUtc,"Each successful kick consumes one Tiger charge");}
                else{Require(AssassinCharges.State(p)==null&&AssassinCharges.State(p,CombatAbility.CobraStrike)==null,"Charges replayed after exhaustion");}
            }
            s=ClassCombat.For(p).Talon;Require(s.Released==4&&s.Until==start+plan.EndFrame*40L&&!ClassCombat.ResolveAction(p,s.Until-1)&&ClassCombat.ResolveAction(p,s.Until)&&ClassCombat.For(p).Talon==null,"Separate final recovery");
            Require(SourceKnockbacks.Snapshot().Any(x=>x.Target==t.Serial.Value),"Final multi-kick knockback request");SourceKnockbacks.Clear(t);
            DragonTalons.HitRoll=()=>1;mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);Begin(p,t);End(p);Require(SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana-1536,"Missed series mana/damage");
            Hold(p,CombatAbility.TigerStrike,3);s=Begin(p,t);Require((s.Kick.Flags&1)!=0,"Charged accuracy bypass");Kick(p);Require(AssassinCharges.State(p)?.Count==2,"Charged consumption");ClassCombat.Stop(p);
            var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);shield.AIObject?.AITimer.Stop();shield.SetHits(50000);shield.Hits=shield.HitsMax;
            try{SourceBlocking.BlockRoll=_=>0;original=AssassinCharges.State(p);hp=SkillHealth.Balance256(shield);s=Begin(p,shield);Require((s.Kick.Flags&16)!=0,"Charged shield block");End(p);Require(AssassinCharges.State(p)==original&&SkillHealth.Balance256(shield)==hp,"Blocked series consumed charge or damaged");}finally{SourceKnockbacks.Clear(shield);shield.Delete();SourceBlocking.BlockRoll=_=>99;}
            DragonTalons.HitRoll=()=>0;t.SetResistance(ResistanceType.Physical,100);Hold(p,CombatAbility.TigerStrike,3);Hold(p,CombatAbility.CobraStrike,3);hp=SkillHealth.Balance256(t);Begin(p,t);Kick(p);Require(AssassinCharges.State(p)?.Count==2&&AssassinCharges.State(p,CombatAbility.CobraStrike)?.Count==2&&SkillHealth.Balance256(t)==hp,"Absorbed contact did not consume charge");ClassCombat.Stop(p);AssassinCharges.Clear(p);t.SetResistance(ResistanceType.Physical,0);
            foreach(string mode in new[]{"stop","movement","weapon","boots","recovery","frozen","map"})
            {
                Hold(p,CombatAbility.TigerStrike,3);s=Begin(p,t);original=AssassinCharges.State(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);var origin=p.Location;var map=p.Map;Item item=null;
                switch(mode)
                {
                    case "stop":ClassCombat.Stop(p);break;
                    case "movement":p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);break;
                    case "weapon":item=CombatTiming.Equipped(p);p.AddToBackpack(item);ClassGear.Refresh(p);break;
                    case "boots":item=Named(p,"Gorerider");break;
                    case "recovery":Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery event");break;
                    case "frozen":p.Frozen=true;break;
                    case "map":p.MoveToWorld(p.Location,Map.Felucca);break;
                }
                Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&ClassCombat.For(p).Talon==null&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinCharges.State(p)==original,"Cancellation refunded/replayed/consumed: "+mode);
                p.Frozen=false;p.MoveToWorld(origin,map);if(mode=="weapon"){Require(p.EquipItem(item),"Restore dagger");ClassGear.Refresh(p);}if(mode=="boots"){p.AddToBackpack(item);ClassGear.Refresh(p);}
            }
            s=Begin(p,t);Kick(p);int remaining=AssassinCharges.State(p)?.Count??0;hp=SkillHealth.Balance256(t);var point=t.Location;t.MoveToWorld(new(t.X+10,t.Y,t.Z),t.Map);s=ClassCombat.For(p).Talon;
            Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&ClassCombat.For(p).Talon==null&&AssassinCharges.State(p)?.Count==remaining&&SkillHealth.Balance256(t)==hp&&SkillHealth.Balance256(other)==untouched,"Target loss retargeted or consumed");t.MoveToWorld(point,t.Map);
            AssassinCharges.Clear(p);NecromancerEffects.SetMana256(p,1535);Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(p).Talon==null&&NecromancerEffects.ManaBalance256(p)==1535,"Underfunded ordinary attack fallback");ClassCombat.Stop(p);
            var single=Fresh(6710,3600);var lone=Target(single);try{NecromancerEffects.SetMana256(single,1536);s=Begin(single,lone);Require(s.Total==1&&s.Kick.Contact.SkillKnockback&&NecromancerEffects.ManaBalance256(single)==0,"Single-kick immediate knockback/exact threshold");Kick(single);End(single);}finally{SourceKnockbacks.Clear(lone);lone.Delete();single.Delete();}
            NecromancerEffects.SetMana256(p,100*256+17);ClassSkillChecks.Train(p,"assassin.dragontalon",20);s=Begin(p,t);SkillHealth.SetBalance256(t,256);Kick(p);Require(!t.Alive&&ClassCombat.For(p).Talon==null&&SkillHealth.Balance256(other)==untouched,"Dead primary retained future kicks/retargeted");
            Log.Information("Talon action:fixed initial target,4contacts/final recovery,one6mana debit,per-contact Tiger+Cobra,miss/block/absorb,single/final knockback,7interruptions,target loss/death,basic fallback;modern executable parity/full items/PvP remain unverified");
        }
        finally{SourceKnockbacks.Clear(t);t.Corpse?.Delete();t.Delete();other.Delete();p.Corpse?.Delete();p.Delete();}
    }
    private static void Save()
    {
        using var control=new Control();var p=Fresh(6720);ClassSkillChecks.Train(p,"assassin.dragontalon",20);var helm=Named(p,"Tarnhelm");var boots=Named(p,"Gorerider");var t=Target(p);
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Saved binding assignment");
        Hold(p,CombatAbility.TigerStrike,3);Begin(p,t);Kick(p);ClassCombat.Stop(p);Require(AssassinCharges.State(p)?.Count==2&&SkillMechanics.Rank(p,Ability)==21,"Effective21 kick");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
        var pending=Fresh(6720,3700);var other=Target(pending);Begin(pending,other);var session=ClassCombat.For(pending);long deadline=Core.TickCount+FixtureSpace.RetentionMilliseconds;session.Talon=session.Talon with{EffectAt=deadline,Until=deadline+1000};session.EffectAt=deadline;session.NextAction=deadline+1000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,boots.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),AssassinCharges.State(p),pending.Serial.Value,other.Serial.Value,NecromancerEffects.ManaBalance256(pending),SkillHealth.Balance256(other))));
    }
    private static void Reload()
    {
        using var control=new Control();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);var other=(BaseCreature)World.FindMobile((Serial)s.PendingTarget);
        Require(World.FindItem((Serial)s.Helm)?.Parent==p&&World.FindItem((Serial)s.Boots)?.Parent==p&&SkillMechanics.Rank(p,Ability)==21&&AssassinCharges.State(p)==s.Charge,"Original gear/rank/charge changed on reload");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Health&&ClassCombat.For(pending).Talon==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana&&SkillHealth.Balance256(other)==s.PendingHealth,"Wounds/mana changed or queued paid series replayed");
        Require(SkillBindings.Instance.Get(p)==(Ability,Ability)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Original bindings changed");
        p.MoveToWorld(s.Origin,Map.Trammel);Begin(p,t);Kick(p);Require(AssassinCharges.State(p)?.Count==1,"Reloaded charge failed learned kick");ClassCombat.Stop(p);
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(s.Charge).ToArray());NecromancerEffects.SetMana256(p,s.Mana);SkillHealth.SetBalance256(p,s.Health);p.Internalize();
    }
    private static void Input(NetState network,string label)
    {
        var control=new Control();var p=Fresh(6740);ClassSkillChecks.Train(p,"assassin.dragontalon",6);var t=Target(p);Hold(p,CombatAbility.TigerStrike,3);
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Natural bindings");FixtureNetwork.Authenticate(network,p,"dragon-talon-input-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);bool windup=false;long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var session=ClassCombat.For(p);
                if(!windup&&session.PendingAbility==Ability){Require(session.Talon is{Total:2}&&NecromancerEffects.ManaBalance256(p)==mana-1536&&session.Timing==DragonTalons.Timing(p),"Input entry payment/clock");windup=true;Log.Information("{Label}: windup",label);}
                if(session.Talon!=null||SkillHealth.Balance256(t)==hp){Require(Core.TickCount<end,"Natural input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&NecromancerEffects.ManaBalance256(p)==mana-1536&&AssassinCharges.State(p)?.Count==1,"Raw input duplicate debit/lost kick or repeated charge");
                control.Dispose();SourceKnockbacks.Clear(t);t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){control.Dispose();SourceKnockbacks.Clear(t);t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
