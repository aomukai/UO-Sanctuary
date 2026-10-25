// SPDX-License-Identifier: GPL-3.0-or-later
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

// Player74 shares do55 packet arithmetic with NPC312, but keeps the existing
// player allegiance, spell/potion, shield and hit-recovery consumers.
internal static class CorpseExplosionCasts
{
    internal static bool Supported(Corpse corpse)
        => CorpseSkillRules.TryBaseLife(corpse,out var life) && life.Seed.HasValue;

    internal static bool Release(PlayerMobile owner,Corpse corpse,SkillValues values)
    {
        if(!Supported(corpse) || !CorpseSkillRules.Usable(owner,corpse,SkeletonPets.Range)) { return false; }
        var definition=SkillMechanics.For(CombatAbility.CorpseExplosion);
        if(definition.Number("srvdofunc")!=55 || definition.Text("EType")!="fire")
        { throw new System.NotSupportedException("Unsupported player corpse-explosion route"); }
        var row=CorpseSkillRules.Profiles.Find(corpse);var life=row.BaseLife;ulong stream=life.Seed.Value;
        var packet=CorpseExplosionPackets.Create(life.Base256,ClassStats.Level(owner),life.Level,
            checked((int)values.Eval("aurarangecalc")),checked((int)values.Eval("calc1")),
            checked((int)values.Eval("calc2")),checked((int)values.Eval("calc3")),
            bound=>(int)SourceRandom.Limited(ref stream,bound));
        CorpseSkillRules.Profiles.Store(row with{NoSelect=true,BaseLife=life with{Seed=stream}});
        var center=corpse.Location;NecromancerCombat.Consume(corpse);
        foreach(var target in SkillCombat.Targets(owner,center,packet.OuterRange/2.0))
        {
            int dx=checked((target.X-center.X)*2),dy=checked((target.Y-center.Y)*2);
            var hit=packet.Recipient(checked(dx*dx+dy*dy));
            // do55's fire writer does not multiply corpse life by item mastery.
            // Approved Sanctuary spell-potion additions retain their own mastery.
            SkillCombat.HitParts(owner,target,new(hit.Physical256/256.0,hit.Fire256/256.0,0,0,0),
                true,spell:true,enhanceBase:false);
        }
        Effects.SendLocationEffect(center,owner.Map,0x36BD,15); // Identified placeholder.
        return true;
    }
}
