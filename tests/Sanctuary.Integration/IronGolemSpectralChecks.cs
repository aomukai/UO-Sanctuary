// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class IronGolemSpectralChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemSpectralChecks));
    private sealed record Saved(uint Owner,uint Body,uint Legacy,uint Removed,uint PoisonTarget,uint ColdTarget,string Prepared,ulong Seed,int Life,int LegacyLife,int Fire,int LegacyFire,TimedEffectState Poison,TimedEffectState Cold,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-spectral-fixtures.json");
    public static void Configure()
    {
        Register("iron-golem-spectral-check",Check);Register("iron-golem-spectral-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("iron-golem-spectral-live","Connected hard20 Spectral during Iron windup",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("iron-golem-spectral-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Shared Spectral producer,typed release and saved owner-credit effects",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,int rank)=>MagePetBuffChecks.Train(p,"necromancer.irongolem",rank);
    private static PlayerMobile Owner(int rank,int mastery,int row)
    {var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6520,40,row);Train(p,rank);if(mastery>0){MagePetBuffChecks.Train(p,"necromancer.golemmastery",mastery);}return p;}
    private static GraveyardSkeleton Target(PlayerMobile p,SkeletonGuardian b)
    {
        var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(b.Location,p.Map);
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}
        Require(NamedElements.MagicResistance(t)==0,"Spectral isolation needs zero magic resistance");return t;
    }
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    private static IronGolemContact.Prepared Packet(PlayerMobile p,IronGolemBody b,BaseCreature t,int channel)
    {
        for(ulong n=1;n<10000;n++)
        {
            NpcEquipmentRandom.Set(b,n);var candidate=IronGolemContact.Prepare(b,t,IronGolemContact.Require(b,p,t),()=>5);
            if(candidate.Impact.Spectral?.Roll.Channel==channel){return candidate;}candidate.Cancel();
        }
        throw new InvalidOperationException("No Spectral seed for channel "+channel);
    }
    private static MonsterContactResult Release(PlayerMobile p,IronGolemBody b,BaseCreature t,IronGolemContact.Prepared packet,DateTime? at=null,bool range=true)
        =>IronGolemContact.Release(b,p,t,packet,_=>range,at??TimedEffects.Now);
    private sealed class FixedRandom(int channel):Random
    {
        internal int AmountCalls,ChoiceCalls;
        public override int Next(int minimum,int maximum){AmountCalls++;return minimum;}
        public override int Next(int maximum){Require(maximum==5,"Wild Spectral changed channel bound");ChoiceCalls++;return channel;}
    }
    private static void Producer()
    {
        int cases=0;
        foreach(var basis in new[]{(0,0),(1,2),(23,24),(165,520)})for(int seed=0;seed<500;seed++)
        {
            var old=new Random(seed);var current=new Random(seed);int amount=old.Next(basis.Item1*66/100,basis.Item2+1),channel=old.Next(5);
            Require(SpectralHit.Roll(basis.Item1,basis.Item2,(lo,hi)=>current.Next(lo,hi+1),current.Next)==new SpectralHitRoll(amount,channel)&&old.Next()==current.Next(),"Extracted wild producer changed draw values/order/state");cases++;
        }
        int draws=0;Require(SpectralHit.Roll(0,0,(lo,hi)=>{draws++;return 0;},n=>{Require(n==5,"Channel bound");draws++;return 3;})==new SpectralHitRoll(0,3)&&draws==2,"Zero amount skipped channel or chill");
        var p=Owner(20,0,3200);var wild=new EncounterCreature();
        try
        {
            wild.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Unique,ChampionKind.Champion,[EliteModifier.SpectralHit],EliteAura.None,null,new Random(17));wild.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);wild.AIObject.AITimer.Stop();
            Require(EliteCombat.Enemy(wild,p,18)&&EliteCombat.CanAct(wild),"Actual wild target gate");
            for(int channel=0;channel<5;channel++)
            {
                TimedEffects.Clear(p);p.Hits=p.HitsMax;int hp=SkillHealth.Balance256(p);var rng=new FixedRandom(channel);int amount=wild.Basis.MinDamage*66/100;
                EliteCombat.Hit(wild,p,0,ResistanceType.Physical,random:rng);
                Require(rng.AmountCalls==1&&rng.ChoiceCalls==1,"Actual wild draw dispatch");
                Require(hp-SkillHealth.Balance256(p)==(channel==4?0:amount*256),"Actual wild direct typed/poison amount");
                var state=State(p);Require((state?.PoisonMs>0)==(channel==4)&&(state?.ChillPercent>0)==(channel==3),"Actual wild post-hit status");
            }
        }
        finally{wild.Delete();p.Delete();}
        Require(cases==2000,"Missing producer contexts");
    }
    private static void Boundaries()
    {
        var p=Owner(19,3,3240);PoisonDaggerChecks.Named(p,"Wormskull");var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));NecroSnapshotChecks.MissingMetadata(b);
        try
        {
            Require(b.Rank==20&&IronGolemUpgrades.HardRank(b)==19,"Hard/soft boundary setup");int draws=0;Require(NpcSpectralHit.Capture(b,n=>{draws++;return 0;})==null&&draws==0,"Soft point unlocked Spectral/drew RNG");
            var before=new CreatureResistances(b.PhysicalResistance,NamedElements.MagicResistance(b),b.FireResistance,b.EnergyResistance,b.ColdResistance,b.PoisonResistance);var expected=EncounterProfiles.Resistances(before,null,[EliteModifier.SpectralHit]);
            string prepared=JsonSerializer.Serialize(b.Prepared);ulong seed=NpcEquipmentRandom.State(b).Seed;SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);int life=SkillHealth.Balance256(b),maximum=SkillHealth.Maximum256(b);Train(p,20);
            var roll=NpcSpectralHit.Capture(b,n=>{draws++;return 0;});Require(draws==2&&roll.Roll.Amount==b.SourceWeaponBase.Min*66/100,"Spectral raw innate basis or draw count");
            int Original(ResistanceType t)=>t==ResistanceType.Poison?100:t is ResistanceType.Cold or ResistanceType.Fire or ResistanceType.Energy?74:0;
            Require(IronGolemUpgrades.Resistance(b,ResistanceType.Cold,Original)==114&&IronGolemUpgrades.Resistance(b,ResistanceType.Fire,Original)==74&&IronGolemUpgrades.Resistance(b,ResistanceType.Energy,Original)==74,"Spectral cold-first/two-immunity rule");
            Require(IronGolemUpgrades.Resistance(b,ResistanceType.Fire,_=>75)==75&&IronGolemUpgrades.Resistance(b,ResistanceType.Fire,_=>50)==90,"Spectral75threshold");
            Require(b.ColdResistance==expected[4]&&b.FireResistance==expected[2]&&b.EnergyResistance==expected[3],"Actual Iron resistance composition");
            Require(JsonSerializer.Serialize(b.Prepared)==prepared&&NpcEquipmentRandom.State(b).Seed==seed&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==maximum,"Queries/training recaptured recipe,rolled RNG or healed");
            var t=Target(p,b);try
            {
                var ordinary=NpcEquipmentDamage.Roll(b,t,_=>0,()=>99);var critical=NpcEquipmentDamage.Roll(b,t,_=>0,()=>0);
                Require(!ordinary.Damage.Intrinsic&&critical.Damage.Intrinsic&&critical.Damage.Physical256==ordinary.Damage.Physical256*2&&critical.Spectral==ordinary.Spectral&&critical.Damage.Elements.Magic==ordinary.Damage.Elements.Magic,"Intrinsic critical doubled independent Spectral or lost physical double");
                foreach(int flags in new[]{0,1|128,1|256,1|512,1|32768})
                {draws=0;var blocked=NpcEquipmentImpact.Prepare(b,t,flags,n=>{draws++;return 0;},()=>0);Require(blocked.Spectral==null&&draws==0&&blocked.Packet.Total256==0,"Miss/blocked no-fill allocated Spectral");}
            }finally{t.Delete();}
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(NpcSpectralHit.Capture(b,_=>throw new InvalidOperationException("Respec RNG"))==null&&b.FireResistance==before.Fire.Value,"Respec retained Spectral");Train(p,20);Require(b.FireResistance==expected[2],"Relearn failed");
        }
        finally{b.Delete();p.Delete();}
    }
    private static void Outcomes()
    {
        var p=Owner(20,3,3280);var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));
        try
        {
            for(int channel=0;channel<5;channel++)foreach(bool lethal in new[]{false,true})
            {
                var t=Target(p,b);try
                {
                    if(lethal){SkillHealth.SetBalance256(t,256);}int hp=SkillHealth.Balance256(t);var packet=Packet(p,b,t,channel);var raw=packet.Impact.Spectral.Roll;var damage=packet.Impact.Packet;DateTime at=TimedEffects.Now;
                    Require(damage.Total256==damage.Physical256+(channel==4?0:raw.Amount*256)&&damage.Poison256==0&&damage.ColdFrames==0&&State(t)==null,"Spectral poisoned/chilled early or multiplied raw amount");
                    var result=Release(p,b,t,packet,at);Require(result.Commit!=null,"Qualified Spectral contact missing commit");
                    if(lethal){Require(!t.Alive&&(State(t)?.PoisonMs??0)==0&&(State(t)?.ChillPercent??0)==0,"Lethal affix installed survivor status");}
                    else
                    {
                        Require(hp-SkillHealth.Balance256(t)==damage.Total256,"Typed prepared HP differs");var state=State(t);
                        Require((state?.PoisonMs>0)==(channel==4)&&(state?.ChillPercent>0)==(channel==3),"Prepared post-hit status channel");
                        if(channel==4){Require(state.PoisonMs==1600&&Math.Abs(state.PoisonRate-raw.Amount/1600.0)<1e-12&&state.PoisonSource==p.Serial.Value&&state.PoisonClock==null,"Spectral confused aggregate poison with item rate or lost owner credit");TimedEffects.Settle(t,at.AddMilliseconds(1600));Require(SkillHealth.Balance256(t)==hp-damage.Total256-raw.Amount*256,"Spectral aggregate expiry damage");}
                        if(channel==3){Require(state.ChillUntilUtc==at.AddMilliseconds(1600),"Spectral minimum cold duration");}
                    }
                }
                finally{t.Corpse?.Delete();t.Delete();}
            }
            foreach(bool immune in new[]{false,true})
            {
                var t=Target(p,b);try{t.SetResistance(ResistanceType.Physical,100);t.SetResistance(ResistanceType.Poison,immune?100:0);var packet=Packet(p,b,t,4);Require(packet.Impact.Packet.Total256==0,"Zero damage setup");Release(p,b,t,packet);Require((State(t)?.PoisonMs>0)==!immune,"Zero physical suppressed poison or poison immunity was pierced");}finally{t.Delete();}
            }
            var outside=Target(p,b);try{var packet=Packet(p,b,outside,3);int hp=SkillHealth.Balance256(outside);Release(p,b,outside,packet,range:false);Require(State(outside)==null&&SkillHealth.Balance256(outside)==hp,"Rejected range applied Spectral");}finally{outside.Delete();}
        }
        finally{b.Delete();p.Delete();}
    }
    private static void ItemPoison()
    {
        var p=Owner(20,3,3300);var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p,"The Jade Tan Do"));
        try
        {
            foreach(int channel in new[]{0,4})
            {
                var t=Target(p,b);try
                {
                    var packet=Packet(p,b,t,channel);var item=packet.Impact.Poison;var damage=packet.Impact.Packet;
                    Require(item.Rate256>0&&item.Frames>0&&damage.Poison256==item.Rate256&&damage.Total256==damage.Physical256+damage.Magic256+item.Rate256,"Original poison initial rate changed with Spectral");
                    Require(channel!=4||item.Rate256/10240.0>packet.Impact.Spectral.Roll.Amount/1600.0,"Need stronger ordinary item poison");
                    int hp=SkillHealth.Balance256(t);DateTime at=TimedEffects.Now;Release(p,b,t,packet,at);var state=State(t);
                    Require(hp-SkillHealth.Balance256(t)==damage.Total256&&state.PoisonClock?.Rate256==item.Rate256&&state.PoisonMs==item.Frames*40&&state.PoisonSource==p.Serial.Value,"Spectral overwrote stronger original poison or duplicated initial HP");
                }finally{t.Delete();}
            }
        }
        finally{b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Producer();Boundaries();Outcomes();ItemPoison();var p=Owner(20,3,3320);var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));var poisoned=Target(p,b);var cold=Target(p,b);
        Release(p,b,poisoned,Packet(p,b,poisoned,4));Release(p,b,cold,Packet(p,b,cold,3));uint removed=b.Serial.Value;b.Delete();
        b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));var legacy=new NecromancerMinion{Name="Historical Iron Spectral [fixture]",Frozen=true,CantWalk=true};legacy.Prepare(p,CombatAbility.IronGolem);legacy.SetControlMaster(p);legacy.MoveToWorld(p.Location,p.Map);legacy.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(legacy);
        Require(NpcSpectralHit.Capture(legacy,_=>0)!=null,"Historical Iron missing live affix");var test=Target(p,legacy);
        try{typeof(NecromancerMinion).GetMethod("Strike",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(legacy,[test]);Require(SkillHealth.Balance256(test)<SkillHealth.Maximum256(test),"Historical fallback strike did not execute");}finally{test.Delete();}
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);SkillHealth.SetBalance256(legacy,SkillHealth.Maximum256(legacy)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);TimedEffects.PreventHealing(legacy,TimedEffects.Now);
        var held=TimedEffects.Now.Add(FixtureSpace.Retention);TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==poisoned.Serial.Value?x with{UpdatedUtc=held}:x.Owner==cold.Serial.Value?x with{UpdatedUtc=held,ChillUntilUtc=held.AddMilliseconds(1600)}:x).ToArray());
        poisoned.Internalize();cold.Internalize();PersistentMinions.Suspend(p);p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,legacy.Serial.Value,removed,poisoned.Serial.Value,cold.Serial.Value,JsonSerializer.Serialize(b.Prepared),NpcEquipmentRandom.State(b).Seed,SkillHealth.Balance256(b),SkillHealth.Balance256(legacy),b.FireResistance,legacy.FireResistance,State(poisoned),State(cold),0)));
        Log.Information("Iron Spectral:2000wild RNG parity contexts,actual five wild/prepared outcomes,survivor cold/aggregate poison,critical exclusion,hard19+soft1/train20/respec/relearn,no-fill/range/lethal/immunity and historical fallback pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(IronGolemBody)World.FindMobile((Serial)s.Body);var legacy=(NecromancerMinion)World.FindMobile((Serial)s.Legacy);var poisoned=(BaseCreature)World.FindMobile((Serial)s.PoisonTarget);var cold=(BaseCreature)World.FindMobile((Serial)s.ColdTarget);
        Require(s.Generation<2&&b.ControlMaster==p&&legacy.ControlMaster==p&&JsonSerializer.Serialize(b.Prepared)==s.Prepared&&NpcEquipmentRandom.State(b).Seed==s.Seed&&SkillHealth.Balance256(b)==s.Life&&SkillHealth.Balance256(legacy)==s.LegacyLife&&b.FireResistance==s.Fire&&legacy.FireResistance==s.LegacyFire,"Saved Spectral actor/recipe/wounds/stream");
        Require(JsonSerializer.Serialize(State(poisoned))==JsonSerializer.Serialize(s.Poison)&&JsonSerializer.Serialize(State(cold))==JsonSerializer.Serialize(s.Cold)&&s.Poison.PoisonSource==p.Serial.Value,"Saved source-removed aggregate/chill changed");
        DateTime at=TimedEffects.Now;TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==s.PoisonTarget?x with{UpdatedUtc=at}:x.Owner==s.ColdTarget?x with{UpdatedUtc=at,ChillUntilUtc=at.AddMilliseconds(1600)}:x).ToArray());
        int hp=SkillHealth.Balance256(poisoned);TimedEffects.Settle(poisoned,at.AddMilliseconds(1600));TimedEffects.Settle(cold,at.AddMilliseconds(1600));Require(hp-SkillHealth.Balance256(poisoned)==(int)Math.Round(s.Poison.PoisonRate*s.Poison.PoisonMs*256)&&(State(cold)?.ChillPercent??0)==0,"Reloaded source-removed poison/chill expiry");
        // Return only these observation rows to their held snapshot for the next
        // real restart; expiry above is verified separately from held persistence.
        TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=s.PoisonTarget&&x.Owner!=s.ColdTarget).Append(s.Poison).Append(s.Cold).ToArray());
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(19,3,3360);FixtureNetwork.Authenticate(network,p,"iron-spectral");var b=IronGolemDamageChecks.Learned(p,IronGolemDamageChecks.Original(p));NecroSnapshotChecks.MissingMetadata(b);var t=Target(p,b);TimedEffects.PreventHealing(t,TimedEffects.Now);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        NpcEquipmentRandom.Set(b,IronGolemWorldChecks.AttackSeed(b,t));b.RestoreController(b.Prepared.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});int hp=SkillHealth.Balance256(t),damage=0;string predicted=null;bool captured=false;long deadline=Core.TickCount+12000;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&b.Prepared.Controller.Fault==null,"Natural Spectral timeout/fault");
                if(!captured&&b.Prepared.Controller is{Mode:4} state)
                {
                    Require(IronGolemUpgrades.HardRank(b)==19&&(State(t)?.ChillPercent??0)==0&&(State(t)?.PoisonMs??0)==0,"Spectral active before training");Train(p,20);IronGolemContact.Prepared packet=null;ulong chosen=0;
                    for(ulong seed=1;seed<10000;seed++){NpcEquipmentRandom.Set(b,seed);var candidate=IronGolemContact.Prepare(b,t,IronGolemContact.Require(b,p,t),()=>IronGolemWorld.Result(b,p,state,t));if((candidate.Impact.Packet.ResultFlags&1)!=0&&candidate.Impact.Spectral?.Roll.Channel==3){packet=candidate;chosen=seed;break;}candidate.Cancel();}
                    Require(packet!=null,"Natural cold contact seed");damage=packet.Impact.Packet.Total256;predicted=JsonSerializer.Serialize(packet.Impact);packet.Cancel();NpcEquipmentRandom.Set(b,chosen);Require((State(t)?.ChillPercent??0)==0&&(State(t)?.PoisonMs??0)==0&&SkillHealth.Balance256(t)==hp,"Canceled Spectral prediction applied status/HP");captured=true;
                }
                if(SkillHealth.Balance256(t)==hp){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&hp-SkillHealth.Balance256(t)==damage&&State(t)?.ChillPercent>0&&b.Prepared.Controller.Attempts==1,$"Actual natural hard20 release:captured={captured},expected={damage},actual={hp-SkillHealth.Balance256(t)},chill={State(t)?.ChillPercent},attempts={b.Prepared.Controller.Attempts},wanted={t.Serial.Value},controller={JsonSerializer.Serialize(b.Prepared.Controller)},packet={predicted}");End();Log.Information("iron-golem-spectral-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"iron-golem-spectral-live-result: failed");}
        }
        Log.Information("iron-golem-spectral-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
