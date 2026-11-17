// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class ArmageddonLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Call(string method,params object[] args)=>typeof(DruidArmageddon).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
    internal static void Run(PlayerMobile owner,WarlockDefilerBody defiler,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Druid);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);ClassSkillChecks.Train(caster,"druid.armageddon",1);
        caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);var original=primary.Location;var received=receiver.Location;var pool=DruidArmageddon.Snapshot();int cases=0;
        try
        {
            primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            foreach(bool periodic in new[]{false,true})
            foreach(string mode in new[]{"normal","potion","receiver-resisted","immune"})
            {
                TimedEffects.Clear(primary);foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}
                if(mode=="immune"){primary.SetResistance(ResistanceType.Physical,100);primary.SetResistance(ResistanceType.Fire,100);}
                if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Physical,25);receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
                var mods=new FirestormModifiers(mode is"potion"or"receiver-resisted"?10:0,10,20,30,40);DateTime at=TimedEffects.Now.AddDays(1);
                long id=DruidArmageddon.Add(caster,periodic?623:577,1,primary.X*2,primary.Y*2,primary.Z,at);
                var actor=DruidArmageddon.Snapshot().Actors.Single(a=>a.Id==id)with{Modifiers=mods,Endpoints=periodic?new(0,0,4096,4096):new(2048,2048,2048,2048)};
                var raw=periodic?new GearDamage(0,16,0,0,0):new GearDamage(8,8,0,0,0);
                var settled=SkillCombat.MitigatePrepared(caster,primary,DruidMoltenBoulder.Potion(mods,raw),periodic?41:0);var typed=DirectDamageParts.From(settled);var share=typed.Fraction(percent);
                int expected=periodic?0:mode=="receiver-resisted"?share.Physical256*75/100+share.Magic256+share.Fire256/2+share.Cold256/4:checked((int)share.Total);
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0;SkillDamageContact observed=default;
                void Observe(SkillDamageContact c){events++;observed=c;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    if(periodic){Call("Advance",actor,true);}
                    else
                    {
                        long watermark=DruidArmageddon.Snapshot().NextId;Call("Terminal",caster,actor);
                        var child=DruidArmageddon.Snapshot().Actors.Single(a=>a.Id==watermark+1);var actualMods=DruidFirestorm.Modifiers(caster);
                        Require(child.Missile==623&&child.Rank==actor.Rank&&child.X==actor.X&&child.Y==actor.Y&&child.Modifiers==actualMods
                            &&child.Endpoints==DruidArmageddon.Endpoints(SkillMechanics.Values(caster,CombatAbility.Armageddon,actor.Rank),623,actualMods.FireMastery,AmazonSlowMissiles.Capture(caster,true)),"Armageddon terminal lost independent child capture");
                    }
                    Require(defiler.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==NamedElements.To256(settled.Total)&&secondary-SkillHealth.Balance256(receiver)==expected
                        &&events==(!periodic&&settled.Total>0?1:0)&&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==typed)&&NecromancerEffects.ManaBalance256(caster)==mana,
                        $"Armageddon {(periodic?"ground":"terminal")}/{mode} typed link:fault={defiler.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");
                    if(settled.Poison>0){Require(typed.Total<NamedElements.To256(settled.Total),"Armageddon potion poison entered shared channels");}cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Require(cases==8,"Incomplete Armageddon link matrix");
        }
        finally
        {
            DruidArmageddon.Restore(pool);primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}TimedEffects.Clear(primary);caster.Delete();
        }
    }
}
