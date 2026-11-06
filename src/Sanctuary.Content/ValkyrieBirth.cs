// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server.Mobiles;
namespace Sanctuary;

// Supplied post-initializer life is explicit. No component/room/player-count
// or allocation-seed provenance is inferred by the prepared body factory.
internal sealed record ValkyrieBirth(int OwnerLevel,int Rank,int Decoy,int Dodge,int Avoid,int Evade,int Critical,
    int SpawnLife256,int ColdSunder,int FireSunder,int LightningSunder,int PoisonSunder,int PhysicalSunder,bool Expansion);
internal static class ValkyrieBirthNumbers
{
    internal static ValkyrieBirth Capture(PlayerMobile owner,int spawnLife256,bool expansion=true)
    {
        if(owner is not{Alive:true,Deleted:false}||!ClassCreation.Profiles.TryGet(owner.Serial,out var p)||p.Class!=DiabloClass.Amazon
            ||ClassCreation.Profiles.GetRank(owner.Serial,"amazon.valkyrie")<1||SkillMechanics.Rank(owner,CombatAbility.Valkyrie)<1)
        {throw new NotSupportedException("Valkyrie birth needs a living trained Amazon");}
        int Hard(string talent)=>ClassCreation.Profiles.GetRank(owner.Serial,"amazon."+talent);
        return new(p.Level,SkillMechanics.Rank(owner,CombatAbility.Valkyrie),Hard("decoy"),Hard("dodge"),Hard("avoid"),Hard("evade"),Hard("criticalstrike"),spawnLife256,
            ClassGear.Bonus(owner,"pierce-immunity-cold"),ClassGear.Bonus(owner,"pierce-immunity-fire"),ClassGear.Bonus(owner,"pierce-immunity-light"),
            ClassGear.Bonus(owner,"pierce-immunity-poison"),ClassGear.Bonus(owner,"pierce-immunity-damage"),expansion);
    }
    internal static ValkyrieCreationStats Calculate(ValkyrieBirth b,MonsterDifficulty difficulty)
    {
        if(b is not{OwnerLevel:>=1 and <=99,Rank:>=1 and <=255}||!Enum.IsDefined(difficulty)
            ||b.Decoy is<0 or>20||b.Dodge is<0 or>20||b.Avoid is<0 or>20||b.Evade is<0 or>20||b.Critical is<0 or>20
            ||b.SpawnLife256%256!=0||b.SpawnLife256<NecromancerSummons.Source("valkyrie","minHP",difficulty)*256
            ||b.SpawnLife256>NecromancerSummons.Source("valkyrie","maxHP",difficulty)*256
            ||NecromancerSummons.Source("valkyrie","noRatio")!=1)
        {throw new NotSupportedException("Unsupported supplied Valkyrie birth/noRatio life context");}
        int Hard(string key)=>key.ToLowerInvariant()switch{"dopplezon"=>b.Decoy,"dodge"=>b.Dodge,"avoid"=>b.Avoid,"evade"=>b.Evade,"critical strike"=>b.Critical,_=>0};
        int Accrued(string key)=>key switch
        {
            "item_pierce_cold_immunity"=>b.ColdSunder,"item_pierce_fire_immunity"=>b.FireSunder,"item_pierce_light_immunity"=>b.LightningSunder,
            "item_pierce_poison_immunity"=>b.PoisonSunder,"item_pierce_damage_immunity"=>b.PhysicalSunder,
            _=>throw new NotSupportedException("Unmapped captured Valkyrie stat: "+key)
        };
        var values=new SkillValues(SkillMechanics.For(CombatAbility.Valkyrie),key=>key=="Valkyrie"?b.Rank:0,Hard,b.OwnerLevel);
        // The complete older spawn initializer requests flags7 (HP/AC/XP),
        // not the weapon-AR lane. Source A1TH is not an initialized stat.
        return ValkyrieCreation.Prepare(values,b.OwnerLevel,(int)difficulty,b.Expansion,
            new(b.SpawnLife256,NecromancerSummons.Source("valkyrie","AC",difficulty),0),Accrued);
    }
}
