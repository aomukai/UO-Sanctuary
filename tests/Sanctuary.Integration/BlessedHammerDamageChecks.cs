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

public static class BlessedHammerDamageChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BlessedHammerDamageChecks));
    private sealed record Saved(uint Owner,uint Target,int Mana,int Life,HammerFlight Flight);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blessed-hammer-damage-checks.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("blessed-hammer-damage-check",Check);Register("blessed-hammer-damage-reload-check",Reload);
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)&&World.FindMobile((Serial)JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Target) is BaseCreature t){t.Frozen=t.CantWalk=true;t.Combatant=null;t.AIObject.AITimer.Stop();}};
        foreach(string mode in new[]{"left","right","quick"})
        {string label="blessed-hammer-damage-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Blessed Hammer typed receiver,capture and original flight saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static PlayerMobile Owner()
    {
        var p=SacrificeContactChecks.Owner();
        // An internalized caster can still own a held original source flight.
        // Keep this retained target out of that flight's remaining corridor.
        p.MoveToWorld(ZealChecks.Origin(p,point=>HolyBolts.Snapshot().Any(f=>f.Map==p.Map.MapID
            &&Math.Abs(f.X-point.X)<30&&Math.Abs(f.Y-point.Y)<30)
            ||BlessedHammers.Snapshot().Any(f=>f.Motion!=null&&f.Map==p.Map.MapID
                &&Math.Abs(f.OriginX+f.XFp16/131072.0-point.X)<30&&Math.Abs(f.OriginY+f.YFp16/131072.0-point.Y)<30)),p.Map);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BlessedHammer)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BlessedHammer)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BlessedHammer),"Blessed Hammer damage bindings");return p;
    }
    private static T Target<T>(PlayerMobile p,T t,int dx=6) where T:BaseCreature
    {
        t.NoKillAwards=t.Frozen=t.CantWalk=true;t.SetHits(30000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static HammerFlight Create(PlayerMobile p)
    {BlessedHammers.Cast(p,SkillMechanics.Values(p,CombatAbility.BlessedHammer));var f=BlessedHammers.Snapshot().Where(x=>x.Owner==p.Serial.Value).MaxBy(x=>x.Id) with{Motion=null};
        BlessedHammers.Restore(BlessedHammers.Snapshot().Select(x=>x.Id==f.Id?f:x).ToArray());return f;}
    private static void Clean(PlayerMobile p,params Mobile[] targets)
    {BlessedHammers.Restore(BlessedHammers.Snapshot().Where(x=>x.Owner!=p.Serial.Value).ToArray());AmazonSlowMissiles.Clear(p);foreach(var t in targets){t.Corpse?.Delete();t.Delete();}p.Corpse?.Delete();p.Delete();}
    private static void LaunchPotionRegression()
    {
        var p=Owner();try
        {
            Require(CombatPotions.Apply(p,3),"Blessed Hammer actual launch potion");var s=Create(p);
            Require(s.Min256==3932&&s.Max256==5242,"Blessed Hammer incorrectly snapshots potion as extra original magic at launch");
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
                if(mode!="after-launch"){Require(CombatPotions.Apply(p,3),"Blessed Hammer actual potion "+mode);}
                var s=Create(p) with{Min256=25600,Max256=25600};
                Require(s.Captured is{Modifiers.Potion:0},"Blessed Hammer missing non-potion capture");
                if(mode=="after-launch"){Require(CombatPotions.Apply(p,3),"Blessed Hammer potion after launch");}
                if(mode=="removed"){TimedEffects.Clear(p);}
                if(mode=="expired"){TimedEffects.Settle(p,TimedEffects.Now.AddMinutes(2));}
                if(mode=="mixed-resistances")
                {t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,100);t.SetResistance(ResistanceType.Energy,-50);}
                int life=SkillHealth.Balance256(t);BlessedHammers.Impact(p,t,s);
                int expected=mode is "removed" or "expired"?38400:mode=="mixed-resistances"?49920:53760;
                Require(life-SkillHealth.Balance256(t)==expected&&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==1
                    &&t.FindDamageEntryFor(p)?.Damager==p&&!TimedEffects.Snapshot().Any(x=>x.Owner==t.Serial.Value&&x.PoisonClock!=null),
                    "Blessed Hammer potion lanes/noncompounding/impact eligibility/ordered notification failed "+mode+" loss="+(life-SkillHealth.Balance256(t)));
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
                if(kind=="undead-immune"){Require(NamedElements.MagicResistance(t)==100,"Blessed Hammer real undead magic immunity");}
                var s=Create(p) with{Min256=25600,Max256=25600};int before=SkillHealth.Balance256(t);BlessedHammers.Impact(p,t,s);
                Require(before-SkillHealth.Balance256(t)==(kind=="undead-immune"?0:kind=="demon"?38400:25600)&&NativeMonsterReturn.Session(t).Packets==1,"Blessed Hammer species/ordinary magic defense/native identity "+kind);
                if(kind!="beast"){Require(NativeMonsterReturn.Session(t).LastHitClass==0,"Unclassified Blessed Hammer species acquired a D2 hit identity");}
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
            BlessedHammers.Impact(p,t,s);
            Require(!t.Alive&&died&&ClassLoot.Killer(t)==p&&SkillHealth.Balance256(p)==hp&&NativeMonsterReturn.Session(t).Packets==1,"Blessed Hammer lethal credit/HP or forbidden weapon leech/Thorns");
        }
        finally{CorpseRecoveryHooks.Created=created;PaladinAuras.Clear(t);Clean(p,t);}
    }
    private static void CaptureAndAtomicRestore()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            AmazonSlowMissiles.Restore(AmazonSlowMissiles.Snapshot().Append(new(p.Serial.Value,t.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            var s=Create(p);Require(s.Captured.Slow==new SlowMissilePayload(100,50)&&s.VelocityQ8==3456&&s.Min256==1966&&s.Max256==2621,"Blessed Hammer captured slow damage/velocity");
            AmazonSlowMissiles.Clear(p);Require(BlessedHammers.Snapshot().Single(x=>x.Id==s.Id)==s,"Blessed Hammer reread removed slow");
            var all=BlessedHammers.Snapshot();bool refused=false;
            try{BlessedHammers.Restore(all.Append(s with{Id=s.Id+1000000,Captured=s.Captured with{Slow=new(101,100)}}).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&BlessedHammers.Snapshot().SequenceEqual(all),"Malformed Blessed Hammer capture partially replaced live pool");
            refused=false;try{BlessedHammers.Restore(all.Append(all[0]).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&BlessedHammers.Snapshot().SequenceEqual(all),"Duplicate Blessed Hammer restore partially replaced live pool");
            var legacy=s with{Captured=null};BlessedHammers.Restore(all.Select(x=>x.Id==s.Id?legacy:x).ToArray());Require(BlessedHammers.Snapshot().Single(x=>x.Id==s.Id).Captured==null,"Legacy flight gained guessed capture");
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
                var s=Create(p);int hp=SkillHealth.Balance256(t);BlessedHammers.Roll=_=>throw new InvalidOperationException("Unsupported Blessed Hammer rolled damage");BlessedHammers.Impact(p,t,s);
                Require(SkillHealth.Balance256(t)==hp&&NativeMonsterReturn.Session(t).Packets==0,"Unsupported Blessed Hammer mutated target "+kind);
            }
            finally{BlessedHammers.Roll=_=>0;MonsterUninterruptibles.Clear(t);Clean(p,t);}
        }
    }
    private static void Save()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());var s=Create(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        s=s with{Min256=3932,Max256=3932,NextUtc=at,UntilUtc=at.AddSeconds(2)};
        BlessedHammers.Restore(BlessedHammers.Snapshot().Select(x=>x.Id==s.Id?s:x).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),s)));
    }
    private static void Check()
    {
        var roll=BlessedHammers.Roll;try
        {
            BlessedHammers.Roll=_=>0;LaunchPotionRegression();Details();
            Log.Information("Blessed Hammer damage:384complete source cases;two magic draws/species;captured Concentration/hard synergy;impact-time four-lane potion;ordinary immunity/demon/beast defenses;ordered notification/death/credit;no weapon leech/Thorns;CanSlow0damage capture;atomic/legacy restore;3pre-roll refusals");
        }
        finally{BlessedHammers.Roll=roll;}
    }
    private static void Details()
    {SourceCases();Concentration();Potions();Species();SpellNotWeapon();CaptureAndAtomicRestore();Refusals();Save();}
    private static void SourceCases()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,".local/class-skills/blessed-hammer-contact/damage-source/cases.json")));
        int count=0;
        foreach(var row in json.RootElement.GetProperty("damage").EnumerateArray())
        {
            int lo=row[0].GetInt32(),hi=row[1].GetInt32(),mastery=row[2].GetInt32(),pattern=row[5].GetInt32(),calls=0;
            lo+=SourcePercentage.Calculate(lo,mastery,100);hi+=SourcePercentage.Calculate(hi,mastery,100);
            int result=BlessedHammerDamage.Roll(lo,hi,row[4].GetInt32()!=0,row[3].GetInt32()!=0,n=>(pattern&(1<<(calls++&1)))!=0?n-1:0);
            Require(result==row[7].GetInt32()&&calls==row[9].GetInt32(),"Hammer source fill/reroll/species/percentage row "+count);count++;
        }
        Require(count==336,"Hammer complete source row count");var p=Owner();
        try
        {
            foreach(var row in json.RootElement.GetProperty("concentration").EnumerateArray())
            {
                PaladinAuras.Clear(p);if(row[1].GetInt32()==0){PaladinAuras.ElementalWeapon(new(p.Serial.Value,p.Serial.Value,CombatAbility.Concentration,1,row[0].GetInt32(),TimedEffects.Now.AddDays(1)));}
                Require(PaladinOffense.HammerBonus(p)==row[2].GetInt32(),"Hammer Concentration integer percentage");
            }
            foreach(var row in json.RootElement.GetProperty("start").EnumerateArray())
            {
                if(row[1].GetInt32()!=0){continue;}PaladinAuras.Clear(p);PaladinAuras.ElementalWeapon(new(p.Serial.Value,p.Serial.Value,CombatAbility.Concentration,1,row[0].GetInt32(),TimedEffects.Now.AddDays(1)));
                var prepared=BlessedHammerDamage.Prepare(p,SkillMechanics.Values(p,CombatAbility.BlessedHammer));
                Require(prepared.Min256==row[8].GetInt32()&&prepared.Max256==row[9].GetInt32(),"Hammer do73 post-factory Concentration snapshot");
            }
        }
        finally{PaladinAuras.Clear(p);Clean(p);}
    }
    private static void Concentration()
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());
        try
        {
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Concentration)&&PaladinOffense.HammerBonus(p)==30,"Actual selected Concentration aura");
            var f=Create(p);Require(f.Min256==5111&&f.Max256==6814&&f.Captured.Concentration==30,"Hammer Concentration launch endpoints");
            Require(SkillBindings.Instance.Assign(p,false,CombatAbility.BlessedHammer)&&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.vigor",out _)
                &&ClassCreation.Profiles.TryTrain(p.Serial,"paladin.blessedhammer",out _),"Hammer later aura/hard/effective changes");
            Require(BlessedHammers.Damage(p,SkillMechanics.Values(p,CombatAbility.BlessedHammer,1)).Min==4362&&BlessedHammers.Snapshot().Single(x=>x.Id==f.Id)==f,"Hammer recaptured changed aura/synergy/rank");
            int life=SkillHealth.Balance256(t);BlessedHammers.Impact(p,t,f);Require(life-SkillHealth.Balance256(t)==7666,"Hammer impact lost captured Concentration damage");
        }
        finally{PaladinAuras.Clear(p);Clean(p,t);}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var t=(BaseCreature)World.FindMobile((Serial)saved.Target);
        var s=BlessedHammers.Snapshot().Single(x=>x.Id==saved.Flight.Id);Require(s==saved.Flight&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(t)==saved.Life,"Original Blessed Hammer capture/actor resources changed across reload");
        // Resume a copy synchronously; the untouched original remains available
        // for the second real save/reload. Natural wire flights run separately.
        Log.Information("Blessed Hammer saved context before fixture placement:ownerMap={OwnerMap},targetMap={TargetMap},supported={Supported},reason={Reason}",p.Map?.MapID,t.Map?.MapID,BlessedHammerDamage.Supported(p,t,out string reason),reason);
        p.MoveToWorld(new(s.OriginX,s.OriginY,s.Z),Map.Maps[s.Map]);
        Require(BlessedHammerDamage.Supported(p,t,out reason),"Saved Blessed Hammer world placement remains unsupported:"+reason);
        int life=SkillHealth.Balance256(t);BlessedHammers.Impact(p,t,s);
        Require(life-SkillHealth.Balance256(t)==5898&&NativeMonsterReturn.Session(t).Packets==1,$"Saved Blessed Hammer capture did not deliver after load:loss={life-SkillHealth.Balance256(t)},packets={NativeMonsterReturn.Session(t).Packets},ownerMap={p.Map?.MapID},targetMap={t.Map?.MapID}");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Life=SkillHealth.Balance256(t)}));
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var t=Target(p,new ConversionContactChecks.Recipient());t.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);bool aura=!label.EndsWith("right",StringComparison.Ordinal);
        if(aura){Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Concentration)&&PaladinOffense.HammerBonus(p)==30,"Wire Hammer Concentration");}
        var account=FixtureNetwork.Authenticate(network,p,"blessed-hammer-damage-");
        var roll=BlessedHammers.Roll;var seed=BlessedHammerFlights.SeedLow;BlessedHammerFlights.SeedLow=()=>0;BlessedHammers.Roll=_=>throw new InvalidOperationException("Natural Hammer used global RNG");NecromancerEffects.SetMana256(p,1297);int life=SkillHealth.Balance256(t);bool began=false;long deadline=Core.TickCount+6000;
        void Cleanup(){BlessedHammers.Roll=roll;BlessedHammerFlights.SeedLow=seed;Clean(p,t);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Blessed Hammer damage input timeout");
                if(ClassCombat.For(p).PendingAbility==CombatAbility.BlessedHammer&&!began)
                {Require(NecromancerEffects.ManaBalance256(p)==1297&&SkillHealth.Balance256(t)==life,"Blessed Hammer paid before action event");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&life-SkillHealth.Balance256(t)==(aura?9349:6795)&&t.Notifications==1&&t.LifeAtNotification==life&&NativeMonsterReturn.Session(t).Packets==1
                    &&NecromancerEffects.ManaBalance256(p)==17,"Actual Blessed Hammer ordered impact/once mana failed");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BlessedHammer,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
