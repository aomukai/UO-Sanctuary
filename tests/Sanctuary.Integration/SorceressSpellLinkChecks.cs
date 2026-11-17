// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class SorceressSpellLinkChecks
{
    private sealed record Route(string Key,string Element,int Rate=0,bool Freeze=false,bool Reaction=false);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static readonly Route[] Direct=[new("firebolt","fire"),new("infernoflame1","fire",82),new("chargedbolt","ltng"),new("lightningbolt","ltng"),new("icebolt","cold"),new("iceblast","cold",Freeze:true),new("nova","ltng"),new("frostnova","cold"),new("chillingarmorbolt","cold"),new("chainlightning","ltng"),new("thunderstorm1","ltng"),new("arcticblast1","cold",41),new("ringoffire","fire",Reaction:true),new("flamewave","fire",Reaction:true)];
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Sorceress);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        foreach(var ability in new[]{CombatAbility.Fireball,CombatAbility.GlacialSpike,CombatAbility.StaticField}){ClassSkillChecks.Train(caster,SkillMechanics.For(ability).TalentId,1);}
        var original=primary.Location;var received=receiver.Location;int cases=0;
        try
        {
            primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            foreach(var route in Direct)
            foreach(string mode in new[]{"normal","potion","receiver-resisted","primary-immune"})
            {
                Setup(mode,route.Element);var captured=new FirestormModifiers(0,10,20,30,40);var mods=captured with{Potion=CombatPotions.BonusAt(caster,"magical-penetration",TimedEffects.Now)};const int raw=4096;
                var basis=route.Element=="fire"?new GearDamage(0,raw/256.0,0,0,0):route.Element=="cold"?new(0,0,raw/256.0,0,0):new(0,0,0,raw/256.0,0);
                var parts=SkillCombat.MitigatePrepared(caster,primary,DruidMoltenBoulder.Potion(mods,basis),route.Rate);
                Check(route.Key+"/"+mode,parts,mode=="receiver-resisted",()=>SorceressSpellMissiles.ImpactPrepared(caster,primary,route.Key,route.Element,raw,captured,TimedEffects.Now,100,route.Freeze,route.Rate,route.Reaction?true:null));
            }
            foreach(var ability in new[]{CombatAbility.Fireball,CombatAbility.GlacialSpike})
            foreach(string mode in new[]{"normal","potion","receiver-resisted","primary-immune"})
            {
                string element=ability==CombatAbility.Fireball?"fire":"cold";Setup(mode,element);var flight=SorceressSpellMissiles.Prepare(caster,primary.Location,SkillMechanics.Values(caster,ability),TimedEffects.Now);int raw=SorceressSpellMissiles.Roll(flight,out ulong expectedSeed);
                var basis=element=="fire"?new GearDamage(0,raw/256.0,0,0,0):new(0,0,raw/256.0,0,0);var parts=SkillCombat.MitigatePrepared(caster,primary,DruidMoltenBoulder.Potion(flight.Modifiers with{Potion=CombatPotions.BonusAt(caster,"magical-penetration",flight.NextUtc)},basis));
                Check(ability+"/"+mode,parts,mode=="receiver-resisted",()=>{SorceressSpellMissiles.Explosion(caster,primary.Location,flight,out ulong actualSeed);Require(actualSeed==expectedSeed,"Copied explosion link RNG");});
            }
            foreach(string mode in new[]{"normal","receiver-resisted","primary-immune"})
            {
                Setup(mode,"ltng");receiver.SetResistance(ResistanceType.Energy,mode=="receiver-resisted"?75:0);var values=SkillMechanics.Values(caster,CombatAbility.StaticField);int life=SkillHealth.Balance256(primary),maximum=SkillHealth.Maximum256(primary),floor=StaticFields.FloorPercent(SummonDifficulties.For(caster.Map));
                int raw=StaticFields.Raw256(life,(int)values.Eval("calc1"),(int)values.Eval("calc2"));var mitigated=SkillCombat.MitigatePrepared(caster,primary,new(0,0,0,raw/256.0,0));int loss=StaticFields.Limit256(life,maximum,floor,NamedElements.To256(mitigated.Total));
                var parts=new GearDamage(0,0,0,loss/256.0,0);Check("Static/"+mode,parts,false,()=>{Require(StaticFields.Cast(caster,caster,values),"Static link cast");},mode=="receiver-resisted"?25:100);
            }
            Require(cases==67,"Incomplete Sorceress/shared spell link matrix");
        }
        finally
        {
            primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}TimedEffects.Clear(primary);caster.Delete();
        }
        void Setup(string mode,string element)
        {
            TimedEffects.Clear(caster);TimedEffects.Clear(primary);if(mode is"potion"or"receiver-resisted"){Require(CombatPotions.Apply(caster,3),"Spell link potion");}
            foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}
            if(mode=="primary-immune"){primary.SetResistance(element=="fire"?ResistanceType.Fire:element=="cold"?ResistanceType.Cold:ResistanceType.Energy,100);}
            if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
        }
        void Check(string label,GearDamage settled,bool resisted,Action impact,int lightningPercent=100)
        {
            var typed=DirectDamageParts.From(settled);var shared=typed.Fraction(percent);int expected=resisted?shared.Physical256+shared.Magic256+shared.Fire256/2+shared.Cold256/4:checked((int)(shared.Total-shared.Lightning256+shared.Lightning256*lightningPercent/100));
            int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),events=0,mana=NecromancerEffects.ManaBalance256(caster);SkillDamageContact observed=default;
            void Observe(SkillDamageContact contact){events++;observed=contact;}
            SkillDamageEvents.BeforeDirectDamage+=Observe;
            try
            {
                impact();Require(body.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==NamedElements.To256(settled.Total)&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(settled.Total>0?1:0)
                    &&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==typed)&&NecromancerEffects.ManaBalance256(caster)==mana,
                    $"Spell {label} typed link:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");
                if(settled.Poison>0){Require(typed.Total<NamedElements.To256(settled.Total),"Spell potion poison was copied into typed lanes");}cases++;
            }
            finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
        }
    }
}
