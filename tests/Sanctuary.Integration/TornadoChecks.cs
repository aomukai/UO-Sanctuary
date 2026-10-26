// SPDX-License-Identifier: GPL-3.0-or-later
using System;
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

public static class TornadoChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(TornadoChecks));
    private sealed record Saved(uint Owner, uint Target, int Life256, TornadoSave State, uint Pending, int PendingMana);
    private static string Manifest => Path.Join(Core.BaseDirectory, "tornado-fixtures.json");
    private static int _scene;
    private static bool _creating;
    private static uint _pending;
    private static int _pendingMana;
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        var old = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = p => _creating || p.Name?.StartsWith("TornadoFixture", StringComparison.Ordinal) == true
            ? TimeSpan.FromHours(1) : old?.Invoke(p) ?? Mobile.DefaultManaRate;
    }
    public static void Configure()
    {
        Register("tornado-check", Check); Register("tornado-reload-check", Reload);
        Register("tornado-live-check", () => FixtureNetwork.Connected("tornado-live-result", n => Live(n, false)));
        Register("tornado-input-check", () => FixtureNetwork.Connected("tornado-input-result", n => Live(n, true)));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Tornado mechanics and persistence", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool value, string why) { if (!value) { throw new InvalidOperationException(why); } }
    private static string Text(TornadoSave saved = null) => JsonSerializer.Serialize(saved ?? DruidTornado.Snapshot());
    private static PlayerMobile Fresh()
    {
        PlayerMobile p; _creating = true;
        try { p = new PlayerMobile { Body = 0x190, Player = true, Name = "TornadoFixture" }; } finally { _creating = false; }
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Corridor(p, 7060, 45, 1500 + 32 * _scene++); return p;
    }
    private static void Train(PlayerMobile p, CombatAbility ability, int rank = 1)
    {
        var row = SkillMechanics.For(ability); Require(TalentCatalog.TryGet(DiabloClass.Druid, row.TalentId, out var talent), "Druid talent");
        foreach (string parent in talent.Prerequisites) { Train(p, SkillMechanics.All.Single(x => x.TalentId == parent).Ability); }
        while (ClassCreation.Profiles.GetRank(p.Serial, row.TalentId) < rank) { Require(ClassCreation.Profiles.TryTrain(p.Serial, row.TalentId, out _), "Train " + row.TalentId); }
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p, int dx = 1)
    {
        var target = new GraveyardSkeleton { Name = "TornadoFixtureTarget", CantWalk = true, NoKillAwards = true }; target.SetHits(10000);
        foreach (var kind in new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Energy, ResistanceType.Poison }) { target.SetResistance(kind, 0); }
        target.MoveToWorld(new(p.X + dx, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop();
        Shield(target, 0); return target;
    }
    private static void Shield(BaseCreature target, int choice) => SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x => x.Actor != target.Serial.Value)
        .Append(new SourceShield(target.Serial.Value, CreatureProfiles.Definition(target).Reference, choice)).ToArray());
    private static void Ready(PlayerMobile p)
    { ClassCombat.Stop(p); ClassCombat.For(p).NextAction = ClassCombat.For(p).RecoveryUntil = Core.TickCount; p.Frozen = p.Paralyzed = false; }
    private static void Mana(PlayerMobile p) => NecromancerEffects.SetMana256(p, p.ManaMax * 256);
    private static bool Start(PlayerMobile p, Mobile target = null, Point3D? point = null)
    { bool result = ClassCombat.Perform(p, CombatAbility.Tornado, target, point ?? target?.Location ?? new Point3D(p.X + 6, p.Y, p.Z), Core.TickCount); ClassCombat.ReleaseCast(p); return result; }
    private static TornadoMissile One(PlayerMobile p, DateTime at, int raw = 1536)
    {
        DruidTornado.Create(p, new(p.X + 6, p.Y, p.Z), SkillMechanics.Values(p, CombatAbility.Tornado), at);
        var first = DruidTornado.Snapshot().Missiles.First(); var fixedDamage = first with { Min256 = raw, Max256 = raw, Seed = SourceRandom.InitLow(1), Modifiers = new(0, 0, 0, 0, 0) };
        DruidTornado.Restore(new([fixedDamage], [], fixedDamage.Id)); return fixedDamage;
    }
    private static void Casting()
    {
        var p = Fresh(); Require(!ClassCombat.Available(p, CombatAbility.Tornado), "Untrained Tornado available"); Train(p, CombatAbility.Tornado); Mana(p);
        Require(SkillBindings.Instance.Assign(p, true, CombatAbility.Tornado) && SkillBindings.Instance.Assign(p, false, CombatAbility.Tornado)
            && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Tornado), "Tornado L/R/quick bindings");
        Ready(p); NecromancerEffects.SetMana256(p, 2559); Require(!Start(p) && NecromancerEffects.ManaBalance256(p) == 2559, "Low mana began Tornado"); Mana(p);
        Ready(p); int mana = NecromancerEffects.ManaBalance256(p); Require(Start(p), "SC windup"); long at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.For(p).Animation == 16 && !ClassCombat.ResolveAction(p, at - 1) && NecromancerEffects.ManaBalance256(p) == mana, "Early SC/payment"); ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Stop cancellation");
        Ready(p); Require(Start(p), "Move windup"); at = ClassCombat.For(p).EffectAt; var origin = p.Location; p.Direction = Direction.East; Require(p.Move(Direction.East), "Outward fixture step"); p.Direction = Direction.West;
        Require(p.Move(Direction.West) && p.Location == origin && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana, "Out/back revived cast");
        Ready(p); Require(Start(p), "Recovery windup"); at = ClassCombat.For(p).EffectAt; Require(HitRecovery.Apply(p, p.HitsMax, Core.TickCount) && !ClassCombat.ResolveAction(p, at), "Recovery cancellation");
        Ready(p); Require(Start(p), "Lost-mana windup"); at = ClassCombat.For(p).EffectAt; NecromancerEffects.SetMana256(p, 0); string before = Text();
        Require(!ClassCombat.ResolveAction(p, at) && Text() == before, "Lost mana emitted missiles"); Mana(p); Ready(p); mana = NecromancerEffects.ManaBalance256(p);
        Require(Start(p), "Ground windup"); at = ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p, at) && !ClassCombat.ResolveAction(p, at) && NecromancerEffects.ManaBalance256(p) == mana - 2560
            && DruidTornado.Snapshot().Missiles.Length == 1 && FireGround.Ready(p, CombatAbility.Tornado), "Once-only10mana/one missile/no local delay"); DruidTornado.Cancel(p);
        Ready(p); Mana(p); var target = Enemy(p, 6); Require(Start(p, target), "Live target windup"); at = ClassCombat.For(p).EffectAt; target.MoveToWorld(new(p.X, p.Y + 2, p.Z), p.Map);
        Require(ClassCombat.ResolveAction(p, at), "Live target release"); var live = DruidTornado.Snapshot().Missiles.First(); var expected = ChargedBolts.Path(ChargedBolts.SeedSubtile(target.X * 2, 0), ChargedBolts.Heading(0, 4), 75);
        Require(live.Path.SequenceEqual(expected), "Live target aim/path"); DruidTornado.Cancel(p); target.Delete();
        Ready(p); Mana(p); target = Enemy(p, 6); Require(Start(p, target), "Deleted target windup"); at = ClassCombat.For(p).EffectAt; target.Delete(); Require(ClassCombat.ResolveAction(p, at), "Deleted-target cached ground fallback"); DruidTornado.Cancel(p);
        Ready(p); Mana(p); Require(Start(p, point: p.Location), "Self-ground windup"); Require(ClassCombat.ResolveAction(p, ClassCombat.For(p).EffectAt), "Self-ground fallback"); DruidTornado.Cancel(p);
        var town = new GuardedRegion("TornadoFixtureTown", p.Map, 1000, new Rectangle2D(p.X - 1, p.Y - 1, 3, 3)); town.Register();
        try { Ready(p); Require(!Start(p), "Tornado began in town"); } finally { town.Unregister(); }
        p.Delete();
    }
    private static void Source()
    {
        using var damage=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tornado-damage-source-probe.json")));int count=0;
        foreach(var row in damage.RootElement.EnumerateArray())
        {
            int rank=row[0].GetInt32(),c=row[1].GetInt32(),t=row[2].GetInt32(),h=row[3].GetInt32();
            var values=new SkillValues(SkillMechanics.For(CombatAbility.Tornado),key=>key=="Tornado"?rank:255,
                key=>key=="Cyclone Armor"?c:key=="Twister"?t:key=="Hurricane"?h:0,99);
            Require(values.PhysicalDamage256(false)==row[6].GetInt32()&&values.PhysicalDamage256(true)==row[7].GetInt32()
                &&values.Mana256==2560&&values.Eval("calc1")==1&&values.Eval("calc4")==15&&values.Eval("aurarangecalc")==3,"Source physical/current formulas");count++;
        }
        Require(count==27540,"Complete physical source scope");
        using var paths=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tornado-path-source-probe.json")));int pathCount=0;
        foreach(var row in paths.RootElement.EnumerateArray())
        {
            int origin=row[0].GetInt32(),dx=row[1].GetInt32(),dy=row[2].GetInt32();var init=DruidEmitterPatterns.InitializeCharged(75,origin+dx,0);
            int heading=ChargedBolts.Heading(dx,dy);Require(init.Frames==75&&row[5].GetInt32()==75&&heading==row[6].GetInt32(),"Complete source initializer/heading");
            var actual=ChargedBolts.Path(init.Seed,heading,init.Distance);var expected=row[8].EnumerateArray().ToArray();Require(actual.Length==38&&actual.Length==expected.Length,"Source path length");
            for(int i=0;i<actual.Length;i++)
            {int x=unchecked((short)(ushort)(expected[i][0].GetInt32()-origin)),y=unchecked((short)(ushort)(expected[i][1].GetInt32()-origin));Require(actual[i].X*2==x&&actual[i].Y*2==y,"Source path/wrap");}
            ulong seed=SourceRandom.InitLow(unchecked((int)init.Seed));for(int i=1;i<actual.Length;i++){SourceRandom.Roll(ref seed);}Require(seed==row[7].GetUInt64(),"37source path RNG draws retained");pathCount++;
        }
        Require(pathCount==200,"Complete source path scope");
        using var area=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"tornado-area-source-probe.json")));int radii=0;
        foreach(var row in area.RootElement.EnumerateArray())
        {
            if(row[2].GetInt32()!=0||row[3].GetInt32()!=0||row[4].GetInt32()!=0||row[5].GetInt32()!=1){continue;}
            int aim=row[6].GetInt32();Require(DruidTornado.InPulse(aim==1?52:50,aim==1?71:70,50+row[0].GetInt32(),70+row[1].GetInt32(),3)==(row[7].GetInt32()==1),"Complete source inclusive center radius");radii++;
        }
        Require(radii==243,"Source geometry scope");Log.Information("Tornado:27540source endpoints/200complete paths/243area-radius comparisons pass");
    }
    private static void PreparedAndPulses()
    {
        var p=Fresh();Train(p,CombatAbility.Tornado);var at=TimedEffects.Now.AddDays(1);Mana(p);int mana=NecromancerEffects.ManaBalance256(p);
        DruidTornado.Create(p,new(p.X+6,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Tornado),at);var shot=DruidTornado.Snapshot().Missiles.Single();
        Require(NecromancerEffects.ManaBalance256(p)==mana&&shot.Frame==0&&shot.Path.Length==38&&shot.Velocity256==1536&&shot.UntilUtc==at.AddMilliseconds(3000)&&shot.LastTarget==p.Serial.Value,"Captured75frame flight");
        var ends=(shot.Min256,shot.Max256);Train(p,CombatAbility.Hurricane);DruidTornado.Tick(shot.NextUtc,true);
        Require(DruidTornado.Snapshot().Missiles.Single() is{} advanced&&advanced.Frame==1&&advanced.X!=advanced.OriginX&&(advanced.Min256,advanced.Max256)==ends,"Movement/captured physical versus live synergy");DruidTornado.Cancel(p);
        var target=Enemy(p);shot=One(p,at);var guard=DruidTornado.Uninterruptible;
        try
        {
            DruidTornado.Uninterruptible=_=>true;int life=SkillHealth.Balance256(target);DruidTornado.Tick(shot.NextUtc,true);
            Require(life-SkillHealth.Balance256(target)==3072&&DruidTornado.Snapshot().Leases.Length==1
                &&DruidTornado.Snapshot().Missiles.Single().LastTarget==target.Serial.Value&&!CombatStuns.Snapshot().Any(x=>x.Target==target.Serial.Value),"Distinct pulse/direct damage and no stun");DruidTornado.Cancel(p);
            Shield(target,1);var block=SourceBlocking.BlockRoll;
            try
            {
                SourceBlocking.BlockRoll=_=>0;shot=One(p,at);life=SkillHealth.Balance256(target);DruidTornado.Tick(shot.NextUtc,true);
                Require(life-SkillHealth.Balance256(target)==1536&&DruidTornado.Snapshot().Missiles.Single().LastTarget==target.Serial.Value,"Area bypasses shield; direct block retains history");
            }
            finally{SourceBlocking.BlockRoll=block;DruidTornado.Cancel(p);}
            // A valid controlled slow pose keeps the target in radius while
            // five real source pulse phases run. Direct history is independent.
            shot=One(p,at) with{Velocity256=15,LastTarget=target.Serial.Value};DruidTornado.Restore(new([shot],[],shot.Id));life=SkillHealth.Balance256(target);
            DruidTornado.Tick(shot.UntilUtc,true);Require(life-SkillHealth.Balance256(target)==5*1536&&DruidTornado.Snapshot() is{Missiles.Length:0,Leases.Length:0},"Five periodic phases bypass LastCollide");
            shot=One(p,at);var other=Enemy(p);shot=shot with{Min256=256,Max256=512,LastTarget=target.Serial.Value};
            var leases=new[]{new WeaponFlightLease(shot.Cast,target.Serial.Value,at.AddSeconds(2)),new WeaponFlightLease(shot.Cast,other.Serial.Value,at.AddSeconds(2))};DruidTornado.Restore(new([shot],leases,shot.Id));
            int a=SkillHealth.Balance256(target),b=SkillHealth.Balance256(other);ulong expected=shot.Seed;SourceRandom.Roll(ref expected);DruidTornado.Tick(shot.NextUtc,true);
            Require(a-SkillHealth.Balance256(target)==b-SkillHealth.Balance256(other)&&a>SkillHealth.Balance256(target)
                &&DruidTornado.Snapshot().Missiles.Single().Seed==expected,"One shared pulse roll/copies, no direct lease gate");other.Delete();DruidTornado.Cancel(p);
            shot=One(p,at) with{Frame=15,NextUtc=at.AddMilliseconds(640),X=p.X+36.99,Y=p.Y,Next=37,Path=Enumerable.Range(0,38).Select(x=>new Point2D(x,0)).ToArray()};
            target.MoveToWorld(new(p.X+37,p.Y,p.Z),p.Map);life=SkillHealth.Balance256(target);DruidTornado.Restore(new([shot],[],shot.Id));DruidTornado.Tick(shot.NextUtc,true);
            Require(life-SkillHealth.Balance256(target)==1536&&DruidTornado.Snapshot().Missiles.Length==0,"Periodic packet precedes terminal path update");
        }
        finally{DruidTornado.Uninterruptible=guard;DruidTornado.Cancel(p);target.Delete();p.Delete();}
    }
    private static void EligibilityGearAndObstacles()
    {
        var p=Fresh();Train(p,CombatAbility.Tornado);var target=Enemy(p);var at=TimedEffects.Now.AddDays(1);var shot=One(p,at);
        string source=CreatureProfiles.Definition(target).Reference;var rules=(System.Collections.Generic.Dictionary<string,SourceAuraTarget>)typeof(SourceAuraTargets).GetField("_rules",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var original=rules[source];
        try
        {
            rules[source]=original with{NoAura=true,Npc=true};Require(DruidTornado.PulseRecipient(p,shot,target,3),"Added optional NOAURA/NPC gate to pulse");
        }
        finally{rules[source]=original;DruidTornado.Cancel(p);}
        target.SetResistance(ResistanceType.Physical,100);shot=One(p,at,2560) with{LastTarget=target.Serial.Value};DruidTornado.Restore(new([shot],[],shot.Id));int life=SkillHealth.Balance256(target);DruidTornado.Tick(shot.NextUtc,true);
        Require(SkillHealth.Balance256(target)==life,"Physical immune pulse");DruidTornado.Cancel(p);Require(CombatPotions.Apply(p,3),"Actual tier3 potion");
        shot=One(p,at,2560) with{LastTarget=target.Serial.Value,Modifiers=DruidFirestorm.Modifiers(p)};DruidTornado.Restore(new([shot],[],shot.Id));DruidTornado.Tick(shot.NextUtc,true);
        Require(life-SkillHealth.Balance256(target)==4*256,"Approved pulse potion channels through physical immunity");DruidTornado.Cancel(p);target.Delete();p.Delete();
        p=Fresh();Train(p,CombatAbility.Tornado);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        Require(p.EquipItem(helm),"Actual Tarnhelm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.Tornado)==2,"Actual effective rank");var values=SkillMechanics.Values(p,CombatAbility.Tornado);
        DruidTornado.Create(p,new(p.X+6,p.Y,p.Z),values,at);shot=DruidTornado.Snapshot().Missiles.Single();Require(shot.Rank==2&&shot.Min256==values.PhysicalDamage256(false),"Gear physical capture");DruidTornado.Cancel(p);
        var slow=AmazonSlowMissiles.Snapshot();
        try
        {
            AmazonSlowMissiles.Restore(slow.Append(new SlowMissilesState(p.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddDays(2))).ToArray());DruidTornado.Create(p,new(p.X+6,p.Y,p.Z),values,at);
            Require(DruidTornado.Snapshot().Missiles.Single() is{Velocity256:768} held&&held.Min256==values.PhysicalDamage256(false),"Slow velocity/physical endpoints");
        }
        finally{AmazonSlowMissiles.Restore(slow);DruidTornado.Cancel(p);p.Delete();}
        p=Fresh();Train(p,CombatAbility.Tornado);target=Enemy(p);target.CantWalk=false;Shield(target,1);var block=SourceBlocking.BlockRoll;
        try
        {
            SourceBlocking.BlockRoll=_=>0;ulong seed=0;for(int low=0;low<10000;low++){var test=SourceRandom.InitLow(low);var copy=test;if(SourceRandom.Roll(ref copy)%100==0){seed=test;break;}}
            Require(seed!=0,"One-percent source seed");shot=One(p,at) with{Frame=1,NextUtc=at.AddMilliseconds(80),Seed=seed};DruidTornado.Restore(new([shot],[],shot.Id));life=SkillHealth.Balance256(target);DruidTornado.Tick(shot.NextUtc,true);
            Require(SkillHealth.Balance256(target)==life&&SourceKnockbacks.Snapshot().Any(x=>x.Target==target.Serial.Value),"Blocked direct knockback independent of pulse");
        }
        finally{SourceBlocking.BlockRoll=block;SourceKnockbacks.Clear(target);DruidTornado.Cancel(p);target.Delete();p.Delete();}
        p=Fresh();Train(p,CombatAbility.Tornado);target=Enemy(p,6);life=SkillHealth.Balance256(target);DruidTornado.Create(p,new(p.X+8,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.Tornado),at);
        var walls=Enumerable.Range(-16,33).Select(y=>{var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y+y,p.Z),p.Map);return wall;}).ToArray();
        try{DruidTornado.Tick(at.AddMilliseconds(3000),true);Require(DruidTornado.Snapshot().Missiles.Length==0&&SkillHealth.Balance256(target)==life,"Flight crossed wall barrier");}
        finally{foreach(var wall in walls){wall.Delete();}DruidTornado.Cancel(p);target.Delete();p.Delete();}
        Log.Information("Tornado:area/direct/shield/history/NOAURA/potion/gear/slow/knockback/wall controls pass");
    }
    private static void Lifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.Tornado);var target=Enemy(p);var at=TimedEffects.Now.AddDays(1);var shot=One(p,at);int life=SkillHealth.Balance256(target);
        DruidTornado.Tick(shot.UntilUtc,false);Require(DruidTornado.Snapshot().Missiles.Length==0&&SkillHealth.Balance256(target)==life,"Offline aged without fighting");
        shot=One(p,at);var location=p.Location;int map=p.Map.MapID;p.MoveToWorld(location,Map.Felucca);DruidTornado.Tick(shot.NextUtc,true);
        Require(DruidTornado.Snapshot().Missiles.Single().Map==map&&SkillHealth.Balance256(target)==life,"Old-facet flight travelled/fought");DruidTornado.Tick(shot.UntilUtc,true);p.MoveToWorld(location,Map.Maps[map]);
        shot=One(p,at);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Amazon),"Class transition");DruidTornado.Tick(shot.NextUtc,true);Require(DruidTornado.Snapshot().Missiles.Length==0,"Class cleanup");target.Delete();p.Delete();
    }
    private static void WorkerAndMalformed()
    {
        var state=DruidTornado.Snapshot();var shot=state.Missiles.Single();string text=Text();
        var bytes=Task.Run(()=>{var writer=new BufferWriter(new byte[2*1024*1024],true);DruidTornado.Profiles.Serialize(writer);return writer.Buffer[..(int)writer.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0&&reader.ReadString()==text&&Text()==text,"Read-only worker serialization");
        var bad=new[]{state with{Missiles=null},state with{Leases=null},state with{NextId=0},state with{DelayScope="global-old"},state with{Missiles=[shot,shot]},
            state with{Missiles=[shot with{Frame=75}]},state with{Missiles=[shot with{NextUtc=shot.NextUtc.AddTicks(1)}]},state with{Missiles=[shot with{MotionAdapter="unknown"}]},
            state with{Missiles=[shot with{Path=[]}]},state with{Missiles=[shot with{Steps=null}]},state with{Missiles=[shot with{Cast=shot.Id+1}]},
            state with{Missiles=[shot with{LastTarget=0x40000001}]},state with{Missiles=[shot with{Min256=-1}]},state with{Leases=[new WeaponFlightLease(long.MaxValue,shot.Owner,shot.NextUtc)]}};
        foreach(var invalid in bad){bool refused=false;try{DruidTornado.Restore(invalid);}catch(InvalidDataException){refused=true;}Require(refused&&Text()==text,"Malformed restore partially replaced pool");}
        var copy=DruidTornado.Snapshot();copy.Missiles[0].Path[0]=new(99,99);copy.Missiles[0].Steps.Cells[0]=new(99,99);Require(Text()==text,"Snapshot mutable arrays escaped");
        DruidTornado.Restore(JsonSerializer.Deserialize<TornadoSave>(text));Require(Text()==text,"Exact JSON resumption");
    }
    private static void Save(PlayerMobile p,Mobile target)=>File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),DruidTornado.Snapshot(),_pending,_pendingMana)));
    private static void Check()
    {
        Require(DruidCombat.Implemented(CombatAbility.Tornado)&&DruidTornado.Snapshot().Missiles.Length==0,"Learned Tornado initial state");
        Source();Casting();PreparedAndPulses();EligibilityGearAndObstacles();Lifecycle();
        var p=Fresh();Train(p,CombatAbility.Tornado);var target=Enemy(p);TimedEffects.PreventHealing(target,TimedEffects.Now);var at=TimedEffects.Now.AddDays(2);
        var shot=One(p,at) with{Frame=1,NextUtc=at.AddMilliseconds(80)};DruidTornado.Restore(new([shot],[],shot.Id));DruidTornado.Tick(shot.NextUtc,true);
        Require(DruidTornado.Snapshot().Leases.Length==1,"Held direct history/lease setup");WorkerAndMalformed();Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Tornado)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Tornado)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Tornado),"Saved bindings");
        var pending=Fresh();Train(pending,CombatAbility.Tornado);Mana(pending);Ready(pending);_pending=pending.Serial.Value;_pendingMana=NecromancerEffects.ManaBalance256(pending);Require(Start(pending),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;Save(p,target);
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=World.FindMobile((Serial)saved.Target);
        Require(p!=null&&target!=null&&Text()==Text(saved.State)&&SkillHealth.Balance256(target)==saved.Life256,"Exact saved path/steps/seed/history/lease/HP");WorkerAndMalformed();
        _pending=saved.Pending;_pendingMana=saved.PendingMana;var pending=(PlayerMobile)World.FindMobile((Serial)_pending);
        Require(pending!=null&&NecromancerEffects.ManaBalance256(pending)==_pendingMana&&ClassCombat.For(pending).PendingAbility==CombatAbility.None,"Unpaid cast replayed");Require(SkillBindings.Instance.Get(p).Left==CombatAbility.Tornado&&SkillBindings.Instance.Get(p).Right==CombatAbility.Tornado&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Tornado,"Saved bindings");
        var before=DruidTornado.Snapshot().Missiles.Single();DruidTornado.Tick(before.NextUtc,true);Require(DruidTornado.Snapshot().Missiles.Single().Frame==before.Frame+1,"Saved clock failed to resume");Save(p,target);
    }
    private static void Live(NetState network,bool input)
    {
        string label=input?"tornado-input":"tornado-live";var held=DruidTornado.Snapshot();var p=Fresh();Train(p,CombatAbility.Tornado);Mana(p);Ready(p);
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Tornado)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Tornado)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Tornado),"Connected bindings");
        var aim=new Point3D(p.X+6,p.Y,p.Z);var target=Enemy(p);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(target);if(!input){DruidTornado.Create(p,aim,SkillMechanics.Values(p,CombatAbility.Tornado),TimedEffects.Now);}
        bool began=!input,released=!input,moved=false,damaged=false;long deadline=Core.TickCount+8000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Natural Tornado timeout");var shots=DruidTornado.Snapshot().Missiles.Where(x=>x.Owner==p.Serial.Value).ToArray();
                if(!began&&ClassCombat.For(p).PendingAbility==CombatAbility.Tornado){began=true;Require(shots.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Quick windup paid/emitted");}
                if(!released&&shots.Length>0){released=true;Require(began&&shots.Length==1&&NecromancerEffects.ManaBalance256(p)==mana-2560&&FireGround.Ready(p,CombatAbility.Tornado),"Quick once-only10mana/no local delay");}
                moved|=shots.Any(x=>x.X!=x.OriginX||x.Y!=x.OriginY);damaged|=SkillHealth.Balance256(target)<life;
                if(!released||shots.Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(moved&&damaged&&NecromancerEffects.ManaBalance256(p)==mana-(input?2560:0),"Natural flight/damage/expiry/once payment");DruidTornado.Cancel(p);target.Delete();p.Delete();
                Require(Text(DruidTornado.Snapshot() with{NextId=held.NextId})==Text(held),"Natural control changed held actors");var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var last=World.FindMobile((Serial)saved.Target);Require(SkillHealth.Balance256(last)==saved.Life256,"Natural control changed held injury");Save(owner,last);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception error){DruidTornado.Cancel(p);target.Delete();p.Delete();Log.Error(error,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} x={X} y={Y} z={Z}",label,(int)CombatAbility.Tornado,aim.X,aim.Y,aim.Z);Inspect();});
    }
}
