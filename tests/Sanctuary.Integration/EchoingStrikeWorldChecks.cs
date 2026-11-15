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

public static class EchoingStrikeWorldChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EchoingStrikeWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Weapon,uint Target,int TargetLife,Point3D Origin,EchoingWorldSave State,int Mana,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"echoing-strike-world-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        // A regeneration timer may already be scheduled before fixture naming.
        // Gate its first callback too;changing the next interval is insufficient.
        var canHp=ClassGearHooks.CanRegenerateHits;var canMp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name=="EchoingWorldFixture")&&canHp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name=="EchoingWorldFixture")&&canMp?.Invoke(m)!=false;
        Mobile.HitsRegenRateHandler=m=>m.Name=="EchoingWorldFixture"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>m.Name=="EchoingWorldFixture"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"echoing-strike-world-reload-check":"echoing-strike-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete proposed Echoing packet/return/SC/lifecycle/save rule",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="EchoingWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){t.SetResistance(r,0);}t.SetResistance(ResistanceType.Poison,100);return t;
    }
    private static void Packets(PlayerMobile p,BaseCreature target)
    {
        var original=EchoingStrikeInputs.Capture(p);int controls=0;
        foreach(int mirror in new[]{0,5,10,15,20})foreach(bool critical in new[]{false,true})
        {
            int n=mirror/5,count=1+n,scalar=(100/count)/2;
            var skill=EchoingStrikePrepared.Capture(new(SkillMechanics.For(CombatAbility.EchoingStrike),k=>k=="Echoing Strike"?1:0,k=>k=="Mirrored Blades"?mirror:0,99));
            var input=original with{Skill=skill,WeaponInputs=new(1024,2048,256,100,0,0,new(FireMin:256,FireMax:256,PoisonMin:193,PoisonMax:193,PoisonFrames:175,PoisonCount:2,LifeDrain:5),null),Slow=SlowMissilePayload.Normal};
            var packet=EchoingStrikePackets.Prepare(input,critical,true);int minimum=696+(8+2*n)*256,maximum=1624+(12+3*n)*256;
            Require(packet.BaseDamage.Min256==minimum&&packet.BaseDamage.Max256==maximum&&packet.BaseDamage.Elements is{FireMin:232,PoisonMin:174,PoisonFrames:175,PoisonCount:2,LifeDrain:5},"Source116/128,flat ledger,raw poison metadata or distinct skill-flat synergy order");
            ulong seed=SourceRandom.InitLow(73),expected=seed;int rolled=minimum+(int)SourceRandom.Limited(ref expected,maximum-minimum);
            int physical=rolled*2*(critical?2:1);physical=physical*(130+mirror*5)/100;physical=physical*scalar/100;
            var raw=EchoingStrikePackets.Roll(packet,target,ref seed,out int rate);
            Require(NamedElements.To256(raw.Physical)==physical&&NamedElements.To256(raw.Fire)==232*scalar/100&&rate==174*scalar/100
                &&raw.Poison==0&&packet.BaseDamage.Elements.PoisonImpactFrames==87,"Per-echo scalar/critical/physical ED or poison-rate placement");controls++;
        }
        Require(controls==10,"Packet control count");
        Log.Information("Echoing world:10independent packet count/critical controls,source116/flat-Damage+ exclusion,physical-only multiplicative ED and rate-only poison scaling pass");
    }
    private static void Scene(NetState network)
    {
        var p=CleaveActionChecks.Fresh();p.Name="EchoingWorldFixture";bool placed=false;
        foreach(int column in new[]{6600,6700,6800,6900,7000})
        {
            try{FixtureSpace.Corridor(p,column,60,2600);placed=true;break;}
            catch(InvalidOperationException e)when(e.Message.StartsWith("No independent fixture corridor",StringComparison.Ordinal)){}
        }
        Require(placed,"No independent long return-test corridor");
        while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Legendary Mallet strength");}
        ClassSkillChecks.Train(p,"warlock.echoingstrike",7);ClassSkillChecks.Train(p,"warlock.mirroredblades",5);ClassSkillChecks.Train(p,"warlock.bladewarp",3);ClassSkillChecks.Train(p,"warlock.hexbane");
        var old=CombatTiming.Equipped(p);p.AddToBackpack(old);var weapon=(BaseWeapon)NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Stone Crusher").Id,99,new Random(173),true);
        p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Actual weapon");
        foreach(string key in new[]{"Snakecord","Venom Grip"}){var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(47),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Actual poison source");}
        ClassGear.Refresh(p);var account=FixtureNetwork.Authenticate(network,p,"echo-world-");var target=Target(p,2);var seed=EchoingStrikeWorld.SeedLow;var block=SourceBlocking.BlockRoll;
        EchoingStrikeWorld.SeedLow=()=>73;SourceBlocking.BlockRoll=_=>99;bool kept=false;
        try
        {
            Require(EchoingStrikeWorld.Approved&&WarlockCombat.Implemented(CombatAbility.EchoingStrike)&&SkillBindings.Instance.Assign(p,true,CombatAbility.EchoingStrike),"Approved learned route unavailable");Packets(p,target);
            Ready(p);Require(HexBane.CastPrepared(p,true),"Actual live Hex buff");Ready(p);int mana=NecromancerEffects.ManaBalance256(p),durability=weapon.HitPoints;
            var pending=EchoingStrikeCasting.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);Require(pending!=null,"Prepared SC startup");var session=ClassCombat.For(p);long effect=session.EffectAt;
            Require(!ClassCombat.ResolveAction(p,effect-1)&&EchoingStrikeWorld.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Early SC debit/emission");
            ClassCombat.Stop(p);Require(pending.Cancelled&&!ClassCombat.ResolveAction(p,effect)&&EchoingStrikeWorld.For(p).Length==0,"Cancelled SC emitted");
            Ready(p);pending=EchoingStrikeCasting.BeginPrepared(p,Serial.Zero,new(p.X+14,p.Y,p.Z),Core.TickCount);Require(pending!=null,"Late mana startup");effect=session.EffectAt;NecromancerEffects.SetMana256(p,1);
            Require(!ClassCombat.ResolveAction(p,effect)&&pending.Released&&pending.Flights.Length==0,"Late mana allocated");
            Ready(p);pending=EchoingStrikeCasting.BeginPrepared(p,Serial.Zero,new(p.X+14,p.Y,p.Z),Core.TickCount);effect=session.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(!ClassCombat.ResolveAction(p,effect)&&pending.Cancelled,"Moved windup emitted");p.MoveToWorld(origin,p.Map);
            Ready(p);pending=EchoingStrikeCasting.BeginPrepared(p,Serial.Zero,new(p.X+14,p.Y,p.Z),Core.TickCount);effect=session.EffectAt;mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.ResolveAction(p,effect)&&pending.Flights.Length==2&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.EchoingStrike).Mana256,"Paid SC release/count");
            Require(!ClassCombat.ResolveAction(p,effect)&&weapon.HitPoints==durability,"Released action replayed/wore weapon");
            var flights=EchoingStrikeWorld.For(p);var at=flights[0].CreatedUtc;int life=SkillHealth.Balance256(target);var captured=flights[0].Packet;
            p.AddToBackpack(weapon);Require(p.EquipItem(old),"Post-release weapon swap");ClassSkillChecks.Train(p,"warlock.mirroredblades",6);
            var first=EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(200),true,p.Serial.Value);
            Require(first is{Fault:null}&&first.Contacts>=2&&SkillHealth.Balance256(target)<life&&life-SkillHealth.Balance256(target)<5000*256&&HexBane.Debuff(target)!=null,
                $"Native echo contacts={first.Contacts},damage256={first.Damage256},delta256={life-SkillHealth.Balance256(target)},hex={HexBane.Debuff(target)!=null},fault={first.Fault??"none"}");
            Require(EchoingStrikeWorld.For(p).All(s=>s.Packet==captured||s.Packet.Input==captured.Input)&&weapon.HitPoints==durability,"Released packet changed with weapon/training");
            var lease=EchoingStrikeWorld.Snapshot().Leases.First(l=>l.Target==target.Serial.Value);Require(!EchoingStrikeWorld.Ready(lease.Cast,lease.Target,lease.UntilUtc.AddTicks(-1))&&EchoingStrikeWorld.Ready(lease.Cast,lease.Target,lease.UntilUtc),"Exclusive per-echo20frame lease");
            Require(EchoingStrikeWorld.Snapshot().Leases.Count(l=>l.Target==target.Serial.Value)==2,"Echoes shared one recipient delay");
            EchoingStrikeWorld.Clear(p);target.Delete();p.AddToBackpack(old);Require(p.EquipItem(weapon),"Restore source weapon");Ready(p);
            MotionAndBlock(p);Save(p,weapon);kept=true;
            Log.Information("Echoing world:prepared SC cancellation/late mana/movement/once-only release,actual two-echo contacts/live Hex,independent20frame leases,no weapon wear/procs and immutable release stats pass");
        }
        finally
        {
            EchoingStrikeWorld.SeedLow=seed;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);target?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){EchoingStrikeWorld.Clear(p);p.Delete();}
        }
    }
    private static void MotionAndBlock(PlayerMobile p)
    {
        var origin=p.Location;Ready(p);var at=TimedEffects.Now;var ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);Require(ids.Length==2,"Motion fan");
        EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(800),true,p.Serial.Value);var turned=EchoingStrikeWorld.For(p);
        Require(turned.Length==2&&turned.All(s=>s.Returning&&s.OutUpdates==20&&s.OutDistance>10&&s.ReturnRemaining==s.OutDistance),"Full outbound budget/turn");
        p.MoveToWorld(new(origin.X+40,origin.Y,origin.Z),p.Map);double budget=turned[0].OutDistance;long id=turned[0].Id;double travelled=0;var previous=turned[0];
        for(int i=21;i<=40;i++)
        {
            EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(i*40),true,p.Serial.Value);var current=EchoingStrikeWorld.For(p).SingleOrDefault(s=>s.Id==id);
            if(current==null){break;}travelled+=Math.Sqrt(Math.Pow(current.X-previous.X,2)+Math.Pow(current.Y-previous.Y,2));previous=current;
        }
        Require(travelled<=budget+.00001&&EchoingStrikeWorld.For(p).Length==0,"Moving owner extended return range/lifetime");p.MoveToWorld(origin,p.Map);EchoingStrikeWorld.Clear(p);
        Ready(p);at=TimedEffects.Now;ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);var before=JsonSerializer.Serialize(EchoingStrikeWorld.Snapshot());
        Require(!EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(39),true,p.Serial.Value).Advanced&&JsonSerializer.Serialize(EchoingStrikeWorld.Snapshot())==before,"Early frame advanced");
        EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(40),false,p.Serial.Value);Require(EchoingStrikeWorld.For(p).Length==0,"Offline projectile kept fighting");
        Ready(p);at=TimedEffects.Now;ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(1640),true,p.Serial.Value);
        Require(EchoingStrikeWorld.For(p).Length==0,"Overdue load replayed expired flight");
        var skeleton=new GraveyardSkeleton{Name="EchoingWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};skeleton.SetHits(50000);skeleton.Hits=skeleton.HitsMax;skeleton.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);skeleton.AIObject?.AITimer.Stop();
        var choice=SourceBlocking.ChoiceRoll;var block=SourceBlocking.BlockRoll;SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=_=>0;
        try
        {
            Require(SourceBlocking.TryRate(skeleton,out int chance,out _)&&chance==10,"Actual shielded skeleton");int hp=SkillHealth.Balance256(skeleton);Ready(p);at=TimedEffects.Now;
            ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);var result=EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(200),true,p.Serial.Value);
            Require(result.Contacts>=2&&result.Damage256==0&&SkillHealth.Balance256(skeleton)==hp,"Automatic hit bypassed native shield block");
        }
        finally{SourceBlocking.ChoiceRoll=choice;SourceBlocking.BlockRoll=block;skeleton.Delete();EchoingStrikeWorld.Clear(p);}
        int wallId=Enumerable.Range(0x80,0x8000).First(i=>(TileData.ItemTable[i].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[i].Height>=16);
        var walls=Enumerable.Range(-2,5).Select(dy=>new Static(wallId)).ToArray();
        try
        {
            for(int i=0;i<walls.Length;i++){walls[i].MoveToWorld(new(p.X+5,p.Y+i-2,p.Z),p.Map);}
            Ready(p);at=TimedEffects.Now;ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);bool turnedAtWall=false;
            for(int frame=1;frame<=20;frame++)
            {
                EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(frame*40),true,p.Serial.Value);var current=EchoingStrikeWorld.For(p);
                Require(current.All(s=>s.X<p.X+5),"Echo crossed actual missile barrier");
                if(current.Any(s=>s.Returning)){Require(current.All(s=>s.OutUpdates<20),"Wall return waited for full outbound budget");turnedAtWall=true;break;}
            }
            Require(turnedAtWall,"Actual wall did not turn Echoing blade");
        }
        finally{foreach(var wall in walls){wall.Delete();}EchoingStrikeWorld.Clear(p);}
        Log.Information("Echoing world:source fan/full20update outbound,bounded live-owner return,early/offline/overdue expiry,real skeleton shield block and actual missile barrier return pass");
    }
    private static void Save(PlayerMobile p,BaseWeapon weapon)
    {
        Ready(p);var target=Target(p,2);var at=TimedEffects.Now.Add(FixtureSpace.Retention);var ids=EchoingStrikeWorld.ReleasePrepared(p,new(p.X+14,p.Y,p.Z),at);
        Require(ids.Length==2,"Inspection flight allocation");EchoingStrikeWorld.TickPrepared(at.AddMilliseconds(160),true,p.Serial.Value);
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);var state=EchoingStrikeWorld.Snapshot();
        Require(state.Leases.Count(l=>l.Target==target.Serial.Value)==2&&state.Leases.All(l=>l.UntilUtc>state.Flights.Min(s=>s.NextUtc)),"Positive saved per-echo leases");
        var malformed=state with{Flights=state.Flights.Select((s,i)=>i==0?s with{Seed=0}:s).ToArray()};bool refused=false;
        try{EchoingStrikeWorld.Restore(malformed);}catch(InvalidDataException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(EchoingStrikeWorld.Snapshot())==JsonSerializer.Serialize(state),"Malformed snapshot partly committed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,weapon.Serial.Value,target.Serial.Value,SkillHealth.Balance256(target),p.Location,state,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p))));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var account=FixtureNetwork.Authenticate(network,p,"echo-world-reload-");
        try
        {
            var live=EchoingStrikeWorld.Snapshot();var ids=saved.State.Flights.Select(f=>f.Id).ToHashSet();
            Require(live.NextId>=saved.State.NextId
                &&JsonSerializer.Serialize(live.Flights.Where(f=>f.Owner==p.Serial.Value))==JsonSerializer.Serialize(saved.State.Flights)
                &&JsonSerializer.Serialize(live.Leases.Where(l=>ids.Contains(l.Cast)))==JsonSerializer.Serialize(saved.State.Leases)
                &&World.FindItem((Serial)saved.Weapon)?.Parent==p&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Saved original flight/weapon/resources changed");
            var target=(BaseCreature)World.FindMobile((Serial)saved.Target);target.AIObject?.AITimer.Stop();
            Require(SkillHealth.Balance256(target)==saved.TargetLife&&saved.State.Leases.Count(l=>l.Target==saved.Target)==2,"Original saved target/positive leases");
            FixtureSpace.Place(p,saved.Origin,Map.Trammel);Require(p.Location==saved.Origin,"Restore inspection world");
            var next=saved.State.Flights.Min(s=>s.NextUtc);Require(!EchoingStrikeWorld.TickPrepared(next.AddTicks(-1),true,p.Serial.Value).Advanced,"Reload replayed a frame");
            var targetOrigin=target.Location;
            // Put the same original NPC into the very next swept cell,so an
            // absent lease would cause a real duplicate native contact.
            target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
            EchoingStrikeWorld.TickPrepared(next,true,p.Serial.Value);Require(EchoingStrikeWorld.For(p).All(s=>s.NextUtc==next.AddMilliseconds(40)),"Saved flight did not resume");
            Require(SkillHealth.Balance256(target)==saved.TargetLife&&saved.State.Leases.All(l=>!EchoingStrikeWorld.Ready(l.Cast,l.Target,next)),"Reload duplicated a leased contact");
            target.MoveToWorld(targetOrigin,p.Map);
            EchoingStrikeWorld.Restore(live);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Require(WarlockCombat.Implemented(CombatAbility.EchoingStrike)&&ClassCombat.For(p).Echoing==null,"Reload admitted learned/transient action");
            Log.Information("Echoing world:original saved flight IDs/packets/critical/RNG/source stats/clock/return budget/weapon/fractional wounds+mana retain and resume through actual reload;future inspection clock only");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
