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

public static class HolyBoltDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HolyBoltDamageChecks));
    private sealed record Saved(uint Owner,uint Target,int Mana,int Life,HolyBoltFlight Flight);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"holy-bolt-damage-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("holy-bolt-damage-check",Check);Register("holy-bolt-damage-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)&&World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Target) is BaseCreature t){t.Frozen=t.CantWalk=true;t.Combatant=null;t.AIObject.AITimer.Stop();}};
        foreach(string mode in new[]{"left","right","quick"})
        {string label="holy-bolt-damage-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Bolt typed receiver,capture and original flight saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();p.MoveToWorld(ZealChecks.Origin(p),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.HolyBolt)&&SkillBindings.Instance.Assign(p,false,CombatAbility.HolyBolt)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.HolyBolt),"Holy Bolt damage bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t,int dx=6) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static HolyBoltFlight Create(PlayerMobile p)
    {HolyBolts.Cast(p,new(p.X+12,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.HolyBolt));return HolyBolts.Snapshot().Where(x=>x.Owner==p.Serial.Value).MaxBy(x=>x.Id);}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {HolyBolts.Restore(HolyBolts.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void LaunchPotionRegression()
    {
        var p=Owner();try
        {
            Require(CombatPotions.Apply(p,3),"Holy Bolt actual launch potion");var s=Create(p);
            Require(s.Min256==3072&&s.Max256==6144,"Holy Bolt incorrectly snapshots potion as extra original magic at launch");
        }
        finally{Clean(p);}
    }
    private static void Potions()
    {
        foreach(string mode in new[]{"active","after-launch","removed","expired","mixed-resistances"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
            try
            {
                if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Holy Bolt actual potion "+mode);}
                var s=Create(p) with{Min256=25600,Max256=25600};
                Require(s.Captured is{Modifiers.Potion:0},"Holy Bolt missing non-potion capture");
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Holy Bolt potion after launch");}
                if(mode=="removed"){TimedEffects.Clear(p);}
                if(mode=="expired"){TimedEffects.Settle(p,TimedEffects.Now.AddMinutes(2));}
                if(mode=="mixed-resistances")
                {t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,100);t.SetResistance(ResistanceType.Energy,-50);}
                int life=SkillHealth.Balance256(t);HolyBolts.Impact(p,t,s);
                int expected=mode is "removed" or "expired"?25600:mode=="mixed-resistances"?33280:35840;
                Require(life-SkillHealth.Balance256(t)==expected&&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==1
                    &&t.FindDamageEntryFor(p)?.Damager==p&&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.PoisonClock!=null),
                    "Holy Bolt potion lanes/noncompounding/impact eligibility/ordered notification failed "+mode+" loss="+(life-SkillHealth.Balance256(t)));
            }
            finally{Clean(p,t);}
        }
    }
    private static void Species()
    {
        foreach(string kind in new[]{"undead-immune","demon","beast"})
        {
            var p=Owner();BaseCreature t;
            if(kind=="undead-immune"){var lich=new LichLord();MonsterVariants.Assign(lich,MonsterDifficulty.Hell);t=Target(p,lich);}
            else{t=Target(p,kind=="demon"?(BaseCreature)new Daemon():new Ogre());}
            try
            {
                if(kind=="undead-immune"){Require(NamedElements.MagicResistance(t)==100,"Holy Bolt real undead magic immunity");}
                var s=Create(p) with{Min256=25600,Max256=25600};int before=SkillHealth.Balance256(t);HolyBolts.Impact(p,t,s);
                Require(before-SkillHealth.Balance256(t)==(kind=="beast"?0:25600)&&NativeMonsterReturn.Session(t).Packets==(kind=="beast"?0:1),"Holy Bolt species/positive magic bypass/native identity "+kind);
                if(kind!="beast"){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Unclassified Holy Bolt species acquired a D2 hit identity");}
            }
            finally{Clean(p,t);}
        }
    }
    private static void SpellNotWeapon()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());var created=CorpseRecoveryHooks.Created;bool died=false;
        try
        {
            PoisonDaggerChecks.Named(p,"Greyform");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-20*256-17);int hp=SkillHealth.Balance256(p);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1000,TimedEffects.Now.AddDays(1)));
            var s=Create(p) with{Min256=25600,Max256=25600};SkillHealth.SetBalance256(t,256);CorpseRecoveryHooks.Created=corpse=>
            {if(corpse.Owner==t){died=SkillHealth.Balance256(t)==0&&t.FindDamageEntryFor(p)?.Damager==p;}created?.Invoke(corpse);};
            HolyBolts.Impact(p,t,s);
            Require(!t.Alive&&died&&ClassLoot.Killer(t)==p&&SkillHealth.Balance256(p)==hp&&NativeMonsterReturn.Session(t).Packets==1,"Holy Bolt lethal credit/HP or forbidden weapon leech/Thorns");
        }
        finally{CorpseRecoveryHooks.Created=created;PaladinAuras.Clear(t);Clean(p,t);}
    }
    private static void CaptureAndAtomicRestore()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            var s=Create(p);Require(s.Captured.Slow==new SlowMissilePayload(50,50)&&s.Vx==.234375&&s.Min256==1536&&s.Max256==3072,"Holy Bolt captured slow damage/velocity");
            AmazonSlowMissiles.Clear(p);Require(HolyBolts.Snapshot().Single(x=>x.Id==s.Id)==s,"Holy Bolt reread removed slow");
            var all=HolyBolts.Snapshot();bool refused=false;
            try{HolyBolts.Restore(all.Append(s with{Id=s.Id+1000000,Captured=s.Captured with{Slow=new(101,100)}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Malformed Holy Bolt capture partially replaced live pool");
            refused=false;try{HolyBolts.Restore(all.Append(all[0]).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&HolyBolts.Snapshot().SequenceEqual(all),"Duplicate Holy Bolt restore partially replaced live pool");
            var legacy=s with{Captured=null,Motion=null};HolyBolts.Restore(all.Select(x=>x.Id==s.Id?legacy:x).ToArray());Require(HolyBolts.Snapshot().Single(x=>x.Id==s.Id).Captured==null,"Legacy flight gained guessed capture");
        }
        finally{Clean(p,t);}
    }
    private static void Refusals()
    {
        foreach(string kind in new[]{"paragon","uninterruptible","evilomen"})
        {
            var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
            try
            {
                if(kind=="paragon"){t.IsParagon=true;}
                if(kind=="uninterruptible"){MonsterUninterruptibles.Restore(MonsterUninterruptibles.Snapshot().Append(new MonsterModeFlags(t.Serial.Value,true,false,0)).ToArray());}
                if(kind=="evilomen"){t.AddSkillMod(new DefaultSkillMod(SkillName.MagicResist,"EvilOmen",false,50));}
                var s=Create(p);int hp=SkillHealth.Balance256(t);HolyBolts.Roll=_=>throw new InvalidOperationException("Unsupported Holy Bolt rolled damage");HolyBolts.Impact(p,t,s);
                Require(SkillHealth.Balance256(t)==hp&&NativeMonsterReturn.Session(t).Packets==0,"Unsupported Holy Bolt mutated target "+kind);
            }
            finally{HolyBolts.Roll=_=>0;MonsterUninterruptibles.Clear(t);Clean(p,t);}
        }
    }
    private static void Save()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());var s=Create(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        s=s with{Min256=3072,Max256=3072,NextUtc=at,UntilUtc=at.AddSeconds(2)};
        HolyBolts.Restore(HolyBolts.Snapshot().Select(x=>x.Id==s.Id?s:x).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),s)));
    }
    private static void Check()
    {
        var roll=HolyBolts.Roll;try
        {
            HolyBolts.Roll=_=>0;LaunchPotionRegression();Potions();Species();SpellNotWeapon();CaptureAndAtomicRestore();Refusals();Save();
            Log.Information("Holy Bolt damage:impact-time four-lane potion;undead immunity/demon/beast rules;ordered notification/death/credit;no weapon leech/Thorns;captured slow;atomic/legacy restore;3pre-roll refusals");
        }
        finally{HolyBolts.Roll=roll;}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(BaseCreature)World.FindMobile((Serial)saved.Target);
        var s=HolyBolts.Snapshot().Single(x=>x.Id==saved.Flight.Id);Require(s==saved.Flight&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(t)==saved.Life,"Original Holy Bolt capture/actor resources changed across reload");
        // Resume a copy synchronously; the untouched original remains available
        // for the second real save/reload. Natural wire flights run separately.
        Log.Information("Holy Bolt saved context before fixture placement:ownerMap={OwnerMap},targetMap={TargetMap},supported={Supported},reason={Reason}",p.Map?.MapID,t.Map?.MapID,HolyBoltDamage.Supported(p,t,out string reason),reason);
        p.MoveToWorld(new((int)s.X,(int)s.Y,s.Z),Map.Maps[s.Map]);
        Require(HolyBoltDamage.Supported(p,t,out reason),"Saved Holy Bolt world placement remains unsupported:"+reason);
        int life=SkillHealth.Balance256(t);HolyBolts.Impact(p,t,s);
        Require(life-SkillHealth.Balance256(t)==3072&&NativeMonsterReturn.Session(t).Packets==1,$"Saved Holy Bolt capture did not deliver after load:loss={life-SkillHealth.Balance256(t)},packets={NativeMonsterReturn.Session(t).Packets},ownerMap={p.Map?.MapID},targetMap={t.Map?.MapID}");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(t)}));
    }
    private static void Input(NetState network,string label)
    {
        // Origin verifies the original six-cell corridor, then offsets the owner
        // by two cells. Keep the recipient inside that verified corridor.
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient(),3);
        Require(PaladinCombat.ValidTarget(p,CombatAbility.HolyBolt,t.Serial,t.Location),"Holy Bolt input fixture must provide an eligible visible target");
        var account=FixtureNetwork.Authenticate(network,p,"holy-bolt-damage-");
        var roll=HolyBolts.Roll;var seed=HolyBoltFlights.SeedLow;HolyBolts.Roll=_=>0;HolyBoltFlights.SeedLow=()=>0;NecromancerEffects.SetMana256(p,529);int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+6000;
        void Cleanup(){HolyBolts.Roll=roll;HolyBoltFlights.SeedLow=seed;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,$"Actual Holy Bolt damage input timeout:sequence={ClassCombat.For(p).Sequence},pending={ClassCombat.For(p).PendingAbility},held={ClassCombat.For(p).HeldAbility},running={network.Running},alive={p.Alive},frozen={p.Frozen},mana={NecromancerEffects.ManaBalance256(p)},valid={PaladinCombat.ValidTarget(p,CombatAbility.HolyBolt,t.Serial,t.Location)},overlap={AlliedOverlap.Allowed(p)},next={ClassCombat.For(p).NextAction-Core.TickCount},map={p.Map?.MapID},targetMap={t.Map?.MapID},position={p.Location},target={t.Location}");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.HolyBolt&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==529&&SkillHealth.Balance256(t)==life,"Holy Bolt paid before action event");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&life-SkillHealth.Balance256(t)==3738&&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==1
                    &&NecromancerEffects.ManaBalance256(p)==17,"Actual Holy Bolt ordered impact/once mana failed");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.HolyBolt,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
