// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

// Prepared own NPC kick arithmetic only. Startup packet caching, repeated
// action events, typed fill and progressive release remain separate gates.
internal static class ShadowKickDamage
{
    internal static KickDamageInput Capture(AssassinShadowBody body,int skillDamagePercent)
    {
        var graph=ShadowEquipmentDefense.Require(body,false);
        var binding=ShadowLoadout.Binding(body);
        var frame=graph.Weapons(binding.Primary,binding.Secondary,true,true,4,0,0);
        // The source reads flat kick before suppressing weapon damage lists.
        int flat=graph.Read(EquipmentStatValues.Definition("item_kickdamage").Id);
        frame.SuppressDamage();
        try
        {
            var boots=ShadowLoadout.Originals(body).Where(i=>i.Snapshot.Equipment.Body==9).ToArray();
            if(boots.Length>1){throw new NotSupportedException("Original NPC kick has multiple feet-slot items");}
            KickBootBase? boot=boots.Length==0?null:DragonKickDamage.Base(boots[0].Snapshot.Metadata.Code);
            return new(graph.Read(0),graph.Read(2),OriginalNpcEquipment.Level(body),false,skillDamagePercent,boot,flat,
                checked(graph.Read(25)+CombatAuras.DamagePercent(body)+NecromancerEffects.DamageStatPercent(body)),graph.Read(17));
        }
        finally{frame.Restore();}
    }
    internal static int Roll256(AssassinShadowBody body,KickDamageInput input)
    {
        _=ShadowEquipmentDefense.Require(body,false);
        if(input.Player||input.Level!=OriginalNpcEquipment.Level(body))
        {throw new NotSupportedException("Shadow kick needs its captured NPC level, not player kick arithmetic");}
        return DragonKickDamage.Roll256(input,n=>NpcEquipmentRandom.Limited(body,n));
    }
}
