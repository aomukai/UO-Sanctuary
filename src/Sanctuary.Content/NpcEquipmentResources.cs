// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal static class NpcEquipmentResources
{
    // Native resource0=life,1=stamina,2=mana;numeric channel1=mana.
    internal static int? Maximum256(Mobile mobile,int resource)
    {
        if(World.Loading||mobile is not SkeletonGuardian actor||resource is <0 or >2
            ||IronGolemItems.State(actor) is not{} binding
            ||World.FindItem((Serial)binding.Item) is not{Deleted:false} original||original.Parent!=actor
            ||NpcEquipmentCombat.Context(actor) is not{} item){return null;}
        int channel=resource==1?2:resource==2?1:0;
        string code=resource==0?"hp":resource==1?"stam":"mana";
        int levelStat=resource==0?216:resource==1?242:217;
        var stat=resource==0?StatType.Str:resource==1?StatType.Dex:StatType.Int;
        string suffix=resource==0?"Str":resource==1?"Dex":"Int";
        int originalOffset=actor.GetStatMod(item.Item.Serial+suffix)?.Offset??0;
        int offset=checked(actor.GetStatOffset(stat)-originalOffset);
        int seed=resource==0?actor.HitsMaxSeed:resource==1?actor.StamMaxSeed:actor.ManaMaxSeed;
        int raw=resource==0?actor.RawStr:resource==1?actor.RawDex:actor.RawInt;
        // A zero seed is an explicit zero source pool. Native UO instead
        // falls back to attributes,whose raw minimum1 would invent mana.
        int basis=checked((seed<0?raw:seed)+offset);
        long base256=resource==0&&seed==actor.CombatProfile.Life?actor.SourceLife256+(long)offset*256:(long)basis*256;
        var extra=item.ResourceInput(code,levelStat);
        int first=resource==0?item.Bonus("hp%"):resource==2?item.Bonus("mana%"):BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor);
        int second=resource==0?BattleOrders.LifePercent(actor):resource==2?BattleOrders.Percent(actor):0;
        int total=NpcResourceValues.Calculate(new(1,channel,item.RecipientLevel,checked((int)base256),extra.Flat256,extra.LevelCoefficientRaw,first,second));
        // Preserve the existing native resource ceiling;the exact FP256 pool
        // is separate from its integral native display/storage field.
        return Math.Clamp(total,resource==0?256:0,65000*256);
    }
    internal static void RestoreBalances(SkeletonGuardian actor,BattleOrders.Pools before)
    {
        if(World.Loading||actor is not{Alive:true,Deleted:false}){return;}
        SkillHealth.SetBalance256(actor,before.Life256);
        NecromancerEffects.SetMana256(actor,before.Mana256);
        BattleOrders.SetStamina256(actor,before.Stamina256);
        actor.CheckStatTimers();actor.Delta(MobileDelta.Hits|MobileDelta.Mana|MobileDelta.Stam);
    }
    internal static void Clamp(Item item)
    {
        if(!World.Loading&&item.Parent is SkeletonGuardian actor&&IronGolemItems.State(actor)?.Item==item.Serial.Value)
        {RestoreBalances(actor,BattleOrders.Capture(actor));}
    }
}
