// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

internal static class AssassinSpellLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Expire(BaseCreature target)
    {PaladinConversion.Restore(PaladinConversion.Snapshot().Select(x=>x.Target==target.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray());PaladinConversion.Prune();}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Assassin);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X+10,owner.Y+2,owner.Z),owner.Map);
        var abilities=new[]{CombatAbility.FireBlast,CombatAbility.ShockWeb,CombatAbility.PsychicHammer,CombatAbility.MindBlast};foreach(var ability in abilities){DeathSentryExplosionChecks.Train(caster,ability,1);}
        var origin=primary.Location;var received=receiver.Location;var fire=FireBlasts.Roll;var shock=ShockWebs.Roll;var hammer=PsychicHammers.Roll;var mind=MindBlasts.Roll;int cases=0;
        FireBlasts.Roll=ShockWebs.Roll=PsychicHammers.Roll=_=>0;
        try
        {
            primary.MoveToWorld(new(caster.X+6,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+18,caster.Y,caster.Z),caster.Map);
            foreach(var ability in abilities)
            foreach(string mode in ability==CombatAbility.MindBlast?new[]{"normal","potion","receiver-resisted","convert","convert-potion"}:new[]{"normal","potion","receiver-resisted"})
            {
                TimedEffects.Clear(caster);bool potion=mode is "potion"or"receiver-resisted"or"convert-potion",convert=mode.StartsWith("convert",StringComparison.Ordinal);if(potion){Require(CombatPotions.Apply(caster,3),"Assassin link potion");}
                MindBlasts.Roll=n=>n==100?(convert?0:99):0;NecromancerEffects.SetMana256(caster,caster.ManaMax*256);var values=SkillMechanics.Values(caster,ability);int bonus=CombatPotions.Bonus(caster,"magical-penetration");bool resisted=mode=="receiver-resisted";
                receiver.SetResistance(ResistanceType.Physical,resisted?25:0);receiver.SetResistance(ResistanceType.Fire,resisted?50:0);receiver.SetResistance(ResistanceType.Cold,resisted?75:0);receiver.SetResistance(ResistanceType.Energy,resisted?100:0);
                var bomb=new FireBomb(1,caster.Serial.Value,caster.Map.MapID,values.Rank,primary.X,primary.Y,primary.Z,0,0,1,true,TimedEffects.Now,values.Damage256(false),values.Damage256(true),bonus);
                var pool=ShockWebs.Snapshot();var shard=new ShockShard(pool.NextId+1,pool.NextCast+1,caster.Serial.Value,caster.Map.MapID,values.Rank,primary.X,primary.Y,primary.Z,0,0,2,true,TimedEffects.Now,values.Damage256(false),values.Damage256(true),bonus);
                GearDamage raw=ability switch{CombatAbility.FireBlast=>FireBlasts.Packet(bomb,bomb.Min256),CombatAbility.ShockWeb=>ShockWebs.Packet(shard,shard.Min256),CombatAbility.PsychicHammer=>PsychicHammers.Potion(caster,PsychicHammers.Damage(values,0,_=>0)),_=>PsychicHammers.Potion(caster,MindBlasts.Damage(values,_=>0))};
                var settled=SkillCombat.MitigatePrepared(caster,primary,raw);var parts=DirectDamageParts.From(settled);var shared=parts.Fraction(percent);int expected=resisted?shared.Physical256*75/100+shared.Magic256+shared.Fire256/2+shared.Cold256/4:checked((int)shared.Total);
                int aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0;SkillDamageContact observed=default;
                void Observe(SkillDamageContact contact){events++;observed=contact;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    if(ability==CombatAbility.FireBlast){FireBlasts.Impact(caster,bomb);}
                    else if(ability==CombatAbility.ShockWeb){ShockWebs.Restore(pool with{Shards=pool.Shards.Append(shard).ToArray(),NextId=shard.Id,NextCast=shard.Cast});Require(ShockWebs.Recipient(caster,shard)==primary,"Shock link recipient");ShockWebs.Tick(shard.NextUtc);Require(!ShockWebs.Ready(shard.Cast,primary.Serial.Value,shard.NextUtc),"Shock link hit-delay lease");}
                    else if(ability==CombatAbility.PsychicHammer){Require(PsychicHammers.Release(caster,primary.Serial),"Hammer link release");}
                    else{Require(MindBlasts.Impact(caster,primary,values,MindBlasts.Damage(values,_=>0)),"Mind Blast link impact");}
                    Require(body.Prepared.Links.Fault==null&&aLife-SkillHealth.Balance256(primary)==NamedElements.To256(settled.Total)&&bLife-SkillHealth.Balance256(receiver)==expected&&events==1&&observed.Source==caster&&observed.Target==primary&&observed.Parts==parts
                        &&NecromancerEffects.ManaBalance256(caster)==mana-(ability==CombatAbility.PsychicHammer?values.Mana256:0),
                        $"Assassin {ability}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");
                    if(potion){Require(settled.Poison>0&&parts.Total<NamedElements.To256(settled.Total),"Assassin potion poison share exclusion");}
                    if(convert){Require(PaladinConversion.Active(primary)&&CombatStuns.Remaining(primary)==0&&SourceKnockbacks.Remaining(primary)==0,"Linked successful Mind Blast conversion gained stun/knockback");}cases++;
                }
                finally
                {
                    SkillDamageEvents.BeforeDirectDamage-=Observe;Expire(primary);SourceKnockbacks.Clear(primary);CombatStuns.Clear(primary);primary.AIObject?.AITimer.Stop();primary.Combatant=null;
                    var current=ShockWebs.Snapshot();ShockWebs.Restore(current with{Shards=current.Shards.Where(x=>x.Owner!=caster.Serial.Value).ToArray(),Leases=current.Leases.Where(x=>x.Cast!=shard.Cast).ToArray()});
                }
            }
            Require(cases==14,"Incomplete Assassin spell/link matrix");
        }
        finally
        {
            FireBlasts.Roll=fire;ShockWebs.Roll=shock;PsychicHammers.Roll=hammer;MindBlasts.Roll=mind;Expire(primary);SourceKnockbacks.Clear(primary);CombatStuns.Clear(primary);primary.MoveToWorld(origin,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var type in Enum.GetValues<ResistanceType>()){receiver.SetResistance(type,0);}caster.Delete();
        }
    }
}
