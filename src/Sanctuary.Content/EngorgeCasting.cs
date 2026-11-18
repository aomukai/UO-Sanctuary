// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal sealed record EngorgeRelease(bool Consumed,int Recipients,int ManaPaid256,string Fault=null);
internal static class EngorgeCasting
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EngorgeCasting));
    internal sealed class Pending(PlayerMobile owner)
    {
        internal readonly PlayerMobile Owner=owner;
        internal bool Released,Cancelled;
        internal EngorgeRelease Result;
    }
    private static bool Actor(PlayerMobile p)=>p is{Alive:true,Deleted:false,NetState:{Running:true},Map:not null}
        &&p.Map!=Map.Internal&&!MinionSafeAreas.Contains(p)&&!SpellHelper.IsTown(p.Location,p)
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Warlock
        &&SkillMechanics.Rank(p,CombatAbility.Engorge)>0;
    internal static bool Recipient(PlayerMobile owner,BaseCreature body)=>WarlockDemonRecipients.InRange(owner,body,19);
    internal static bool Usable(PlayerMobile p,Corpse corpse)=>Actor(p)&&CorpseSkillRules.Usable(p,corpse,19)
        &&!SpellHelper.IsTown(corpse.Location,p);
    private static BaseCreature[] Roster(PlayerMobile p)=>WarlockDemonRecipients.Roster(p).Where(b=>Recipient(p,b)).ToArray();
    internal static Corpse Acquire(PlayerMobile owner,Point3D point)=>Actor(owner)
        ?NearClickTargets.AcquireCorpse(owner,point,CorpseCastTargets.NearClickTiles,c=>Usable(owner,c),stableTies:true):null;
    internal static Pending BeginPrepared(PlayerMobile p,Serial selected,Point3D aim,long now)
    {
        if(!Actor(p)){return null;}var corpse=World.FindItem(selected)as Corpse;
        if(selected==Serial.Zero){corpse=Acquire(p,aim);}if(!Usable(p,corpse)||Roster(p).Length==0){return null;}
        if(!SkillCombat.BeginPrepared(p,CombatAbility.Engorge,corpse.Serial,corpse.Location,now,
            (who,_,id,point)=>Usable(who,World.FindItem(id)as Corpse)&&Roster(who).Length>0,(who,_)=>Actor(who),CombatTiming.For(p,CombatAbility.Engorge)))
        {return null;}
        var pending=new Pending(p);ClassCombat.For(p).Engorge=pending;return pending;
    }
    internal static void Cancel(PlayerMobile p)
    {
        var s=ClassCombat.Existing(p);if(s?.Engorge is not{} pending){return;}pending.Cancelled=true;s.Engorge=null;
        if(s.PendingAbility==CombatAbility.Engorge){s.PendingAbility=CombatAbility.None;}
    }
    internal static EngorgeRelease ReleasePrepared(PlayerMobile owner,Corpse corpse,DateTime at)
    {
        if(!Usable(owner,corpse)){return new(false,0,0);}var bodies=Roster(owner);if(bodies.Length==0){return new(false,0,0);}
        var captured=EngorgePrepared.Capture(owner);
        if(at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-40L*captured.DurationFrames))
        {throw new ArgumentException("Invalid Engorge release clock");}
        if(!NecromancerEffects.CanPay(owner,captured.Mana256)||!CorpseSkillRules.Reserve(corpse)){return new(false,0,0);}
        // Single main-loop commitment:reservation prevents competing corpse
        // consumers. Preserve loot and never resurrect a spent corpse if a
        // later native recipient callback fails.
        var origin=corpse.Location;var map=corpse.Map;
        NecromancerCombat.Consume(corpse);int paid=0,applied=0;
        try
        {
            if(!NecromancerEffects.Pay(owner,captured.Mana256)){throw new NotSupportedException("Engorge mana changed during corpse commitment");}
            paid=captured.Mana256;
            WarlockRitualVisuals.Attached(owner,SpellEffectGraphics.EngorgeCast,SpellEffectSounds.EngorgeCast);
            WarlockRitualVisuals.Ground(map,origin,SpellEffectGraphics.EngorgeSource,SpellEffectSounds.EngorgeSource);
            foreach(var body in bodies)
            {
                EngorgeEffects.ApplyPrepared(body,owner,captured,at);applied++;
                if(!body.Hidden&&body.Map==map)
                {WarlockRitualVisuals.Trail(map,origin,body.Location,SpellEffectGraphics.EngorgeTravel);}
            }
            return new(true,applied,paid);
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException)
        {Log.Warning("Engorge refused unsupported recipient after corpse commitment: {Reason}",e.Message);return new(true,applied,paid,e.Message);}
    }
    internal static bool ResolvePrepared(PlayerMobile p,long now)
    {
        var s=ClassCombat.For(p);if(s.Engorge is not{} action||action.Released||action.Cancelled){return false;}
        if(!Actor(p)||!ShapeRestrictions.Allowed(p,CombatAbility.Engorge)||p.Frozen||p.Paralyzed||p.Spell!=null||!AlliedOverlap.Allowed(p)
            ||s.PendingAbility!=CombatAbility.Engorge||p.Map!=s.ActionMap||p.Location!=s.ActionOrigin||(CombatTiming.Equipped(p)?.Serial??Serial.Zero)!=s.PendingWeapon)
        {Cancel(p);return false;}
        if(now<s.EffectAt){return false;}action.Released=true;s.Engorge=null;s.PendingAbility=CombatAbility.None;
        action.Result=ReleasePrepared(p,World.FindItem(s.PendingTarget)as Corpse,TimedEffects.Now);
        return action.Result.Consumed&&action.Result.Recipients>0&&action.Result.Fault==null;
    }
}
