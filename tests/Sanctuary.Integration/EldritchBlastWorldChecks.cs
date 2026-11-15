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

public static class EldritchBlastWorldChecks
{
    // Explicit native encounter resistance control. It does not remap the
    // unmapped UO Lich Lord or claim a D2 source species certificate.
    public sealed class ImmuneRecipient:EncounterCreature
    {
        [Constructible]public ImmuneRecipient(){}
        public ImmuneRecipient(Serial serial):base(serial){}
        protected override int[] AdjustResistances(int[] resistances)=>[0,100,0,0,0,0];
        public override bool CanRegenHits=>false;
        public override void OnThink(){}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EldritchBlastWorldChecks));
    private sealed record Saved(int Generation,uint Owner,uint Helm,uint[] Targets,EldritchBlastState State,int Life,int Mana,string Bane,string Siphon,string Purge);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"eldritch-blast-world-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="EldritchWorldFixture"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Creating||m.Name=="EldritchWorldFixture"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"eldritch-blast-world-reload-check":"eldritch-blast-world-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Eldritch Blast periodic Hex refresh,magic recovery and saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static GoatmanNpcContactChecks.Recipient Target(PlayerMobile p,int x,int y=0)
    {
        var t=new GoatmanNpcContactChecks.Recipient{Name="EldritchWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetStr(100);t.SetHits(30000);t.Hits=t.HitsMax;
        t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.Combatant=null;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(type,0);}
        Require(NamedElements.MagicResistance(t)==0,"Fixture magic resistance");return t;
    }
    private static void Attach(PlayerMobile p,BaseCreature t,CombatAbility skill,DateTime at)
    {
        foreach(var a in new[]{CombatAbility.HexBane,CombatAbility.HexSiphon,CombatAbility.HexPurge})
        {if(a==CombatAbility.HexBane){HexBane.ClearDebuff(t);}else if(a==CombatAbility.HexSiphon){HexSiphon.ClearDebuff(t);}else{HexPurge.ClearDebuff(t);}}
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
        // Fresh setup after removing +skills must not try to overwrite a
        // stronger retained buff; production correctly refuses that recast.
        if(skill==CombatAbility.HexBane){HexBane.ClearBuff(p);}
        else if(skill==CombatAbility.HexSiphon){HexSiphon.ClearBuff(p);}
        else{HexPurge.ClearBuff(p);}
        if(skill==CombatAbility.HexBane)
        {Require(HexBane.CastPrepared(p,true)&&HexBane.ApplyPrepared(p,t,HexBane.Buff(p),true,at,true),"Bane actual attachment");}
        else if(skill==CombatAbility.HexSiphon)
        {Require(HexSiphon.CastPrepared(p,true)&&HexSiphon.ApplyPrepared(p,t,HexSiphon.Buff(p),true,at,true),"Siphon actual attachment");}
        else
        {
            Require(HexPurge.CastPrepared(p,true),"Purge buff");var v=SkillMechanics.Values(p,skill);var plan=HexPurge.Plan(v);
            Require(HexPurgeReactionProposal.AttachPrepared(p,t,HexPurge.Buff(p),plan,
                HexPurgeReactionProposal.Capture(plan,0,new(0,0,0,0,0),SourceRandom.InitLow(31337)),true,at),"Purge actual attachment");
        }
    }
    private static void Scene(NetState network)
    {
        Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="EldritchWorldFixture";p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.eldritchblast",20);
        ClassSkillChecks.Train(p,"warlock.hexpurge",20);ClassSkillChecks.Train(p,"warlock.psychicward",4);
        FixtureSpace.Corridor(p,6800,16,3300);var account=FixtureNetwork.Authenticate(network,p,"eldritch-world-");
        var weapon=new Dagger();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Hex weapon");
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(719),true);
        p.AddToBackpack(helm);Require(p.EquipItem(helm),"Real +skills helm");ClassGear.Refresh(p);
        var targets=new[]{Target(p,1),Target(p,2),Target(p,1,1),Target(p,0,2),Target(p,4)};
        var seed=EldritchBlastWorld.SeedLow;int seeds=0;EldritchBlastWorld.SeedLow=()=>{seeds++;return 31337;};bool kept=false;
        try
        {
            Require(EldritchBlastWorld.Approved&&WarlockCombat.Implemented(CombatAbility.EldritchBlast),"Approved world admission missing");
            var at=TimedEffects.Now;
            Attach(p,targets[0],CombatAbility.HexBane,at);Attach(p,targets[1],CombatAbility.HexSiphon,at);Attach(p,targets[2],CombatAbility.HexPurge,at);Attach(p,targets[4],CombatAbility.HexBane,at);
            NecromancerEffects.SetMana256(p,0);Require(EldritchBlastWorld.ReleasePrepared(p,at)==null&&seeds==0,"Underfunded cast drew seed or stored state");
            NecromancerEffects.SetMana256(p,100*256+17);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2-13);
            int mana=NecromancerEffects.ManaBalance256(p);var state=EldritchBlastWorld.ReleasePrepared(p,at);
            Require(state!=null&&state.Numbers.Rank==21&&state.Numbers.DurationFrames==1200&&state.NextUtc==at.AddMilliseconds(1200)
                &&NecromancerEffects.ManaBalance256(p)==mana-3968&&seeds==1,"Once-only fractional cast cost/clock/snapshot");
            var before=targets.Select(SkillHealth.Balance256).ToArray();var bane=HexBane.Debuff(targets[0]);var siphon=HexSiphon.Debuff(targets[1]);var purge=HexPurge.Debuff(targets[2]);
            Require(!EldritchBlastWorld.TickPrepared(p,at.AddMilliseconds(1199)).Due&&targets.Select(SkillHealth.Balance256).SequenceEqual(before),"Pulse before exact deadline");
            p.AddToBackpack(helm);ClassGear.Refresh(p);ClassSkillChecks.Train(p,"warlock.psychicward",5);
            Require(EldritchBlastWorld.State(p)==state&&SkillMechanics.Rank(p,CombatAbility.EldritchBlast)==20,"Snapshot changed with gear/hard point changes");
            ulong expectedSeed=state.Seed;int roll=SourceSpellPacketRoll.Range(ref expectedSeed,state.Numbers.MagicMinimum256,state.Numbers.MagicMaximum256);
            int recovery=0;for(int i=0;i<3;i++)
            {int d=MonsterVariants.Difficulty(targets[i])switch{MonsterDifficulty.Nightmare=>2,MonsterDifficulty.Hell=>3,_=>1};recovery+=WeaponLeech.Calculate(roll,before[i],0,5,5,GemCombat.Drain(targets[i]),d,d).Life256;}
            int life=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);int events=0;
            void Notice(SkillDamageContact c){if(targets.Contains(c.Target)){events++;}}
            SkillDamageEvents.BeforeDirectDamage+=Notice;EldritchBlastPulse pulse;
            try{pulse=EldritchBlastWorld.TickPrepared(p,state.NextUtc);}finally{SkillDamageEvents.BeforeDirectDamage-=Notice;}
            int expectedLife=Math.Min(recovery,SkillHealth.Maximum256(p)-life),expectedMana=Math.Min(recovery,p.ManaMax*256-mana);
            Require(pulse is{Due:true,Refreshed:3,Damaged:3,Fault:null}&&pulse.Life256==expectedLife&&pulse.Mana256==expectedMana
                &&SkillHealth.Balance256(p)==life+expectedLife&&NecromancerEffects.ManaBalance256(p)==mana+expectedMana&&events==0,"Pulse/recovery or direct weapon reaction escaped periodic boundary");
            Require(Enumerable.Range(0,3).All(i=>before[i]-SkillHealth.Balance256(targets[i])==roll)
                &&targets.Skip(3).Select(SkillHealth.Balance256).SequenceEqual(before.Skip(3))&&EldritchBlastWorld.State(p).Seed==expectedSeed,"Shared roll/hex filter/range/seed");
            Require(HexBane.Debuff(targets[0])==bane with{UntilUtc=state.NextUtc.AddMilliseconds(bane.RefreshFrames.Value*40L)}
                &&HexSiphon.Debuff(targets[1])==siphon with{UntilUtc=state.NextUtc.AddMilliseconds(siphon.RefreshFrames.Value*40L)}
                &&HexPurge.Debuff(targets[2])==purge with{UntilUtc=state.NextUtc.AddMilliseconds(purge.Plan.DebuffFrames*40L)},"Hex maintenance rewrote creator/stats/reaction/charges");
            Require(!EldritchBlastWorld.TickPrepared(p,state.NextUtc).Due,"Repeated tick replayed pulse");
            var next=EldritchBlastWorld.State(p);before=targets.Select(SkillHealth.Balance256).ToArray();var skipped=EldritchBlastWorld.TickPrepared(p,at.AddMilliseconds(3600),false);
            Require(skipped is{Due:true,Refreshed:0,Damaged:0}&&EldritchBlastWorld.State(p).NextUtc==at.AddMilliseconds(4800)
                &&EldritchBlastWorld.State(p).Seed==next.Seed&&targets.Select(SkillHealth.Balance256).SequenceEqual(before),"Offline catchup/RNG/refresh");
            var late=EldritchBlastWorld.TickPrepared(p,at.AddMilliseconds(6000),true);Require(late.Refreshed==3&&EldritchBlastWorld.State(p).NextUtc==at.AddMilliseconds(7200),"Late tick must pulse once and retain grid");
            // Equal expiry wins, with no final pulse or recovery.
            var expiry=EldritchBlastWorld.State(p).UntilUtc;before=targets.Select(SkillHealth.Balance256).ToArray();
            Require(!EldritchBlastWorld.TickPrepared(p,expiry,true).Due&&EldritchBlastWorld.State(p)==null&&targets.Select(SkillHealth.Balance256).SequenceEqual(before),"Expiry emitted an extra pulse");
            LegacyAndAtomic(p,targets[0]);
            ImmunityAndLifecycle(p,targets);
            // This is an inspection clock, not a gameplay lifetime extension.
            // Candidate auto-ticking stays disabled; save the untouched capture
            // and inspect it with original absolute dates on both real reloads.
            Require(p.EquipItem(helm),"Restore gear");ClassGear.Refresh(p);var inspect=TimedEffects.Now.Add(FixtureSpace.Retention);
            var attachAt=TimedEffects.Now;
            Attach(p,targets[0],CombatAbility.HexBane,attachAt);Attach(p,targets[1],CombatAbility.HexSiphon,attachAt);Attach(p,targets[2],CombatAbility.HexPurge,attachAt);
            var baneSave=HexBane.Snapshot();HexBane.Restore(baneSave with{Debuffs=baneSave.Debuffs.Select(d=>d.Target==targets[0].Serial.Value?d with{UntilUtc=d.UntilUtc.Add(FixtureSpace.Retention)}:d).ToArray()});
            var siphonSave=HexSiphon.Snapshot();HexSiphon.Restore(siphonSave with{Debuffs=siphonSave.Debuffs.Select(d=>d.Target==targets[1].Serial.Value?d with{UntilUtc=d.UntilUtc.Add(FixtureSpace.Retention)}:d).ToArray()});
            var purgeSave=HexPurge.Snapshot();HexPurge.Restore(purgeSave with{Targets=purgeSave.Targets.Select(d=>d.Target==targets[2].Serial.Value?d with{UntilUtc=d.UntilUtc.Add(FixtureSpace.Retention)}:d).ToArray()});
            NecromancerEffects.SetMana256(p,100*256+17);state=EldritchBlastWorld.ReleasePrepared(p,inspect);int savedLife=SkillHealth.Balance256(p),savedMana=NecromancerEffects.ManaBalance256(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,helm.Serial.Value,targets.Select(t=>t.Serial.Value).ToArray(),state,savedLife,savedMana,
                JsonSerializer.Serialize(HexBane.Debuff(targets[0])),JsonSerializer.Serialize(HexSiphon.Debuff(targets[1])),JsonSerializer.Serialize(HexPurge.Debuff(targets[2])))));kept=true;
            Log.Information("Eldritch world:real +skills capture/fractional payment,three existing Hex types,shared magic roll,actual FP8 recovery,periodic event suppression,unhexed/range rejection,offline/late/expiry and legacy/atomic controls pass;approved world component retained");
        }
        finally
        {
            EldritchBlastWorld.SeedLow=seed;ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){EldritchBlastWorld.Clear(p);foreach(var t in targets){t.Delete();}p.Delete();}
        }
    }
    private static void ImmunityAndLifecycle(PlayerMobile p,BaseCreature[] others)
    {
        var locations=others.Select(t=>t.Location).ToArray();for(int i=0;i<others.Length;i++){others[i].MoveToWorld(new(p.X+10+i,p.Y,p.Z),p.Map);}
        var lich=new LichLord{Name="EldritchWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};lich.AIObject?.AITimer.Stop();
        try
        {
            MonsterVariants.Assign(lich,MonsterDifficulty.Hell);lich.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            Require(NamedElements.MagicResistance(lich)==100&&!SourceAuraTargets.Eligible(lich),"Unmapped Hell Lich source boundary changed");
        }
        finally{lich.Delete();}
        var immune=new ImmuneRecipient{Name="EldritchWorldFixture",Frozen=true,CantWalk=true,NoKillAwards=true};immune.AIObject?.AITimer.Stop();
        immune.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);immune.SetHits(30000);immune.Hits=immune.HitsMax;
        try
        {
            Require(NamedElements.MagicResistance(immune)==100,"Explicit encounter immunity control");var at=TimedEffects.Now;
            Attach(p,immune,CombatAbility.HexBane,at);var hex=HexBane.Debuff(immune);NecromancerEffects.SetMana256(p,100*256);
            var state=EldritchBlastWorld.ReleasePrepared(p,at);int life=SkillHealth.Balance256(immune),mana=NecromancerEffects.ManaBalance256(p);
            var pulse=EldritchBlastWorld.TickPrepared(p,state.NextUtc,true);
            Require(pulse is{Refreshed:1,Damaged:0,Life256:0,Mana256:0,Fault:null}&&SkillHealth.Balance256(immune)==life
                &&NecromancerEffects.ManaBalance256(p)==mana&&HexBane.Debuff(immune).UntilUtc>hex.UntilUtc,"Magic immunity prevented Hex refresh or granted recovery");
            state=EldritchBlastWorld.ReleasePrepared(p,at);
            var modifiers=state with{MagicMastery=50,Modifiers=new(20,0,0,0,0)};EldritchBlastWorld.Restore([modifiers]);
            pulse=EldritchBlastWorld.TickPrepared(p,modifiers.NextUtc,true);
            Require(pulse is{Refreshed:1,Damaged:1,Life256:0,Mana256:0,Fault:null}&&SkillHealth.Balance256(immune)<life,"Added elements must work through own defenses without magic leech");
            var saved=EldritchBlastWorld.State(p);int damage=SkillHealth.Balance256(immune);var start=p.Location;
            p.Internalize();pulse=EldritchBlastWorld.TickPrepared(p,saved.NextUtc,true);
            Require(pulse is{Due:true,Refreshed:0,Damaged:0}&&SkillHealth.Balance256(immune)==damage&&EldritchBlastWorld.State(p).Seed==saved.Seed,"Internalized owner continued combat");
            p.MoveToWorld(start,Map.Trammel);saved=EldritchBlastWorld.State(p);
            var dying=new PlayerMobile{Name="EldritchWorldFixture",Player=true,Body=0x190};dying.AddItem(new Backpack());
            ClassCreation.Profiles.Assign(dying,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(dying,99);ClassSkillChecks.Train(dying,"warlock.eldritchblast");
            dying.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);NecromancerEffects.SetMana256(dying,dying.ManaMax*256);
            try
            {
                var doomed=EldritchBlastWorld.ReleasePrepared(dying,at);Require(doomed!=null,"Lifecycle live fixture");dying.Kill();
                EldritchBlastWorld.TickPrepared(dying,doomed.NextUtc,true);Require(EldritchBlastWorld.State(dying)==null,"Dead owner retained periodic state");
            }
            finally{dying.Delete();}
            Log.Information("Eldritch world:explicit native encounter magic immunity retains Hex maintenance with zero magic recovery;unmapped Hell Lich source still refuses;captured potion components deal damage without magic leech;internal/dead owners stop");
        }
        finally{immune.Delete();EldritchBlastWorld.Clear(p);for(int i=0;i<others.Length;i++){others[i].MoveToWorld(locations[i],p.Map);}}
    }
    private static void LegacyAndAtomic(PlayerMobile p,BaseCreature target)
    {
        var at=TimedEffects.Now;Attach(p,target,CombatAbility.HexBane,at);var old=HexBane.Debuff(target);
        var snap=HexBane.Snapshot();HexBane.Restore(snap with{Debuffs=snap.Debuffs.Select(d=>d.Target==target.Serial.Value?d with{RefreshFrames=null}:d).ToArray()});
        string legacy=JsonSerializer.Serialize(HexBane.Debuff(target));Require(!legacy.Contains("RefreshFrames"),"Legacy shape was rewritten");
        bool refused=false;try{WeaponHexRefresh.RefreshPrepared(target,at.AddMilliseconds(1200));}catch(NotSupportedException){refused=true;}
        Require(refused&&JsonSerializer.Serialize(HexBane.Debuff(target))==legacy,"Unknown legacy duration was guessed or mutated");HexBane.Restore(snap);
        Require(!HexBane.RefreshPrepared(old,old.UntilUtc)&&HexBane.Debuff(target)==old,"Expired Hex resurrected");
        HexBane.ClearDebuff(target);Require(!HexBane.RefreshPrepared(old,at),"Stale removed Hex resurrected");HexBane.Restore(snap);
        var state=EldritchBlastWorld.ReleasePrepared(p,at);Require(state!=null,"Atomic fixture state");var all=EldritchBlastWorld.Snapshot();
        foreach(var malformed in new[]{state with{Seed=0},state with{NextUtc=state.NextUtc.AddTicks(1)},state with{Numbers=state.Numbers with{LifeStealPercent=20}}})
        {
            refused=false;try{EldritchBlastWorld.Restore([malformed]);}catch(InvalidDataException){refused=true;}
            Require(refused&&EldritchBlastWorld.Snapshot().SequenceEqual(all),"Malformed restore partly committed");
        }
        EldritchBlastWorld.Clear(p);
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var account=FixtureNetwork.Authenticate(network,p,"eldritch-reload-");
        try
        {
            ClassGear.Refresh(p);foreach(var serial in saved.Targets){((BaseCreature)World.FindMobile((Serial)serial)).AIObject?.AITimer.Stop();}
            Require(EldritchBlastWorld.State(p)==saved.State&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana
                &&World.FindItem((Serial)saved.Helm)?.Parent==p,"Original saved capture/resources/gear changed");
            Require(JsonSerializer.Serialize(HexBane.Debuff(World.FindMobile((Serial)saved.Targets[0])))==saved.Bane
                &&JsonSerializer.Serialize(HexSiphon.Debuff(World.FindMobile((Serial)saved.Targets[1])))==saved.Siphon
                &&JsonSerializer.Serialize(HexPurge.Debuff(World.FindMobile((Serial)saved.Targets[2])))==saved.Purge,"Saved Hex duration/owner/charge state changed");
            Require(!EldritchBlastWorld.TickPrepared(p,saved.State.StartedUtc,true).Due&&WarlockCombat.Implemented(CombatAbility.EldritchBlast),"Reload replayed or lost approved admission");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Eldritch world:original capture/absolute clocks/seed/fractional wounds and mana/original helm/three Hex attachments persist without replay;inspection clock only");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
