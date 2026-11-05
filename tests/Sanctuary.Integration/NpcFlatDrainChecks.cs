// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcFlatDrainChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcFlatDrainChecks));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(n=>n.GetInt32()).ToArray();
    private static string Json<T>(T o)=>JsonSerializer.Serialize(o);
    private sealed record Saved(uint Actor,uint Target,int SourceLife,int TargetLife,int Mana,int Stamina,CombatStunState Stun,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-flat-drains-fixtures.json");
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-flat-drains-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=Numbers(row.GetProperty("input"));int physical=Math.Min(a[0],a[1]),total=checked(a[0]+a[5]);
            int heal=NpcFlatDrains.Healing(physical,a[2],a[3],a[4],a[5],a[6],a[7],a[8]);
            int life=total>0?a[1]-total:a[1];if(total>0&&life<256){life=0;}
            int flags=life<=0?a[9]|2:a[9]&~2;
            int[] actual=[1000+heal,life,NpcFlatDrains.Remaining(a[2],a[6]),NpcFlatDrains.Remaining(a[3],a[7]),physical,total,a[5],a[6],a[7],flags,heal>0?1:0];
            Require(actual.SequenceEqual(Numbers(row.GetProperty("output"))),"Full total/ExecuteEvents drain control "+count+" input="+Json(a)+" got="+Json(actual)+" expected="+row.GetProperty("output").GetRawText());count++;
        }
        Require(count==11664,"Missing full-source NPC drain controls");
    }
    private sealed class Services(BaseCreature actor,BaseCreature target,int initialLife,int initialMana,int initialStamina,int expectedActor,int expectedLife,int expectedMana,int expectedStamina):PreparedMonsterDamageServices
    {
        internal readonly List<string> Trace=[];internal bool Admitted=true;
        internal override bool OrdinaryNpcFlatDrains=>Admitted;
        internal override void StoreLastAttacker()=>Trace.Add("last");
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p){Trace.Add("dealt");return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p){Trace.Add("took");return p;}
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {Trace.Add("before");Require(SkillHealth.Balance256(target)==initialLife&&NecromancerEffects.ManaBalance256(target)==initialMana&&BattleOrders.Stamina256(target)==initialStamina,"Drains applied before their source phase");}
        internal override void BeforeHealthCommit(Mobile source,BaseCreature victim,int after)
        {
            Trace.Add("commit");Require(SkillHealth.Balance256(actor)==expectedActor&&SkillHealth.Balance256(target)==initialLife&&NecromancerEffects.ManaBalance256(target)==initialMana&&BattleOrders.Stamina256(target)==initialStamina,"NPC recovery must precede HP and resource loss");
            Require(after==expectedLife,"Incorrect direct HP including NPC life-drain lane");
        }
        internal override void ApplyCold(int frames)
        {Trace.Add("status");Require(SkillHealth.Balance256(actor)==expectedActor&&SkillHealth.Balance256(target)==expectedLife&&NecromancerEffects.ManaBalance256(target)==expectedMana&&BattleOrders.Stamina256(target)==expectedStamina,"Mana/stamina must commit after HP and before status/death");}
        internal override void SurvivedDamage()=>Trace.Add("survived");
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p){Trace.Add("killed");return p;}
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p){Trace.Add("kill");return p;}
        internal override void PresentOverlay(int value){}
        internal override int BaseHitClass()=>3;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p){Trace.Add("attempt");return p;}
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p){Trace.Add("attacked");return p;}
        internal override bool AttackerInActiveMode()=>true;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death){Trace.Add("return");if((p.ResultFlags&2)!=0){death();}}
        internal override void FreePacket()=>Trace.Add("free");
    }

    private static void Rates()
    {
        bool Fixture(Mobile m)=>m.Name?.StartsWith("Flat drain ",StringComparison.Ordinal)==true;
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;var sp=Mobile.StamRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
        Mobile.StamRegenRateHandler=m=>Fixture(m)?TimeSpan.FromHours(1):sp?.Invoke(m)??Mobile.DefaultStamRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private sealed class DrainFill:NpcDamageFillServices
    {
        internal int Entries,Restores;
        internal override bool AttackerPresent=>true;internal override bool DefenderPresent=>true;
        internal override int ActorKind=>1;internal override bool GoodAlignment=>false;internal override bool TargetMonster=>true;
        internal override bool Demon=>false;internal override bool Undead=>false;internal override int MonsterType=>0;
        internal override int Armor{get=>0;set=>throw new InvalidOperationException("Unexpected target armor write");}
        internal override bool Blunt=>false;internal override int Hireling=>0;internal override int IntrinsicCritical=>0;
        internal override int Stat(NpcFillStat stat)=>stat switch
        {NpcFillStat.LIFEDRAINMINDAM or NpcFillStat.LIFEDRAINMAXDAM=>2,NpcFillStat.MANADRAINMINDAM or NpcFillStat.MANADRAINMAXDAM or NpcFillStat.STAMDRAINMINDAM or NpcFillStat.STAMDRAINMAXDAM=>1,NpcFillStat.STUNLENGTH=>40,_=>0};
        internal override IEnumerable<EquipmentStatValue> TypeDamage=>[];
        internal override bool TypeMatches(int a,int b)=>false;
        internal override int WeaponCritical()=>throw new InvalidOperationException("Unexpected physical critical read");
        internal override void Enter(bool ranged)=>Entries++;internal override void Restore(bool ranged)=>Restores++;
        internal override int Physical(int a,int b,int c)=>throw new InvalidOperationException("Initial physical should be retained");
        internal override int Limited(int bound)=>throw new InvalidOperationException("Constant drain range should not roll");
        internal override uint Raw()=>throw new InvalidOperationException("No critical or conversion random draw expected");
        internal override NpcFillPacket AttackedInMelee(NpcFillPacket p)=>throw new InvalidOperationException("Non-hireling fill must not use player/hireling event");
    }

    private static EncounterCreature Body(string name)
    {
        var b=new EncounterCreature{Name=name,Frozen=true,CantWalk=true};
        var profile=EncounterProfiles.All.First(p=>p.Archetype==MonsterArchetype.Tank);
        b.Assign(profile,Math.Clamp(60,profile.MinimumLevel,profile.MaximumLevel),MonsterDifficulty.Normal,EncounterGrade.Ordinary,ChampionKind.Champion,[],EliteAura.None,name,new Random(173));
        b.AIObject?.AITimer.Stop();b.SetHits(1000);b.SetMana(100);b.SetStam(100);
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){b.SetResistance(kind,0);}
        b.MoveToWorld(new(6400,2800,0),Map.Trammel);return b;
    }
    private static void NativeCases()
    {
        var actor=Body("Flat drain source [fixture]");var target=Body("Flat drain recipient [fixture]");Require(GemCombat.Drain(target)==100,"Fixture must use ordinary full drain effectiveness");
        int count=0;
        foreach(var drains in new[]{new[]{0,0,0},new[]{513,0,0},new[]{0,517,0},new[]{0,0,521},new[]{0,1,521},new[]{513,517,521}})
        foreach(int flags in new[]{1,33})
        {
            SkillHealth.SetBalance256(actor,1000);SkillHealth.SetBalance256(target,51200);NecromancerEffects.SetMana256(target,1337);BattleOrders.SetStamina256(target,1537);
            var fill=new NpcFillPacket(Physical:257,LifeDrain:drains[0],ManaDrain:drains[1],StaminaDrain:drains[2],ResultFlags:flags,HitFlags:32);
            var impact=NpcEquipmentImpact.FromFill(actor,target,fill,new(actor.Serial.Value,actor.Serial.Value,new(),0,0));
            Require(impact.Packet.Total256==257+drains[0]&&impact.Packet.LifeDrain256==drains[0]&&impact.Packet.ManaDrain256==drains[1]&&impact.Packet.StaminaDrain256==drains[2],"Filled allocation dropped or mis-scaled a drain lane");
            int expectedActor=1000+NpcFlatDrains.Healing(257,1337,1537,SkillHealth.Maximum256(actor)-1000,drains[0],drains[1],drains[2],100);
            int expectedLife=51200-impact.Packet.Total256,expectedMana=NpcFlatDrains.Remaining(1337,drains[1]),expectedStamina=NpcFlatDrains.Remaining(1537,drains[2]);
            var service=new Services(actor,target,51200,1337,1537,expectedActor,expectedLife,expectedMana,expectedStamina);
            var result=PreparedMonsterDamage.Release(actor,target,impact.Packet,new(false,()=>false,()=>5,()=>false),_=>0,service,TimedEffects.Now);
            Require(result.AfterHealth256==expectedLife&&service.Trace.Last()=="free","Actual native drain release/free incomplete");
            Require(service.Trace.IndexOf("before")<service.Trace.IndexOf("commit")&&service.Trace.IndexOf("commit")<service.Trace.IndexOf("status"),"Incorrect actual drain/HP/status ordering");
            count++;
        }
        // Missing explicit receiver admission must refuse before callbacks or resources.
        var denied=new Services(actor,target,0,0,0,0,0,0,0){Admitted=false};var before=BattleOrders.Capture(target);bool refused=false;
        try{PreparedMonsterDamage.Release(actor,target,new(1,2,0,0,1,LifeDrain256:1),new(false,()=>false,()=>5,()=>false),_=>0,denied,TimedEffects.Now);}catch(NotSupportedException){refused=true;}
        Require(refused&&denied.Trace.Count==0&&BattleOrders.Capture(target)==before,"Unadmitted drain receiver changed callbacks/resources");
        // Retain one injured,drained original through both native reloads.
        SkillHealth.SetBalance256(actor,1000);SkillHealth.SetBalance256(target,51200);NecromancerEffects.SetMana256(target,1337);BattleOrders.SetStamina256(target,1537);
        var filler=new DrainFill();var filled=NpcDamageFill.Fill(filler,new(Physical:257,ResultFlags:1,HitFlags:1),false);
        Require(filler.Entries==1&&filler.Restores==1&&filled.LifeDrain==1024&&filled.ManaDrain==512&&filled.StaminaDrain==512&&filled.StunFrames==40,"Full filler failed to reach the native drain path");
        var final=NpcEquipmentImpact.FromFill(actor,target,filled,new(actor.Serial.Value,actor.Serial.Value,new(),0,0));
        var savedService=new Services(actor,target,51200,1337,1537,1000+1281,49919,825,1025);
        PreparedMonsterDamage.Release(actor,target,final.Packet,new(false,()=>false,()=>5,()=>false),_=>0,savedService,TimedEffects.Now);
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);CombatStuns.Restore(CombatStuns.Snapshot().Select(s=>s.Target==target.Serial.Value?s with{UntilUtc=until}:s).ToArray());
        actor.Internalize();target.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(actor.Serial.Value,target.Serial.Value,SkillHealth.Balance256(actor),SkillHealth.Balance256(target),NecromancerEffects.ManaBalance256(target),BattleOrders.Stamina256(target),CombatStuns.Snapshot().Single(s=>s.Target==target.Serial.Value),0)));
        Require(count==12,"Missing actual fill/mitigation/native drain cases");
    }
    private static void Check(bool reload)
    {
        SourceCases();
        if(!reload){NativeCases();}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var actor=World.FindMobile((Serial)s.Actor)as BaseCreature;var target=World.FindMobile((Serial)s.Target)as BaseCreature;
            Require(actor is{Alive:true,Deleted:false}&&target is{Alive:true,Deleted:false}&&SkillHealth.Balance256(actor)==s.SourceLife&&SkillHealth.Balance256(target)==s.TargetLife&&NecromancerEffects.ManaBalance256(target)==s.Mana&&BattleOrders.Stamina256(target)==s.Stamina&&CombatStuns.Snapshot().Contains(s.Stun),"Original drained resource/HP/status replayed or recovered on reload");
            File.WriteAllText(Manifest,Json(s with{Generation=s.Generation+1}));
        }
        Log.Information("NPC flat drains:11664full-source contexts;12actual full-fill/native HP/resource contacts;life/mana/stamina ordering and explicit receiver admission;original wounded pools/stun retained through two reloads");
    }
    public static void Configure()
    {
        Rates();
        foreach(string name in new[]{"npc-flat-drains-check","npc-flat-drains-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Full ordinary NPC allocation/drain receiver",_=>Core.LoopContext.Post(()=>{try{Check(name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception ex){Log.Error(ex,"{Name}: failed",name);}}));
    }
}
