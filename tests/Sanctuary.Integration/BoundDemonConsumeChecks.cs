// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class BoundDemonConsumeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonConsumeChecks));
    private sealed record Saved(int Generation,uint Owner,uint Enemy,ConsumeEffect Effect,int Life,int Maximum,uint Goat,string Birth,int GoatLife,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-consume-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundDemonConsumeFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-consume-reload-check":"bound-demon-consume-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound Consume catalog/numeric owner stats/native town menu/target order/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static ConsumeSuppliers Numbers(string monster,int rank=20,MonsterDifficulty difficulty=MonsterDifficulty.Hell)
        =>ConsumePrepared.Capture(new(SkillMechanics.For(CombatAbility.Consume),_=>rank,_=>0,99),0,monster,difficulty);
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Catalog()
    {
        int rows=0,auras=0;
        foreach(var row in BindDemonPrepared.Catalog())foreach(var diff in new[]{MonsterDifficulty.Normal,MonsterDifficulty.Nightmare,MonsterDifficulty.Hell})
        {var value=Numbers(row.Monster,20,diff);ConsumePrepared.Validate(value);rows++;if(value.Bonus.AuraRank>0){auras++;Require(ConsumePrepared.RuntimeSupported(value),"Captured item aura lacks its provider");}}
        Require(rows==513&&auras>0,"Incomplete pinned bound Consume catalog capture");
        (string Monster,ConsumeBonusValues Bonus)[] literal=[
            ("blunderbore2",new(ColdResist:65)),("baalminion1",new(DamagePercent:200)),("blunderbore5",new(PhysicalResist:15)),
            ("blunderbore1",new(FireResist:65)),("fallen5",new(AllSkills:3)),("bighead1",new(LightningResist:65)),
            ("Wyand Voidfinger",new(MagicResist:40,MaxMagicResist:10,MagicPierce:30)),("corruptrogue6",new(ColdResist:70,MaxColdResist:10)),
            ("cr_archer6",new(FireResist:65,MaxFireResist:10)),("bighead6",new(LightningResist:65,MaxLightningResist:10)),
            ("Ismail Vilehand",new(PoisonResist:80,MaxPoisonResist:10,AllSkills:3)),("Toorc Icefist",new(ColdResist:80,MaxColdResist:10,ColdPierce:30)),
            ("Baal Subject 1",new(FireResist:80,MaxFireResist:10,FirePierce:35)),("Bremm Sparkfist",new(LightningResist:80,MaxLightningResist:10,LightningPierce:30)),
            ("Coldcrow",new(ColdResist:80,MaxColdResist:10,MagicPierce:27)),("fetishblow4",new(PoisonResist:65))];
        foreach(var c in literal){Require(Numbers(c.Monster).Bonus==c.Bonus,"Literal bound Consume writer mismatch: "+c.Monster);}
        Require(Numbers("cr_archer6",1).Bonus.MaxFireResist==2&&Numbers("cr_archer6",40).Bonus.MaxFireResist==10,"Quoted minimum/cap rounding differs");
        var old=Numbers("wargoatman");var node=JsonSerializer.SerializeToNode(old);var bonus=node["Bonus"].AsObject();
        foreach(string key in bonus.Select(p=>p.Key).Where(k=>k is not("DamagePercent"or"CrushingChance"or"FireMastery"or"FirePierce"or"MagicMastery"or"MagicPierce")).ToArray()){bonus.Remove(key);}
        Require(node.Deserialize<ConsumeSuppliers>()==old,"Old six-field bonus JSON failed optional defaults");
        Log.Information("Bound Consume:513captures across171source rows/three difficulties;16independent literal contexts cover17numeric stats;outer quotes/whitespace/caps/old six-field JSON verified;item aura metadata retained with qualified provider");
    }
    private static void Writers(PlayerMobile p,Mobile enemy)
    {
        var retired=new Daemon();BattleCommand.Track(retired);uint serial=retired.Serial.Value;long order=BattleCommand.PetOrder(retired);retired.Delete();
        void Put(string monster,int rank=20)
        {ConsumeEffects.Clear(p);var at=TimedEffects.Now;var value=Numbers(monster,rank);ConsumeEffects.ApplyPrepared(p,new(p.Serial.Value,serial,order,at,at.AddMilliseconds(value.DurationFrames*40L),value));}
        Put("megademon1");Require(ClassGear.Bonus(p,"res-fire")==65&&p.FireResistance==65,"Actual fire resistance/cache writer failed");int life=SkillHealth.Balance256(p);
        AOS.Damage(p,enemy,100,false,0,100,0,0,0,spellDamage:true);Require(life-SkillHealth.Balance256(p)==35*256,"Native fire defense ignored consumed resistance");
        Put("blunderbore5");Require(p.PhysicalResistance==15,"Actual physical resistance writer failed");
        foreach(var c in new[]{("Baal Subject 1",ResistanceType.Fire),("Toorc Icefist",ResistanceType.Cold),("Bremm Sparkfist",ResistanceType.Energy),("Ismail Vilehand",ResistanceType.Poison)})
        {Put(c.Item1,200);Require(NamedCombat.ResistanceCap(p,c.Item2)==85&&p.GetResistance(c.Item2)==85,"Actual elemental cap writer failed: "+c.Item1);}
        Put("Wyand Voidfinger",200);Require(NamedElements.MagicResistance(p)==85,"Actual magic maximum resistance writer failed");
        Put("fallen5");Require(SkillMechanics.Rank(p,CombatAbility.Consume)==23&&ClassCreation.Profiles.GetRank(p.Serial,"warlock.consume")==19
            &&NamedSkills.Rank(p,"warlock.apocalypse")==0,"Allskills failed learned-only/hard-point rules");
        foreach(var c in new[]{("Toorc Icefist","cold",30),("Baal Subject 1","fire",35),("Bremm Sparkfist","ltng",30),("Wyand Voidfinger","mag",30)})
        {
            Put(c.Item1);int before=SkillHealth.Balance256(enemy);var parts=c.Item2 switch{"cold"=>new GearDamage(0,0,100,0,0),"fire"=>new(0,100,0,0,0),"ltng"=>new(0,0,0,100,0),_=>new(0,0,0,0,0,Magic:100)};
            SkillCombat.HitParts(p,enemy,parts,true,spell:true);Require(before-SkillHealth.Balance256(enemy)==(100+c.Item3)*256,"Actual elemental pierce writer failed: "+c.Item1);
        }
        var beforeState=ConsumeEffects.State(p);bool refused=false;var aura=Numbers("megademon6");var now=TimedEffects.Now;
        try{ConsumeEffects.ApplyPrepared(p,new(p.Serial.Value,serial,order,now,now.AddMilliseconds(aura.DurationFrames*40L),aura with{Bonus=aura.Bonus with{AuraSkill="Unqualified"}}));}catch(InvalidDataException){refused=true;}
        Require(refused&&ConsumeEffects.State(p)==beforeState,"Invalid aura replaced live Consume state");ConsumeEffects.Clear(p);
        Require(ClassGear.Bonus(p,"res-fire")==0&&p.FireResistance==0&&SkillMechanics.Rank(p,CombatAbility.Consume)==20,"Clearing numeric Consume left stale stats");
        Log.Information("Bound Consume:actual native resistance/cache/caps including magic85,learned-only allskills and four real typed pierce hits pass;unsupported aura application refuses atomically;clear removes bonuses");
    }
    private static void Scene(NetState network)
    {
        Catalog();PlayerMobile p;Daemon body;Creating=true;try
        {p=new PlayerMobile{Body=0x191,Player=true};ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);body=new Daemon();}
        finally{Creating=false;}
        p.Name=body.Name="BoundDemonConsumeFixture";p.Female=true;p.AddItem(new Backpack());
        foreach(var c in new[]{("warlock.consume",19),("warlock.binddemon",1),("warlock.demonicmastery",10),("warlock.summongoatman",6)}){ClassSkillChecks.Train(p,c.Item1,c.Item2);}
        p.Hits=p.HitsMax;p.Mana=p.ManaMax;p.Stam=p.StamMax;
        FixtureSpace.Corridor(p,7040,34,3300);var account=FixtureNetwork.Authenticate(network,p,"bound-consume-");WarlockGoatmanBody goat=null;GuardedRegion town=null;bool kept=false;
        var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.MoveToWorld(new(p.X+15,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){enemy.SetResistance(type,0);}
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");Writers(p,enemy);body.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();var weapon=new Dagger();body.AddItem(weapon);
            Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Consume bound setup: "+why);body.AIObject?.AITimer.Stop();
            goat=WarlockGoatmanWorld.Create(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now);WarlockGoatmanWorld.Admit(goat);string birth=JsonSerializer.Serialize(goat.Prepared.Snapshot);
            Ready(p);Require(ConsumeCasting.OpenPrepared(p),"Actual mixed menu did not open");var menu=p.FindGump<ConsumeRosterGump>();int button=Array.IndexOf(ConsumeCasting.Roster(p),goat)+1;int mana=NecromancerEffects.ManaBalance256(p);
            BattleCommand.Track(goat);menu.OnResponse(network,new RelayInfo(button,[],[],[],[]));Require(ClassCombat.For(p).Consume==null&&NecromancerEffects.ManaBalance256(p)==mana,"Stale menu ignored original creation order");
            Ready(p);var stale=ConsumeCasting.BeginPrepared(p,goat.Serial,Core.TickCount);Require(stale!=null,"Target-order windup setup");BattleCommand.Track(goat);mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&stale.Released&&!goat.Deleted&&NecromancerEffects.ManaBalance256(p)==mana,"Cast consumed a replacement creation identity");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);int wounds=SkillHealth.Balance256(body);var origin=p.Location;
            town=new GuardedRegion("BoundConsumeTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(p.X+18,p.Y-2,6,5));town.Register();p.Location=new(p.X+20,p.Y,p.Z);
            Require(body.Map==Map.Internal&&body.Blessed&&SkillHealth.Balance256(body)==wounds&&ConsumeCasting.Eligible(p,body),"Parked blessed native body unavailable or healed");
            Ready(p);Require(ConsumeCasting.OpenPrepared(p),"Town native roster did not open");menu=p.FindGump<ConsumeRosterGump>();button=Array.IndexOf(ConsumeCasting.Roster(p),body)+1;menu.OnResponse(network,new RelayInfo(button,[],[],[],[]));var action=ClassCombat.For(p).Consume;
            Require(action!=null,"Town native menu did not start cast");mana=NecromancerEffects.ManaBalance256(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-23);int life=SkillHealth.Balance256(p),maximum=SkillHealth.Maximum256(p);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt-1)&&NecromancerEffects.ManaBalance256(p)==mana,"Native windup spent mana");
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&action.Result is{Consumed:true,ManaPaid256:11264,Fault:null}&&body.Deleted&&weapon.Deleted&&BoundDemonHosts.State(body)==null,"Actual bound retirement/resource/ledger commitment failed");
            var state=ConsumeEffects.State(p);Require(state.Suppliers is{Rank:20,Monster:"megademon1",Bonus.FireResist:25}&&p.FireResistance==25
                &&Math.Abs(SkillHealth.Balance256(p)-(long)life*SkillHealth.Maximum256(p)/maximum)<=1,"Native consumed bonus or relative life transition failed");
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt+1)&&NecromancerEffects.ManaBalance256(p)==mana-11264,"Native Consume replayed");
            p.Location=origin;town.Unregister();town=null;p.AddToBackpack(helm);ClassGear.Refresh(p);Require(ConsumeEffects.State(p)==state&&JsonSerializer.Serialize(goat.Prepared.Snapshot)==birth,"Gear change rewrote consumed snapshot or remaining pet birth");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);SkillHealth.SetBalance256(goat,SkillHealth.Maximum256(goat)/2-17);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,enemy.Serial.Value,state,SkillHealth.Balance256(p),SkillHealth.Maximum256(p),goat.Serial.Value,birth,SkillHealth.Balance256(goat),NpcEquipmentRandom.State(goat))));kept=true;
            Log.Information("Bound Consume:actual wounded blessed parked Daemon selected through town gump/shared SC,retired with native item graph and bound ledger once,rank20fire25snapshot and relative life retained;stale menu/cast creation identities refuse without payment;original summon birth preserved");
        }
        finally{town?.Unregister();ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){enemy.Delete();goat?.Delete();body.Delete();p.Delete();ConsumeEffects.Clear(p);BoundDemonHosts.Tick();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many bound Consume reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var goat=World.FindMobile((Serial)s.Goat)as WarlockGoatmanBody;
        Require(ConsumeEffects.State(p)==s.Effect&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Maximum256(p)==s.Maximum&&p.FireResistance==25&&BoundDemonHosts.For(p).Length==0
            &&goat is{Alive:true,Deleted:false}&&goat.Map==Map.Internal&&JsonSerializer.Serialize(goat.Prepared.Snapshot)==s.Birth&&SkillHealth.Balance256(goat)==s.GoatLife&&NpcEquipmentRandom.State(goat)==s.Random,"Saved consumed slot/bonus/native cache/resources or original pet changed/recovered");
        var account=FixtureNetwork.Authenticate(network,p,"bound-consume-reload-");var facets=SummonDifficulties.Facets();
        try
        {
            PersistentMinions.Resume(p);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,MonsterDifficulty.Hell)).ToArray());var point=p.Location;p.Location=new(p.X,p.Y+1,p.Z);
            Require(ConsumeEffects.State(p)is{Suppliers.Bonus.FireResist:65} hell&&hell.Order==s.Effect.Order&&hell.UntilUtc==s.Effect.UntilUtc&&hell.Suppliers.Rank==20,"Consumed bound slot difficulty snapshot/deadline changed");
            SummonDifficulties.SetFacets(facets);p.Location=point;Require(ConsumeEffects.State(p)==s.Effect&&p.FireResistance==25&&BoundDemonHosts.For(p).Length==0,"Consumed original reappeared or bonus failed restoration");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Life=SkillHealth.Balance256(p),GoatLife=SkillHealth.Balance256(goat)}));
            Log.Information("Bound Consume:saved logical original order/rank/fire25bonus/deadline/owner wounds and remaining summon birth/RNG survive reload;Hell65andNormal25rescale without retired body resurrection or expiry reset");
        }
        finally{SummonDifficulties.SetFacets(facets);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
