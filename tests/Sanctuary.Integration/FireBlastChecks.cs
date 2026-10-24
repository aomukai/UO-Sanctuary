// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class FireBlastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FireBlastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Helm,int Mana,int Health,FireBomb Air,FireBomb Ground,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fire-blast-fixtures.json");
    private static bool _creating;
    private static int _next;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("FireBlastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("FireBlastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();Register("fire-blast-check",Check);Register("fire-blast-reload-check",Reload);Register("fire-blast-input-check",Input);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)s.Target) is BaseCreature c){Stop(c);}}};
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fire Blast damage, conversion, life restoration and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh(int level=99,DiabloClass characterClass=DiabloClass.Assassin)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="FireBlastFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,characterClass);if(level>1){ClassCreation.Profiles.AdvanceLevel(p,level);}
        for(int i=0;i<100;i++)
        {
            try{FixtureSpace.Place(p,new(6000,1950+(_next++%50)*32,0),Map.Trammel);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var other in p.Map.GetMobilesInRange<Mobile>(p.Location,20)){if(other!=p){clear=false;break;}}
            var end=new Point3D(p.X+8,p.Y,p.Z);
            if(!clear || !p.Map.CanSpawnMobile(end) || !p.InLOS(end)){continue;}
            p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
        }
        p.Delete();throw new InvalidOperationException("No independent Fire Blast corridor");
    }
    private static void Train(PlayerMobile p,CombatAbility a,int rank=1)
    {
        var row=SkillMechanics.For(a);foreach(string key in new[]{"reqskill1","reqskill2","reqskill3"}){if(row.Text(key) is{} text && text!="" && text!="0"){Train(p,SkillMechanics.Source(text).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+a);}
    }
    private static void Stop(BaseCreature t){t.Combatant=null;t.FocusMob=null;t.AIObject?.AITimer.Stop();}
    private static Skeleton Target(PlayerMobile p,int dx=4,int dy=0)
    {
        Skeleton t;_creating=true;try{t=new Skeleton{Name="FireBlastFixtureTarget",NoKillAwards=true};}finally{_creating=false;}
        t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);Stop(t);t.SetHits(20000);t.SetResistance(ResistanceType.Fire,0);return t;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static SkillValues Values(int rank,int hard=0)
    {
        var row=SkillMechanics.For(CombatAbility.FireBlast);string[] keys=["Shock Field","Charged Bolt Sentry","Lightning Sentry","Wake of Fire Sentry","Inferno Sentry"];
        return new(row,k=>k==row.SourceKey?rank:0,k=>Array.IndexOf(keys,k) is int i && i>=0?Math.Clamp(hard-i*20,0,20):0,99);
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.FireBlast);Require(row.SourceId==251 && row.Text("anim")=="S2" && row.Number("UseAttackRate")==1,"Source S2 identity");
        foreach(var (rank,min,max,mana) in new[]{(1,768,1024,768),(3,1536,2304,832),(20,21888,29056,1376),(21,24448,32128,1408),(255,1741440,1986688,8896)})
        {var v=Values(rank);Require(v.Damage256(false)==min && v.Damage256(true)==max && v.Mana256==mana,"Independent endpoint/mana golden "+rank);}
        Require(Values(3,1).Damage256(false)==1704 && Values(3,1).Damage256(true)==2557,"Hard synergy FP256 truncation");
        Require(FireBlasts.Supported(Values(40,100)) && !FireBlasts.Supported(Values(255,100)),"Explicit signed32 unsupported boundary");
        Require(FireBlasts.Lifetime(8,0)==14 && FireBlasts.Lifetime(8,8)==21 && FireBlasts.Lifetime(1,1)==1,"Independent source distance/current lifetime");
        Require(CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.FireBlast,new(AttackFamily.Unarmed,0))==new ActionTiming(15,8)
            && CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.FireBlast,new(AttackFamily.Unarmed,0),fcr:200)==new ActionTiming(15,8)
            && CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.FireBlast,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"S2 IAS/FCR schedule");
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fire-blast-timing-source-probe.json")));int count=0;
        foreach(var item in d.RootElement.GetProperty("clock").EnumerateArray())
        {int[] x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Require(FireBlasts.Timing(x[8])==new ActionTiming(x[10]-1,x[9]),"Full source schedule comparison");count++;}Require(count==1152,"Timing coverage");
        var b=new FireBomb(1,1,0,1,0,0,0,0,0,1,true,TimedEffects.Now,768,1024,10,50,25,0,0);
        var raw=FireBlasts.Packet(b,768);Require(raw.Fire==3.45 && raw.Cold==.375 && raw.Lightning==.3 && raw.Poison==.3,"Approved T29 additions retain original mastered fire");
    }
    private static FireBomb Cast(PlayerMobile p,Mobile target=null,Point3D? point=null,bool held=false)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var at=point??target?.Location??p.Location;
        Require(held?ClassCombat.ContinueCast(p,CombatAbility.FireBlast,target,at,Core.TickCount):ClassCombat.Perform(p,CombatAbility.FireBlast,target,at,Core.TickCount),"Cast begin");
        if(held){Require(ClassCombat.ContinueCast(p,CombatAbility.FireBlast,target,at,Core.TickCount),"Held refresh");}ClassCombat.ReleaseCast(p);
        var s=ClassCombat.For(p);Require(s.Animation==9 && s.Timing==FireBlasts.Timing(p) && NecromancerEffects.ManaBalance256(p)==mana && !ClassCombat.ResolveAction(p,s.EffectAt-1),"Unpaid S2 windup");
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.FireBlast).Mana256,"Once-only release payment");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt),"Duplicate release");return FireBlasts.Snapshot().Last(b=>b.Owner==p.Serial.Value);
    }
    private static void Clear(PlayerMobile p)=>FireBlasts.Restore(FireBlasts.Snapshot().Where(b=>b.Owner!=p.Serial.Value).ToArray());
    private static void Run(PlayerMobile p,FireBomb b)=>FireBlasts.Tick(b.NextUtc.AddSeconds(3));
    private static void Equipment()
    {
        var p=Fresh();Train(p,CombatAbility.FireBlast);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Claw Strength");}
        while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Claw Dexterity");}
        var first=new Tekagi();ClassGear.Profiles.Convert(first,99,GearRarity.Normal,new Random(173));p.AddToBackpack(first);Require(p.EquipItem(first),"Primary claw equip");
        var second=new Tekagi();bool found=false;
        for(int seed=0;seed<2000 && !found;seed++){ClassGear.Profiles.Convert(second,99,GearRarity.Magic,new Random(seed));found=ClassGear.Bonus(second,"swing1")+ClassGear.Bonus(second,"swing2")+ClassGear.Bonus(second,"swing3")>=20;}
        Require(found,"No legal IAS claw roll");p.AddToBackpack(second);Require(p.EquipItem(second),"Legal IAS offhand equip");ClassGear.Refresh(p);
        Require(WeaponHands.Pair(p)==new WeaponPair(first,second) && CombatTiming.Ias(p)==0,"S2 primary IAS excludes actual offhand affix");
        var timing=FireBlasts.Timing(p);
        var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(gloves);Require(p.EquipItem(gloves),"Real FCR gloves");ClassGear.Refresh(p);Require(CombatTiming.Fcr(p)>=20 && FireBlasts.Timing(p)==timing,"FCR altered S2 clock");
        p.AddToBackpack(first);Require(p.EquipItem(first),"Reequip original claw as secondary");ClassGear.Refresh(p);
        Require(WeaponHands.Pair(p)==new WeaponPair(second,first) && CombatTiming.Ias(p)>=20 && FireBlasts.Timing(p).Frames<timing.Frames,"Promoted primary IAS failed to accelerate S2 with unchanged average WSM");
        p.Delete();
    }
    private static void Check()
    {
        Goldens();Equipment();BoundsAndLifecycle();var p=Fresh();var t=Target(p);Require(!ClassCombat.Available(p,CombatAbility.FireBlast),"Unlearned gate");Train(p,CombatAbility.FireBlast);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FireBlast) && SkillBindings.Instance.Assign(p,false,CombatAbility.FireBlast) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.FireBlast),"Both mouse/quick bindings");
        Ready(p);NecromancerEffects.SetMana256(p,767);Require(!ClassCombat.Perform(p,CombatAbility.FireBlast,null,t.Location,Core.TickCount),"Insufficient mana admission");
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.FireBlast,null,t.Location,Core.TickCount),"Cancel begin");long when=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,when) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled payment");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireBlast,null,t.Location,Core.TickCount),"Lost mana begin");when=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,when),"Lost mana release");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.FireBlast,null,t.Location,Core.TickCount),"Move begin");when=ClassCombat.For(p).EffectAt;p.Direction=Direction.East;Require(p.Move(Direction.East) && !ClassCombat.ResolveAction(p,when),"Movement cancellation");t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        var old=FireBlasts.Roll;try
        {
            int draws=0;FireBlasts.Roll=span=>{draws++;return 0;};int hp=SkillHealth.Balance256(t);var b=Cast(p,t,held:true);
            Require(!b.Ground && b.Min256==0 && hp==SkillHealth.Balance256(t),"No instantaneous/air packet damage");
            var center=t.Location;t.MoveToWorld(new(t.X+5,t.Y,t.Z),t.Map);Run(p,b);Require(SkillHealth.Balance256(t)==hp && draws==1,"Moving escape / single empty area draw");
            t.MoveToWorld(center,t.Map);var other=Target(p,4,1);var outside=Target(p,7,0);var immune=Target(p,4,-1);immune.SetResistance(ResistanceType.Fire,100);
            var ally=Fresh();ally.MoveToWorld(new(p.X+4,p.Y+1,p.Z),p.Map);int ah=SkillHealth.Balance256(ally),oh=SkillHealth.Balance256(other),outHp=SkillHealth.Balance256(outside),ih=SkillHealth.Balance256(immune);draws=0;
            b=Cast(p,point:center);FireBlasts.Tick(b.NextUtc.AddMilliseconds((b.Remaining-1)*40L));var ground=FireBlasts.Snapshot().Single(x=>x.Id==b.Id);
            Require(ground.Ground && ground.Remaining==5 && ground.Min256==768 && ground.Max256==1024 && SkillHealth.Balance256(t)==hp,"Separate five-frame landed child");
            FireBlasts.Tick(ground.NextUtc.AddMilliseconds(119));Require(SkillHealth.Balance256(t)==hp,"Premature ground impact");
            Run(p,ground);Require(SkillHealth.Balance256(t)==hp-768 && SkillHealth.Balance256(other)==oh-768 && SkillHealth.Balance256(outside)==outHp && SkillHealth.Balance256(immune)==ih && SkillHealth.Balance256(ally)==ah && draws==1,"Shared roll / radius / immunity / friendly filter");
            Run(p,ground);Require(SkillHealth.Balance256(t)==hp-768,"Expired bomb replay");other.Delete();outside.Delete();immune.Delete();ally.Delete();
            // An enemy on the travel line must not trigger hit36 or take damage.
            var mid=Target(p,2,0);int mh=SkillHealth.Balance256(mid);b=Cast(p,point:new(p.X+8,p.Y,p.Z));Run(p,b);Require(SkillHealth.Balance256(mid)==mh,"Midair actor collision invented");mid.Delete();
            // Read hard synergies at landing, not throw time; retain rank after landing.
            b=Cast(p,point:center);Train(p,CombatAbility.ShockWeb);FireBlasts.Tick(b.NextUtc.AddMilliseconds((b.Remaining-1)*40L));ground=FireBlasts.Snapshot().Single(x=>x.Id==b.Id);Require(ground.Min256==852 && ground.Max256==1136,"Fresh child hard synergy snapshot");Clear(p);
            Train(p,CombatAbility.FireBlast,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real soft rank helm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.FireBlast)==21,"Hard20/effective21");
            b=Cast(p,point:center);var air=b with{NextUtc=TimedEffects.Now.AddHours(1)};var b2=Cast(p,point:center);FireBlasts.Restore(FireBlasts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(b2).ToArray());FireBlasts.Tick(b2.NextUtc.AddMilliseconds((b2.Remaining-1)*40L));ground=FireBlasts.Snapshot().Single(x=>x.Id==b2.Id) with{NextUtc=TimedEffects.Now.AddHours(1)};
            FireBlasts.Restore(FireBlasts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(air).Append(ground).ToArray());
            var pending=Fresh();Train(pending,CombatAbility.FireBlast);Ready(pending);int pm=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.FireBlast,null,pending.Location,Core.TickCount),"Saved unpaid begin");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),air,ground,pending.Serial.Value,pm)));
        }finally{FireBlasts.Roll=old;}
        Natural("fire-blast-result");
    }
    private static void BoundsAndLifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.FireBlast);Ready(p);
        Require(!ClassCombat.Perform(p,CombatAbility.FireBlast,null,new(p.X+18,p.Y,p.Z),Core.TickCount),"Out-of-range admission");
        var origin=p.Location;p.MoveToWorld(new(1438,1696,0),Map.Trammel);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.FireBlast,null,p.Location,Core.TickCount),"Town admission");p.MoveToWorld(origin,Map.Trammel);
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.FireBlast,null,new(p.X+4,p.Y,p.Z),Core.TickCount),"Recovery begin");long at=ClassCombat.For(p).EffectAt;
        Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Recovery cancellation paid");
        var old=FireBlasts.Roll;FireBlasts.Roll=_=>0;
        try
        {
            var near=Target(p,3);var far=Target(p,6);int nh=SkillHealth.Balance256(near),fh=SkillHealth.Balance256(far);var b=Cast(p,point:new(p.X+8,p.Y,p.Z));var wall=new Static(0x80);
            try
            {
                wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);Require(!p.InLOS(near),"Wall fixture did not obstruct");
                for(int frame=0;frame<100 && FireBlasts.Snapshot().Any(x=>x.Id==b.Id && !x.Ground);frame++){FireBlasts.Tick(FireBlasts.Snapshot().Single(x=>x.Id==b.Id).NextUtc);}
                var landed=FireBlasts.Snapshot().Single(x=>x.Id==b.Id);bool recipient=FireBlasts.Recipient(p,near,landed);Run(p,landed);
                Require(SkillHealth.Balance256(near)==nh-768 && SkillHealth.Balance256(far)==fh,$"Barrier landing / copied-area no-LOS filter: landed={landed.X-p.X},{landed.Y-p.Y}, near={SkillHealth.Balance256(near)}/{nh-768}, far={SkillHealth.Balance256(far)}/{fh}, recipient={recipient}, packet={landed.Min256}");
            }finally{wall.Delete();near.Delete();far.Delete();Clear(p);}
            var t=Target(p);int hp=SkillHealth.Balance256(t);b=Cast(p,t);ClassCombat.Stop(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Run(p,b);
            Require(SkillHealth.Balance256(t)==hp-768,"Stop/respec cancelled committed rank1 flight");Train(p,CombatAbility.FireBlast);
            b=Cast(p,t);hp=SkillHealth.Balance256(t);p.MoveToWorld(origin,Map.Felucca);Run(p,b);Require(!FireBlasts.Snapshot().Any(x=>x.Id==b.Id) && SkillHealth.Balance256(t)==hp,"Cross-map flight retained");p.MoveToWorld(origin,Map.Trammel);
            t.Delete();
            var switching=Fresh();Train(switching,CombatAbility.FireBlast);b=Cast(switching);Ready(switching);Require(ClassCreation.Profiles.TryActivateHistory(switching,DiabloClass.Paladin),"Idle class history transition");Run(switching,b);Require(!FireBlasts.Snapshot().Any(x=>x.Id==b.Id),"Foreign class retained Assassin flight");switching.Delete();
        }finally{Clear(p);p.Delete();FireBlasts.Roll=old;}
    }
    private static void Natural(string marker)
    {
        var p=Fresh();Train(p,CombatAbility.FireBlast);var t=Target(p);int hp=SkillHealth.Balance256(t);var b=Cast(p,t);
        Later(1600,marker,()=>{Require(!FireBlasts.Snapshot().Any(x=>x.Id==b.Id) && SkillHealth.Balance256(t)<hp,"Natural scheduled flight/landing/impact");t.Delete();p.Delete();});
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(SkillMechanics.Rank(p,CombatAbility.FireBlast)==21 && World.FindItem((Serial)s.Helm)?.Parent==p && NecromancerEffects.ManaBalance256(p)==s.Mana && SkillHealth.Balance256(t)==s.Health,"Saved rank/gear/mana/life");
        Require(FireBlasts.Snapshot().Contains(s.Air) && FireBlasts.Snapshot().Contains(s.Ground),"Saved distinct exact air/ground payloads");
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==s.PendingMana && SkillBindings.Instance.Get(p).Left==CombatAbility.FireBlast && SkillBindings.Instance.Get(p).Right==CombatAbility.FireBlast && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,9)==CombatAbility.FireBlast,"Unpaid replay/bindings");
        p.MoveToWorld(new((int)s.Air.X,(int)s.Air.Y,s.Air.Z),Map.Maps[s.Air.Map]); // Saved offline players are internalized; resume after returning to their saved world.
        var old=FireBlasts.Roll;try
        {
            FireBlasts.Roll=_=>0;var due=s.Ground with{NextUtc=TimedEffects.Now};FireBlasts.Restore(FireBlasts.Snapshot().Where(x=>x.Id!=due.Id && x.Id!=s.Air.Id).Append(due).ToArray());Run(p,due);
            Require(SkillHealth.Balance256(t)==s.Health-s.Ground.Min256, $"Resumed saved ground packet: actual={SkillHealth.Balance256(t)}, expected={s.Health-s.Ground.Min256}, map={p.Map}, fire={t.FireResistance}");SkillHealth.SetBalance256(t,s.Health);
            var air=s.Air with{NextUtc=TimedEffects.Now};FireBlasts.Restore(FireBlasts.Snapshot().Append(air).ToArray());Run(p,air);Require(SkillHealth.Balance256(t)==s.Health-s.Ground.Min256,"Resumed saved air creates fresh landed packet");SkillHealth.SetBalance256(t,s.Health);
            FireBlasts.Restore(FireBlasts.Snapshot().Append(s.Air).Append(s.Ground).ToArray());
        }finally{FireBlasts.Roll=old;}
        Natural("fire-blast-reload-result");
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.FireBlast);var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.FireBlast) && SkillBindings.Instance.AssignQuick(p,9,CombatAbility.FireBlast),"Connected bindings");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+5000;bool begun=false;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected cast timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.FireBlast){begun=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature input payment");}if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(begun && NecromancerEffects.ManaBalance256(p)==mana-768,"Real ground input/mana");t.Delete();p.Delete();Log.Information("fire-blast-input-result: passed");}catch(Exception e){t.Delete();p.Delete();Log.Error(e,"fire-blast-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("fire-blast-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.FireBlast,t.X,t.Y,t.Z);Wait();});
    }
}
