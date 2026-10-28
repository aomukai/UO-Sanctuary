// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;

namespace Sanctuary.Integration;

public static class MoltenBoulderChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MoltenBoulderChecks));
    // Connecting the player activates the sector again, so stopping AITimer
    // alone does not keep this hit fixture on the projectile's intended path.
    // Permit actual forced Mobile.Move steps while excluding voluntary AI motion.
    private sealed class InputTarget:GraveyardSkeleton
    {
        public InputTarget(){}
        public InputTarget(Serial serial):base(serial){}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader)
        {base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid Boulder input fixture version");}}
        public override bool DisallowAllMoves=>!CombatDisplacement.Active(this);
        protected override bool OnMove(Direction direction)=>CombatDisplacement.Active(this) && base.OnMove(direction);
    }
    private sealed record Saved(uint Owner,uint Target,int Life256,BoulderSave State,uint Pending=0,int PendingMana=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"molten-boulder-fixtures.json");
    private static int _scene;private static uint _pending;private static int _pendingMana;private static bool _creating;
    private static void Rates(){var prior=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=x=>x is PlayerMobile && (_creating || x.Name?.StartsWith("BoulderFixture",StringComparison.Ordinal)==true)?TimeSpan.FromHours(1):prior?.Invoke(x)??Mobile.DefaultManaRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();Register("molten-boulder-check",Check);Register("molten-boulder-reload-check",Reload);Register("molten-boulder-input-check",Input);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared native Molten Boulder actors",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="BoulderFixture"};}finally{_creating=false;}p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,6860,25,3400+32*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var reason),"Train: "+reason);}
    }
    private static GraveyardSkeleton Target(PlayerMobile p,int dx,bool forcedMotionOnly=false)
    {
        GraveyardSkeleton t=forcedMotionOnly?new InputTarget():new GraveyardSkeleton();
        t.Frozen=t.CantWalk=t.NoKillAwards=true;t.SetHits(10000);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}
        t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();return t;
    }
    private static string Text()=>JsonSerializer.Serialize(DruidMoltenBoulder.Snapshot());
    private static BoulderMissile Launch(PlayerMobile p,DateTime at)
    {long id=DruidMoltenBoulder.Create(p,new(p.X+12,p.Y,p.Z),1,at);return DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Id==id);}
    private static BoulderMissile Rolling(PlayerMobile p,DateTime at)
    {
        Launch(p,at);DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);
        return DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Missile==452 && x.Owner==p.Serial.Value);
    }
    private static void DamageSource()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"molten-boulder-damage-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int kind=row[0].GetInt32(),rank=row[1].GetInt32(),firestorm=row[2].GetInt32(),volcano=row[3].GetInt32(),mastery=row[4].GetInt32();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.MoltenBoulder),_=>rank,key=>key=="Firestorm"?firestorm:key=="Volcano"?volcano:0,99);
            var actual=DruidMoltenBoulder.Endpoints(values,kind==0?453:kind==1?452:455,mastery,SlowMissilePayload.Normal);
            Require(actual==new BoulderEndpoints(row[6].GetInt32(),row[7].GetInt32(),row[8].GetInt32(),row[9].GetInt32()),"Complete source damage capture differs at kind/rank "+kind+"/"+rank);
            Require(values.Mana256==2560+(rank-1)*128,"Half-mana rank growth");count++;
        }
        Require(count==12240,"Incomplete source damage coverage");Log.Information("Molten Boulder:12240complete source damage capture comparisons/all255ranks and mana growth pass");
    }
    private static void ImpactSource()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"molten-boulder-impact-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var kind=(BoulderImpactKind)row[0].GetInt32();int species=row[1].GetInt32();bool kb=row[2].GetInt32()!=0,guard=row[3].GetInt32()!=0;
            int defense=row[4].GetInt32(),resistance=row[5].GetInt32();ulong seed=SourceRandom.InitLow(unchecked((int)row[6].GetUInt32()));
            var flags=MoltenBoulderPackets.ImpactFlags(kind,species==1,species==2,guard,seed);
            var plan=MoltenBoulderPackets.ResolveImpact(kind,flags,defense,kb,guard);
            int health=25600-(plan.Damage && resistance==0?(kind==BoulderImpactKind.Ground?1536:3072):0);
            Require(health==row[7].GetInt32() && plan.Push==(row[8].GetInt32()==13) && plan.Seed==row[9].GetUInt64(),"Complete impact health/KB mode/RNG composition");
            if(kind!=BoulderImpactKind.Area){Require(plan.Flags==row[11].GetInt32(),"Complete generic impact flags");}count++;
        }
        Require(count==3024,"Incomplete source impact grid");Log.Information("Molten Boulder:3024complete impact health/mode/RNG comparisons pass, including blocked KB and uninterruptible controls");
    }
    private static void Motion()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);int mana=NecromancerEffects.ManaBalance256(p);
        var initial=Launch(p,at);Require(initial.Missile==453 && initial.Remaining==5 && initial.Velocity256==576 && initial.PhysicalMax256==0 && initial.FireMax256==0,"Emergence factory/lifetime/empty packet");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(160),true);var fourth=DruidMoltenBoulder.Snapshot().Missiles.Single();
        Require(fourth.Missile==453 && fourth.Remaining==1,"InitSteps introduced extra server updates");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);var rolling=DruidMoltenBoulder.Snapshot().Missiles.Single();
        Require(rolling.Missile==452 && rolling.Remaining==50 && rolling.Velocity256==1536 && rolling.X>initial.X
            && rolling.AimX==initial.AimX && rolling.AimY==initial.AimY && rolling.PhysicalMin256==1536 && rolling.PhysicalMax256==3072,"Fresh rolling child capture");
        var v=SkillMechanics.Values(p,CombatAbility.MoltenBoulder,1);Require(rolling.FireMin256==v.Damage256(false) && rolling.FireMax256==v.Damage256(true),"Hard Firestorm synergy capture");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(240),true);Require(!DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Missile==455),"Trail emitted on new movement rather than previous movement flag");
        DruidMoltenBoulder.Tick(at.AddMilliseconds(480),true);var fields=DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455).ToArray();
        Require(fields.Length>0 && fields.All(x=>x.Remaining is >=1 and <=37 && x.Velocity256==0 && x.PhysicalMax256==0),"Independent stationary trail fields");
        Require(NecromancerEffects.ManaBalance256(p)==mana,"Prepared actor charged cast mana");DruidMoltenBoulder.Cancel(p);p.Delete();
    }
    private static void Contacts()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Rolling(p,at);var t=Target(p,2);
        int last=SkillHealth.Balance256(t),changes=0;
        for(int i=6;i<=16;i++)
        {
            DruidMoltenBoulder.Tick(at.AddMilliseconds(i*40),true);int life=SkillHealth.Balance256(t);
            if(life<last){changes++;}last=life;
        }
        Require(changes>=2 && DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Missile==452),"Non-LARGE repeated contacts/continuation");
        Require(DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455).All(x=>x.Remaining<=37),"Trail lifetime");
        DruidMoltenBoulder.Cancel(p);t.Delete();p.Delete();
        // A declared LARGE species input exercises native area/disc services;
        // it does not relabel the production skeleton species as LARGE.
        p=Fresh();Train(p,CombatAbility.MoltenBoulder);Rolling(p,at);t=Target(p,2);var prior=DruidMoltenBoulder.Model;
        try
        {
            DruidMoltenBoulder.Model=c=>SourceKnockbacks.Rule(c) with{Large=true};
            int before=SkillHealth.Balance256(t);bool exploded=false;
            for(int i=6;i<=12;i++)
            {
                DruidMoltenBoulder.Tick(at.AddMilliseconds(i*40),true);
                if(!DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value && x.Missile==452))
                {
                    var disc=DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Missile==455 && x.Remaining==7).ToArray();
                    Require(disc.Length==18 && SkillHealth.Balance256(t)<before,"LARGE area damage/18 shortened children");exploded=true;break;
                }
            }
            Require(exploded,"LARGE contact never exploded");
        }
        finally{DruidMoltenBoulder.Model=prior;DruidMoltenBoulder.Cancel(p);t.Delete();p.Delete();}
    }
    private static void Offline()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Launch(p,at);
        DruidMoltenBoulder.Tick(at.AddSeconds(1),false);Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0,"Offline emergence spawned a child instead of expiring");
        var state=Rolling(p,at);var target=Target(p,2);int life=SkillHealth.Balance256(target);
        DruidMoltenBoulder.Tick(at.AddSeconds(4),false);Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0 && SkillHealth.Balance256(target)==life,"Offline rolling fought/emitted or stopped expiring");target.Delete();
        Launch(p,at);var saved=DruidMoltenBoulder.Snapshot();string text=Text();
        Task.Run(()=>{for(int i=0;i<20;i++){Require(Text()==text,"Worker snapshot mutation");}}).GetAwaiter().GetResult();
        var broken=saved with{Missiles=saved.Missiles.Concat(new[]{saved.Missiles[0]}).ToArray()};bool refused=false;
        try{DruidMoltenBoulder.Restore(broken);}catch(InvalidDataException){refused=true;}
        Require(refused && Text()==text,"Failed restore modified current state");
        var copy=DruidMoltenBoulder.Snapshot();copy.Missiles[0].Steps.Cells[0]=new(1,1);Require(Text()==text,"Snapshot exposed mutable saved steps");
        DruidMoltenBoulder.Cancel(p);p.Delete();
    }
    private static void SlowCapture()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);var original=AmazonSlowMissiles.Snapshot();
        try
        {
            AmazonSlowMissiles.Restore(original.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddDays(2))).ToArray());
            var emergence=Launch(p,at);Require(emergence.Velocity256==288,"Emergence Slow Missiles velocity");
            DruidMoltenBoulder.Tick(at.AddMilliseconds(200),true);var rolling=DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value);
            var values=SkillMechanics.Values(p,CombatAbility.MoltenBoulder,1);
            Require(rolling.Missile==452 && rolling.Velocity256==768 && rolling.PhysicalMin256==values.PhysicalDamage256(false)
                && rolling.PhysicalMax256==values.PhysicalDamage256(true) && rolling.FireMin256==values.Damage256(false)/2
                && rolling.FireMax256==values.Damage256(true)/2,"Slow elemental capture must preserve physical endpoints");
        }
        finally{AmazonSlowMissiles.Restore(original);DruidMoltenBoulder.Cancel(p);p.Delete();}
    }
    private static void Ready(PlayerMobile p,bool cooldown=true)
    {
        ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;
        if(cooldown){var saved=FireGround.Snapshot();FireGround.Restore(saved with{Cooldowns=saved.Cooldowns.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.MoltenBoulder).ToArray()});}
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static bool Start(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {bool started=ClassCombat.Perform(p,CombatAbility.MoltenBoulder,target,aim??target?.Location??new Point3D(p.X+10,p.Y,p.Z),Core.TickCount);ClassCombat.ReleaseCast(p);return started;}
    private static void Casting()
    {
        var p=Fresh();Require(!ClassCombat.Available(p,CombatAbility.MoltenBoulder),"Untrained Boulder available");Train(p,CombatAbility.MoltenBoulder);Mana(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MoltenBoulder) && SkillBindings.Instance.Assign(p,false,CombatAbility.MoltenBoulder) && SkillBindings.Instance.AssignQuick(p,6,CombatAbility.MoltenBoulder),"Boulder left/right/quick bindings");
        Ready(p);NecromancerEffects.SetMana256(p,2559);Require(!Start(p) && NecromancerEffects.ManaBalance256(p)==2559,"Low mana began Boulder");Mana(p);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(Start(p),"Boulder SC windup");long at=ClassCombat.For(p).EffectAt;
        Require(ClassCombat.For(p).Animation==16 && !ClassCombat.ResolveAction(p,at-1) && NecromancerEffects.ManaBalance256(p)==mana,"SC timing/early payment");ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Stop paid cancelled Boulder");
        Ready(p);Require(Start(p),"Movement windup");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;
        Require(p.Move(Direction.West) && p.Location==origin && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Out/back restored cancelled Boulder");
        Ready(p);Require(Start(p),"Recovery windup");at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Recovery cancellation");
        Ready(p);Require(Start(p),"Mana-loss windup");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);string before=Text();
        Require(!ClassCombat.ResolveAction(p,at) && Text()==before && FireGround.Ready(p,CombatAbility.MoltenBoulder),"Failed release emitted/charged/delayed");Mana(p);
        Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(Start(p),"Ground windup");at=ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana-2560 && DruidMoltenBoulder.Snapshot().Missiles.Count(x=>x.Owner==p.Serial.Value)==1,"Once-only release/payment");
        var delay=FireGround.Snapshot().Cooldowns.Single(x=>x.Owner==p.Serial.Value && x.Ability==CombatAbility.MoltenBoulder);
        Require(delay.UntilUtc>TimedEffects.Now && delay.UntilUtc<=TimedEffects.Now.AddMilliseconds(1000) && !Start(p),"25frame local delay");
        Ready(p,false);Require(ClassCombat.Perform(p,CombatAbility.Firestorm,null,new(p.X+8,p.Y,p.Z),Core.TickCount),"Boulder local delay became global");ClassCombat.Stop(p);DruidMoltenBoulder.Cancel(p);
        Ready(p);Mana(p);var t=Target(p,6);Require(Start(p,t),"Live target windup");at=ClassCombat.For(p).EffectAt;t.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);
        Require(ClassCombat.ResolveAction(p,at) && DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value) is{Vx:0,Vy:>0},"Live target aim at release");DruidMoltenBoulder.Cancel(p);t.Delete();
        Ready(p);Mana(p);t=Target(p,6);Require(Start(p,t),"Deleted target windup");at=ClassCombat.For(p).EffectAt;t.Delete();
        Require(ClassCombat.ResolveAction(p,at) && DruidMoltenBoulder.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value),"Deleted target lost cached ground aim");DruidMoltenBoulder.Cancel(p);
        Ready(p);Mana(p);Require(Start(p,aim:p.Location),"Self-coordinate windup");at=ClassCombat.For(p).EffectAt;Require(ClassCombat.ResolveAction(p,at) && DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value) is{Vx:>0,Vy:>0},"Self-coordinate factory fallback");DruidMoltenBoulder.Cancel(p);p.Delete();
    }
    private static void BlockedPush()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);var priorBlock=SourceBlocking.BlockRoll;var priorChoice=SourceBlocking.ChoiceRoll;
        GraveyardSkeleton t=null;
        try
        {
            SourceBlocking.ChoiceRoll=span=>Math.Min(1,span-1);SourceBlocking.BlockRoll=_=>0;Rolling(p,at);t=Target(p,1);t.Frozen=t.CantWalk=false;
            Require(SourceBlocking.TryRate(t,out int chance,out _) && chance>0,"Shielded native target");int life=SkillHealth.Balance256(t);
            DruidMoltenBoulder.Tick(at.AddMilliseconds(240),true);
            Require(SkillHealth.Balance256(t)==life && SourceKnockbacks.Snapshot().Any(x=>x.Target==t.Serial.Value),"Blocked rolling hit must retain source knockback");
        }
        finally{SourceBlocking.BlockRoll=priorBlock;SourceBlocking.ChoiceRoll=priorChoice;if(t!=null){SourceKnockbacks.Clear(t);t.Delete();}DruidMoltenBoulder.Cancel(p);p.Delete();}
    }
    private static void GearAndImmunity()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);Mana(p);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.MoltenBoulder)==2,"Actual Boulder soft rank");
        if(NamedGear.Available("unique/358"))
        {
            for(int i=0;i<70;i++){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength allocation for actual robe");}
            var robe=NamedGear.Create("unique/358",99,new Random(173),true);Require(p.EquipItem(robe),"Actual Ormus robe");ClassGear.Refresh(p);
            Require(NamedElements.MasteryPercent(p,"fire")>0,"Actual fire mastery supplier");
        }
        else{Log.Information("Molten Boulder: live Druid Ormus mastery check deferred by existing named-item gate: {Reason}",string.Join(",",NamedGear.Unavailable["unique/358"]));}
        int mastery=NamedElements.MasteryPercent(p,"fire");Ready(p);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
        Require(Start(p),"Geared Boulder cast");Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-2688,"Rank2 half-mana cost");
        var emerge=DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value);DruidMoltenBoulder.Tick(emerge.NextUtc.AddMilliseconds(160),true);
        var rolling=DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Owner==p.Serial.Value && x.Missile==452);var values=SkillMechanics.Values(p,CombatAbility.MoltenBoulder,2);
        Require(rolling.Rank==2 && rolling.Modifiers.FireMastery==mastery && rolling.FireMin256==values.Damage256(false)+(int)((long)values.Damage256(false)*mastery/100),"Actual mastered skill fire capture");
        DruidMoltenBoulder.Tick(emerge.NextUtc.AddMilliseconds(440),true);var field=DruidMoltenBoulder.Snapshot().Missiles.First(x=>x.Owner==p.Serial.Value && x.Missile==455);
        int expected=FireGround.MissileDamage256("moltenboulderfirepath",2,8,false);
        Require(field.Rank==2 && field.Modifiers.FireMastery==mastery && field.FireMin256==expected,"Ground fire inherited player mastery or soft-point synergy");
        DruidMoltenBoulder.Cancel(p);var t=Target(p,2);var other=Target(p,2);
        var constant=field with{Id=field.Id+1,X=t.X,Y=t.Y,CellX=t.X*2,CellY=t.Y*2,Remaining=2,FireMin256=2560,FireMax256=2560,
            Modifiers=new(0,0,0,0,0),Animation256=0,Steps=new([new(t.X*2,t.Y*2)]),NextUtc=TimedEffects.Now.AddDays(1)};
        t.SetResistance(ResistanceType.Fire,100);other.SetResistance(ResistanceType.Fire,100);int life=SkillHealth.Balance256(t)+SkillHealth.Balance256(other);
        DruidMoltenBoulder.Restore(new([constant],constant.Id));DruidMoltenBoulder.Tick(constant.NextUtc,true);Require(SkillHealth.Balance256(t)+SkillHealth.Balance256(other)==life,"Ground fire bypassed real immunity");
        Require(CombatPotions.Apply(p,3),"Actual spell potion");constant=constant with{Id=constant.Id+1,Modifiers=DruidFirestorm.Modifiers(p),NextUtc=constant.NextUtc.AddSeconds(1)};
        DruidMoltenBoulder.Restore(new([constant],constant.Id));DruidMoltenBoulder.Tick(constant.NextUtc,true);
        // Fire remains immune. Ormus enhances the captured cold/lightning
        // potion components; poison is unmastered. Exactly one collider takes it.
        int extra=new[]{constant.Modifiers.ColdMastery,constant.Modifiers.LightningMastery,constant.Modifiers.PoisonMastery}.Sum(x=>(int)(256L*Math.Max(0,100+x)/100));
        Require(life-SkillHealth.Balance256(t)-SkillHealth.Balance256(other)==extra && (SkillHealth.Full(t)||SkillHealth.Full(other)),"Potion nonfire components/one collider");
        DruidMoltenBoulder.Cancel(p);t.Delete();other.Delete();p.Delete();
    }
    private static void TownAndWall()
    {
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);var target=Target(p,6);var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        try{int life=SkillHealth.Balance256(target);Launch(p,at);DruidMoltenBoulder.Tick(at.AddSeconds(5),true);Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0 && SkillHealth.Balance256(target)==life,"Boulder crossed a real wall or left nonexpiring fire");}
        finally{wall.Delete();target.Delete();}
        var origin=p.Location;target=Target(p,2);var town=new GuardedRegion("BoulderFixtureTown",p.Map,1000,new Rectangle2D(p.X-4,p.Y-2,2,5));town.Register();
        try
        {
            int life=SkillHealth.Balance256(target);Launch(p,at);p.MoveToWorld(new(origin.X-3,origin.Y,origin.Z),p.Map);Require(SpellHelper.IsTown(p.Location,p),"Real owner-town fixture");
            DruidMoltenBoulder.Tick(at.AddSeconds(5),true);Require(SkillHealth.Balance256(target)<life,"SrcTown0 cancelled released Boulder outside town");Ready(p);Require(!Start(p,aim:origin),"Boulder cast from town");
            p.MoveToWorld(origin,p.Map);DruidMoltenBoulder.Create(p,new(origin.X-10,origin.Y,origin.Z),1,at);DruidMoltenBoulder.Tick(at.AddSeconds(5),true);
            Require(DruidMoltenBoulder.Snapshot().Missiles.Length==0,"Town0 projectile/field failed to expire at its own town boundary");
        }
        finally{DruidMoltenBoulder.Cancel(p);town.Unregister();target.Delete();p.Delete();}
    }
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.MoltenBoulder),"Learned Boulder handler missing");
        var block=SourceBlocking.BlockRoll;var seed=DruidMoltenBoulder.SeedLow;
        try{SourceBlocking.BlockRoll=_=>99;DruidMoltenBoulder.SeedLow=()=>1;DamageSource();ImpactSource();Casting();Motion();Contacts();Offline();SlowCapture();BlockedPush();GearAndImmunity();TownAndWall();}
        finally{SourceBlocking.BlockRoll=block;DruidMoltenBoulder.SeedLow=seed;}
        var p=Fresh();Train(p,CombatAbility.MoltenBoulder);var at=TimedEffects.Now.AddDays(1);Rolling(p,at);var t=Target(p,2);
        DruidMoltenBoulder.Tick(at.AddMilliseconds(280),true);var saved=DruidMoltenBoulder.Snapshot();
        // Hold the checkpoint clock after separate normal motion/lifetime tests.
        DruidMoltenBoulder.Restore(saved with{Missiles=saved.Missiles.Select(x=>x with{NextUtc=TimedEffects.Now.AddDays(2)}).ToArray()});
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.MoltenBoulder) && SkillBindings.Instance.Assign(p,false,CombatAbility.MoltenBoulder) && SkillBindings.Instance.AssignQuick(p,6,CombatAbility.MoltenBoulder),"Saved Boulder bindings");
        var pending=Fresh();Train(pending,CombatAbility.MoltenBoulder);Ready(pending);Mana(pending);_pending=pending.Serial.Value;_pendingMana=NecromancerEffects.ManaBalance256(pending);Require(Start(pending),"Saved unpaid Boulder windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        Save(p,t);Log.Information("Molten Boulder: learned casting and native emergence/rolling/trail/direct/area/disc/offline/atomic-worker/blocked-push checks pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        Require(Text()==JsonSerializer.Serialize(saved.State),"Native reload lost exact Boulder position/steps/damage/seed/clock");
        Require(World.FindMobile((Serial)saved.Owner) is PlayerMobile && World.FindMobile((Serial)saved.Target) is GraveyardSkeleton t && SkillHealth.Balance256(t)==saved.Life256,"Saved owner/target/life changed");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);Require(SkillBindings.Instance.Get(p)==(CombatAbility.MoltenBoulder,CombatAbility.MoltenBoulder) && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,6)==CombatAbility.MoltenBoulder,"Saved Boulder bindings changed");
        _pending=saved.Pending;_pendingMana=saved.PendingMana;var pending=World.FindMobile((Serial)_pending) as PlayerMobile;
        Require(pending!=null && NecromancerEffects.ManaBalance256(pending)==_pendingMana && ClassCombat.For(pending).PendingAbility==CombatAbility.None,"Saved unpaid Boulder replayed/paid");
        Log.Information("Molten Boulder: exact prepared native state survives actual reload");
    }
    private static void Save(PlayerMobile p,Mobile target)=>File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),DruidMoltenBoulder.Snapshot(),_pending,_pendingMana)));
    private static void Held(BoulderSave held)
    {
        var current=DruidMoltenBoulder.Snapshot();Require(JsonSerializer.Serialize(current with{NextId=held.NextId})==JsonSerializer.Serialize(held),"Connected fixture changed held Boulder state");
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=World.FindMobile((Serial)saved.Target);
        Require(SkillHealth.Balance256(t)==saved.Life256,"Connected fixture changed held target life");Save(p,t);
    }
    private static void Input()=>FixtureNetwork.Connected("molten-boulder-input-result",Input);
    private static void Input(NetState network)
    {
        var held=DruidMoltenBoulder.Snapshot();var p=Fresh();Train(p,CombatAbility.MoltenBoulder);Mana(p);Ready(p);var target=Target(p,3,true);target.Frozen=target.CantWalk=false;var start=target.Location;
        Require(SkillBindings.Instance.AssignQuick(p,6,CombatAbility.MoltenBoulder),"Connected quick binding");
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Input protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);long deadline=Core.TickCount+9000;bool began=false,released=false,damaged=false,pushed=false;
        var seed=DruidMoltenBoulder.SeedLow;var block=SourceBlocking.BlockRoll;DruidMoltenBoulder.SeedLow=()=>1;SourceBlocking.BlockRoll=_=>99;
        void Cleanup(){DruidMoltenBoulder.SeedLow=seed;SourceBlocking.BlockRoll=block;DruidMoltenBoulder.Cancel(p);SourceKnockbacks.Clear(target);target.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Boulder quick-cast timeout");var missiles=DruidMoltenBoulder.Snapshot().Missiles.Where(x=>x.Owner==p.Serial.Value).ToArray();
                if(!began && ClassCombat.For(p).PendingAbility==CombatAbility.MoltenBoulder){began=true;Require(missiles.Length==0 && NecromancerEffects.ManaBalance256(p)==mana,"Input paid/emitted during windup");}
                if(!released && missiles.Length>0){released=true;Require(began && missiles.Length==1 && missiles[0].Missile==453 && NecromancerEffects.ManaBalance256(p)==mana-2560 && !FireGround.Ready(p,CombatAbility.MoltenBoulder),"Quick cast release/payment/delay");}
                damaged|=SkillHealth.Balance256(target)<life;
                if(target.Location!=start)
                {
                    Require(damaged && target.X>start.X && target.Y==start.Y && target.Z==start.Z,"Input target moved without damaging outward knockback");
                    pushed=true;
                }
                if(!released || missiles.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(damaged && pushed && NecromancerEffects.ManaBalance256(p)==mana-2560 && FireGround.Ready(p,CombatAbility.MoltenBoulder),$"Natural damage/knockback/once-only mana/lifetime: damaged={damaged} pushed={pushed} mana={NecromancerEffects.ManaBalance256(p)}/{mana-2560} ready={FireGround.Ready(p,CombatAbility.MoltenBoulder)} target={target.Location} start={start}");Cleanup();Held(held);Log.Information("molten-boulder-input-result: passed");
            }
            catch(Exception e){Cleanup();Log.Error(e,"molten-boulder-input-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("molten-boulder-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.MoltenBoulder,p.X+10,p.Y,p.Z);Wait();});
    }
}
