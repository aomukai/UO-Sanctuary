// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BloodLifeTapChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BloodLifeTapChecks));
    private sealed record Saved(uint Owner,uint Source,uint Target,NecromancerCurse Curse,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-life-tap-fixtures.json");
    public static void Configure()
    {
        Register("blood-life-tap-check",Check);Register("blood-life-tap-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("blood-life-tap-live","Connected learned Blood hard20 proc",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("blood-life-tap-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Blood Life Tap fixed NPC curse/source event",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=20,int row=3160)=>NpcOpenWoundsChecks.Owner(rank,row);
    private static BloodGolemWorldBody Body(PlayerMobile p)=>NpcOpenWoundsChecks.Body(p);
    private static GraveyardSkeleton Target(PlayerMobile p,BloodGolemBody b)
    {var t=NpcOpenWoundsChecks.Target(p,b);Require(GemCombat.Drain(t)==0,"Life Tap isolation needs drain-zero skeleton");return t;}
    private static ulong ProcSeed(bool success)
    {
        for(ulong n=1;n<10000;n++){ulong s=n;SourceRandom.Roll(ref s);if((SourceRandom.Roll(ref s)%100<5)==success){return n;}}
        throw new InvalidOperationException("Missing wound/proc seed");
    }
    internal static IronGolemContact.Prepared Packet(PlayerMobile p,BloodGolemBody b,BaseCreature t,bool proc=true,int flags=5)
    {
        var packet=IronGolemContact.Prepare(b,t,UnarmedSummonContact.Require(b,p,t),()=>flags);
        NpcEquipmentRandom.Set(b,ProcSeed(proc));return packet;
    }
    internal static MonsterContactResult Release(PlayerMobile p,BloodGolemBody b,BaseCreature t,IronGolemContact.Prepared packet,DateTime? at=null)
        =>IronGolemContact.Release(b,p,t,packet,_=>true,at??TimedEffects.Now);
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-hit-skill-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var v=row[0].EnumerateArray().Select(x=>x.GetInt64()).ToArray();if(v[4]!=0){continue;}
            bool actual=NpcHitSkills.Chance((int)v[0],(uint)v[2],v[3]!=0,(uint)v[1]);
            Require(actual==(row[1][0].GetInt32()!=0),"Complete proc source chance/hitflag/RNG mismatch");count++;
        }
        Require(count==720,"Missing proc source controls");
    }
    private static void Events()
    {
        var p=Owner(19);PoisonDaggerChecks.Named(p,"Wormskull");var b=Body(p);NecroSnapshotChecks.MissingMetadata(b);var t=Target(p,b);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try
        {
            Require(b.Rank==20&&NpcHitSkills.BloodChance(b)==0,"Soft ranks unlocked Blood proc");var packet=Packet(p,b,t);ulong seed=NpcEquipmentRandom.State(b).Seed,expected=seed;SourceRandom.Roll(ref expected);
            Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Hard19 proc/draw");
            t.Delete();t=Target(p,b);packet=Packet(p,b,t);MagePetBuffChecks.Train(p,"necromancer.bloodgolem",20);MagePetBuffChecks.Train(p,"necromancer.lifetap",20);
            var near=Target(p,b);near.MoveToWorld(new(t.X+4,t.Y,t.Z),t.Map);var far=Target(p,b);far.MoveToWorld(new(t.X+5,t.Y,t.Z),t.Map);
            try
            {
                NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);SkillHealth.SetBalance256(b,1001);int life=SkillHealth.Balance256(b),physical=packet.Impact.Packet.Physical256;DateTime at=TimedEffects.Now;
                expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);SourceRandom.Roll(ref expected);var controller=b.WorldState.Controller;
                Release(p,b,t,packet,at);var curse=NecromancerEffects.Curse(t);
                Require(curse is{Ability:CombatAbility.LifeTap,Rank:5}&&curse.Source==b.Serial.Value&&curse.OriginX==b.X&&curse.OriginY==b.Y&&curse.UntilUtc==at.AddMilliseconds(25600),"Proc inherited owner rank/caster/duration");
                Require(NecromancerEffects.Curse(near)?.Rank==5&&NecromancerEffects.Curse(far)==null,"Fixed rank5target-centered radius boundary");
                Require(t.Aggressors.Any(a=>a.Attacker==b)&&near.Aggressors.Any(a=>a.Attacker==b),"Indirect item curse lost native aggression credit");
                Require(physical>0&&SkillHealth.Balance256(b)==life+NecromancerEffects.LifeTapHealing256(physical,50)&&NpcEquipmentRandom.State(b).Seed==expected,"First proccing swing did not heal from pre-HP physical or duplicated RNG");
                Require(NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0&&b.WorldState.Controller==controller,"NPC proc spent mana or replaced its attack controller");
            }
            finally{near.Delete();far.Delete();}
            t.Delete();t=Target(p,b);packet=Packet(p,b,t,false);expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);SourceRandom.Roll(ref expected);Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Failed5percentproc altered curse or skipped draw");
            t.Delete();t=Target(p,b);packet=Packet(p,b,t,true,0);expected=NpcEquipmentRandom.State(b).Seed;Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Miss invoked wound/proc");
            t.Delete();t=Target(p,b);var shields=SourceBlocking.Snapshot();
            try
            {
                SourceBlocking.Restore(shields.Where(x=>x.Actor!=t.Serial.Value).Append(new(t.Serial.Value,CreatureProfiles.Definition(t).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;packet=null;
                for(ulong n=1;n<1000;n++){NpcEquipmentRandom.Set(b,n);var candidate=UnarmedSummonContact.Prepare(b,p,t,UnarmedSummonContact.Require(b,p,t));if((candidate.Impact.Packet.ResultFlags&16)!=0){packet=candidate;break;}candidate.Cancel();}
                Require(packet!=null,"Actual block seed");expected=NpcEquipmentRandom.State(b).Seed;Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Actual shield block invoked proc");
            }
            finally{SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;}
            // Callback's packet gate precedes RNG even if called directly.
            expected=NpcEquipmentRandom.State(b).Seed;Require(!NpcHitSkills.BloodLifeTap(b,t,new(0,0,0,0,1),TimedEffects.Now)&&NpcEquipmentRandom.State(b).Seed==expected,"Missing hitflag0x20drewproc");
            foreach(bool armor in new[]{false,true})
            {
                t.Delete();t=Target(p,b);if(armor){NecromancerEffects.Profiles.Store(t,new(BoneArmor:100000));}else{t.SetResistance(ResistanceType.Physical,100);}
                packet=Packet(p,b,t);int life=SkillHealth.Balance256(b);Require(packet.Impact.Packet.Total256==0,"Proc zero-damage setup");Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)?.Rank==5&&SkillHealth.Balance256(b)==life,"Zero/absorbed contact lost proc or fabricated Life Tap healing");
            }
            t.Delete();t=Target(p,b);packet=Packet(p,b,t);var modes=MonsterUninterruptibles.Snapshot();
            try{MonsterUninterruptibles.Restore(modes.Where(x=>x.Actor!=b.Serial.Value).Append(new(b.Serial.Value,true,false,0)).ToArray());expected=NpcEquipmentRandom.State(b).Seed;SourceRandom.Roll(ref expected);SourceRandom.Roll(ref expected);Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Uninterruptible cast incorrectly applied or skipped prior draw");}
            finally{MonsterUninterruptibles.Restore(modes);}
            foreach(var ability in new[]{CombatAbility.Attract,CombatAbility.LifeTap})
            {
                t.Delete();t=Target(p,b);Require(NecromancerEffects.Apply(p,t,ability,20),"Existing curse setup");var previous=NecromancerEffects.Curse(t);packet=Packet(p,b,t);Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==previous,"Proc overwrote Attract/stronger same curse");
            }
            foreach(int rank in new[]{3,5})
            {
                t.Delete();t=Target(p,b);Require(NecromancerEffects.Apply(p,t,CombatAbility.LifeTap,rank),"Equal/weaker curse setup");var previous=NecromancerEffects.Curse(t);packet=Packet(p,b,t);DateTime at=TimedEffects.Now;Release(p,b,t,packet,at);var next=NecromancerEffects.Curse(t);
                Require(next?.Rank==5&&next.Source==(rank==5?p.Serial.Value:b.Serial.Value)&&next.UntilUtc==at.AddMilliseconds(25600),"NPC proc lost shared equal-source refresh/weaker replacement");
            }
            t.Delete();t=Target(p,b);packet=Packet(p,b,t);SkillHealth.SetBalance256(t,256);SkillHealth.SetBalance256(b,1001);int before=SkillHealth.Balance256(b),raw=packet.Impact.Packet.Physical256;
            Require(raw>256,"Need lethal overkill");Release(p,b,t,packet);Require(!t.Alive&&SkillHealth.Balance256(b)==before+NecromancerEffects.LifeTapHealing256(raw,50)&&t.LastKiller==b&&ClassLoot.Killer(t)==p,$"First-proc lethal hit healed post-clip or lost owner kill: alive={t.Alive},before={before},after={SkillHealth.Balance256(b)},raw={raw},expected={before+NecromancerEffects.LifeTapHealing256(raw,50)},max={SkillHealth.Maximum256(b)},killer={t.LastKiller?.Serial.Value},owner={p.Serial.Value},curse={NecromancerEffects.Curse(t)?.Rank}");
            t.Delete();t=Target(p,b);packet=Packet(p,b,t);expected=NpcEquipmentRandom.State(b).Seed;ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Release(p,b,t,packet);Require(NecromancerEffects.Curse(t)==null&&NpcEquipmentRandom.State(b).Seed==expected,"Windup respec retained proc or wound RNG");
        }
        finally{SourceBlocking.BlockRoll=block;t.Corpse?.Delete();t.Delete();b.Delete();p.Delete();}
    }
    private static void ItemCastTarget()
    {
        var p=Owner();var b=Body(p);var t=Target(p,b);var original=Target(p,b);
        original.MoveToWorld(new(t.X+5,t.Y,t.Z),t.Map);
        var modes=MonsterUninterruptibles.Snapshot();
        try
        {
            b.Combatant=original;
            Require(b.Combatant==original,"Actual item-cast original target setup");
            var controller=b.WorldState.Controller;
            NecromancerEffects.SetMana256(p,0);NecromancerEffects.SetMana256(b,0);
            ulong seed=NpcEquipmentRandom.State(b).Seed;
            Require(NpcItemSpellCasts.LifeTap(b,t,5,TimedEffects.Now),"Actual item-effect Life Tap executor");
            Require(b.Combatant==original&&b.WorldState.Controller==controller&&NpcEquipmentRandom.State(b).Seed==seed,
                $"Item cast changed state:combatant={b.Combatant?.Serial.Value},expected={original.Serial.Value},controllerSame={b.WorldState.Controller==controller},seed={NpcEquipmentRandom.State(b).Seed},expectedSeed={seed}");
            Require(NecromancerEffects.Curse(t) is{Rank:5} curse&&curse.Source==b.Serial.Value&&NecromancerEffects.Curse(original)==null
                &&t.Aggressors.Any(a=>a.Attacker==b),"Item cast lost source/area/aggression credit");
            Require(!NpcItemSpellCasts.KeepsCombatTarget(b),"Successful item cast leaked targeting scope");
            b.Combatant=t;Require(b.Combatant==t,"Ordinary targeting remained locked after item cast");
            b.Combatant=original;Require(b.Combatant==original,"Restored ordinary target setup after cast");
            Require(NecromancerEffects.ManaBalance256(p)==0&&NecromancerEffects.ManaBalance256(b)==0,"Item cast paid player/NPC mana");
            var untouched=Target(p,b);
            try
            {
                MonsterUninterruptibles.Restore(modes.Where(x=>x.Actor!=b.Serial.Value).Append(new(b.Serial.Value,true,false,0)).ToArray());
                Require(!NpcItemSpellCasts.LifeTap(b,untouched,5,TimedEffects.Now)&&NecromancerEffects.Curse(untouched)==null
                    &&b.Combatant==original&&b.WorldState.Controller==controller&&NpcEquipmentRandom.State(b).Seed==seed,
                    "Refused item cast changed source targeting/controller/RNG or delivered a curse");
                Require(!NpcItemSpellCasts.KeepsCombatTarget(b),"Refused item cast leaked targeting scope");
            }
            finally{untouched.Delete();}
        }
        finally{MonsterUninterruptibles.Restore(modes);b.Combatant=null;t.Delete();original.Delete();b.Delete();p.Delete();}
    }
    private static void Check()
    {
        Source();ItemCastTarget();Events();var p=Owner(20,3200);var b=Body(p);var t=Target(p,b);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        try{Release(p,b,t,Packet(p,b,t));var curse=NecromancerEffects.Curse(t);Require(curse?.Rank==5,"Saved NPC curse");curse=curse with{UntilUtc=TimedEffects.Now.AddHours(1)};NecromancerEffects.Profiles.Store(t,NecromancerEffects.Profiles.Find(t) with{Curse=curse});TimedEffects.Clear(t);uint source=b.Serial.Value;b.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,source,t.Serial.Value,curse,0)));}
        finally{SourceBlocking.BlockRoll=block;}
        Log.Information("Blood Life Tap:720complete proc comparisons;live hard19+soft1refusal/train20/respec,real source rank5area/no mana/first swing including overkill,miss/zero/absorb/uninterruptible/stronger/Attract pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(s.Generation<2&&World.FindMobile((Serial)s.Source)==null&&NecromancerEffects.Curse(t)==s.Curse&&s.Curse.Source==s.Source&&s.Curse.Rank==5,"Saved curse lost deleted NPC creator/rank/expiry");
        SkillHealth.SetBalance256(p,1001);Require(NecromancerEffects.LifeTap(p,t,257)==128&&SkillHealth.Balance256(p)==1129,"Source-removed saved curse lost defender healing");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network)
    {
        var p=Owner(20,3660);FixtureNetwork.Authenticate(network,p,"blood-tap");var b=Body(p);var t=Target(p,b);t.SetResistance(ResistanceType.Physical,100);NecromancerEffects.SetMana256(p,0);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
        NpcEquipmentRandom.Set(b,BloodGolemWorldChecks.Seed(b,t));b.Ground.Store(b.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});long deadline=Core.TickCount+10000;bool captured=false;
        void End(){SourceBlocking.BlockRoll=block;t.Delete();b.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running,"Natural Blood proc timeout/disconnect");
                if(!captured&&b.WorldContact!=null){NpcEquipmentRandom.Set(b,ProcSeed(true));captured=true;}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(captured&&curse is{Ability:CombatAbility.LifeTap,Rank:5}&&curse.Source==b.Serial.Value&&b.WorldState.Controller.Attempts==1&&NecromancerEffects.ManaBalance256(p)==0,"Natural learned Blood proc context/action/mana");
                b.Delete();SkillHealth.SetBalance256(p,1001);Require(NecromancerEffects.Curse(t)==curse&&NecromancerEffects.LifeTap(p,t,257)==128,"Natural source deletion removed curse/healing");End();Log.Information("blood-life-tap-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"blood-life-tap-live-result: failed");}
        }
        Log.Information("blood-life-tap-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
