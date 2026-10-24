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

public static class ShockWebChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShockWebChecks));
    private sealed record Saved(uint Owner,uint Target,Point3D TargetLocation,uint Helm,int Mana,int Health,ShockShard Air,ShockShard Field,WeaponFlightLease Lease,FireCooldown Cooldown,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shock-web-fixtures.json");
    private static bool _creating;
    private static int _next;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("ShockWebFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("ShockWebFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();Register("shock-web-check",Check);Register("shock-web-reload-check",Reload);Register("shock-web-input-check",Input);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)s.Target) is BaseCreature c){Stop(c);c.Frozen=true;}}};
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shock Web damage, conversion, life restoration and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh(int level=99,DiabloClass characterClass=DiabloClass.Assassin)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="ShockWebFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,characterClass);if(level>1){ClassCreation.Profiles.AdvanceLevel(p,level);}
        for(int i=0;i<100;i++)
        {
            try{FixtureSpace.Place(p,new(6000,1950+(_next++%50)*32,0),Map.Trammel);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var other in p.Map.GetMobilesInRange<Mobile>(p.Location,20)){if(other!=p){clear=false;break;}}
            var end=new Point3D(p.X+8,p.Y,p.Z);
            if(!clear || !p.Map.CanSpawnMobile(end) || !p.InLOS(end)){continue;}
            p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
        }
        p.Delete();throw new InvalidOperationException("No independent Shock Web corridor");
    }
    private static void Train(PlayerMobile p,CombatAbility a,int rank=1)
    {
        var row=SkillMechanics.For(a);foreach(string key in new[]{"reqskill1","reqskill2","reqskill3"}){if(row.Text(key) is{} text && text!="" && text!="0"){Train(p,SkillMechanics.Source(text).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+a);}
    }
    private static void Stop(BaseCreature t){t.Combatant=null;t.FocusMob=null;t.AIObject?.AITimer.Stop();}
    private static Skeleton Target(PlayerMobile p,int dx=4,int dy=0)
    {
        Skeleton t;_creating=true;try{t=new Skeleton{Name="ShockWebFixtureTarget",NoKillAwards=true};}finally{_creating=false;}
        t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);Stop(t);t.SetHits(20000);t.SetResistance(ResistanceType.Energy,0);return t;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);DelayClear(p);}
    private static void DelayClear(PlayerMobile p)
    {var state=FireGround.Snapshot();FireGround.Restore(state with{Cooldowns=state.Cooldowns.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.ShockWeb).ToArray()});}
    private static SkillValues Values(int rank,int hard=0,int fire=1,int softFire=99)
    {
        var row=SkillMechanics.For(CombatAbility.ShockWeb);return new(row,k=>k==row.SourceKey?rank:k=="Fire Trauma"?softFire:0,
            k=>k=="Charged Bolt Sentry"?Math.Min(hard,20):k=="Lightning Sentry"?Math.Max(0,hard-20):k=="Fire Trauma"?fire:0,99);
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.ShockWeb);Require(row.SourceId==256 && row.Number("srvdofunc")==43 && row.Number("localdelay")==15,"Pinned do43/local delay");
        foreach(var (rank,max,count) in new[]{(1,2560,6),(3,4096,6),(20,30464,11),(21,33024,11),(255,1278976,69)})
        {var v=Values(rank);Require(v.Damage256(false)==256 && v.Damage256(true)==max && v.Mana256==1536 && ShockWebs.Count(v)==count,"Independent damage/count/mana golden "+rank);}
        Require(Values(1,1).Damage256(false)==256 && Values(1,1).Damage256(true)==2995 && ShockWebs.Count(Values(20,0,20))==17,"Fixed minimum/hard synergies versus hard count");
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shock-web-source-probe.json")));int compared=0;
        foreach(var r in d.RootElement.EnumerateArray())
        {
            if(r[0].GetInt32()==0 || r[1].GetInt32()==0 || r[2].GetInt32()<0){continue;}
            int rank=r[3].GetInt32(),hard=r[4].GetInt32(),count=6+rank/4+hard/3;
            var actual=ShockWebs.Scatter(100,200,r[5].GetInt32(),r[6].GetInt32(),count,count/4+1);
            var expected=r[13].EnumerateArray().Select(p=>new Point2D(p[0].GetInt32(),p[1].GetInt32())).ToArray();Require(actual.SequenceEqual(expected),"Full source scatter/seed comparison");compared++;
        }Require(compared==840,"Missing scatter comparisons");
        using var dmg=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shock-web-damage-source-probe.json")));var names=dmg.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();compared=0;
        foreach(var r in dmg.RootElement.GetProperty("cases").EnumerateArray())
        {
            if(r[0].GetInt32()!=1 || r[3].GetInt32()!=0 || r[4].GetInt32()!=0){continue;}
            var v=Values(r[1].GetInt32(),r[2].GetInt32());Require(v.Damage256(false)==r[5][Array.IndexOf(names,"nLightMinDamage")].GetInt32() && v.Damage256(true)==r[5][Array.IndexOf(names,"nLightMaxDamage")].GetInt32(),"Full creator/getter comparison");compared++;
        }Require(compared==48,"Missing damage comparisons");
        var field=new ShockShard(1,1,1,0,1,0,0,0,0,0,90,true,TimedEffects.Now,256,512,10,50,25,0,0);var packet=ShockWebs.Packet(field,256);
        Require(NamedElements.To256(packet.Fire)==38 && NamedElements.To256(packet.Cold)==32 && NamedElements.To256(packet.Lightning)==281 && NamedElements.To256(packet.Poison)==25,"T29 keeps original lightning and separate mastered additions");
    }
    private static ShockShard[] Own(PlayerMobile p)=>ShockWebs.Snapshot().Shards.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static void Keep(PlayerMobile p,params ShockShard[] fields)
    {
        var saved=ShockWebs.Snapshot();var casts=Own(p).Select(x=>x.Cast).ToHashSet();ShockWebs.Restore(saved with{Shards=saved.Shards.Where(x=>x.Owner!=p.Serial.Value).Concat(fields).ToArray(),Leases=saved.Leases.Where(x=>!casts.Contains(x.Cast)).ToArray()});
    }
    private static ShockShard Field(PlayerMobile p,long cast,int dx=4)=>new(ShockWebs.Snapshot().NextId+1,cast,p.Serial.Value,p.Map.MapID,1,p.X+dx,p.Y,p.Z,0,0,90,true,TimedEffects.Now.AddMilliseconds(40),256,512);
    private static ShockShard[] Cast(PlayerMobile p,Mobile target=null,Point3D? point=null,bool held=false)
    {
        Ready(p);var aim=point??target?.Location??new Point3D(p.X+4,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);long before=ShockWebs.Snapshot().NextCast;
        Require(held?ClassCombat.ContinueCast(p,CombatAbility.ShockWeb,target,aim,Core.TickCount):ClassCombat.Perform(p,CombatAbility.ShockWeb,target,aim,Core.TickCount),"Begin learned S2 throw");
        if(held){Require(ClassCombat.ContinueCast(p,CombatAbility.ShockWeb,target,aim,Core.TickCount),"Held throw refresh");}ClassCombat.ReleaseCast(p);var session=ClassCombat.For(p);
        Require(session.Animation==9 && session.Timing==LobProjectiles.Timing(p) && NecromancerEffects.ManaBalance256(p)==mana && !ClassCombat.ResolveAction(p,session.EffectAt-1),"Unpaid windup");
        Require(ClassCombat.ResolveAction(p,session.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-1536 && !ClassCombat.ResolveAction(p,session.EffectAt),"Once-only6mana release");
        var shots=Own(p).Where(x=>x.Cast>before).ToArray();Require(shots.Length>0 && shots.All(x=>!x.Ground && x.Min256==0 && x.Cast==shots[0].Cast),"Airborne pure packets/shared cast identity");return shots;
    }
    private static void Fields()
    {
        var p=Fresh();Train(p,CombatAbility.ShockWeb);var t=Target(p);var old=ShockWebs.Roll;int calls=0;ShockWebs.Roll=_=>{calls++;return 0;};
        try
        {
            var first=Field(p,ShockWebs.Snapshot().NextCast+1);Keep(p,first);int hp=SkillHealth.Balance256(t);
            ShockWebs.Tick(first.NextUtc);Require(SkillHealth.Balance256(t)==hp-256 && CreatureHitRecovery.Remaining(t)==0,"Initial SoftHit without recovery");
            ShockWebs.Tick(first.NextUtc.AddMilliseconds(999));Require(SkillHealth.Balance256(t)==hp-256,"25-frame NHD opened early");
            ShockWebs.Tick(first.NextUtc.AddMilliseconds(1000));Require(SkillHealth.Balance256(t)==hp-512,"25-frame equality boundary");
            ShockWebs.Tick(first.NextUtc.AddMilliseconds(3560));Require(Own(p).Length==0 && SkillHealth.Balance256(t)==hp-1024 && calls==4,"Four source contacts / no extra expiry hit");
            first=Field(p,ShockWebs.Snapshot().NextCast+1);var second=first with{Id=first.Id+1};Keep(p,first,second);hp=SkillHealth.Balance256(t);calls=0;ShockWebs.Tick(first.NextUtc.AddSeconds(4));Require(SkillHealth.Balance256(t)==hp-1024 && calls==4,"Same-throw bomblets failed to share NHD");
            first=Field(p,ShockWebs.Snapshot().NextCast+1);second=first with{Id=first.Id+1,Cast=first.Cast+1};Keep(p,first,second);hp=SkillHealth.Balance256(t);ShockWebs.Tick(first.NextUtc.AddSeconds(4));Require(SkillHealth.Balance256(t)==hp-2048,"Distinct native origin/cast groups shared a window");
            var another=Target(p);first=Field(p,ShockWebs.Snapshot().NextCast+1);Keep(p,first);hp=SkillHealth.Balance256(t);int ah=SkillHealth.Balance256(another);ShockWebs.Tick(first.NextUtc.AddSeconds(4));Require(SkillHealth.Balance256(t)==hp-1024 && SkillHealth.Balance256(another)==ah-1024,"NHD recipient was not filtered before first-collider selection");another.Delete();
            t.SetResistance(ResistanceType.Energy,100);first=Field(p,ShockWebs.Snapshot().NextCast+1);Keep(p,first);hp=SkillHealth.Balance256(t);calls=0;ShockWebs.Tick(first.NextUtc.AddSeconds(4));Require(SkillHealth.Balance256(t)==hp && calls==4,"Immunity bypassed NHD or lost mitigation");t.SetResistance(ResistanceType.Energy,0);
            var wall=new Static(0x80);try
            {first=Field(p,ShockWebs.Snapshot().NextCast+1);Keep(p,first);wall.MoveToWorld(t.Location,p.Map);hp=SkillHealth.Balance256(t);ShockWebs.Tick(first.NextUtc);Require(Own(p).Length==1 && SkillHealth.Balance256(t)==hp-256,"Barrier took priority over valid collider");ShockWebs.Tick(first.NextUtc.AddMilliseconds(40));Require(Own(p).Length==0,"Barrier retained field without eligible collider");}finally{wall.Delete();}
        }finally{Keep(p);t.Delete();p.Delete();ShockWebs.Roll=old;}
    }
    private static void Check()
    {
        Goldens();Fields();var p=Fresh();var t=Target(p);Require(!ClassCombat.Available(p,CombatAbility.ShockWeb),"Unlearned skill available");Train(p,CombatAbility.ShockWeb);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.ShockWeb) && SkillBindings.Instance.Assign(p,false,CombatAbility.ShockWeb) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.ShockWeb),"Left/right/quick binding");
        Ready(p);NecromancerEffects.SetMana256(p,1535);Require(!ClassCombat.Perform(p,CombatAbility.ShockWeb,null,t.Location,Core.TickCount),"Insufficient mana admission");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.ShockWeb,null,t.Location,Core.TickCount),"Cancellation begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast paid");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ShockWeb,null,t.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(p).EffectAt;Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at),"Hit recovery did not cancel");
        Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ShockWeb,null,new(p.X+18,p.Y,p.Z),Core.TickCount),"Range gate");var origin=p.Location;p.MoveToWorld(new(1438,1696,0),Map.Trammel);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.ShockWeb,null,p.Location,Core.TickCount),"Town gate");p.MoveToWorld(origin,Map.Trammel);
        var shots=Cast(p,t,held:true);int hp=SkillHealth.Balance256(t);Require(!FireGround.Ready(p,CombatAbility.ShockWeb) && FireGround.Ready(p,CombatAbility.FireBlast),"Skill-local delay leaked or absent");ClassCombat.For(p).NextAction=Core.TickCount;Require(!ClassCombat.Perform(p,CombatAbility.ShockWeb,null,t.Location,Core.TickCount),"15-frame delay bypassed by action clock");
        var expected=ShockWebs.Scatter(p.X*2,p.Y*2,t.X*2,t.Y*2,6,2);Require(shots.Length==expected.Length,"Native release/source scatter count");
        t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);ShockWebs.Tick(shots.Min(x=>x.NextUtc).AddSeconds(5));Require(SkillHealth.Balance256(t)==hp && Own(p).Length==0,"Moving escape/natural lifetime");t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        shots=Cast(p,t);Train(p,CombatAbility.ChargedBoltSentry);var earliest=shots.Min(x=>x.NextUtc.AddMilliseconds((x.Remaining-1)*40L));ShockWebs.Tick(earliest);var child=Own(p).First(x=>x.Ground);Require(child.Min256==256 && child.Max256==2995,"Fresh child hard synergy / fixed minimum");Keep(p);
        Train(p,CombatAbility.ShockWeb,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real soft-rank helm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.ShockWeb)==21,"Hard20/effective21");
        var air=Cast(p,t)[0] with{NextUtc=TimedEffects.Now.AddHours(1)};Keep(p,air);var group=Cast(p,t);var landing=group.Min(x=>x.NextUtc.AddMilliseconds((x.Remaining-1)*40L));ShockWebs.Tick(landing);child=Own(p).First(x=>x.Ground);Keep(p,air,child);
        t.Frozen=true;Stop(t);t.MoveToWorld(new((int)Math.Round(child.X),(int)Math.Round(child.Y),child.Z),p.Map);var old=ShockWebs.Roll;try{ShockWebs.Roll=_=>0;ShockWebs.Tick(child.NextUtc);}finally{ShockWebs.Roll=old;}
        child=Own(p).Single(x=>x.Ground);var lease=ShockWebs.Snapshot().Leases.Single(x=>x.Cast==child.Cast && x.Target==t.Serial.Value);var hold=TimedEffects.Now.AddHours(1);lease=lease with{UntilUtc=hold+ (lease.UntilUtc-child.NextUtc)};child=child with{NextUtc=hold};Keep(p,air,child);var saved=ShockWebs.Snapshot();ShockWebs.Restore(saved with{Leases=saved.Leases.Append(lease).ToArray()});
        var cooldown=new FireCooldown(p.Serial.Value,CombatAbility.ShockWeb,hold);var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(x=>x.Owner!=p.Serial.Value || x.Ability!=CombatAbility.ShockWeb).Append(cooldown).ToArray()});
        var pending=Fresh();Train(pending,CombatAbility.ShockWeb);Ready(pending);int pm=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.ShockWeb,null,pending.Location,Core.TickCount),"Saved unpaid begin");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,t.Location,helm.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),air,child,lease,cooldown,pending.Serial.Value,pm)));
        Natural("shock-web-result");
    }
    private static void Natural(string marker)
    {
        var p=Fresh();Train(p,CombatAbility.ShockWeb);var t=Target(p);int hp=SkillHealth.Balance256(t);var shots=Cast(p,t);
        Later(4700,marker,()=>{Require(Own(p).Length==0 && SkillHealth.Balance256(t)<hp && CreatureHitRecovery.Remaining(t)==0 && FireGround.Ready(p,CombatAbility.ShockWeb),"Natural lob/field expiry/delay/soft-hit");t.Delete();p.Delete();});
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);var saved=ShockWebs.Snapshot();
        Require(SkillMechanics.Rank(p,CombatAbility.ShockWeb)==21 && World.FindItem((Serial)s.Helm)?.Parent==p && NecromancerEffects.ManaBalance256(p)==s.Mana && SkillHealth.Balance256(t)==s.Health && t.Location==s.TargetLocation,"Saved ranks/gear/resources/position");
        Require(saved.Shards.Contains(s.Air) && saved.Shards.Contains(s.Field) && saved.Leases.Contains(s.Lease) && FireGround.Snapshot().Cooldowns.Contains(s.Cooldown),"Saved flight/field/NHD/local-delay payloads");
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==s.PendingMana && SkillBindings.Instance.Get(p).Left==CombatAbility.ShockWeb && SkillBindings.Instance.Get(p).Right==CombatAbility.ShockWeb && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,10)==CombatAbility.ShockWeb,"Saved binding/unpaid replay");
        p.MoveToWorld(new((int)s.Air.X,(int)s.Air.Y,s.Air.Z),Map.Maps[s.Air.Map]);Require(!FireGround.Ready(p,CombatAbility.ShockWeb) && FireGround.Ready(p,CombatAbility.FireBlast),"Saved skill-local gate");
        var old=ShockWebs.Roll;try
        {
            ShockWebs.Roll=_=>0;var at=TimedEffects.Now;var field=s.Field with{NextUtc=at};var lease=s.Lease with{UntilUtc=at+(s.Lease.UntilUtc-s.Field.NextUtc)};Keep(p,field);var current=ShockWebs.Snapshot();ShockWebs.Restore(current with{Leases=current.Leases.Append(lease).ToArray()});
            ShockWebs.Tick(lease.UntilUtc.AddMilliseconds(-1));Require(SkillHealth.Balance256(t)==s.Health,"Saved NHD reopened early");ShockWebs.Tick(lease.UntilUtc);Require(SkillHealth.Balance256(t)==s.Health-s.Field.Min256 && CreatureHitRecovery.Remaining(t)==0,$"Saved deadline contact / SoftHit: actual={SkillHealth.Balance256(t)}, expected={s.Health-s.Field.Min256}, recovery={CreatureHitRecovery.Remaining(t)}, victim={t.Location}, field={s.Field.X},{s.Field.Y}, energy={t.EnergyResistance}, size={CreatureProfiles.For(t).Traits.Size}, shards={JsonSerializer.Serialize(Own(p))}, leases={JsonSerializer.Serialize(ShockWebs.Snapshot().Leases)}");SkillHealth.SetBalance256(t,s.Health);
            Keep(p,s.Air with{NextUtc=at});ShockWebs.Tick(at.AddSeconds(1));Require(Own(p).Length==1 && Own(p)[0].Ground && Own(p)[0].Rank==21,"Saved airborne lob failed to land with retained rank");
            Keep(p,s.Air,s.Field);current=ShockWebs.Snapshot();ShockWebs.Restore(current with{Leases=current.Leases.Append(s.Lease).ToArray()});Require(NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload repeated cast payment");
        }finally{ShockWebs.Roll=old;SkillHealth.SetBalance256(t,s.Health);}
        Natural("shock-web-reload-result");
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.ShockWeb);var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.ShockWeb) && SkillBindings.Instance.AssignQuick(p,10,CombatAbility.ShockWeb),"Connected bindings");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+6000;bool begun=false;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected cast timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.ShockWeb){begun=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature input debit");}if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(begun && NecromancerEffects.ManaBalance256(p)==mana-1536 && CreatureHitRecovery.Remaining(t)==0,"Actual ground throw/mana/SoftHit");Keep(p);t.Delete();p.Delete();Log.Information("shock-web-input-result: passed");}catch(Exception e){Keep(p);t.Delete();p.Delete();Log.Error(e,"shock-web-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("shock-web-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.ShockWeb,t.X,t.Y,t.Z);Wait();});
    }
}
