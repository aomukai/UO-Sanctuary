// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// NPC context in the existing fire controller: no second warning/field store,
// timer, persistence service or player Sorceress proxy.
public static partial class FireGround
{
    internal static Point2D[] MeteorOffsets()=>[new(2,-2),new(-2,-2),new(0,2),new(0,5),new(-3,3),new(0,3),new(3,3),new(-1,2),new(1,1),new(-1,-1),new(2,-1),new(-4,-2),new(-3,-2),new(-1,-3),new(0,-4),new(1,-3),new(3,-3),new(4,-2)];
    private static NecromancerMinion NpcSource(uint caster,uint owner,int map,bool? actions)
    {
        var actor=World.FindMobile((Serial)caster) as NecromancerMinion;var player=World.FindMobile((Serial)owner) as PlayerMobile;
        return NecromancerMageMissiles.Fighting(actor,player,map,actions)?actor:null;
    }
    internal static long CastNpcMeteor(NecromancerMinion actor,Mobile target,DateTime at)
    {
        if(actor is not{Alive:true,Deleted:false,IsMage:true,ControlMaster:PlayerMobile owner}
            ||!IronGolemWorld.Eligible(actor,owner,target)||!Ready(actor,CombatAbility.Meteor,at)){return 0;}
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("NPC Meteor requires UTC");}
        var values=MagePetBuffs.Values(CombatAbility.Meteor,10,actor.Level);var row=new MissileValues("meteorcenter",values);var fire=new MissileValues("meteorfire",values);
        if(row.Number("pSrvDoFunc")!=1||row.Number("pSrvHitFunc")!=14||row.Number("Range")!=60||row.Number("sHitPar1")!=0||row.Number("sHitPar2")!=1
            ||row.Number("AlwaysExplode")!=1||row.Text("HitSubMissile1")!="meteorfire"||fire.Number("pSrvDoFunc")!=5||fire.Number("pSrvDmgFunc")!=3
            ||fire.Number("Size")!=2||fire.Number("DamageRate")!=41||fire.Number("dParam1")!=19||SkillMechanics.For(CombatAbility.Meteor).Number("localdelay")!=30)
        {throw new NotSupportedException("Unmapped NPC Meteor route");}
        long id=checked(++_nextId);
        var meteor=new MeteorDrop(id,owner.Serial.Value,10,actor.Map.MapID,target.X,target.Y,target.Z,
            values.Damage256(false),values.Damage256(true),SkillGeometry.Tiles(values.Eval("aurarangecalc")),at.AddMilliseconds(2400))
            {NpcCaster=actor.Serial.Value,Seed=SourceRandom.InitLow(NecromancerMageMissiles.SeedLow())};
        if(!ValidNpc(meteor)){throw new NotSupportedException("Invalid NPC Meteor placement");}
        _meteors.Add(id,meteor);Delay(actor,CombatAbility.Meteor,at);
        Effects.SendLocationEffect(target.Location,actor.Map,0x3709,60,10,0x489,0); // Identified warning placeholder.
        return id;
    }
    private static void NpcMeteorImpact(NecromancerMinion actor,MeteorDrop meteor,DateTime at)
    {
        var owner=(PlayerMobile)actor.ControlMaster;var center=new Point3D(meteor.X,meteor.Y,meteor.Z);ulong seed=meteor.Seed;
        int raw=WeaponMissileDamage.RollValue(meteor.Min256,meteor.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));
        var packet=new MageMissilePacket("meteorcenter","fire",10,raw,raw,0){Spell=CombatAbility.Meteor};
        foreach(var target in SkillCombat.Targets(actor,owner,center,meteor.Radius))
        {
            if(!actor.Alive||actor.Deleted){break;}
            if(target is{Alive:true,Deleted:false}&&actor.CanBeHarmful(target,false))
            {NecromancerMageMissiles.ImpactSpell(actor,target,packet,meteor.Owner,seed,at);}
        }
        if(actor.Deleted||!actor.Alive||actor.Map.MapID!=meteor.Map){return;}
        var values=MagePetBuffs.Values(CombatAbility.Meteor,10,actor.Level);int frames=SkillMechanics.For(CombatAbility.Meteor).Parameter(3)+9*SkillMechanics.For(CombatAbility.Meteor).Parameter(4);
        foreach(var offset in MeteorOffsets())
        {
            double x=meteor.X+offset.X/2.0,y=meteor.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),meteor.Z);
            if(x<0||y<0||x>=actor.Map.Width||y>=actor.Map.Height||SpellHelper.IsTown(point,owner)){continue;}
            long id=checked(++_nextId);var patch=new FirePatch(id,meteor.Owner,CombatAbility.Meteor,meteor.Map,x,y,meteor.Z,
                MeteorFireDamage256(values,false),MeteorFireDamage256(values,true),41,at.AddMilliseconds(frames*40L),at.AddMilliseconds(40),2)
                {NpcCaster=meteor.NpcCaster,SkillRank=10,Seed=SourceRandom.InitLow(NecromancerMageMissiles.SeedLow())};
            if(!ValidNpc(patch)){throw new NotSupportedException("Invalid NPC Meteor field");}_patches.Add(id,patch);
        }
        Effects.SendLocationEffect(center,actor.Map,0x36BD,20,10,0x489,0); // Identified impact placeholder.
    }
    private static void NpcMeteorField(NecromancerMinion actor,FirePatch patch)
    {
        var point=new Point3D((int)Math.Round(patch.X),(int)Math.Round(patch.Y),patch.Z);
        if(!actor.Map.CanFit(point,1,false,false,false)){_patches.Remove(patch.Id);return;}
        var next=patch with{NextUtc=patch.NextUtc.AddMilliseconds(40)};
        if(ImmolationArrows.Recipient(actor,(PlayerMobile)actor.ControlMaster,patch) is{} target)
        {
            ulong seed=patch.Seed;int damage=WeaponMissileDamage.RollValue(patch.Min256,patch.Max256,0,n=>(int)SourceRandom.Limited(ref seed,n));
            // Field03 SOFTHIT uses its own missile stream. The shared NPC
            // receiver separately retains caster intrinsic critical, with
            // field damage-rate mitigation and no ordinary hit recovery.
            SourceRandom.Limited(ref seed,128);next=next with{Seed=seed};_patches[patch.Id]=next;
            var packet=new MageMissilePacket("meteorfire","fire",10,damage,damage,0){Spell=CombatAbility.Meteor};
            NecromancerMageMissiles.ImpactSpell(actor,target,packet,patch.Owner,seed,patch.NextUtc,recover:false,damageRate:patch.DamageRate);
        }
        else{_patches[patch.Id]=next;}
        if((patch.NextUtc.Ticks/TimeSpan.TicksPerMillisecond/40)%5==0)
        {Effects.SendLocationEffect(point,actor.Map,0x398C,5,10,0x489,0);} // Identified ground-fire placeholder.
    }
    private static bool NpcPosition(uint caster,uint owner,int map,double x,double y,int z)=>((Serial)caster).IsMobile&&((Serial)owner).IsMobile
        &&map>=0&&map<Map.Maps.Length&&Map.Maps[map]!=null&&Map.Maps[map]!=Map.Internal&&z is >=-128 and <=127
        &&double.IsFinite(x)&&double.IsFinite(y)&&x>=0&&y>=0&&x<Map.Maps[map].Width&&y<Map.Maps[map].Height;
    private static bool ValidNpc(MeteorDrop s)=>s.NpcCaster==0||s.Rank==10&&s.Radius==3&&s.ImpactUtc.Kind==DateTimeKind.Utc
        &&NpcPosition(s.NpcCaster,s.Owner,s.Map,s.X,s.Y,s.Z);
    private static bool ValidNpc(FirePatch s)=>s.NpcCaster==0||s.Ability==CombatAbility.Meteor&&s.SkillRank==10&&s.Size==2&&s.DamageRate==41
        &&s.NextUtc.Kind==DateTimeKind.Utc&&s.UntilUtc.Kind==DateTimeKind.Utc&&s.NextUtc<=s.UntilUtc&&s.UntilUtc<=DateTime.MaxValue.AddMilliseconds(-40)
        &&s.X*2==Math.Floor(s.X*2)&&s.Y*2==Math.Floor(s.Y*2)&&NpcPosition(s.NpcCaster,s.Owner,s.Map,s.X,s.Y,s.Z);
}
