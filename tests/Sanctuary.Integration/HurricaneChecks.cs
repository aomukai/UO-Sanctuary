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
using Server.Spells;
namespace Sanctuary.Integration;

public static class HurricaneChecks
{
    private static readonly ILogger Log = LogFactory.GetLogger(typeof(HurricaneChecks));
    private sealed record Saved(uint Owner, uint Target, int Life256, HurricaneState[] State, uint Pending, int PendingMana, int Map, Point3D Origin);
    private static string Manifest => Path.Join(Core.BaseDirectory, "hurricane-fixtures.json");
    private static int _scene;
    private static bool _creating;
    private static void Rates()
    {
        var old = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = p => _creating || p.Name?.StartsWith("HurricaneFixture", StringComparison.Ordinal) == true
            ? TimeSpan.FromHours(1) : old?.Invoke(p) ?? Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)] public static void Initialize() => Rates();
    public static void Configure()
    {
        Rates(); Register("hurricane-check", Check); Register("hurricane-reload-check", Reload);
        Register("hurricane-live-check", () => FixtureNetwork.Connected("hurricane-live-result", n => Live(n, false)));
        Register("hurricane-input-check", () => FixtureNetwork.Connected("hurricane-input-result", n => Live(n, true)));
    }
    private static void Register(string name, Action action) => ConsoleInputHandler.RegisterCommand(name, "Hurricane weather, periodic cold and saved casting", _ => Core.LoopContext.Post(() =>
    { try { action(); Log.Information("{Name}: passed", name); } catch (Exception e) { Log.Error(e, "{Name}: failed", name); } }));
    private static void Require(bool condition, string why) { if (!condition) { throw new InvalidOperationException(why); } }
    private static string Text() => JsonSerializer.Serialize(DruidHurricane.Snapshot());
    private static PlayerMobile Fresh()
    {
        PlayerMobile p; _creating = true; try { p = new PlayerMobile { Body = 0x190, Player = true, Name = "HurricaneFixture" }; } finally { _creating = false; }
        p.AddItem(new Backpack()); ClassCreation.Profiles.Assign(p, DiabloClass.Druid); ClassCreation.Profiles.AdvanceLevel(p, 99);
        FixtureSpace.Corridor(p, 6880, 20, 700 + 32 * _scene++); return p;
    }
    private static void Train(PlayerMobile p, CombatAbility ability, int rank = 1)
    {
        var row = SkillMechanics.For(ability); Require(TalentCatalog.TryGet(DiabloClass.Druid, row.TalentId, out var talent), "Druid talent");
        foreach (string parent in talent.Prerequisites) { Train(p, SkillMechanics.All.Single(x => x.TalentId == parent).Ability); }
        while (ClassCreation.Profiles.GetRank(p.Serial, row.TalentId) < rank) { Require(ClassCreation.Profiles.TryTrain(p.Serial, row.TalentId, out _), "Train " + row.TalentId); }
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p, int dx = 1)
    {
        var target = new GraveyardSkeleton { Name = "HurricaneFixtureTarget", CantWalk = true, NoKillAwards = true }; target.SetHits(10000);
        foreach (var type in new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Energy, ResistanceType.Poison }) { target.SetResistance(type, 0); }
        target.MoveToWorld(new(p.X + dx, p.Y, p.Z), p.Map); target.AIObject.AITimer.Stop(); return target;
    }
    private static void Ready(PlayerMobile p)
    { ClassCombat.Stop(p); ClassCombat.For(p).NextAction = ClassCombat.For(p).RecoveryUntil = Core.TickCount; p.Frozen = p.Paralyzed = false; }
    private static void Mana(PlayerMobile p) => NecromancerEffects.SetMana256(p, p.ManaMax * 256);
    private static bool Cast(PlayerMobile p)
    {
        Ready(p); int mana = NecromancerEffects.ManaBalance256(p);
        Require(ClassCombat.Perform(p, CombatAbility.Hurricane, null, new(65530, 65530, 0), Core.TickCount), "Self SC start");
        var s = ClassCombat.For(p); Require(s.PendingTarget == Serial.Zero && s.PendingPoint == p.Location && s.Animation == 16 && NecromancerEffects.ManaBalance256(p) == mana, "Unpaid self windup");
        ClassCombat.ReleaseCast(p); bool result = ClassCombat.ResolveAction(p, s.EffectAt);
        Require(NecromancerEffects.ManaBalance256(p) == mana - (result ? 7680 : 0) && !ClassCombat.ResolveAction(p, s.EffectAt), "Once-only30mana payment"); return result;
    }
    private static void Source()
    {
        Require(ClassAbilities.SelfTarget(CombatAbility.Hurricane) && DruidCombat.Implemented(CombatAbility.Hurricane), "Self learned identity");
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory, "hurricane-damage-source-probe.json"))); int count = 0;
        var definition = SkillMechanics.For(CombatAbility.Hurricane);
        foreach (var row in data.RootElement.EnumerateArray())
        {
            if (row[3].GetInt32() != 0) { continue; }
            int rank = row[0].GetInt32(), t = row[1].GetInt32(), n = row[2].GetInt32();
            var values = new SkillValues(definition, key => key == "Hurricane" ? rank : 255, key => key == "Twister" ? t : key == "Tornado" ? n : 0, 99);
            Require(values.Damage256(false) == row[6].GetInt32() && values.Damage256(true) == row[7].GetInt32() && values.DurationFrames == 50, "Complete source endpoint/cold length"); count++;
        }
        Require(count == 9180, "Source endpoint scope");
        foreach (long tick in new long[] { 0, Core.TickCount, Core.TickCount + 800, Core.TickCount + 86400000 }) { Require(TimedEffects.TickAt(TimedEffects.TimeAt(tick)) == tick, "Shared effect-clock inverse"); }
        var potion = DruidMoltenBoulder.Potion(new(10, 0, 0, 0, 0), new(0, 0, 10, 0, 0));
        Require(potion.Cold == 11 && potion.Fire == 1 && potion.Lightning == 1 && potion.Poison == 1, "Shared potion retained cold base");
        Log.Information("Hurricane:9180source endpoint/length comparisons and shared clock/potion controls pass");
    }
    private static void Casting()
    {
        var p = Fresh(); Require(!ClassCombat.Available(p, CombatAbility.Hurricane), "Unlearned skill available"); Train(p, CombatAbility.Hurricane);
        Require(!SkillBindings.Instance.Assign(p, true, CombatAbility.Hurricane) && SkillBindings.Instance.Assign(p, false, CombatAbility.Hurricane) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Hurricane), "Right/quick only");
        Ready(p); NecromancerEffects.SetMana256(p, 7679); Require(!ClassCombat.Perform(p, CombatAbility.Hurricane, null, p.Location, Core.TickCount) && DruidHurricane.State(p) == null, "Low mana windup"); Mana(p);
        foreach (int cancel in new[] { 0, 1, 2, 3 })
        {
            Ready(p); Mana(p); int mana = NecromancerEffects.ManaBalance256(p); Require(ClassCombat.Perform(p, CombatAbility.Hurricane, null, p.Location, Core.TickCount), "Cancelled start"); long at = ClassCombat.For(p).EffectAt;
            if (cancel == 0) { ClassCombat.Stop(p); }
            if (cancel == 1) { var origin = p.Location; p.Direction = Direction.East; Require(p.Move(Direction.East), "Outward step"); p.Direction = Direction.West; Require(p.Move(Direction.West) && p.Location == origin, "Return step"); }
            if (cancel == 2) { NecromancerEffects.SetMana256(p, 0); }
            if (cancel == 3) { Require(HitRecovery.Apply(p, p.HitsMax, Core.TickCount), "Recovery cancellation"); }
            Require(!ClassCombat.ResolveAction(p, at) && DruidHurricane.State(p) == null && NecromancerEffects.ManaBalance256(p) == (cancel == 2 ? 0 : mana), "Cancelled cast paid/emitted");
        }
        Ready(p); Mana(p); Require(Cast(p) && FireGround.Ready(p, CombatAbility.Hurricane), "Learned cast/no local delay");
        var prior = DruidHurricane.State(p); Require(prior.Rank == 1 && prior.NextUtc - prior.StartedUtc == TimeSpan.FromMilliseconds(800) && prior.UntilUtc - prior.StartedUtc == TimeSpan.FromSeconds(12), "Captured rank/duration/first pulse");
        var helm = NamedGear.Create(NamedGear.Catalog.Items.Single(x => x.Name == "Tarnhelm").Id, 99, new Random(173), true); Require(p.EquipItem(helm), "Tarnhelm equip"); ClassGear.Refresh(p);
        Require(DruidHurricane.State(p) == prior && Cast(p) && DruidHurricane.State(p).Rank == 2 && DruidHurricane.Snapshot().Count(x => x.Owner == p.Serial.Value) == 1, "Captured rank/equal replacement");
        p.AddToBackpack(helm); ClassGear.Refresh(p); Require(Cast(p) && DruidHurricane.State(p).Rank == 1, "Lower-rank recast replaces weather");
        DruidHurricane.Clear(p); p.Delete();
    }
    private static void Pulses()
    {
        var p = Fresh(); Train(p, CombatAbility.Hurricane); var a = Enemy(p); var b = Enemy(p, 2); var outside = Enemy(p, 5); var recovery = Enemy(p, 3); recovery.SetHits(100); var at = TimedEffects.Now.AddHours(2);
        DruidHurricane.Create(p, SkillMechanics.Values(p, CombatAbility.Hurricane), at); var state = DruidHurricane.State(p); int life = SkillHealth.Balance256(a), other = SkillHealth.Balance256(b), far = SkillHealth.Balance256(outside);
        DruidHurricane.Tick(state.NextUtc.AddTicks(-1), true); Require(SkillHealth.Balance256(a) == life && DruidHurricane.State(p) == state, "Early pulse");
        var values = SkillMechanics.Values(p, CombatAbility.Hurricane, state.Rank); ulong seed = state.Seed; int raw = WeaponMissileDamage.RollValue(values.Damage256(false), values.Damage256(true), 0, n => (int)SourceRandom.Limited(ref seed, n));
        DruidHurricane.Tick(state.NextUtc, true);
        Require(life - SkillHealth.Balance256(a) == raw && other - SkillHealth.Balance256(b) == raw && SkillHealth.Balance256(outside) == far && DruidHurricane.State(p).Seed == seed, "One shared roll/center radius/ordinary cold damage");
        Require(TimedEffects.TryActor(a, out var chilled) && chilled.ChillPercent > 0 && chilled.ChillUntil > state.NextUtc && CreatureHitRecovery.Remaining(recovery) == 0, "Cold state without source get-hit request"); recovery.Delete();
        // New hard synergies are read on the next pulse; state rank/lifetime are not rewritten.
        Train(p, CombatAbility.Twister, 2); var next = DruidHurricane.State(p); values = SkillMechanics.Values(p, CombatAbility.Hurricane, next.Rank); seed = next.Seed;
        raw = WeaponMissileDamage.RollValue(values.Damage256(false), values.Damage256(true), 0, n => (int)SourceRandom.Limited(ref seed, n)); life = SkillHealth.Balance256(a);
        DruidHurricane.Tick(next.NextUtc, true); Require(life - SkillHealth.Balance256(a) == raw && DruidHurricane.State(p).UntilUtc == state.UntilUtc, "Live hard synergy versus captured lifetime");
        a.SetResistance(ResistanceType.Cold, 100); life = SkillHealth.Balance256(a); next = DruidHurricane.State(p); DruidHurricane.Tick(next.NextUtc, true); Require(SkillHealth.Balance256(a) == life, "Cold immunity");
        next = DruidHurricane.State(p); life = SkillHealth.Balance256(a); DruidHurricane.Tick(next.NextUtc, false); Require(SkillHealth.Balance256(a) == life && DruidHurricane.State(p).Seed == next.Seed, "Offline age/no RNG/damage");
        var map = p.Map; var point = p.Location; p.MoveToWorld(point, Map.Trammel); var newTarget = Enemy(p); next = DruidHurricane.State(p); life = SkillHealth.Balance256(a); int newLife = SkillHealth.Balance256(newTarget);
        DruidHurricane.Tick(next.NextUtc, true); Require(SkillHealth.Balance256(a) == life && SkillHealth.Balance256(newTarget) < newLife, "Weather follows caster facet");
        DruidHurricane.Tick(state.UntilUtc, true); Require(DruidHurricane.State(p) == null, "Natural deadline/no historical replay");
        p.MoveToWorld(point, map); var transition = Fresh(); Train(transition, CombatAbility.Hurricane); DruidHurricane.Create(transition, SkillMechanics.Values(transition, CombatAbility.Hurricane), at); Require(ClassCreation.Profiles.TryActivateHistory(transition, DiabloClass.Amazon) && DruidHurricane.State(transition) == null, "Immediate class cleanup outside combat"); transition.Delete();
        a.Delete(); b.Delete(); outside.Delete(); newTarget.Delete(); p.Delete();
    }
    private static void Eligibility()
    {
        var p=Fresh();Train(p,CombatAbility.Hurricane);var target=Enemy(p,3);var at=TimedEffects.Now.AddHours(1);
        string source=CreatureProfiles.Definition(target).Reference;
        var rules=(System.Collections.Generic.Dictionary<string,SourceAuraTarget>)typeof(SourceAuraTargets).GetField("_rules",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var original=rules[source];
        try{rules[source]=original with{NoAura=true,Npc=true};Require(DruidHurricane.Recipient(p,target,9),"Added optional NoAura/NPC exclusion");}finally{rules[source]=original;}
        var walls=Enumerable.Range(-8,17).Select(y=>{var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y+y,p.Z),p.Map);return wall;}).ToArray();
        try{Require(!DruidHurricane.Recipient(p,target,9),"Hurricane ignored source LOS gate");DruidHurricane.Create(p,SkillMechanics.Values(p,CombatAbility.Hurricane),at);int life=SkillHealth.Balance256(target);DruidHurricane.Tick(DruidHurricane.State(p).NextUtc,true);Require(SkillHealth.Balance256(target)==life,"Weather hit through wall");}finally{foreach(var wall in walls){wall.Delete();}DruidHurricane.Clear(p);}
        var town=new GuardedRegion("HurricaneFixtureTown",p.Map,1000,new Rectangle2D(p.X-1,p.Y-1,3,3));town.Register();
        try{Ready(p);Mana(p);Require(SpellHelper.IsTown(p.Location,p)&&!ClassCombat.Perform(p,CombatAbility.Hurricane,null,p.Location,Core.TickCount),"Town windup gate");DruidHurricane.Create(p,SkillMechanics.Values(p,CombatAbility.Hurricane),at);DruidHurricane.Tick(DruidHurricane.State(p).NextUtc,true);Require(DruidHurricane.State(p)==null,"Town failed to remove weather");}finally{town.Unregister();}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        DruidHurricane.Create(p,SkillMechanics.Values(p,CombatAbility.Hurricane),at);var state=DruidHurricane.State(p);Require(p.EquipItem(helm),"Live Tarnhelm");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.Hurricane)==2&&state.Rank==1,"Retained rank versus live gear");
        var values=SkillMechanics.Values(p,CombatAbility.Hurricane,state.Rank);ulong seed=state.Seed;int raw=WeaponMissileDamage.RollValue(values.Damage256(false),values.Damage256(true),0,n=>(int)SourceRandom.Limited(ref seed,n));int before=SkillHealth.Balance256(target);
        var block=SourceBlocking.BlockRoll;SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new SourceShield(target.Serial.Value,source,1)).ToArray());
        try{SourceBlocking.BlockRoll=_=>0;DruidHurricane.Tick(state.NextUtc,true);Require(before-SkillHealth.Balance256(target)==raw,"Area shield bypass/retained effective rank");}finally{SourceBlocking.BlockRoll=block;}
        target.SetResistance(ResistanceType.Cold,100);Require(CombatPotions.Apply(p,3),"Actual tier3 potion");state=DruidHurricane.State(p);values=SkillMechanics.Values(p,CombatAbility.Hurricane,state.Rank);seed=state.Seed;raw=WeaponMissileDamage.RollValue(values.Damage256(false),values.Damage256(true),0,n=>(int)SourceRandom.Limited(ref seed,n));before=SkillHealth.Balance256(target);
        var packet=DruidMoltenBoulder.Potion(DruidFirestorm.Modifiers(p),new(0,0,raw/256.0,0,0));int expected=NamedElements.To256(packet.Fire)+NamedElements.To256(packet.Lightning)+NamedElements.To256(packet.Poison);
        DruidHurricane.Tick(state.NextUtc,true);Require(before-SkillHealth.Balance256(target)==expected&&expected>0,"Approved potion channels through cold immunity");DruidHurricane.Clear(p);target.Delete();p.Delete();
        Log.Information("Hurricane:LOS/town/NoAura/retained gear/area shield/potion controls pass");
    }
    private static void WorkerAndMalformed()
    {
        string before = Text(); var rows = DruidHurricane.Snapshot(); var state = rows.Single();
        var bytes = Task.Run(() => { var w = new BufferWriter(new byte[1024 * 1024], true); DruidHurricane.Profiles.Serialize(w); return w.Buffer[..(int)w.Position]; }).GetAwaiter().GetResult();
        var r = new BufferReader(bytes); Require(r.ReadInt() == 0 && r.ReadString() == before && Text() == before, "Read-only worker serialization");
        foreach (var bad in new HurricaneState[][] { null, [state, state], [state with { Rank = 0 }], [state with { Owner = 0x40000001 }], [state with { NextUtc = state.NextUtc.AddTicks(1) }], [state with { UntilUtc = state.StartedUtc }], [state with { StartedUtc = DateTime.SpecifyKind(state.StartedUtc, DateTimeKind.Local) }] })
        { bool refused = false; try { DruidHurricane.Restore(bad); } catch (InvalidDataException) { refused = true; } Require(refused && Text() == before, "Atomic malformed restore"); }
        rows[0] = state with { Rank = 255 }; Require(Text() == before, "Snapshot isolation"); DruidHurricane.Restore(JsonSerializer.Deserialize<HurricaneState[]>(before)); Require(Text() == before, "Exact JSON state");
    }
    private static void Check()
    {
        Require(DruidHurricane.Snapshot().Length == 0, "Initial weather state"); Source(); Casting(); Pulses(); Eligibility();
        var p = Fresh(); Train(p, CombatAbility.Hurricane); var target = Enemy(p); TimedEffects.PreventHealing(target, TimedEffects.Now);
        DruidHurricane.Create(p, SkillMechanics.Values(p, CombatAbility.Hurricane), TimedEffects.Now.AddDays(2)); var state = DruidHurricane.State(p);
        DruidHurricane.Tick(state.NextUtc, true); WorkerAndMalformed(); Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Hurricane) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Hurricane), "Saved bindings");
        var pending = Fresh(); Train(pending, CombatAbility.Hurricane); Mana(pending); Ready(pending); int mana = NecromancerEffects.ManaBalance256(pending);
        Require(ClassCombat.Perform(pending, CombatAbility.Hurricane, null, pending.Location, Core.TickCount), "Saved unpaid windup"); ClassCombat.For(pending).EffectAt = Core.TickCount + 3600000; ClassCombat.For(pending).NextAction = Core.TickCount + 3601000;
        File.WriteAllText(Manifest, JsonSerializer.Serialize(new Saved(p.Serial.Value, target.Serial.Value, SkillHealth.Balance256(target), DruidHurricane.Snapshot(), pending.Serial.Value, mana, p.Map.MapID, p.Location)));
    }
    private static void Reload()
    {
        Source(); var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)); var p = (PlayerMobile)World.FindMobile((Serial)saved.Owner); var target = World.FindMobile((Serial)saved.Target); var pending = (PlayerMobile)World.FindMobile((Serial)saved.Pending);
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(Text() == JsonSerializer.Serialize(saved.State) && SkillHealth.Balance256(target) == saved.Life256, "Saved weather clock/seed/rank/injury"); WorkerAndMalformed();
        Require(pending != null && DruidHurricane.State(pending) == null && ClassCombat.For(pending).PendingAbility == CombatAbility.None && NecromancerEffects.ManaBalance256(pending) == saved.PendingMana, "Unpaid cast replayed");
        Require(SkillBindings.Instance.Get(p).Right == CombatAbility.Hurricane && SkillBindings.Instance.GetQuick(p.Serial, DiabloClass.Druid, 7) == CombatAbility.Hurricane, "Saved binding identity");
        var before = DruidHurricane.State(p); DruidHurricane.Tick(before.NextUtc, true); Require(DruidHurricane.State(p).NextUtc == before.NextUtc.AddMilliseconds(800) && SkillHealth.Balance256(target) < saved.Life256, "Reload resumes actual periodic cold");
        File.WriteAllText(Manifest, JsonSerializer.Serialize(saved with { State = DruidHurricane.Snapshot(), Life256 = SkillHealth.Balance256(target) }));
    }
    private static void Live(NetState network, bool input)
    {
        string label = input ? "hurricane-input" : "hurricane-live"; var p = Fresh(); Train(p, CombatAbility.Hurricane); Mana(p); Ready(p); var target = Enemy(p); int life = SkillHealth.Balance256(target), mana = NecromancerEffects.ManaBalance256(p);
        network.Seeded = true; network.CompressionEnabled = false; network.Mobile = p; p.NetState = network;
        var protocol = typeof(NetState).GetField("_protocolState", BindingFlags.Instance | BindingFlags.NonPublic); Require(protocol != null, "Protocol bootstrap"); protocol.SetValue(network, Enum.Parse(protocol.FieldType, "GameServer_LoggedIn")); network.ResetMovementState(); network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p, false, CombatAbility.Hurricane) && SkillBindings.Instance.AssignQuick(p, 7, CombatAbility.Hurricane), "Connected bindings");
        if (!input) { DruidHurricane.Create(p, SkillMechanics.Values(p, CombatAbility.Hurricane), TimedEffects.Now); }
        bool began = !input, released = !input, damaged = false; long deadline = Core.TickCount + 17000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount < deadline, "Natural weather timeout"); var state = DruidHurricane.State(p);
                if (!began && ClassCombat.For(p).PendingAbility == CombatAbility.Hurricane) { began = true; Require(state == null && NecromancerEffects.ManaBalance256(p) == mana, "Unpaid quick windup"); }
                if (!released && state != null) { released = true; Require(began && NecromancerEffects.ManaBalance256(p) == mana - 7680, "Quick release/payment"); }
                damaged |= SkillHealth.Balance256(target) < life;
                if (!released || state != null) { Timer.StartTimer(TimeSpan.FromMilliseconds(30), Inspect); return; }
                Require(damaged && NecromancerEffects.ManaBalance256(p) == mana - (input ? 7680 : 0), "Natural pulses/expiry/no periodic mana"); DruidHurricane.Clear(p); target.Delete(); p.Delete(); Log.Information("{Label}-result: passed", label);
            }
            catch (Exception e) { DruidHurricane.Clear(p); target.Delete(); p.Delete(); Log.Error(e, "{Label}-result: failed", label); }
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30), () => { Log.Information("{Label}: ready ability={Ability} x=65530 y=65530 z=0", label, (int)CombatAbility.Hurricane); Inspect(); });
    }
}
