// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

[SerializationGenerator(0)]
public partial class FireGolemWorldBody:FireGolemDeathBody,IUnarmedGroundPet,INecromancerGroundPeriodic
{
    internal const string ProfileId="prepared-necropet-ground-fit-fire-clock-v1";
    [SerializableField(0,setter:"private")] private string _worldSaved="{}";
    [SerializableField(1,setter:"private")] private int _nextAuraFrame=50;
    private UnarmedPetWorld _ground;
    internal UnarmedPetWorld Ground=>_ground??=new(this,ProfileId,()=>_worldSaved,s=>_worldSaved=s,
        target=>base.PrepareMeleeContact(target),target=>base.CanReleaseMeleeContact(target),target=>base.ResolveMeleeContact(target),()=>base.DiscardMeleeContact());
    UnarmedPetWorld IUnarmedGroundPet.Ground=>Ground;
    (int Action,int End) IUnarmedGroundPet.GroundAttackClock=>SourceAttackClock;
    int IUnarmedGroundPet.GroundVelocityPercent=>VelocityPercent;
    internal SkeletonWorldState WorldState=>Ground.State;
    internal IronGolemContact.Prepared WorldContact=>Ground.Contact;
    internal int AuraPeriod=>Math.Max(5,checked((int)HolyFireValues.Eval("perdelay")));
    public override bool UsesNativeSummonTimer=>false;
    public FireGolemWorldBody(){CantWalk=true;AIObject?.AITimer.Stop();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override TimeSpan BeginMelee(Mobile target)=>TimeSpan.FromMilliseconds(SourceAttackClock.End*40);
    internal void BeginWorld(PlayerMobile owner,DateTime at,ulong seed)
    {
        Name="Fire Golem [placeholder]";_nextAuraFrame=AuraPeriod;Ground.Initialize(owner,at,seed);
        // Preserve the existing first two-second pulse adapter. Exact current
        // summon activation and periodic-versus-AI priority remain unverified.
    }
    void INecromancerGroundPeriodic.GroundFrame(int frame,DateTime at)
    {
        if(frame<_nextAuraFrame){return;}AdvanceAura(frame);
        if(Alive&&!Retiring&&ControlMaster is PlayerMobile owner){PulseHolyFire(owner,at);}
    }
    void INecromancerGroundPeriodic.GroundSkipped(int frame)=>AdvanceAura(frame);
    private void AdvanceAura(int frame)
    {
        if(frame>=_nextAuraFrame){int period=AuraPeriod;_nextAuraFrame=checked(_nextAuraFrame+checked((frame-_nextAuraFrame)/period+1)*period);}
    }
    public override void SuspendActions(){base.SuspendActions();Ground.Suspend();}
    internal override void InterruptForRecovery(){base.InterruptForRecovery();Ground.Suspend();}
    public override void OnDeath(Container corpse){IronGolems.Forget(this);Ground.Suspend();base.OnDeath(corpse);}
    public override void OnAfterDelete(){Ground.Cancel();IronGolems.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterFireWorldLoad()
    {
        if(_nextAuraFrame<1){throw new InvalidDataException("Invalid Fire periodic deadline");}
        Name="Fire Golem [placeholder]";AIObject?.AITimer.Stop();if(!Retiring){Ground.Load();}
    }
}
