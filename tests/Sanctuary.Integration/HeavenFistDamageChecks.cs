// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class HeavenFistDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HeavenFistDamageChecks));
    public sealed class Recipient:Ogre
    {
        internal int Notifications,LifeAtNotification;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool willKill)
        {Notifications++;LifeAtNotification=SkillHealth.Balance256(this);base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private sealed record Saved(uint Owner,uint Target,int Mana,int Life,int TargetLife,int Generation,HeavenStrike Strike);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"heaven-fist-damage-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("heaven-fist-damage-check",Check);Register("heaven-fist-damage-reload-check",()=>FixtureNetwork.Connected("heaven-fist-damage-reload-result",Reload));
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)&&World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Target) is BaseCreature t){t.Frozen=t.CantWalk=true;t.AIObject.AITimer.Stop();}};
        foreach(string mode in new[]{"left","right","quick"}){string label="heaven-fist-damage-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"FoH typed delayed damage,impact potion and original saved strike",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=BlessedHammerDamageChecks.Owner();Require(SkillBindings.Instance.Assign(p,true,CombatAbility.FistOfTheHeavens)&&SkillBindings.Instance.Assign(p,false,CombatAbility.FistOfTheHeavens)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.FistOfTheHeavens),"FoH receiver bindings");return p;
    }
    private static Recipient Target(PlayerMobile p)
    {
        var t=new Recipient{NoKillAwards=true,Frozen=true,CantWalk=true};t.SetHits(30000);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);
        Require(PaladinCombat.ValidTarget(p,CombatAbility.FistOfTheHeavens,t.Serial,t.Location),"FoH fixture target must be eligible and visible");return t;
    }
    private static HeavenStrike Create(PlayerMobile p,Mobile t)
    {HeavenFists.Cast(p,t,SkillMechanics.Values(p,CombatAbility.FistOfTheHeavens));return HeavenFists.Snapshot().Where(f=>f.Owner==p.Serial.Value).MaxBy(f=>f.Id);}
    private static void Clear(PlayerMobile p)
    {HeavenFists.Restore(HeavenFists.Snapshot().Where(f=>f.Owner!=p.Serial.Value).ToArray());HolyBolts.Restore(HolyBolts.Snapshot().Where(f=>f.Owner!=p.Serial.Value).ToArray());}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {Clear(p);AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void Check()
    {
        // First regression only uses existing parent APIs.
        var p=Owner();var t=Target(p);
        try{Require(CombatPotions.Apply(p,3),"Actual FoH launch potion");var f=Create(p,t);Require(f.Min256==41088&&f.Max256==54784,"FoH incorrectly snapshots potion as extra original lightning at launch");}
        finally{Clean(p,t);}
        Details();Log.Information("FoH damage:impact-time four lanes;captured lightning/slow/rank;ordered native notification/HP/credit/death;no weapon leech/Thorns;atomic/legacy restore;3pre-roll refusals;original paid delayed strike staged");
    }
    private static void Details()
    {
        var seed=HeavenFistDamage.SeedLow;var roll=HeavenFists.Roll;
        try{HeavenFistDamage.SeedLow=()=>0;HeavenFists.Roll=_=>throw new InvalidOperationException("Typed FoH used compatibility RNG");Potions();Snapshots();Atomic();Refusals();SpellAndDeath();Lifecycle();Save();}
        finally{HeavenFistDamage.SeedLow=seed;HeavenFists.Roll=roll;}
    }
    private static void Potions()
    {
        foreach(string mode in new[]{"active","after-launch","removed","expired","mixed","immune"})
        {
            var p=Owner();var t=Target(p);
            try
            {
                if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"FoH potion "+mode);}var f=Create(p,t) with{Min256=25600,Max256=25600};
                Require(f.Captured is{Modifiers.Potion:0},"FoH missing non-potion capture");
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"FoH impact potion");}
                if(mode=="removed"){TimedEffects.Clear(p);}if(mode=="expired"){TimedEffects.Settle(p,TimedEffects.Now.AddMinutes(2));}
                if(mode=="mixed"){t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,100);t.SetResistance(ResistanceType.Energy,-50);}
                if(mode=="immune"){t.SetResistance(ResistanceType.Energy,100);}int life=SkillHealth.Balance256(t);HeavenFists.Impact(p,f);
                int expected=mode is "removed" or "expired"?25600:mode=="mixed"?46080:mode=="immune"?7680:35840;
                Require(life-SkillHealth.Balance256(t)==expected&&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==1
                    &&t.FindDamageEntryFor(p)?.Damager==p&&!TimedEffects.Snapshot().Any(s=>s.Owner==t.Serial.Value&&s.PoisonClock!=null),"FoH potion components/order "+mode+" loss="+(life-SkillHealth.Balance256(t)));
                Require(HolyBolts.Snapshot().Any(f=>f.Owner==p.Serial.Value),"Typed primary lost following secondary factory");
            }
            finally{Clean(p,t);}
        }
    }
    private static void Snapshots()
    {
        var p=Owner();var t=Target(p);
        try
        {
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            var f=Create(p,t);Require(f.Min256==20544&&f.Max256==27392&&f.Captured.Slow==new SlowMissilePayload(100,50),"Stationary FoH slow damage snapshot");AmazonSlowMissiles.Clear(p);
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"paladin.holyshock",out _)&&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.fistoftheheavens",out _),"Later FoH rank/synergy");
            int life=SkillHealth.Balance256(t);HeavenFists.Impact(p,f);Require(life-SkillHealth.Balance256(t)==21210,"FoH recaptured rank/synergy/slow or changed first seeded roll");
        }
        finally{Clean(p,t);}
    }
    private static void Atomic()
    {
        var p=Owner();var t=Target(p);
        try
        {
            var f=Create(p,t);var all=HeavenFists.Snapshot();bool refused=false;
            try{HeavenFists.Restore(all.Append(f with{Id=f.Id+1000000,Captured=f.Captured with{Slow=new(101,100)}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HeavenFists.Snapshot().SequenceEqual(all),"Malformed FoH tail partially replaced pool");refused=false;
            try{HeavenFists.Restore(all.Append(f).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HeavenFists.Snapshot().SequenceEqual(all),"Duplicate FoH partially replaced pool");
            var old=f with{Captured=null};HeavenFists.Restore(all.Select(x=>x.Id==f.Id?old:x).ToArray());Require(JsonSerializer.Serialize(old).IndexOf("Captured",StringComparison.Ordinal)<0,"Legacy FoH gained guessed capture");
            f=f with{ImpactUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};HeavenFists.Restore(all.Select(x=>x.Id==f.Id?f:x).ToArray());t.Delete();HeavenFists.RestoreReferences();
            Require(HeavenFists.Snapshot().Single(x=>x.Id==f.Id).Captured.TargetLost,"Missing FoH target kept reusable identity");
        }
        finally{Clean(p,t);}
    }
    private static void Refusals()
    {
        foreach(string mode in new[]{"paragon","uninterruptible","evilomen"})
        {
            var p=Owner();var t=Target(p);
            try
            {
                var f=Create(p,t);if(mode=="paragon"){t.IsParagon=true;}if(mode=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(mode=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}int life=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);bool refused=false;
                try{HeavenFists.Impact(p,f);}catch(NotSupportedException){refused=true;}
                Require(refused&&SkillHealth.Balance256(t)==life&&NativeMonsterReturn.Session(t).Packets==0&&HeavenFistDamage.Supported(p,t,out _)==false,"FoH unsupported target mutated "+mode);
                Require(!PaladinCombat.Release(p,CombatAbility.FistOfTheHeavens,t.Serial,t.Location)&&NecromancerEffects.ManaBalance256(p)==mana,"FoH unsupported release paid "+mode);
            }
            finally{MonsterUninterruptibles.Clear(t);Clean(p,t);}
        }
    }
    private static void SpellAndDeath()
    {
        var p=Owner();var t=Target(p);var created=CorpseRecoveryHooks.Created;bool died=false;
        try
        {
            PoisonDaggerChecks.Named(p,"Greyform");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int life=SkillHealth.Balance256(p);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));var f=Create(p,t) with{Min256=25600,Max256=25600};SkillHealth.SetBalance256(t,256);
            CorpseRecoveryHooks.Created=corpse=>{if(corpse.Owner==t){died=SkillHealth.Balance256(t)==0&&t.FindDamageEntryFor(p)?.Damager==p;}created?.Invoke(corpse);};HeavenFists.Impact(p,f);
            Require(!t.Alive&&died&&ClassLoot.Killer(t)==p&&SkillHealth.Balance256(p)==life&&NativeMonsterReturn.Session(t).Packets==1,"FoH ordered death/credit or forbidden weapon leech/Thorns");
        }
        finally{CorpseRecoveryHooks.Created=created;PaladinAuras.Clear(t);Clean(p,t);}
    }
    private static void Lifecycle()
    {
        foreach(string mode in new[]{"offline","internal","facet","death","respec","wardrobe","overdue"})
        {
            var p=Owner();var t=Target(p);
            try
            {
                var f=Create(p,t) with{ImpactUtc=TimedEffects.Now.AddMilliseconds(-1)};
                HeavenFists.Restore(HeavenFists.Snapshot().Select(x=>x.Id==f.Id?f:x).ToArray());int life=SkillHealth.Balance256(t);
                if(mode=="internal"){p.Internalize();}if(mode=="facet"){p.MoveToWorld(p.Location,Map.Felucca);}
                if(mode=="death"){p.Kill();Require(!HeavenFists.Snapshot().Any(x=>x.Id==f.Id),"Host death cleanup retained typed FoH");}
                if(mode=="respec"){ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(!HeavenFists.Snapshot().Any(x=>x.Id==f.Id),"Respec retained typed FoH");}
                if(mode=="wardrobe"){Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Druid,out _,out _)&&!HeavenFists.Snapshot().Any(x=>x.Id==f.Id),"Wardrobe retained typed FoH");}
                if(mode=="overdue"){HeavenFists.RestoreReferences();}else{HeavenFists.Tick();}
                Require(!HeavenFists.Snapshot().Any(x=>x.Id==f.Id)&&SkillHealth.Balance256(t)==life&&!HolyBolts.Snapshot().Any(x=>x.Owner==p.Serial.Value),"FoH host lifecycle/offline replay "+mode);
            }
            finally{Clean(p,t);}
        }
        var owner=Owner();var first=Target(owner);var other=Target(owner);other.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);
        try
        {
            var f=Create(owner,first);int life=SkillHealth.Balance256(first);Require(first.SetControlMaster(owner),"FoH friendly primary fixture");HeavenFists.Impact(owner,f);
            Require(SkillHealth.Balance256(first)==life&&HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Friendly primary hurt or suppressed burst");first.SetControlMaster(null);Clear(owner);
            f=Create(owner,first);first.Damage(first.Hits+1,owner);CombatDamage.Finish(first,owner);var dead=HeavenFists.Snapshot().Single(x=>x.Id==f.Id);
            Require(dead.Captured.TargetDead&&World.FindItem((Serial)dead.Corpse) is Corpse,"FoH captured death identity");int otherLife=SkillHealth.Balance256(other);
            HeavenFists.Impact(owner,dead with{Target=other.Serial.Value});Require(SkillHealth.Balance256(other)==otherLife&&HolyBolts.Snapshot().Any(x=>x.Owner==owner.Serial.Value),"Dead FoH identity alias damaged unrelated live actor");
        }
        finally{Clean(owner,first,other);}
    }
    private static void Save()
    {
        var p=Owner();var t=Target(p);NecromancerEffects.SetMana256(p,6417);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-7*256-17);
        Require(PaladinCombat.Release(p,CombatAbility.FistOfTheHeavens,t.Serial,t.Location),"Original paid FoH release");var f=HeavenFists.Snapshot().Single(f=>f.Owner==p.Serial.Value) with{ImpactUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        HeavenFists.Restore(HeavenFists.Snapshot().Select(x=>x.Id==f.Id?f:x).ToArray());Require(NecromancerEffects.ManaBalance256(p)==17,"FoH25mana once");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,17,SkillHealth.Balance256(p),SkillHealth.Balance256(t),0,f)));p.Internalize();
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(Recipient)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&HeavenFists.Snapshot().Single(f=>f.Id==s.Strike.Id)==s.Strike&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.TargetLife,"Original FoH source/target/capture/resources changed");
        var account=FixtureNetwork.Authenticate(network,p,"foh-source-reload-");
        if(s.Generation==0)
        {
            // Even an inspection-only reload authenticates the fixture socket;
            // leaving it unseeded correctly triggers the server's denylist.
            network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=1}));Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Log.Information("heaven-fist-damage-reload-result: passed"));return;
        }
        p.MoveToWorld(new(s.Strike.X-3,s.Strike.Y,s.Strike.Z),Map.Maps[s.Strike.Map]);
        var f=s.Strike with{ImpactUtc=TimedEffects.Now.AddMilliseconds(400)};HeavenFists.Restore(HeavenFists.Snapshot().Select(x=>x.Id==f.Id?f:x).ToArray());long deadline=Core.TickCount+6000;
        void Observe()
        {
            try
            {
                if(NativeMonsterReturn.Session(t).Packets==0){Require(Core.TickCount<deadline,$"Natural FoH impact timeout:exists={HeavenFists.Snapshot().Any(x=>x.Id==f.Id)},ownerMap={p.Map?.MapID},targetMap={t.Map?.MapID},connected={p.NetState?.Running}");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(s.TargetLife-SkillHealth.Balance256(t)==41754&&t.Notifications==1&&!HeavenFists.Snapshot().Any(x=>x.Id==f.Id)&&NecromancerEffects.ManaBalance256(p)==17&&SkillHealth.Balance256(p)==s.Life,"Natural original delayed FoH damage/consume/resources");
                Require(HolyBolts.Snapshot().Any(x=>x.Owner==p.Serial.Value),"Natural FoH lost child factory");Clear(p);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=2,TargetLife=SkillHealth.Balance256(t)}));Log.Information("heaven-fist-damage-reload-result: passed");
            }
            catch(Exception e){p.Internalize();Log.Error(e,"heaven-fist-damage-reload-result: failed");}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p);var account=FixtureNetwork.Authenticate(network,p,"foh-damage-input-");var seed=HeavenFistDamage.SeedLow;var roll=HeavenFists.Roll;HeavenFistDamage.SeedLow=()=>0;HeavenFists.Roll=_=>throw new InvalidOperationException("Natural FoH used global RNG");NecromancerEffects.SetMana256(p,6417);int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+8000;
        void Cleanup(){HeavenFistDamage.SeedLow=seed;HeavenFists.Roll=roll;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"FoH typed input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.FistOfTheHeavens&&!began){Require(NecromancerEffects.ManaBalance256(p)==6417&&SkillHealth.Balance256(t)==life,"FoH paid/hit before action event");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&life-SkillHealth.Balance256(t)==41754&&t.Notifications==1&&t.LifeAtNotification==life&&NecromancerEffects.ManaBalance256(p)==17,"Actual FoH typed delayed input/once mana");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.FistOfTheHeavens,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
