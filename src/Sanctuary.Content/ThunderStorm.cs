// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ThunderStormState(DateTime UntilUtc, DateTime NextStrikeUtc, int IntervalFrames,
    int RadiusSubtiles, int Rank, uint LastTarget = 0);

public static class ThunderStorm
{
    private static readonly Dictionary<Serial, TimerExecutionToken> _timers = new();
    internal static bool Valid(ThunderStormState state)=>state!=null&&state.IntervalFrames is >=25 and <=110&&state.RadiusSubtiles==17&&state.Rank is >=1 and <=255
        &&state.UntilUtc.Kind==DateTimeKind.Utc&&state.NextStrikeUtc.Kind==DateTimeKind.Utc&&(state.LastTarget==0||((Serial)state.LastTarget).IsMobile);
    public static ThunderStormState Active(Mobile owner) => SorceressEffects.Profiles.Find(owner)?.Storm is { } storm
        && storm.UntilUtc > TimedEffects.Now ? storm : null;
    internal static ThunderStormState Prepare(SkillValues values,DateTime now)
    {
        var row=SkillMechanics.For(CombatAbility.ThunderStorm);int M(string name)=>NecromancerSummons.Source("thunderstorm1",name);
        if(values.Ability!=CombatAbility.ThunderStorm||values.Rank is <1 or >255||now.Kind!=DateTimeKind.Utc||row.SourceId!=57||row.Number("srvstfunc")!=13||row.Number("srvdofunc")!=29
            ||row.Text("srvmissilea")!="thunderstorm1"||row.Text("EType")!="ltng"||row.Number("SrcDam")!=0||values.Mana256!=4864||values.Param(7)!=17
            ||M("pSrvDoFunc")!=3||M("Range")!=9||M("CanSlow")!=0||M("Size")!=3||M("ReturnFire")!=1||M("GetHit")!=1||M("ResultFlags")!=4)
        {throw new NotSupportedException("Unmapped Thunder Storm route");}
        int frames=checked((int)values.Eval("perdelay"));
        var storm=new ThunderStormState(now.AddMilliseconds(values.Eval("auralencalc")*40),now.AddMilliseconds(frames*40),frames,values.Param(7),values.Rank);
        if(!Valid(storm)){throw new NotSupportedException("Unsupported Thunder Storm state");}return storm;
    }
    private static void Commit(PlayerMobile owner,ThunderStormState storm)
    {
        SorceressEffects.Profiles.Store(owner,(SorceressEffects.Profiles.Find(owner)??new()) with{Storm=storm});Schedule(owner.Serial,storm);
        owner.FixedEffect(0x375A,10,15,0x482,0); // Identified storm aura placeholder.
    }
    public static void Cast(PlayerMobile owner,SkillValues values)=>Commit(owner,Prepare(values,TimedEffects.Now));
    internal static bool Release(PlayerMobile owner,SkillValues values)
    {
        var storm=Prepare(values,TimedEffects.Now);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Commit(owner,storm);owner.PlaySound(0x1E5);return true;
    }
    internal static void Cancel(Serial serial)
    { if (_timers.Remove(serial, out var timer)) { timer.Cancel(); } }
    internal static void Schedule(Serial serial, ThunderStormState storm)
    {
        if(!Valid(storm)){throw new ArgumentException("Invalid Thunder Storm schedule");}
        Cancel(serial);
        var next = storm.NextStrikeUtc < storm.UntilUtc ? storm.NextStrikeUtc : storm.UntilUtc;
        Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(1, (next - TimedEffects.Now).TotalMilliseconds)),
            () => Tick(serial), out var timer);
        _timers.Add(serial, timer);
    }
    private static void Tick(Serial serial)=>Tick(serial,TimedEffects.Now);
    internal static void Tick(Serial serial,DateTime now,bool? actions=null)
    {
        if(now.Kind!=DateTimeKind.Utc){throw new ArgumentException("Storm clock must be UTC");}
        Cancel(serial);
        if (World.FindMobile(serial) is not PlayerMobile owner || SorceressEffects.Profiles.Find(owner) is not { Storm: { } storm } state)
        { return; }
        if (storm.UntilUtc <= now || !ClassCreation.Profiles.TryGet(serial, out var profile) || profile.Class != DiabloClass.Sorceress
            || SkillMechanics.Rank(owner, CombatAbility.ThunderStorm) == 0)
        { SorceressEffects.Profiles.Store(owner, state with { Storm = null }); return; }
        if (storm.NextStrikeUtc > now) { Schedule(serial, storm); return; }
        // Explicit owner policy: disconnected/internalized casters age without
        // attacks or replay. Keep target memory while action is suspended.
        if(owner.Map==null||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {
            storm=storm with{NextStrikeUtc=now.AddMilliseconds(storm.IntervalFrames*40)};
            SorceressEffects.Profiles.Store(owner,state with{Storm=storm});Schedule(serial,storm);return;
        }
        Mobile selected = null;
        if (owner.Map != null && owner.Map != Map.Internal)
        {
            // The source picks the next greater unit ID, wrapping to the smallest; a sole target may repeat.
            var targets = SkillCombat.Targets(owner, owner.Location, SkillGeometry.Tiles(storm.RadiusSubtiles));
            foreach (var candidate in targets)
            {
                selected ??= candidate;
                if (candidate.Serial.Value > storm.LastTarget) { selected = candidate; break; }
            }
        }
        storm = storm with { NextStrikeUtc = now.AddMilliseconds(storm.IntervalFrames * 40), LastTarget = selected?.Serial.Value ?? 0 };
        SorceressEffects.Profiles.Store(owner, state with { Storm = storm });
        Schedule(serial, storm); // Missed/offline periods never replay a burst of strikes.
        if (selected == null || SpellHelper.IsTown(owner.Location, owner) || SpellHelper.IsTown(selected.Location, owner)) { return; }
        var values = SkillMechanics.Values(owner, CombatAbility.ThunderStorm, storm.Rank);
        // do29 creates a fresh instantaneous missile at retained rank. Reuse
        // the supported live creation modifiers/slow payload and typed impact.
        var modifiers=DruidFirestorm.Modifiers(owner);var slow=AmazonSlowMissiles.Capture(owner,false);
        var ends=DruidFirestorm.Endpoints(values,modifiers.LightningMastery,slow);
        ulong seed=SourceRandom.InitLow(SorceressSpellMissiles.SeedLow());int raw=SorceressSpellMissiles.Roll(ends.Min,ends.Max,seed,out _);
        SorceressSpellMissiles.ImpactPrepared(owner,selected,"thunderstorm1","ltng",raw,modifiers,now);
        selected.BoltEffect(0x482); // Placeholder lightning strike.
    }
}
