// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary;

public static class ClassCombat
{
    public sealed class Session
    {
        public Serial Target { get; internal set; }
        public CombatAbility Left { get; set; } = CombatAbility.BasicAttack;
        public CombatAbility Right { get; set; } = CombatAbility.Teeth;
        public uint Sequence { get; internal set; }
        public long NextAction { get; internal set; } = Core.TickCount;
        public CombatAbility PendingAbility { get; internal set; }
        public long EffectAt { get; internal set; } = Core.TickCount;
        public long RecoveryUntil { get; internal set; } = Core.TickCount;
        public ActionTiming Timing { get; internal set; }
        public byte Animation { get; internal set; }
        internal Serial PendingTarget, PendingWeapon;
        internal TeleportCast PendingTeleport;
        internal DruidPlayerForm AttackForm;
        internal DruidFormCasting.Pending Transform;
        internal DruidFormInterruption.Action FormAction;
        internal PaladinZeal.Series Zeal;
        internal PaladinCharge.Flight Charge;
        internal PaladinConversion.Prepared Conversion;
        internal BarbarianMelee.Prepared BarbarianAttack;
        internal BarbarianDoubleThrow.Series DoubleThrow;
        internal AmazonJab.Series Jab;
        internal AmazonImpale.Attack Impale;
        internal AmazonFend.Series Fend;
        internal AmazonStrafe.Series Strafe;
        internal AmazonMelee.Prepared AmazonAttack;
        internal TigerStrikes.Pending TigerAttack;
        internal DruidChargedAttacks.Pending DruidChargeAttack;
        internal DruidFireClaws.Pending FireClawAttack;
        internal DruidHunger.Pending HungerAttack;
        internal DruidRabies.Pending RabiesAttack;
        internal DruidShockWave.Pending ShockWaveAttack;
        internal DruidFury.Series Fury;
        internal string RabiesFault;
        internal AssassinClaws.Series ClawAttack;
        internal Serial FindItemTarget;
        internal Serial FindPotionTarget;
        internal BarbarianConcentrate.Mode Concentrate;
        internal BarbarianBerserk.Temporary BerserkMode;
        internal bool ZealPaid;
        internal int ZealHits;
        internal Point3D PendingPoint, ActionOrigin;
        internal Map ActionMap;
        internal bool PendingLeft;
        internal int TooltipSignature;
        internal bool TooltipKnown, WasOverlapped;
        public bool Running { get; internal set; } = true;
        internal bool RunningStep;
        internal long RunningUntil = Core.TickCount;
        internal Point3D StepPosition;
        internal Map StepMap;
        public CombatAbility HeldAbility { get; internal set; }
        public Serial CastTarget { get; internal set; }
        public bool HeldAttack { get; internal set; }
        internal Point3D CastPoint;
        internal long HoldUntil = Core.TickCount;
        internal long LastState = Core.TickCount - CombatProtocol.StateIntervalMs;
        internal long Started = Core.TickCount;
        internal bool Handshake;
        internal Point3D LastPosition;
        internal long LastProgress = Core.TickCount;
    }
    private static readonly Dictionary<Serial, Session> _sessions = new();
    internal static Session Existing(PlayerMobile owner)=>owner!=null && _sessions.TryGetValue(owner.Serial,out var state)?state:null;
    private static readonly List<Serial> _expired = new();

    public static bool Enabled(PlayerMobile from) => ClassCreation.Profiles?.TryGet(from.Serial, out _) == true;
    public static Session For(PlayerMobile from)
    {
        if (!_sessions.TryGetValue(from.Serial, out var session))
        {
            _sessions[from.Serial] = session = new Session
            {
                LastPosition = from.Location,
                Left = SkillBindings.Instance.Get(from).Left,
                Right = SkillBindings.Instance.Get(from).Right
            };
        }
        return session;
    }
    public static void Configure()
    {
        ClassCombatHooks.Enabled = Enabled;
        ClassCombatHooks.Friendly = Friendly;
        ClassCombatHooks.AttackRequested = (from, target) => Begin(from, target);
        ClassCombatHooks.Moved = ClassMovement.Moved;
        ClassCombatHooks.MovementAllowed = from => ChargeMotion.Active(from)
            || !HitRecovery.Active(from,Core.TickCount) && !AmazonStrafe.Locked(from,Core.TickCount) && !AmazonFend.Locked(from,Core.TickCount) && !AmazonImpale.Locked(from,Core.TickCount) && !AmazonJab.Locked(from,Core.TickCount) && !PaladinZeal.Locked(from,Core.TickCount) && !PaladinCharge.Locked(from,Core.TickCount) && !BarbarianConcentrate.Locked(from,Core.TickCount) && !BarbarianLeap.Locked(from) && !BarbarianWhirlwind.Locked(from);
        ClassCombatHooks.RunningDelay = (from, delay) => ClassCombat.Enabled(from)
            ? EquipmentBalance.RunDelay(delay, EquipmentBalance.RunPenalty(from), CombatPotions.RunBonus(from) + FireGround.Speed(from) + PaladinVigor.VelocityPercent(from)+BarbarianCombat.Passive(from,CombatAbility.IncreasedSpeed)+AssassinBuffs.Velocity(from)+DruidAttackCharges.Velocity(from)) : delay;
        ClassCombatHooks.WalkingDelay = (from, delay) => ClassCombat.Enabled(from) ? EquipmentBalance.RunDelay(delay, 0, FireGround.Speed(from) + PaladinVigor.VelocityPercent(from)+BarbarianCombat.Passive(from,CombatAbility.IncreasedSpeed)+AssassinBuffs.Velocity(from)+DruidAttackCharges.Velocity(from)) : delay;
        ClassCombatHooks.EquipmentChanged = from =>
        {
            if (ClassCombat.Enabled(from))
            {
                if(DualEquip.Deferred(from)){return;}
                DualEquip.Changed(from);
                StatNetwork.Send(from);
                ClassGear.Changed(from);
                AmazonBow.EquipmentChanged(from);
                AmazonJavelins.EquipmentChanged(from);
                BladeFuryModes.EquipmentChanged(from);DruidArcticModes.EquipmentChanged(from);
                TigerStrikes.EquipmentChanged(from);
                DruidChargedAttacks.EquipmentChanged(from);
                DruidFireClaws.EquipmentChanged(from);DruidHunger.EquipmentChanged(from);DruidRabies.EquipmentChanged(from);DruidShockWave.EquipmentChanged(from);DruidFury.EquipmentChanged(from);
                AssassinClaws.EquipmentChanged(from);
            }
        };
        ClassCombatHooks.DefenseSuppressed = from => ClassMovement.IsRunning(from, Core.TickCount);
        EventSink.Disconnected += Remove;
        EventSink.Logout += Remove;
    }
    public static unsafe void Initialize()
    {
        var stockManaRegen = Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler = mobile => mobile is PlayerMobile player && Enabled(player)
            ? TimeSpan.FromSeconds(2) : stockManaRegen?.Invoke(mobile) ?? Mobile.DefaultManaRate;
        var stockStamRegen = Mobile.StamRegenRateHandler;
        Mobile.StamRegenRateHandler = mobile => mobile is PlayerMobile player && Enabled(player)
            ? TimeSpan.FromSeconds(5) : stockStamRegen?.Invoke(mobile) ?? Mobile.DefaultStamRate;
        IncomingExtendedCommandPackets.RegisterExtended(CombatProtocol.SubCommand, true, &Receive);
        Timer.StartTimer(TimeSpan.FromMilliseconds(CombatProtocol.CombatTickMs),
            TimeSpan.FromMilliseconds(CombatProtocol.CombatTickMs), Tick);
    }
    private static void Remove(Mobile from) => _sessions.Remove(from.Serial);

    public static bool Friendly(Mobile source, Mobile target)
    {
        if(SkillHealth.IsSelfCost(source,target)){return false;}
        if(PaladinConversion.Friendly(source,target)){return true;}
        static bool Allied(Mobile mobile) => mobile is PlayerMobile player ? Enabled(player)
            : mobile is SkeletonGuardian || mobile is BaseCreature pet && pet.GetMaster() is PlayerMobile owner && Enabled(owner);
        return source != null && target != null && Allied(source) && Allied(target);
    }
    public static bool IsHostile(PlayerMobile from, Mobile target) =>
        target is BaseCreature creature && target != from && target.Alive && !target.Deleted
        && target.Map == from.Map && !target.Blessed && !target.IsDeadBondedPet && !PaladinConversion.Active(target)
        && creature is not BaseVendor
        && !creature.Controlled && !creature.Summoned && creature.GetMaster() == null
        && !creature.InitialInnocent && creature.FightMode is not (FightMode.None or FightMode.Aggressor or FightMode.Evil)
        && creature.IsEnemy(from) && from.CanSee(target) && from.CanBeHarmful(target, false);

    public static int TeethRank(PlayerMobile from)
    {
        if (!ClassCreation.Profiles.TryGet(from.Serial, out var profile) || profile.Class != DiabloClass.Necromancer)
        {
            return 0;
        }
        var weapon = CombatTiming.Equipped(from);
        return NamedSkills.Rank(from, "necromancer.teeth", ClassCreation.Profiles.GetRank(from.Serial, "necromancer.teeth")
            + (ClassEquipment.IsStarter(weapon) && ClassGear.Active(from, weapon) ? 1 : 0));
    }
    public static bool Available(PlayerMobile from, CombatAbility ability) => ShapeRestrictions.Allowed(from,ability)&&(ability switch
    {
        CombatAbility.BasicAttack => Enabled(from) && (DruidFormAttacks.Active(from)?DruidFormAttacks.Supported(from):NamedAmmunition.CanAttack(CombatTiming.Equipped(from))),
        CombatAbility.Teeth => TeethRank(from) > 0,
        CombatAbility.RaiseSkeleton => SkeletonPets.Rank(from) > 0,
        CombatAbility.Teleport => Teleportation.Rank(from) > 0,
        _ => NecromancerCombat.Available(from, ability) || SorceressCombat.Available(from, ability) || PaladinCombat.Available(from, ability) || BarbarianCombat.Available(from,ability) || AmazonCombat.Available(from,ability) || AssassinCombat.Available(from,ability) || DruidCombat.Available(from,ability)
    });
    public static bool CastSummon(PlayerMobile from, Serial preferred, long now)
    {
        if (!Enabled(from) || !from.Alive || !AlliedOverlap.Allowed(from) || from.Frozen || from.Paralyzed || from.Spell != null
            || HitRecovery.Active(from, now))
        {
            return false;
        }
        var session = For(from);
        session.Target = Serial.Zero;
        ReleaseCast(from);
        if (now - session.NextAction < 0 || session.PendingAbility != CombatAbility.None)
        {
            return false;
        }
        var corpse = SkeletonPets.FindCorpse(from, preferred);
        if (!SkeletonPets.CanRaise(from, corpse))
        {
            return false;
        }
        session.Timing = CombatTiming.For(from, CombatAbility.RaiseSkeleton);
        session.NextAction = now + session.Timing.DurationMs;
        session.EffectAt = now + session.Timing.EffectMs;
        session.PendingAbility = CombatAbility.RaiseSkeleton;
        session.PendingTarget = corpse.Serial;
        session.PendingWeapon = CombatTiming.Equipped(from)?.Serial ?? Serial.Zero;
        session.ActionOrigin = from.Location;
        session.ActionMap = from.Map;
        session.Animation = 16;
        from.Direction = from.GetDirectionTo(corpse);
        from.RevealingAction();
        SendState(from, session);
        SendAnimation(from, session);
        from.Animate(16, 5, 1, true, false, 0);
        return true;
    }
    public static bool Begin(PlayerMobile from, Mobile target)
    {
        var session = For(from);
        Stop(from);
        if (!Enabled(from) || !from.Alive || !AlliedOverlap.Allowed(from) || !Available(from, session.Left) || !IsHostile(from, target)
            || !from.InRange(target, CombatProtocol.SightRange))
        {
            return false;
        }
        session.Target = target.Serial;
        session.Started = session.LastProgress = Core.TickCount;
        session.LastPosition = from.Location;
        return true;
    }
    public static void ReleaseCast(PlayerMobile from)
    {
        var session = For(from);
        session.HeldAbility = CombatAbility.None;
        session.HeldAttack = false;
        session.CastTarget = Serial.Zero;
    }
    public static void Stop(PlayerMobile from)
    {
        if(AmazonStrafe.Locked(from,Core.TickCount) || AmazonFend.Locked(from,Core.TickCount) || AmazonImpale.Locked(from,Core.TickCount) || AmazonJab.Locked(from,Core.TickCount) || PaladinZeal.Locked(from,Core.TickCount) || PaladinCharge.Locked(from,Core.TickCount) || BarbarianConcentrate.Locked(from,Core.TickCount) || BarbarianLeap.Locked(from) || BarbarianWhirlwind.Locked(from)){For(from).Target=Serial.Zero;ReleaseCast(from);return;}
        BarbarianConcentrate.Cancel(from);
        BarbarianDoubleThrow.Cancel(from);
        AmazonJab.Cancel(from);
        AmazonImpale.Cancel(from);
        AmazonStrafe.Cancel(from);
        AmazonFend.Cancel(from);
        AmazonMelee.Cancel(from);
        TigerStrikes.Cancel(from);
        DruidChargedAttacks.Cancel(from);
        DruidFireClaws.Cancel(from);DruidHunger.Cancel(from);DruidRabies.Cancel(from);DruidShockWave.Cancel(from);DruidFury.Cancel(from);
        AssassinClaws.Cancel(from);
        BarbarianBerserk.CancelMode(from);
        BladeShields.Cancel(from);
        BladeFuryModes.Interrupt(from);DruidArcticModes.Interrupt(from);
        PaladinCharge.Cancel(from);
        For(from).Target = Serial.Zero;
        For(from).PendingAbility = CombatAbility.None;
        For(from).AttackForm = null;
        For(from).Transform = null;
        For(from).FormAction = null;
        For(from).Conversion = null;
        For(from).BarbarianAttack = null;
        For(from).FindItemTarget = Serial.Zero;
        For(from).FindPotionTarget = Serial.Zero;
        For(from).PendingTeleport = null;
        For(from).Zeal = null;
        For(from).ZealPaid = false;
        For(from).ZealHits = 0;
        ReleaseCast(from);
    }
    public static int ActionInterval(PlayerMobile from, CombatAbility ability) => PaladinCombat.IsAura(ability) ? 0 : CombatTiming.For(from, ability).DurationMs;
    public static bool ContinueCast(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now)
        => ContinueHold(from, ability, target, point, now, false);

    public static bool ContinueAttack(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now)
        => ContinueHold(from, ability, target, point, now, true);

    private static bool CastTargetAllowed(PlayerMobile from, CombatAbility ability, Mobile target) => IsHostile(from, target)
        || ability is (CombatAbility.Raven or CombatAbility.SummonSpiritWolf or CombatAbility.SummonDireWolf or CombatAbility.SummonGrizzly or CombatAbility.OakSage or CombatAbility.HeartOfWolverine or CombatAbility.SpiritOfBarbs) && target is { Deleted: false } && target.Map==from.Map
        || ClassAbilities.AllyTarget(ability) && target is { Alive: true, Deleted: false } && target.Map == from.Map
            && (target == from || Friendly(from, target));

    private static bool ContinueHold(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now, bool attack)
    {
        if(ability==CombatAbility.CloakOfShadows){CloakShadows.Face(from,point);target=null;point=from.Location;}
        if(ClassAbilities.SelfTarget(ability)){target=null;point=from.Location;}
        if (!attack && PaladinCombat.IsAura(ability)) { return ability==For(from).Right && PaladinCombat.Begin(from,ability); }
        var session = For(from);
        session.Target = Serial.Zero;
        if (!Enabled(from) || !from.Alive || ability != (attack ? session.Left : session.Right) || !Available(from, ability)
            || target != null && !CastTargetAllowed(from, ability, target))
        {
            ReleaseCast(from);
            return false;
        }
        session.HeldAbility = ability;
        session.HeldAttack = attack;
        session.CastTarget = target?.Serial ?? Serial.Zero;
        session.CastPoint = point;
        session.HoldUntil = now + CombatProtocol.HoldLeaseMs;
        if (ability != CombatAbility.Teleport && session.PendingAbility == ability && session.PendingLeft == attack)
        {
            // Ground FoH/Psychic Hammer already acquired a primary. Held empty-ground input
            // must not erase that identity before the cast event.
            if(ability!=CombatAbility.Strafe && ability!=CombatAbility.Fend && ability!=CombatAbility.Impale && !AssassinClaws.Handles(ability) && !AmazonMelee.Handles(ability) && !BarbarianMelee.Handles(ability) && (ability is not (CombatAbility.FistOfTheHeavens or CombatAbility.PsychicHammer) || session.CastTarget!=Serial.Zero)){session.PendingTarget = session.CastTarget;}
            session.PendingPoint = point;
        }
        UpdateHeldCast(from, now);
        return true;
    }
    private static void UpdateHeldCast(PlayerMobile from, long now)
    {
        var session = For(from);
        if (now - session.HoldUntil >= 0 || !from.Alive || !Available(from, session.HeldAbility)
            || session.HeldAbility != (session.HeldAttack ? session.Left : session.Right))
        {
            ReleaseCast(from);
            return;
        }
        var target = World.FindMobile(session.CastTarget);
        if (session.CastTarget != Serial.Zero && (target == null || !CastTargetAllowed(from, session.HeldAbility, target)))
        {
            ReleaseCast(from);
            return;
        }
        Perform(from, session.HeldAbility, target, session.CastPoint, now, session.HeldAttack);
        if (target != null && (target.Deleted || !target.Alive))
        {
            ReleaseCast(from);
        }
    }
    public static void UpdateCombat(PlayerMobile from, long now)
    {
        if(BarbarianLeap.Locked(from) || BarbarianWhirlwind.Locked(from)){return;}
        if (!AlliedOverlap.Allowed(from))
        {
            if(For(from).Charge!=null){ResolveAction(from,now);return;}
            var overlap = For(from);
            if (!overlap.WasOverlapped) { from.SendMessage("You are sharing a tile with an ally. Walk clear before taking another action."); }
            overlap.WasOverlapped = true; Stop(from); return;
        }
        For(from).WasOverlapped = false;
        ResolveAction(from, now);
        // Resolve a due weapon event before expiring its animation state, including
        // a delayed server tick that crosses both the impact and recovery deadline.
        BarbarianConcentrate.Tick(from,now);
        BarbarianBerserk.Tick(from,now);
        var session = For(from);
        if(session.ClawAttack!=null || session.DoubleThrow!=null || session.Jab!=null || session.Fend!=null || session.Strafe!=null || session.Fury!=null){return;}
        if (session.HeldAbility != CombatAbility.None)
        {
            UpdateHeldCast(from, now);
            return;
        }
        if (session.Target == Serial.Zero)
        {
            return;
        }
        var target = World.FindMobile(session.Target);
        if (!from.Alive || !IsHostile(from, target) || !from.InRange(target, CombatProtocol.SightRange)
            || !Available(from, session.Left))
        {
            Stop(from);
            return;
        }
        if (from.Location != session.LastPosition)
        {
            session.LastPosition = from.Location;
            session.LastProgress = now;
        }
        var range = CombatTiming.Range(from, session.Left);
        if (!from.InRange(target, range) || !from.InLOS(target))
        {
            if (now - session.LastProgress >= 8000 || now - session.Started >= 30000)
            {
                Stop(from);
            }
            return;
        }
        Perform(from, session.Left, target, target.Location, now);
        if (!target.Alive || target.Deleted)
        {
            Stop(from);
        }
    }
    internal static bool CorpseCast(CombatAbility ability)=>ClassAbilities.CorpseTarget(ability) && ability!=CombatAbility.RaiseSkeleton;
    internal static bool BeginCorpse(PlayerMobile owner,CombatAbility ability,Serial selected,Point3D point,long now)=>ability==CombatAbility.FindPotion?BarbarianFindPotion.Begin(owner,selected,point,now):ability==CombatAbility.FindItem
        ?BarbarianFindItem.Begin(owner,selected,point,now):NecromancerCombat.CorpseTarget(ability) && NecromancerCombat.Begin(owner,ability,selected,point,now);
    public static bool Perform(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now)
        => Perform(from, ability, target, point, now, false);

    private static bool Perform(PlayerMobile from, CombatAbility ability, Mobile target, Point3D point, long now, bool attackInPlace)
    {
        if(!ShapeRestrictions.Allowed(from,ability)){return false;}
        var previous=For(from);
        if(previous.Zeal is{Remaining:<=0} && now-previous.NextAction>=0){previous.Zeal=null;}
        if (AmazonCombat.Handles(ability)) { return AmazonCombat.Begin(from,ability,target?.Serial ?? Serial.Zero,point,now); }
        if (BarbarianCombat.Handles(ability)) { return BarbarianCombat.Begin(from,ability,target?.Serial ?? Serial.Zero,point,now); }
        if (PaladinCombat.Handles(ability)) { return PaladinCombat.Begin(from,ability,target?.Serial ?? Serial.Zero,point,now); }
        if (SorceressCombat.Handles(ability)) { return SorceressCombat.Begin(from, ability, target?.Serial ?? Serial.Zero, point, now); }
        if (AssassinCombat.Handles(ability)) { return AssassinCombat.Begin(from,ability,target?.Serial ?? Serial.Zero,point,now); }
        if (DruidCombat.Handles(ability)) { return DruidCombat.Begin(from,ability,target?.Serial ?? Serial.Zero,point,now); }
        if (NecromancerCombat.Handles(ability))
        {
            return NecromancerCombat.Begin(from, ability, target?.Serial ?? Serial.Zero, point, now);
        }
        if (ability == CombatAbility.Teleport)
        {
            return Teleportation.Begin(from, point, now);
        }
        if (ability == CombatAbility.RaiseSkeleton)
        {
            return CastSummon(from, Serial.Zero, now);
        }
        var session = For(from);
        if (!Enabled(from) || !from.Alive || !AlliedOverlap.Allowed(from) || from.Frozen || from.Paralyzed || from.Spell != null
            || HitRecovery.Active(from, now) || !Available(from, ability) || now - session.NextAction < 0 || session.PendingAbility != CombatAbility.None)
        {
            return false;
        }
        if (target != null)
        {
            if (!IsHostile(from, target))
            {
                return false;
            }
            point = target.Location;
        }
        if (ability != CombatAbility.BasicAttack && (point.X < 0 || point.Y < 0 || point.X >= from.Map.Width || point.Y >= from.Map.Height
            || target == null && point.X == from.X && point.Y == from.Y || from.Mana < 1))
        {
            return false;
        }
        var weapon = CombatTiming.Equipped(from);
        session.Timing = CombatTiming.For(from, ability, weapon);
        session.NextAction = now + session.Timing.DurationMs;
        session.EffectAt = now + session.Timing.EffectMs;
        session.PendingAbility = ability;
        session.AttackForm = ability==CombatAbility.BasicAttack?DruidPlayerForms.State(from):null;
        session.PendingTarget = target?.Serial ?? Serial.Zero;
        session.PendingPoint = point;
        session.PendingWeapon = weapon?.Serial ?? Serial.Zero;
        session.PendingLeft = attackInPlace;
        session.ActionOrigin = from.Location;
        session.ActionMap = from.Map;
        session.Animation = AnimationFor(from, ability, weapon);
        DruidFormInterruption.Capture(from,now);
        if (point.X != from.X || point.Y != from.Y)
        {
            from.Direction = from.GetDirectionTo(point);
        }
        from.RevealingAction();
        SendState(from, session);
        SendAnimation(from, session);
        from.Animate(session.Animation, ability == CombatAbility.Teeth ? 5 : 7, 1, true, false, 0);
        return true;
    }
    private static void SendAnimation(PlayerMobile from, Session session)
        => SendAnimation(from, session.Animation, session.Timing.DurationMs);
    internal static void SendAnimation(Mobile from, byte animation, int durationMs)
    {
        Span<byte> data = stackalloc byte[13];
        var writer = new SpanWriter(data);
        writer.Write((byte)0xBF);
        writer.Write((ushort)data.Length);
        writer.Write(CombatProtocol.AnimationSubCommand);
        writer.Write(CombatProtocol.Version);
        writer.Write(from.Serial.Value);
        writer.Write(animation);
        writer.Write((ushort)durationMs);
        foreach (var state in from.Map.GetClientsInRange(from.Location))
        {
            state.Send(data);
        }
    }
    internal static byte AnimationFor(PlayerMobile from, CombatAbility ability, BaseWeapon weapon)
    {
        if((DruidFormAttacks.SingleA1(ability)||ability is (CombatAbility.Hunger or CombatAbility.Rabies or CombatAbility.Fury))&&DruidFormAttacks.Active(from)){return (byte)WeaponAnimation.Wrestle;} // Identified human melee pose until final form bodies.
        if (ability is CombatAbility.FireBlast or CombatAbility.ShockWeb){return from.Mounted?(byte)26:(byte)9;} // Identified throw animation placeholder.
        if (ability == CombatAbility.Teeth)
        {
            return 16;
        }
        var animation = CombatTiming.Animation(weapon);
        if (!from.Mounted)
        {
            return (byte)animation;
        }
        return animation switch
        {
            WeaponAnimation.ShootBow => 27,
            WeaponAnimation.ShootXBow => 28,
            WeaponAnimation.Slash2H or WeaponAnimation.Bash2H or WeaponAnimation.Pierce2H => 29,
            _ => 26
        };
    }
    private static bool MeleeReach(PlayerMobile from, Mobile target) => target != null
        && from.InRange(target, CombatTiming.Range(from, CombatAbility.BasicAttack)) && from.InLOS(target)
        && Math.Abs(from.Z - target.Z) < 16;

    // Input release ends repetition; an already-started action can still finish.
    // Stop/movement/incapacitation can cancel its effect without refunding its cycle.
    public static bool ResolveAction(PlayerMobile from, long now)
    {
        if(BarbarianLeap.State(from)!=null || BarbarianWhirlwind.State(from)!=null){return false;}
        if(For(from).ClawAttack!=null){return AssassinClaws.Resolve(from,now);}
        if(For(from).Jab!=null){return AmazonJab.Resolve(from,now);}
        if(For(from).Impale!=null){return AmazonImpale.Resolve(from,now);}
        if(For(from).Strafe!=null){return AmazonStrafe.Resolve(from,now);}
        if(For(from).Fend!=null){return AmazonFend.Resolve(from,now);}
        if(For(from).Fury!=null){return DruidFury.Resolve(from,now);}
        if(For(from).DoubleThrow!=null){return BarbarianDoubleThrow.Resolve(from,now);}
        var session = For(from);
        if(session.Charge!=null){return PaladinCharge.Resolve(from,now);}
        if(session.Zeal!=null){return PaladinZeal.Resolve(from,now);}
        var ability = session.PendingAbility;
        if (ability == CombatAbility.None)
        {
            return false;
        }
        if(!ShapeRestrictions.Allowed(from,ability)||ability==CombatAbility.BasicAttack&&!ReferenceEquals(session.AttackForm,DruidPlayerForms.State(from)))
        {session.PendingAbility=CombatAbility.None;session.AttackForm=null;return false;}
        if (ability == CombatAbility.Teleport) { return Teleportation.Resolve(from, now); }
        if (!Enabled(from) || !from.Alive || !AlliedOverlap.Allowed(from) || from.Frozen || from.Paralyzed || from.Spell != null
            || !Available(from, ability) || from.Location != session.ActionOrigin || from.Map != session.ActionMap
            || (CombatTiming.Equipped(from)?.Serial ?? Serial.Zero) != session.PendingWeapon)
        {
            session.PendingAbility = CombatAbility.None;
            if(DruidFormCasting.Handles(ability)){session.Transform=null;}
            if(ability==CombatAbility.BladeShield){BladeShields.Cancel(from);}
            if(ability==CombatAbility.FindItem){session.FindItemTarget=Serial.Zero;}
            if(ability==CombatAbility.FindPotion){session.FindPotionTarget=Serial.Zero;}
            if(ability==CombatAbility.Conversion){session.Conversion=null;}
            if(BarbarianMelee.Handles(ability)){session.BarbarianAttack=null;}
            if(AmazonMelee.Handles(ability)){session.AmazonAttack=null;}
            if(TigerStrikes.Handles(ability)){session.TigerAttack=null;}
            if(DruidChargedAttacks.Handles(ability)){session.DruidChargeAttack=null;}
            if(ability==CombatAbility.FireClaws){session.FireClawAttack=null;}
            if(ability==CombatAbility.Hunger){session.HungerAttack=null;}
            if(ability==CombatAbility.Rabies){session.RabiesAttack=null;}
            if(ability==CombatAbility.ShockWave){session.ShockWaveAttack=null;}
            if(ability==CombatAbility.Concentrate){BarbarianConcentrate.Cancel(from);}
            if(ability==CombatAbility.Berserk){BarbarianBerserk.CancelMode(from);}
            if(ability==CombatAbility.Zeal){session.ZealPaid=false;session.ZealHits=0;}
            return false;
        }
        if (now - session.EffectAt < 0)
        {
            return false;
        }
        session.PendingAbility = CombatAbility.None;
        if (ability == CombatAbility.RaiseSkeleton)
        {
            return SkeletonPets.Raise(from, World.FindItem(session.PendingTarget) as Corpse);
        }
        if (AmazonCombat.Handles(ability)) { return AmazonCombat.Release(from,ability,now); }
        if (BarbarianCombat.Handles(ability)) { return BarbarianCombat.Release(from,ability,session.PendingTarget,now); }
        if (AssassinCombat.Handles(ability)) { return AssassinCombat.Release(from,ability,session.PendingTarget,now); }
        if (DruidCombat.Handles(ability)) { return DruidCombat.Release(from,ability,now); }
        if (SorceressCombat.Handles(ability))
        {
            var aimed = World.FindEntity(session.PendingTarget);
            return SorceressCombat.Release(from, ability, session.PendingTarget, aimed?.Location ?? session.PendingPoint);
        }
        if (PaladinCombat.Handles(ability))
        {
            var aimed=World.FindEntity(session.PendingTarget);
            return PaladinCombat.Release(from,ability,session.PendingTarget,aimed?.Location ?? session.PendingPoint,now);
        }
        if (NecromancerCombat.Handles(ability))
        {
            var aimed = World.FindEntity(session.PendingTarget);
            return NecromancerCombat.Release(from, ability, session.PendingTarget, aimed?.Location ?? session.PendingPoint);
        }
        bool formMelee=ability==CombatAbility.BasicAttack&&DruidFormAttacks.Active(from);
        using var formWeapon=formMelee&&CombatTiming.Equipped(from) is{} formItem?WeaponHands.UseMelee(from,formItem):null;
        var target = World.FindMobile(session.PendingTarget);
        if (session.PendingTarget != Serial.Zero && (!IsHostile(from, target) || !from.InRange(target, CombatProtocol.SightRange)))
        {
            Stop(from);
            return false;
        }
        var point = target?.Location ?? session.PendingPoint;
        ThrowRelease? throwing=null;
        if (ability == CombatAbility.Teeth)
        {
            if (from.Mana < 1)
            {
                return false;
            }
            from.Mana--;
            var endpoint = TeethEndpoint(from, point);
            target = FindRayTarget(from, endpoint);
            IEntity destination = target ?? (IEntity)new Entity(Serial.Zero, endpoint, from.Map);
            from.MovingEffect(destination, 0x36E4, 5, 0, false, false);
            from.PlaySound(0x1E5);
        }
        else
        {
            from.PlaySound(0x238);
            bool emptyThrow=target==null && ability==CombatAbility.BasicAttack && BarbarianThrowing.Supported(from)
                && point.X>=0 && point.Y>=0 && point.X<from.Map.Width && point.Y<from.Map.Height
                && from.InRange(point,CombatTiming.Range(from,ability)) && from.InLOS(new Point3D(point.X,point.Y,point.Z+14)) && Math.Abs(from.Z-point.Z)<16;
            if (!MeleeReach(from, target) && !emptyThrow)
            {
                return true;
            }
            if(ability==CombatAbility.BasicAttack && BarbarianThrowing.Supported(from))
            {throwing=BarbarianThrowing.Release(from,TimedEffects.Now);if(throwing==null){return false;}}
            else if (!formMelee&&!NamedAmmunition.Consume(CombatTiming.Equipped(from), TimedEffects.Now)) { return false; }
            if (CombatTiming.Weapon(CombatTiming.Equipped(from)).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw)
            {
                from.MovingEffect(target ?? (IEntity)new Entity(Serial.Zero,point,from.Map), 0xF42, 5, 0, false, false);
            }
        }
        int pierces = throwing?.Pierces ?? (ability == CombatAbility.BasicAttack ? NamedProjectiles.Budget(from) : 0);
        bool weaponHit = false;
        if (target != null)
        {
            using var attemptedMelee=ability==CombatAbility.BasicAttack?PaladinThorns.Attempt(from,target,
                CombatTiming.Weapon(CombatTiming.Equipped(from)).Family is AttackFamily.Bow or AttackFamily.Crossbow or AttackFamily.Throw):null;
            from.DoHarmful(target, true);
            if (ability == CombatAbility.Teeth)
            {
                int before = target.Hits;
                int resistance = target is BaseCreature creature && CreatureProfiles.For(creature) is { ApplyStats: true } creatureProfile
                    ? creatureProfile.Resistances.Magic ?? 0 : 0;
                target.Damage((int)CombatPotions.TeethDamage(from, target, 4 + TeethRank(from) * 2, resistance), from);
                if (target is BaseCreature victim)
                {
                    CreatureHitRecovery.Apply(victim, from, Math.Max(0, before - target.Hits));
                }
            }
            else if (ability==CombatAbility.BasicAttack && AssassinCharges.Any(from) && PaladinVengeance.WeaponAllowed(from))
            {
                weaponHit=TigerStrikes.BasicStrike(from,target);
            }
            else if ((formMelee?ClassStats.CheckHit(from,target,0,DruidFormAttacks.HitRoll,projectile:false):ClassStats.CheckHit(from,target)) == true)
            {
                bool blocked=formMelee&&target is BaseCreature defender&&SourceBlocking.TryBlock(defender,true,out bool shielded)&&shielded;
                if(!blocked){GearEffects.Strike(from, target, Utility.RandomDouble(), wearWeapon: pierces == 0,projectileCritical:throwing?.Critical); weaponHit = true;}
            }
            else
            {
                from.PlaySound(0x238);
            }
            CombatDamage.Finish(target, from);
            if (pierces > 0)
            {
                var releasedWeapon = CombatTiming.Equipped(from);
                NamedProjectiles.Continue(from, target, point, pierces,projectileCritical:throwing?.Critical);
                if (weaponHit && releasedWeapon != null && NamedAmmunition.Maximum(releasedWeapon) == 0)
                { releasedWeapon.WearOnHit(from, target); }
            }
            if (session.PendingTarget == target.Serial && (!target.Alive || target.Deleted))
            {
                Stop(from);
            }
        }
        return true;
    }
    public static Point3D TeethEndpoint(PlayerMobile from, Point3D aim)
    {
        var dx = aim.X - from.X;
        var dy = aim.Y - from.Y;
        var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0)
        {
            return from.Location;
        }
        var result = from.Location;
        for (var step = 1; step <= CombatProtocol.TeethRange; step++)
        {
            var next = new Point3D(from.X + (int)Math.Round((double)dx * step / distance),
                from.Y + (int)Math.Round((double)dy * step / distance), from.Z);
            if (next.X < 0 || next.Y < 0 || next.X >= from.Map.Width || next.Y >= from.Map.Height || !from.InLOS(new Point3D(next.X, next.Y, next.Z + 14)))
            {
                break;
            }
            result = next;
        }
        return result;
    }
    internal static Mobile FindRayTarget(PlayerMobile from, Point3D point,int range=CombatProtocol.TeethRange)
    {
        Mobile result = null;
        var best = double.MaxValue;
        foreach (var candidate in from.Map.GetMobilesInRange<Mobile>(from.Location, range))
        {
            if (!IsHostile(from, candidate) || !from.InLOS(candidate)
                || !CombatHitboxes.Intersect(from.Location, point, candidate.Location, out var entry))
            {
                continue;
            }
            if (entry < best || entry == best && (result == null || candidate.Serial.Value < result.Serial.Value))
            {
                best = entry;
                result = candidate;
            }
        }
        return result;
    }
    private static void Receive(NetState state, SpanReader reader)
    {
        if (state.Mobile is not PlayerMobile from || !Enabled(from) || reader.Remaining < 6)
        {
            return;
        }
        var version = reader.ReadByte();
        var action = (CombatRequest)reader.ReadByte();
        var sequence = reader.ReadUInt32();
        var session = For(from);
        if (version != CombatProtocol.Version || unchecked((int)(sequence - session.Sequence)) <= 0
            || reader.Remaining != (action == CombatRequest.Attack ? 4 : action is CombatRequest.Cast or CombatRequest.HoldCast or CombatRequest.HoldAttack ? 10 : action == CombatRequest.MovementMode ? 1 : 0)
            || action is < CombatRequest.Hello or > CombatRequest.ReleaseAttack)
        {
            return;
        }
        session.Sequence = sequence;
        switch (action)
        {
            case CombatRequest.MovementMode:
                var mode = reader.ReadByte();
                if (mode > 1)
                {
                    return;
                }
                session.Running = mode != 0;
                break;
            case CombatRequest.Hello:
                session.Handshake = true;
                Stop(from);
                ClassEquipment.Prepare(from);
                from.Warmode = false;
                from.Combatant = null;
                break;
            case CombatRequest.Attack:
                Begin(from, World.FindMobile((Serial)reader.ReadUInt32()));
                break;
            case CombatRequest.Stop:
                Stop(from);
                break;
            case CombatRequest.ReleaseCast:
            case CombatRequest.ReleaseAttack:
                if (session.HeldAttack == (action == CombatRequest.ReleaseAttack))
                {
                    ReleaseCast(from);
                }
                break;
            case CombatRequest.Cast:
            case CombatRequest.HoldCast:
            case CombatRequest.HoldAttack:
                var ability = (CombatAbility)reader.ReadByte();
                var target = (Serial)reader.ReadUInt32();
                var point = new Point3D(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadSByte());
                if(ClassAbilities.ItemTarget(ability))
                {
                    if(action==CombatRequest.Cast||ability==(action==CombatRequest.HoldAttack?session.Left:session.Right))
                    {NecromancerCombat.Begin(from,ability,target,point,Core.TickCount);}
                    break;
                }
                if (CorpseCast(ability))
                {
                    if (action == CombatRequest.Cast || ability == (action == CombatRequest.HoldAttack ? session.Left : session.Right))
                    { BeginCorpse(from, ability, target, point, Core.TickCount); }
                    break;
                }
                if (ability == CombatAbility.Teleport)
                {
                    if (action == CombatRequest.Cast) { Teleportation.Begin(from, point, Core.TickCount); }
                    else if (ability == (action == CombatRequest.HoldAttack ? session.Left : session.Right))
                    { ContinueHold(from, ability, null, point, Core.TickCount, action == CombatRequest.HoldAttack); }
                    break;
                }
                if (ability == CombatAbility.RaiseSkeleton
                    && (action == CombatRequest.Cast || ability == (action == CombatRequest.HoldAttack ? session.Left : session.Right)))
                {
                    if (action == CombatRequest.HoldAttack && World.FindItem(target) is Corpse { Owner: var owner } own && owner == from)
                    {
                        ClassDeath.Recover(from, own);
                    }
                    else
                    {
                        CastSummon(from, target, Core.TickCount);
                    }
                    break;
                }
                if ((action == CombatRequest.Cast || ability == (action == CombatRequest.HoldAttack ? session.Left : session.Right))
                    && (target == Serial.Zero || World.FindMobile(target) != null))
                {
                    if (action == CombatRequest.HoldAttack)
                    {
                        ContinueAttack(from, ability, World.FindMobile(target), point, Core.TickCount);
                    }
                    else if (action == CombatRequest.HoldCast)
                    {
                        ContinueCast(from, ability, World.FindMobile(target), point, Core.TickCount);
                    }
                    else
                    {
                        Stop(from);
                        Perform(from, ability, World.FindMobile(target), point, Core.TickCount);
                    }
                }
                break;
        }
        if (action is CombatRequest.Hello or CombatRequest.MovementMode || Core.TickCount - session.LastState >= CombatProtocol.StateIntervalMs)
        {
            session.LastState = Core.TickCount;
            SendState(from, session);
        }
    }
    private static void Tick()
    {
        _expired.Clear();
        foreach (var (serial, session) in _sessions)
        {
            if (World.FindMobile(serial) is not PlayerMobile { Deleted: false, NetState: { } } from)
            {
                _expired.Add(serial);
                continue;
            }
            UpdateCombat(from, Core.TickCount);
            if (session.Handshake && Core.TickCount - session.LastState >= CombatProtocol.StateIntervalMs)
            {
                session.LastState = Core.TickCount;
                SendState(from, session);
            }
        }
        foreach (var serial in _expired)
        {
            _sessions.Remove(serial);
        }
    }
    internal static void SendState(PlayerMobile from, Session session)
    {
        if (!session.Handshake || from.NetState?.Running != true)
        {
            return;
        }
        Span<uint> hostiles = stackalloc uint[CombatProtocol.MaxHostiles];
        var count = 0;
        Span<uint> allies = stackalloc uint[CombatProtocol.MaxAllies]; int allyCount = 0;
        foreach (var candidate in from.Map.GetMobilesInRange<Mobile>(from.Location, CombatProtocol.SightRange))
        {
            if (allyCount < allies.Length && AlliedOverlap.PassThrough(from, candidate)) { allies[allyCount++] = candidate.Serial.Value; }
            if (count < hostiles.Length && IsHostile(from, candidate))
            {
                hostiles[count++] = candidate.Serial.Value;
            }
        }
        var weapon = CombatTiming.Equipped(from);
        var signature = HashCode.Combine(weapon?.Serial, CombatTiming.Ias(from), CombatTiming.Fcr(from),PaladinOffense.AttackRate(from),DruidPlayerForms.State(from)?.Form,DruidAttackCharges.State(from,CombatAbility.Maul));
        if (!session.TooltipKnown || signature != session.TooltipSignature)
        {
            session.TooltipKnown = true;
            session.TooltipSignature = signature;
            weapon?.InvalidateProperties();
        }
        var length = 33 + (count + allyCount) * 4;
        Span<byte> data = stackalloc byte[length];
        var writer = new SpanWriter(data);
        writer.Write((byte)0xBF);
        writer.Write((ushort)length);
        writer.Write(CombatProtocol.SubCommand);
        writer.Write(CombatProtocol.Version);
        writer.Write(session.Sequence);
        writer.Write((byte)session.Left);
        writer.Write((byte)session.Right);
        writer.Write((byte)Math.Min(255, TeethRank(from)));
        writer.Write(session.Target.Value);
        writer.Write((byte)count);
        writer.Write((byte)(session.Running ? 1 : 0));
        writer.Write((byte)CombatTiming.Range(from, session.Left));
        writer.Write((ushort)ActionInterval(from, session.Left));
        writer.Write((ushort)ActionInterval(from, session.Right));
        writer.Write(session.Animation);
        writer.Write((ushort)session.Timing.DurationMs);
        writer.Write((ushort)Math.Clamp(session.NextAction - Core.TickCount, 0, session.Timing.DurationMs));
        writer.Write((ushort)Math.Clamp(session.RecoveryUntil - Core.TickCount, 0, 2000));
        writer.Write((byte)(AlliedOverlap.Blocked(from) ? 1 : 0));
        for (var i = 0; i < count; i++)
        {
            writer.Write(hostiles[i]);
        }
        writer.Write((byte)allyCount);
        for (int i = 0; i < allyCount; i++) { writer.Write(allies[i]); }
        from.NetState.Send(data);
    }
}
