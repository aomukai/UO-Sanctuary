// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;
public static class CycloneArmorChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CycloneArmorChecks));
    private sealed record Saved(uint Owner,uint Helm,CycloneArmorState State,int Mana,Point3D Origin,int Map,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cyclone-armor-fixtures.json");
    private static bool _creating;private static int _scene;
    private static void Rates(){var prior=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=x=>x is PlayerMobile && (_creating || x.Name?.StartsWith("CycloneFixture",StringComparison.Ordinal)==true)?TimeSpan.FromHours(1):prior?.Invoke(x)??Mobile.DefaultManaRate;}
    public static void Configure(){Rates();Register("cyclone-armor-check",Check);Register("cyclone-armor-reload-check",Reload);Register("cyclone-armor-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Druid Cyclone Armor casting, shared incoming shields and saved FP256 capacity",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="CycloneFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Corridor(p,6860,25,2440+40*_scene++);return p;
    }
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent),"Druid talent");
        foreach(string parent in talent.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==parent).Ability);}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank){bool learned=ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out var reason);Require(learned,"Druid train: "+reason);}
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static Item Named(PlayerMobile p,string name)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name==name).Id,99,new Random(173),true);Require(p.EquipItem(item),"Actual named gear "+name);ClassGear.Refresh(p);return item;}
    private static bool Cast(PlayerMobile p,bool held=false,Mobile hover=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var old=DruidCycloneArmor.State(p);var values=SkillMechanics.Values(p,CombatAbility.CycloneArmor);
        bool started=held?ClassCombat.ContinueCast(p,CombatAbility.CycloneArmor,hover,new(65530,65530,0),Core.TickCount):ClassCombat.Perform(p,CombatAbility.CycloneArmor,hover,new(65530,65530,0),Core.TickCount);
        Require(started,"Self cast begin");ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero && s.PendingPoint==p.Location && s.Animation==16 && NecromancerEffects.ManaBalance256(p)==mana,"Self SC windup or premature payment");
        bool success=ClassCombat.ResolveAction(p,s.EffectAt);int cost=success?values.Mana256:0;
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost && !ClassCombat.ResolveAction(p,s.EffectAt),"Once-only release/payment");
        if(!success){Require(DruidCycloneArmor.State(p)==old,"Failed lower-rank cast changed armor");}return success;
    }
    private static void Pool(PlayerMobile p,int remaining,int maximum=10240)
    {DruidCycloneArmor.Restore(DruidCycloneArmor.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Append(new(p.Serial.Value,1,remaining,maximum)).ToArray());}
    private static void Goldens()
    {
        var definition=SkillMechanics.For(CombatAbility.CycloneArmor);Require(definition.SourceId==235 && definition.Number("srvdofunc")==18 && ClassAbilities.SelfTarget(definition.Ability),"Pinned Druid/self identity");
        int count=0;
        for(int rank=1;rank<=255;rank++)foreach(int twister in new[]{0,1,20})foreach(int tornado in new[]{0,1,20})foreach(int hurricane in new[]{0,1,20})
        {
            int Hard(string key)=>key=="Twister"?twister:key=="Tornado"?tornado:key=="Hurricane"?hurricane:0;
            var v=new SkillValues(definition,key=>key==definition.SourceKey?rank:200,Hard,99);
            int expected=(40+12*(rank-1))*(100+7*(twister+tornado+hurricane))/100*256;
            Require(v.Eval("aurastatcalc1")==expected && v.Eval("aurastatcalc2")==expected && v.Mana256==(rank+4)*256 && v.Eval("auralencalc")==0,"Hard-only capacity/FP256/mana formula");count++;
        }
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cyclone-armor-source-probe.json")));int packets=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {int V(int i)=>row[i].GetInt32();if(V(0)!=0){continue;}var hit=CycloneArmorPackets.Absorb(new(V(1),V(2),V(6)),V(3),V(4),V(5));
         Require(hit.Applied==(V(7)!=0) && hit.Pool.Remaining256==V(8) && hit.Fire256==V(9) && hit.Cold256==V(10) && hit.Lightning256==V(11) && hit.Removed==(V(12)!=0) && hit.DisplayChanged==(V(14)!=0) && hit.Pool.DisplayPercent==V(15),"Complete source event comparison");packets++;}
        Require(count==6885 && packets==9072,"Incomplete formula/source coverage");
        Log.Information("Cyclone Armor:6885current formula cases/9072complete source packet comparisons passed");
    }
    private static void Casting(PlayerMobile p)
    {
        Require(!ClassCombat.Available(p,CombatAbility.CycloneArmor),"Unlearned Druid skill available");Train(p,CombatAbility.CycloneArmor);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.CycloneArmor) && SkillBindings.Instance.Assign(p,false,CombatAbility.CycloneArmor)
            && SkillBindings.Instance.AssignQuick(p,5,CombatAbility.CycloneArmor),"Self/right/quick binding rules");
        Ready(p);NecromancerEffects.SetMana256(p,1279);Require(!ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount) && DruidCycloneArmor.State(p)==null && NecromancerEffects.ManaBalance256(p)==1279,"Low mana cast spent/created armor");Mana(p);
        int mana=NecromancerEffects.ManaBalance256(p);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Cancelled start");long at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana && DruidCycloneArmor.State(p)==null,"Cancelled cast applied/paid");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Movement start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;p.Direction=Direction.East;Require(p.Move(Direction.East),"Outward step");p.Direction=Direction.West;Require(p.Move(Direction.West) && p.Location==origin && !ClassCombat.ResolveAction(p,at) && NecromancerEffects.ManaBalance256(p)==mana && DruidCycloneArmor.State(p)==null,"Return step revived cast");
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Mana-loss start");at=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);Require(!ClassCombat.ResolveAction(p,at) && DruidCycloneArmor.State(p)==null,"Mana-loss release created armor");Mana(p);
        Ready(p);Require(ClassCombat.Perform(p,CombatAbility.CycloneArmor,null,p.Location,Core.TickCount),"Recovery start");at=ClassCombat.For(p).EffectAt;mana=NecromancerEffects.ManaBalance256(p);Require(HitRecovery.Apply(p,p.HitsMax,Core.TickCount) && !ClassCombat.ResolveAction(p,at) && DruidCycloneArmor.State(p)==null && NecromancerEffects.ManaBalance256(p)==mana,"Recovery failed to cancel unpaid buff");Ready(p);
        var friend=Fresh();Require(Cast(p,true,friend) && DruidCycloneArmor.State(p) is{Rank:1,Remaining256:10240,Maximum256:10240},"Friendly hover self cast/rank1 pool");friend.Delete();
        DruidCycloneArmor.Absorb(p,new(0,1.5,0,0,0));Require(DruidCycloneArmor.State(p).Remaining256==9856 && Cast(p) && DruidCycloneArmor.State(p).Remaining256==10240,"Equal-rank refill");
        var old=DruidCycloneArmor.State(p);Train(p,CombatAbility.Twister);Require(DruidCycloneArmor.State(p)==old,"New synergy retroactively changed pool");Require(Cast(p) && DruidCycloneArmor.State(p).Maximum256==42*256,"Actual hard synergy floor");
    }
    private static void Consumers(PlayerMobile p)
    {
        var source=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};source.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);source.AIObject.AITimer.Stop();
        try
        {
            Pool(p,300);var raw=new GearDamage(512/256.0,1,.5,200/256.0,7,Magic:1);int hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
            SkillCombat.HitParts(source,p,raw with{Poison=0},false,spell:true);
            Require(SkillHealth.Balance256(p)==hp-1052 && DruidCycloneArmor.State(p)==null && NecromancerEffects.ManaBalance256(p)==mana,"Mixed raw shield/physical/magic/fractional overflow or mana drain");
            Pool(p,256);var ignored=DruidCycloneArmor.Absorb(p,new(3,0,0,0,7,Magic:11));Require(ignored==new GearDamage(3,0,0,0,7,Magic:11) && DruidCycloneArmor.State(p).Remaining256==256,"Non-elemental channels consumed pool");
            var amulet=Named(p,"Nokozan Relic");Require(p.FireResistance==50,"Real fire-resistance control");Pool(p,10*256);hp=SkillHealth.Balance256(p);SkillCombat.HitParts(source,p,new(0,20,0,0,0),false,spell:true);
            Require(SkillHealth.Balance256(p)==hp-5*256 && DruidCycloneArmor.State(p)==null,"Armor ran after resistance");p.AddToBackpack(amulet);ClassGear.Refresh(p);
            Pool(p,3*256);hp=SkillHealth.Balance256(p);AOS.Damage(p,source,10,0,100,0,0,0);Require(SkillHealth.Balance256(p)==hp-7*256 && DruidCycloneArmor.State(p)==null,"Native AOS incoming fire bypassed shield");
            Pool(p,10*256);hp=SkillHealth.Balance256(p);long bolt=WeaponMissiles.LaunchHostileFirebolt(source,p.Location,8*256,8*256);Require(bolt>0,"Real hostile firebolt creation");for(int i=0;i<100 && WeaponMissiles.State(bolt) is{} f;i++){WeaponMissiles.Tick(f.NextUtc);}
            Require(WeaponMissiles.State(bolt)==null && SkillHealth.Balance256(p)==hp && DruidCycloneArmor.State(p)?.Remaining256==2*256,"Travelling hostile firebolt bypassed armor");
            var elite=new EncounterCreature{Frozen=true,CantWalk=true,NoKillAwards=true};elite.MoveToWorld(new(p.X+4,p.Y+1,p.Z),p.Map);elite.AIObject.AITimer.Stop();
            Pool(p,5*256);hp=SkillHealth.Balance256(p);EliteCombat.Damage(elite,p,fire:9,cold:7,lightning:4);
            Require(SkillHealth.Balance256(p)==hp-15*256 && DruidCycloneArmor.State(p)==null,"Distinct elite damage path bypassed armor");elite.Delete();
            SkillHealth.SetBalance256(p,p.HitsMax*256);Ready(p);Mana(p);Require(Cast(p),"Recast after native damage");
        }
        finally{WeaponMissiles.Cancel(source);source.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();Train(p,CombatAbility.CycloneArmor);Cast(p);p.Kill();p.Resurrect();Require(DruidCycloneArmor.State(p)==null,"Death retained armor");p.Corpse?.Delete();p.Delete();
        p=Fresh();Train(p,CombatAbility.CycloneArmor);Cast(p);Ready(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(DruidCycloneArmor.State(p)==null,"Respec retained armor");p.Delete();
        p=Fresh();Train(p,CombatAbility.CycloneArmor);Cast(p);Ready(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin),"Direct history exit");Require(DruidCycloneArmor.State(p)==null && !DruidCycloneArmor.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Class history retained armor");p.Delete();
        p=Fresh();Train(p,CombatAbility.CycloneArmor);Cast(p);Ready(p);Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _),"Wardrobe exit");Require(!DruidCycloneArmor.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Wardrobe retained armor");p.Delete();
    }
    private static void WorkerAndMalformed(PlayerMobile p)
    {
        var before=DruidCycloneArmor.Snapshot();var bytes=Task.Run(()=>{var w=new BufferWriter(new byte[1024*1024],true);DruidCycloneArmor.Profiles.Serialize(w);return w.Buffer[..(int)w.Position];}).GetAwaiter().GetResult();
        var reader=new BufferReader(bytes);Require(reader.ReadInt()==0 && JsonSerializer.Deserialize<CycloneArmorState[]>(reader.ReadString()).SequenceEqual(before) && DruidCycloneArmor.Snapshot().SequenceEqual(before),"Worker serializer changed or lost live armor");
        var state=DruidCycloneArmor.State(p);foreach(var bad in new[]{state with{Rank=0},state with{Remaining256=0},state with{Remaining256=state.Maximum256+1},state with{Maximum256=-1},state with{DisplayPercent=101}})
        {bool refused=false;try{DruidCycloneArmor.Restore(before.Where(x=>x.Owner!=p.Serial.Value).Append(bad).ToArray());}catch(InvalidDataException){refused=true;}Require(refused && DruidCycloneArmor.Snapshot().SequenceEqual(before),"Malformed save partially changed state");}
    }
    private static void Check()
    {
        Goldens();Lifecycle();var owner=Fresh();Casting(owner);Consumers(owner);var helm=Named(owner,"Tarnhelm");Require(Cast(owner) && DruidCycloneArmor.State(owner).Rank==2 && DruidCycloneArmor.State(owner).Maximum256==55*256,"Real soft rank/hard synergy capture");
        var captured=DruidCycloneArmor.State(owner);owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(DruidCycloneArmor.State(owner)==captured && !Cast(owner) && DruidCycloneArmor.State(owner)==captured,"Older-source lower-rank recast rule/captured armor");Require(owner.EquipItem(helm),"Saved helm re-equip");ClassGear.Refresh(owner);
        DruidCycloneArmor.Absorb(owner,new(0,257/256.0,0,0,0));WorkerAndMalformed(owner);
        var pending=Fresh();Train(pending,CombatAbility.CycloneArmor);Ready(pending);int pendingMana=NecromancerEffects.ManaBalance256(pending);Require(ClassCombat.Perform(pending,CombatAbility.CycloneArmor,null,pending.Location,Core.TickCount),"Saved unpaid windup");ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,DruidCycloneArmor.State(owner),NecromancerEffects.ManaBalance256(owner),owner.Location,owner.Map.MapID,pending.Serial.Value,pendingMana)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(DruidCycloneArmor.State(owner)==saved.State && NecromancerEffects.ManaBalance256(owner)==saved.Mana && World.FindItem((Serial)saved.Helm)?.Parent==owner,"Saved rank/pool/gear/mana changed");
        Require(DruidCycloneArmor.State(pending)==null && ClassCombat.For(pending).PendingAbility==CombatAbility.None && NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Unpaid windup replayed/paid on load");
        Require(SkillBindings.Instance.Get(owner).Right==CombatAbility.CycloneArmor && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Druid,5)==CombatAbility.CycloneArmor,"Saved binding identity");WorkerAndMalformed(owner);
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.CycloneArmor);Ready(p);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.CycloneArmor) && SkillBindings.Instance.AssignQuick(p,5,CombatAbility.CycloneArmor),"Connected bindings");
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(protocol!=null,"Protocol bootstrap");protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        int mana=NecromancerEffects.ManaBalance256(p);long deadline=Core.TickCount+5000;bool started=false;
        void Wait(){try{Require(Core.TickCount<deadline,"Connected cast timeout");var state=DruidCycloneArmor.State(p);if(!started && ClassCombat.For(p).PendingAbility==CombatAbility.CycloneArmor){started=true;Require(state==null && NecromancerEffects.ManaBalance256(p)==mana,"Natural unpaid SC windup");}
            if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}Require(started && state is{Rank:1,Remaining256:10240,Maximum256:10240} && NecromancerEffects.ManaBalance256(p)==mana-1280,"Natural self cast/payment");p.Delete();Log.Information("cyclone-armor-input-result: passed");}
            catch(Exception e){p.Delete();Log.Error(e,"cyclone-armor-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("cyclone-armor-input: ready ability={Ability} x=65530 y=65530 z=0",(int)CombatAbility.CycloneArmor);Wait();});
    }
}
