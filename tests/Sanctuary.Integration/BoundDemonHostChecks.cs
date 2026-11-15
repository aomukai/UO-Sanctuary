// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class BoundDemonHostChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonHostChecks));
    private sealed record Saved(int Generation,uint Owner,uint Actor,uint Weapon,uint Loot,BoundDemonState State,int Life,int Maximum,int Mana,int Stamina,int Ai,double Magery);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-host-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static bool Creating;
    private static void Rates()
    {var hits=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundDemonHostFixture"?TimeSpan.FromHours(1):hits?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-host-reload-check":"bound-demon-host-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared original living demon identity,stats,pool,parking and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static T Body<T>(PlayerMobile p,int offset)where T:BaseCreature,new()
    {
        T body;Creating=true;try{body=new T();}finally{Creating=false;}
        body.Name="BoundDemonHostFixture";body.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);body.AIObject?.AITimer.Stop();return body;
    }
    private static void Ratio(BaseCreature b,int before,int maximum,string why)
    {Require(Math.Abs(SkillHealth.Balance256(b)-(long)before*SkillHealth.Maximum256(b)/maximum)<=1,why);}
    private static void Bind(PlayerMobile p,BaseCreature body)
    {
        var ai=body.AIObject;int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(body),maximum=SkillHealth.Maximum256(body);var serial=body.Serial;
        Require(BoundDemonHosts.BindPrepared(p,body,out var issue),"Original binding refused: "+issue);
        Require(body.Serial==serial&&ReferenceEquals(body,World.FindMobile(serial))&&ReferenceEquals(ai,body.AIObject)
            &&body.ControlMaster==p&&body.Controlled&&!body.Summoned&&!body.Commandable&&ClassCombatHooks.PersistentMinion(body)
            &&body.ControlOrder==OrderType.Guard&&NecromancerEffects.ManaBalance256(p)==mana,"Binding replaced original AI/body,opened native pet commands or spent channel mana");
        Ratio(body,life,maximum,"Binding healed/reset original wounds");body.AIObject?.AITimer.Stop();
    }
    private static void LegacyBaseline(PlayerMobile p)
    {
        var old=new Daemon{Name="BoundDemonHostFixture"};
        try
        {
            MonsterVariants.Assign(old,MonsterDifficulty.Hell);old.MoveToWorld(new(p.X+18,p.Y,p.Z),p.Map);old.AIObject?.AITimer.Stop();
            var snapshots=MonsterVariantBaselines.Snapshot();var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(snapshots));
            foreach(var row in json.AsArray()){if(row["Actor"].GetValue<uint>()==old.Serial.Value){row.AsObject().Remove("Life256");}}
            MonsterVariantBaselines.Restore(JsonSerializer.Deserialize<MonsterNativeCombat[]>(json.ToJsonString()));
            Require(MonsterVariantBaselines.Find(old)is{Life256:null},"Old baseline JSON omission was not accepted");
            int life=SkillHealth.Balance256(old);Require(!BoundDemonHosts.BindPrepared(p,old,out var issue)&&issue.Contains("Normal life baseline")
                &&!old.Controlled&&SkillHealth.Balance256(old)==life,"Unknown old Normal life was invented or failed bind mutated original");
            MonsterVariantBaselines.Restore(snapshots);
            Log.Information("Bound host:old baseline JSON without Life256 loads;unknown higher-born Normal life refuses before ownership/resources and does not alter existing actor");
        }
        finally{old.Delete();}
    }
    private static void Scene(NetState network)
    {
        Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.AddItem(new Backpack());p.Female=true;ClassSkillChecks.Train(p,"warlock.binddemon",1);ClassSkillChecks.Train(p,"warlock.demonicmastery",20);ClassSkillChecks.Train(p,"warlock.bloodoath",20);
        FixtureSpace.Corridor(p,6900,40,3300);var account=FixtureNetwork.Authenticate(network,p,"bound-demon-host-");var facets=SummonDifficulties.Facets();GuardedRegion town=null;bool kept=false;string phase="setup";
        PlayerMobile orders=null;BaseCreature first=null,body=null;GraveyardZombie enemy=null;BaseCreature refused=null;
        try
        {
            PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);Require(p.AccessLevel==AccessLevel.Player&&!WarlockCombat.Implemented(CombatAbility.BindDemon),"Prepared fixture opened learned route/GM bypass");
            orders=ClassSkillChecks.Player(DiabloClass.Barbarian);
            LegacyBaseline(p);
            refused=Body<Zombie>(p,12);var refusePoint=refused.Location;
            Require(!BoundDemonHosts.BindPrepared(p,refused,out _)&&!refused.Controlled&&refused.Location==refusePoint,"Non-demon preflight mutated native body");refused.Delete();
            refused=Body<Imp>(p,12);refused.ChaosDamage=5;int nativeMinimum=refused.DamageMin;Require(!BoundDemonHosts.BindPrepared(p,refused,out var typedIssue)&&typedIssue.Contains("Unsupported native damage channels")&&!refused.Controlled&&refused.DamageMin==nativeMinimum,"Unqualified native chaos damage was silently changed");refused.Delete();
            refused=Body<Gargoyle>(p,12);Require(!BoundDemonHosts.BindPrepared(p,refused,out var mapIssue)&&mapIssue.Contains("No pinned Bind Demon")&&!refused.Controlled,"Nonbinding reward analogue was silently remapped");refused.Delete();
            var goat=WarlockGoatmanWorld.Create(p,new(p.X+3,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman);WarlockGoatmanWorld.Admit(goat);
            var tainted=WarlockGoatmanWorld.Create(p,new(p.X+6,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted);WarlockGoatmanWorld.Admit(tainted);
            phase="first binding";first=Body<Daemon>(p,12);SkillHealth.SetBalance256(first,SkillHealth.Maximum256(first)/2-13);Bind(p,first);
            var state=BoundDemonHosts.State(first);Require(state.Birth is{Rank:2,HardRank:1,MasteryRank:21,HardMastery:20,OathRank:21,AttackSpeed:25,AttackBonus:625,DamagePercent:290,Armor:105,FlatDamage:75},"Actual gear/captured host supplier context");
            Require(ClassGearHooks.AttackDelay(first,TimeSpan.FromSeconds(1))==TimeSpan.FromMilliseconds(800)
                &&Math.Abs(ClassGearHooks.MovementDelay(first,1)-1/1.33)<.000001,"Actual native movement/attack hook did not consume captured skill speed");
            Require(CreatureProfiles.AttackRating(first)==CreatureProfiles.NativeAttack(first)+625&&CreatureProfiles.Defense(first)==CreatureProfiles.NativeDefense(first)+105,"Actual native AR/defense consumers");
            enemy=new GraveyardZombie{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.MoveToWorld(new(p.X+14,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();enemy.SetResistance(ResistanceType.Physical,0);
            phase="native combat";var packet=SkillCombat.ResistedDamage(enemy,first,25,100,0,0,0,0,false,true)??throw new InvalidOperationException("Bound original packet was not admitted");
            int before=SkillHealth.Balance256(enemy);SkillHealth.Damage(first,enemy,packet.Total256??throw new InvalidOperationException("Missing exact native damage"));
            Require(packet.Total256==390*256&&before-SkillHealth.Balance256(enemy)==packet.Total256,"Actual native physical multiplier/health commitment");
            Require(NamedElements.Resistance(enemy,first,"fire",first.FireResistance)==first.FireResistance+67
                &&NamedElements.Resistance(enemy,first,"physical",first.PhysicalResistance)==first.PhysicalResistance+10,"Actual bound summon/physical resistance pools");
            Require(ClassCombatHooks.GuardTarget(first,enemy)==true&&ClassCombatHooks.GuardTarget(first,p)==false&&!first.AIObject.HandlesOnSpeech(p),"Native class-minion guard/speech integration");
            p.DoHarmful(enemy,true);p.Combatant=enemy;first.AIObject.DoOrderGuard();
            Require(first.Combatant==enemy&&first.ControlOrder==OrderType.Guard,"Retained native AI did not acquire its owner's enemy");
            PersistentMinions.Suspend(first);Require(first.Spell==null&&first.Map==Map.Internal,"Native suspend left an in-progress spell/combat action active");
            PersistentMinions.Resume(p);Require(BoundDemonHosts.CanAct(first)&&ReferenceEquals(first,World.FindMobile(first.Serial)),"Native AI body failed resume");first.AIObject?.AITimer.Stop();p.Combatant=null;
            var defiler=WarlockGoatmanWorld.Create(p,new(p.X+9,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonDefiler);WarlockGoatmanWorld.Admit(defiler);WarlockGoatmanWorld.Enforce(p);
            Require(goat.Deleted&&!tainted.Deleted&&!first.Deleted&&!defiler.Deleted,"Bound host was omitted from shared demon FIFO");
            BattleOrders.Apply(orders,p,1,TimedEffects.Now);Require(BattleOrders.Percent(p)==35&&ClassResources.Parts(p,0).SkillPercent==35,"Positive Barbarian Battle Orders control");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-13);int ownerLife=SkillHealth.Balance256(p),ownerMaximum=SkillHealth.Maximum256(p);
            var consumed=ConsumeCasting.ReleasePrepared(p,tainted,TimedEffects.Now);
            Require(consumed is{Consumed:true,Fault:null,Effect.Suppliers.LifePercent:6}&&ClassResources.Parts(p,0).SkillPercent==41
                &&Math.Abs(SkillHealth.Balance256(p)-(long)ownerLife*SkillHealth.Maximum256(p)/ownerMaximum)<=1,"Actual positive Battle Orders35+Consume6 additive life/ratio");
            ConsumeEffects.Clear(p);BattleOrders.Clear(p);Log.Information("Bound host:positive actual Barbarian Battle Orders35plus Consume6joins one41percent life pool;earlier Warlock-supplier control was zero rank,not evidence of positive overlap");
            phase="replacement inventory";body=Body<Daemon>(p,16);body.SetHits(25000);SkillHealth.SetBalance256(body,25000*128-513);var weapon=new Dagger();body.AddItem(weapon);var loot=new Gold(37);if(body.Backpack==null){body.AddItem(new Backpack());}body.Backpack.DropItem(loot);int ai=(int)body.AI;double magery=body.Skills[SkillName.Magery].Base;
            phase="second binding";Bind(p,body);Require(first.Deleted&&BoundDemonHosts.For(p).Length==1&&SkillHealth.Maximum256(body)==20000*256
                &&weapon.Parent==body&&loot.Parent==body.Backpack&&body.Skills[SkillName.Magery].Base==magery,"One-bound replacement,life cap,original equipment/loot/ability fields");
            int beforeOrders=SkillHealth.Balance256(body),beforeOrdersMax=SkillHealth.Maximum256(body);BattleOrders.Apply(orders,body,1,TimedEffects.Now);
            Require(BattleOrders.Percent(body)==35&&SkillHealth.Maximum256(body)==27000*256,"Bound body ignored existing Battle Orders maximum-life pool");Ratio(body,beforeOrders,beforeOrdersMax,"Battle Orders reset bound wounds");
            beforeOrders=SkillHealth.Balance256(body);beforeOrdersMax=SkillHealth.Maximum256(body);BattleOrders.Clear(body);Ratio(body,beforeOrders,beforeOrdersMax,"Battle Orders removal lost bound wound percentage");
            first=null;var birth=BoundDemonHosts.State(body);var captured=birth.Birth;
            phase="facet change";int life=SkillHealth.Balance256(body),maximum=SkillHealth.Maximum256(body);SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,MonsterDifficulty.Hell)).ToArray());p.Location=new(p.X,p.Y+1,p.Z);
            Require(BoundDemonHosts.State(body)is{Difficulty:MonsterDifficulty.Hell} hell&&hell.Birth==captured&&hell.Order==birth.Order,"Facet change rewrote original bound snapshot/order");Ratio(body,life,maximum,"Facet rescale changed wound percentage");
            life=SkillHealth.Balance256(body);maximum=SkillHealth.Maximum256(body);SummonDifficulties.SetFacets(facets);p.Location=new(p.X,p.Y-1,p.Z);Require(BoundDemonHosts.State(body).Difficulty==MonsterDifficulty.Normal&&SkillHealth.Maximum256(body)==20000*256,"Normal bound baseline/cap was not restored");Ratio(body,life,maximum,"Returning Normal healed original");
            var intact=BoundDemonHosts.SaveSnapshot();bool invalid=false;try{BoundDemonHosts.Restore([intact[0]with{Order=0}]);}catch(InvalidDataException){invalid=true;}
            Require(invalid&&BoundDemonHosts.SaveSnapshot().SequenceEqual(intact),"Invalid bound host partially replaced ledger");
            phase="town parking";town=new GuardedRegion("BoundDemonTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(p.X+18,p.Y-2,6,5));town.Register();life=SkillHealth.Balance256(body);p.Location=new(p.X+20,p.Y,p.Z);
            Require(MinionSafeAreas.Parked(body)&&body.Blessed&&!ClassGearHooks.CanRegenerateHits(body)&&!ClassGearHooks.CanRegenerateMana(body)&&!ClassGearHooks.CanRegenerateStamina(body)
                &&SkillHealth.Balance256(body)==life&&weapon.Parent==body&&loot.Parent==body.Backpack,"Town parking granted recovery/reset or dropped original items");
            p.Location=new(p.X-20,p.Y,p.Z);Require(body.Map==p.Map&&!body.Blessed&&BoundDemonHosts.CanAct(body)&&SkillHealth.Balance256(body)==life,"Leaving town failed original resume/wounds");town.Unregister();town=null;
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);body.Mana=Math.Max(0,body.ManaMax-3);body.Stam=Math.Max(0,body.StamMax-5);PersistentMinions.Suspend(p);
            Require(body.Map==Map.Internal&&!BoundDemonHosts.CanAct(body)&&!ClassGearHooks.CanRegenerateHits(body),"Offline original combat/recovery remained active");
            // The preceding physical-only prototype stored this derived flat
            // bonus in native endpoints. Reload must discard those old fields
            // and rebuild the original base before adding packet damage once.
            var legacy=BoundDemonHosts.State(body);body.SetDamage(legacy.Normal.Minimum+legacy.Birth.FlatDamage,legacy.Normal.Maximum+legacy.Birth.FlatDamage);
            phase="save";File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,weapon.Serial.Value,loot.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),SkillHealth.Maximum256(body),body.Mana,body.Stam,ai,magery)));kept=true;
            Log.Information("Bound host:original Daemon identity and actual native guard acquisition/stop/resume,real gear snapshot/native AR/armor/damage/speed/resist consumers,health commitment,one-bound/shared FIFO,source life cap,facet wound ratios,town/offline parking and atomic restore pass;channel/chance/modifier/underling delivery remain gated");
        }
        catch(Exception e){throw new InvalidOperationException("Bound host scene failed during "+phase,e);}
        finally
        {
            ConsumeEffects.Clear(p);BattleOrders.Clear(p);if(body!=null){BattleOrders.Clear(body);}orders?.Delete();
            SummonDifficulties.SetFacets(facets);town?.Unregister();enemy?.Delete();refused?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){first?.Delete();body?.Delete();foreach(var pet in p.AllFollowers?.ToArray()??[]){pet.Delete();}p.Delete();BoundDemonHosts.Tick();}
        }
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many bound host reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=World.FindMobile((Serial)saved.Actor)as Daemon;
        Require(b is{Alive:true,Deleted:false,Map:not null}&&b.Map==Map.Internal&&b.ControlMaster==p&&!b.Commandable&&BoundDemonHosts.State(b)==saved.State
            &&SkillHealth.Balance256(b)==saved.Life&&SkillHealth.Maximum256(b)==saved.Maximum&&b.Mana==saved.Mana&&b.Stam==saved.Stamina
            &&b.DamageMin==saved.State.Normal.Minimum&&b.DamageMax==saved.State.Normal.Maximum
            &&(int)b.AI==saved.Ai&&b.Skills[SkillName.Magery].Base==saved.Magery&&World.FindItem((Serial)saved.Weapon)?.Parent==b
            &&World.FindItem((Serial)saved.Loot)?.Parent==b.Backpack,"Original bound body,items,AI/skills,wounds/resources or captured state changed on reload");
        var account=FixtureNetwork.Authenticate(network,p,"bound-demon-host-reload-");
        try
        {
            PersistentMinions.Resume(p);Require(BoundDemonHosts.CanAct(b)&&b.Map==p.Map&&SkillHealth.Balance256(b)==saved.Life,"Original bound actor failed native resume");
            PersistentMinions.Suspend(p);Require(!BoundDemonHosts.CanAct(b)&&b.Map==Map.Internal,"Original bound actor failed suspend");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            if(saved.Generation==1){p.Kill();BoundDemonHosts.Tick();Require(!p.Alive&&b.Deleted&&BoundDemonHosts.For(p).Length==0,"Owner death left a hostile/orphaned bound demon");Log.Information("Bound host:terminal owner death retires the original body and removes its active roster identity");}
            Log.Information("Bound host:original Daemon serial,weapon/backpack loot,AI/Magery,captured modifiers/stats,normal baseline,exact wounds/mana/stamina and native parking retain through reload without cloning or recovery");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
