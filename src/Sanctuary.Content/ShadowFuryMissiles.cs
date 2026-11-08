// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record NpcFuryMissileSource(uint CreditOwner,ShadowFuryPacket Packet,ulong Seed);

// Prepared NPC Fury flights share the weapon-missile path/collision/save host.
// The future intrinsic caster supplies its qualified critical and child seed;
// neither player ownership nor hostile-spell targeting substitutes for the NPC.
internal static class ShadowFuryMissiles
{
    internal const string Profile="source-original-shadow-fury-missile-v1";
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowFuryMissiles));
    private static readonly HashSet<string> Gaps=new();
    internal static bool CanLaunch(AssassinShadowBody body,Point3D aim,DateTime at)
        =>at.Kind==DateTimeKind.Utc&&ShadowSkillCasting.Attached(body)&&body.ControlMaster is PlayerMobile
            &&aim.X>0&&aim.Y>0&&aim.X<body.Map.Width&&aim.Y<body.Map.Height&&body.InLOS(aim)&&!SpellHelper.IsTown(aim,body.ControlMaster);
    internal static long LaunchPrepared(AssassinShadowBody body,Point3D aim,int rank,bool critical,ulong seed,DateTime at)
    {
        if(!CanLaunch(body,aim,at)){return 0;}
        return LaunchCaptured(body,aim,rank,ShadowFuryPackets.Capture(body,rank,critical),seed,at);
    }
    internal static long LaunchCaptured(AssassinShadowBody body,Point3D aim,int rank,ShadowFuryPacket packet,ulong seed,DateTime at)
    {
        if(!CanLaunch(body,aim,at)){return 0;}RequirePacket(packet);
        if(packet.Actor!=body.Serial.Value||packet.Binding!=ShadowLoadout.Binding(body).Identity){throw new NotSupportedException("Fury launch lost its actual packet source");}
        foreach(var pair in new[]{("GetHit",1),("HitClass",10),("CollideKill",1),("Size",1),("ToHit",1),("Vel",22),("Range",40),("Activate",1)})
        {if(NecromancerSummons.Source("bladefragment1",pair.Item1)!=pair.Item2){throw new NotSupportedException("Unqualified own Fury missile field: "+pair.Item1);}}
        var owner=(PlayerMobile)body.ControlMaster;var slow=AmazonSlowMissiles.Capture(body,true);var damage=BladeFuryMissiles.Scale(packet.Damage,slow);
        return WeaponMissiles.LaunchNpcFury(body,new(owner.Serial.Value,packet,seed),aim,rank,damage,slow,at);
    }
    internal static void RequirePacket(ShadowFuryPacket packet)
    {
        if(packet?.Damage is not{} d||packet.Flags!=(d.Critical?3:1)||packet.TargetArmorDelta>0||d.Elements is not{} e||e.BurnMin!=0||e.BurnMax!=0||e.BurnFrames!=0)
        {throw new NotSupportedException("NPC Fury burn/bypass/positive armor packet needs an explicit missile adapter");}
        WeaponStartEffects.RequireArmorDelta(packet.TargetArmorDelta);
    }
    internal static bool Valid(WeaponFlight f)
    {
        var n=f.Npc;var p=n?.Packet;
        return p!=null&&((Serial)n.CreditOwner).IsMobile&&((Serial)p.Actor).IsMobile&&p.Actor==f.Owner&&p.Actor!=n.CreditOwner
            &&p.Binding is{Length:64}&&!p.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))&&p.TargetArmorDelta<=0
            &&p.Flags==(p.Damage?.Critical==true?3:1)&&p.Damage?.Elements is{} e&&e.BurnMin==0&&e.BurnMax==0&&e.BurnFrames==0
            &&f.Ability==CombatAbility.BladeFury&&!f.HostileSpell&&f.Guided==null&&!f.PathChanged&&f.MechanicalProfile==Profile
            &&BladeFuryMissiles.ValidValues(f.SkillRank,f.Frames,f.Activation,f.NextDelay,f.Pierces,f.Flags,f.Missile,f.Damage,f.Slow)
            &&f.SourceSteps?.Cells is{Length:>=1 and <=10}&&f.Slow is{} slow&&BladeFuryMissiles.Scale(p.Damage,slow)==f.Damage;
    }
    internal static AssassinShadowBody Owner(WeaponFlight f)
    {
        if(World.FindMobile((Serial)f.Owner)is not AssassinShadowBody{Alive:true,Deleted:false} body||body.ControlMaster is not PlayerMobile{Alive:true,Deleted:false} owner
            ||owner.Serial.Value!=f.Npc.CreditOwner||OriginalNpcEquipment.Binding(body)!=f.Npc.Packet.Binding){return null;}
        return body;
    }
    internal static bool Active(AssassinShadowBody body,WeaponFlight f,bool? actions)
        =>body.ControlMaster is PlayerMobile owner&&(actions??owner.NetState is{Running:true})&&NpcItemSpellFlights.Attached(body,owner,f.Map,f.Npc.Packet.Binding);
    internal static bool Target(AssassinShadowBody body,Mobile target)=>body.ControlMaster is PlayerMobile owner&&target is BaseCreature{Alive:true,Deleted:false,Blessed:false}
        &&target!=body&&target.Map==body.Map&&ClassCombat.IsHostile(owner,target)&&!ClassCombat.Friendly(body,target)&&!SpellHelper.IsTown(target.Location,target);
    internal static int Chance(AssassinShadowBody body,BaseCreature target,int missileToHit)
    {
        var g=OriginalNpcEquipment.Graph(body);
        int percent=checked(g.Read(119)+(SorceressEffects.Enchantment(body)?.AttackPercent??0)+CombatAuras.AttackPercent(body)+NecromancerEffects.AttackStatPercent(body)+HexBane.TargetAttackPercent(body));
        return AssassinTrapAttack.Chance(OriginalNpcEquipment.Level(body),g.Read(19),g.Read(2),percent,ClassStats.Level(target),NamedCombat.DefenseFor(body,target,ClassStats.Defense(target),true),missileToHit);
    }
    internal static int Result(int defense,bool critical)
    {
        if((defense&~31)!=0){throw new NotSupportedException("Unknown NPC missile avoidance result");}
        int flags=5|(critical?8192:0);if((defense&8)!=0){flags|=512;}if((defense&2)!=0){flags|=256;}if((defense&17)!=0){flags|=16;}
        return defense==0?flags:flags&~1;
    }
    internal static int Impact(AssassinShadowBody body,BaseCreature target,WeaponFlight f,DateTime at,out ulong finalSeed)
    {
        if(!Valid(f)||body.Serial.Value!=f.Owner||!Target(body,target)){throw new NotSupportedException("Invalid own NPC Fury impact context");}
        var owner=(PlayerMobile)body.ControlMaster;RequirePacket(f.Npc.Packet);var rules=IronGolemContact.Require(body,owner,target,missile:true);
        if(!SourceBlocking.TryRate(target,out _,out _)){throw new NotSupportedException("NPC Fury needs a mapped recipient shield/avoidance producer");}
        var graph=OriginalNpcEquipment.Graph(body);
        if(NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0){throw new NotSupportedException("Fury intrinsic NPC critical needs its separate impact roll");}
        finalSeed=f.Npc.Seed;
        if(!AssassinTrapAttack.Hit(Chance(body,target,f.Damage.SkillAttackPercent),NpcEquipmentRandom.Raw(body))){return 0;}
        ulong seed=finalSeed;var damage=f.Damage.Roll(target,n=>(int)SourceRandom.Limited(ref seed,n),out int poison);finalSeed=seed;
        if(!NpcBlockOrDodge.Missile(target,damage.Physical!=0,out int block)){throw new NotSupportedException("Unqualified NPC Fury avoidance context");}
        int flags=Result(block,f.Damage.Critical);var e=f.Damage.Elements;NpcWeaponImpact impact;
        if((flags&1)==0){impact=new(body.Serial.Value,target.Serial.Value,new(0,0,0,0,flags,HitClass:10),default,owner.Serial.Value);}
        else
        {
            var fill=new NpcFillPacket(Physical:NamedElements.To256(damage.Physical),Fire:NamedElements.To256(damage.Fire),Magic:NamedElements.To256(damage.Magic),Lightning:NamedElements.To256(damage.Lightning),Cold:NamedElements.To256(damage.Cold),ColdFrames:e.ColdFrames,
                Poison:poison,PoisonFrames:e.PoisonImpactFrames,LifeDrain:e.LifeDrain,ManaDrain:e.ManaDrain,ResultFlags:flags,HitFlags:32,HitClass:10);
            var pierce=new NpcElementPierce(graph.Read(333),graph.Read(335),graph.Read(334),graph.Read(358));
            impact=NpcEquipmentImpact.FromFill(body,target,fill,new(body.Serial.Value,owner.Serial.Value,e,0,Math.Max(0,graph.Read(336))),pierce.For,PaladinAuras.NativeWeaponMultiplier(body,true));
        }
        int hp=SkillHealth.Balance256(target);_=IronGolemContact.ReleaseMissile(body,owner,target,new(rules,impact),at);
        // Complete collision applies the saved missile armor delta after the
        // damage/return path,including shield block. Accuracy miss returns early.
        if(target is{Alive:true,Deleted:false}&&target is not BaseHire&&f.Npc.Packet.TargetArmorDelta!=0){WeaponStartEffects.ApplyArmorDelta(target,f.Npc.Packet.TargetArmorDelta,at);}
        return Math.Max(0,hp-SkillHealth.Balance256(target));
    }
    internal static void TryImpact(AssassinShadowBody body,BaseCreature target,WeaponFlight f)
    {
        try{_=Impact(body,target,f,f.NextUtc,out _);}
        catch(NotSupportedException e){if(Gaps.Add(target.GetType().Name+":"+e.Message)){Log.Warning("NPC Fury recipient unsupported: {Target}: {Reason}",target.GetType().Name,e.Message);}}
    }
}
