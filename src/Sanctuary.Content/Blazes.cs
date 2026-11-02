// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class Blazes
{
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
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
    internal static PreparedMonsterDamagePacket Roll(int minimum,int maximum,ref ulong seed)=>SourceFireFields.Roll(minimum,maximum,ref seed);
    internal static bool Supported(PlayerMobile owner,Mobile target,out string reason)=>SourceFireFields.Supported(owner,target,out reason);
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at)
        =>SourceFireFields.Deliver(owner,target,packet,mods,at);

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
        if(patch.FireWallModifiers!=null&&_loadedWallFields.Remove(patch.Id)||owner.Map==Map.Internal||!(actions??owner.NetState is{Running:true}))
        {if(patch.UntilUtc<=at){_patches.Remove(patch.Id);}else{_patches[patch.Id]=patch with{NextUtc=at.AddMilliseconds(40)};}return;}
        var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
        // Field location decides town/terrain collision. Entering town alone
        // does not remove an existing nontown field in the same facet.
        if(SpellHelper.IsTown(point,owner)||!owner.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);return;}
        ulong seed=patch.Seed;
        _patches[patch.Id]=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};
        SourceFireFields.Burn(owner,patch,patch.NextUtc,ref seed);
        if(_patches.TryGetValue(patch.Id,out var current)){_patches[patch.Id]=current with{Seed=seed};}
        if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0){Effects.SendLocationEffect(point,owner.Map,0x398C,5,10,0x489,0);} // Identified ground-fire placeholder.
    }
}
