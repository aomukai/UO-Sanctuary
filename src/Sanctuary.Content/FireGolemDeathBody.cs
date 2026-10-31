// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record FireGolemRetirement(uint Owner,int Map,int X,int Y,int Z,DateTime DueUtc);

// A new saved subtype keeps existing FireGolemBody v0 saves readable. The
// original dying entity is retained; no replacement caster or player proxy.
[SerializationGenerator(0)]
public partial class FireGolemDeathBody:FireGolemBody
{
    private static readonly ILogger DeathLog=LogFactory.GetLogger(typeof(FireGolemDeathBody));
    [SerializableField(0,setter:"private")] private string _retirementJson="";
    private FireGolemRetirement _retired;
    private bool _finalizing;
    private long _retireTick;
    private TimerExecutionToken _deathTimer;
    internal static Action<FireGolemDeathBody> Restored;
    internal FireGolemRetirement Retirement=>_retired??(string.IsNullOrEmpty(_retirementJson)?null:_retired=JsonSerializer.Deserialize<FireGolemRetirement>(_retirementJson));
    internal bool Retiring=>Retirement!=null;
    internal PlayerMobile DeathOwner=>Retirement is{} state&&World.FindMobile((Serial)state.Owner) is PlayerMobile{Deleted:false} owner?owner:null;
    public override bool Alive=>!Retiring&&base.Alive;
    [Constructible]public FireGolemDeathBody(){}
    public override void Explode()
    {
        // Roster replacement follows real NPC death bookkeeping. Natural
        // OnBeforeDeath skips the historical immediate explosion hook.
        if(!Retiring&&!Deleted){Kill();}
    }
    public override void OnDeath(Container corpse)
    {
        if(Retiring){return;}
        if(Kind!=CombatAbility.FireGolem||ControlMaster is not PlayerMobile owner||Map==null||Map==Map.Internal)
        {base.OnDeath(corpse);return;}
        _retired=new(owner.Serial.Value,Map.MapID,X,Y,Z,TimedEffects.Now.AddMilliseconds(160));
        _retireTick=Core.TickCount+160;
        _retirementJson=JsonSerializer.Serialize(_retired);
        SuspendActions();AIObject?.AITimer.Stop();Frozen=true;
        PaladinAuras.Clear(this);SkillHealth.SetBalance256(this,0);
        try{base.OnDeath(corpse);}
        finally{ScheduleRetirement();}
    }
    public override void Delete()
    {
        if(Retiring&&!_finalizing){return;}
        base.Delete();
    }
    private void ScheduleRetirement()
    {
        _deathTimer.Cancel();var state=Retirement;if(state==null||Deleted){return;}
        var remaining=TimeSpan.FromMilliseconds(Math.Max(0,_retireTick-Core.TickCount));
        Timer.StartTimer(remaining>TimeSpan.Zero?remaining:TimeSpan.Zero,FinishRetirement,out _deathTimer);
    }
    private void FinishRetirement()
    {
        if(Deleted||!Retiring){return;}
        if(_retireTick-Core.TickCount>0){ScheduleRetirement();return;}
        try{ReleaseDeathBurst();}
        catch(Exception e){DeathLog.Error(e,"Fire Golem death burst failed: {Actor}",Serial.Value);}
        finally{_finalizing=true;Delete();}
    }
    private void ReleaseDeathBurst()
    {
        var state=Retirement;var owner=DeathOwner;
        // Offline time expires the callback without a catch-up attack. The
        // registered original source and its actual owner must still agree.
        if(owner==null||owner.NetState==null||ControlMaster!=owner||World.FindMobile(Serial)!=this){return;}
        var map=Map.Maps[state.Map];
        if(Map!=map||owner.Map!=map)
        {DeathLog.Warning("Fire Golem cross-facet death emission is not mapped: {Actor}",Serial.Value);return;}
        var center=new Point3D(state.X,state.Y,state.Z);
        foreach(var target in SkillCombat.Targets(owner,center,SkillGeometry.Tiles(6)))
        {
            if(target==this||target==owner){continue;}
            bool blocked=false;
            if(target is BaseCreature creature)
            {
                if(!SourceBlocking.TryRoll(creature,true,out blocked,out _))
                {DeathLog.Warning("Fire death recipient uses legacy shield rules: {Recipient}",target.GetType().Name);}
            }
            else{blocked=ClassAvoidance.Missile(target,false);}
            // SetDamageFlags calls intrinsic critical even after blocking.
            bool critical=NpcEquipmentRandom.Raw(this)%100<(uint)NecromancerSummons.Source("firegolem","Crit");
            if(blocked){continue;}
            int amount=critical?200:100;
            SkillCombat.ResolveHitParts(this,target,new(amount,amount,0,0,0),recover:false,enhanceBase:false);
        }
        Effects.SendLocationEffect(center,map,0x36BD,15); // Identified explosion placeholder.
    }
    public override void OnAfterDelete(){_deathTimer.Cancel();base.OnAfterDelete();}
    [AfterDeserialization]private void AfterRetirementLoad()
    {
        _retired=null;var state=Retirement;if(state==null){return;}
        if(!((Serial)state.Owner).IsMobile||state.Map<0||state.Map>=Map.Maps.Length||Map.Maps[state.Map] is not{} map||map==Map.Internal
            ||state.X<0||state.X>=map.Width||state.Y<0||state.Y>=map.Height||state.Z is <-128 or >127||state.DueUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid saved Fire Golem retirement");}
        _retireTick=Core.TickCount+Math.Clamp((long)(state.DueUtc-TimedEffects.Now).TotalMilliseconds,0,160);
        SuspendActions();AIObject?.AITimer.Stop();Frozen=true;Restored?.Invoke(this);ScheduleRetirement();
    }
}
