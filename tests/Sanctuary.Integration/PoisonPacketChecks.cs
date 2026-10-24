// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class PoisonPacketChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PoisonPacketChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,int Rate,int Frames,int Balance,TimedEffectState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"poison-packet.json");
    public static void Configure(){Register("poison-packet-check",Check);Register("poison-packet-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Poison Javelin/cloud and independent rate/length mitigation",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var mitigation=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-mitigation-source-probe.json")));int count=0;
        foreach(var row in mitigation.RootElement.EnumerateArray())
        {
            int Read(int i)=>row[i].GetInt32();int penalty=Read(1)==0?0:Read(1)==1?-40:-100;
            var packet=PoisonPackets.Mitigate(Read(2),Read(3),Read(0)==0,Read(4),Read(5),Read(6),Read(7),penalty,Read(8)!=0);
            Require(packet.Rate256==Read(10) && packet.Frames==Read(11),"Complete source independent rate/length resistance differential");count++;
        }
        Require(count==72000,"Poison resistance case count");
        using var creator=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"poison-javelin-damage-source-probe.json")));
        var fields=creator.RootElement.GetProperty("fields").EnumerateArray().Select(x=>x.GetString()).ToArray();count=0;
        foreach(var row in creator.RootElement.GetProperty("cases").EnumerateArray())
        {
            int Read(int i)=>row[i].GetInt32();int Source(string field)=>row[8][Array.IndexOf(fields,field)].GetInt32();
            bool cloud=Read(0)!=0;int rank=Read(1),hard=Read(2),mastery=Read(3);
            var values=new SkillValues(SkillMechanics.For(CombatAbility.PoisonJavelin),_=>rank,key=>key==SkillMechanics.For(CombatAbility.PlagueJavelin).SourceKey?hard:0,99);
            var owner=new MissileSourceStats(FireMin:2560,FireMax:7936,PoisonMin:Read(4),PoisonMax:Read(4)+19,PoisonFrames:Read(5),PoisonCount:Read(6),LifeDrain:7);
            var stats=PoisonJavelinPackets.Prepare(owner,values,mastery,cloud);
            Require(stats.PoisonMin==Source("nPoisonMinDamage") && stats.PoisonMax==Source("nPoisonMaxDamage")
                && stats.PoisonFrames==Source("nPoisonLength") && stats.PoisonCount==Source("nPoisonCount")
                && stats.FireMin==Source("nFireMinDamage") && stats.FireMax==Source("nFireMaxDamage") && stats.LifeDrain==Source("nLifeDrainMinDamage"),"Full skill/mastery/creator packet differential");
            var packet=cloud?new WeaponMissileDamage(0,0,0,0,0,false,stats):BowLaunch.FromSource(1280,2560,0,0,128,94,0,11,13,Read(7)!=0,stats);
            Require(packet.Min256==Source("nMinDamage") && packet.Max256==Source("nMaxDamage") && packet.Percent==Source("nDamagePercent")
                && packet.DemonPercent==Source("nDemonDamagePercent") && packet.UndeadPercent==Source("nUndeadDamagePercent") && packet.Critical==((Source("nFlags")&2)!=0),"Parent/source-zero physical and critical differential");count++;
        }
        Require(count==3456,"Poison creator case count");Log.Information("Poison packets:72000 full-source mitigation and3456 full-source parent/cloud packet comparisons passed");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank){Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var d),"Talent");foreach(var prerequisite in d.Prerequisites){Train(owner,prerequisite);}Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}}
    private static TimedEffectState State(Mobile target)=>TimedEffects.Snapshot().Single(x=>x.Owner==target.Serial.Value);
    private static void FreezeClock(Mobile target)
    {var state=State(target);TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(state with{UpdatedUtc=TimedEffects.Now.AddMinutes(10)}).ToArray());}
    private static void Check()
    {
        Differential();var owner=MagicArrowChecks.Fresh("PoisonPacketOwner");Train(owner,"amazon.poisonjavelin");
        if(CombatTiming.Equipped(owner) is{} old){owner.AddToBackpack(old);}
        var weapon=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(weapon,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));owner.AddToBackpack(weapon);Require(owner.EquipItem(weapon),"Ordinary javelin equip");ClassGear.Refresh(owner);
        int mana=NecromancerEffects.ManaBalance256(owner),quantity=NamedAmmunition.Remaining(weapon);
        var values=SkillMechanics.Values(owner,CombatAbility.PoisonJavelin);var parent=BowLaunch.Capture(owner,CombatAbility.PoisonJavelin,true,preparedValues:values);
        var cloud=PoisonJavelinPackets.Cloud(owner,values.Rank);Require(parent is{Min256:>0,Critical:true} && parent.Elements.PoisonMin==32 && cloud.Min256==0 && !cloud.Critical && cloud.Elements.PoisonMin==32,"Actual parent weapon/cloud source-zero packet");
        Train(owner,"amazon.plaguejavelin");var synergized=PoisonJavelinPackets.Cloud(owner,1);Require(synergized.Elements.PoisonMin==35 && cloud.Elements.PoisonMin==32,"Live hard synergy or prior packet mutation");
        var helm=NamedGear.Create("unique/72",99,new Random(31),true);owner.AddToBackpack(helm);Require(owner.EquipItem(helm),"Real Tarnhelm");ClassGear.Refresh(owner);
        Require(PoisonJavelinPackets.Cloud(owner,1)==synergized && SkillMechanics.Rank(owner,CombatAbility.PoisonJavelin)>1,"Cloud lost retained rank or used soft synergy");
        owner.AddToBackpack(weapon);Require(BowLaunch.Capture(owner,CombatAbility.PoisonJavelin,false)==null && PoisonJavelinPackets.Cloud(owner,1)==synergized,"Cloud required current javelin");Require(owner.EquipItem(weapon),"Restore javelin");
        Require(NecromancerEffects.ManaBalance256(owner)==mana && NamedAmmunition.Remaining(weapon)==quantity,"Preparation charged resources");
        Require(AmazonCombat.Implemented(CombatAbility.PoisonJavelin),"Verified packet consumer is unavailable");
        var target=new PoisonRegenProbe{Frozen=true,RawStr=2000,Hits=2000};target.MoveToWorld(new(owner.X+3,owner.Y+3,owner.Z),owner.Map);
        var prepared=PoisonPackets.Mitigate(257,1550,false,33,33,0,0,0);Require(prepared==new PreparedPoison(172,1038) && prepared.Commit(owner,target,TimedEffects.Now),"Native resisted per-frame commit");
        int before=SkillHealth.Balance256(target);var at=State(target).UpdatedUtc;TimedEffects.Settle(target,at.AddMilliseconds(40));Require(before-SkillHealth.Balance256(target)==172 && State(target).PoisonMs==41520-40 && target.DamageCalls==0,"Prepared rate/frame consumer integration");
        FreezeClock(target);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,weapon.Serial.Value,172,1037,SkillHealth.Balance256(target),State(target))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(PoisonRegenProbe)World.FindMobile((Serial)saved.Target);var weapon=(SoulGlaive)World.FindItem((Serial)saved.Weapon);
        Require(owner is{Alive:true} && target is{Alive:true} && weapon is{Deleted:false} && SkillHealth.Balance256(target)==saved.Balance && JsonSerializer.Serialize(State(target))==JsonSerializer.Serialize(saved.State),$"Prepared poison saved world state: hp={SkillHealth.Balance256(target)}/{saved.Balance}; state={JsonSerializer.Serialize(State(target))}; saved={JsonSerializer.Serialize(saved.State)}");
        var row=State(target);var now=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=target.Serial.Value).Append(row with{UpdatedUtc=now}).ToArray());TimedEffects.Settle(target,now.AddMilliseconds(40));
        Require(saved.Balance-SkillHealth.Balance256(target)==saved.Rate && State(target).PoisonMs==(saved.Frames-1)*40 && State(target).PoisonSource==saved.Owner,"Prepared packet lost/replayed rate, frame or owner");
        FreezeClock(target);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Frames=saved.Frames-1,Balance=SkillHealth.Balance256(target),State=State(target)}));
    }
}
