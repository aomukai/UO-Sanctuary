// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ApocalypsePotionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypsePotionChecks));
    private sealed record Saved(uint Owner,uint Target,ApocalypsePacket Packet,Point3D Point,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-potions-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("apocalypse-potions-check",Check);Register("apocalypse-potions-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Apocalypse existing potion composition and fire-only resistance delta",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Reset(BaseCreature t)
    {foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}}
    private static void Hit(PlayerMobile owner,BaseCreature target,ApocalypsePacket packet,int expected)
    {
        int before=SkillHealth.Balance256(target);var result=ApocalypsePackets.ReleasePrepared(owner,target,packet,pierceOverride:0);
        Require(result.Applied&&result.Damage256==expected&&before-SkillHealth.Balance256(target)==expected,
            $"Potion composition:result={result.Damage256},HP={before-SkillHealth.Balance256(target)},expected={expected}");
    }
    private static void Exercise(PlayerMobile owner,BaseCreature target,ApocalypsePacket packet)
    {
        TimedEffects.RemoveBuff(owner,TimedEffectCategory.Potion);Reset(target);Hit(owner,target,packet,26880); //100fire,penalty5=>105.
        Require(CombatPotions.Apply(owner,3)&&CombatPotions.Bonus(owner,"magical-penetration")==10,"Live potion fixture");
        // The packet predates this potion;it is not a birth-snapshotted buff.
        Hit(owner,target,packet,37248); //110fire*1.05 +10cold +10lightning +10poison.
        ulong fractionalSeed=SourceRandom.InitLow(43);var fractional=ApocalypsePackets.CapturePrepared(owner,1,1001,1001,ref fractionalSeed);Hit(owner,target,fractional,1456);
        target.SetResistance(ResistanceType.Fire,105);Hit(owner,target,packet,30*256); //Fire stays immune;other elements still work.
        target.SetResistance(ResistanceType.Poison,100);Hit(owner,target,packet,20*256);
        target.SetResistance(ResistanceType.Cold,50);target.SetResistance(ResistanceType.Energy,75);Hit(owner,target,packet,1920);
        Reset(target);target.SetResistance(ResistanceType.Fire,100);Hit(owner,target,packet,7961); //1.1fire+30;per-channel FP truncation.
        Reset(target);int before=SkillHealth.Balance256(target);
        Require(!ApocalypsePackets.ReleasePrepared(owner,target,packet,false).Applied&&SkillHealth.Balance256(target)==before,"Allocation refusal leaked potion damage");
        ulong seed=packet.SeedBefore;var zero=ApocalypsePackets.CapturePrepared(owner,1,0,0,ref seed);Hit(owner,target,zero,0);
        TimedEffects.RemoveBuff(owner,TimedEffectCategory.Potion);Hit(owner,target,packet,26880);
        Require(CombatPotions.Apply(owner,3,TimedEffects.Now.AddSeconds(-31))&&CombatPotions.Bonus(owner,"magical-penetration")==0,"Expired potion fixture");
        Hit(owner,target,packet,26880);TimedEffects.RemoveBuff(owner,TimedEffectCategory.Potion);
        Require(CombatPotions.Apply(owner,3),"Potion reapplication");
        var definition=NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Dwarf Star");Require(NamedGear.Available(definition.Id),"Dwarf Star fixture unavailable");
        var ring=NamedGear.Create(definition.Id,99,new Random(173),true);owner.AddToBackpack(ring);Require(owner.EquipItem(ring),"Player MDR fixture equip");ClassGear.Refresh(owner);
        try
        {
            // Ordinary NPCs have no implicit worn-item context. Check the
            // shared mitigation kernel on its supported player item path;
            // this does not admit Apocalypse PvP/special recipients.
            int reduction=GemCombat.Reduction(owner);Require(reduction is >=12 and <=15,"Player flat reduction fixture");
            int post=(110-reduction)*256*105/100,expectedAbsorb=post*15/100,expected=post-expectedAbsorb+2560;
            int unchanged=SkillHealth.Balance256(owner);
            var raw=DruidMoltenBoulder.Potion(DruidFirestorm.Modifiers(owner),new GearDamage(0,100,0,0,0));
            var parts=SkillCombat.MitigatePrepared(owner,owner,raw,out int absorbed,skillResistanceDelta:e=>e=="fire"?-5:0);
            Require(NamedElements.To256(parts.Total)==expected&&absorbed==expectedAbsorb&&SkillHealth.Balance256(owner)==unchanged,
                $"Combined fire MDR/absorption kernel:damage={NamedElements.To256(parts.Total)},absorb={absorbed},expected={expected}/{expectedAbsorb}");
        }
        finally{ring.Delete();ClassGear.Refresh(owner);TimedEffects.RemoveBuff(owner,TimedEffectCategory.Potion);}
        Require(target.FireResistance==0&&target.PoisonResistance==0&&!WarlockCombat.Implemented(CombatAbility.Apocalypse),"Potion path left resistance state or enabled unfinished cast");
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,30,2900);var target=FireBoltChecks.Target(owner,6);ulong seed=SourceRandom.InitLow(42);
        var packet=ApocalypsePackets.CapturePrepared(owner,1,25600,25600,ref seed);Exercise(owner,target,packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,packet,owner.Location,owner.Map.MapID,0)));
        Log.Information("Apocalypse potion:existing live four-element rule,expiry/removal/reapply,fire-only temporary penalty,independent immunities/resistances,zero/allocation refusal,combined fire MDR/absorption kernel on supported player Dwarf Star path;ordinary NPC item inheritance remains outside scope;no second base enhancement. Saved packet/ordinary target reusable;learned cast remains gated.");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);owner.MoveToWorld(s.Point,Map.Maps[s.Map]);
        Require(s.Generation<2,"Extra potion reload");Exercise(owner,(BaseCreature)World.FindMobile((Serial)s.Target),s.Packet);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
