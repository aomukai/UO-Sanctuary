// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

// Own-body NPC allocation and the existing ordered ordinary contact receiver.
// Automatic GenericPet startup/selection/path/event scheduling stays separate.
internal static class GoatmanNpcContact
{
    internal static int Rank(WarlockGoatmanBody body,int skill)
    {
        var s=body?.Prepared?.Snapshot.Birth.Suppliers??throw new NotSupportedException("Missing Goatman skill snapshot");
        return skill switch{416=>s.StunRank,417=>s.FrenzyRank,418=>s.BerserkRank,419=>s.CleaveRank,_=>throw new NotSupportedException("Unknown Goatman NPC skill")};
    }
    private static void Context(WarlockGoatmanBody body,PlayerMobile owner,BaseCreature target,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc||body is not{Alive:true,Deleted:false,Prepared:not null}||owner is not{Alive:true,Deleted:false}
            ||body.ControlMaster!=owner||body.Prepared.Owner!=owner.Serial.Value||body.Map==null||body.Map==Map.Internal||owner.Map!=body.Map
            ||target is not{Alive:true,Deleted:false}||target==body||target.Map!=body.Map||!body.Permitted(owner)||!PersistentMinions.Active(owner,body))
        {throw new NotSupportedException("Goatman contact lost its captured living world/owner context");}
        if(NpcEquipmentCombat.Context(body)!=null||body.Prepared.Snapshot.Birth.Suppliers is not{InheritedPhysical5:0,InheritedPhysical7:0})
        {throw new NotSupportedException("Goatman equipped-item and duplicate nonzero sunder contexts remain unsupported");}
        _=ReviveMeleeGeometry.Unit(body);_=ReviveMeleeGeometry.Unit(target);
    }
    internal static IronGolemContact.Prepared Prepare(WarlockGoatmanBody body,PlayerMobile owner,BaseCreature target,int skill,DateTime at,
        Func<int,int> limited=null,Func<uint> raw=null)
    {
        Context(body,owner,target,at);int rank=Rank(body,skill);
        if(rank<1){throw new NotSupportedException("Captured Goatman has not unlocked this NPC skill");}
        var rules=IronGolemContact.Require(body,owner,target);
        limited??=n=>NpcEquipmentRandom.Limited(body,n);raw??=()=>NpcEquipmentRandom.Raw(body);
        var v=GoatmanNpcSkills.Values(skill,rank,body.Prepared.Snapshot.Birth.PetLevel);
        var physical=((ISourceUnarmedNpc)body).UnarmedPhysical;var added=NpcEquipmentElements.Capture(body).Stats;
        if(added.MagicMin!=0||added.MagicMax!=0)
        {throw new NotSupportedException("Goatman added magic stats require a qualified ordinary fill extension");}
        int flags=NpcMeleeResult.Resolve(body,target,b=>ReviveMeleeGeometry.Within(body,target,b),()=>ClassCombat.IsHostile(owner,target),limited,checked((int)v.Formula("toht")));
        var weapon=new GoatmanNpcWeapon(physical.Minimum,physical.Maximum,physical.DamagePercent,added.FireMin,added.FireMax,
            added.LightningMin,added.LightningMax,added.ColdMin,added.ColdMax,added.ColdFrames);
        int absorb=0;
        GoatmanNpcDamage Mitigate(GoatmanNpcDamage d)
        {
            var parts=SkillCombat.MitigatePrepared(body,target,new(d.Physical256/256.0*PaladinAuras.NativeWeaponMultiplier(body,true),
                d.Fire256/256.0,d.Cold256/256.0,d.Lightning256/256.0,0,Magic:d.Magic256/256.0),out absorb,pierceOverride:_=>0);
            int frames=d.ColdFrames<=0?0:ColdStates.LengthFrames(d.ColdFrames,NamedElements.Resistance(body,target,"cold",target.ColdResistance,pierceOverride:0),
                ItemDurationDefenses.CannotFreeze(target),ItemDurationDefenses.HalfFreeze(target));
            return new(NamedElements.To256(parts.Physical),NamedElements.To256(parts.Magic),NamedElements.To256(parts.Fire),NamedElements.To256(parts.Lightning),NamedElements.To256(parts.Cold),frames,d.StunFrames);
        }
        // do2rows allocate before their release callback;Frenzy/Cleave call
        // this allocator at release under the approved NPC action rule.
        var p=GoatmanNpcMelee.Prepare(skill,rank,flags,SkillHealth.Balance256(target),weapon,Mitigate,limited,raw);
        var packet=new PreparedMonsterDamagePacket(p.Physical256,p.Total256,p.StunFrames,p.ColdFrames,p.ResultFlags,p.HitClass,false,p.CriticalOverlay,
            p.Magic256,p.Fire256,p.Lightning256,p.Cold256,AbsorbedLife256:absorb);
        if(skill==418){GoatmanBerserkEffects.Begin(body,rank,at);}
        return new(rules,new(body.Serial.Value,target.Serial.Value,packet,default,owner.Serial.Value));
    }
    internal static IronGolemContact.Prepared PrepareBasic(WarlockGoatmanBody body,PlayerMobile owner,BaseCreature target,DateTime at)
    {
        Context(body,owner,target,at);var rules=IronGolemContact.Require(body,owner,target);
        return IronGolemContact.Prepare(body,target,rules,()=>NpcMeleeResult.Resolve(body,target,b=>ReviveMeleeGeometry.Within(body,target,b),
            ()=>ClassCombat.IsHostile(owner,target),n=>NpcEquipmentRandom.Limited(body,n)));
    }
    internal static MonsterContactResult ReleaseBasic(WarlockGoatmanBody body,PlayerMobile owner,BaseCreature target,IronGolemContact.Prepared prepared,DateTime at)
    {
        Context(body,owner,target,at);
        var point=target.Location;
        var map=target.Map;
        var result=ReviveMeleeGeometry.Release(body,owner,target,prepared,at);
        WarlockDemonVisuals.GoatmanHit(body,target,point,map,result,at);
        return result;
    }
    internal static MonsterContactResult Release(WarlockGoatmanBody body,PlayerMobile owner,BaseCreature target,int skill,
        IronGolemContact.Prepared prepared,DateTime at)
    {
        Context(body,owner,target,at);
        if(prepared==null||prepared.Consumed||prepared.Impact.Actor!=body.Serial.Value||prepared.Impact.Target!=target.Serial.Value)
        {throw new InvalidOperationException("Goatman contact is missing,changed or already consumed");}
        int rank=Rank(body,skill);
        if(rank<1){throw new NotSupportedException("Captured Goatman release skill is locked");}
        if(skill is 416 or 418){GoatmanNpcMelee.RefreshForRelease(body,skill,rank,at);}
        var point=target.Location;
        var map=target.Map;
        var result=IronGolemContact.Release(body,owner,target,prepared,b=>ReviveMeleeGeometry.Within(body,target,b),at);
        WarlockDemonVisuals.GoatmanHit(body,target,point,map,result,at);
        return result;
    }
    internal sealed class World(WarlockGoatmanBody body,PlayerMobile owner,Func<DateTime> clock,Func<bool> actionActive=null):GoatmanNpcActionProposal.Services
    {
        internal override bool Active(BaseCreature actor)=>ReferenceEquals(actor,body)&&body is{Alive:true,Deleted:false}
            &&owner is{Alive:true,Deleted:false,NetState:{Running:true}}&&body.ControlMaster==owner&&body.Map==owner.Map&&body.Map!=Map.Internal
            &&PersistentMinions.Active(owner,body)&&body.Permitted(owner)&&!MinionSafeAreas.Contains(owner)&&(actionActive?.Invoke()??true);
        internal override bool Eligible(BaseCreature actor,BaseCreature target)=>ReferenceEquals(actor,body)&&target is{Alive:true,Deleted:false}
            &&target.Map==body.Map&&ClassCombat.IsHostile(owner,target)&&!SpellHelper.IsTown(target.Location,owner)&&ReviveMeleeGeometry.Within(body,target,0);
        internal override int Reach(BaseCreature actor)=>ReviveMeleeGeometry.Unit(body).RawRange;
        internal override BaseCreature[] Nearby(BaseCreature actor,int sourceRadius)=>[..SkillCombat.NearbySnapshot<BaseCreature>(body.Map,body.Location,(int)Math.Ceiling(SkillGeometry.Tiles(sourceRadius)))];
        internal override bool Contact(BaseCreature actor,BaseCreature target,int skill,int rank)
        {
            if(!ReferenceEquals(actor,body)||rank!=Rank(body,skill)){throw new NotSupportedException("Goatman action source/rank changed");}
            var at=clock();var prepared=Prepare(body,owner,target,skill,at);var result=Release(body,owner,target,skill,prepared,at);
            return result.Commit!=null&&(result.Packet.ResultFlags&SourceMeleeResult.Successful)!=0;
        }
    }
}
