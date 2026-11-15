// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SlowMissilesRulesChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SlowMissilesRulesChecks));
    private sealed record Saved(int Generation,uint Owner,uint Lich,uint Encounter,Point3D Origin,int Mana,int Life,SlowMissilesState[] States);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"slow-missiles-rules-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("SlowRules",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("SlowRules",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        // Reapply test-only idle controls before ordinary post-load AI can attack saved actors.
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));foreach(uint id in new[]{saved.Lich,saved.Encounter}){if(World.FindMobile((Serial)id)is BaseCreature t){t.Frozen=t.CantWalk=true;t.Combatant=null;t.AIObject?.AITimer.Stop();}}}};
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"slow-missiles-rules-reload-check":"slow-missiles-rules-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete proposed Slow Missiles native target/current producer rule",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,80*256+17);}
    private static void Cast(PlayerMobile p)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(AmazonSlowMissilesCasting.BeginCandidate(p,Core.TickCount),"Candidate SC startup");var s=ClassCombat.For(p);
        Require(s.SlowMissilesCast is{NativeTargets:true}&&NecromancerEffects.ManaBalance256(p)==mana&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Unpaid candidate windup");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-1280,"One live-rank release/payment");
    }
    private static void Scene(NetState network)
    {
        // This delivery check must not randomly Avoid its single incoming bolt.
        // The shared all-skills helper trains Avoid; create only this skill and its prerequisites.
        var p=new PlayerMobile{Name="SlowRulesFixture",Player=true,Body=0x191,Female=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.slowmissiles",20);
        Require(AmazonAvoidance.AvoidChance(p)==0,"Delivery fixture unexpectedly learned Avoid");FixtureSpace.Corridor(p,6200,30,3550);
        var lich=new GraveyardLich{Name="SlowRulesLich",Frozen=true,CantWalk=true,NoKillAwards=true};lich.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);lich.AIObject?.AITimer.Stop();
        var enemy=new EncounterCreature{Name="SlowRulesEncounter",Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+4,p.Y+2,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        var account=FixtureNetwork.Authenticate(network,p,"slow-rules-");bool kept=false;
        try
        {
            Require(AmazonSlowMissilesCast.Target(p,lich,27)==SlowMissilesRules.Approved&&AmazonSlowMissilesCast.Target(p,lich,27,true),"Native adapter silently changed strict target policy");
            Require(PaladinConversion.SourceRule(lich)==null,"Native Lich acquired an invented D2 species");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);Require(AmazonSlowMissilesCasting.BeginCandidate(p,Core.TickCount),"Cancellation start");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,effect)&&AmazonSlowMissiles.State(lich)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled native cast changed state/payment");
            Cast(p);Require(AmazonSlowMissiles.State(lich)is{Rank:20,VelocityPercent:33,DamagePercent:67}&&AmazonSlowMissiles.State(enemy)is{Rank:20,VelocityPercent:33,DamagePercent:67},"Released dual-stat payload missed native/encounter targets");
            ProducerPackets(p,lich,enemy);
            var at=TimedEffects.Now;var low=new SkillValues(SkillMechanics.For(CombatAbility.SlowMissiles),_=>1,_=>1,99);
            Require(!AmazonSlowMissiles.Apply(p,lich,low.Rank,at)&&AmazonSlowMissiles.State(lich).Rank==20,"Weaker caster overwrote stronger effect");
            var state=AmazonSlowMissiles.Snapshot();string before=JsonSerializer.Serialize(state);bool refused=false;
            try{AmazonSlowMissiles.Restore(state.Select((n,i)=>i==0?n with{UntilUtc=DateTime.SpecifyKind(n.UntilUtc,DateTimeKind.Unspecified)}:n).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&JsonSerializer.Serialize(AmazonSlowMissiles.Snapshot())==before,"Non-UTC recipient clock partly committed");
            var until=TimedEffects.Now.Add(FixtureSpace.Retention);state=state.Select(n=>n.Target==lich.Serial.Value||n.Target==enemy.Serial.Value?n with{UntilUtc=until}:n).ToArray();AmazonSlowMissiles.Restore(state);
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,80*256+17);
            Ready(p);Require(AmazonSlowMissilesCasting.BeginCandidate(p,Core.TickCount),"Held unpaid candidate save");var pending=ClassCombat.For(p);pending.EffectAt+=FixtureSpace.RetentionMilliseconds;pending.NextAction+=FixtureSpace.RetentionMilliseconds;
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,lich.Serial.Value,enemy.Serial.Value,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state.Where(n=>n.Target==lich.Serial.Value||n.Target==enemy.Serial.Value).ToArray())));kept=true;
            Require(SlowMissilesRules.Approved&&ClassCombat.Available(p,CombatAbility.SlowMissiles),"Approved Slow Missiles admission missing");
            Log.Information("Slow Missiles rule:explicit native classification without D2 identity,SC/payment/cancel,strong state priority,real Lich/encounter projectile capture,independent speed/damage and atomic UTC state pass;complete rule approved");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){WeaponMissiles.Cancel(lich);EliteBolts.Cancel(enemy);AmazonSlowMissiles.Clear(lich);AmazonSlowMissiles.Clear(enemy);lich.Delete();enemy.Delete();p.Delete();}}
    }
    private static void ProducerPackets(PlayerMobile p,GraveyardLich lich,EncounterCreature enemy)
    {
        var state=AmazonSlowMissiles.State(lich);var payload=new SlowMissilePayload(state.VelocityPercent,state.DamagePercent);
        long id=WeaponMissiles.LaunchHostileFirebolt(lich,p.Location,1025,2049);var flight=WeaponMissiles.Snapshot().Flights.Single(n=>n.Id==id);
        Require(flight.Slow==payload&&flight.Damage.Elements.FireMin==686&&flight.Damage.Elements.FireMax==1372,"Actual homing-family firebolt packet did not scale raw endpoints once");
        var v=MissileVectors.VelocityFp8(p.X-lich.X,p.Y-lich.Y,SlowMissilePayload.VelocityFp8(20,0,1,0,0,true,33));
        Require((flight.Vx,flight.Vy)==v,"Actual firebolt velocity lost factory order");
        AmazonSlowMissiles.Clear(lich);Require(WeaponMissiles.Snapshot().Flights.Single(n=>n.Id==id)==flight,"Clearing state changed an already launched bolt");
        int hp=SkillHealth.Balance256(p);WeaponMissiles.Tick(flight.NextUtc.AddMilliseconds(flight.Frames*40L),onlyOwner:lich.Serial.Value);
        Require(!WeaponMissiles.Snapshot().Flights.Any(n=>n.Id==id)&&SkillHealth.Balance256(p)<hp,$"Slowed actual firebolt failed travel/impact damage: hp={hp}/{SkillHealth.Balance256(p)},player={p.Location},lich={lich.Location},maps={p.Map}/{lich.Map},enemy={lich.IsEnemy(p)},visible={lich.CanSee(p)},harmful={lich.CanBeHarmful(p,false)},remaining={JsonSerializer.Serialize(WeaponMissiles.Snapshot().Flights.Where(n=>n.Id==id))}");
        Require(AmazonSlowMissiles.Apply(p,lich,20,TimedEffects.Now),"Restore native state");EliteBolts.Lightning(enemy);var shots=EliteBolts.Inspect(enemy);
        Require(shots.Length==8&&shots.All(n=>n.Slow==payload&&n.DistancePerFrame<1),"Encounter projectile factory lost released payload/speed");EliteBolts.Cancel(enemy);
        var raw=new GearDamage(20,30,40,50,60,1600,2400,70);var scaled=EliteCombat.ScaleMissile(raw,payload);
        Require(scaled.Physical==13.3984375&&scaled.Fire==20.09765625&&scaled.Lightning==33.5&&scaled.Cold==26.796875&&scaled.Magic==46.8984375&&scaled.Poison==40.15625&&scaled.ColdMs==raw.ColdMs&&scaled.PoisonMs==raw.PoisonMs,"Six-channel final packet/unchanged lengths");
        Require(AmazonSlowMissiles.Capture(enemy,false)==new SlowMissilePayload(100,67),"CanSlow0 suppressed damage reduction");
        WeaponMissiles.Cancel(lich);
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var lich=(GraveyardLich)World.FindMobile((Serial)saved.Lich);var enemy=(EncounterCreature)World.FindMobile((Serial)saved.Encounter);var account=FixtureNetwork.Authenticate(network,p,"slow-rules-reload-");
        try
        {
            Require(saved.States.All(n=>AmazonSlowMissiles.State(World.FindMobile((Serial)n.Target))==n)&&p.Female&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life
                &&ClassCombat.For(p).SlowMissilesCast==null,"Saved native states/Witch/resources or transient replay");FixtureSpace.Place(p,saved.Origin,Map.Trammel);lich.AIObject?.AITimer.Stop();enemy.AIObject?.AITimer.Stop();
            ProducerPackets(p,lich,enemy);AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Where(n=>n.Target!=lich.Serial.Value&&n.Target!=enemy.Serial.Value).Concat(saved.States).ToArray());
            SkillHealth.SetBalance256(p,saved.Life);NecromancerEffects.SetMana256(p,saved.Mana);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Slow Missiles rule:original native/encounter recipient payloads and UTC expiry,Amazon identity/fractional wounds+mana retain through actual reload;real projectile factories still consume them without replaying unpaid cast");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
