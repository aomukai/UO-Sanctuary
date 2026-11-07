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

public static class ValkyrieCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieCastChecks));
    private sealed record Held(uint Body,string Text,string[] Items,string Resources,NpcEquipmentSeed Random,int Life);
    private sealed record Saved(uint Owner,Point3D Point,Held Body,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-cast-fixtures.json");
    private static bool Creating;private static int Scene;
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("ValkyrieCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Register("valkyrie-cast-check",()=>Check(false));Register("valkyrie-cast-reload-check",()=>Check(true));
        foreach(bool quick in new[]{false,true}){bool q=quick;string name=q?"valkyrie-cast-quick":"valkyrie-cast-right";Register(name+"-check",()=>FixtureNetwork.Connected(name+"-result",n=>Input(n,q)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned original Valkyrie cast,payment,roster and input",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AmazonValkyrieBody b)=>new(b.Serial.Value,b.SavedText,ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray(),Json(GeneratedNpcLiveResources.State(b)),NpcEquipmentRandom.Require(b),SkillHealth.Balance256(b));
    private static void SavedBody(AmazonValkyrieBody b,Held saved)
    {
        var actual=Capture(b);
        Require(actual.Body==saved.Body&&Json(actual.Items)==Json(saved.Items)&&actual.Resources==saved.Resources&&actual.Random==saved.Random&&actual.Life==saved.Life,"Paid original items/resources/wounds/RNG changed on reload");
        var before=JsonSerializer.Deserialize<ValkyrieBodyState>(saved.Text);var old=before.GroundMotion.World.Controller;var now=b.Prepared.GroundMotion.World.Controller;
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
        PlayerMobile p;Creating=true;try{p=new RecyclerFixturePlayer{Body=0x190,Player=true,Name="ValkyrieCastFixture"};}finally{Creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Valkyrie fixture Strength");}
        if(trained){ClassSkillChecks.Train(p,"amazon.valkyrie",1);}FixtureSpace.Corridor(p,6400,28,3340+32*Scene++);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var a=ClassCombat.For(p);a.NextAction=a.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,100*256+17);}
    private static void Drop(PlayerMobile p)
    {foreach(var b in (p.AllFollowers??[]).OfType<AmazonValkyrieBody>().ToArray()){b.Delete();}p.Delete();}
    private static AmazonValkyrieBody Cast(PlayerMobile p,Point3D? aim=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Valkyrie).Mana256;var old=AmazonValkyrieCasting.For(p);var world=NpcWorldAllocation.Capture();var point=aim??new Point3D(p.X+3,p.Y,p.Z);
        Require(ClassCombat.Perform(p,CombatAbility.Valkyrie,null,point,Core.TickCount),"Learned Valkyrie windup");var a=ClassCombat.For(p);Require(AmazonCastClock.TryTiming(p,out var timing)&&a.Timing==timing&&a.Animation==16&&a.PendingAbility==CombatAbility.Valkyrie&&NecromancerEffects.ManaBalance256(p)==mana&&NpcWorldAllocation.Capture()==world,"Valkyrie SC start paid or allocated early");
        Require(!ClassCombat.ResolveAction(p,a.EffectAt-1)&&AmazonValkyrieCasting.For(p).SequenceEqual(old),"Early Valkyrie emission");ClassCombat.ReleaseCast(p);
        Require(ClassCombat.ResolveAction(p,a.EffectAt)&&!ClassCombat.ResolveAction(p,a.EffectAt),"Valkyrie once-only release");var b=AmazonValkyrieCasting.For(p).Single();
        Require(old.All(x=>x.Deleted)&&b.Prepared is{LearnedCast:true,Initialization:not null}&&b.Prepared.GroundMotion.StartupProfile==ValkyrieStartup.Profile&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Learned roster commit or exact one-time payment");
        return b;
    }
    private static void Transactions(PlayerMobile p)
    {
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.Valkyrie)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Valkyrie)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Valkyrie),"Valkyrie right-only/quick policy");
        var b=Cast(p);SkillHealth.SetBalance256(b,b.MaximumLife256-1025);PersistentMinions.Park(b);var held=Capture(b);var factory=AmazonValkyrieCasting.Spawn;
        try
        {
            foreach(int mode in new[]{0,1})
            {
                Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var world=NpcWorldAllocation.Capture();AmazonValkyrieBody failed=null;
                AmazonValkyrieCasting.Spawn=(owner,point,at)=>
                {
                    if(mode==0){throw new NotSupportedException("Controlled unavailable candidate");}
                    failed=factory(owner,point,at);NecromancerEffects.SetMana256(owner,0);return failed;
                };
                Require(ClassCombat.Perform(p,CombatAbility.Valkyrie,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Failed transaction start");Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Failed transaction committed");
                Require(AmazonValkyrieCasting.For(p).Single()==b&&Json(Capture(b))==Json(held)&&(mode!=0||NecromancerEffects.ManaBalance256(p)==mana),"Failed candidate replaced or healed the original");
                if(mode==0){Require(NpcWorldAllocation.Capture()==world,"Unavailable candidate consumed allocation RNG");}else{Require(failed is{Deleted:true}&&NecromancerEffects.ManaBalance256(p)==0&&NpcWorldAllocation.Capture()!=world,"Unpaid allocated candidate persisted or allocation was rewound");}
            }
        }
        finally{AmazonValkyrieCasting.Spawn=factory;}
        foreach(int mode in new[]{0,1,2,3})
        {
            Ready(p);var world=NpcWorldAllocation.Capture();int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,CombatAbility.Valkyrie,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Cancellation windup");long effect=ClassCombat.For(p).EffectAt;
            if(mode==0){NecromancerEffects.SetMana256(p,0);}else if(mode==1){p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);}else if(mode==2){p.Frozen=true;}else{ClassCombat.Stop(p);}
            Require(!ClassCombat.ResolveAction(p,effect)&&AmazonValkyrieCasting.For(p).Single()==b&&NpcWorldAllocation.Capture()==world,"Cancelled/lost-mana cast allocated or replaced");
            Require(NecromancerEffects.ManaBalance256(p)==(mode==0?0:mana),"Cancelled cast spent mana");p.Frozen=false;
        }
        Ready(p);NecromancerEffects.SetMana256(p,25*256-1);var noDraw=NpcWorldAllocation.Capture();Require(!ClassCombat.Perform(p,CombatAbility.Valkyrie,null,new(p.X+3,p.Y,p.Z),Core.TickCount)&&NpcWorldAllocation.Capture()==noDraw,"Fractionally underfunded start");
        // Prepared backend bodies are not a paid learned roster member.
        var prepared=factory(p,new(p.X+4,p.Y,p.Z),TimedEffects.Now.AddHours(8));var next=Cast(p);Require(!prepared.Deleted&&b.Deleted&&next!=b,"Learned replacement claimed an uncommitted backend body");prepared.Delete();
    }
    private static void Town(PlayerMobile p)
    {
        var origin=p.Location;var point=new Point3D(p.X+12,p.Y,p.Z);var region=new GuardedRegion("ValkyrieLearnedTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(point.X-2,point.Y-2,7,7));region.Register();
        try
        {
            p.MoveToWorld(point,p.Map);Require(MinionSafeAreas.Contains(p),"Learned town fixture");var b=Cast(p,new(p.X+1,p.Y,p.Z));Require(MinionSafeAreas.Parked(b),"Paid town Valkyrie did not park");SkillHealth.SetBalance256(b,b.MaximumLife256-1025);int life=SkillHealth.Balance256(b);var rng=NpcEquipmentRandom.Require(b);string items=Json(ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray());
            p.MoveToWorld(origin,p.Map);PersistentMinions.Resume(p);Require(!MinionSafeAreas.Parked(b)&&b.Map==p.Map&&SkillHealth.Balance256(b)==life&&NpcEquipmentRandom.Require(b)==rng&&Json(ValkyrieEquipment.Originals(b).Select(i=>i.SavedText).ToArray())==items,"Town return changed learned originals or wounds");ValkyrieEquipmentDefense.Require(b);
        }
        finally{region.Unregister();}
    }
    private static void Check(bool reload)
    {
        Require(NpcWorldAllocation.Capture()==NpcWorldAllocationChecks.CheckedContinuation,"World allocation before learned Valkyrie");PlayerMobile p;Point3D point;AmazonValkyrieBody b;int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;Require(generation<=2,"Learned Valkyrie reload count");p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;b=World.FindMobile((Serial)saved.Body.Body)as AmazonValkyrieBody;point=saved.Point;
            SavedBody(b,saved.Body);
            Require(NecromancerEffects.ManaBalance256(p)==saved.Mana,"Paid owner mana changed:expected="+saved.Mana+",actual="+NecromancerEffects.ManaBalance256(p));
            Require(b.Prepared.LearnedCast&&AmazonValkyrieCasting.For(p).Single()==b,"Paid learned roster changed");
            Require(ClassCombat.For(p).PendingAbility==CombatAbility.None,"Unpaid windup replayed");
            p.MoveToWorld(point,Map.Trammel);PersistentMinions.Resume(p);
        }
        else
        {
            var untrained=Owner(false);Require(!ClassCombat.Available(untrained,CombatAbility.Valkyrie),"Untrained Valkyrie available");Drop(untrained);p=Owner();point=p.Location;Transactions(p);Town(p);
        }
        b=Cast(p);var target=new ValkyrieContactChecks.Recipient();target.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);target.AIObject.AITimer.Stop();
        try
        {
            var held=Capture(b);var world=NpcWorldAllocation.Capture();int life=SkillHealth.Balance256(target);
            Require(b.BeginMelee(target)==TimeSpan.FromSeconds(1)&&Json(Capture(b))==Json(held)&&NpcWorldAllocation.Capture()==world&&SkillHealth.Balance256(target)==life,"Native fist timer duplicated source damage/resources/RNG");
        }
        finally{target.Delete();}
        SkillHealth.SetBalance256(b,b.MaximumLife256-1025);PersistentMinions.Park(b);Ready(p);
        Require(ClassCombat.Perform(p,CombatAbility.Valkyrie,null,new(p.X+3,p.Y,p.Z),Core.TickCount),"Saved unpaid windup");var a=ClassCombat.For(p);a.EffectAt+=FixtureSpace.RetentionMilliseconds;a.NextAction+=FixtureSpace.RetentionMilliseconds;
        PersistentMinions.Suspend(p);p.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,point,Capture(b),NecromancerEffects.ManaBalance256(p),generation)));
        Log.Information("Learned Valkyrie:shared Amazon SC/right/quick admission,exact fractional once-only payment,candidate rollback/forward allocation and learned-only replacement;town parking and original items/resources/wounds/RNG/paid roster through two reloads,unpaid windup not replayed;current executable/search/item-only/sunder/client limits remain");
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();if(quick){var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Magefist").Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Valkyrie FCR gear");ClassGear.Refresh(p);}
        var t=new ValkyrieContactChecks.Recipient();t.SetHits(65000);t.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Valkyrie)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Valkyrie),"Valkyrie socket bindings");var account=FixtureNetwork.Authenticate(network,p,"valkyrie-cast-");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.Valkyrie).Mana256,life=SkillHealth.Balance256(t);var point=new Point3D(p.X+3,p.Y,p.Z);string label=quick?"valkyrie-cast-quick":"valkyrie-cast-right";long limit=Core.TickCount+15000;bool began=false,finished=false;
        void Cleanup(){ClassCombat.Stop(p);foreach(var pet in AmazonValkyrieCasting.For(p)){SorceressSpellMissiles.Cancel(pet);ChainLightning.Cancel(pet);}t.Delete();Drop(p);account.Delete();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=limit){throw new InvalidOperationException("Valkyrie real input/combat timeout:began="+began+",recipient="+Json(new{Life=SkillHealth.Balance256(t),InitialLife=life,t.Hits,t.HitsMax,t.Notifications,t.AtNotification,Packets=NativeMonsterReturn.Session(t).Packets,CanDamage=t.CanBeDamaged(),t.Blessed,Town=Server.Spells.SpellHelper.IsTown(t.Location,p),Metadata=NativeMonsterReturn.Metadata(t),Position=t.Location,Resists=new[]{t.PhysicalResistance,t.FireResistance,t.ColdResistance,t.PoisonResistance,t.EnergyResistance}})+",controller="+Json(AmazonValkyrieCasting.For(p).SingleOrDefault()?.Prepared?.GroundMotion));}var a=ClassCombat.For(p);
                if(a.PendingAbility==CombatAbility.Valkyrie&&!began){Require(AmazonCastClock.TryTiming(p,out var timing)&&a.Timing==timing&&NecromancerEffects.ManaBalance256(p)==mana,"Socket SC windup paid early");began=true;Log.Information("{Label}: windup",label);}
                var b=AmazonValkyrieCasting.For(p).SingleOrDefault();if(b==null||SkillHealth.Balance256(t)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost&&b.Prepared.LearnedCast&&b.Ground.State.Controller.Frame>=20&&t.Notifications>0&&NativeMonsterReturn.Session(t).Packets>0,"Actual input/source controller combat or one-time mana failed");finished=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)CombatAbility.Valkyrie,(int)DiabloClass.Amazon,point.X,point.Y,point.Z);Inspect();}));
    }
}
