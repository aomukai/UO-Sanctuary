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

public static class MiasmaChainWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(MiasmaChainWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,uint Weapon,uint Target,int TargetLife,Point3D Origin,int Mana,int Life,MiasmaWorldSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"miasma-chain-world-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("MiasmaChain",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("MiasmaChain",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"miasma-chain-world-reload-check":"miasma-chain-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared full Chain link/stamp/damage/lifecycle and saved graph",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x,int y=0)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="MiasmaChainTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(r,0);}return t;
    }
    private static MiasmaNode[] Fields(PlayerMobile p)=>MiasmaWorld.For(p).Where(n=>n.Cloud?.Ability==CombatAbility.MiasmaChain).ToArray();
    private static MiasmaChainStamp[] Makers(PlayerMobile p)=>MiasmaChainWorld.Stamps().Where(n=>n.Owner==p.Serial.Value).ToArray();
    private static (MiasmaChainAdvance Link,MiasmaAdvance Field) Pump(PlayerMobile p,DateTime at,bool actions=true)
    {
        var l=MiasmaChainWorld.TickPrepared(at,actions,p.Serial.Value);var f=MiasmaWorld.TickPrepared(at,actions,p.Serial.Value);
        Require(l.Fault==null&&f.Fault==null,$"Chain/field fault {l.Fault??f.Fault}");return(l,f);
    }
    private static void Arm(PlayerMobile p,DateTime at)
    {Pump(p,at.AddMilliseconds(40));Pump(p,at.AddMilliseconds(80));Pump(p,at.AddMilliseconds(540));Pump(p,at.AddMilliseconds(580));}
    private static long Release(PlayerMobile p,BaseCreature t,DateTime at)
    {Ready(p);long id=MiasmaChainWorld.ReleasePrepared(p,t.Serial,t.Location,at);Require(id>0,"Prepared Chain allocation");return id;}
    private static void Scene(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="MiasmaChainFixture";p.Female=true;p.Body=0x191;p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.miasmachain");ClassSkillChecks.Train(p,"warlock.enhancedentropy");FixtureSpace.Corridor(p,6500,60,2900);
        var weapon=new Dagger();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Original Chain dagger");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(719),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Chain soft ranks");ClassGear.Refresh(p);
        var account=FixtureNetwork.Authenticate(network,p,"chain-world-");var target=Target(p,8);var seed=MiasmaChainWorld.SeedLow;MiasmaChainWorld.SeedLow=()=>73;bool kept=false;
        try
        {
            Require(!MiasmaChainWorld.Approved&&!WarlockCombat.Implemented(CombatAbility.MiasmaChain)&&!SkillBindings.Instance.Assign(p,true,CombatAbility.MiasmaChain),"Prepared Chain admitted public route");
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var a=MiasmaChainCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);Require(a!=null,"Chain SC preparation");
            var session=ClassCombat.For(p);long effect=session.EffectAt;Require(session.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Prepared explicit SC/FCR choice");
            Require(!ClassCombat.ResolveAction(p,effect-1)&&NecromancerEffects.ManaBalance256(p)==mana&&MiasmaChainWorld.For(p).Length==0,"Early SC release");ClassCombat.Stop(p);
            Require(a.Cancelled&&!ClassCombat.ResolveAction(p,effect),"Cancelled SC emitted");
            Ready(p);a=MiasmaChainCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=session.EffectAt;NecromancerEffects.SetMana256(p,1);
            Require(!ClassCombat.ResolveAction(p,effect)&&a.Released&&MiasmaChainWorld.For(p).Length==0,"Late mana allocated");
            Ready(p);a=MiasmaChainCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=session.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,effect)&&a.Cancelled,"Moved SC emitted");p.MoveToWorld(origin,p.Map);
            Ready(p);a=MiasmaChainCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=session.EffectAt;p.AddToBackpack(weapon);
            Require(!ClassCombat.ResolveAction(p,effect)&&a.Cancelled,"Weapon-change SC emitted");Require(p.EquipItem(weapon),"Restore dagger");
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);a=MiasmaChainCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);effect=session.EffectAt;
            Require(ClassCombat.ResolveAction(p,effect)&&a.Chain>0&&a.ManaPaid256==1280&&NecromancerEffects.ManaBalance256(p)==mana-a.ManaPaid256&&!ClassCombat.ResolveAction(p,effect),"One SC release/payment");
            var chain=MiasmaChainWorld.For(p).Single();Require(chain is{Maker:true,Attached:false}&&Fields(p).Length==0,"Release skipped711maker");var at=chain.CreatedUtc;var birth=chain.Birth;int wear=weapon.HitPoints,hp=SkillHealth.Balance256(target);
            p.AddToBackpack(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.enhancedentropy",10);
            var r=Pump(p,at.AddMilliseconds(40));Require(r.Link is{Updates:1,Stamps:1,Clouds:0}&&MiasmaChainWorld.For(p).Single()is{Maker:false,Attached:false}&&Makers(p).Length==1&&Fields(p).Length==0,"711→712/713stage order");
            r=Pump(p,at.AddMilliseconds(80));Require(r.Link.Clouds==1&&Fields(p).Length==1&&Fields(p)[0].Cloud.Link is{} initial&&initial.EndX==origin.X&&initial.EndY==origin.Y,"713→696initial stamp");
            Pump(p,at.AddMilliseconds(400));r=Pump(p,at.AddMilliseconds(540));Require(r.Link.Stamps==1&&MiasmaChainWorld.For(p).Single().Attached&&SkillHealth.Balance256(target)<hp,"Head did not attach/damage once");
            int directLife=SkillHealth.Balance256(target);r=Pump(p,at.AddMilliseconds(580));Require(r.Link.Clouds==1&&r.Field.CloudContacts==1&&SkillHealth.Balance256(target)<directLife,"Full linked cloud did not damage primary");
            Require(Fields(p).All(n=>n.Cloud.Shape==birth.Numbers.Shape&&n.Cloud.Rank==birth.Numbers.Rank)&&target.Poison==null&&weapon.HitPoints==wear,"Snapshot/poison/weapon semantics");
            var lease=MiasmaWorld.Snapshot().Leases.Single(l=>l.Cast==chain.Id&&l.Target==target.Serial.Value);Require(lease.UntilUtc==at.AddMilliseconds(1580),"Per-chain lease origin");
            hp=SkillHealth.Balance256(target);r=Pump(p,at.AddMilliseconds(1040));Require(r.Link.Stamps==1&&r.Field.CloudContacts==0&&SkillHealth.Balance256(target)==hp,"Next stamp bypassed chain lease");
            r=Pump(p,at.AddMilliseconds(1080));Require(r.Link.Clouds==1&&r.Field.CloudContacts==0&&Fields(p).Length==3,"Separate stamps double-hit");
            var entrant=Target(p,4);try{r=Pump(p,at.AddMilliseconds(1120));Require(r.Field.CloudContacts==1&&SkillHealth.Balance256(entrant)<30000*256,"New entrant failed corridor contact");}finally{entrant.Delete();}
            hp=SkillHealth.Balance256(target);r=Pump(p,at.AddMilliseconds(1580));Require(r.Field.CloudContacts==1&&SkillHealth.Balance256(target)<hp,"One-second chain repeat");
            var old=Fields(p).Select(n=>n.Cloud.Link).ToArray();target.MoveToWorld(new(p.X+8,p.Y+2,p.Z),p.Map);Pump(p,at.AddMilliseconds(2040));Pump(p,at.AddMilliseconds(2080));
            Require(MiasmaChainWorld.For(p).Single().HeadY==target.Y&&Fields(p).Any(n=>n.Cloud.Link.EndY==target.Y)&&old.All(n=>n.EndY==origin.Y),"Moving target rewrote prior stamps or failed live link");
            Require(p.EquipItem(helm),"Restore snapshot helm");ClassGear.Refresh(p);MiasmaWorld.Clear(p);target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);
            Capacity(p,target);TimingAndClip(p,target);MotionAndLifecycle(p,target,weapon);Immunity(p,target);Save(p,helm,weapon,target);kept=true;
            Log.Information("Chain world:prepared SC/cancel/movement/weapon/late mana/one payment,711→712→713→696graph,real head+cloud damage,half-second stamps,per-chain lease/entrant and immutable rank/shape pass");
        }
        finally
        {
            MiasmaChainWorld.SeedLow=seed;ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){target.Delete();MiasmaWorld.Clear(p);p.Delete();}
        }
    }
    private static void Capacity(PlayerMobile p,BaseCreature target)
    {
        var at=TimedEffects.Now;long first=Release(p,target,at),second=Release(p,target,at);Pump(p,at.AddMilliseconds(540));var r=Pump(p,at.AddMilliseconds(580));
        Require(r.Field.CloudContacts==2&&MiasmaWorld.Snapshot().Leases.Count(l=>l.Target==target.Serial.Value&&new[]{first,second}.Contains(l.Cast))==2,"Independent chains shared recipient delay");
        MiasmaWorld.Clear(p);long oldest=Release(p,target,at);Arm(p,at);var retained=Fields(p);for(int i=1;i<5;i++){Release(p,target,at.AddMilliseconds(i));}var old=MiasmaChainWorld.For(p);Require(old.Length==5&&retained.Length>0,"Rank2 cap with emitted fields");
        NecromancerEffects.SetMana256(p,1);long next=MiasmaWorld.NextIdentity;Require(MiasmaChainWorld.ReleasePrepared(p,target.Serial,target.Location,at)==0&&MiasmaWorld.NextIdentity==next&&MiasmaChainWorld.For(p).SequenceEqual(old),"Unpaid cast replaced/allocated");
        Release(p,target,at.AddMilliseconds(10));Require(MiasmaChainWorld.For(p).Length==5&&!MiasmaChainWorld.For(p).Any(n=>n.Id==old[0].Id)&&MiasmaChainWorld.For(p).Any(n=>n.Id==old[1].Id),"Paid FIFO cap replacement");Require(retained.All(n=>Fields(p).Any(f=>f.Id==n.Id&&f.UntilUtc==n.UntilUtc)),"Replacement recalled or renewed prior stamps");
        MiasmaWorld.Clear(p);ClassSkillChecks.Train(p,"warlock.miasmachain",20);for(int i=0;i<11;i++){Release(p,target,at.AddMilliseconds(i));}Require(MiasmaChainWorld.For(p).Length==10,"High-rank chain hard cap");MiasmaWorld.Clear(p);
        Log.Information("Chain world:separate-chain damage/leases,5/10active caps,paid oldest-first replacement and failed-payment identity/roster preservation pass");
    }
    private static void TimingAndClip(PlayerMobile p,BaseCreature target)
    {
        var at=TimedEffects.Now;Release(p,target,at);int stamps=0;
        for(int i=0;i<10;i++)
        {var due=at.AddMilliseconds(40+i*500);stamps+=Pump(p,due).Link.Stamps;Pump(p,due.AddMilliseconds(40));}
        int hp=SkillHealth.Balance256(target);var r=Pump(p,at.AddMilliseconds(5040));
        Require(stamps==10&&r.Link.Stamps==0&&MiasmaChainWorld.For(p).Length==0&&SkillHealth.Balance256(target)==hp,"Five-second link stamps/half-open expiry");MiasmaWorld.Clear(p);
        Release(p,target,at);r=Pump(p,at.AddMilliseconds(4000));Require(r.Link.Stamps==1&&Fields(p).Length==0,"Late update backfilled old stamp fields");
        Pump(p,at.AddMilliseconds(4040));Pump(p,at.AddMilliseconds(4080));var save=MiasmaWorld.Snapshot();
        MiasmaWorld.Restore(save with{Nodes=save.Nodes.Select(n=>n.Owner==p.Serial.Value?n with{Cloud=n.Cloud with{Slow=new SlowMissilePayload(100,0)}}:n).ToArray()});
        hp=SkillHealth.Balance256(target);r=Pump(p,at.AddMilliseconds(5040));Require(r.Field.CloudContacts==1&&r.Field.Damage256==0&&SkillHealth.Balance256(target)==hp
            &&MiasmaChainWorld.For(p).Length==0&&Fields(p).Length>0,"Captured cloud slow or independent post-link lifetime");
        Pump(p,Fields(p).Max(n=>n.UntilUtc));Require(Fields(p).Length==0&&SkillHealth.Balance256(target)==hp,"Lingering fields hit on expiry");MiasmaWorld.Clear(p);
        var origin=p.Location;Release(p,target,at);Arm(p,at);p.MoveToWorld(new(origin.X+32,origin.Y,origin.Z),p.Map);
        Pump(p,at.AddMilliseconds(1040));Pump(p,at.AddMilliseconds(1080));
        Require(MiasmaChainWorld.For(p).Length==1&&Fields(p).Any(n=>n.X==p.X&&n.Cloud.Link.EndX>target.X
            &&Math.Abs(MiasmaWorld.LinkLength(n.X,n.Y,n.Cloud.Link.EndX,n.Cloud.Link.EndY)-40)<.0001),"Teleport clipping failed raw40link length");
        p.MoveToWorld(origin,p.Map);MiasmaWorld.Clear(p);
        Log.Information("Chain world:ten half-second stamps/five-second link,no terminal stamp,one late stamp without backfill,captured cloud Slow Missiles,independent lingering expiry and40source-unit teleport clipping pass");
    }
    private static void MotionAndLifecycle(PlayerMobile p,BaseCreature target,Item weapon)
    {
        var origin=p.Location;var at=TimedEffects.Now;Release(p,target,at);Arm(p,at);var birth=MiasmaChainWorld.For(p).Single();var fields=Fields(p);int hp=SkillHealth.Balance256(target);
        p.MoveToWorld(new(origin.X-4,origin.Y,origin.Z),p.Map);Pump(p,at.AddMilliseconds(1040));Require(MiasmaChainWorld.For(p).Length==1,"Same-facet teleport broke link");Pump(p,at.AddMilliseconds(1080));
        Require(Fields(p).Any(n=>n.X==p.X)&&fields.All(n=>n.X==origin.X)&&birth.UntilUtc==MiasmaChainWorld.For(p).Single().UntilUtc,"Teleport reset lifetime or moved old stamps");p.MoveToWorld(origin,p.Map);MiasmaWorld.Clear(p);
        Release(p,target,at);Arm(p,at);hp=SkillHealth.Balance256(target);var until=Fields(p).Max(n=>n.UntilUtc);Pump(p,at.AddMilliseconds(1040),false);
        Require(MiasmaChainWorld.For(p)is[{} parked]&&parked.UntilUtc==birth.UntilUtc&&Makers(p).Length==0&&Fields(p).Max(n=>n.UntilUtc)==until&&SkillHealth.Balance256(target)==hp,"Offline link/field lifetime or damage");
        var map=p.Map;p.Internalize();Pump(p,at.AddMilliseconds(1080));Require(SkillHealth.Balance256(target)==hp,"Facet field kept fighting");p.MoveToWorld(origin,map);
        Pump(p,at.AddMilliseconds(1120));Require(MiasmaChainWorld.For(p)is[{} resumed]&&resumed.UntilUtc==birth.UntilUtc,"Parked link did not resume remaining lifetime");
        p.Internalize();Pump(p,birth.UntilUtc>until?birth.UntilUtc:until);Require(MiasmaChainWorld.For(p).Length==0&&Fields(p).Length==0&&SkillHealth.Balance256(target)==hp,"Inactive expiry hit or renewed");p.MoveToWorld(origin,map);MiasmaWorld.Clear(p);
        Release(p,target,at);Arm(p,at);var destination=new Point3D(p.X+16,p.Y,p.Z);var town=new GuardedRegion("ChainTown"+p.Serial.Value,p.Map,1000,new Rectangle2D(destination.X-2,destination.Y-2,5,5));town.Register();
        try{p.MoveToWorld(destination,p.Map);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);Pump(p,at.AddMilliseconds(620));
            Require(MinionSafeAreas.Contains(p)&&MiasmaChainWorld.For(p).Length==1&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life,"Safe-area parking refunded/healed");}
        finally{p.MoveToWorld(origin,p.Map);town.Unregister();MiasmaWorld.Clear(p);}
        target.Internalize();var dying=Target(p,8);
        try
        {
            Release(p,dying,at);Arm(p,at);var end=MiasmaChainWorld.For(p).Single();dying.Kill();Pump(p,at.AddMilliseconds(1040));
            Require(MiasmaChainWorld.For(p).Single().HeadX==end.HeadX&&MiasmaChainWorld.For(p).Single().HeadY==end.HeadY,"Dead target ended or moved retained endpoint");
        }
        finally{dying.Delete();MiasmaWorld.Clear(p);target.MoveToWorld(new(origin.X+8,origin.Y,origin.Z),p.Map);}
        int wallId=Enumerable.Range(0x80,0x8000).First(i=>(TileData.ItemTable[i].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[i].Height>=16);var walls=Enumerable.Range(-2,5).Select(_=>new Static(wallId)).ToArray();
        try
        {
            target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);Release(p,target,at);for(int i=0;i<walls.Length;i++){walls[i].MoveToWorld(new(p.X+5,p.Y+i-2,p.Z),p.Map);}target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);
            Pump(p,at.AddMilliseconds(580));Require(MiasmaChainWorld.For(p).Length==0&&Fields(p).All(n=>n.Cloud.Link.EndX<p.X+5),"Head crossed wall or failed invalid-location cut");
        }
        finally{foreach(var wall in walls){wall.Delete();}MiasmaWorld.Clear(p);}
        Release(p,target,at);p.AddToBackpack(weapon);if(p.FindItemOnLayer(Layer.Helm)is{} head){p.AddToBackpack(head);}Ready(p);p.Combatant=null;
        // End this fixture's combat before the restricted history helper;
        // gameplay still enforces its ordinary equipment/combat cooldowns.
        foreach(var info in p.Aggressed.ToArray()){p.RemoveAggressed(info.Defender);info.Defender.RemoveAggressor(p);}
        foreach(var info in p.Aggressors.ToArray()){p.RemoveAggressor(info.Attacker);info.Attacker.RemoveAggressed(p);}
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Sorceress)&&MiasmaChainWorld.For(p).Length==0,"Class switch retained Chain");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Warlock)&&p.EquipItem(weapon),"Restore Chain class/weapon");Release(p,target,at);p.Kill();Require(!p.Alive&&MiasmaChainWorld.For(p).Length==0&&Fields(p).Length==0,"Death retained graph");p.Resurrect();
        if(weapon.Parent!=p){p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Restore original dagger after death");}Ready(p);
        Log.Information("Chain world:live endpoint/fixed prior stamps,same-facet teleport,offline/facet/safe-area parking/resume/no reset,dead target freeze,wall cut and actual class/death cleanup pass");
    }
    private static void Immunity(PlayerMobile p,BaseCreature other)
    {
        other.Internalize();var t=new EldritchBlastWorldChecks.ImmuneRecipient{Name="MiasmaChainImmune",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(30000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        try
        {
            Require(NamedElements.MagicResistance(t)==100,"Native explicit magic immunity");var at=TimedEffects.Now;Release(p,t,at);int hp=SkillHealth.Balance256(t);Arm(p,at);Require(SkillHealth.Balance256(t)==hp,"Immune recipient took base magic");
            var save=MiasmaWorld.Snapshot();MiasmaWorld.Restore(save with{Nodes=save.Nodes.Select(n=>n.Owner==p.Serial.Value?n with{Cloud=n.Cloud with{Modifiers=new(100,0,0,0,0)}}:n).ToArray()});
            var r=Pump(p,at.AddMilliseconds(1580));Require(r.Field.CloudContacts==1&&SkillHealth.Balance256(t)<hp,"Potion components did not bypass own magic immunity through separate resistance");
        }
        finally{t.Delete();MiasmaWorld.Clear(p);other.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);}
    }
    private static void Save(PlayerMobile p,Item helm,Item weapon,BaseCreature target)
    {
        if(helm.Parent!=p){p.AddToBackpack(helm);Require(p.EquipItem(helm),"Restore original helm after death");ClassGear.Refresh(p);}
        var at=TimedEffects.Now.Add(FixtureSpace.Retention);long cast=Release(p,target,at);Pump(p,at.AddMilliseconds(40));Pump(p,at.AddMilliseconds(80));Pump(p,at.AddMilliseconds(540));Pump(p,at.AddMilliseconds(580));Pump(p,at.AddMilliseconds(1040));
        var state=MiasmaWorld.Snapshot();Require(MiasmaChainWorld.For(p)is[{Maker:false,Attached:true}]&&Makers(p).Length==1&&Fields(p).Length==2
            &&state.Leases.Any(l=>l.Cast==cast&&l.Target==target.Serial.Value&&l.UntilUtc>at.AddMilliseconds(1040)),"Saved graph must include link,713maker,two stamps and positive lease");
        string before=JsonSerializer.Serialize(state);bool refused=false;
        try{MiasmaWorld.Restore(state with{ChainMakers=state.ChainMakers.Select((n,i)=>i==0?n with{Seed=0}:n).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Malformed Chain stage partially committed root graph");
        refused=false;try{MiasmaWorld.Restore(state with{ChainMakers=state.ChainMakers.Select((n,i)=>i==0?n with{NextUtc=DateTime.SpecifyKind(n.NextUtc,DateTimeKind.Unspecified)}:n).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Non-UTC maker clock partly committed graph");
        refused=false;try{MiasmaWorld.Restore(state with{Chains=state.Chains.Select((n,i)=>i==0?n with{Id=state.Nodes[0].Id}:n).ToArray()});}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Cross-pool duplicate ID partly restored");
        MiasmaWorld.Restore(JsonSerializer.Deserialize<MiasmaWorldSave>("{\"Nodes\":[],\"Leases\":[],\"NextId\":"+state.NextId+"}"));
        Require(MiasmaChainWorld.Snapshot().Length==0&&MiasmaChainWorld.Stamps().Length==0,"Legacy three-field graph did not load");MiasmaWorld.Restore(state);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,weapon.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),state)));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"chain-world-reload-");
        try
        {
            var live=MiasmaWorld.Snapshot();var casts=saved.State.Chains.Where(n=>n.Owner==p.Serial.Value).Select(n=>n.Id).ToHashSet();
            Require(live.NextId>=saved.State.NextId&&JsonSerializer.Serialize(live.Nodes.Where(n=>n.Owner==p.Serial.Value))==JsonSerializer.Serialize(saved.State.Nodes.Where(n=>n.Owner==p.Serial.Value))
                &&JsonSerializer.Serialize(MiasmaChainWorld.For(p))==JsonSerializer.Serialize(saved.State.Chains.Where(n=>n.Owner==p.Serial.Value))
                &&JsonSerializer.Serialize(Makers(p))==JsonSerializer.Serialize(saved.State.ChainMakers.Where(n=>n.Owner==p.Serial.Value))
                &&JsonSerializer.Serialize(live.Leases.Where(n=>casts.Contains(n.Cast)))==JsonSerializer.Serialize(saved.State.Leases.Where(n=>casts.Contains(n.Cast)))
                &&p.Female&&World.FindItem((Serial)saved.Helm)?.Parent==p&&World.FindItem((Serial)saved.Weapon)?.Parent==p&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Saved original graph/owner/gear/resources changed");
            var target=(BaseCreature)World.FindMobile((Serial)saved.Target);target.AIObject?.AITimer.Stop();Require(SkillHealth.Balance256(target)==saved.TargetLife,"Saved target changed");FixtureSpace.Place(p,saved.Origin,Map.Trammel);
            var next=Makers(p).Single().NextUtc;var earliest=MiasmaWorld.For(p).Select(n=>n.NextUtc).Concat(MiasmaChainWorld.For(p).Select(n=>n.NextUtc)).Concat(Makers(p).Select(n=>n.NextUtc)).Min();
            string before=JsonSerializer.Serialize(MiasmaWorld.Snapshot());Pump(p,earliest.AddTicks(-1));Require(JsonSerializer.Serialize(MiasmaWorld.Snapshot())==before,"Reload replayed before earliest stage deadline");
            var r=Pump(p,next);Require(r.Link.Clouds==1&&r.Field.CloudContacts==0&&Fields(p).Length==3&&SkillHealth.Balance256(target)==saved.TargetLife,"Saved maker did not resume or stamp bypassed retained lease");
            var expiry=live.Leases.Single(l=>casts.Contains(l.Cast)&&l.Target==target.Serial.Value).UntilUtc;r=Pump(p,expiry);
            Require(r.Field.CloudContacts==1&&SkillHealth.Balance256(target)<saved.TargetLife,"Saved one-second lease failed to reopen for one shared contact");
            MiasmaWorld.Restore(live);SkillHealth.SetBalance256(target,saved.TargetLife);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Require(ClassCombat.For(p).MiasmaChain==null&&!WarlockCombat.Implemented(CombatAbility.MiasmaChain),"Reload admitted learned/transient Chain");
            Log.Information("Chain world:original saved link/713maker/fixed clouds/positive lease/RNG/clocks/Witch/gear/FP256wounds+mana retain and resume through actual reload;legacy optional graph loads atomically;future inspection clock only");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
