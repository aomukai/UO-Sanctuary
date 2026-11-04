// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;

namespace Sanctuary;

// Current table + complete older ordinary-human mode-entry projection. SQ
// initializes dwSeqSpeed=256. With A1 transition and UseAttackRate disabled,
// Other changes only wAnimSpeed; IAS/FCR do not replace the sequence clock.
// This does not register a learned cast, pay mana or decide a landing policy.
internal static class DragonFlightPreparation
{
    internal const int Advance=256;
    internal static SourceAnimationState Plan(string sourceWeaponCode,int frame=0)
    {
        var skill=SkillMechanics.For(CombatAbility.DragonFlight);
        if(skill.SourceId!=275||skill.Text("anim")!="SQ"||skill.Text("seqtrans")!="A1"
            ||skill.Number("seqnum")!=21||skill.Number("UseAttackRate")!=0)
        {throw new InvalidDataException("Unsupported Dragon Flight mode/rate context");}
        return PlayerWeaponSequences.Schedule(21,sourceWeaponCode,Advance,0,frame);
    }
    internal static (int Teleport,int Kick,int End) Clock(string sourceWeaponCode="HTH")
    {
        var plan=Plan(sourceWeaponCode);var actions=plan.Events.Where(x=>x.Type==SourceAnimationEvents.ModeChange).ToArray();
        if(actions.Length!=2||actions[0].Kind!=1||actions[0].Param!=0||actions[1].Kind!=1||actions[1].Param!=1)
        {throw new InvalidDataException("Unsupported Dragon Flight sequence events");}
        return(actions[0].Frame,actions[1].Frame,plan.Events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame);
    }
    // ActionTiming's first event is the teleport. A future Flight controller
    // must also dispatch Plan's second event; it is not an ordinary one-hit cast.
    internal static ActionTiming Timing()
    {
        var clock=Clock();return new(clock.End,clock.Teleport);
    }
}
