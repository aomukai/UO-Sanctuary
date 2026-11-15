// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoundDemonAuraChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonAuraChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint Weapon,uint Enemy,BoundDemonState State,int Life,int Mana,int Stamina,int OwnerLife);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-aura-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundAuraFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-aura-reload-check":"bound-demon-aura-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound captured aura30mapping/shared NPC providers/old JSON/lifecycle/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static Daemon Bind(PlayerMobile p)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}b.Name="BoundAuraFixture";b.SetDamage(0,0);b.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();b.AddItem(new Dagger());
        Require(BoundDemonHosts.BindPrepared(p,b,out var why),"Aura original bind: "+why);b.AIObject.AITimer.Stop();b.Mana=0;return b;
    }
    private static DateTime Due(BoundDemonAuraGrant g)=>SkillMechanics.For(g.Ability).Number("immediate")!=0?g.BornUtc:g.BornUtc.AddMilliseconds(Math.Max(5,(int)BoundDemonAuras.Values(g).Eval("perdelay"))*40L);
    private static void ClearLeases(PlayerMobile p,BaseCreature b){PaladinAuras.Clear(p);if(b!=null){PaladinAuras.Clear(b);} }
    private static void Pulse(PlayerMobile p,Daemon body,BaseCreature target,BaseCreature outside)
    {
        var state=BoundDemonHosts.State(body);var grant=state.Aura;var v=BoundDemonAuras.Values(grant);var due=Due(grant);var ability=grant.Ability;
        NecromancerEffects.SetMana256(p,0);body.Mana=0;int hp=SkillHealth.Balance256(target),far=SkillHealth.Balance256(outside);var selected=PaladinAuras.Selected(p);
        if(SkillMechanics.For(ability).Number("immediate")==0)
        {BoundDemonAuras.Tick(body,due.AddTicks(-1));Require(PaladinAuras.Effect(body,ability)==null&&SkillHealth.Balance256(target)==hp,"Delayed NPC aura emitted early");}
        BoundDemonAuras.Tick(body,due);var self=PaladinAuras.Effect(body,ability);var ally=PaladinAuras.Effect(p,ability);
        Require(self!=null&&self.Rank==grant.Rank&&self.ProviderOwner==body.Serial.Value&&self.ProviderOrder==state.Order&&self.Source==body.Serial.Value
            &&PaladinAuras.Selected(body)==CombatAbility.None&&PaladinAuras.Selected(p)==selected&&NecromancerEffects.ManaBalance256(p)==0&&body.Mana==0,"NPC fixed rank/lineage/zero upkeep/independent selection failed");
        if(ability==CombatAbility.HolyFreeze)
        {
            double radius=SkillGeometry.Tiles(v.Eval("aurarangecalc"));int expected=(int)Math.Floor(v.Damage256(false)*PaladinElements.Proximity(body.GetDistanceToSqrt(target),radius));
            Require(hp-SkillHealth.Balance256(target)==expected&&SkillHealth.Balance256(outside)==far&&PaladinHolyFreeze.Effect(target)?.Source==body.Serial.Value&&PaladinHolyFreeze.VelocitySlow(target)>0,"NPC centered HolyFreeze damage/slow/source/range differs");
            var range=PaladinElements.WeaponRange(body,ability,v);Require(self.WeaponMin==range.Min&&self.WeaponMax==range.Max,"NPC HolyFreeze weapon values differ");
            hp=SkillHealth.Balance256(target);BoundDemonAuras.Tick(body,due);Require(SkillHealth.Balance256(target)==hp,"Same NPC pulse replayed");
            BoundDemonAuras.Tick(body,due.AddSeconds(20));Require(hp-SkillHealth.Balance256(target)==expected,"Lag emitted multiple NPC aura pulses");
            target.SetResistance(ResistanceType.Cold,100);hp=SkillHealth.Balance256(target);BoundDemonAuras.Tick(body,due.AddSeconds(22));Require(SkillHealth.Balance256(target)==hp&&PaladinHolyFreeze.VelocitySlow(target)>0,"HolyFreeze ignored immunity or lost its independent slow");target.SetResistance(ResistanceType.Cold,0);
            var hit=ClassCombatHooks.SkillResistedDamage;ClassSkillDamage? captured=null;var roll=BoundDemonUpgrades.Roll;BoundDemonUpgrades.Roll=_=>0;
            ClassCombatHooks.SkillResistedDamage=(to,from,damage,physical,fire,cold,poison,lightning,spell,weapon,ranged)=>
            {var result=hit(to,from,damage,physical,fire,cold,poison,lightning,spell,weapon,ranged);if(from==body&&to==target){captured=result;}return result;};
            try{using var scope=ClassCombatHooks.MeleeAttempt(body,target,false);AOS.Damage(target,body,100,false,100,0,0,0,0,weaponDamage:true);Require(captured is{} packet&&packet.Total256-packet.Physical256>=range.Min*256&&packet.Total256-packet.Physical256<=range.Max*256,"Actual native weapon lost HolyFreeze cold payload");}
            finally{ClassCombatHooks.SkillResistedDamage=hit;BoundDemonUpgrades.Roll=roll;}
        }
        else
        {
            Require(ally!=null&&ally.Rank==grant.Rank&&ally.ProviderOwner==body.Serial.Value&&ally.ProviderOrder==state.Order&&ally.Source==body.Serial.Value,"NPC ally reception/lineage failed");
            if(ability==CombatAbility.Concentration){Require(self.Percent==(int)v.Eval("aurastatcalc1")&&ally.Percent==self.Percent&&self.UninterruptChance==(int)v.Eval("aurastatcalc2"),"NPC Concentration payload differs");}
            if(ability==CombatAbility.Vigor){Require(self.StaminaPercent==(int)v.Eval("aurastatcalc2")&&self.VelocityPercent==(int)v.Eval("aurastatcalc3")&&PaladinVigor.VelocityPercent(p)==self.VelocityPercent,"NPC Vigor payload differs");}
            if(ability==CombatAbility.Thorns){Require(PaladinThorns.FlatDamage(body)==self.Percent&&PaladinThorns.ReturnedPercent(body,self.DamagePercent)==self.DamagePercent&&ally.DamagePercent==(int)v.Eval("aurastatcalc2"),"NPC Thorns return payload differs");}
            if(ability==CombatAbility.Fanaticism){Require(self.DamagePercent==(int)v.Eval("passivecalc1")&&ally.DamagePercent==(int)v.Eval("aurastatcalc3")&&self.AttackPercent==(int)v.Eval("aurastatcalc2")&&self.Percent==(int)v.Eval("aurastatcalc1"),"Zero-mana NPC Fanaticism lost full-self/half-allied damage or AR/IAS");}
            var old=self;BoundDemonAuras.Tick(body,due);Require(PaladinAuras.Effect(body,ability)==old,"Immediate NPC pulse replayed in same frame");
        }
    }
    private static void StateChecks(PlayerMobile p,Daemon body)
    {
        var rows=BoundDemonHosts.SaveSnapshot().Select(s=>s with{SavedLife256=null}).ToArray();var state=BoundDemonHosts.State(body);var grant=state.Aura;
        string before=JsonSerializer.Serialize(BoundDemonHosts.SaveSnapshot()),auras=JsonSerializer.Serialize(PaladinAuras.Snapshot());bool refused=false;
        try{BoundDemonHosts.Restore(rows.Select(s=>s.Actor==state.Actor?s with{Aura=grant with{Rank=grant.Rank+1}}:s).ToArray());}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(BoundDemonHosts.SaveSnapshot())==before&&JsonSerializer.Serialize(PaladinAuras.Snapshot())==auras,"Invalid grant partially replaced bound state or leases");
        var node=JsonSerializer.SerializeToNode(state).AsObject();node.Remove("Aura");var legacy=node.Deserialize<BoundDemonState>();Require(legacy.Aura==null,"Old bound JSON default failed");
        var choose=BoundDemonAuras.Choose;int rolls=0;BoundDemonAuras.Choose=_=>{rolls++;return EliteAura.Might;};
        try
        {
            BoundDemonHosts.Restore(rows.Select(s=>s.Actor==state.Actor?legacy:s).ToArray());BoundDemonAuras.Tick(body,TimedEffects.Now.AddSeconds(30));Require(rolls==0&&BoundDemonHosts.State(body).Aura==null&&PaladinAuras.Effect(body,grant.Ability)==null,"Old missing grant silently rolled/adopted an aura");
            BoundDemonHosts.Restore(rows);BoundDemonAuras.Tick(body,TimedEffects.Now);Require(rolls==0&&BoundDemonHosts.State(body).Aura==grant,"Restore rerolled captured grant");
        }
        finally{BoundDemonAuras.Choose=choose;}
        var original=PaladinAuras.Snapshot();var strong=new PaladinAuraLease(body.Serial.Value,p.Serial.Value,grant.Ability,grant.Rank+1,99,TimedEffects.Now.AddSeconds(5),DamagePercent:999);
        PaladinAuras.Restore(original with{Leases=original.Leases.Where(l=>l.Target!=body.Serial.Value||l.Ability!=grant.Ability).Append(strong).ToArray()});
        PaladinAuras.PulseBoundNpc(body,p,BoundDemonAuras.Values(grant),TimedEffects.Now,state.Order);BoundDemonAuras.Stop(body,state.Order);Require(PaladinAuras.Effect(body,grant.Ability)==strong,"Weaker NPC provider/cleanup erased stronger ordinary lease");
        var equal=strong with{Rank=grant.Rank};PaladinAuras.Restore(original with{Leases=original.Leases.Where(l=>l.Target!=body.Serial.Value||l.Ability!=grant.Ability).Append(equal).ToArray()});
        BoundDemonAuras.Stop(body,state.Order);Require(PaladinAuras.Effect(body,grant.Ability)==equal,"Cleanup erased ordinary equal lease");PaladinAuras.Clear(body);BoundDemonAuras.Tick(body,TimedEffects.Now);
        Log.Information("Bound aura:malformed grant refuses atomically;old missing-grant JSON never rerolls;capture survives restore;synthetic ordinary stronger/equal leases survive provider cleanup");
    }
    private static void Command(PlayerMobile owner,Daemon body)
    {
        var state=BoundDemonHosts.State(body);var grant=state.Aura;var barb=ClassSkillChecks.Player(DiabloClass.Barbarian);barb.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        try
        {
            BattleCommand.Apply(barb,body,1,TimedEffects.Now);var at=TimedEffects.Now.AddMinutes(2);BoundDemonAuras.Tick(body,at);
            Require(BoundDemonHosts.State(body)==state&&BoundDemonAuras.Values(grant,body).Rank==grant.Rank+1&&PaladinAuras.Effect(body,grant.Ability)?.Rank==grant.Rank+1,"NPC Battle Command failed shared innate getter or rewrote birth");
            BattleCommand.Clear(body);BoundDemonAuras.Tick(body,at.AddSeconds(2));Require(PaladinAuras.Effect(body,grant.Ability)?.Rank==grant.Rank+1,"Lower aura overwrote unexpired stronger lease");
            BoundDemonAuras.Tick(body,at.AddSeconds(4));Require(PaladinAuras.Effect(body,grant.Ability)?.Rank==grant.Rank&&BoundDemonHosts.State(body)==state,"Aura did not return to captured base after stronger lease expired");
            Log.Information("Bound aura:actual Barbarian BattleCommand raises shared innate aura rank without changing birth;lower rank waits for stronger lease expiry then returns to captured base");
        }
        finally{BattleCommand.Clear(body);barb.Delete();}
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}p.Name="BoundAuraFixture";p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.demonicmastery",10);ClassSkillChecks.Train(p,"warlock.binddemon",18);
        FixtureSpace.Corridor(p,7040,48,3750);var account=FixtureNetwork.Authenticate(network,p,"bound-aura-");Daemon body=null;var target=PoisonDaggerChecks.Target(p);var outside=PoisonDaggerChecks.Target(p);
        foreach(var t in new[]{target,outside}){t.SetHits(50000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Cold,0);t.AIObject.AITimer.Stop();}target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);outside.MoveToWorld(new(p.X+30,p.Y,p.Z),p.Map);
        var choose=BoundDemonAuras.Choose;var roll=PaladinElements.Roll;PaladinElements.Roll=_=>0;bool kept=false;int choices=0;
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");BoundDemonAuras.Choose=_=>{choices++;return EliteAura.Might;};body=Bind(p);Require(choices==0&&BoundDemonHosts.State(body).Aura==null,"Below rank20 allocated aura choice");ClassSkillChecks.Train(p,"warlock.binddemon",19);
            foreach(var old in new[]{EliteAura.Might,EliteAura.HolyFire,EliteAura.BlessedAim,EliteAura.HolyFreeze,EliteAura.Conviction,EliteAura.HolyShock,EliteAura.Fanaticism})
            {
                var previous=body;ClearLeases(p,body);BoundDemonAuras.Choose=_=>{choices++;return old;};int count=choices;body=Bind(p);var g=BoundDemonHosts.State(body).Aura;
                int divisor=old is EliteAura.Might or EliteAura.HolyFire?6:old==EliteAura.BlessedAim?5:old==EliteAura.HolyFreeze?7:8;
                Require(previous.Deleted&&choices==count+1&&g.Rolled==old&&g.Ability==BoundDemonAuras.Replacement(old)&&g.Rank==Math.Clamp(g.SourceLevel/divisor,1,99)&&BoundDemonHosts.State(body).Birth.Rank==20,"Actual native choice/captured level/rank/replacement differs");
                Require(!PaladinAuras.Snapshot().Leases.Any(l=>l.Target==previous.Serial.Value&&l.ProviderOrder>0),"Replacement retained old self contribution");Pulse(p,body,target,outside);
            }
            Require(choices==7,"Seven original aura choices did not map to five providers");StateChecks(p,body);Command(p,body);var state=BoundDemonHosts.State(body);p.AddToBackpack(helm);ClassSkillChecks.Train(p,"warlock.binddemon",20);BoundDemonAuras.Tick(body,TimedEffects.Now.AddSeconds(60));Require(BoundDemonHosts.State(body)==state&&choices==7,"Gear/training changed snapshot or rerolled aura");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);var facets=SummonDifficulties.Facets();try
            {foreach(var d in new[]{MonsterDifficulty.Hell,MonsterDifficulty.Normal}){int hp=SkillHealth.Balance256(body),max=SkillHealth.Maximum256(body);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,d)).ToArray());BoundDemonHosts.Sync(p);Require(BoundDemonHosts.State(body).Aura==state.Aura&&Math.Abs(SkillHealth.Balance256(body)-(long)hp*SkillHealth.Maximum256(body)/max)<=1&&choices==7,"Facet transition rewrote aura or wounds");}}
            finally{SummonDifficulties.SetFacets(facets);BoundDemonHosts.Sync(p);}
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);PersistentMinions.Suspend(p);Require(PaladinAuras.Effect(body,state.Aura.Ability)==null,"Parking retained NPC self aura");BoundDemonAuras.Tick(body,TimedEffects.Now.AddHours(1));Require(choices==7&&BoundDemonHosts.State(body).Aura==state.Aura,"Parked interval rerolled or reset grant");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,body.FindItemOnLayer(Layer.OneHanded).Serial.Value,target.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam,SkillHealth.Balance256(p))));kept=true;
            Log.Information("Bound aura:seven original choices become five fixed native providers;all stat payloads/zero upkeep/independent selection,HolyFreeze centered damage/slow/weapon/immunity/range and no replay/lag burst pass;soft20birth,gear/facet/wounds/replacement/parking retain immutable choice without rerolls");
        }
        finally{BoundDemonAuras.Choose=choose;PaladinElements.Roll=roll;network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();outside.Delete();if(!kept){body?.Delete();target.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many aura reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(Daemon)World.FindMobile((Serial)s.Body);
        Require(BoundDemonHosts.State(b)==s.State&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&b.Mana==s.Mana&&b.Stam==s.Stamina&&SkillHealth.Balance256(p)==s.OwnerLife&&b.FindItemOnLayer(Layer.OneHanded)?.Serial.Value==s.Weapon,"Saved grant/body/resources/wounds/items changed");
        var account=FixtureNetwork.Authenticate(network,p,"bound-aura-reload-");var choose=BoundDemonAuras.Choose;BoundDemonAuras.Choose=_=>throw new InvalidOperationException("Saved aura rerolled");try
        {
            NecromancerEffects.SetMana256(p,0);PersistentMinions.Resume(p);b.AIObject.AITimer.Stop();BoundDemonAuras.Tick(b,TimedEffects.Now);var g=s.State.Aura;var lease=PaladinAuras.Effect(b,g.Ability);
            Require(lease!=null&&lease.Rank==g.Rank&&lease.ProviderOwner==b.Serial.Value&&lease.ProviderOrder==s.State.Order&&PaladinAuras.Effect(p,g.Ability)?.Rank==g.Rank&&NecromancerEffects.ManaBalance256(p)==0&&BoundDemonHosts.State(b)==s.State&&SkillHealth.Balance256(b)==s.Life,"Resumed saved source failed provider/rank/allegiance/no-heal/zero upkeep");
            var at=TimedEffects.Now;BoundDemonAuras.Tick(b,at);Require(PaladinAuras.Effect(b,g.Ability)==lease,"Resume pulse replayed");PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
            Log.Information("Bound aura:original saved grant/type/serial/weapon/wounds/resources survive reload;fixed Fanaticism resumes once at zero mana from actual native body with original order and no reroll/reset");
        }finally{BoundDemonAuras.Choose=choose;network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
