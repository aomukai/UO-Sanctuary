// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;

namespace Sanctuary.Integration;

public static class VolcanoChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(VolcanoChecks));
    private sealed record Saved(uint Owner,uint Target,int Life256,VolcanoSave State,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"volcano-fixtures.json");
    private static int _scene;
    private static bool _creating;
    private static uint _pending;
    private static int _pendingMana;
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("VolcanoFixture",StringComparison.Ordinal)==true
            ?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Register("volcano-check",Check);Register("volcano-reload-check",Reload);
        Register("volcano-live-check",()=>FixtureNetwork.Connected("volcano-live-result",n=>Live(n,false)));
        Register("volcano-input-check",()=>FixtureNetwork.Connected("volcano-input-result",n=>Live(n,true)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Volcano mechanics and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static string Text(VolcanoSave state=null)=>JsonSerializer.Serialize(state??DruidVolcano.Snapshot());
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;
        try{p=new PlayerMobile{Body=0x190,Player=true,Name="VolcanoFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,7000,45,2100+32*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int dx=4)
    {
        var target=new GraveyardSkeleton{Name="VolcanoFixtureTarget",CantWalk=true,NoKillAwards=true};target.SetHits(10000);
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){target.SetResistance(kind,0);}
        target.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();Shield(target,0);return target;
    }
    private static void Shield(BaseCreature target,int choice)=>SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value)
        .Append(new SourceShield(target.Serial.Value,CreatureProfiles.Definition(target).Reference,choice)).ToArray());
    private static void Ready(PlayerMobile p,bool cooldown=true)
    {
        ClassCombat.Stop(p);var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;
        if(cooldown){var state=FireGround.Snapshot();FireGround.Restore(state with{Cooldowns=state.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=CombatAbility.Volcano).ToArray()});}
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static bool Start(PlayerMobile p,Mobile target=null,Point3D? point=null)
    {bool result=ClassCombat.Perform(p,CombatAbility.Volcano,target,point??target?.Location??new Point3D(p.X+6,p.Y,p.Z),Core.TickCount);ClassCombat.ReleaseCast(p);return result;}
    private static VolcanoActor Actor(PlayerMobile p,DateTime at,int missile=483,int physical=256,int fire=256,long id=2,long cast=1,int frames=0)
    {
        if(frames==0){frames=missile==479?150:missile==481?27:18;}
        return new(id,cast,p.Serial.Value,p.Map.MapID,1,missile,p.Z,p.X+4,p.Y,missile==481?.328125:0,0,frames,0,
            missile==481?0:physical,missile==481?0:physical,missile==481?0:fire,missile==481?0:fire,
            new(0,0,0,0,0),SourceRandom.InitLow(1),p.Serial.Value,at.AddMilliseconds(40),at.AddMilliseconds(frames*40L));
    }
    private static void Set(params VolcanoActor[] actors)=>DruidVolcano.Restore(new(actors,[],actors.Max(x=>x.Id)));
    private static void Casting()
    {
        var p = Fresh(); Require(!ClassCombat.Available(p, CombatAbility.Volcano), "Untrained Volcano available"); Train(p, CombatAbility.Volcano); Mana(p);
        Require(!SkillBindings.Instance.Assign(p, true, CombatAbility.Volcano) && SkillBindings.Instance.Assign(p, false, CombatAbility.Volcano)
            && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Volcano), "Volcano right-only/quick binding");
        Ready(p); NecromancerEffects.SetMana256(p, 6399); Require(!Start(p) && NecromancerEffects.ManaBalance256(p) == 6399, "Low mana began Volcano"); Mana(p);
        Ready(p); int mana = NecromancerEffects.ManaBalance256(p); Require(Start(p), "SC windup"); long at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.For(p).Animation == 16 && !ClassCombat.ResolveAction(p, at - 1) && NecromancerEffects.ManaBalance256(p) == mana, "SC timing/early payment"); ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Stopped cast paid");
        Ready(p); Require(Start(p), "Movement windup"); at = ClassCombat.For(p).EffectAt; var origin = p.Location; p.Direction = Direction.East;
        Require(p.Move(Direction.East), "Outward fixture step"); p.Direction = Direction.West;
        Require(p.Move(Direction.West) && p.Location == origin && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Out/back revived cancelled cast");
        Ready(p); Require(Start(p), "Recovery windup"); at = ClassCombat.For(p).EffectAt;
        Require(HitRecovery.Apply(p, p.HitsMax, Core.TickCount) && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Recovery cancellation");
        Ready(p); Require(Start(p), "Mana-loss windup"); at = ClassCombat.For(p).EffectAt; NecromancerEffects.SetMana256(p, 0); string before = Text();
        Require(!ClassCombat.ResolveAction(p, at) && Text() == before && FireGround.Ready(p, CombatAbility.Volcano), "Failed release emitted/delayed"); Mana(p);
        Ready(p); mana = NecromancerEffects.ManaBalance256(p); Require(Start(p), "Ground windup"); at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p, at) && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana - 6400
            && DruidVolcano.Snapshot().Actors.Count(x => x.Owner == p.Serial.Value) == 1, "Once-only payment/creation");
        var delay = FireGround.Snapshot().Cooldowns.Single(x => x.Owner == p.Serial.Value && x.Ability == CombatAbility.Volcano);
        Require(delay.UntilUtc > TimedEffects.Now.AddMilliseconds(3900) && delay.UntilUtc <= TimedEffects.Now.AddMilliseconds(4000), "100frame delay");
        Ready(p, false); Require(!Start(p) && NecromancerEffects.ManaBalance256(p) == mana - 6400, "Local delay accepted second windup");
        Ready(p, false); Require(ClassCombat.Perform(p, CombatAbility.Firestorm, null, new(p.X + 6, p.Y, p.Z), Core.TickCount), "Volcano delay became global"); ClassCombat.Stop(p); DruidVolcano.Cancel(p);
        Ready(p); Mana(p); var target = Enemy(p, 6); Require(Start(p, target), "Live-target windup"); at = ClassCombat.For(p).EffectAt; target.MoveToWorld(new(p.X, p.Y + 2, p.Z), p.Map);
        Require(ClassCombat.ResolveAction(p, at) && DruidVolcano.Snapshot().Actors.Single(x => x.Owner == p.Serial.Value) is { } center
            && center.X == target.X && center.Y == target.Y, "Live target position not captured at release"); DruidVolcano.Cancel(p); target.Delete();
        Ready(p); Mana(p); target = Enemy(p, 6); var cached = target.Location; Require(Start(p, target), "Deleted-target windup"); at = ClassCombat.For(p).EffectAt; target.Delete();
        Require(ClassCombat.ResolveAction(p, at) && DruidVolcano.Snapshot().Actors.Single(x => x.Owner == p.Serial.Value).X == cached.X, "Deleted target lost cached ground aim"); DruidVolcano.Cancel(p);
        Ready(p); Mana(p); Require(Start(p, point: p.Location), "Self-ground windup"); at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p, at) && DruidVolcano.Snapshot().Actors.Single(x => x.Owner == p.Serial.Value).X == p.X, "Self-ground center moved"); DruidVolcano.Cancel(p);
        var town = new GuardedRegion("VolcanoFixtureTown", p.Map, 1000, new Rectangle2D(p.X - 1, p.Y - 1, 3, 3)); town.Register();
        try { Ready(p); Require(!Start(p), "Volcano began in town"); } finally { town.Unregister(); }
        p.Delete();
    }
    private static void SourceDamage()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"volcano-damage-source-probe.json")));int count=0;
        foreach(var row in json.RootElement.EnumerateArray())
        {
            if(row[0].GetInt32()==1){continue;}int rank=row[1].GetInt32(),b=row[2].GetInt32(),f=row[3].GetInt32(),a=row[4].GetInt32(),mastery=row[5].GetInt32();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.Volcano),key=>key=="Volcano"?rank:255,
                key=>key=="Molten Boulder"?b:key=="Eruption"?f:key=="Armageddon"?a:0,99);
            var fire=DruidFirestorm.Endpoints(values,mastery,SlowMissilePayload.Normal);
            Require(values.PhysicalDamage256(false)==row[7].GetInt32()&&values.PhysicalDamage256(true)==row[8].GetInt32()
                &&fire==(row[9].GetInt32(),row[10].GetInt32()),"Complete source vent/lava damage");count++;
        }
        Require(count==55080,"Complete source endpoint scope");Log.Information("Volcano:55080complete-source endpoint comparisons/all255ranks pass");
    }
    private static void Emitter()
    {
        var p=Fresh();Train(p,CombatAbility.Volcano);var at=TimedEffects.Now.AddDays(1);int mana=NecromancerEffects.ManaBalance256(p);
        long id=DruidVolcano.Create(p,new(p.X+6,p.Y,p.Z),1,at);var vent=DruidVolcano.Snapshot().Actors.Single();
        Require(vent.Id==id&&vent.Cast==id&&vent.Remaining==150&&vent.UntilUtc==at.AddMilliseconds(6000),"Initial vent clock");
        Require(vent.PhysicalMin==SkillMechanics.Values(p,CombatAbility.Volcano).PhysicalDamage256(false),"Captured vent endpoints");
        Set(vent with{Memory=0x87654321U});var seen=new HashSet<long>();int emitted=0;
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"volcano-source-probe.json")));
        foreach(var row in source.RootElement.GetProperty("sequence").EnumerateArray())
        {
            int frame=row[0].GetInt32();DruidVolcano.Tick(at.AddMilliseconds((frame+1)*40),true);var state=DruidVolcano.Snapshot();
            var root=state.Actors.SingleOrDefault(x=>x.Missile==479);
            if(frame<149){Require(root!=null&&root.Remaining==149-frame&&root.Memory==row[2].GetUInt32(),"Actual vent elapsed/memory order");}
            var fresh=state.Actors.Where(x=>x.Missile==481&&seen.Add(x.Id)).ToArray();var requests=row[4];
            Require(fresh.Length==requests.GetArrayLength(),"Actual62emission opportunities");
            if(fresh.Length>0)
            {
                var shot=fresh.Single();int dx=requests[0][4].GetInt32()-300,dy=requests[0][5].GetInt32()-400;
                var plan=VolcanoPatterns.Flight((int)vent.X*2,(int)vent.Y*2,(int)vent.X*2+dx,(int)vent.Y*2+dy,1);
                Require(shot.Cast==id&&shot.Rank==1&&shot.X==vent.X&&shot.Y==vent.Y&&shot.Vx==plan.Vx&&shot.Vy==plan.Vy
                    &&shot.Remaining==plan.Remaining&&shot.NextUtc==at.AddMilliseconds((frame+2)*40)
                    &&shot.PhysicalMax==0&&shot.FireMax==0,"Source request/factory/owner/zero-damage air capture");emitted++;
            }
        }
        Require(emitted==62&&!DruidVolcano.Snapshot().Actors.Any(x=>x.Missile==479),"150frame vent/62shots");
        DruidVolcano.Tick(at.AddMilliseconds(7000),true);Require(DruidVolcano.Snapshot().Actors.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Independent final expiry/prepared payment");
        DruidVolcano.Cancel(p);p.Delete();
    }
    private static void FlightAndFreshCapture()
    {
        var p=Fresh();Train(p,CombatAbility.Volcano);var target=Enemy(p,5);var at=TimedEffects.Now.AddDays(1);int life=SkillHealth.Balance256(target);
        var air=Actor(p,at,481,frames:8) with{X=p.X+2};Set(air);
        var wall=new Static(0x80);wall.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            Require(!DruidVolcano.Placement(p.Map,wall.Location),"Size3 placement ignored wall");
            DruidVolcano.Tick(at.AddMilliseconds(280),true);var before=DruidVolcano.Snapshot().Actors.Single();
            Require(before.Missile==481&&before.X>wall.X&&SkillHealth.Balance256(target)==life,"Mode0 air intercepted wall/actor");
            Train(p,CombatAbility.MoltenBoulder,2);Train(p,CombatAbility.Fissure,2);Train(p,CombatAbility.Volcano,2);
            DruidVolcano.Tick(at.AddMilliseconds(320),true);var lava=DruidVolcano.Snapshot().Actors.Single();
            Require(lava.Missile==483&&lava.Rank==1&&lava.Remaining==18&&lava.X==Math.Floor((air.X+air.Vx*8)*2+.5)/2
                &&lava.X!=air.X&&lava.NextUtc==at.AddMilliseconds(360)&&lava.UntilUtc==at.AddMilliseconds(1040),"Actual landing position/retained rank/lava clock");
            var values=SkillMechanics.Values(p,CombatAbility.Volcano,1);var fire=DruidFirestorm.Endpoints(values,0,SlowMissilePayload.Normal);
            Require(lava.PhysicalMin==values.PhysicalDamage256(false)&&lava.FireMin==fire.Min,"Fresh hard synergies at retained rank");
            DruidVolcano.Tick(lava.NextUtc,true);Require(SkillHealth.Balance256(target)<life,"Lava damage behind caster LOS wall");
            DruidVolcano.Tick(lava.UntilUtc,true);Require(DruidVolcano.Snapshot().Actors.Length==0,"Independent18frame lava expiry");
        }
        finally{wall.Delete();DruidVolcano.Cancel(p);target.Delete();p.Delete();}
        Log.Information("Volcano:mode0wall/actor passage, actual terminal position and fresh retained-rank lava pass");
    }
    private static void CollidersAndDamage()
    {
        var p=Fresh();Train(p,CombatAbility.Volcano);var a=Enemy(p);var b=Enemy(p);var at=TimedEffects.Now.AddDays(1);var lava=Actor(p,at);Set(lava);
        var first=DruidVolcano.Recipient(p,lava);Require(first==a||first==b,"Native collider");var second=first==a?b:a;int life=SkillHealth.Balance256(first),other=SkillHealth.Balance256(second);
        DruidVolcano.Tick(lava.NextUtc,true);var hit=DruidVolcano.Snapshot().Actors.Single();var lease=DruidVolcano.Snapshot().Leases.Single();
        Require(life-SkillHealth.Balance256(first)==512&&SkillHealth.Balance256(second)==other&&hit.LastTarget==first.Serial.Value,"One physical/fire collider");
        Require(!DruidVolcano.Ready(1,first.Serial.Value,lease.UntilUtc.AddTicks(-1))&&DruidVolcano.Ready(1,first.Serial.Value,lease.UntilUtc)
            &&DruidVolcano.Ready(2,first.Serial.Value,lava.NextUtc),"Explicit provisional cast/target delay boundary");
        DruidVolcano.Tick(hit.NextUtc,true);Require(SkillHealth.Balance256(second)==other-512,"Preselection LastCollide/lease permitted next victim");
        DruidVolcano.Cancel(p);b.Delete();a.Delete();a=Enemy(p);life=SkillHealth.Balance256(a);Set(lava);DruidVolcano.Tick(at.AddMilliseconds(600),true);
        Require(life-SkillHealth.Balance256(a)==512,"Lava LastCollide became per-victim repeat cooldown");DruidVolcano.Cancel(p);
        var root=Actor(p,at,479,id:1);Set(root);life=SkillHealth.Balance256(a);DruidVolcano.Tick(at.AddMilliseconds(440),true);
        Require(life-SkillHealth.Balance256(a)>=1024,"Vent failed repeated10frame contact");DruidVolcano.Cancel(p);
        a.SetResistance(ResistanceType.Physical,100);Set(lava);life=SkillHealth.Balance256(a);DruidVolcano.Tick(lava.NextUtc,true);
        Require(life-SkillHealth.Balance256(a)==256,"Physical immunity removed fire");DruidVolcano.Cancel(p);
        a.SetResistance(ResistanceType.Fire,100);Set(lava);life=SkillHealth.Balance256(a);DruidVolcano.Tick(lava.NextUtc,true);
        Require(SkillHealth.Balance256(a)==life,"Dual immunity");DruidVolcano.Cancel(p);
        Require(CombatPotions.Apply(p,3),"Tier3 potion");Set(lava with{PhysicalMin=2560,PhysicalMax=2560,FireMin=2560,FireMax=2560,Modifiers=DruidFirestorm.Modifiers(p)});DruidVolcano.Tick(lava.NextUtc,true);
        Require(life-SkillHealth.Balance256(a)==6*256,"Approved potion nonimmune channels through dual immunity");
        DruidVolcano.Cancel(p);a.Delete();p.Delete();
    }
    private static void GearBlockAndFootprint()
    {
        var p=Fresh();Train(p,CombatAbility.Volcano);var at=TimedEffects.Now.AddDays(1);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.Volcano)==2,"Actual soft rank");
        long id=DruidVolcano.Create(p,new(p.X+4,p.Y,p.Z),2,at);var root=DruidVolcano.Snapshot().Actors.Single();
        var values=SkillMechanics.Values(p,CombatAbility.Volcano);Require(root.Rank==2&&root.PhysicalMin==values.PhysicalDamage256(false),"Actual gear capture");
        var projection=MissileFootprints.Snapshot().Single(x=>x.Source=="volcano"&&x.X==(p.X+4)*2&&x.Y==p.Y*2);
        Require(projection.UntilUtc==root.UntilUtc&&DruidFissure.LandingBlocked(root.Map,(p.X+4)*2,p.Y*2,p.Z,at)
            &&!MissileFootprints.Covers(projection,root.Map,(p.X+4)*2,p.Y*2,p.Z,root.UntilUtc),"Vent Collision1 projection/expiry");DruidVolcano.Cancel(p);
        var slow=AmazonSlowMissiles.Snapshot();
        try
        {
            AmazonSlowMissiles.Restore(slow.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddDays(2))).ToArray());
            DruidVolcano.Create(p,new(p.X+4,p.Y,p.Z),2,at);DruidVolcano.Tick(at.AddMilliseconds(200),true);var air=DruidVolcano.Snapshot().Actors.Single(x=>x.Missile==481);
            Require(Math.Abs(air.Vx)>.16||Math.Abs(air.Vy)>.16,"CanSlow-absent air was slowed");
            Require(DruidVolcano.Snapshot().Actors.Single(x=>x.Missile==479).PhysicalMin==root.PhysicalMin,"Slow changed physical vent endpoints");
        }
        finally{AmazonSlowMissiles.Restore(slow);DruidVolcano.Cancel(p);}
        var target=Enemy(p);Shield(target,1);var roll=SourceBlocking.BlockRoll;var guard=DruidVolcano.Uninterruptible;
        try
        {
            SourceBlocking.BlockRoll=_=>0;var field=Actor(p,at);int life=SkillHealth.Balance256(target);Set(field);DruidVolcano.Tick(field.NextUtc,true);
            Require(SkillHealth.Balance256(target)==life&&DruidVolcano.Snapshot().Actors.Single().LastTarget==target.Serial.Value,"Shield block lost history/did damage");DruidVolcano.Cancel(p);
            ulong seed=0;for(int low=0;low<10000;low++){var probe=SourceRandom.InitLow(low);var copy=probe;if(SourceRandom.Roll(ref copy)%100==0){seed=probe;break;}}
            Require(seed!=0,"One-percent source seed");var vent=Actor(p,at,479,id:1) with{Seed=seed};Set(vent);target.CantWalk=false;
            DruidVolcano.Tick(vent.NextUtc,true);Require(SkillHealth.Balance256(target)==life&&SourceKnockbacks.Snapshot().Any(x=>x.Target==target.Serial.Value),"Blocked vent one-percent knockback");
            SourceKnockbacks.Clear(target);DruidVolcano.Cancel(p);DruidVolcano.Uninterruptible=_=>true;Set(vent);DruidVolcano.Tick(vent.NextUtc,true);
            Require(!SourceKnockbacks.Snapshot().Any(x=>x.Target==target.Serial.Value),"Uninterruptible vent pushed");
        }
        finally{DruidVolcano.Uninterruptible=guard;SourceBlocking.BlockRoll=roll;SourceKnockbacks.Clear(target);DruidVolcano.Cancel(p);target.Delete();p.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.Volcano);var target=Enemy(p);var at=TimedEffects.Now.AddDays(1);int life=SkillHealth.Balance256(target);
        Set(Actor(p,at,479,id:1),Actor(p,at,481,id:2),Actor(p,at,id:3));DruidVolcano.Tick(at.AddMilliseconds(6000),false);
        Require(DruidVolcano.Snapshot() is{Actors.Length:0,Leases.Length:0}&&SkillHealth.Balance256(target)==life,"Offline fighting/emission or stalled aging");
        var field=Actor(p,at);Set(field);var origin=p.Location;int map=p.Map.MapID;p.MoveToWorld(origin,Map.Felucca);DruidVolcano.Tick(field.NextUtc,true);
        Require(DruidVolcano.Snapshot().Actors.Single().Map==map&&SkillHealth.Balance256(target)==life,"Old-facet field moved/fought");
        DruidVolcano.Tick(field.UntilUtc,true);Require(DruidVolcano.Snapshot().Actors.Length==0,"Old-facet expiry stalled");p.MoveToWorld(origin,Map.Maps[map]);
        Set(field);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Amazon),"Class transition");DruidVolcano.Tick(field.NextUtc,true);
        Require(DruidVolcano.Snapshot().Actors.Length==0,"Class cleanup");target.Delete();p.Delete();
    }
    private static void WorkerAndMalformed()
    {
        var before=DruidVolcano.Snapshot();string text=Text();
        var bytes=Task.Run(()=>{var writer=new BufferWriter(new byte[2*1024*1024],true);DruidVolcano.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0&&reader.ReadString()==text&&Text()==text,"Worker serialization mutated pool");
        var first=before.Actors.First();var bad=new[]{before with{Actors=null},before with{Leases=null},before with{NextId=0},before with{DelayScope="legacy-global"},
            before with{Actors=[first,first]},before with{Actors=[first with{Remaining=0}]},before with{Actors=[first with{Missile=480}]},
            before with{Actors=[first with{NextUtc=first.NextUtc.AddTicks(1)}]},before with{Actors=[first with{PhysicalMin=-1}]},before with{Actors=[first with{X=double.NaN}]},
            before with{Actors=[first with{MotionAdapter="unknown"}]},before with{Leases=[new WeaponFlightLease(long.MaxValue,first.Owner,first.NextUtc)]}};
        foreach(var state in bad){bool refused=false;try{DruidVolcano.Restore(state);}catch(InvalidDataException){refused=true;}Require(refused&&Text()==text,"Malformed restore partially replaced pool");}
        var copy=DruidVolcano.Snapshot();copy.Actors[0]=first with{Remaining=1};Require(Text()==text,"Snapshot array leaked pool");
        DruidVolcano.Restore(JsonSerializer.Deserialize<VolcanoSave>(text));Require(Text()==text,"Exact JSON resumption");
    }
    private static void Save(PlayerMobile p,Mobile target)=>File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),DruidVolcano.Snapshot(),_pending,_pendingMana)));
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Volcano)&&DruidVolcano.Snapshot().Actors.Length==0,"Volcano initial state");
        var seed=DruidVolcano.SeedLow;
        try{DruidVolcano.SeedLow=()=>1;SourceDamage();Casting();Emitter();FlightAndFreshCapture();CollidersAndDamage();GearBlockAndFootprint();Lifecycle();}
        finally{DruidVolcano.SeedLow=seed;}
        var p=Fresh();Train(p,CombatAbility.Volcano);var target=Enemy(p);var at=TimedEffects.Now.AddDays(2);TimedEffects.PreventHealing(target,TimedEffects.Now);
        Set(Actor(p,at,479,id:1),Actor(p,at,481,id:3),Actor(p,at,id:2));DruidVolcano.Tick(at.AddMilliseconds(40),true);
        Require(DruidVolcano.Snapshot().Actors.Length==3&&DruidVolcano.Snapshot().Leases.Length==1,"Held vent/air/lava/lease setup");WorkerAndMalformed();
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Volcano)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Volcano),"Saved bindings");
        var pending=Fresh();Train(pending,CombatAbility.Volcano);Mana(pending);Ready(pending);_pending=pending.Serial.Value;_pendingMana=NecromancerEffects.ManaBalance256(pending);
        Require(Start(pending),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Save(p,target);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(p!=null&&target!=null&&Text()==Text(saved.State)&&SkillHealth.Balance256(target)==saved.Life256,"Exact saved actors/pose/seed/clock/lease/HP");WorkerAndMalformed();
        _pending=saved.Pending;_pendingMana=saved.PendingMana;var pending=(PlayerMobile)World.FindMobile((Serial)_pending);
        Require(pending!=null&&NecromancerEffects.ManaBalance256(pending)==_pendingMana&&ClassCombat.For(pending).PendingAbility==CombatAbility.None,"Unpaid windup replayed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Volcano&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Volcano,"Saved right/quick bindings");
        var before=DruidVolcano.Snapshot();DateTime next=before.Actors.Min(x=>x.NextUtc);DruidVolcano.Tick(next,true);
        Require(DruidVolcano.Snapshot().Actors.Single(x=>x.Missile==479).Remaining==before.Actors.Single(x=>x.Missile==479).Remaining-1,"Saved vent did not resume");Save(p,target);
    }
    private static void Live(NetState network,bool input)
    {
        string label=input?"volcano-input":"volcano-live";var held=DruidVolcano.Snapshot();var p=Fresh();Train(p,CombatAbility.Volcano);Mana(p);Ready(p);
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(protocol!=null,"Input protocol bootstrap");
        protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Volcano)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Volcano),"Connected bindings");
        var aim=new Point3D(p.X+6,p.Y,p.Z);var target=Enemy(p,6);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);
        if(!input){DruidVolcano.Create(p,aim,1,TimedEffects.Now);}bool began=!input,released=!input,air=false,lava=false,damaged=false;long deadline=Core.TickCount+12000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Volcano timeout");var actors=DruidVolcano.Snapshot().Actors.Where(x=>x.Owner==p.Serial.Value).ToArray();
                if(!began&&ClassCombat.For(p).PendingAbility==CombatAbility.Volcano){began=true;Require(actors.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Quick windup paid/emitted");}
                if(!released&&actors.Any(x=>x.Missile==479)){released=true;Require(began&&NecromancerEffects.ManaBalance256(p)==mana-6400&&!FireGround.Ready(p,CombatAbility.Volcano),"Quick release/payment/delay");}
                air|=actors.Any(x=>x.Missile==481);lava|=actors.Any(x=>x.Missile==483);damaged|=SkillHealth.Balance256(target)<life;
                if(!released||actors.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(air&&lava&&damaged&&NecromancerEffects.ManaBalance256(p)==mana-(input?6400:0),"Natural vent/air/lava/damage/once payment");
                DruidVolcano.Cancel(p);target.Delete();p.Delete();Require(Text(DruidVolcano.Snapshot() with{NextId=held.NextId})==Text(held),"Natural control changed held actors");
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var last=World.FindMobile((Serial)saved.Target);
                Require(SkillHealth.Balance256(last)==saved.Life256,"Natural control changed held injury");Save(owner,last);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidVolcano.Cancel(p);target.Delete();p.Delete();Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} x={X} y={Y} z={Z}",label,(int)CombatAbility.Volcano,aim.X,aim.Y,aim.Z);Inspect();});
    }
}
