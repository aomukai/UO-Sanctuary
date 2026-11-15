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

public static class MiasmaWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,uint Weapon,uint Target,int TargetLife,Point3D Origin,int Mana,int Life,MiasmaWorldSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"miasma-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="MiasmaWorldFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>m.Name!="MiasmaWorldFixture"&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"miasma-world-reload-check":"miasma-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Miasma bolt/maker/cloud action,delay and save policy",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x,int y=0)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="MiasmaWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(r,0);}return t;
    }
    private static string State()=>JsonSerializer.Serialize(MiasmaWorld.Snapshot());
    private static DateTime Grow(PlayerMobile p,DateTime at)
    {
        for(int frame=1;frame<=55;frame++)
        {
            var now=at.AddMilliseconds(frame*40);var r=MiasmaWorld.TickPrepared(now,true,p.Serial.Value);Require(r.Fault==null,r.Fault??"Unexpected Miasma fault");
            if(r.CreatedClouds>0){Require(r.CreatedClouds==2,"Maker emission must produce exactly two clouds");return now;}
        }
        throw new InvalidOperationException("Bolt did not produce clouds");
    }
    private static void Suppliers(PlayerMobile p)
    {
        var captured=MiasmaBoltBirths.Capture(p);Require(captured.Rank==2&&captured.Shape.EffectiveRank==2,"Actual soft rank capture");
        foreach(int rank in new[]{0,1,5,10,20,40})
        {
            var v=new SkillValues(SkillMechanics.For(CombatAbility.MiasmaBolt),k=>k=="Miasma Bolt"?1:k=="Enhanced Entropy"?rank:0,_=>0,99);
            var shape=WarlockEntropyClouds.Capture(v);Require(shape.LifetimeFrames==50+Math.Min(rank*5,100)&&shape.Radius==(rank>=10?4:rank>=5?3:2),"Effective Entropy lifetime/radius");
        }
        Require(MiasmaBoltBirths.Makers()is[{X:0,Y:2},{X:-2,Y:2}],"Shared ring direction/order");int emitted=0;
        Require(MiasmaBoltBirths.MakerUpdate(3,()=>emitted++)==2&&MiasmaBoltBirths.MakerUpdate(2,()=>emitted++)==1&&emitted==0
            &&MiasmaBoltBirths.MakerUpdate(1,()=>emitted++)==0&&emitted==1,"Three-update maker emission order");
        var seed=SourceRandom.InitLow(73);var baseSeed=seed;var raw=MiasmaBoltBirths.Roll(captured with{MagicMastery=100,Modifiers=new(0,0,0,0,0)},true,ref seed);
        int min=captured.Numbers.CloudMinimum256*2,max=captured.Numbers.CloudMaximum256*2;
        int expected=SourceSpellPacketRoll.Range(ref baseSeed,min,max);Require(NamedElements.To256(raw.Magic)==expected&&raw.Physical==0&&raw.Poison==0,"Magic mastery or poison substitution");
        seed=SourceRandom.InitLow(73);var potion=MiasmaBoltBirths.Roll(captured with{MagicMastery=100,Modifiers=new(10,0,0,0,0)},true,ref seed);
        Require(potion.Fire>0&&potion.Cold>0&&potion.Lightning>0&&potion.Poison>0&&potion.Magic==raw.Magic,"Captured potion packet lost components");
    }
    private static void Scene(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="MiasmaWorldFixture";p.Body=0x191;p.Female=true;p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.miasmabolt");ClassSkillChecks.Train(p,"warlock.enhancedentropy");FixtureSpace.Corridor(p,6500,60,2700);
        var weapon=new Dagger();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Original dagger");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Original soft-rank source");ClassGear.Refresh(p);
        var account=FixtureNetwork.Authenticate(network,p,"miasma-world-");var target=Target(p,4);bool kept=false;var oldSeed=MiasmaWorld.SeedLow;MiasmaWorld.SeedLow=()=>73;
        try
        {
            Require(MiasmaWorld.Approved&&WarlockCombat.Implemented(CombatAbility.MiasmaBolt)&&!WarlockCombat.Implemented(CombatAbility.EnhancedEntropy)
                &&SkillBindings.Instance.Assign(p,true,CombatAbility.MiasmaBolt),"Approved world admission missing");Suppliers(p);
            Ready(p);var action=MiasmaBoltCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);Require(action!=null,"Prepared SC startup");var s=ClassCombat.For(p);long effect=s.EffectAt;int mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.ResolveAction(p,effect-1)&&MiasmaWorld.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Early SC debit");ClassCombat.Stop(p);
            Require(action.Cancelled&&!ClassCombat.ResolveAction(p,effect),"Cancelled SC emitted");
            Ready(p);action=MiasmaBoltCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=s.EffectAt;NecromancerEffects.SetMana256(p,1);
            Require(!ClassCombat.ResolveAction(p,effect)&&action.Released&&MiasmaWorld.For(p).Length==0,"Late mana emitted");
            Ready(p);action=MiasmaBoltCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=s.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,effect)&&action.Cancelled,"Moving windup emitted");p.MoveToWorld(origin,p.Map);
            Ready(p);action=MiasmaBoltCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=s.EffectAt;p.AddToBackpack(weapon);
            Require(!ClassCombat.ResolveAction(p,effect)&&action.Cancelled,"Weapon change windup emitted");Require(p.EquipItem(weapon),"Restore dagger");
            Ready(p);action=MiasmaBoltCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=s.EffectAt;mana=NecromancerEffects.ManaBalance256(p);int wear=weapon.HitPoints;
            target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(ClassCombat.ResolveAction(p,effect)&&action.Flight>0,"Valid SC release");
            var bolt=MiasmaWorld.For(p).Single();Require(bolt.Kind==MiasmaNodeKind.Bolt&&bolt.Vx>0&&bolt.Vy==0&&NecromancerEffects.ManaBalance256(p)==mana-action.ManaPaid256&&action.ManaPaid256==640,"One fractional payment");
            Require(!ClassCombat.ResolveAction(p,effect),"Release replayed");var birth=bolt.Birth;ClassSkillChecks.Train(p,"warlock.enhancedentropy",10);p.AddToBackpack(helm);ClassGear.Refresh(p);
            int hp=SkillHealth.Balance256(target);var cloudAt=Grow(p,bolt.CreatedUtc);var clouds=MiasmaWorld.For(p);Require(clouds.Length==2&&clouds.All(n=>n.Kind==MiasmaNodeKind.Cloud&&n.Cloud.Shape==birth.Shape)&&SkillHealth.Balance256(target)<hp&&target.Poison==null&&weapon.HitPoints==wear,"Direct/cloud damage, snapshot,poison or weapon effect");
            var leases=MiasmaWorld.Snapshot().Leases.Where(l=>l.Cast==bolt.Cast&&l.Target==target.Serial.Value).ToArray();Require(leases.Length==1&&leases[0].UntilUtc==cloudAt.AddSeconds(1),"Overlapping makers double-hit or delay identity");
            int held=SkillHealth.Balance256(target);var r=MiasmaWorld.TickPrepared(cloudAt.AddMilliseconds(960),true,p.Serial.Value);Require(r.CloudContacts==0&&SkillHealth.Balance256(target)==held,"Early cloud repeat");
            r=MiasmaWorld.TickPrepared(cloudAt.AddSeconds(1),true,p.Serial.Value);Require(r.CloudContacts==1&&SkillHealth.Balance256(target)<held,"One-second repeat");
            var entrant=Target(p,5);try{r=MiasmaWorld.TickPrepared(cloudAt.AddMilliseconds(1040),true,p.Serial.Value);Require(r.CloudContacts==1,"Entrant did not hit on next scan independently");}finally{entrant.Delete();}
            held=SkillHealth.Balance256(target);var until=clouds[0].UntilUtc;r=MiasmaWorld.TickPrepared(until,true,p.Serial.Value);
            Require(MiasmaWorld.For(p).Length==0&&r.CloudContacts==0&&SkillHealth.Balance256(target)==held,"Expiry added damage");
            Require(p.EquipItem(helm),"Restore helm");ClassGear.Refresh(p);OverlapAndLifecycle(p,target);target.Internalize();Immunity(p);target.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Boundary(p,target);Save(p,helm,weapon,target);kept=true;
            Log.Information("Miasma world:SC early/cancel/mana/movement/weapon/once-only release,actual bolt-to-two-maker-to-two-cloud damage,effective Entropy snapshot,per-cast lease/entrant/expiry and no poison/weapon wear pass");
        }
        finally
        {
            MiasmaWorld.SeedLow=oldSeed;ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){target.Delete();MiasmaWorld.Clear(p);p.Delete();}
        }
    }
    private static void OverlapAndLifecycle(PlayerMobile p,BaseCreature target)
    {
        Ready(p);var at=TimedEffects.Now;long a=MiasmaWorld.ReleasePrepared(p,target.Location,at),b=MiasmaWorld.ReleasePrepared(p,target.Location,at);Require(a>0&&b>a,"Independent cast allocation");
        DateTime cloudAt=default;
        for(int i=1;i<=55;i++){var now=at.AddMilliseconds(i*40);var r=MiasmaWorld.TickPrepared(now,true,p.Serial.Value);Require(r.Fault==null,"Overlap fault");if(r.CreatedClouds>0){Require(r.CreatedClouds==4&&r.CloudContacts==2,"Separate casts shared delay or each maker hit");cloudAt=now;break;}}
        Require(cloudAt!=default&&MiasmaWorld.Snapshot().Leases.Count(l=>l.Target==target.Serial.Value)==2,"Independent cast leases");
        int life=SkillHealth.Balance256(target);var saved=MiasmaWorld.Snapshot();var until=MiasmaWorld.For(p).Min(n=>n.UntilUtc);var next=cloudAt.AddSeconds(1);
        var r0=MiasmaWorld.TickPrepared(next,false,p.Serial.Value);Require(r0.CloudContacts==0&&SkillHealth.Balance256(target)==life&&MiasmaWorld.For(p).All(n=>n.UntilUtc==until),"Offline cloud damage/lifetime reset");
        var origin=p.Location;var map=p.Map;p.Internalize();MiasmaWorld.TickPrepared(next.AddMilliseconds(40),true,p.Serial.Value);Require(SkillHealth.Balance256(target)==life,"Facet cloud continued damage");p.MoveToWorld(origin,map);
        var resumed=MiasmaWorld.TickPrepared(next.AddMilliseconds(80),true,p.Serial.Value);Require(resumed.CloudContacts==2,"Cloud did not resume remaining lifetime");
        life=SkillHealth.Balance256(target);MiasmaWorld.TickPrepared(until,true,p.Serial.Value);Require(MiasmaWorld.For(p).Length==0&&SkillHealth.Balance256(target)==life,"Offline time reset expiry");
        Ready(p);at=TimedEffects.Now;MiasmaWorld.ReleasePrepared(p,target.Location,at);MiasmaWorld.TickPrepared(at.AddMilliseconds(40),false,p.Serial.Value);Require(MiasmaWorld.For(p).Length==0,"Offline bolt survived");
        MiasmaWorld.Restore(saved);var before=State();bool refused=false;try{MiasmaWorld.Restore(saved with{Nodes=saved.Nodes.Select((n,i)=>i==0?n with{Seed=0}:n).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&State()==before,"Invalid snapshot partially committed");refused=false;
        try{MiasmaWorld.Restore(saved with{Nodes=saved.Nodes.Select((n,i)=>i==0?n with{Cloud=n.Cloud with{Modifiers=new(-1,0,0,0,0)}}:n).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&State()==before,"Malformed cloud modifiers partly committed");MiasmaWorld.Clear(p);
        Log.Information("Miasma world:separate cast stacking,shared two-cloud lease,offline/facet suppression with continuing absolute lifetime,resume and atomic malformed-save rejection pass");
    }
    private static void Immunity(PlayerMobile p)
    {
        var t=new EldritchBlastWorldChecks.ImmuneRecipient{Name="MiasmaWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        try
        {
            Require(NamedElements.MagicResistance(t)==100,"Explicit encounter magic immunity");Ready(p);var at=TimedEffects.Now;MiasmaWorld.ReleasePrepared(p,t.Location,at);int hp=SkillHealth.Balance256(t);Grow(p,at);Require(SkillHealth.Balance256(t)==hp,"Magic immune took base magic damage");
            var save=MiasmaWorld.Snapshot();MiasmaWorld.Restore(save with{Nodes=save.Nodes.Select(n=>n with{Cloud=n.Cloud with{Modifiers=new(100,0,0,0,0)}}).ToArray()});
            var r=MiasmaWorld.TickPrepared(MiasmaWorld.For(p)[0].NextUtc.AddSeconds(1),true,p.Serial.Value);Require(r.CloudContacts==1&&SkillHealth.Balance256(t)<hp,$"Potion immune control contacts={r.CloudContacts},fault={r.Fault},before={hp},after={SkillHealth.Balance256(t)}");
        }
        finally{t.Delete();MiasmaWorld.Clear(p);}
    }
    private static void Boundary(PlayerMobile p,BaseCreature target)
    {
        var targetOrigin=target.Location;target.Internalize();Ready(p);var at=TimedEffects.Now;MiasmaWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);var end=Grow(p,at);
        Require(end==at.AddMilliseconds(2120),"Max-range expiry plus three maker steps");MiasmaWorld.Clear(p);
        int id=Enumerable.Range(0x80,0x8000).First(i=>(TileData.ItemTable[i].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[i].Height>=16);
        var walls=Enumerable.Range(-2,5).Select(_=>new Static(id)).ToArray();
        try
        {
            for(int i=0;i<walls.Length;i++){walls[i].MoveToWorld(new(p.X+5,p.Y+i-2,p.Z),p.Map);}Require(LobProjectiles.Barrier(p.Map,new(p.X+5,p.Y,p.Z)),"Fixture wall does not block missile mask");Ready(p);at=TimedEffects.Now;
            Require(MiasmaWorld.ReleasePrepared(p,new(p.X+4,p.Y,p.Z),at)>0,"Wall-bound flight");end=Grow(p,at);Require(end<at.AddMilliseconds(2120)&&MiasmaWorld.For(p).All(n=>n.X<p.X+5),$"Wall terminal at={(end-at).TotalMilliseconds}ms,nodes={JsonSerializer.Serialize(MiasmaWorld.For(p).Select(n=>new{n.Kind,X=n.X-p.X,Y=n.Y-p.Y}))}");
        }
        finally{foreach(var w in walls){w.Delete();}MiasmaWorld.Clear(p);target.MoveToWorld(targetOrigin,p.Map);}
        Log.Information("Miasma world:real magic immunity plus independent potion elements,range/wall terminal cloud creation and no terminal damage tick pass");
    }
    private static void Save(PlayerMobile p,Item helm,Item weapon,BaseCreature target)
    {
        Ready(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);long first=MiasmaWorld.ReleasePrepared(p,target.Location,at),second=MiasmaWorld.ReleasePrepared(p,target.Location,at),third=MiasmaWorld.ReleasePrepared(p,target.Location,at);
        var snapshot=MiasmaWorld.Snapshot();MiasmaWorld.Restore(snapshot with{Nodes=snapshot.Nodes.Select(n=>n.Id==first?Shift(n,1000):n.Id==second?Shift(n,40):n).ToArray()});var cloudAt=Grow(p,at);
        var nodes=MiasmaWorld.For(p);Require(nodes.Any(n=>n.Kind==MiasmaNodeKind.Bolt)&&nodes.Any(n=>n.Kind==MiasmaNodeKind.Maker)&&nodes.Count(n=>n.Kind==MiasmaNodeKind.Cloud)==2,"Save must retain all three real graph stages");
        Require(MiasmaWorld.Snapshot().Leases.Any(l=>l.Cast==third&&l.Target==target.Serial.Value&&l.UntilUtc>cloudAt),"Positive saved recipient lease");
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,weapon.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),MiasmaWorld.Snapshot())));
    }
    private static MiasmaNode Shift(MiasmaNode n,int ms)=>n with{CreatedUtc=n.CreatedUtc.AddMilliseconds(ms),NextUtc=n.NextUtc.AddMilliseconds(ms),UntilUtc=n.UntilUtc.AddMilliseconds(ms)};
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"miasma-world-reload-");
        try
        {
            var live=MiasmaWorld.Snapshot();var casts=saved.State.Nodes.Select(n=>n.Cast).ToHashSet();
            Require(live.NextId>=saved.State.NextId
                &&JsonSerializer.Serialize(live.Nodes.Where(n=>n.Owner==p.Serial.Value))==JsonSerializer.Serialize(saved.State.Nodes)
                &&JsonSerializer.Serialize(live.Leases.Where(l=>casts.Contains(l.Cast)))==JsonSerializer.Serialize(saved.State.Leases)&&p.Female&&p.Body==0x191
                &&World.FindItem((Serial)saved.Helm)?.Parent==p&&World.FindItem((Serial)saved.Weapon)?.Parent==p&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Saved graph/Witch/gear/fractional resources changed");
            var target=(BaseCreature)World.FindMobile((Serial)saved.Target);target.AIObject?.AITimer.Stop();Require(SkillHealth.Balance256(target)==saved.TargetLife,"Saved target changed");FixtureSpace.Place(p,saved.Origin,Map.Trammel);
            var next=live.Nodes.Min(n=>n.NextUtc);var before=State();MiasmaWorld.TickPrepared(next.AddTicks(-1),true,p.Serial.Value);Require(State()==before,"Early reload replayed");
            var r=MiasmaWorld.TickPrepared(next,true,p.Serial.Value);Require(r is{Fault:null,CreatedClouds:2,CloudContacts:1}&&SkillHealth.Balance256(target)<saved.TargetLife,"Original maker/cloud clocks,independent cast lease or saved damage failed to resume");
            MiasmaWorld.Restore(live);SkillHealth.SetBalance256(target,saved.TargetLife);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Require(ClassCombat.For(p).Miasma==null&&WarlockCombat.Implemented(CombatAbility.MiasmaBolt),"Reload replayed transient or lost learned admission");
            Log.Information("Miasma world:original simultaneous bolt/makers/clouds,positive lease,RNG/birth/shape/clocks,Witch/gear/FP256wounds+mana retained and continued through actual reload;future inspection clock only");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
