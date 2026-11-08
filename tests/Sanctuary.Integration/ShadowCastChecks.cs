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

public static class ShadowCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowCastChecks));
    private sealed record Held(uint Body,string Text,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Point3D Point,Held Body,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-cast-fixtures.json");
    private static CombatAbility Ability=CombatAbility.ShadowWarrior;
    private static bool Creating;private static int Scene;
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("ShadowCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Register("shadow-cast-check",()=>Check(false));Register("shadow-cast-reload-check",()=>Check(true));
        foreach(bool quick in new[]{false,true}){bool q=quick;string name=q?"shadow-cast-quick":"shadow-cast-right";Register(name+"-check",()=>FixtureNetwork.Connected(name+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned original Shadow cast,payment,shared roster and input",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody b)=>new(b.Serial.Value,b.SavedText,ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static void SavedBody(AssassinShadowBody b,Held saved)
    {
        var actual=Capture(b);
        Require(actual.Body==saved.Body&&Json(actual.Items)==Json(saved.Items)&&actual.Resources==saved.Resources&&actual.Random==saved.Random&&actual.Life==saved.Life,"Paid original items/resources/wounds/RNG changed on reload");
        var before=JsonSerializer.Deserialize<ShadowBodyState>(saved.Text);var old=before.GroundMotion.World.Controller;var now=b.Prepared.GroundMotion.World.Controller;
        Require(now.Frame>=old.Frame,"Paid parked source clock moved backwards");
        // The real conductor keeps its idle clock while parked. Derive the
        // only allowed change from its existing Skip operation,not loaded HP
        // or item/RNG state,and compare every other body/controller field.
        var next=IronGolemWorld.Park(old with{Frame=now.Frame,NextUtc=old.NextUtc.AddMilliseconds(40L*(now.Frame-old.Frame))});
        var expected=before with{GroundMotion=before.GroundMotion with{World=before.GroundMotion.World with{Controller=next}}};
        Require(Json(expected)==actual.Text,"Paid parked controller/body differs beyond exact Skip clock:expected="+Json(expected)+",actual="+actual.Text);
    }
    private static PlayerMobile Owner(bool trained=true)
    {
        PlayerMobile p;Creating=true;try{p=new RecyclerFixturePlayer{Body=0x190,Player=true,Name="ShadowCastFixture"};}finally{Creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Shadow fixture Strength");}
        if(trained){ClassSkillChecks.Train(p,"assassin.shadowwarrior",20);ClassSkillChecks.Train(p,"assassin.shadowmaster",20);}FixtureSpace.Corridor(p,6600,28,3340+32*Scene++);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||!AssassinShadowCasting.Handles(c.Ability)).ToArray()});ClassCombat.Stop(p);var a=ClassCombat.For(p);a.NextAction=a.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Drop(PlayerMobile p)
    {foreach(var b in (p.AllFollowers??[]).OfType<AssassinShadowBody>().ToArray()){b.Delete();}p.Delete();}
    private static AssassinShadowBody Cast(PlayerMobile p,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;var old=AssassinShadowCasting.For(p);var world=NpcWorldAllocation.Capture();var point=aim??new Point3D(p.X+3,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Learned Shadow windup");var a=ClassCombat.For(p);Require(a.Timing==AssassinCastClock.Calculate(CombatTiming.Fcr(p))&&a.Animation==16&&a.PendingAbility==Ability&&NecromancerEffects.ManaBalance256(p)==mana&&NpcWorldAllocation.Capture()==world,"Shadow SC start paid or allocated early");
        Require(!ClassCombat.ResolveAction(p,a.EffectAt-1)&&AssassinShadowCasting.For(p).SequenceEqual(old),"Early Shadow emission");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,a.EffectAt)&&!ClassCombat.ResolveAction(p,a.EffectAt),"Shadow once-only release");var b=AssassinShadowCasting.For(p).Single();
        Require(old.All(x=>x.Deleted)&&b.Prepared is{LearnedCast:true,Initialization:not null}&&ShadowGroundMotion.Active(b)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned roster commit or exact one-time payment");
        var until=FireGround.Snapshot().Cooldowns.Single(c=>c.Owner==p.Serial.Value&&c.Ability==Ability).UntilUtc;
        Require(!FireGround.Ready(p,Ability,until.AddMilliseconds(-1))&&FireGround.Ready(p,Ability,until),"Learned Shadow did not commit its local cooldown boundary");
        return b;
    }
    private static void Transactions(PlayerMobile p)
    {
        Require(!SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Shadow right-only/quick policy");
        var b=Cast(p);SkillHealth.SetBalance256(b,b.MaximumLife256-1025);PersistentMinions.Park(b);var held=Capture(b);var factory=AssassinShadowCasting.Spawn;
        try
        {
            foreach(int mode in new[]{0,1})
            {
                Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var world=NpcWorldAllocation.Capture();AssassinShadowBody failed=null;
                AssassinShadowCasting.Spawn=(owner,point,at,ability)=>
                {
                    if(mode==0){throw new NotSupportedException("Controlled unavailable candidate");}
                    failed=factory(owner,point,at,ability);NecromancerEffects.SetMana256(owner,0);return failed;
                };
                Require(ClassCombat.Perform(p,Ability,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Failed transaction start");Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Failed transaction committed");
                Require(AssassinShadowCasting.For(p).Single()==b&&Json(Capture(b))==Json(held)&&(mode!=0||NecromancerEffects.ManaBalance256(p)==mana),"Failed candidate replaced or healed the original");
                if(mode==0){Require(NpcWorldAllocation.Capture()==world,"Unavailable candidate consumed allocation RNG");}else{Require(failed is{Deleted:true}&&NecromancerEffects.ManaBalance256(p)==0&&NpcWorldAllocation.Capture()!=world,"Unpaid allocated candidate persisted or allocation was rewound");}
            }
        }
        finally{AssassinShadowCasting.Spawn=factory;}
        foreach(int mode in new[]{0,1,2,3})
        {
            Ready(p);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Cancellation windup");long effect=ClassCombat.For(p).EffectAt;
            if(mode==0){NecromancerEffects.SetMana256(p,0);}else if(mode==1){p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);}else if(mode==2){p.Frozen=true;}else{ClassCombat.Stop(p);}
            Require(!ClassCombat.ResolveAction(p,effect)&&AssassinShadowCasting.For(p).Single()==b&&NpcWorldAllocation.Capture()==world,"Cancelled/lost-mana cast allocated or replaced");
            Require(NecromancerEffects.ManaBalance256(p)==(mode==0?0:mana),"Cancelled cast spent mana");p.Frozen=false;
        }
        Ready(p);NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);var noDraw=NpcWorldAllocation.Capture();Require(!ClassCombat.Perform(p,Ability,null,new(p.X+3,p.Y,p.Z),Core.TickCount)&&NpcWorldAllocation.Capture()==noDraw,"Fractionally underfunded start");
        // Prepared backend bodies are not a paid learned roster member.
        var prepared=factory(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now.AddHours(8),Ability);var next=Cast(p);Require(!prepared.Deleted&&b.Deleted&&next!=b,"Learned replacement claimed an uncommitted backend body");prepared.Delete();
    }
    private static void Samples(PlayerMobile p)
    {
        var game=NpcWorldAllocation.Capture();
        try
        {
            foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})
            foreach(int seed in new[]{1,4,9,17})
            {
                NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,(uint)seed,SourceRandom.InitLow(seed)));
                var b=AssassinShadowBody.CreateInitializedController(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now,ability);
                Require(ShadowGroundMotion.Active(b)&&b.Prepared.DefenseProfile!=null&&!b.Prepared.LearnedCast,"Original creation cohort failed admission");PersistentMinions.Park(b);b.Delete();
            }
        }
        finally{NpcWorldAllocation.Restore(game);}
    }
    private static void Town(PlayerMobile p)
    {
        var origin=p.Location;var point=new Point3D(p.X+12,p.Y,p.Z);var region=new GuardedRegion("ShadowLearnedTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(point.X-2,point.Y-2,7,7));region.Register();
        try
        {
            p.MoveToWorld(point,p.Map);Require(MinionSafeAreas.Contains(p),"Learned town fixture");var b=Cast(p,new(p.X+1,p.Y,p.Z));Require(MinionSafeAreas.Parked(b),"Paid town Shadow did not park");SkillHealth.SetBalance256(b,b.MaximumLife256-1025);int life=SkillHealth.Balance256(b);var rng=NpcEquipmentRandom.Require(b);string items=Json(ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray());
            p.MoveToWorld(origin,p.Map);PersistentMinions.Resume(p);Require(!MinionSafeAreas.Parked(b)&&b.Map==p.Map&&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.Require(b)==rng&&Json(ShadowLoadout.Originals(b).Select(i=>i.SavedText).ToArray())==items,"Town return changed learned originals or wounds");ShadowEquipmentDefense.Require(b);
        }
        finally{region.Unregister();}
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"World allocation before learned Shadow");PlayerMobile p;Point3D point;AssassinShadowBody b;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Learned Shadow reload count");p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;b=World.FindMobile((Serial)saved.Body.Body)as AssassinShadowBody;point=saved.Point;
            Ability=b.Prepared.Capture.Ability;SavedBody(b,saved.Body);
            Require(NecromancerEffects.ManaBalance256(p)==saved.Mana,"Paid owner mana changed:expected="+saved.Mana+",actual="+NecromancerEffects.ManaBalance256(p));
            Require(b.Prepared.LearnedCast&&AssassinShadowCasting.For(p).Single()==b,"Paid learned roster changed");
            Require(ClassCombat.For(p).PendingAbility==CombatAbility.None,"Unpaid windup replayed");
            p.MoveToWorld(point,Map.Trammel);PersistentMinions.Resume(p);
        }
        else
        {
            var untrained=Owner(false);Require(!ClassCombat.Available(untrained,Ability),"Untrained Shadow available");Drop(untrained);p=Owner();point=p.Location;foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster}){Ability=ability;Transactions(p);Town(p);}Samples(p);
        }
        b=Cast(p);var target=new ValkyrieContactChecks.Recipient();target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
        try
        {
            var held=Capture(b);var world=NpcWorldAllocation.Capture();int life=SkillHealth.Balance256(target);
            Require(b.BeginMelee(target)==TimeSpan.FromSeconds(1)&&Json(Capture(b))==Json(held)&&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(target)==life,"Native fist timer duplicated source damage/resources/RNG");
        }
        finally{target.Delete();}
        SkillHealth.SetBalance256(b,b.MaximumLife256-1025);PersistentMinions.Park(b);Ready(p);
        Require(ClassCombat.Perform(p,Ability,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Saved unpaid windup");var a=ClassCombat.For(p);a.EffectAt+=FixtureSpace.RetentionMilliseconds;a.NextAction+=FixtureSpace.RetentionMilliseconds;
        PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,Capture(b),NecromancerEffects.ManaBalance256(p),generation)));
        Log.Information("Learned Shadow:shared Assassin SC/right/quick admission,exact fractional once-only payment,candidate rollback/forward allocation and learned-only replacement;town parking and original items/resources/wounds/RNG/paid roster through two reloads,unpaid windup not replayed;current executable/search/item-only/sunder/client limits remain");
    }
    private static void Input(NetState network,bool quick)
    {
        Ability=quick?CombatAbility.ShadowMaster:CombatAbility.ShadowWarrior;var p=Owner();if(quick){var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Shadow FCR gear");ClassGear.Refresh(p);}
        var t=new ValkyrieContactChecks.Recipient();t.SetHits(65000);t.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);
        Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Shadow socket bindings");var account=FixtureNetwork.Authenticate(network,p,"shadow-cast-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,life=SkillHealth.Balance256(t);var point=new Point3D(p.X+3,p.Y,p.Z);string label=quick?"shadow-cast-quick":"shadow-cast-right";long limit=Core.TickCount+15000;bool began=false,finished=false;
        void Cleanup(){ClassCombat.Stop(p);foreach(var pet in AssassinShadowCasting.For(p)){SorceressSpellMissiles.Cancel(pet);ChainLightning.Cancel(pet);}t.Delete();Drop(p);account.Delete();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=limit){throw new InvalidOperationException("Shadow real input/combat timeout:began="+began+",recipient="+Json(new{Life=SkillHealth.Balance256(t),InitialLife=life,t.Hits,t.HitsMax,t.Notifications,t.AtNotification,Packets=NativeMonsterReturn.Session(t).Packets,CanDamage=t.CanBeDamaged(),t.Blessed,Town=Server.Spells.SpellHelper.IsTown(t.Location,p),Metadata=NativeMonsterReturn.Metadata(t),Position=t.Location,Resists=new[]{t.PhysicalResistance,t.FireResistance,t.ColdResistance,t.PoisonResistance,t.EnergyResistance}})+",controller="+Json(AssassinShadowCasting.For(p).SingleOrDefault()?.Prepared?.GroundMotion));}var a=ClassCombat.For(p);
                if(a.PendingAbility==Ability&&!began){Require(a.Timing==AssassinCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana,"Socket SC windup paid early");began=true;Log.Information("{Label}: windup",label);}
                var b=AssassinShadowCasting.For(p).SingleOrDefault();if(b==null||SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost&&b.Prepared.LearnedCast&&b.WorldGround.State.Controller.Frame>=20&&t.Notifications>0&&NativeMonsterReturn.Session(t).Packets>0,"Actual input/source controller combat or one-time mana failed");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Assassin,point.X,point.Y,point.Z);Inspect();}));
    }
}
