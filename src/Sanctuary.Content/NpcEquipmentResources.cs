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
        long base256=Basis256(actor,resource,item);
        var extra=item.ResourceInput(code,levelStat);
        int first=resource==0?item.Bonus("hp%"):resource==2?item.Bonus("mana%"):BattleOrders.Percent(actor)+PaladinVigor.StaminaPercent(actor);
        int second=resource==0?BattleOrders.LifePercent(actor):resource==2?BattleOrders.Percent(actor):0;
        int total=NpcResourceValues.Calculate(new(1,channel,item.RecipientLevel,checked((int)base256),extra.Flat256,extra.LevelCoefficientRaw,first,second));
        // Preserve the existing native resource ceiling;the exact FP256 pool
        // is separate from its integral native display/storage field.
        return Math.Clamp(total,resource==0?256:0,65000*256);
    }
    private static long Basis256(SkeletonGuardian actor,int resource,ItemStatContext? item)
    {
        var stat=resource==0?StatType.Str:resource==1?StatType.Dex:StatType.Int;
        string suffix=resource==0?"Str":resource==1?"Dex":"Int";
        int originalOffset=item is{} held?actor.GetStatMod(held.Item.Serial+suffix)?.Offset??0:0;
        int offset=checked(actor.GetStatOffset(stat)-originalOffset);
        int seed=resource==0?actor.HitsMaxSeed:resource==1?actor.StamMaxSeed:actor.ManaMaxSeed;
        int raw=resource==0?actor.RawStr:resource==1?actor.RawDex:actor.RawInt;
        // A zero seed is an explicit zero source pool. Native UO instead
        // falls back to attributes,whose raw minimum1 would invent mana.
        int basis=checked((seed<0?raw:seed)+offset);
        long base256=resource==0&&seed==actor.CombatProfile.Life?actor.SourceLife256+(long)offset*256:(long)basis*256;
        return base256;
    }
    // Source comparison basis before/after a held item,independent from the
    // native integer-only display used by an unbound prepared holder.
    internal static int LifeForRegeneration(SkeletonGuardian actor)
    {
        if(actor is ISourceLifeMaximum){return SkillHealth.Maximum256(actor);}
        if(Maximum256(actor,0) is{} exact){return exact;}
        return Math.Clamp(NpcResourceValues.Calculate(new(1,0,CreatureProfiles.Level(actor),checked((int)Basis256(actor,0,null)),0,0,0,BattleOrders.LifePercent(actor))),256,65000*256);
    }
    internal static BattleOrders.Pools BeforeAttach(SkeletonGuardian actor)
    {
        var before=BattleOrders.Capture(actor);
        // The native zero-seed fallback invents one point from raw Int. A
        // source zero-mana species has neither that maximum nor that balance.
        return actor.ManaMaxSeed==0?before with{ExactManaMaximum256=0,Mana256=0}:before;
    }
    internal static void RestoreBalances(SkeletonGuardian actor,BattleOrders.Pools before,bool rescale=true)
    {
        if(World.Loading||actor is not{Alive:true,Deleted:false}){return;}
        BattleOrders.RefreshRegeneration(actor,before.RegenerationLifeMaximum256);
        int Apply(int current,int previous,int next)=>rescale?BattleOrders.Rescale256(current,previous,next):current;
        SkillHealth.SetBalance256(actor,Apply(before.Life256,before.ExactLifeMaximum256,SkillHealth.Maximum256(actor)));
        NecromancerEffects.SetMana256(actor,Apply(before.Mana256,before.ExactManaMaximum256,NecromancerEffects.ManaMaximum256(actor)));
        BattleOrders.SetStamina256(actor,Apply(before.Stamina256,before.ExactStaminaMaximum256,BattleOrders.StaminaMaximum256(actor)));
        actor.CheckStatTimers();actor.Delta(MobileDelta.Hits|MobileDelta.Mana|MobileDelta.Stam);
    }
    internal static BattleOrders.Pools? BeforeChange(Item item)=>!World.Loading&&item.Parent is SkeletonGuardian actor&&ItemDurationDefenses.HasEquipment(actor)?BattleOrders.Capture(actor):null;
    internal static void Clamp(Item item,BattleOrders.Pools? prepared=null)
    {
        if(!World.Loading&&item.Parent is SkeletonGuardian actor&&IronGolemItems.State(actor)?.Item==item.Serial.Value)
        {
            var before=prepared??BattleOrders.Capture(actor);
            if(prepared==null&&BattleOrders.ObservedNpcMaximums(actor) is{} prior)
            {
                before=before with{ExactLifeMaximum256=prior.Life256,ExactManaMaximum256=prior.Mana256,ExactStaminaMaximum256=prior.Stamina256,
                    Life256=SkillHealth.BalanceAtMaximum256(actor,prior.Life256),
                    Mana256=Math.Min(prior.Mana256,NecromancerEffects.RawManaBalance256(actor)),
                    Stamina256=Math.Min(prior.Stamina256,BattleOrders.RawStamina256(actor))};
            }
            RestoreBalances(actor,before);
        }
    }
}
