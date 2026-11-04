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

public static class ReviveSwingClockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ReviveSwingClockChecks));
    private sealed record Saved(uint Owner,uint Actor,int Ias,int WeaponSpeed,ReviveSwingPlan Plan,int Life,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"revive-swing-clock-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("revive-swing-clock-check",Check);Register("revive-swing-clock-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Owned original A1 clocks and frozen IAS/weapon speed",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceGrid()
    {
        var rows=BalanceCatalog.Read<ReviveAttackAnimation>("revive-attack-clocks");int accepted=0,refused=0;
        foreach(var row in rows.Where(r=>r.Issue!=null))
        {bool no=false;try{_=ReviveSwingClock.Calculate(row.Source,0,100,0);}catch(NotSupportedException){no=true;}Require(no,"Uncertain A1 row was guessed: "+row.Source);}
        Require(rows.Length==751&&rows.Count(r=>r.Issue==null)==436,"Owned A1 source coverage changed");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"revive-attack-clock-source-probe.json")));
        foreach(var sample in doc.RootElement.EnumerateArray())
        {
            var row=rows[sample[0].GetInt32()];var actions=sample[5].EnumerateArray().Where(e=>e[0].GetInt32()==1&&e[2].GetInt32()==1).ToArray();
            ReviveSwingPlan plan=null;bool no=false;try{plan=ReviveSwingClock.Calculate(row.Source,sample[1].GetInt32(),sample[2].GetInt32(),sample[3].GetInt32());}catch(NotSupportedException){no=true;}
            Require(no==(actions.Length!=1),"Source A1 skipped-action refusal differs");
            if(no){refused++;continue;}
            var end=sample[5].EnumerateArray().Single(e=>e[0].GetInt32()==2);
            Require(plan.Speed==sample[4].GetInt32()&&plan.Action==actions[0][1].GetInt32()&&plan.End==end[1].GetInt32(),"Complete source A1 rate/event clock differs: "+row.Source);accepted++;
        }
        Require(accepted+refused==97664,"Incomplete source clock context grid");
        Require(ReviveSwingClock.Calculate("skeleton1",0,100,0)is{Speed:224,Action:12,End:19}
            &&ReviveSwingClock.Calculate("zombie1",0,100,0)is{Speed:256,Action:9,End:16},"Owned basic monster clock goldens differ");
        Log.Information("Revive A1 clocks:97664complete source contexts,{Accepted}accepted/{Refused}explicit skipped-action refusals;436ordinary rows,315metadata refusals",accepted,refused);
    }
    private static void Exercise(PlayerMobile owner,BaseCreature actor,int ias,int speed,ReviveSwingPlan expected)
    {
        owner.MoveToWorld(new(6600,3680,0),Map.Trammel);actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        Require(ReviveSwingClock.Capture(actor)==expected&&expected.FasterAttackRate==ias&&expected.WeaponSpeed==speed&&expected.ActorRate==100,"Frozen original A1 lost independent live item inputs");
        int life=SkillHealth.Balance256(actor);var rng=NpcEquipmentRandom.State(actor);
        for(int n=0;n<3;n++){_=ReviveSwingClock.Capture(actor);}
        Require(SkillHealth.Balance256(actor)==life&&NpcEquipmentRandom.State(actor)==rng,"Clock queries healed or advanced original RNG");
        var state=ReviveNativeActivation.State(actor);var before=ReviveBeforeDeath.Decode(state.Original.BeforeDeathJson);var frozen=ReviveBeforeDeath.Equipment(before);
        var historical=frozen.Select(s=>s with{CombatJson=JsonSerializer.Serialize(FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(s.CombatJson)with
            {Basis=FrozenEquipmentEffects.Decode<FrozenCombatSnapshot>(s.CombatJson).Basis with{WeaponSpeed=null}})}).ToArray();
        bool no=false;try{_=ReviveSwingClock.Calculate("skeleton1",historical.Select(FrozenEquipmentEffects.Bind).ToArray(),state.Setup.CombatPlan.Level,100);}catch(NotSupportedException){no=true;}
        Require(no,"Historical missing weapon speed was invented");
        var old=state.Original with{BeforeDeathJson=JsonSerializer.Serialize(before with{EquipmentJson=JsonSerializer.Serialize(historical)})};
        Require(ReviveEquipmentGraph.Prepare(state.Setup,old)==state.Setup,"Historical weapon timing gap broke the admitted old stat/event profile");
        var items=frozen.Select(FrozenEquipmentEffects.Bind).ToArray();var weapons=items.Where(i=>i.CombatBase(state.Setup.CombatPlan.Level).Weapon).ToArray();
        no=false;try{_=ReviveSwingClock.Calculate("skeleton1",weapons.Concat(weapons).ToArray(),state.Setup.CombatPlan.Level,100);}catch(NotSupportedException){no=true;}Require(no,"Multiple original weapons silently selected a timing hand");
        SkillHealth.SetBalance256(actor,SkillHealth.Maximum256(actor)*2/3+17);owner.Internalize();actor.Internalize();
    }
    private static void Check()
    {
        SourceGrid();var owner=PoisonDaggerChecks.Owner();owner.Name="PoisonDaggerFixtureReviveSwingClockOwner";
        MagePetBuffChecks.Train(owner,"necromancer.revive",20);MagePetBuffChecks.Train(owner,"necromancer.skeletonmastery",20);FixtureSpace.Corridor(owner,6600,24,3680);
        var weapon=new Axe();ClassGear.Profiles.Ensure(weapon);var belt=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Goldwrap").Id,99,new Random(173),true);
        int speed=ClassGear.Base(weapon).Speed,ias=new[]{weapon,belt}.Sum(item=>new[]{"swing1","swing2","swing3"}.Sum(code=>new ItemStatContext(item,1).Bonus(code)));
        Require(speed!=0&&ias>0,"Clock fixture needs positive independent weapon/IAS controls");
        var actor=ReviveMeleeGeometryChecks.Create(owner,[weapon,belt]);actor.Name="PoisonDaggerFixtureReviveSwingClock";
        Require(weapon.Deleted&&belt.Deleted,"Clock fixture lost independent loot deletion");var plan=ReviveSwingClock.Calculate("skeleton1",ias,100,speed);
        Exercise(owner,actor,ias,speed,plan);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,actor.Serial.Value,ias,speed,plan,SkillHealth.Balance256(actor))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var actor=World.FindMobile((Serial)saved.Actor)as BaseCreature;
        Require(actor is{Alive:true,Deleted:false}&&actor.Map==Map.Internal&&SkillHealth.Balance256(actor)==saved.Life,"Clock fixture changed original wounds/body during reload");
        Exercise(owner,actor,saved.Ias,saved.WeaponSpeed,saved.Plan);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(actor),Generation=saved.Generation+1}));
    }
}
