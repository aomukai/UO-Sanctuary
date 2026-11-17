// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class WarCryLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody defiler,BaseCreature originalPrimary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Barbarian);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);ClassSkillChecks.Train(caster,"barbarian.warcry",1);
        caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        var savedDefiler=defiler.Prepared;var primary=new GraveyardSkeleton{CantWalk=true,Frozen=true,NoKillAwards=true};primary.SetHits(50000);primary.Hits=primary.HitsMax;
        primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);primary.AIObject?.AITimer.Stop();
        defiler.RestorePrepared(savedDefiler with{Links=savedDefiler.Links with{Marks=savedDefiler.Links.Marks.Select(m=>m.Target==originalPrimary.Serial.Value?m with{Target=primary.Serial.Value}:m).ToArray()}});DefilerHealthLink.Tick(TimedEffects.Now);
        var received=receiver.Location;var shields=SourceBlocking.Snapshot();var blockRoll=SourceBlocking.BlockRoll;var damageRoll=BarbarianWarCry.Roll;int cases=0;
        try
        {
            receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            foreach(bool warcry in new[]{true,false})
            foreach(string mode in new[]{"normal","primary-resisted","receiver-resisted","immune","blocked"})
            {
                CombatStuns.Clear(primary);foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}
                if(mode=="primary-resisted"){primary.SetResistance(ResistanceType.Physical,50);}
                if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Physical,25);}
                if(mode=="immune"){primary.SetResistance(ResistanceType.Physical,100);}
                SourceBlocking.Restore(shields.Where(s=>s.Actor!=primary.Serial.Value).Append(new SourceShield(primary.Serial.Value,CreatureProfiles.Definition(primary).Reference,mode=="blocked"?1:0)).ToArray());
                SourceBlocking.BlockRoll=_=>mode=="blocked"?0:99;int draws=0;
                int Roll(int bound){Require(bound==2,"Physical spell roll bound");draws++;return 1;}BarbarianWarCry.Roll=Roll;
                var settled=SkillCombat.MitigatePrepared(caster,primary,new(4097/256.0,0,0,0,0));int total=mode=="blocked"?0:NamedElements.To256(settled.Total);
                var typed=new DirectDamageParts(total,0,0,0,0);int expected=typed.Fraction(percent).Physical256;if(mode=="receiver-resisted"){expected=expected*75/100;}
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0;SkillDamageContact observed=default;bool stunnedBeforeHealth=false;
                void Observe(SkillDamageContact c){events++;observed=c;stunnedBeforeHealth=CombatStuns.Remaining(primary)>0;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    DateTime at=TimedEffects.Now;var ray=new ShoutRay(1,caster.Serial.Value,caster.Map.MapID,1,0,caster.X,caster.Y,caster.Z,0,2304,0,at.AddMilliseconds(640),at,CombatAbility.WarCry,caster.Serial.Value,null,4096,4098);
                    bool hit=warcry?BarbarianWarCry.Hit(caster,primary,ray):BarbarianWarCry.PhysicalHit(caster,primary,4096,4098,Roll);
                    Require(hit==(mode!="blocked")&&draws==1&&defiler.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected
                        &&events==(total>0?1:0)&&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==typed&&stunnedBeforeHealth==warcry)
                        &&(CombatStuns.Remaining(primary)>0)==(warcry&&mode!="blocked")&&NecromancerEffects.ManaBalance256(caster)==mana,
                        $"War Cry {(warcry?"stun":"landing-receiver")}/{mode} typed link:fault={defiler.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};stun={CombatStuns.Remaining(primary)>0};draws={draws}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Require(cases==10,"Incomplete physical spell link matrix");
        }
        finally
        {
            defiler.RestorePrepared(savedDefiler);DefilerHealthLink.Tick(TimedEffects.Now);SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=blockRoll;BarbarianWarCry.Roll=damageRoll;
            receiver.MoveToWorld(received,owner.Map);foreach(var type in Enum.GetValues<ResistanceType>()){receiver.SetResistance(type,0);}CombatStuns.Clear(primary);primary.Delete();caster.Delete();
        }
    }
}
