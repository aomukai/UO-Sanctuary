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
public static class HexBaneCastChecks
{
    private const CombatAbility Ability=CombatAbility.HexBane;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexBaneCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,bool Female,int Life,int Mana,HexBaneBuff Buff,HexBaneDebuff Debuff);
    private sealed record Manifest(int Generation,Saved[] Rows);
    private static string FileName=>Path.Join(Core.BaseDirectory,"bane-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Bane and approved mastery/result ordering",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("bane-cast-check",()=>Check(false));Register("bane-cast-reload-check",()=>Check(true));
        foreach(string choice in new[]{"right","quick"}){string mode=choice;Register("bane-cast-"+mode+"-check",()=>FixtureNetwork.Connected("bane-cast-"+mode+"-result",n=>Input(n,mode)));}
    }
    public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mana=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="BaneCastSaved"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="BaneCastSaved"&&mana?.Invoke(p)!=false;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(bool female,int x)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="BaneCastFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.hexbane");ClassSkillChecks.Train(p,"warlock.hexsiphon");while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Bane strength");}
        FixtureSpace.Corridor(p,x,24,3500);var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Bane sword");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static ShockWaveTarget Enemy(PlayerMobile p)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(5000);SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);return t;}
    private static void Cast(PlayerMobile p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(ClassCombat.Perform(p,Ability,null,new(-20,-20,0),Core.TickCount),"Bane learned start");var s=ClassCombat.For(p);int cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Bane self SC context");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256-cost,"Bane one payment/release");Ready(p);
    }
    private static void Put(PlayerMobile p,HexBaneBuff buff)
    {var s=HexBane.Snapshot();HexBane.Restore(new(s.Buffs.Where(b=>b.Owner!=p.Serial.Value).Append(buff).ToArray(),s.Debuffs));}
    private static void Casting(PlayerMobile p)
    {
        Require(HexBaneDamageProposal.Approved&&WeaponHexGroups.Approved&&ClassCombat.Available(p,Ability)&&ClassAbilities.SelfTarget(Ability)
            &&!SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Bane metadata/bindings");
        int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Bane unpaid start");var s=ClassCombat.For(p);long at=s.EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&HexBane.Buff(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Bane early effect/payment");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&HexBane.Buff(p)==null,"Bane Stop release");
        Cast(p);var old=HexBane.Buff(p);Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Bane late mana start");at=s.EffectAt;NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
        Require(!ClassCombat.ResolveAction(p,at)&&HexBane.Buff(p)==old,"Bane low release mana replaced buff");Ready(p);NecromancerEffects.SetMana256(p,mana);
        Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Bane move start");at=s.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!ClassCombat.ResolveAction(p,at)&&HexBane.Buff(p)==old,"Bane moved windup released");p.MoveToWorld(origin,p.Map);Ready(p);var sword=CombatTiming.Equipped(p);p.AddToBackpack(sword);
        Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Bane unarmed start accepted");Require(p.EquipItem(sword),"Bane sword restore");ClassGear.Refresh(p);
        // Read a real zero-mastery record emitted by the frozen predecessor.
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"bane-zero-legacy.json")));
        var legacy=JsonSerializer.Deserialize<HexBaneBuff>(document.RootElement.GetProperty("Owners")[0].GetProperty("Legacy").GetRawText());
        Require(legacy.CastMastery==0&&!JsonSerializer.Serialize(legacy).Contains("CastMastery"),"Old Bane JSON default/shape changed");Put(p,legacy with{Owner=p.Serial.Value,UntilUtc=TimedEffects.Now.AddMinutes(10)});
        Require(HexBane.Buff(p).Values==legacy.Values,"Old zero-mastery values changed");HexBane.ClearBuff(p);Cast(p);
        var plan=HexBaneDamageProposal.Capture(SkillMechanics.Values(p,Ability),50);var current=HexBane.Buff(p);Put(p,current with{Values=plan.Values,CastMastery=50});
        Require(HexBane.WeaponMagic(p,0)==plan.Values.Minimum&&HexBane.WeaponMagic(p,1)==plan.Values.Maximum,"Captured mastered endpoints not used");
        Log.Information("Bane learned:selfSC/right/quick,early/Stop/movement/late-mana/unarmed controls,one table debit,actual old JSON and explicit50percent captured mastery metadata pass.");
    }
    private static void Events(PlayerMobile p,ShockWaveTarget t)
    {
        var registered=HexBane.Buff(p);Require(registered!=null,"Bane registration missing");HexBane.ClearDebuff(t);int hp=SkillHealth.Balance256(t),defense=ClassStats.Defense(t);
        bool hit=ClassStats.CheckHit(p,t,0,()=>{Require(HexBane.Debuff(t)==null&&ClassStats.Defense(t)==defense,"Bane modified defense before accuracy roll");return 1;})==true;
        Require(!hit&&HexBane.Debuff(t)!=null&&SkillHealth.Balance256(t)==hp,"Bane automatic miss/post-result event");
        HexBane.ClearDebuff(t);var check=ClassCombatHooks.ClassHitCheck;
        try{ClassCombatHooks.ClassHitCheck=(a,b)=>a==p?ClassStats.CheckHit(a,b,0,()=>1):check?.Invoke(a,b);CombatTiming.Equipped(p).OnSwing(p,t);
            Require(HexBane.Debuff(t)!=null&&SkillHealth.Balance256(t)==hp,"Native Bane miss did not use result hook");}finally{ClassCombatHooks.ClassHitCheck=check;}
        HexBane.ClearDebuff(t);var ordinary=PlayerWeaponContact.PrepareOrdinary(p,t,0,0,()=>PlayerMeleeGeometry.Within(p,t,0),()=>1,()=>0,_=>0,_=>0,new Random(173),allowNative:true);
        Require((ordinary.Flags&SourceMeleeResult.Successful)==0&&HexBane.Debuff(t)!=null,"Ordinary contact Bane result hook");
        var origin=t.Location;t.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);HexBane.ClearDebuff(t);_=ClassStats.CheckHit(p,t,0,()=>1);Require(HexBane.Debuff(t)==null,"Out-of-reach Bane attempt");t.MoveToWorld(origin,p.Map);
        var sword=CombatTiming.Equipped(p);var bow=new Bow();p.AddToBackpack(bow);p.AddToBackpack(sword);Require(p.EquipItem(bow),"Bane bow");ClassGear.Refresh(p);
        try
        {
            _=ClassStats.CheckHit(p,t,0,()=>1,projectile:true);Require(HexBane.Debuff(t)==null,"Ranged Bane miss applied");
            foreach(bool weapon in new[]{false,true}){HexBane.ClearDebuff(t);AOS.Damage(t,p,3,false,100,0,0,0,0,archer:true,weaponDamage:weapon,spellDamage:!weapon);
                Require((HexBane.Debuff(t)!=null)==weapon,"Native Bane missile/spell qualification");}
        }
        finally{p.AddToBackpack(bow);bow.Delete();Require(p.EquipItem(sword),"Bane melee restore");ClassGear.Refresh(p);}
        HexBane.ClearDebuff(t);Require(!HexBaneEvents.QualifiedHexTrigger(p,t,false)&&HexBaneEvents.QualifiedHexTrigger(p,t,true),"Bane trigger qualification");
        var curse=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),t.X,t.Y);NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        Require(HexBaneEvents.QualifiedHexTrigger(p,t,true)&&NecromancerEffects.Curse(t)==curse,"Bane replaced ordinary curse");NecromancerEffects.Profiles.Clear(t);
        var raw=new GearDamage(0,0,0,0,0,Magic:HexBane.WeaponMagic(p,0));var expected=SkillCombat.MitigatePrepared(p,t,raw);hp=SkillHealth.Balance256(t);
        using(CombatItemEvents.Suppress(p,t)){GearEffects.Strike(p,t,0,new Random(173),wearWeapon:false,preparedDamage:raw);}
        Require(hp-SkillHealth.Balance256(t)==NamedElements.To256(expected.Total)&&HexBane.Buff(p)==registered,"Actual stored magic/weapon mitigation altered capture");
        Log.Information("Bane events:direct/native/ordinary post-result misses,unmodified pre-roll defense,range/ranged-miss/spell exclusion,successful missile,qualified trigger,curse coexistence and stored magic delivery pass.");
    }
    private static void Check(bool reload)
    {
        Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6480:6400);Casting(p);var t=Enemy(p);Events(p,t);var s=HexBane.Snapshot();var until=TimedEffects.Now.Add(FixtureSpace.Retention);
                HexBane.Restore(new(s.Buffs.Select(b=>b.Owner==p.Serial.Value?b with{UntilUtc=until}:b).ToArray(),s.Debuffs.Select(d=>d.Target==t.Serial.Value?d with{UntilUtc=until}:d).ToArray()));p.Name="BaneCastSaved";
                SkillHealth.SetBalance256(p,p.HitsMax*256-333);NecromancerEffects.SetMana256(p,p.ManaMax*256-517);
                rows[i++]=new(p.Serial.Value,t.Serial.Value,CombatTiming.Equipped(p).Serial.Value,female,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),HexBane.Buff(p),HexBane.Debuff(t));
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Bane reload generation");
            foreach(var row in old.Rows)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)row.Target);
                Require(p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon&&HexBane.Buff(p)==row.Buff&&HexBane.Debuff(t)==row.Debuff&&row.Buff.CastMastery==50
                    &&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana,"Bane original saved mastered state/resources/gear");
                FixtureSpace.Corridor(p,row.Female?6480:6400,24,3500);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Events(p,t);var s=HexBane.Snapshot();
                HexBane.Restore(new(s.Buffs.Select(b=>b.Owner==row.Owner?row.Buff:b).ToArray(),s.Debuffs.Select(d=>d.Target==row.Target?row.Debuff:d).ToArray()));SkillHealth.SetBalance256(p,row.Life);NecromancerEffects.SetMana256(p,row.Mana);Ready(p);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Log.Information("Bane original:Warlock/Witch explicit mastered registration,old-save compatibility,target state,weapon and fractional resources persist across two reloads;events resume.");
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",7080);Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Bane wire bindings");var account=FixtureNetwork.Authenticate(network,p,"BaneWire");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;bool began=false,done=false;long deadline=Core.TickCount+6000;string label="bane-cast-"+mode;
        void Cleanup(){ClassCombat.Stop(p);HexBane.Clear(p);p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Bane wire timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Bane wire self/unpaid context");began=true;Log.Information("{Label}: windup",label);}
                if(HexBane.Buff(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Bane wire one debit");var t=Enemy(p);
                try{_=ClassStats.CheckHit(p,t,0,()=>1);Require(HexBane.Debuff(t)!=null,"Wire-created Bane did not apply after miss");}finally{HexBane.Clear(t);t.Delete();}
                done=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
