// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class MartialElementLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Assassin);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X-1,owner.Y,owner.Z),owner.Map);
        foreach(var ability in new[]{CombatAbility.BladesOfIce,CombatAbility.ClawsOfThunder,CombatAbility.PhoenixStrike}){DeathSentryExplosionChecks.Train(caster,ability,1);}int cases=0;
        try
        {
            Require(MartialLightning.AfterParent(caster,primary.Location,caster.Map,new(3,1,2560,2560,5120,2560,5120,0),TimedEffects.Now)>0,"Thunder link emission");
            foreach(int count in new[]{2,3}){Require(MartialPhoenix.AfterParent(caster,primary,primary.Location,caster.Map,new(caster.Serial.Value,CombatAbility.PhoenixStrike,count,1,count*25,TimedEffects.Now.AddHours(1)),TimedEffects.Now)>0,"Phoenix link emission");}
            var thunder=MartialLightning.Snapshot().Flights.Where(x=>x.Owner==caster.Serial.Value).ToArray();var phoenix=MartialPhoenix.Snapshot().Flights.Where(x=>x.Owner==caster.Serial.Value).ToArray();
            foreach(string route in new[]{"ice","thunder-nova","thunder-bolt","phoenix-lightning","phoenix-cold"})
            foreach(string mode in new[]{"normal","receiver-resisted","primary-immune"})
            {
                bool cold=route is"ice"or"phoenix-cold",immune=mode=="primary-immune",resisted=mode=="receiver-resisted";
                primary.SetResistance(ResistanceType.Cold,cold&&immune?100:0);primary.SetResistance(ResistanceType.Energy,!cold&&immune?100:0);receiver.SetResistance(ResistanceType.Cold,resisted?75:0);receiver.SetResistance(ResistanceType.Energy,resisted?50:0);
                ulong seed=SourceRandom.InitLow(31337),advanced=seed;var t=thunder.First(x=>x.Bolt==(route=="thunder-bolt"))with{Seed=seed};var p=phoenix.First(x=>x.Cold==cold)with{Seed=seed};
                int raw=route=="ice"?3840:route.StartsWith("thunder",StringComparison.Ordinal)?WeaponMissileDamage.RollValue(t.Min256,t.Max256,0,n=>(int)SourceRandom.Limited(ref advanced,n)):WeaponMissileDamage.RollValue(p.Minimum256,p.Maximum256,0,n=>(int)SourceRandom.Limited(ref advanced,n));
                var reduced=NamedElements.Resolve256(caster,primary,raw,cold?"cold":"ltng",enhance:false,pierceOverride:0);var parts=cold?new DirectDamageParts(0,0,0,reduced.Damage256,0):new(0,0,0,0,reduced.Damage256);var shared=parts.Fraction(percent);
                int expected=resisted?shared.Cold256/4+shared.Lightning256/2:checked((int)shared.Total),aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),displayed=primary.Hits,mana=NecromancerEffects.ManaBalance256(caster),events=0;var charges=AssassinCharges.Finisher(caster);SkillDamageContact observed=default;
                void Observe(SkillDamageContact contact){events++;observed=contact;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    int loss;
                    if(route=="ice"){loss=MartialCold.Impact(caster,primary,raw,100,100,0,Core.TickCount);}
                    else if(route.StartsWith("thunder",StringComparison.Ordinal)){var hit=MartialLightning.Impact(caster,primary,t);loss=hit.Loss;Require(hit.Seed==advanced&&!MartialLightning.Ready(t.Cast,primary.Serial.Value,t.NextUtc.AddMilliseconds(159))&&MartialLightning.Ready(t.Cast,primary.Serial.Value,t.NextUtc.AddMilliseconds(160)),"Thunder link RNG/lease");}
                    else{var hit=MartialPhoenix.Impact(caster,primary,p);loss=displayed-primary.Hits;Require(hit.Seed==advanced&&!MartialPhoenix.Ready(p.Cast,primary.Serial.Value,p.NextUtc.AddMilliseconds(159))&&MartialPhoenix.Ready(p.Cast,primary.Serial.Value,p.NextUtc.AddMilliseconds(160)),"Phoenix link RNG/lease");}
                    Require(body.Prepared.Links.Fault==null&&aLife-SkillHealth.Balance256(primary)==reduced.Damage256&&bLife-SkillHealth.Balance256(receiver)==expected&&loss==displayed-primary.Hits
                        &&events==(reduced.Damage256>0?1:0)&&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==parts)&&NecromancerEffects.ManaBalance256(caster)==mana&&AssassinCharges.Finisher(caster).SequenceEqual(charges),
                        $"Martial {route}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{reduced.Damage256};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Require(cases==15,"Incomplete Martial element/link matrix");
        }
        finally
        {
            var a=MartialLightning.Snapshot();var casts=a.Flights.Where(x=>x.Owner==caster.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialLightning.Restore(a with{Flights=a.Flights.Where(x=>x.Owner!=caster.Serial.Value).ToArray(),Leases=a.Leases.Where(x=>!casts.Contains(x.Cast)).ToArray()});
            var b=MartialPhoenix.Snapshot();casts=b.Flights.Where(x=>x.Owner==caster.Serial.Value).Select(x=>x.Cast).ToHashSet();MartialPhoenix.Restore(b with{Drops=b.Drops.Where(x=>x.Owner!=caster.Serial.Value).ToArray(),Flights=b.Flights.Where(x=>x.Owner!=caster.Serial.Value).ToArray(),Leases=b.Leases.Where(x=>!casts.Contains(x.Cast)).ToArray()});
            primary.SetResistance(ResistanceType.Cold,0);primary.SetResistance(ResistanceType.Energy,0);receiver.SetResistance(ResistanceType.Cold,0);receiver.SetResistance(ResistanceType.Energy,0);caster.Delete();
        }
    }
}
