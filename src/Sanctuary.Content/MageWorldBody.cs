// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

[SerializationGenerator(0)]
public partial class MageWorldBody:NecromancerMinion,INecromancerGroundActor
{
    internal const string ProfileId="prepared-necropet-ground-fit-mage-clock-v1";
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    private MageWorld _ranged;
    internal MageWorld Ranged=>_ranged??=new(this,()=>_worldSaved,s=>_worldSaved=s);
    internal void ObserveCombatTarget(bool present)=>Ranged.ObserveCombatTarget(present);
    internal void CombatStart(PlayerMobile owner)=>Ranged.CombatStart(owner);
    internal NecromancerGroundWorld Ground=>Ranged.World;
    NecromancerGroundWorld INecromancerGroundActor.WorldGround=>Ground;
    int INecromancerGroundActor.MovementPercent=>0;
    internal SkeletonWorldState WorldState=>Ground.State;
    internal override string SourceMonsterId=>IsMage?"necromage":base.SourceMonsterId;
    public override bool UsesNativeSummonTimer=>false;
    public MageWorldBody(){CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(MageAttackClock.End*40);
    internal void BeginWorld(PlayerMobile owner,DateTime at,ulong seed)=>Ranged.Begin(owner,at,seed);
    internal NecromancerPetAiTarget Primary(PlayerMobile owner,IronGolemWorldState state)=>Ranged.Primary(owner,state);
    internal bool Launch(PlayerMobile owner,Mobile target,DateTime at)=>Ranged.Launch(owner,target,at);
    [AfterDeserialization]private void AfterMageWorldLoad(){Name="Skeletal Mage [placeholder]";Ground.Load();}
}
