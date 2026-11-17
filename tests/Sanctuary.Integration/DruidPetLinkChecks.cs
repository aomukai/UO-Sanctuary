// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Collections;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class DruidPetLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static bool Credited(BaseCreature target,Mobile pet,PlayerMobile owner)
    {foreach(var entry in target.DamageEntries){if(entry.Damager==pet&&entry.Responsible?.Any(e=>e.Damager==owner&&e.DamageGiven>0)==true){return true;}}return false;}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody defiler,BaseCreature originalPrimary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Druid);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);
        caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        foreach(var ability in new[]{CombatAbility.SummonSpiritWolf,CombatAbility.SummonDireWolf,CombatAbility.SummonGrizzly})
        {ClassSkillChecks.Train(caster,SkillMechanics.For(ability).TalentId,1);}
        var savedDefiler=defiler.Prepared;var primary=new GraveyardSkeleton{CantWalk=true,Frozen=true,NoKillAwards=true};primary.SetHits(50000);primary.Hits=primary.HitsMax;
        primary.MoveToWorld(new(caster.X+2,caster.Y,caster.Z),caster.Map);primary.AIObject?.AITimer.Stop();
        defiler.RestorePrepared(savedDefiler with{Links=savedDefiler.Links with{Marks=savedDefiler.Links.Marks.Select(m=>m.Target==originalPrimary.Serial.Value?m with{Target=primary.Serial.Value}:m).ToArray()}});DefilerHealthLink.Tick(TimedEffects.Now);
        var received=receiver.Location;var shields=SourceBlocking.Snapshot();var knockbacks=SourceKnockbacks.Snapshot();var blockRoll=SourceBlocking.BlockRoll;int cases=0;
        try
        {
            primary.MoveToWorld(new(caster.X+2,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            foreach(string route in new[]{"spirit","dire","grizzly","smite"})
            {
                DateTime at=TimedEffects.Now.AddHours(2);var point=new Point3D(caster.X+1,caster.Y,caster.Z);
                SkeletonGuardian pet=route=="spirit"?DruidSpiritWolfWorld.Create(caster,point,at,1):route=="dire"?DruidDireWolfWorld.Create(caster,point,at,1):DruidGrizzlyWorld.Create(caster,point,at,1);
                try
                {
                    foreach(string mode in new[]{"normal","enchant","receiver-resisted","primary-resisted","immune","critical","miss","blocked"})
                    {
                        SourceKnockbacks.Clear(primary);primary.MoveToWorld(new(caster.X+2,caster.Y,caster.Z),caster.Map);
                        TimedEffects.Clear(primary);SorceressEffects.Profiles.Clear(pet);
                        foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}
                        bool enchanted=mode is"enchant"or"receiver-resisted"or"primary-resisted";
                        if(enchanted){SorceressEffects.Profiles.Store(pet,new(Enchantment:new(at,1,1024,1024,100,caster.Serial.Value)));}
                        if(mode=="immune"){primary.SetResistance(ResistanceType.Physical,100);primary.SetResistance(ResistanceType.Cold,100);primary.SetResistance(ResistanceType.Fire,100);}
                        if(mode=="primary-resisted"){primary.SetResistance(ResistanceType.Physical,50);primary.SetResistance(ResistanceType.Fire,25);primary.SetResistance(ResistanceType.Cold,50);}
                        if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Physical,25);receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
                        SourceBlocking.Restore(shields.Where(s=>s.Actor!=primary.Serial.Value).Append(new SourceShield(primary.Serial.Value,CreatureProfiles.Definition(primary).Reference,mode=="blocked"?1:0)).ToArray());
                        SourceBlocking.BlockRoll=_=>mode=="blocked"?0:99;
                        var wolf=pet as DruidSpiritWolfBody;var dire=pet as DruidDireWolfBody;var bear=pet as DruidGrizzlyBody;
                        var spiritState=wolf==null?null:wolf.Prepared.Controller with{AttackPacket=DruidBeastAttackStats.Capture(SkillMechanics.Values(caster,CombatAbility.SummonSpiritWolf),wolf.SummonDifficulty,4)};
                        var direState=dire==null?null:dire.Prepared.Controller with{AttackPacket=DruidBeastAttackStats.Capture(SkillMechanics.Values(caster,CombatAbility.SummonDireWolf),dire.SummonDifficulty,4)};
                        var bearState=bear==null?null:bear.Prepared.Controller with{AttackPacket=DruidBeastAttackStats.Capture(SkillMechanics.Values(caster,CombatAbility.SummonGrizzly),bear.SummonDifficulty,route=="smite"?14:4)};
                        int chance=wolf!=null?DruidSpiritWolfHit.Chance(wolf,spiritState,primary):dire!=null?DruidDireWolfHit.Chance(dire,direState,primary):DruidGrizzlyHit.Chance(bear,bearState,primary);
                        bool contact=mode is not("miss"or"blocked"),wantedHit=mode!="miss",critical=mode=="critical";ulong initial=0,expectedSeed=0;DirectDamageParts typed=default;int total=0;
                        BearSmitePrepared prepared=null;bool blockMode=false;
                        for(ulong candidate=1;candidate<10000;candidate++)
                        {
                            ulong current=candidate;bool hit=AssassinTrapAttack.Hit(chance,SourceRandom.Roll(ref current));if(hit!=wantedHit){continue;}
                            bool doubled=false;GearDamage raw=default;
                            if(contact&&wolf!=null)
                            {
                                var b=wolf.Prepared.Snapshot.Birth;
                                var p=DruidSpiritWolfHit.Packet(b.ColdMinimum*256,b.ColdMaximum*256,b.ColdFrames,enchanted?1024:0,enchanted?1024:0,0,0,0,NecromancerSummons.Source("spiritwolf","Crit"),n=>(int)SourceRandom.Limited(ref current,n),()=>SourceRandom.Roll(ref current));
                                doubled=p.Critical;raw=new(0,p.Fire256/256.0,p.Cold256/256.0,p.Lightning256/256.0,0);
                            }
                            else if(contact&&dire!=null)
                            {
                                var a=direState.AttackPacket.Value;
                                var p=DruidDireWolfHit.Packet(a.Minimum,a.Maximum,DruidDireWolfHit.DamagePercent(dire,direState),enchanted?1024:0,enchanted?1024:0,0,0,0,0,0,NecromancerSummons.Source("fenris","Crit"),n=>(int)SourceRandom.Limited(ref current,n),()=>SourceRandom.Roll(ref current));
                                doubled=p.Critical;raw=new(p.Physical256/256.0,p.Fire256/256.0,p.Cold256/256.0,p.Lightning256/256.0,0);
                            }
                            if(bear!=null)
                            {
                                current=candidate;prepared=DruidGrizzlyHit.Prepare(bear,caster,bearState,primary,route=="smite",1,ref current,out int actualChance,out blockMode);
                                Require(actualChance==chance&&prepared!=null,"Prepared Grizzly link packet");doubled=prepared.Critical;
                                if(contact){total=prepared.Total256;typed=new(checked(total-prepared.Fire256-prepared.Cold256-prepared.Lightning256),0,prepared.Fire256,prepared.Cold256,prepared.Lightning256);}
                            }
                            else if(contact){var settled=SkillCombat.MitigatePrepared(pet,primary,raw);total=NamedElements.To256(settled.Total);typed=DirectDamageParts.From(settled);}
                            if(contact&&doubled!=critical){continue;}initial=candidate;expectedSeed=current;break;
                        }
                        Require(initial>0,"No deterministic Druid pet hit seed");
                        int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0;SkillDamageContact observed=default;
                        var split=typed.Fraction(percent);int expected=mode=="receiver-resisted"?split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256/4:checked((int)split.Total);
                        void Observe(SkillDamageContact c){events++;observed=c;}
                        SkillDamageEvents.BeforeDirectDamage+=Observe;
                        try
                        {
                            ulong seed=initial;bool outcome;
                            if(wolf!=null){var result=DruidSpiritWolfHit.Resolve(wolf,caster,spiritState,primary,ref seed);outcome=result.Outcome==(contact?SpiritWolfHitOutcome.Hit:mode=="miss"?SpiritWolfHitOutcome.Miss:SpiritWolfHitOutcome.Blocked);}
                            else if(dire!=null){var result=DruidDireWolfHit.Resolve(dire,caster,direState,primary,ref seed);outcome=result.Outcome==(contact?DireWolfHitOutcome.Hit:mode=="miss"?DireWolfHitOutcome.Miss:DireWolfHitOutcome.Blocked);}
                            else
                            {
                                seed=expectedSeed;var result=DruidGrizzlyHit.Release(bear,caster,bearState,primary,prepared,chance,blockMode,route=="smite",ref seed);
                                outcome=result.Outcome==(contact?GrizzlyHitOutcome.Hit:mode=="miss"?GrizzlyHitOutcome.Miss:GrizzlyHitOutcome.Blocked);
                                if(contact&&route=="smite"){Require(prepared.Physical256-typed.Physical256==(int)DruidNpcSkills.BearSmite(1).Eval("calc2")*256,"BearSmite post-allocation physical field entered settled total");}
                            }
                            Require(outcome&&seed==expectedSeed&&defiler.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected
                                &&events==(contact&&total>0?1:0)&&(events==0||observed.Source==pet&&observed.Target==primary&&observed.Parts==typed)&&NecromancerEffects.ManaBalance256(caster)==mana,
                                $"Druid pet {route}/{mode} typed link:fault={defiler.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};outcome={outcome};seed={seed==expectedSeed}");
                            if(total>0){Require(pet.GetDamageMaster(primary)==caster&&Credited(primary,pet,caster),"Pet hit lost actual NPC source/owner credit");}cases++;
                        }
                        finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
                    }
                }
                finally{pet.Delete();}
            }
            Require(cases==32,"Incomplete Druid pet link matrix");
        }
        finally
        {
            defiler.RestorePrepared(savedDefiler);DefilerHealthLink.Tick(TimedEffects.Now);SourceKnockbacks.Restore(knockbacks);SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=blockRoll;receiver.MoveToWorld(received,owner.Map);
            foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}TimedEffects.Clear(primary);primary.Delete();caster.Delete();
        }
    }
}
