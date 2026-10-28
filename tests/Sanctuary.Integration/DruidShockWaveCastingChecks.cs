// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidShockWaveCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidShockWaveCastingChecks));
    private sealed record Saved(uint Owner,int Mana,int Life,DruidPlayerForm Form,ShockWaveSave Waves,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shock-wave-casting-fixtures.json");
    private static bool _creating;private static int _scene;
    public static void Configure()
    {
        Rates();Register("shock-wave-casting-check",Check);Register("shock-wave-casting-reload-check",Reload);Register("shock-wave-casting-snapshot-check",Snapshot);
        foreach(string label in new[]{"shock-wave-left","shock-wave-right","shock-wave-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("ShockWaveCastingFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Shock Wave bear casting,resources,input and reload",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Talent");
        foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Name="ShockWaveCastingFixture",Body=0x190,Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        Train(p,CombatAbility.ShockWave);Train(p,CombatAbility.Lycanthropy,20);FixtureSpace.Corridor(p,6300,20,1000+32*_scene++);
        var w=new Dagger();ClassGear.Profiles.Convert(w,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(w),"Weapon");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now),"Bear");Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256-17);SkillHealth.SetBalance256(p,p.HitsMax*128-17);}
    private static ShockWaveTarget Target(PlayerMobile p,int offset=2)
    {
        var t=new ShockWaveTarget{Name="ShockWaveCastingFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Delete();}
    private static DruidShockWave.Pending Begin(PlayerMobile p,Mobile target=null,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string waves=Json(DruidShockWave.Snapshot());
        Require(ClassCombat.Perform(p,CombatAbility.ShockWave,target,aim??target?.Location??new(p.X+10,p.Y,p.Z),Core.TickCount),"Shock Wave begin");var action=ClassCombat.For(p);
        Require(action.ShockWaveAttack!=null&&action.Timing==DruidFormCastClock.Calculate(CombatAbility.Werebear,CombatTiming.Fcr(p))&&action.Animation==16,"Bear SC/FCR clock/placeholder");
        Require(mana==NecromancerEffects.ManaBalance256(p)&&Json(DruidShockWave.Snapshot())==waves&&!ClassCombat.ResolveAction(p,action.EffectAt-1),"Early mana/waves/action");return action.ShockWaveAttack;
    }
    private static ShockWaveMissile[] Cast(PlayerMobile p,Mobile target=null)
    {
        var pending=Begin(p,target);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Release");
        Require(NecromancerEffects.ManaBalance256(p)==mana-1792&&ClassCombat.For(p).ShockWaveAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"One-time7mana/replay");
        return DruidShockWave.Snapshot().Missiles.Where(x=>x.Owner==p.Serial.Value).TakeLast(5).ToArray();
    }
    private static void Cases()
    {
        var held=DruidShockWave.Snapshot();var p=Fresh();var t=Target(p);var allocate=DruidShockWave.Allocate;var slows=AmazonSlowMissiles.Snapshot();
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ShockWave)&&SkillBindings.Instance.Assign(p,false,CombatAbility.ShockWave)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ShockWave),"Left/right/quick bindings");
            var first=Cast(p,t);Require(first.Length==5&&first.All(x=>x.Rank==1&&x.Min256==2816&&x.Max256==5632),"Rank1/hard Maul1 endpoints");
            Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.Maul)),"Maul charge");var charge=DruidAttackCharges.State(p,CombatAbility.Maul);var again=Cast(p);
            Require(again.All(x=>x.Min256==first[0].Min256&&x.Max256==first[0].Max256)&&DruidAttackCharges.State(p,CombatAbility.Maul)==charge,"Charge ED leaked into source-zero or consumed");
            Train(p,CombatAbility.Maul,3);var boosted=Cast(p);Require(boosted.All(x=>x.Min256==3328&&x.Max256==6656),"Hard Maul synergy");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);Require(p.EquipItem(helm),"Tarnhelm");ClassGear.Refresh(p);
            var soft=Cast(p);Require(soft.All(x=>x.Rank==2&&x.Min256==4096&&x.Max256==7424)&&SkillMechanics.Values(p,CombatAbility.ShockWave).Eval("DmgSymPerCalc")==30,"Soft Maul leaked into synergy/effective rank");
            while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"FCR glove strength");}
            var fcr=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(321),true);Require(p.EquipItem(fcr),"Magefist");ClassGear.Refresh(p);Require(CombatTiming.Fcr(p)>0,"FCR gear");
            var pending=Begin(p);Require(ClassCombat.For(p).Timing.EffectMs<DruidFormCastClock.Calculate(CombatAbility.Werebear,0).EffectMs,"FCR did not accelerate SC");ClassCombat.Stop(p);
            pending=Begin(p);Require(!HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&ClassCombat.For(p).ShockWaveAttack==pending,"Active bear SC lost uninterruptible guarantee");ClassCombat.Stop(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.For(p).ShockWaveAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana,"Stop failed");
            pending=Begin(p);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);Require(ClassCombat.For(p).ShockWaveAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Movement retained unpaid cast");
            pending=Begin(p);var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);Require(ClassCombat.For(p).ShockWaveAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Weapon change retained cast");Require(p.EquipItem(weapon),"Reequip");ClassGear.Refresh(p);
            pending=Begin(p);NecromancerEffects.SetMana256(p,1791);string before=Json(DruidShockWave.Snapshot());Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&Json(DruidShockWave.Snapshot())==before&&NecromancerEffects.ManaBalance256(p)==1791,"Lost mana produced unpaid waves");
            Ready(p);NecromancerEffects.SetMana256(p,1791);Require(!ClassCombat.Perform(p,CombatAbility.ShockWave,null,new(p.X+10,p.Y,p.Z),Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Low-mana spell attacked/started");
            pending=Begin(p);mana=NecromancerEffects.ManaBalance256(p);before=Json(DruidShockWave.Snapshot());DruidShockWave.Allocate=_=>false;
            Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-1792&&DruidShockWave.Snapshot().Missiles.Length==JsonSerializer.Deserialize<ShockWaveSave>(before).Missiles.Length,"Successful do8 with failed factories did not pay");DruidShockWave.Allocate=allocate;
            pending=Begin(p);AmazonSlowMissiles.Restore(slows.Where(x=>x.Target!=p.Serial.Value).Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddMinutes(1))).ToArray());mana=NecromancerEffects.ManaBalance256(p);before=Json(DruidShockWave.Snapshot());
            Require(!ClassCombat.ResolveAction(p,pending.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana&&Json(DruidShockWave.Snapshot())==before,"Unsupported slow damage paid/allocated");AmazonSlowMissiles.Restore(slows);
            pending=Begin(p,t);t.Delete();Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Deleted target did not use cached ground");
            pending=Begin(p);DruidPlayerForms.Clear(p);Require(ClassCombat.For(p).ShockWaveAttack==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&!DruidCombat.Available(p,CombatAbility.ShockWave),"Human cast/unshift cancellation");
            Train(p,CombatAbility.Werewolf);Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now)&&!DruidCombat.Available(p,CombatAbility.ShockWave),"Wolf allowed");
            Log.Information("Shock Wave learned:bear SC/FCR,left/right/quick bindings,source-zero/no charge ED,hard/soft Maul,7mana,failed factories,Stop/movement/weapon/unshift,lost mana,unsupported slow and deleted target controls pass");
        }
        finally{DruidShockWave.Allocate=allocate;AmazonSlowMissiles.Restore(slows);DruidShockWave.Restore(held);t.Delete();Drop(p);}
    }
    private static void Check()
    {
        using(var clock=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-cast-clock-source-probe.json"))))
        {
            int count=0;
            foreach(var row in clock.RootElement.EnumerateArray())
            {if(row[0].GetInt32()!=9){continue;}var actual=DruidFormCastClock.Calculate(CombatAbility.Werebear,row[1].GetInt32());Require(actual.EffectFrame==row[3][0][1].GetInt32()&&actual.Frames==row[3][1][1].GetInt32(),"Bear source SC/FCR schedule");count++;}
            Require(count==259,"Incomplete bear SC/FCR matrix");Log.Information("Shock Wave:259complete-source bear SC/FCR schedules pass");
        }
        Cases();var p=Fresh();var waves=Cast(p);var shift=FixtureSpace.Retention+FixtureSpace.Retention;
        DruidShockWave.Restore(DruidShockWave.Snapshot() with{Missiles=DruidShockWave.Snapshot().Missiles.Select(x=>x.Owner==p.Serial.Value?x with{NextUtc=x.NextUtc.Add(shift)}:x).ToArray()});
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ShockWave)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ShockWave),"Saved bindings");
        var unpaid=Fresh();Begin(unpaid);var saved=new Saved(p.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),DruidPlayerForms.State(p),DruidShockWave.Snapshot(),unpaid.Serial.Value,NecromancerEffects.ManaBalance256(unpaid));
        // Save before the pending action can release: this is a deliberately
        // unconnected unpaid windup; generic sessions are not persistent.
        ClassCombat.For(unpaid).EffectAt=long.MaxValue;File.WriteAllText(Manifest,Json(saved));
    }
    private static void Snapshot()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var unpaid=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(DruidPlayerForms.State(p)==s.Form&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life
            &&NecromancerEffects.ManaBalance256(unpaid)==s.PendingMana&&Json(DruidShockWave.Snapshot().Missiles.Where(x=>x.Owner==s.Owner).ToArray())==Json(s.Waves.Missiles.Where(x=>x.Owner==s.Owner).ToArray()),"Saved learned wave/form/life/mana changed");
    }
    private static void Reload()
    {
        Snapshot();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var unpaid=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(ClassCombat.For(unpaid).PendingAbility==CombatAbility.None&&ClassCombat.For(unpaid).ShockWaveAttack==null
            &&SkillBindings.Instance.Get(p).Right==CombatAbility.ShockWave&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.ShockWave,"Unpaid windup replay/bindings");
    }
    private static void Input(NetState network,string label)
    {
        var held=DruidShockWave.Snapshot();var p=Fresh();var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ShockWave)&&SkillBindings.Instance.Assign(p,false,CombatAbility.ShockWave)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.ShockWave),"Input bindings");
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false,hit=false;long deadline=Core.TickCount+6000;
        void Cleanup(){SourceBlocking.BlockRoll=block;DruidShockWave.Restore(held);CombatStuns.Clear(t);t.Delete();Drop(p);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Shock Wave input timeout");var action=ClassCombat.For(p);
                if(action.ShockWaveAttack!=null&&!began){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Input paid during windup");Log.Information("{Label}: windup",label);}
                hit|=SkillHealth.Balance256(t)<life&&CombatStuns.Remaining(t)>0;
                if(!began||!hit||DruidShockWave.Snapshot().Missiles.Any(x=>x.Owner==p.Serial.Value)){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(NecromancerEffects.ManaBalance256(p)==mana-1792&&action.ShockWaveAttack==null,"Input7mana/replay");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.ShockWave,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
