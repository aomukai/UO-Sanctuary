// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowDeathState(int Map,int X,int Y,int Z,DateTime StartedUtc,DateTime DueUtc,DateTime ExpiryUtc,
    NpcItemSpellSource Source,bool Emitted=false,string Fault=null);

internal static class ShadowDeathNovas
{
    internal static int Rank(ShadowOwnerSnapshot capture,int lifePercent)
        =>lifePercent>10?1:Math.Min(capture.SkillRanks().Single(s=>s.Id==268).Rank/2+1,15);
    internal static void Validate(ShadowDeathState death,ShadowBodyState birth)
    {
        if(death==null){return;}
        var source=death.Source;var packet=source?.Packet;
        if(death.Map<0||death.Map>=Map.Maps.Length||Map.Maps[death.Map] is not{} map||map==Map.Internal
            ||death.X<0||death.X>=map.Width||death.Y<0||death.Y>=map.Height||death.Z is <-128 or >127
            ||death.StartedUtc.Kind!=DateTimeKind.Utc||death.StartedUtc>DateTime.MaxValue.AddSeconds(-4)
            ||death.DueUtc!=death.StartedUtc.AddMilliseconds(160)||death.ExpiryUtc!=death.DueUtc.AddMilliseconds((SorceressSpellMissiles.NovaFrames+1)*40)
            ||source is not{DeathNova:true}||source.Actor!=birth.Equipment?.Actor||source.Binding!=birth.Weapons?.Identity
            ||packet is not{Skill:48,Missile:"nova",Element:"ltng",Emissions:64,ColdFrames:0}||packet.Rank!=Rank(birth.Capture,0)
            ||packet.Minimum256<0||packet.Maximum256<packet.Minimum256||death.Fault?.Length>512)
        {throw new InvalidDataException("Invalid retained original Shadow death nova");}
    }
    internal static bool Attached(AssassinShadowBody body,PlayerMobile owner,int map,NpcItemSpellSource source)
        =>body is{Deleted:false,Retiring:true}&&body.DeathState is{} death&&death.Source==source&&source.DeathNova
            &&owner is{Alive:true,Deleted:false}&&body.Prepared.Owner==owner.Serial.Value&&body.ControlMaster==owner
            &&body.Map!=null&&body.Map!=Map.Internal&&body.Map.MapID==death.Map&&death.Map==map&&owner.Map==body.Map
            &&body.Location==new Point3D(death.X,death.Y,death.Z)&&!Server.Spells.SpellHelper.IsTown(body.Location,owner);
    internal static ShadowDeathState Capture(AssassinShadowBody body,DateTime at)
    {
        var rank=Rank(body.Prepared.Capture,0);var packet=NpcItemSpellDamage.CaptureOriginal(body,48,rank);
        var context=new NpcItemSpellSource(body.Serial.Value,OriginalNpcEquipment.Binding(body),packet){DeathNova=true};
        return new(body.Map.MapID,body.X,body.Y,body.Z,at,at.AddMilliseconds(160),at.AddMilliseconds(160+(SorceressSpellMissiles.NovaFrames+1)*40),context);
    }
}

public partial class AssassinShadowBody
{
    private static readonly Server.Logging.ILogger DeathLog=Server.Logging.LogFactory.GetLogger(typeof(AssassinShadowBody));
    private ShadowDeathState _pendingDeath;
    private bool _finalizingDeath;
    private TimerExecutionToken _deathTimer;
    internal ShadowDeathState DeathState=>State?.Death;
    internal bool Retiring=>DeathState!=null;
    public override bool Alive=>!Retiring&&base.Alive;
    public override bool KeepsItemsOnDeath=>_pendingDeath!=null||Retiring||base.KeepsItemsOnDeath;
    public override bool OnBeforeDeath()
    {
        bool allowed=base.OnBeforeDeath();if(!allowed||Retiring){return allowed;}
        if(SourceUnique&&Map!=null&&Map!=Map.Internal&&ControlMaster is PlayerMobile{Alive:true,Deleted:false})
        {_pendingDeath=ShadowDeathNovas.Capture(this,TimedEffects.Now);}
        return allowed;
    }
    public override void OnDeath(Container corpse)
    {
        if(Retiring){return;}
        if(_pendingDeath==null){base.OnDeath(corpse);return;}
        RestorePrepared(State with{Death=_pendingDeath});_pendingDeath=null;
        SuspendActions();AIObject?.AITimer.Stop();Frozen=true;SkillHealth.SetBalance256(this,0);
        try{base.OnDeath(corpse);}finally{ScheduleDeath();}
    }
    public override void Delete()
    {
        // Native OnDeath deletes NPCs. Keep this actual dead identity until
        // its own delayed effect/children expire; ordinary living Delete is unchanged.
        if(Retiring&&!_finalizingDeath){return;}base.Delete();
    }
    private void ScheduleDeath()
    {
        _deathTimer.Cancel();var death=DeathState;if(death==null||Deleted){return;}
        DateTime next=death.Emitted?death.ExpiryUtc:death.DueUtc;
        Timer.StartTimer(TimeSpan.FromMilliseconds(Math.Max(0,(next-TimedEffects.Now).TotalMilliseconds)),()=>
            AdvanceDeath(TimedEffects.Now,ControlMaster is PlayerMobile{NetState:{Running:true}}),out _deathTimer);
    }
    internal void AdvanceDeath(DateTime at,bool actions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Death callback requires UTC");}
        var death=DeathState;if(death==null||Deleted){return;}
        if(at>=death.ExpiryUtc){_finalizingDeath=true;Delete();return;}
        if(!death.Emitted&&at>=death.DueUtc)
        {
            // Persist the once-only transition before publishing children.
            RestorePrepared(State with{Death=death with{Emitted=true}});
            var allocation=NpcWorldAllocation.Capture();
            try
            {
                if(actions&&ShadowDeathNovas.Attached(this,ControlMaster as PlayerMobile,death.Map,death.Source))
                {SorceressSpellMissiles.CommitValkyrie(SorceressSpellMissiles.PrepareDeathNova(this,death.Source,death.DueUtc));}
            }
            catch(Exception e)
            {
                NpcWorldAllocation.Restore(allocation);string reason=e.Message[..Math.Min(512,e.Message.Length)];
                RestorePrepared(State with{Death=DeathState with{Fault=reason}});DeathLog.Error(e,"Original Shadow death nova failed: {Actor}",Serial.Value);
            }
        }
        ScheduleDeath();
    }
    private void RestoreDeath()
    {SuspendActions();AIObject?.AITimer.Stop();Frozen=true;ScheduleDeath();}
}
