// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// New learned skeletons opt into exact resources without rewriting historical
// SkeletonGuardian saves or the existing prepared NPC/item fixture actors.
[SerializationGenerator(0)]
public partial class RaisedSkeleton:SkeletonGuardian,ISourceLifeMaximum,ISourceUnarmedNpc
{
    private SkeletonBirthStats _birth;
    private SkeletonBirthStats Birth=>_birth??=NecromancerSkeletonBirth.Prepare(Level,Rank,Mastery,SummonDifficulty);
    public override long SourceLife256=>Birth.Life256;
    internal override int SourceRegeneration256
    {
        get
        {
            int maximum=Math.Clamp(Birth.Life256,256,65000*256);
            return BattleOrders.RegenerationAfter(Birth.Life256,maximum,maximum,NecromancerSummons.Source("necroskeleton","DamageRegen"),Birth.Regeneration256);
        }
    }
    int ISourceLifeMaximum.MaximumLife256=>NpcEquipmentResources.Maximum256(this,0)??NpcEquipmentResources.UnboundLifeMaximum256(this);
    NpcPhysicalStats ISourceUnarmedNpc.UnarmedPhysical=>UnarmedPhysical;
    internal NpcPhysicalStats UnarmedPhysical=>new(NecromancerSummons.Source("necroskeleton","A1MinD",SummonDifficulty),
        NecromancerSummons.Source("necroskeleton","A1MaxD",SummonDifficulty),Birth.FlatDamage,
        checked(Birth.DamagePercent+CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this)),0,0,false);
    internal (int Action,int End) SourceAttackClock=>DruidWolfGeometry.Clock("A1",
        SourceAttackRate.Speed(NecromancerSummons.Source("necroskeleton-animation","A1Speed"),0,SourceAttackRate.WolfRate(this)),"necroskeleton-animation");
    protected override (int Cycle,int Action) MeleeClock()
    {var clock=SourceAttackClock;return(checked(clock.End*40),checked(clock.Action*40));}
    protected override bool CheckAttackHit(Mobile target)=>ClassStats.CheckHit(this,target,0,()=>NpcEquipmentRandom.Limited(this,100)/100.0)==true;
    public override MonsterProfile CombatProfile
    {
        get
        {
            var b=Birth;var weapon=SourceWeaponBase;int percent=Math.Max(10,100+b.DamagePercent);
            return new(b.Level,(Math.Clamp(b.Life256,256,65000*256)+255)/256,b.Attack,b.Armor,
                weapon.Min*percent/100,weapon.Max*percent/100,DruidWolfGeometry.Clock("A1",source:"necroskeleton-animation").End*40,240,0,0,0,0,0);
        }
    }
    public override (int Min,int Max) SkillWeaponDamage
    {
        get
        {
            var weapon=SourceWeaponBase;int percent=Math.Max(10,100+Birth.DamagePercent+CombatAuras.DamagePercent(this)+NecromancerEffects.DamageStatPercent(this));
            return(weapon.Min*percent/100,weapon.Max*percent/100);
        }
    }
    internal override (int Min,int Max,int Percent) SourceWeaponBase=>(NecromancerSummons.Source("necroskeleton","A1MinD",SummonDifficulty)+Birth.FlatDamage,
        NecromancerSummons.Source("necroskeleton","A1MaxD",SummonDifficulty)+Birth.FlatDamage,Birth.DamagePercent);
    [Constructible]public RaisedSkeleton(){}
    internal void InitializeExact(PlayerMobile owner,int rank,int mastery)
    {
        _birth=null;Initialize(owner,rank,mastery);SkillHealth.SetBalance256(this,SkillHealth.Maximum256(this));
    }
    internal override void RefreshDifficulty(){_birth=null;base.RefreshDifficulty();}
    public override void OnAfterDelete(){NpcEquipmentRandom.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterRaisedLoad(){_birth=null;ResetLifeRecovery();}
}
