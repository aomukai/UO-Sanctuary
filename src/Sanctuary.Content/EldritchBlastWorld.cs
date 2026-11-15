// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record EldritchBlastState(uint Owner,EldritchBlastNumbers Numbers,int MagicMastery,
    FirestormModifiers Modifiers,ulong Seed,DateTime StartedUtc,DateTime NextUtc,DateTime UntilUtc);
internal readonly record struct EldritchBlastPulse(bool Due,int Refreshed,int Damaged,int Life256,int Mana256,string Fault=null);
public sealed class EldritchBlastProfiles:GenericPersistence
{
    public EldritchBlastProfiles():base("SanctuaryEldritchBlast",252){}
    public override void Serialize(IGenericWriter writer)
    {writer.Write(0);writer.Write(JsonSerializer.Serialize(EldritchBlastWorld.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Eldritch Blast version");}EldritchBlastWorld.Restore(JsonSerializer.Deserialize<EldritchBlastState[]>(reader.ReadString()));}
}

// Prepared complete world candidate. No learned route, automatic tick or
// input admission until the documented Sanctuary rule is approved. Table
// identity is pinned; the unrecovered do165 ordering is not asserted.
internal static class EldritchBlastWorld
{
    internal static bool Approved=>false;
    private static ImmutableDictionary<uint,EldritchBlastState> _states=ImmutableDictionary<uint,EldritchBlastState>.Empty;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    public static void Configure()=>_=new EldritchBlastProfiles();
    private static bool Owner(PlayerMobile p)=>p is{Alive:true,Deleted:false}
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Warlock;
    internal static bool Castable(PlayerMobile p)=>Owner(p)&&SkillMechanics.Rank(p,CombatAbility.EldritchBlast)>0
        &&p.Map!=null&&p.Map!=Map.Internal&&!MinionSafeAreas.Contains(p);
    internal static EldritchBlastState State(Mobile p)=>p!=null?_states.GetValueOrDefault(p.Serial.Value):null;
    internal static void Clear(Mobile p){if(p!=null){_states=_states.Remove(p.Serial.Value);}}
    private static bool Percent(int n)=>n is >=-100 and <=1000000;
    internal static void Validate(EldritchBlastState s)
    {
        if(s==null||!((Serial)s.Owner).IsMobile||s.Numbers is not{} n||n.Rank is <1 or >255||s.Seed==0
            ||!Percent(s.MagicMastery)||s.Modifiers is not{} m||m.Potion is <0 or >1000000
            ||!Percent(m.FireMastery)||!Percent(m.ColdMastery)||!Percent(m.LightningMastery)||!Percent(m.PoisonMastery)
            ||s.StartedUtc.Kind!=DateTimeKind.Utc||s.StartedUtc<=DateTime.UnixEpoch||s.NextUtc.Kind!=DateTimeKind.Utc||s.UntilUtc.Kind!=DateTimeKind.Utc
            ||n.DurationFrames is <1000 or >2000||n.DurationFrames%50!=0||s.UntilUtc!=s.StartedUtc.AddMilliseconds(n.DurationFrames*40L)
            ||s.NextUtc<=s.StartedUtc||s.NextUtc>s.UntilUtc.AddMilliseconds(1200)
            ||(s.NextUtc.Ticks-s.StartedUtc.Ticks)%TimeSpan.FromMilliseconds(1200).Ticks!=0)
        {throw new InvalidDataException("Invalid Eldritch Blast state/clock");}
        int ward=(n.DurationFrames-1000)/50;
        bool supplied=Enumerable.Range(0,41).Any(hard=>n==EldritchBlastSuppliers.Capture(new SkillValues(SkillMechanics.For(CombatAbility.EldritchBlast),
            k=>k=="Eldritch Blast"?n.Rank:0,k=>k=="Blade Warp"?Math.Min(hard,20):k=="Hex Purge"?Math.Max(0,hard-20):k=="Psychic Ward"?ward:0,99)));
        if(!supplied){throw new InvalidDataException("Invalid Eldritch Blast captured suppliers");}
    }
    internal static EldritchBlastState Prepare(PlayerMobile owner,DateTime at)
    {
        if(!Castable(owner)||at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch){throw new NotSupportedException("Unsupported Eldritch Blast owner/world/clock");}
        // Explicit candidate capture, including mastery. Do not weaken the
        // older source-only supplier's refusal of unverified mastery timing.
        var n=EldritchBlastSuppliers.Capture(SkillMechanics.Values(owner,CombatAbility.EldritchBlast));
        var s=new EldritchBlastState(owner.Serial.Value,n,NamedElements.MasteryPercent(owner,"mag"),DruidFirestorm.Modifiers(owner),
            SourceRandom.InitLow(SeedLow()),at,at.AddMilliseconds(n.PeriodFrames*40L),at.AddMilliseconds(n.DurationFrames*40L));
        Validate(s);return s;
    }
    internal static EldritchBlastState ReleasePrepared(PlayerMobile owner,DateTime at)
    {
        if(!Castable(owner)){return null;}
        var values=SkillMechanics.Values(owner,CombatAbility.EldritchBlast);
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return null;}
        var s=Prepare(owner,at);if(!NecromancerEffects.Pay(owner,s.Numbers.Mana256)){return null;}
        _states=_states.SetItem(s.Owner,s);
        owner.FixedEffect(0x375A,10,15,0x48D,0);owner.PlaySound(0x1E5); // Identified cast/aura/audio placeholders.
        return s;
    }
    internal static EldritchBlastState[] Snapshot()=>_states.Values.OrderBy(s=>s.Owner).ToArray();
    internal static void Restore(EldritchBlastState[] rows)
    {
        if(rows==null||rows.Length>1000000){throw new InvalidDataException("Missing Eldritch Blast snapshot");}
        var next=ImmutableDictionary<uint,EldritchBlastState>.Empty;
        foreach(var s in rows){Validate(s);if(next.ContainsKey(s.Owner)){throw new InvalidDataException("Duplicate Eldritch Blast owner");}next=next.Add(s.Owner,s);}
        _states=next;
    }
    private static bool Recipient(PlayerMobile owner,BaseCreature target,int radius)
        =>target is{Alive:true,Deleted:false,Blessed:false}&&WeaponHexRefresh.HasHex(target)
            &&SourceDamageAreas.Recipient(owner,target,owner.Map.MapID,owner.X*2,owner.Y*2,owner.Z,radius,true,owner.Location);
    // One pulse at most per tick. Skip missed/offline/safe-area periods without
    // extra rolls, refreshes, recovery, replay or extending the saved lifetime.
    internal static EldritchBlastPulse TickPrepared(PlayerMobile owner,DateTime at,bool? actions=null)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Eldritch Blast clock must be UTC");}
        if(owner==null||State(owner)is not{} s){return default;}
        if(!Owner(owner)||SkillMechanics.Rank(owner,CombatAbility.EldritchBlast)==0||at>=s.UntilUtc)
        {Clear(owner);return default;}
        if(at<s.NextUtc){return default;}
        long period=TimeSpan.FromMilliseconds(s.Numbers.PeriodFrames*40L).Ticks;
        long skipped=(at.Ticks-s.NextUtc.Ticks)/period+1;
        var next=s with{NextUtc=s.NextUtc.AddTicks(skipped*period)};
        _states=_states.SetItem(s.Owner,next); // Claim before any nested callbacks.
        if(!Castable(owner)||!(actions??owner.NetState is{Running:true})){return new(true,0,0,0,0);}
        var map=owner.Map;var center=owner.Location;
        var targets=SkillCombat.NearbySnapshot<BaseCreature>(map,center,(int)Math.Ceiling(SkillGeometry.Tiles(s.Numbers.RawAuraRange)))
            .Where(t=>Recipient(owner,t,s.Numbers.RawAuraRange)).OrderBy(t=>t.Serial.Value).ToArray();
        try
        {
            foreach(var target in targets)
            {
                TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);
                WeaponHexRefresh.RequirePrepared(target);
            }
        }
        catch(NotSupportedException ex){return new(true,0,0,0,0,ex.Message);}
        if(targets.Length==0){return new(true,0,0,0,0);}
        ulong seed=s.Seed;
        int raw=SourceSpellPacketRoll.Range(ref seed,HexPurgeReactionProposal.Enhanced(s.Numbers.MagicMinimum256,s.MagicMastery),
            HexPurgeReactionProposal.Enhanced(s.Numbers.MagicMaximum256,s.MagicMastery));
        next=next with{Seed=seed};_states=_states.SetItem(s.Owner,next);
        var packet=DruidMoltenBoulder.Potion(s.Modifiers,new(0,0,0,0,0,Magic:raw/256.0));
        int refreshed=0,damaged=0;long life=0,mana=0;
        foreach(var target in targets)
        {
            if(!Castable(owner)||State(owner)!=next||owner.Map!=map||owner.Location!=center){break;}
            if(!Recipient(owner,target,s.Numbers.RawAuraRange)){continue;}
            // Refresh before mitigation; immunity cannot prevent maintenance.
            if(!WeaponHexRefresh.RefreshPrepared(target,at)){continue;}refreshed++;
            owner.DoHarmful(target,true);var parts=SkillCombat.MitigatePrepared(owner,target,packet);
            int before=SkillHealth.Balance256(target);
            SkillHealth.TypedDamage(owner,target,NamedElements.To256(parts.Total),DirectDamageParts.From(parts),SkillDamageKind.Periodic);
            int after=target is{Alive:true,Deleted:false}?SkillHealth.Balance256(target):0;
            int loss=Math.Max(0,before-after);if(loss>0){damaged++;}
            // Recovery uses only the magic component of actual FP256 life loss,
            // never added potion elements, overkill, item leech or victim mana.
            int magic=Math.Min(loss,NamedElements.To256(parts.Magic));
            int divisor=MonsterVariants.Difficulty(target)switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};
            var recovery=WeaponLeech.Calculate(magic,before,0,s.Numbers.LifeStealPercent,s.Numbers.ManaStealPercent,GemCombat.Drain(target),divisor,divisor);
            if(Owner(owner))
            {
                int hp=SkillHealth.Balance256(owner),mp=NecromancerEffects.ManaBalance256(owner);
                SkillHealth.Heal(owner,recovery.Life256);NecromancerEffects.AddMana(owner,recovery.Mana256);
                life+=Math.Max(0,SkillHealth.Balance256(owner)-hp);mana+=Math.Max(0,NecromancerEffects.ManaBalance256(owner)-mp);
            }
            if(target is{Alive:true,Deleted:false}){target.FixedEffect(0x375A,5,5,0x48D,0);} // Identified pulse contact placeholder.
        }
        Effects.SendLocationEffect(center,map,0x37CC,8,5,0x48D,0); // Cosmetic nova; no second hit actor.
        return new(true,refreshed,damaged,(int)Math.Min(life,int.MaxValue),(int)Math.Min(mana,int.MaxValue));
    }
}
