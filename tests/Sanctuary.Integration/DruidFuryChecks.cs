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

public sealed class FuryTarget:GraveyardZombie
{
    internal Action<PlayerMobile> AfterHit;
    internal int Impacts;
    public FuryTarget(){}
    public FuryTarget(Serial serial):base(serial){}
    public override bool CanRegenHits=>false;
    public override void OnThink(){}
    public override void OnSectorActivate()=>AIObject?.AITimer.Stop();
    public override void OnDamage(int amount,Mobile from,bool willKill)
    {Impacts++;base.OnDamage(amount,from,willKill);if(from is PlayerMobile p){AfterHit?.Invoke(p);}}
    public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
    public override void Deserialize(IGenericReader reader){base.Deserialize(reader);if(reader.ReadInt()!=0){throw new InvalidDataException("Invalid Fury fixture");}}
}
public static class DruidFuryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFuryChecks));
    private sealed record Saved(uint Owner,uint Target,int Mana,int Life,int TargetLife,DruidPlayerForm Form,int PaidRemaining);
    private sealed class Rolls(bool critical=false):Random{public override int Next(int maximum)=>critical?0:maximum-1;}
    private static int _scene;private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-fury-fixtures.json");
    public static void Configure()
    {
        Rates();Register("druid-fury-check",Check);Register("druid-fury-reload-check",Reload);Register("druid-fury-snapshot-check",()=>Snapshot(true));Register("druid-fury-stage-check",()=>FixtureNetwork.Connected("druid-fury-stage-result",Stage));
        foreach(string label in new[]{"fury-left","fury-right","fury-quick"}){Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidFuryFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Fury combat,input and paid-series persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Talent");
        foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Name="DruidFuryFixture",Body=0x190,Player=true};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,CombatAbility.Fury);Train(p,CombatAbility.Lycanthropy,20);
        FixtureSpace.Corridor(p,6360,16,1000+32*_scene++);var w=new Dagger();ClassGear.Profiles.Convert(w,1,GearRarity.Normal,new Random(173));Require(p.EquipItem(w),"Dagger");ClassGear.Refresh(p);
        Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werewolf,TimedEffects.Now),"Wolf");Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;SkillHealth.SetBalance256(p,p.HitsMax*128-17);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);}
    private static FuryTarget Target(PlayerMobile p,int dy=0)
    {
        var t=new FuryTarget{Name="DruidFuryFixtureTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+1,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}return t;
    }
    private static void Drop(PlayerMobile p){DruidPlayerForms.Clear(p);p.Corpse?.Delete();p.Delete();}
    private static void Controlled(Action action)
    {
        var hit=DruidFury.HitRoll;var element=DruidFury.ElementRoll;var physical=DruidFury.PhysicalRoll;var effects=DruidFury.Effects;var basic=DruidFormAttacks.HitRoll;var block=SourceBlocking.BlockRoll;
        try{DruidFury.HitRoll=()=>0;DruidFury.ElementRoll=()=>0;DruidFury.PhysicalRoll=_=>0;DruidFury.Effects=new Rolls();DruidFormAttacks.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;action();}
        finally{DruidFury.HitRoll=hit;DruidFury.ElementRoll=element;DruidFury.PhysicalRoll=physical;DruidFury.Effects=effects;DruidFormAttacks.HitRoll=basic;SourceBlocking.BlockRoll=block;}
    }
    private static DruidFury.Series Begin(PlayerMobile p,Mobile target=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),life=target==null?0:SkillHealth.Balance256(target);
        Require(ClassCombat.Perform(p,CombatAbility.Fury,target,target?.Location??p.Location,Core.TickCount),"Fury begin");var s=ClassCombat.For(p);var series=s.Fury;
        Require(series!=null&&s.Timing==DruidFury.Timing(p)&&s.Animation==(byte)WeaponAnimation.Wrestle&&series.Released==0,"Fury wolf A1/clock/pose");
        Require(NecromancerEffects.ManaBalance256(p)==mana-1024&&(target==null||SkillHealth.Balance256(target)==life)&&!ClassCombat.ResolveAction(p,series.EffectAt-1),"Fury early contact/start payment");return series;
    }
    private static int Finish(PlayerMobile p)
    {
        int attempts=0;
        while(ClassCombat.For(p).Fury is{Remaining:>0} s)
        {Require(attempts++<5&&ClassCombat.ResolveAction(p,s.EffectAt),"Fury contact failed/looped");}
        if(ClassCombat.For(p).Fury is{} tail)
        {Require(!ClassCombat.ResolveAction(p,tail.Until-1)&&ClassCombat.ResolveAction(p,tail.Until)&&ClassCombat.For(p).Fury==null,"Fury final whole-cycle tail");}
        return attempts;
    }
    private static void Scalars()
    {
        using var clock=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-fury-full-clock-source-probe.json")));int rows=0;
        foreach(var row in clock.RootElement.GetProperty("preparedSchedules").EnumerateArray())
        {
            var actual=DruidFury.Schedule(row.GetProperty("advance").GetInt32(),row.GetProperty("count").GetInt32());var events=row.GetProperty("events");
            Require(actual.End==row.GetProperty("end").GetInt32()&&actual.Events.Length==events.GetArrayLength()&&actual.Events.Select((x,i)=>x==events[i].GetInt32()).All(x=>x),"Fury source schedule");rows++;
        }
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-fury-source-probe.json")));int ranks=0;
        foreach(var c in source.RootElement.GetProperty("starts").EnumerateArray())
        {
            if(c[1].GetInt32()!=1||c[2].GetInt32()!=1||c[3].GetInt32()!=1||c[4].GetInt32()!=1){continue;}
            int rank=c[0].GetInt32();var values=new SkillValues(SkillMechanics.For(CombatAbility.Fury),_=>rank,_=>0,99);
            Require(values.Eval("calc1")==c[6].GetInt32()&&values.Formula("toht")==c[8].GetInt32()&&values.Mana256==c[9].GetInt32(),"Fury rank/count/AR/mana source");ranks++;
        }
        Require(rows==1996&&ranks==255,"Fury missing source cases");Log.Information("Fury:1996source clock schedules/255rank-count-AR-mana comparisons pass");
    }
    private static void Cases()
    {
        var p=Fresh();var t=Target(p);var shields=SourceBlocking.Snapshot();
        try
        {
            Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Fury)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Fury)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Fury),"Fury bindings");
            for(int rank=1;rank<=4;rank++)
            {
                Train(p,CombatAbility.Fury,rank);var s=Begin(p,t);int mana=NecromancerEffects.ManaBalance256(p),initialLife=SkillHealth.Balance256(t),impacts=t.Impacts;
                var v=SkillMechanics.Values(p,CombatAbility.Fury);var raw=GearEffects.Damage(p,0,t,(int)v.Eval("calc2"),_=>0);var packet=GearEffects.PrepareWeaponImpact(p,t,0,new Rolls(),raw);int loss=packet.LifeDamage256??NamedElements.To256(packet.Parts.Total);
                int count=Finish(p);Require(count==rank+1&&t.Impacts-impacts==count&&initialLife-SkillHealth.Balance256(t)==count*loss&&NecromancerEffects.ManaBalance256(p)==mana,"Fury count/actual ordinary physical/one payment");
                if(rank==3)
                {
                    var captured=Begin(p,t);var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);
                    Require(p.EquipItem(item),"Mid-series skill helm");ClassGear.Refresh(p);
                    Require(SkillMechanics.Rank(p,CombatAbility.Fury)==4&&ClassCombat.For(p).Fury.Remaining==4&&Finish(p)==4,"Soft rank grew captured attempt count");
                    p.AddToBackpack(item);item.Delete();ClassGear.Refresh(p);
                }
            }
            var freeAim=Begin(p);Require(Finish(p)==5,"Empty-ground melee acquisition");
            var near=t.Location;t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);Ready(p);int emptyMana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.Perform(p,CombatAbility.Fury,null,p.Location,Core.TickCount)&&ClassCombat.For(p).Fury==null&&NecromancerEffects.ManaBalance256(p)==emptyMana,"No-target cast paid or started");t.MoveToWorld(near,p.Map);
            Require(DruidAttackCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.FeralRage))&&GemCombat.Drain(t)==100,"Feral/100drain fixture");var charge=DruidAttackCharges.State(p,CombatAbility.FeralRage);
            var pending=Begin(p,t);int before=SkillHealth.Balance256(p);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(p)>before&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==charge,"Fury omitted/consumed/refreshed Feral leech");ClassCombat.Stop(p);
            pending=Begin(p,t);DruidFury.HitRoll=()=>1;int targetLife=SkillHealth.Balance256(t);before=SkillHealth.Balance256(p);Require(Finish(p)==5&&SkillHealth.Balance256(t)==targetLife&&SkillHealth.Balance256(p)==before,"Miss damaged/leeched");DruidFury.HitRoll=()=>0;
            var skeleton=new GraveyardSkeleton{Name="DruidFuryFixtureBlocker",NoKillAwards=true,Frozen=true,CantWalk=true};skeleton.MoveToWorld(t.Location,p.Map);skeleton.AIObject.AITimer.Stop();skeleton.SetHits(50000);skeleton.Hits=skeleton.HitsMax;
            var reference=CreatureProfiles.Definition(skeleton).Reference;var rule=BalanceCatalog.Read<SourceBlockRule>("source-block-rules").Single(x=>x.Source==reference);int choice=Array.FindIndex(rule.ShieldItems,x=>x);Require(choice>=0,"Shield rule");
            SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=skeleton.Serial.Value).Append(new SourceShield(skeleton.Serial.Value,reference,choice)).ToArray());SourceBlocking.BlockRoll=_=>0;
            pending=Begin(p,skeleton);int life=SkillHealth.Balance256(skeleton);before=SkillHealth.Balance256(p);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(skeleton)==life&&SkillHealth.Balance256(p)==before,"Shield block damaged/leeched");ClassCombat.Stop(p);SourceBlocking.BlockRoll=_=>99;skeleton.Delete();
            t.SetResistance(ResistanceType.Physical,100);pending=Begin(p,t);targetLife=SkillHealth.Balance256(t);before=SkillHealth.Balance256(p);Require(Finish(p)==5&&SkillHealth.Balance256(t)==targetLife&&SkillHealth.Balance256(p)==before,"Physical immunity/physical leech");t.SetResistance(ResistanceType.Physical,0);
            pending=Begin(p,t);NecromancerEffects.SetMana256(p,0);Require(Finish(p)==5&&NecromancerEffects.ManaBalance256(p)==0,"Already paid Fury required more mana");
            Ready(p);NecromancerEffects.SetMana256(p,1023);Require(ClassCombat.Perform(p,CombatAbility.Fury,t,t.Location,Core.TickCount)&&ClassCombat.For(p).PendingAbility==CombatAbility.BasicAttack&&ClassCombat.For(p).Fury==null
                &&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==1023,"Approved low-mana ordinary Attack");
            pending=Begin(p,t);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount)&&ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Fury incorrectly inherited Fend recovery lock");
            pending=Begin(p,t);ClassCombat.Stop(p);Require(ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Stop retained Fury");
            pending=Begin(p,t);var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);ClassMovement.Moved(p,Direction.South);p.MoveToWorld(origin,p.Map);Require(ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Movement retained Fury");
            pending=Begin(p,t);var dagger=CombatTiming.Equipped(p);p.AddToBackpack(dagger);ClassGear.Refresh(p);Require(ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt),"Weapon swap retained Fury");Require(p.EquipItem(dagger),"Reequip");ClassGear.Refresh(p);
            pending=Begin(p,t);int impactsBefore=t.Impacts;long late=pending.EffectAt+10000;Require(ClassCombat.ResolveAction(p,late)&&t.Impacts==impactsBefore+1&&ClassCombat.For(p).Fury is{Remaining:4} next&&next.EffectAt>late&&!ClassCombat.ResolveAction(p,late),"Late update replayed a burst");ClassCombat.Stop(p);
            var b=Target(p,1);int aHits=t.Impacts,bHits=b.Impacts;pending=Begin(p,t);Require(Finish(p)==5&&t.Impacts-aHits==3&&b.Impacts-bHits==2,"Living targets did not cycle within the series");pending=Begin(p,t);targetLife=SkillHealth.Balance256(b);t.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(b)<targetLife,"Lost-range target not replaced");ClassCombat.Stop(p);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            pending=Begin(p,t);t.SetHits(1);t.Hits=1;targetLife=SkillHealth.Balance256(b);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&!t.Alive&&ClassCombat.For(p).Fury is{Remaining:4} following&&following.Target==b.Serial,"Death did not retarget next attempt");Require(Finish(p)==4&&SkillHealth.Balance256(b)<targetLife,"Retargeted continuation");t.Corpse?.Delete();t.Delete();t=b;
            pending=Begin(p,t);t.AfterHit=owner=>ClassCombat.Stop(owner);targetLife=SkillHealth.Balance256(t);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(t)<targetLife&&ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt+10000),"Reentrant stop resurrected Fury");t.AfterHit=null;
            pending=Begin(p,t);t.AfterHit=owner=>{t.AfterHit=null;ClassCombat.Stop(owner);ClassCombat.For(owner).NextAction=Core.TickCount;Require(DruidFury.Begin(owner,t.Serial,t.Location,Core.TickCount),"Reentrant new series");};Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&ClassCombat.For(p).Fury is{Released:0,Remaining:5},"Old contact damaged replacement series");ClassCombat.Stop(p);
            while(p.RawStr<75){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<75){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(321),true);pending=Begin(p,t);Require(p.EquipItem(helm),"Tarnhelm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.Fury)==5&&ClassCombat.For(p).Fury.Remaining==5&&Finish(p)==5,"Effective rank/current capture");
            var amulet=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Highlord's Wrath").Id,99,new Random(173),true);Require(p.EquipItem(amulet),"Highlord");ClassGear.Refresh(p);
            pending=Begin(p,t);var values=SkillMechanics.Values(p,CombatAbility.Fury);var plain=GearEffects.PrepareWeaponImpact(p,t,0,new Rolls(),GearEffects.Damage(p,0,t,(int)values.Eval("calc2"),_=>0));targetLife=SkillHealth.Balance256(t);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&targetLife-SkillHealth.Balance256(t)==(plain.LifeDamage256??NamedElements.To256(plain.Parts.Total)),"Noncritical actual contact");ClassCombat.Stop(p);
            DruidFury.Effects=new Rolls(true);pending=Begin(p,t);var critical=GearEffects.PrepareWeaponImpact(p,t,0,new Rolls(true),GearEffects.Damage(p,0,t,(int)values.Eval("calc2"),_=>0));targetLife=SkillHealth.Balance256(t);
            Require(critical.Parts.Physical==plain.Parts.Physical*2&&critical.Parts.Lightning==plain.Parts.Lightning&&ClassCombat.ResolveAction(p,pending.EffectAt)&&targetLife-SkillHealth.Balance256(t)==(critical.LifeDamage256??NamedElements.To256(critical.Parts.Total)),"Critical doubled typed damage or omitted ordinary physical");DruidFury.Effects=new Rolls();ClassCombat.Stop(p);
            var sword=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Hellplague").Id,99,new Random(173),true);p.AddToBackpack(dagger);Require(p.EquipItem(sword),"Hellplague");ClassGear.Refresh(p);
            t.SetResistance(ResistanceType.Physical,100);pending=Begin(p,t);targetLife=SkillHealth.Balance256(t);Require(ClassCombat.ResolveAction(p,pending.EffectAt)&&SkillHealth.Balance256(t)<targetLife,"Source128 omitted actual typed weapon through physical immunity");ClassCombat.Stop(p);t.SetResistance(ResistanceType.Physical,0);
            pending=Begin(p,t);DruidPlayerForms.Clear(p);Require(ClassCombat.For(p).Fury==null&&!ClassCombat.ResolveAction(p,pending.EffectAt)&&!DruidCombat.Available(p,CombatAbility.Fury),"Human/unshift Fury");Train(p,CombatAbility.Werebear);Require(DruidPlayerForms.ApplyPrepared(p,CombatAbility.Werebear,TimedEffects.Now)&&!DruidCombat.Available(p,CombatAbility.Fury),"Bear Fury");
            Log.Information("Fury combat:2..5contacts,4mana,ordinary physical/typed/critical,Feral drain,miss/block/immunity,low/lost mana,normal recovery,living/death/range retargeting,captured count/soft rank,empty acquisition,cancellation,reentry and real gear controls pass");
        }
        finally{t.AfterHit=null;t.Corpse?.Delete();t.Delete();Drop(p);SourceBlocking.Restore(shields);}
    }
    private static void Check()=>Controlled(()=>{Scalars();Cases();});
    private static void Bind(PlayerMobile p,NetState network)
    {
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
    }
    private static void Stage(NetState network)=>Controlled(()=>
    {
        var p=Fresh();Train(p,CombatAbility.Fury,4);var t=Target(p);Bind(p,network);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Fury)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Fury)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Fury),"Saved bindings");
        var pending=Begin(p,t);Require(ClassCombat.ResolveAction(p,pending.EffectAt),"Saved first paid hit");var action=ClassCombat.For(p);Require(action.Fury is{Remaining:4},"Saved paid remainder");
        // Park only the unspent action deadline. Keep a real native connection
        // through the actual world save so the offline sweeper cannot remove it.
        action.Fury=action.Fury with{EffectAt=long.MaxValue};action.EffectAt=long.MaxValue;
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(t),DruidPlayerForms.State(p),4)));
        Snapshot(true);Log.Information("druid-fury-stage-result: passed");
    });
    private static void Snapshot(bool pending=false)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=World.FindMobile((Serial)s.Target);
        Require(DruidPlayerForms.State(p)==s.Form&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife,"Saved Fury form/fractional mana/life changed");
        if(pending){Require(p.NetState is{Running:true}&&ClassCombat.For(p).Fury is{Remaining:4,Released:1,EffectAt:long.MaxValue},"Paid Fury series was not pending on a real connection at save");}
    }
    private static void Reload()
    {
        Snapshot();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(ClassCombat.For(p).Fury==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Fury&&SkillBindings.Instance.Get(p).Left==CombatAbility.Fury&&SkillBindings.Instance.Get(p).Right==CombatAbility.Fury,"Paid series replay or binding loss");
        World.FindMobile((Serial)s.Target).Delete();Drop(p);
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh();var t=Target(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Fury)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Fury)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Fury),"Input bindings");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(t),attempts=0;var hit=DruidFury.HitRoll;var block=SourceBlocking.BlockRoll;DruidFury.HitRoll=()=>{attempts++;return 0;};SourceBlocking.BlockRoll=_=>99;
        Bind(p,network);
        bool began=false;long deadline=Core.TickCount+7000;
        void Cleanup(){DruidFury.HitRoll=hit;SourceBlocking.BlockRoll=block;t.Delete();Drop(p);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Fury input timeout");var action=ClassCombat.For(p);
                if(action.Fury!=null&&!began){began=true;Require(NecromancerEffects.ManaBalance256(p)==mana-1024,"Actual input start payment");Log.Information("{Label}: windup",label);}
                if(!began||action.Fury!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(attempts==2&&t.Impacts==2&&SkillHealth.Balance256(t)<life&&NecromancerEffects.ManaBalance256(p)==mana-1024,"Actual two-contact input/payment");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Fury,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
