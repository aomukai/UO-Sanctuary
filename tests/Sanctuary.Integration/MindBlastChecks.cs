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

public static class MindBlastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MindBlastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Helm,int Mana,int Health,ConversionAllegiance State,uint LowOwner,uint HighTarget,int HighHealth,ConversionAllegiance HighState,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mind-blast-fixtures.json");
    private static bool _creating;
    private static int _next;
    private static void Rates(){var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;Mobile.HitsRegenRateHandler=x=>_creating || x.Name?.StartsWith("MindBlastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(x)??Mobile.DefaultHitsRate;Mobile.ManaRegenRateHandler=x=>_creating || x.Name?.StartsWith("MindBlastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();Register("mind-blast-check",Check);Register("mind-blast-reload-check",Reload);Register("mind-blast-input-check",Input);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{s.Target,s.HighTarget}){if(World.FindMobile((Serial)id) is BaseCreature c){Stop(c);}}}};
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Mind Blast damage, conversion, life restoration and persistence",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}});
    private static PlayerMobile Fresh(int level=99,DiabloClass characterClass=DiabloClass.Assassin)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x191,Name="MindBlastFixture",Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,characterClass);if(level>1){ClassCreation.Profiles.AdvanceLevel(p,level);}
        for(int i=0;i<100;i++)
        {
            try{FixtureSpace.Place(p,new(6000,1950+(_next++%50)*32,0),Map.Trammel);}catch(InvalidOperationException){continue;}
            bool clear=true;foreach(var other in p.Map.GetMobilesInRange<Mobile>(p.Location,20)){if(other!=p){clear=false;break;}}
            var end=new Point3D(p.X+8,p.Y,p.Z);
            if(!clear || !p.Map.CanSpawnMobile(end) || !p.InLOS(end)){continue;}
            p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
        }
        p.Delete();throw new InvalidOperationException("No independent Mind Blast corridor");
    }
    private static void Train(PlayerMobile p,CombatAbility a,int rank=1)
    {
        var row=SkillMechanics.For(a);foreach(string key in new[]{"reqskill1","reqskill2","reqskill3"}){if(row.Text(key) is{} text && text!="" && text!="0"){Train(p,SkillMechanics.Source(text).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+a);}
    }
    private static void Stop(BaseCreature t){t.Combatant=null;t.FocusMob=null;t.AIObject?.AITimer.Stop();}
    private static Skeleton Target(PlayerMobile p,int dx=4,int dy=0)
    {
        Skeleton t;_creating=true;try{t=new Skeleton{Name="MindBlastFixtureTarget",NoKillAwards=true};}finally{_creating=false;}
        t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);Stop(t);t.SetHits(20000);t.SetResistance(ResistanceType.Physical,0);return t;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static SkillValues Values(int rank){var row=SkillMechanics.For(CombatAbility.MindBlast);return new(row,k=>k==row.SourceKey?rank:0,_=>0,99);}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.MindBlast);Require(row.SourceId==273 && row.Number("srvdofunc")==51 && !ClassAbilities.SelfTarget(row.Ability),"Pinned do51 target identity");
        foreach(var (rank,min,max,chance) in new[]{(1,10,20,18),(3,14,24,24),(20,96,106,36),(21,104,114,36),(255,1976,1986,40)})
        {
            var v=Values(rank);Require(v.PhysicalDamage256(false)==min*256 && v.PhysicalDamage256(true)==max*256 && v.DurationFrames==50+5*(rank-1) && v.Mana256==3840 && MindBlasts.Chance(v)==chance,"Independent rank golden "+rank);
            int calls=0;Require(MindBlasts.Damage(v,span=>{calls++;Require(span==(max-min)*256,"Damage span");return 0;}).Physical==min && calls==1 && NamedElements.To256(MindBlasts.Damage(v,span=>span-1).Physical)==max*256-1,"Single physical FP256 exclusive draw");
        }
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"mind-blast-source-probe.json")));int count=0;
        foreach(var item in d.RootElement.GetProperty("chance").EnumerateArray()){int[] x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(x[0]==1 && x[1]>0){Require(MindBlasts.Chance(Values(x[1]))==x[2],"Complete source chance helper");count++;}}Require(count==255,"Chance coverage");
        count=0;foreach(var item in d.RootElement.GetProperty("callback").EnumerateArray())
        {
            int[] x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(x[0]!=0 || x[7]!=1 || x[2]<=x[1]){continue;}
            var life=PaladinConversion.Scale(x[2],1000,x[1],out int current,1000*x[3]/100);
            Require(current==x[10] && life.Maximum256==x[11] && life.Level==x[9] && life.OriginalLevel==x[17] && life.OriginalMaximum==x[18],"Full callback higher-level scaling differential");count++;
        }
        Require(count>0,"Missing higher-level source comparisons");count=0;
        foreach(var item in d.RootElement.GetProperty("expiry").EnumerateArray())
        {int[] x=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(x[0]==0){continue;}Require(PaladinConversion.RestoredLife256(new(80,4000,30,256),x[1]*256,x[2]*256)==x[4] && x[3]==80 && x[5]==4000*256,"Full removal callback current-life clamp differential");count++;}Require(count==20,"Removal coverage");
        var tiny=PaladinConversion.Scale(99,1,1,out int tinyHp,1);Require(tiny.Maximum256==1 && tinyHp==1,"Source minimum is one FP256 unit, not one whole hitpoint");
    }
    private static void Cast(PlayerMobile p,Mobile target=null,Point3D? point=null,bool held=false)
    {
        Ready(p);var at=point??target?.Location??p.Location;int mana=NecromancerEffects.ManaBalance256(p);
        Require(held?ClassCombat.ContinueCast(p,CombatAbility.MindBlast,target,at,Core.TickCount):ClassCombat.Perform(p,CombatAbility.MindBlast,target,at,Core.TickCount),"Cast begin");
        if(held){Require(ClassCombat.ContinueCast(p,CombatAbility.MindBlast,target,at,Core.TickCount),"Held refresh");}ClassCombat.ReleaseCast(p);
        var s=ClassCombat.For(p);Require(s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana && !ClassCombat.ResolveAction(p,s.EffectAt-1),"Unpaid SC windup");
        Require(ClassCombat.ResolveAction(p,s.EffectAt) && NecromancerEffects.ManaBalance256(p)==mana-3840 && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only15mana release");
    }
    private static ConversionAllegiance Hold(BaseCreature t)
    {
        var state=PaladinConversion.Snapshot().Single(x=>x.Target==t.Serial.Value) with{UntilUtc=TimedEffects.Now.AddHours(1)};
        PaladinConversion.Restore(PaladinConversion.Snapshot().Where(x=>x.Target!=state.Target).Append(state).ToArray());return state; // Inspection hold only; production duration is150..249frames.
    }
    private static void Expire(BaseCreature t)
    {PaladinConversion.Restore(PaladinConversion.Snapshot().Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray());PaladinConversion.Prune();}
    private static void Protection(PlayerMobile p,BaseCreature t)
    {
        int hp=SkillHealth.Balance256(t);Require(PaladinConversion.Active(t) && ClassCombat.Friendly(p,t) && !ClassCombat.IsHostile(p,t) && t.IsFriend(p) && !t.IsEnemy(p) && !p.CanBeHarmful(t,false),"Shared friendly allegiance");
        Require(AOS.Damage(t,p,100,100,0,0,0,0)==0 && SkillCombat.Hit(p,t,100,"fire")==0 && SkillHealth.Balance256(t)==hp,"Converted ally hurt by ordinary attack/spell");
        var enemy=new Ogre{NoKillAwards=true};enemy.MoveToWorld(new(t.X+1,t.Y,t.Z),t.Map);Stop(enemy);try{Require(t.IsEnemy(enemy) && enemy.IsEnemy(t) && t.GetDamageMaster(enemy)==p,"Shared enemy/damage-owner allegiance");}finally{enemy.Delete();}
    }
    private static void AuraBoundary()
    {
        var p=Fresh();var t=Target(p);var supplier=Fresh(characterClass:DiabloClass.Barbarian);var sorc=Fresh(characterClass:DiabloClass.Sorceress);
        try
        {
            Train(supplier,CombatAbility.Shout);Train(supplier,CombatAbility.BattleOrders);Train(supplier,CombatAbility.BattleCommand);Train(sorc,CombatAbility.Enchant);
            SorceressEffects.CastEnchant(sorc,t,SkillMechanics.Values(sorc,CombatAbility.Enchant));var enchant=SorceressEffects.Enchantment(t);
            BattleOrders.Apply(supplier,t,1,TimedEffects.Now);BarbarianShout.Apply(supplier,t,1,TimedEffects.Now);BattleCommand.Apply(supplier,t,1,TimedEffects.Now);
            Require(BattleOrders.Percent(t)>0 && BarbarianShout.State(t)!=null && BattleCommand.State(t)!=null && enchant!=null,"Aura/Enchant fixture suppliers");
            Require(PaladinConversion.ApplyMindBlast(p,t,150) && BattleOrders.Percent(t)==0 && BarbarianShout.State(t)==null && BattleCommand.State(t)==null && SorceressEffects.Enchantment(t)==enchant,"Entry aura mask removed unrelated Enchant or retained auras");
            BattleOrders.Apply(supplier,t,1,TimedEffects.Now);BarbarianShout.Apply(supplier,t,1,TimedEffects.Now);BattleCommand.Apply(supplier,t,1,TimedEffects.Now);
            Require(!PaladinConversion.ApplyMindBlast(sorc,t,249),"Friendly converted target acquired by another caster");Expire(t);
            Require(BattleOrders.Percent(t)==0 && BarbarianShout.State(t)==null && BattleCommand.State(t)==null && SorceressEffects.Enchantment(t)==enchant,"Removal aura mask boundary");
        }
        finally{t.Delete();p.Delete();supplier.Delete();sorc.Delete();PaladinConversion.Prune();}
    }
    private static void Check()
    {
        Goldens();AuraBoundary();var p=Fresh();var t=Target(p);Require(!ClassCombat.Available(p,CombatAbility.MindBlast),"Unlearned availability");Train(p,CombatAbility.MindBlast);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.MindBlast) && SkillBindings.Instance.Assign(p,false,CombatAbility.MindBlast) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.MindBlast),"Right/quick-only binding");
        var old=MindBlasts.Roll;MindBlasts.Roll=_=>0;
        try
        {
            Ready(p);NecromancerEffects.SetMana256(p,3839);Require(!ClassCombat.Perform(p,CombatAbility.MindBlast,t,t.Location,Core.TickCount),"Insufficient mana cast");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.MindBlast,t,t.Location,Core.TickCount),"Cancellation begin");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Cancelled cast paid");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MindBlast,t,t.Location,Core.TickCount),"Lost mana begin");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at),"Lost mana delivered");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MindBlast,t,t.Location,Core.TickCount),"Recovery begin");at=ClassCombat.For(p).EffectAt;mana=NecromancerEffects.ManaBalance256(p);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana,"Recovery failed cancellation");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.MindBlast,t,t.Location,Core.TickCount),"Movement begin");at=ClassCombat.For(p).EffectAt;p.Direction=Direction.East;Require(p.Move(Direction.East) && !ClassCombat.ResolveAction(p,at),"Movement failed cancellation");t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
            Cast(p,point:new Point3D(p.X-4,p.Y,p.Z));Require(!PaladinConversion.Active(t),"Empty-ground cast found distant victim");
            var t2=Target(p,5);var outside=Target(p,7);try
            {
                int hp=SkillHealth.Balance256(t),hp2=SkillHealth.Balance256(t2),hp3=SkillHealth.Balance256(outside),draws=0;
                MindBlasts.Roll=span=>{draws++;return 0;};Cast(p,point:t.Location,held:true);
                Require(draws==5 && SkillHealth.Balance256(t)==hp-2560 && SkillHealth.Balance256(t2)==hp2-2560 && SkillHealth.Balance256(outside)==hp3,"Shared cast packet/radius/per-target draws");
                Require(CombatStuns.Remaining(t)==0 && SourceKnockbacks.Remaining(t)==0 && CombatStuns.Remaining(t2)==0 && SourceKnockbacks.Remaining(t2)==0,"Conversion success applied stun/knockback");Protection(p,t);Expire(t);Expire(t2);
                Require(ClassCombat.IsHostile(p,t) && t.Combatant==null && t.FocusMob==null && t.IsEnemy(p),"Expiry grace or stale target");
            }finally{t2.Delete();outside.Delete();}
            MindBlasts.Roll=span=>span==100?99:0;int before=SkillHealth.Balance256(t);Cast(p,t);
            Require(!PaladinConversion.Active(t) && SkillHealth.Balance256(t)==before-2560 && CombatStuns.Remaining(t)>0 && SourceKnockbacks.Remaining(t)>0,"Failed conversion damage/stun/knockback");CombatStuns.Clear(t);SourceKnockbacks.Clear(t);CreatureProfiles.Session(t).RecoveryUntil=Core.TickCount;
            t.SetResistance(ResistanceType.Physical,100);before=SkillHealth.Balance256(t);Cast(p,t);Require(SkillHealth.Balance256(t)==before && CombatStuns.Remaining(t)>0,"Physical immunity suppressed stun");t.SetResistance(ResistanceType.Physical,0);CombatStuns.Clear(t);SourceKnockbacks.Clear(t);
            MindBlasts.Roll=span=>span==100?MindBlasts.Chance(Values(1)):0;Cast(p,t);Require(PaladinConversion.Active(t),"Inclusive conversion equality failed");Expire(t);
            var loc=p.Location;FixtureSpace.Place(p,new(1438,1696,0),Map.Trammel);Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.MindBlast,null,p.Location,Core.TickCount),"Town cast");p.MoveToWorld(loc,p.Map);
            Ready(p);Require(!ClassCombat.Perform(p,CombatAbility.MindBlast,null,new(p.X+18,p.Y,p.Z),Core.TickCount),"Out-of-range ground");
            var low=Fresh(1);var high=Target(low,2);int originalLevel=ClassStats.Level(high),originalMax=high.HitsMax;SkillHealth.SetBalance256(high,originalMax*128+111);
            Require(originalLevel>1 && !PaladinConversion.Eligible(low,high) && PaladinConversion.ApplyMindBlast(low,high,249),"Separate higher-level Mind Blast consumer / retained Paladin gate");
            Require(ClassStats.Level(high)==1 && high.HitsMax==originalMax/originalLevel && SkillHealth.Balance256(high)==originalMax/2/originalLevel*256,"Real temporary level/max/current pools");Protection(low,high);
            var highState=Hold(high);int highHealth=SkillHealth.Balance256(high);
            Train(p,CombatAbility.MindBlast,20);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Soft-rank helm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.MindBlast)==21 && !ClassCreation.Profiles.TryTrain(p.Serial,"assassin.mindblast",out _),"Hard20/effective21 cap");
            MindBlasts.Roll=_=>0;before=SkillHealth.Balance256(t);Cast(p,t);Require(SkillHealth.Balance256(t)==before-104*256,"Current success damage at effective21");var state=Hold(t);
            var pending=Fresh();Train(pending,CombatAbility.MindBlast);Ready(pending);int pendingMana=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.MindBlast,null,pending.Location,Core.TickCount),"Saved unpaid begin");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,helm.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),state,low.Serial.Value,high.Serial.Value,highHealth,highState,pending.Serial.Value,pendingMana)));
            Natural("mind-blast-result");
        }finally{MindBlasts.Roll=old;}
    }
    private static void Natural(string marker)
    {
        var p=Fresh();Train(p,CombatAbility.MindBlast);var t=Target(p);var old=MindBlasts.Roll;try{MindBlasts.Roll=_=>0;Cast(p,t);}finally{MindBlasts.Roll=old;}
        var state=PaladinConversion.Snapshot().Single(x=>x.Target==t.Serial.Value);Require(state.UntilUtc>TimedEffects.Now.AddSeconds(5.8) && state.UntilUtc<=TimedEffects.Now.AddSeconds(6),"Natural duration150frames");
        int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);
        Later(6250,marker,()=>{try{Require(!PaladinConversion.Active(t) && ClassCombat.IsHostile(p,t) && SkillHealth.Balance256(t)==hp && NecromancerEffects.ManaBalance256(p)==mana,"Natural expiry repeated damage/payment or retained allegiance");}finally{t.Delete();p.Delete();PaladinConversion.Prune();}});
    }
    private static void Reload()
    {
        Goldens();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(Skeleton)World.FindMobile((Serial)s.Target);var high=(Skeleton)World.FindMobile((Serial)s.HighTarget);var low=(PlayerMobile)World.FindMobile((Serial)s.LowOwner);var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);
        Require(SkillMechanics.Rank(p,CombatAbility.MindBlast)==21 && ClassCreation.Profiles.GetRank(p.Serial,"assassin.mindblast")==20 && World.FindItem((Serial)s.Helm)?.Parent==p && NecromancerEffects.ManaBalance256(p)==s.Mana && SkillHealth.Balance256(t)==s.Health,"Saved rank/gear/mana/current life");
        Require(PaladinConversion.Snapshot().Single(x=>x.Target==t.Serial.Value)==s.State && PaladinConversion.Snapshot().Single(x=>x.Target==high.Serial.Value)==s.HighState && SkillHealth.Balance256(high)==s.HighHealth && ClassStats.Level(high)==1,"Saved allegiance/higher-level temporary stats");Protection(p,t);Protection(low,high);
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==s.PendingMana && SkillBindings.Instance.Get(p).Right==CombatAbility.MindBlast && SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,8)==CombatAbility.MindBlast,"Saved input/unpaid replay");
        var legacy=JsonSerializer.Deserialize<ConversionAllegiance>("{\"Target\":1,\"Source\":2,\"UntilUtc\":\"2026-10-24T00:00:00Z\"}");Require(legacy.Skill==CombatAbility.Conversion && legacy.Life==null,"Legacy version0 allegiance defaults");
        var life=s.HighState.Life;int expected=PaladinConversion.RestoredLife256(life,SkillHealth.Maximum256(high),SkillHealth.Balance256(high));Expire(high);
        Require(ClassStats.Level(high)==life.OriginalLevel && high.HitsMax==life.OriginalMaximum && SkillHealth.Balance256(high)==expected,"Reloaded exact source restoration clamp");
        PaladinConversion.Restore(PaladinConversion.Snapshot().Append(s.HighState).ToArray());SkillHealth.SetBalance256(high,s.HighHealth); // Retain fixture for the second reload, not a production refresh rule.
        Natural("mind-blast-reload-result");
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.MindBlast);var t=Target(p);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.MindBlast) && SkillBindings.Instance.AssignQuick(p,8,CombatAbility.MindBlast),"Connected bindings");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t);long deadline=Core.TickCount+5000;bool begun=false;var old=MindBlasts.Roll;MindBlasts.Roll=_=>0;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected cast timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.MindBlast){begun=true;Require(NecromancerEffects.ManaBalance256(p)==mana,"Premature connected payment");}if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(begun && NecromancerEffects.ManaBalance256(p)==mana-3840 && PaladinConversion.Active(t) && CombatStuns.Remaining(t)==0 && SourceKnockbacks.Remaining(t)==0,$"Real ground input: begun={begun}, mana={NecromancerEffects.ManaBalance256(p)}/{mana-3840}, hp={SkillHealth.Balance256(t)}/{hp}, converted={PaladinConversion.Active(t)}, stun={CombatStuns.Remaining(t)}, kb={SourceKnockbacks.Remaining(t)}");MindBlasts.Roll=old;t.Delete();p.Delete();PaladinConversion.Prune();Log.Information("mind-blast-input-result: passed");}catch(Exception e){MindBlasts.Roll=old;t.Delete();p.Delete();PaladinConversion.Prune();Log.Error(e,"mind-blast-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("mind-blast-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.MindBlast,t.X,t.Y,t.Z);Wait();});
    }
}
