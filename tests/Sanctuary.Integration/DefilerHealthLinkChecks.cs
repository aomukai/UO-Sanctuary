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

public static class DefilerHealthLinkChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerHealthLinkChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint[] Targets,string Text,int Life,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-health-link-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-health-link-reload-check":"defiler-health-link-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved Health Link groups/typed damage/lifecycle on actual native NPCs",_=>Core.LoopContext.Post(()=>
            {try{Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>{if(reload){Reload(n);}Scene(n,!reload,name+"-result");});}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Name="Defiler approved link fixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.summondefiler",20);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);FixtureSpace.Corridor(p,6560,30,3500);
        var account=FixtureNetwork.Authenticate(network,p,"defiler-link-");WarlockDefilerBody b=null;var targets=new GoatmanNpcContactChecks.Recipient[6];bool kept=false;
        try
        {
            DateTime at=TimedEffects.Now;b=WarlockDefilerBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at.AddHours(2));NpcEquipmentRandom.Set(b,SourceRandom.InitLow(405));
            Require(!b.SavedText.Contains("Links"),"Legacy Defiler JSON gained an empty link field");
            for(int i=0;i<targets.Length;i++)
            {
                var t=targets[i]=new(){NoKillAwards=true,CantWalk=true,Frozen=true};t.SetHits(50000);t.Hits=t.HitsMax;
                foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}t.MoveToWorld(new(b.X+4+i%3,b.Y+i/3,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            }
            Require(DefilerHealthLink.Cast(b,targets[0],at)&&b.Prepared.Links.Marks.Length==5&&b.Prepared.Links.Marks[0].Target==targets[0].Serial.Value,"Approved target-first own group/cap selection failed");
            var group=b.Prepared.Links;int pct=group.Marks[0].Percent;var linked=group.Marks.Select(m=>(GoatmanNpcContactChecks.Recipient)World.FindMobile((Serial)m.Target)).ToArray();
            Require(!DefilerHealthLink.Cast(b,targets[5],at.AddMilliseconds(1))&&b.Prepared.Links==group,"Full group refreshed/replaced old marks");
            var other=WarlockDefilerBody.CreatePrepared(p,new(p.X,p.Y+1,p.Z),at.AddHours(2));
            try
            {
                Require(DefilerHealthLink.Cast(other,targets[5],at)&&other.Prepared.Links.Marks.Length==1
                    &&other.Prepared.Links.Marks[0].Target==targets[5].Serial.Value&&b.Prepared.Links==group,
                    "Separate Defiler overwrote another group's marks");
            }
            finally{other.Delete();}
            var mixed=new DirectDamageParts(10000,20000,30000,40000,50000);var victim=linked[1];victim.SetResistance(ResistanceType.Physical,25);victim.SetResistance(ResistanceType.Fire,50);victim.SetResistance(ResistanceType.Energy,100);
            int[] before=linked.Select(SkillHealth.Balance256).ToArray();var split=mixed.Fraction(pct);
            SkillHealth.TypedDamage(p,linked[0],checked((int)mixed.Total),mixed);
            int expected=split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256;
            Require(before[0]-SkillHealth.Balance256(linked[0])==mixed.Total&&before[1]-SkillHealth.Balance256(victim)==expected
                &&before[2]-SkillHealth.Balance256(linked[2])==split.Total,"Typed share lost original hit,FP8/recipient defense or recursed");
            Require(MonsterUninterruptibles.State(victim)?.LastAttacker==b.Serial.Value&&PreparedMonsterCredit.Owner(victim,b)==p
                &&b.Prepared.Links.Fault==null,"Shared NPC source/owner credit or typed compatibility failed");
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillHealth.TypedDamage(p,linked[0],1000,new(1000,0,0,0,0),SkillDamageKind.Periodic);
            Require(SkillHealth.Balance256(victim)==before[1],"Periodic poison/other ticks initiated link sharing");
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillCombat.FireImpact(p,linked[0],25600);
            Require(before[0]-SkillHealth.Balance256(linked[0])==25600&&before[1]-SkillHealth.Balance256(victim)==(25600*pct/100)/2,"Actual elemental direct route did not publish settled typed damage");
            victim.SetResistance(ResistanceType.Physical,0);victim.SetResistance(ResistanceType.Fire,0);victim.SetResistance(ResistanceType.Energy,0);
            victim.MoveToWorld(new(b.X+20,b.Y,b.Z),b.Map);before=linked.Select(SkillHealth.Balance256).ToArray();SkillCombat.FireImpact(p,linked[0],25600);
            Require(before[1]-SkillHealth.Balance256(victim)==25600*pct/100&&DefilerHealthLink.Mark(victim,at)!=null,"Leaving application radius broke unexpired link");
            before=linked.Select(SkillHealth.Balance256).ToArray();network.Mobile=null;p.NetState=null;SkillHealth.TypedDamage(p,linked[0],1000,new(1000,0,0,0,0));
            Require(SkillHealth.Balance256(victim)==before[1]&&b.Prepared.Links==group,"Offline sharing occurred or mark duration/reset changed");network.Mobile=p;p.NetState=network;PersistentMinions.Resume(p);
            Require(b.Map==p.Map&&b.Prepared.Links==group,"Reconnect did not reactivate the unchanged link group");
            var state=b.Prepared;string text=b.SavedText;bool refused=false;
            try{b.RestorePrepared(state with{Links=group with{Owner=p.Serial.Value+1}});}catch(InvalidDataException){refused=true;}
            Require(refused&&b.SavedText==text,"Malformed link ownership partially restored");
            NecromancerSpells(p,b,linked[0],victim,pct);
            MageSpells(p,b,linked[0],victim,pct);
            SentryExplosions(p,b,linked[0],victim,pct);
            SentryMissiles(p,b,linked[0],victim,pct);
            AssassinSpellLinkChecks.Run(p,b,linked[0],victim,pct);
            MartialElementLinkChecks.Run(p,b,linked[0],victim,pct);
            BladeLinkChecks.Run(p,b,linked[0],victim,pct);
            SorceressSpellLinkChecks.Run(p,b,linked[0],victim,pct);
            DruidElementLinkChecks.Run(p,b,linked[0],victim,pct);
            DruidPetLinkChecks.Run(p,b,linked[0],victim,pct);
            ArmageddonLinkChecks.Run(p,b,linked[0],victim,pct);
            before=linked.Select(SkillHealth.Balance256).ToArray();SkillHealth.Damage(p,linked[0],257);
            Require(before[0]-SkillHealth.Balance256(linked[0])==257&&before[1]==SkillHealth.Balance256(victim)
                &&b.Prepared.Links.Fault=="Direct damage lacks settled typed components", $"Untyped damage mismatch:primary={before[0]-SkillHealth.Balance256(linked[0])},secondary={before[1]-SkillHealth.Balance256(victim)},fault={b.Prepared.Links.Fault},bodyMap={b.Map},ownerMap={p.Map}");
            b.RestorePrepared(state); // Fixture restores its exact pre-fault state for remaining independent cases.
            var doomed=linked[4];SkillHealth.SetBalance256(doomed,256);int directEvents=0;
            void Observe(SkillDamageContact c){directEvents++;}
            SkillDamageEvents.BeforeDirectDamage+=Observe;
            try
            {
                SkillCombat.FireImpact(p,linked[0],25600);
                Require(!doomed.Alive&&doomed.LastKiller==b&&PreparedMonsterCredit.Owner(doomed,b)==p&&directEvents==1,
                    "Shared lethal damage lost Defiler/owner credit or dispatched secondary direct/proc events");
            }
            finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            DefilerHealthLink.Tick(at.AddSeconds(21));Require(b.Prepared.Links==null&&DefilerHealthLink.Mark(victim,at.AddSeconds(21))==null,"Original mark expiry did not remove group");
            foreach(var t in targets){if(t!=null&&!t.Deleted){t.MoveToWorld(new(b.X+4+Array.IndexOf(targets,t)%3,b.Y+Array.IndexOf(targets,t)/3,b.Z),b.Map);}}
            Require(DefilerHealthLink.Cast(b,targets[0],at),"Held-clock saved group setup failed");
            b.RestorePrepared(b.Prepared with{Links=b.Prepared.Links with{Marks=b.Prepared.Links.Marks.Select(m=>m with{UntilUtc=at.AddHours(1)}).ToArray()}});
            if(retain)
            {
                // Explicit held expiry/recovery is for original-body load
                // inspection;the separate21second check establishes expiry.
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);
                b.RestorePrepared(b.Prepared with{NextRecoveryUtc=at.AddHours(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,targets.Select(t=>t.Serial.Value).ToArray(),b.SavedText,SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))));kept=true;
            }
            Require(WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Learned Defiler route missing after source host integration");
            Log.Information("Defiler links:own target/cap/expiry,no refresh,typed FP8 sharing/recipient defenses/no recursion,NPC owner credit,actual fire route,range retention,offline suppression and atomic malformed ownership pass");Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){foreach(var t in targets){t?.Delete();}b?.Delete();p.Delete();}
        }
    }
    private static void NecromancerSpells(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Necromancer);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);
        foreach(var ability in new[]{CombatAbility.Teeth,CombatAbility.BoneSpear,CombatAbility.BoneSpirit}){ClassSkillChecks.Train(caster,SkillMechanics.For(ability).TalentId);}
        int cases=0;
        try
        {
            foreach(string mode in new[]{"normal","potion","receiver-resisted"})
            {
                if(mode=="potion"){Require(CombatPotions.Apply(caster,3),"Spell link potion setup");}
                foreach(var ability in new[]{CombatAbility.Teeth,CombatAbility.BoneSpear,CombatAbility.BoneSpirit})
                {
                    if(mode=="primary-magic-immune"&&ability!=CombatAbility.BoneSpear){continue;}
                    bool resisted=mode=="receiver-resisted";receiver.SetResistance(ResistanceType.Physical,0);receiver.SetResistance(ResistanceType.Fire,resisted?50:0);receiver.SetResistance(ResistanceType.Cold,resisted?75:0);receiver.SetResistance(ResistanceType.Energy,resisted?100:0);
                    ClassCombat.Stop(caster);var action=ClassCombat.For(caster);action.NextAction=action.RecoveryUntil=Core.TickCount;caster.Frozen=caster.Paralyzed=false;NecromancerEffects.SetMana256(caster,caster.ManaMax*256);var values=SkillMechanics.Values(caster,ability);int mana=NecromancerEffects.ManaBalance256(caster);
                    Require(ClassCombat.Perform(caster,ability,primary,primary.Location,Core.TickCount),"Spell link learned start");ClassCombat.ReleaseCast(caster);Require(ClassCombat.ResolveAction(caster,ClassCombat.For(caster).EffectAt)&&NecromancerEffects.ManaBalance256(caster)==mana-values.Mana256,"Spell link learned release/payment");
                    var flight=NecromancerSpellMissiles.Snapshot().Flights.First(f=>f.Owner==caster.Serial.Value);ulong expectedSeed=flight.Seed;int raw=WeaponMissileDamage.RollValue(flight.Minimum256,flight.Maximum256,0,n=>(int)SourceRandom.Limited(ref expectedSeed,n));
                    var parts=SkillCombat.MitigatePrepared(caster,primary,NecromancerSpellMissiles.Packet(flight,raw,CombatPotions.BonusAt(caster,"magical-penetration",flight.NextUtc)));var shared=DirectDamageParts.From(parts).Fraction(percent);
                    int expected=resisted?shared.Magic256+shared.Fire256/2+shared.Cold256/4:checked((int)shared.Total),aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),events=0;SkillDamageContact observed=default;
                    void Observe(SkillDamageContact contact){events++;observed=contact;}
                    SkillDamageEvents.BeforeDirectDamage+=Observe;
                    try
                    {
                        ulong actualSeed=NecromancerSpellMissiles.Impact(caster,primary,flight);
                        Require(body.Prepared.Links.Fault==null&&aLife-SkillHealth.Balance256(primary)==NamedElements.To256(parts.Total)&&bLife-SkillHealth.Balance256(receiver)==expected&&events==1&&observed.Source==caster&&observed.Target==primary&&observed.Parts==DirectDamageParts.From(parts)&&actualSeed==expectedSeed,
                            $"Necromancer {ability}/{mode} typed link: fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{NamedElements.To256(parts.Total)};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");
                        if(mode!="normal"){Require(parts.Poison>0&&shared.Total<NamedElements.To256(parts.Total),"Potion poison was lost or included in shared non-poison channels");}cases++;
                    }
                    finally{SkillDamageEvents.BeforeDirectDamage-=Observe;NecromancerSpellMissiles.Cancel(caster);}
                }
            }
            Require(cases==9,"Incomplete magic missile/link matrix");
        }
        finally
        {
            receiver.SetResistance(ResistanceType.Physical,0);receiver.SetResistance(ResistanceType.Fire,0);receiver.SetResistance(ResistanceType.Cold,0);receiver.SetResistance(ResistanceType.Energy,0);NecromancerSpellMissiles.Cancel(caster);ClassCombat.Stop(caster);caster.Delete();
        }
    }
    private static void MageSpells(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Necromancer);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);
        MagePetBuffChecks.Train(caster,"necromancer.skeletalmage",10);MagePetBuffChecks.Train(caster,"necromancer.skeletonmastery",5);int cases=0;
        try
        {
            foreach(int element in new[]{1,2,3})
            {
                var mage=MagePetBuffChecks.Pet(caster,element);
                try
                {
                    foreach(string route in element==1?new[]{"basic","legacy","glacial"}:new[]{"basic","legacy"})
                    foreach(bool critical in new[]{false,true})
                    {
                        receiver.SetResistance(ResistanceType.Fire,critical?50:0);receiver.SetResistance(ResistanceType.Cold,critical?75:0);receiver.SetResistance(ResistanceType.Energy,critical?100:0);
                        long id=route=="basic"?NecromancerMageMissiles.Launch(mage,primary,TimedEffects.Now):route=="legacy"?NecromancerMageMissiles.LaunchLegacy(mage,primary,TimedEffects.Now):NecromancerMageMissiles.LaunchGlacial(mage,primary,TimedEffects.Now);
                        Require(id>0,"Mage link real flight allocation");var flight=NecromancerMageMissiles.Snapshot().Bolts.Single(x=>x.Id==id);
                        ulong chosen=1,nextCritical=0;for(;chosen<10000;chosen++){nextCritical=chosen;if((SourceRandom.Roll(ref nextCritical)%100<5)==critical){break;}}
                        Require(chosen<10000,"Mage link critical witness");NecromancerMageMissiles.SetActorSeed(mage,chosen);ulong damageSeed=flight.Seed;
                        int raw=WeaponMissileDamage.RollValue(flight.Packet.Minimum256,flight.Packet.Maximum256,0,n=>(int)SourceRandom.Limited(ref damageSeed,n));if(critical){raw=checked(raw*2);}
                        var input=flight.Packet.Element switch{"fire"=>new GearDamage(0,raw/256.0,0,0,0),"cold"=>new(0,0,raw/256.0,0,0),"ltng"=>new(0,0,0,raw/256.0,0),_=>throw new InvalidOperationException("Mage link element")};
                        var settled=SkillCombat.MitigatePrepared(mage,primary,input);var share=DirectDamageParts.From(settled).Fraction(percent);
                        int expected=critical?share.Fire256/2+share.Cold256/4:checked((int)share.Total),aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),events=0,ownerMana=NecromancerEffects.ManaBalance256(caster);SkillDamageContact observed=default;
                        void Observe(SkillDamageContact contact){events++;observed=contact;}
                        SkillDamageEvents.BeforeDirectDamage+=Observe;
                        try
                        {
                            int loss=NecromancerMageMissiles.Impact(mage,primary,flight);
                            Require(body.Prepared.Links.Fault==null&&loss==NamedElements.To256(settled.Total)&&aLife-SkillHealth.Balance256(primary)==loss&&bLife-SkillHealth.Balance256(receiver)==expected
                                &&events==1&&observed.Source==mage&&observed.Target==primary&&observed.Parts==DirectDamageParts.From(settled)&&NecromancerMageMissiles.ActorSeed(mage).Seed==nextCritical&&NecromancerEffects.ManaBalance256(caster)==ownerMana,
                                $"Mage {element}/{route}/critical={critical} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");cases++;
                        }
                        finally
                        {
                            SkillDamageEvents.BeforeDirectDamage-=Observe;var all=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(all with{Bolts=all.Bolts.Where(x=>x.Actor!=mage.Serial.Value).ToArray()});
                        }
                    }
                }
                finally{mage.Delete();}
            }
            Require(cases==14,"Incomplete Mage/link matrix");
        }
        finally
        {
            receiver.SetResistance(ResistanceType.Fire,0);receiver.SetResistance(ResistanceType.Cold,0);receiver.SetResistance(ResistanceType.Energy,0);caster.Delete();
        }
    }
    private static void SentryExplosions(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Assassin);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X+12,owner.Y+2,owner.Z),owner.Map);
        DeathSentryExplosionChecks.Train(caster,CombatAbility.DeathSentry,6);var original=primary.Location;var received=receiver.Location;
        var actor=AssassinSentryBody.CreatePrepared(caster,new(caster.X+1,caster.Y,caster.Z),TimedEffects.Now.AddHours(1),SourceRandom.InitLow(23),CombatAbility.DeathSentry);int cases=0;
        try
        {
            foreach(string mode in new[]{"mixed","primary-physical-immune","primary-fire-immune","receiver-resisted","outer-fire"})
            {
                var corpse=DeathSentryExplosionChecks.Body(caster,new(caster.X+3,caster.Y,caster.Z));
                try
                {
                    primary.MoveToWorld(new(corpse.X+(mode=="outer-fire"?4:1),corpse.Y,corpse.Z),corpse.Map);receiver.MoveToWorld(new(caster.X+15,caster.Y,caster.Z),caster.Map);
                    primary.SetResistance(ResistanceType.Physical,mode=="primary-physical-immune"?100:0);primary.SetResistance(ResistanceType.Fire,mode=="primary-fire-immune"?100:0);
                    bool resisted=mode=="receiver-resisted";receiver.SetResistance(ResistanceType.Physical,resisted?25:0);receiver.SetResistance(ResistanceType.Fire,resisted?50:0);
                    Require(actor.TrapTarget(caster,primary),$"Sentry link eligibility:visible={actor.CanSee(primary)},enemy={primary.IsEnemy(caster)},harm={actor.CanBeHarmful(primary,false,true)},conversion={PaladinConversion.Active(primary)},fight={primary.FightMode},master={primary.GetMaster()},blessed={primary.Blessed},town={Server.Spells.SpellHelper.IsTown(primary.Location,primary)}");
                    var before=CorpseSkillRules.Profiles.Find(corpse);var c=actor.Prepared.Creation;var values=SentryNpcSkills.Values(312,c.Rank,c.Level,c.Skill);ulong expectedSeed=before.BaseLife.Seed.Value;
                    var packet=CorpseExplosionPackets.Create(before.BaseLife.Base256,c.Level,before.BaseLife.Level,(int)values.Eval("aurarangecalc"),(int)values.Eval("calc1"),(int)values.Eval("calc2"),(int)values.Eval("calc3"),n=>(int)SourceRandom.Limited(ref expectedSeed,n));
                    var hit=packet.Recipient(mode=="outer-fire"?64:4);int physical=NamedElements.Resolve256(actor,primary,hit.Physical256,"physical",enhance:false,pierceOverride:0).Damage256;
                    int fire=NamedElements.Resolve256(actor,primary,hit.Fire256,"fire",enhance:false,pierceOverride:c.Passive("passive_fire_pierce")).Damage256;
                    var parts=new DirectDamageParts(physical,0,fire,0,0);var shared=parts.Fraction(percent);int expected=resisted?shared.Physical256*75/100+shared.Fire256/2:checked((int)shared.Total);
                    int aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),displayed=primary.Hits,events=0,mana=NecromancerEffects.ManaBalance256(caster);SkillDamageContact observed=default;
                    void Observe(SkillDamageContact contact){events++;observed=contact;}
                    SkillDamageEvents.BeforeDirectDamage+=Observe;
                    try
                    {
                        var result=AssassinDeathSentryCorpses.Explode(actor,corpse);
                        Require(body.Prepared.Links.Fault==null&&result.Status==DeathExplosionStatus.Success&&result.Targets==1&&result.Packet==packet&&result.AdvancedSeed==expectedSeed&&result.WholeLoss==displayed-primary.Hits
                            &&aLife-SkillHealth.Balance256(primary)==parts.Total&&bLife-SkillHealth.Balance256(receiver)==expected&&events==1&&observed.Source==actor&&observed.Target==primary&&observed.Parts==parts&&NecromancerEffects.ManaBalance256(caster)==mana,
                            $"Death Sentry/{mode} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{parts.Total};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};targets={result.Targets}");
                        Require(corpse.SanctuaryConsumed&&corpse.GetAmount(typeof(Gold))==1&&CorpseSkillRules.Profiles.Find(corpse).BaseLife.Seed==expectedSeed
                            &&AssassinDeathSentryCorpses.Explode(actor,corpse).Status==DeathExplosionStatus.NotEligible&&bLife-SkillHealth.Balance256(receiver)==expected,"Linked explosion lost atomic corpse/seed/loot or fired twice");cases++;
                    }
                    finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
                }
                finally{corpse.Delete();}
            }
            Require(cases==5,"Incomplete Death Sentry/link matrix");
        }
        finally
        {
            primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);primary.SetResistance(ResistanceType.Physical,0);primary.SetResistance(ResistanceType.Fire,0);receiver.SetResistance(ResistanceType.Physical,0);receiver.SetResistance(ResistanceType.Fire,0);actor.Delete();caster.Delete();
        }
    }
    private static void SentryMissiles(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Assassin);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);int cases=0;
        var routes=new[]{(CombatAbility.ChargedBoltSentry,"sentrychargedbolt","ltng"),(CombatAbility.LightningSentry,"sentrylightningbolt","ltng"),(CombatAbility.DeathSentry,"sentrylightningbolt2","ltng"),(CombatAbility.WakeOfInferno,"inferno sentry 1","fire"),(CombatAbility.WakeOfFire,"wake of destruction","fire")};
        try
        {
            foreach(var (ability,missile,element) in routes)
            {
                DeathSentryExplosionChecks.Train(caster,ability,6);var actor=AssassinSentryBody.CreatePrepared(caster,new(caster.X+2,caster.Y,caster.Z),TimedEffects.Now.AddHours(1),SourceRandom.InitLow(23),ability);
                try
                {
                    var captured=actor.Prepared;var endpoints=element=="fire"?AssassinSentryCreation.Fire(captured.Creation):AssassinSentryCreation.Lightning(captured.Creation);int rate=NecromancerSummons.Source(missile,"DamageRate"),pierce=captured.Creation.Passive(element=="fire"?"passive_fire_pierce":"passive_ltng_pierce");
                    foreach(string mode in new[]{"normal","receiver-resisted","primary-immune"})
                    {
                        primary.SetResistance(ResistanceType.Fire,mode=="primary-immune"&&element=="fire"?100:0);primary.SetResistance(ResistanceType.Energy,mode=="primary-immune"&&element=="ltng"?100:0);
                        bool resisted=mode=="receiver-resisted";receiver.SetResistance(ResistanceType.Fire,resisted?50:0);receiver.SetResistance(ResistanceType.Energy,resisted?75:0);
                        ulong seed=SourceRandom.InitLow(31337),advanced=seed;int raw=WeaponMissileDamage.RollValue(endpoints.Min,endpoints.Max,0,n=>(int)SourceRandom.Limited(ref advanced,n));
                        var settled=NamedElements.Resolve256(actor,primary,raw,element,enhance:false,damageRate:rate,pierceOverride:pierce);
                        var parts=element=="fire"?new DirectDamageParts(0,0,settled.Damage256,0,0):new(0,0,0,0,settled.Damage256);var shared=parts.Fraction(percent);
                        int expected=resisted?shared.Fire256/2+shared.Lightning256/4:checked((int)shared.Total),aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),displayed=primary.Hits,events=0,mana=NecromancerEffects.ManaBalance256(caster),npcMana=actor.Mana;SkillDamageContact observed=default;
                        void Observe(SkillDamageContact contact){events++;observed=contact;}
                        SkillDamageEvents.BeforeDirectDamage+=Observe;
                        try
                        {
                            var hit=AssassinSentryBolts.Impact(actor,primary,missile,endpoints.Min,endpoints.Max,pierce,seed,element,rate);
                            Require(body.Prepared.Links.Fault==null&&aLife-SkillHealth.Balance256(primary)==settled.Damage256&&bLife-SkillHealth.Balance256(receiver)==expected&&hit.WholeLoss==displayed-primary.Hits&&hit.Seed==advanced
                                &&events==(settled.Damage256>0?1:0)&&(events==0||observed.Source==actor&&observed.Target==primary&&observed.Parts==parts)&&actor.Prepared==captured&&NecromancerEffects.ManaBalance256(caster)==mana&&actor.Mana==npcMana,
                                $"Sentry {ability}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{settled.Damage256};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");cases++;
                        }
                        finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
                    }
                }
                finally{actor.Delete();}
            }
            Require(cases==15,"Incomplete sentry missile/link matrix");
        }
        finally
        {
            primary.SetResistance(ResistanceType.Fire,0);primary.SetResistance(ResistanceType.Energy,0);receiver.SetResistance(ResistanceType.Fire,0);receiver.SetResistance(ResistanceType.Energy,0);caster.Delete();
        }
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockDefilerBody;
        var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.Map==Map.Internal&&b.SavedText==s.Text&&SkillHealth.Balance256(b)==s.Life
            &&NpcEquipmentRandom.State(b)==s.Random&&b.Prepared.Links.Marks.All(m=>m.UntilUtc>TimedEffects.Now),$"Original held link mismatch:body={b!=null},owner={b?.ControlMaster?.Serial.Value},map={b?.Map},text={b?.SavedText==s.Text},life={b?.Hits},rng={NpcEquipmentRandom.State(b)==s.Random},marks={b?.Prepared.Links?.Marks.Length}");
        var account=FixtureNetwork.Authenticate(network,p,"defiler-link-reload-");
        try
        {
            PersistentMinions.Resume(p);
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==s.Life,"Original link body did not resume without healing");
            var marks=b.Prepared.Links.Marks;var first=(BaseCreature)World.FindMobile((Serial)marks[0].Target);
            var next=(BaseCreature)World.FindMobile((Serial)marks[1].Target);int life=SkillHealth.Balance256(next);
            SkillCombat.FireImpact(p,first,25600);
            Require(life-SkillHealth.Balance256(next)==25600*marks[0].Percent/100&&b.Prepared.Links.Marks.SequenceEqual(marks)
                &&SkillHealth.Balance256(b)==s.Life,"Original saved link group failed actual damage after login or reset its deadline");
            PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Text=b.SavedText,Random=NpcEquipmentRandom.State(b)}));
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
