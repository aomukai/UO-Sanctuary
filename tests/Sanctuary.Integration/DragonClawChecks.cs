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

public static class DragonClawChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DragonClawChecks));
    private const CombatAbility Ability=CombatAbility.DragonClaw;
    private sealed record Saved(uint Owner,uint Target,uint Primary,uint Secondary,uint Helm,Point3D Origin,int Mana,int Health,MartialCharge Charge,uint PendingOwner,uint PendingTarget,int PendingMana,int PendingHealth);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-claw-fixtures.json");
    private sealed class Fixed(bool minimum=false):Random{public override int Next(int max)=>minimum?0:Math.Max(0,max-1);}
    private sealed class Control:IDisposable
    {
        private readonly Func<double> Hit=DragonClaws.HitRoll,Element=DragonClaws.ElementRoll;
        private readonly Func<int,int> Physical=DragonClaws.PhysicalRoll,Poison=DragonClaws.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=DragonClaws.Effects;
        internal Control(){DragonClaws.HitRoll=DragonClaws.ElementRoll=()=>0;DragonClaws.PhysicalRoll=DragonClaws.PoisonRoll=_=>0;DragonClaws.Effects=new Fixed();SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>99;}
        public void Dispose(){DragonClaws.HitRoll=Hit;DragonClaws.ElementRoll=Element;DragonClaws.PhysicalRoll=Physical;DragonClaws.PoisonRoll=Poison;DragonClaws.Effects=Effects;SourceBlocking.ChoiceRoll=Choice;SourceBlocking.BlockRoll=Block;}
    }
    private static bool Creating;
    private static void Rates()
    {
        var mana=Mobile.ManaRegenRateHandler;var life=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonClawFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:mana?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>Creating||p.Name?.StartsWith("DragonClawFixture",StringComparison.Ordinal)==true?FixtureSpace.Retention:life?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    public static void Configure()
    {
        Rates();Register("dragon-claw-check",Save);Register("dragon-claw-reload-check",Reload);Register("dragon-claw-mechanics-check",Mechanics);
        foreach(string mode in new[]{"left","right","quick"}){string label="dragon-claw-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        Rates();if(!File.Exists(Manifest)){return;}var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(uint id in new[]{s.Target,s.PendingTarget}){if(World.FindMobile((Serial)id) is BaseCreature t){t.Frozen=t.CantWalk=true;t.AIObject?.AITimer.Stop();}}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved Dragon Claw dual sequence/finisher/contact/resources/lifecycle",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh(int column=6770,int row=3500)
    {
        Creating=true;try
        {
            var p=BladeBodyChecks.Fresh(column,30,row);p.Name="DragonClawFixture";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
            var first=new Tekagi();ClassGear.Profiles.Convert(first,99,GearRarity.Normal,new Random(173));p.AddToBackpack(first);Require(p.EquipItem(first),"Actual primary claw");
            var second=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Cutthroat1").Id,99,new Fixed(),true);p.AddToBackpack(second);Require(p.EquipItem(second),"Actual Bartuc offhand");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,100*256+17);return p;
        }
        finally{Creating=false;}
    }
    private static BaseCreature Target(PlayerMobile p,int dx=1)
    {
        var t=new CobraPassiveTarget{Name="DragonClawFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(r,0);}return t;
    }
    private static void Hold(PlayerMobile p,CombatAbility ability,int count)
    {
        int rank=SkillMechanics.Rank(p,ability);var v=SkillMechanics.Values(p,ability);var charge=new MartialCharge(p.Serial.Value,ability,count,rank,count*(int)v.Eval("aurastatcalc2"),TimedEffects.Now.Add(FixtureSpace.Retention));
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value||x.Ability!=ability).Append(charge).ToArray());
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static AssassinClaws.Series Begin(PlayerMobile p,BaseCreature t)
    {Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount),"Learned Dragon Claw begin");return ClassCombat.For(p).ClawAttack;}
    private static bool Contact(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void End(PlayerMobile p)
    {var s=ClassCombat.For(p).ClawAttack;if(s==null){return;}ClassCombat.ResolveAction(p,s.Until);Require(ClassCombat.For(p).ClawAttack==null,"Sequence end cleanup");}
    private static int Physical(PlayerMobile p,BaseCreature t,BaseWeapon weapon)
    {
        using var selected=WeaponHands.Use(p,weapon);var tiger=AssassinCharges.State(p);
        int ed=DragonClawPreparation.PhysicalPercent(p)+AssassinCharges.TigerPercent(tiger,SkillMechanics.Rank(p,CombatAbility.TigerStrike));
        return GearEffects.PhysicalRange256(p,t,ed).Min;
    }
    private static void Mechanics()
    {
        using var control=new Control();var p=Fresh();var t=Target(p);var other=Target(p);var pair=WeaponHands.Pair(p).Value;
        try
        {
            ClassSkillChecks.Train(p,"assassin.dragonclaw",20);ClassSkillChecks.Train(p,"assassin.clawmastery",10);
            Require(AssassinCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned bindings");
            var plan=DragonClawPreparation.Plan(p);Hold(p,CombatAbility.TigerStrike,3);Require(DragonClaws.Prepare(p,t).HitFlags==2,"Successful source Claw hit flags2");var original=AssassinCharges.State(p);int mana=NecromancerEffects.ManaBalance256(p),untouched=SkillHealth.Balance256(other),hits=0;
            DragonClaws.ElementRoll=()=>{Require(CombatTiming.Equipped(p)==(hits++==0?pair.Primary:pair.Secondary),"Wrong contact hand");return 0;};var s=Begin(p,t);
            Require(s.Events.SequenceEqual(plan.Contacts.Select(x=>x*40))&&s.Until==s.Started+plan.EndFrame*40L&&NecromancerEffects.ManaBalance256(p)==mana-512&&s.Ability==Ability,"Distinct entry clock/one debit");
            for(int i=0;i<2;i++)
            {
                int hp=SkillHealth.Balance256(t),physical=Physical(p,t,i==0?pair.Primary:pair.Secondary);long at=s.Started+s.Events[i];
                Require(!ClassCombat.ResolveAction(p,at-1)&&ClassCombat.ResolveAction(p,at)&&!ClassCombat.ResolveAction(p,at),"Each scheduled contact exactly once");
                Require(hp-SkillHealth.Balance256(t)==physical&&NecromancerEffects.ManaBalance256(p)==mana-512&&SkillHealth.Balance256(other)==untouched,"Selected weapon/Tiger packet or repeated mana/retarget");
                Require(AssassinCharges.State(p)?.Count==2-i&&AssassinCharges.State(p).UntilUtc==original.UntilUtc,"Per-contact consume-one/deadline");
            }
            Require(hits==2&&CombatTiming.Equipped(p)==pair.Primary&&!ClassCombat.ResolveAction(p,s.Until-1),"Selected-hand restoration/final recovery");End(p);DragonClaws.ElementRoll=()=>0;
            AssassinCharges.Clear(p);DragonClaws.HitRoll=()=>1;var miss=DragonClaws.Prepare(p,t);Require((miss.Flags&1)==0&&miss.HitFlags==0,"Miss allocated successful hit flags");mana=NecromancerEffects.ManaBalance256(p);int before=SkillHealth.Balance256(t);Begin(p,t);End(p);Require(SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana-512,"Missed series debit/damage");
            Hold(p,CombatAbility.TigerStrike,3);Begin(p,t);End(p);Require(AssassinCharges.State(p)?.Count==1,"Charged accuracy bypass/per-contact consumption");
            Hold(p,CombatAbility.TigerStrike,1);before=SkillHealth.Balance256(t);Begin(p,t);Contact(p);int one=SkillHealth.Balance256(t);Contact(p);Require(AssassinCharges.State(p)==null&&one<before&&SkillHealth.Balance256(t)==one,"Second hand kept exhausted accuracy guarantee");End(p);
            Hold(p,CombatAbility.TigerStrike,3);var shield=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};shield.MoveToWorld(t.Location,p.Map);shield.AIObject?.AITimer.Stop();shield.SetHits(50000);shield.Hits=shield.HitsMax;
            try{SourceBlocking.BlockRoll=_=>0;original=AssassinCharges.State(p);before=SkillHealth.Balance256(shield);Begin(p,shield);End(p);Require(AssassinCharges.State(p)==original&&SkillHealth.Balance256(shield)==before,"Charged shield block consumed/damaged");}finally{shield.Delete();SourceBlocking.BlockRoll=_=>99;}
            DragonClaws.HitRoll=()=>0;t.SetResistance(ResistanceType.Physical,100);Hold(p,CombatAbility.TigerStrike,3);Hold(p,CombatAbility.CobraStrike,3);before=SkillHealth.Balance256(t);Begin(p,t);End(p);Require(AssassinCharges.State(p)?.Count==1&&AssassinCharges.State(p,CombatAbility.CobraStrike)?.Count==1&&SkillHealth.Balance256(t)==before,"Fully absorbed contact did not consume each family");AssassinCharges.Clear(p);t.SetResistance(ResistanceType.Physical,0);
            DragonClaws.Effects=new Fixed(true);int critical=2*Physical(p,t,pair.Primary);before=SkillHealth.Balance256(t);Begin(p,t);Contact(p);Require(before-SkillHealth.Balance256(t)==critical,"Claw inherited kick critical suppression");ClassCombat.Stop(p);DragonClaws.Effects=new Fixed();
            Hold(p,CombatAbility.BladesOfIce,3);Begin(p,t);Contact(p);Require(AssassinCharges.State(p,CombatAbility.BladesOfIce)?.Count==2&&FreezeStates.Shattered(t),"Ice inline freeze/charge delivery");ClassCombat.Stop(p);AssassinCharges.Clear(p);TimedEffects.ClearGear(t);untouched=SkillHealth.Balance256(other);
            foreach(string mode in new[]{"stop","movement","secondary","recovery","frozen","map"})
            {
                Hold(p,CombatAbility.TigerStrike,3);s=Begin(p,t);original=AssassinCharges.State(p);mana=NecromancerEffects.ManaBalance256(p);before=SkillHealth.Balance256(t);var origin=p.Location;var map=p.Map;
                switch(mode){case "stop":ClassCombat.Stop(p);break;case "movement":p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);break;case "secondary":p.AddToBackpack(pair.Secondary);ClassGear.Refresh(p);break;case "recovery":Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount),"Recovery event");break;case "frozen":p.Frozen=true;break;case "map":p.MoveToWorld(p.Location,Map.Felucca);break;}
                Require(!ClassCombat.ResolveAction(p,s.Started+s.Events[0])&&ClassCombat.For(p).ClawAttack==null&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinCharges.State(p)==original,"Cancellation replay/refund/consume: "+mode);
                p.Frozen=false;p.MoveToWorld(origin,map);if(mode=="secondary"){Require(ClassCombat.ActionInterval(p,Ability)==0,"Missing-pair timing preview");Require(p.EquipItem(pair.Secondary),"Restore offhand");ClassGear.Refresh(p);}
            }
            s=Begin(p,t);Contact(p);int count=AssassinCharges.State(p)?.Count??0;before=SkillHealth.Balance256(t);var point=t.Location;t.MoveToWorld(new(t.X+10,t.Y,t.Z),t.Map);Require(!ClassCombat.ResolveAction(p,s.Started+s.Events[1])&&ClassCombat.For(p).ClawAttack==null&&SkillHealth.Balance256(t)==before&&AssassinCharges.State(p)?.Count==count&&SkillHealth.Balance256(other)==untouched,"Target loss retargeted or consumed");t.MoveToWorld(point,t.Map);
            AssassinCharges.Clear(p);NecromancerEffects.SetMana256(p,511);Ready(p);Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(p).ClawAttack==null&&NecromancerEffects.ManaBalance256(p)==511,"Underfunded ordinary attack fallback");ClassCombat.Stop(p);
            NecromancerEffects.SetMana256(p,512);s=Begin(p,t);Require(NecromancerEffects.ManaBalance256(p)==0,"Exact two-mana threshold");End(p);
            NecromancerEffects.SetMana256(p,100*256+17);s=Begin(p,t);SkillHealth.SetBalance256(t,256);Contact(p);Require(!t.Alive&&ClassCombat.For(p).ClawAttack==null&&SkillHealth.Balance256(other)==untouched,"Dead initial target retained/redirected offhand");
            Log.Information("Dragon Claw action:separate primary/secondary packets,dualOnly captured clock,2mana once,per-contact charges/accuracy/block/absorb,ordinary critical,Ice freeze,6interruptions,target loss/death,basic fallback;modern timing/roll-sharing and full item/PvP/client acceptance remain open");
        }
        finally{t.Corpse?.Delete();t.Delete();other.Delete();p.Corpse?.Delete();p.Delete();}
    }
    private static void Save()
    {
        using var control=new Control();var p=Fresh(6790);ClassSkillChecks.Train(p,"assassin.dragonclaw",20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Fixed(),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Original Tarnhelm");ClassGear.Refresh(p);var t=Target(p);var pair=WeaponHands.Pair(p).Value;Require(SkillMechanics.Rank(p,Ability)==24,"Actual effective24 finisher rank");
        Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Saved bindings");Hold(p,CombatAbility.TigerStrike,3);Begin(p,t);Contact(p);ClassCombat.Stop(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);
        var pending=Fresh(6790,3700);var other=Target(pending);Begin(pending,other);var session=ClassCombat.For(pending);long start=Core.TickCount+FixtureSpace.RetentionMilliseconds;session.ClawAttack=session.ClawAttack with{Started=start,Until=start+1000};session.EffectAt=start+session.ClawAttack.Events[0];session.NextAction=start+1000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,pair.Primary.Serial.Value,pair.Secondary.Serial.Value,helm.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),AssassinCharges.State(p),pending.Serial.Value,other.Serial.Value,NecromancerEffects.ManaBalance256(pending),SkillHealth.Balance256(other))));
    }
    private static void Reload()
    {
        using var control=new Control();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);var other=(BaseCreature)World.FindMobile((Serial)s.PendingTarget);
        Require(World.FindItem((Serial)s.Primary)?.Parent==p&&World.FindItem((Serial)s.Secondary)?.Parent==p&&World.FindItem((Serial)s.Helm)?.Parent==p&&SkillMechanics.Rank(p,Ability)==24&&AssassinCharges.State(p)==s.Charge,"Original equipment/charge changed");
        Require(NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Health&&ClassCombat.For(pending).ClawAttack==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana&&SkillHealth.Balance256(other)==s.PendingHealth,"Saved wounds/mana changed or queued paid sequence replayed");
        Require(SkillBindings.Instance.Get(p)==(Ability,Ability)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==Ability,"Bindings changed");p.MoveToWorld(s.Origin,Map.Trammel);Begin(p,t);Contact(p);Require(AssassinCharges.State(p)?.Count==1,"Restored charge failed finisher");ClassCombat.Stop(p);
        AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(s.Charge).ToArray());NecromancerEffects.SetMana256(p,s.Mana);SkillHealth.SetBalance256(p,s.Health);p.Internalize();
    }
    private static void Input(NetState network,string label)
    {
        var control=new Control();var p=Fresh(6810);var t=Target(p);Hold(p,CombatAbility.TigerStrike,3);Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Natural bindings");FixtureNetwork.Authenticate(network,p,"dragon-claw-input-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);bool windup=false;long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var session=ClassCombat.For(p);if(!windup&&session.PendingAbility==Ability){Require(session.ClawAttack is{Ability:Ability}&&NecromancerEffects.ManaBalance256(p)==mana-512&&session.Timing==AssassinClaws.Timing(p,Ability),"Input entry clock/payment");windup=true;Log.Information("{Label}: windup",label);}
                if(session.ClawAttack!=null||SkillHealth.Balance256(t)==hp){Require(Core.TickCount<end,"Natural input timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&NecromancerEffects.ManaBalance256(p)==mana-512&&AssassinCharges.State(p)?.Count==1,"Natural input repeated payment/lost contact/charge");control.Dispose();t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){control.Dispose();t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
