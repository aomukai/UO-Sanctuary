// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class Blazes
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(Blazes));
    private static readonly HashSet<string> Warned=new();
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    private static void Gap(string reason){if(Warned.Add(reason)){Log.Warning("Blaze unsupported context: {Reason}",reason);}}
    internal static BlazeState Prepare(PlayerMobile owner,SkillValues values,DateTime at)
    {
        var row=SkillMechanics.For(CombatAbility.Blaze);
        if(values.Ability!=CombatAbility.Blaze||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc
            ||owner is not{Alive:true,Deleted:false}||owner.Map==null||owner.Map==Map.Internal
            ||row.SourceId!=46||row.Number("srvdofunc")!=23||row.Text("aurastate")!="blaze"||row.Text("passivestat1")!="velocitypercent"
            ||row.Left||!row.Right||values.Eval("auralencalc")<=0||values.Eval("passivecalc1")<0)
        {throw new NotSupportedException("Unsupported pinned Blaze cast");}
        return new(at.AddMilliseconds(values.Eval("auralencalc")*40),values.Rank,(int)values.Eval("passivecalc1"),owner.Map.MapID,owner.X,owner.Y,owner.Z);
    }
    private static bool CanApply(PlayerMobile owner,BlazeState prepared)=>FireGround.Blaze(owner) is not{} old||old.Rank<=prepared.Rank;
    internal static bool Apply(PlayerMobile owner,BlazeState state)
    {
        if(!CanApply(owner,state)){return false;}
        SorceressEffects.Profiles.Store(owner,(SorceressEffects.Profiles.Find(owner)??new()) with{Blaze=state});
        owner.FixedEffect(0x375A,10,10,0x489,0);return true; // Identified Blaze aura placeholder.
    }
    internal static bool Release(PlayerMobile owner,SkillValues values)
    {
        var prepared=Prepare(owner,values,TimedEffects.Now);
        if(!CanApply(owner,prepared)||!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        if(!Apply(owner,prepared)){throw new InvalidOperationException("Blaze state changed during synchronous release");}
        owner.PlaySound(0x1E5);return true; // Identified fire-cast placeholder.
    }
    internal static void Moved(PlayerMobile owner)
    {
        if(FireGround.Blaze(owner) is not{} blaze||owner.Map==null||owner.Map==Map.Internal){return;}
        bool adjacent=owner.Map.MapID==blaze.Map&&Math.Abs(owner.X-blaze.X)<=1&&Math.Abs(owner.Y-blaze.Y)<=1&&Math.Abs(owner.Z-blaze.Z)<16;
        if(adjacent&&(owner.X!=blaze.X||owner.Y!=blaze.Y)&&!SpellHelper.IsTown(owner.Location,owner))
        {
            // Retain the existing native one-UO-tile/two-source-subtile bridge.
            // Complete outer player movement cadence is explicitly unverified.
            for(int step=1;step<=2;step++)
            {FireGround.AddBlaze(owner,blaze.Rank,blaze.X+(owner.X-blaze.X)*step/2.0,blaze.Y+(owner.Y-blaze.Y)*step/2.0,owner.Z,TimedEffects.Now);}
        }
        SorceressEffects.Profiles.Store(owner,SorceressEffects.Profiles.Find(owner) with{Blaze=blaze with{Map=owner.Map.MapID,X=owner.X,Y=owner.Y,Z=owner.Z}});
    }
    internal static void Route()
    {
        int F(string field)=>NecromancerSummons.Source("blaze",field);
        if(F("pSrvDoFunc")!=5||F("pSrvHitFunc")!=8||F("pSrvDmgFunc")!=3||F("dParam1")!=19||F("Size")!=1
            ||F("DamageRate")!=0||F("GetHit")!=0||F("SoftHit")!=0||F("CollideKill")!=0||F("LastCollide")!=0
            ||F("CollideType")!=3||F("CanSlow")!=1||F("HitClass")!=32)
        {throw new NotSupportedException("Unsupported pinned Blaze field");}
    }
    internal static bool Valid(FirePatch p)=>p.BlazeModifiers==null||p.Ability==CombatAbility.Blaze&&p.NpcCaster==0&&p.SkillRank is >=1 and <=255
        &&p.Size==1&&p.DamageRate==0&&p.BlazeModifiers.Potion>=0&&p.Min256>=0&&p.Max256>=p.Min256
        &&p.X*2==Math.Floor(p.X*2)&&p.Y*2==Math.Floor(p.Y*2)&&p.NextUtc.Kind==DateTimeKind.Utc&&p.UntilUtc.Kind==DateTimeKind.Utc;
    internal static PreparedMonsterDamagePacket Roll(int minimum,int maximum,ref ulong seed)
    {
        int raw=SorceressSpellMissiles.Roll(minimum,maximum,seed,out seed);
        int soft=(SourceRandom.Roll(ref seed)&127)<19?16384:0;
        return new(0,raw,0,0,1|soft,32,true,Fire256:raw);
    }
    internal static bool Supported(PlayerMobile owner,Mobile actor,out string reason)
    {
        reason=null;
        if(actor is not BaseCreature{Alive:true,Deleted:false} target){reason="Player/PvP return needs its own prepared adapter";return false;}
        if(target.Map!=owner.Map||!target.CanBeDamaged()){reason="Field recipient is not in the caster combat world";return false;}
        try
        {
            PreparedMonsterNativeDamage.RequireIncoming(owner,target);
            if(MonsterUninterruptibles.Active(target)||MonsterUninterruptibles.Delayed(target))
            {reason="Uninterruptible/death-delay field completion needs its actor adapter";return false;}
            return true;
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at)
    {
        if(!Supported(owner,target,out string reason)||packet.Physical256!=0||packet.StunFrames!=0||packet.ColdFrames!=0
            ||packet.HitClass!=32||!packet.HitClassActive||packet.HitFlags!=0||packet.ResultFlags is not(1 or 16385)
            ||packet.Total256!=packet.Fire256||packet.Magic256!=0||packet.Lightning256!=0||packet.Cold256!=0||packet.Poison256!=0)
        {throw new NotSupportedException(reason??"Unmapped Blaze field packet");}
        // No block/KB/GETHIT requests: identity/mode/velocity fields are unused,
        // as in Shiver's SOFTHIT path. Do not invent a D2 species for UO actors.
        NativeMonsterReturn.Session(target); // Capture pre-hit life presentation state.
        var services=new InstantSpellNpcDamage(owner,target,new(-1,-1,0,0,false,"Blaze unused return identity"),mods,at,"Blaze",
            ()=>throw new InvalidOperationException("Blaze must not draw defender get-hit RNG"));
        return PreparedMonsterDamage.ReleaseMissile(owner,target,packet,new(NamedCombat.Elite(target),()=>false,()=>0,()=>false),
            _=>throw new InvalidOperationException("Blaze has no timed stun"),services,at);
    }
    internal static void Burn(PlayerMobile owner,FirePatch patch,DateTime at,ref ulong seed)
    {
        if(ImmolationArrows.Recipient(owner,patch) is not{} target){return;}
        if(!Supported(owner,target,out string reason)){Gap(reason);return;}
        var packet=Roll(patch.Min256,patch.Max256,ref seed);Deliver(owner,(BaseCreature)target,packet,patch.BlazeModifiers,at);
    }
}

public static partial class FireGround
{
    // An explicit owner clock also lets disconnected model/reference callers
    // advance the actual controller without advancing unrelated NPC fields.
    internal static void AdvanceBlaze(PlayerMobile owner,DateTime at,bool actions)
    {
        while(true)
        {
            FirePatch next=null;
            foreach(var p in _patches.Values)
            {
                if(p.Owner!=owner.Serial.Value||p.BlazeModifiers==null||p.NextUtc>at){continue;}
                if(next==null||p.NextUtc<next.NextUtc||p.NextUtc==next.NextUtc&&p.Id<next.Id){next=p;}
            }
            if(next==null){return;}TickBlaze(next,at,actions);
        }
    }
    internal static void AddBlaze(PlayerMobile owner,int rank,double x,double y,int z,DateTime at)
    {
        Blazes.Route();var values=SkillMechanics.Values(owner,CombatAbility.Blaze,rank);var mods=DruidFirestorm.Modifiers(owner);
        var ends=DruidFirestorm.Endpoints(values,mods.FireMastery,AmazonSlowMissiles.Capture(owner,true));
        int frames=NecromancerSummons.Formula("blaze","Range",values);
        if(frames!=90+rank*25){throw new NotSupportedException("Unsupported Blaze evaluated lifetime");}
        long id=checked(++_nextId);var patch=new FirePatch(id,owner.Serial.Value,CombatAbility.Blaze,owner.Map.MapID,x,y,z,ends.Min,ends.Max,0,
            at.AddMilliseconds(frames*40L),at.AddMilliseconds(40),1){SkillRank=rank,BlazeModifiers=mods,Seed=SourceRandom.InitLow(Blazes.SeedLow())};
        if(!Blazes.Valid(patch)){throw new NotSupportedException("Invalid prepared Blaze field");}_patches.Add(id,patch);
    }
    private static void TickBlaze(FirePatch patch,DateTime at,bool? actions)
    {
        var owner=Owner(patch.Owner);
        if(owner==null||patch.NextUtc>=patch.UntilUtc||owner.Map!=Map.Internal&&owner.Map?.MapID!=patch.Map){_patches.Remove(patch.Id);return;}
        if(owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {if(patch.UntilUtc<=at){_patches.Remove(patch.Id);}else{_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};}return;}
        var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
        // Field location decides town/terrain collision. Entering town alone
        // does not remove an existing nontown field in the same facet.
        if(SpellHelper.IsTown(point,owner)||!owner.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);return;}
        ulong seed=patch.Seed;
        _patches[patch.Id]=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};
        Blazes.Burn(owner,patch,patch.NextUtc,ref seed);
        if(_patches.TryGetValue(patch.Id,out var current)){_patches[patch.Id]=current with{Seed=seed};}
        if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0){Effects.SendLocationEffect(point,owner.Map,0x398C,5,10,0x489,0);} // Identified ground-fire placeholder.
    }
}
