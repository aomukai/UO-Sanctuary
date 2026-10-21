// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Engines.Harvest;

namespace Sanctuary;

public static class ProfessionProgression
{
    // Only a successful extraction whose original challenge is exhausted gets a second gain check.
    // Its result never rerolls the catch, resource vein, byproducts or original success.
    internal static void HarvestGain(Mobile owner, HarvestDefinition definition, double trained, Type output)
    {
        if (!ClassProfessions.Affected(owner) || !TradeSkills.IsGathering((int)definition.Skill)
            || trained < 100 || trained >= 120 || definition.Resources == null) { return; }
        var skill = owner.Skills[definition.Skill];
        if (skill.BaseFixedPoint >= skill.CapFixedPoint || skill.Lock != SkillLock.Up) { return; }
        double maximum = -1;
        foreach (var resource in definition.Resources)
        {
            // Fishing mutations use the same primary fish check before choosing SOS/net/monster outcomes.
            if (definition.Skill == SkillName.Fishing) { maximum = Math.Max(maximum, resource.MaxSkill); continue; }
            foreach (var type in resource.Types)
            { if (type.IsAssignableFrom(output)) { maximum = Math.Max(maximum, resource.MaxSkill); } }
        }
        if (maximum >= 0 && trained >= maximum) { owner.CheckSkill(definition.Skill, .5); }
    }
}
