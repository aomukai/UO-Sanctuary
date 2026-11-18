// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class DruidElementLinkChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static object Call(Type type,string method,params object[] arguments)=>type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,arguments);
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Druid);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);
        caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        foreach(var ability in new[]{CombatAbility.Twister,CombatAbility.Tornado,CombatAbility.MoltenBoulder,CombatAbility.Volcano,CombatAbility.ShockWave,CombatAbility.Fissure})
        {ClassSkillChecks.Train(caster,SkillMechanics.For(ability).TalentId,1);}
        var twisters=DruidTwister.Snapshot();var tornadoes=DruidTornado.Snapshot();var boulders=DruidMoltenBoulder.Snapshot();var volcanoes=DruidVolcano.Snapshot();var fissures=DruidFissure.Snapshot();var waves=DruidShockWave.Snapshot();
        var twisterGuard=DruidTwister.Uninterruptible;var tornadoGuard=DruidTornado.Uninterruptible;var volcanoGuard=DruidVolcano.Uninterruptible;var boulderGuard=DruidMoltenBoulder.Uninterruptible;
        var origin=primary.Location;var received=receiver.Location;int cases=0;
        try
        {
            primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            DruidTwister.Uninterruptible=_=>true;DruidTornado.Uninterruptible=_=>true;DruidVolcano.Uninterruptible=_=>true;DruidMoltenBoulder.Uninterruptible=_=>true;
            foreach(string route in new[]{"twister","tornado","tornado-area","fissure","volcano","shockwave","boulder","boulder-area","boulder-ground"})
            foreach(string mode in new[]{"normal","potion","receiver-resisted","primary-immune"})
            {
                TimedEffects.Clear(primary);foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}
                bool fire=route=="fissure"||route=="boulder-ground",mixed=route is"volcano"or"boulder"or"boulder-area",periodic=route=="boulder-ground";
                if(mode=="primary-immune")
                {primary.SetResistance(ResistanceType.Physical,100);primary.SetResistance(ResistanceType.Fire,100);}
                if(mode=="receiver-resisted")
                {receiver.SetResistance(ResistanceType.Physical,25);receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
                var mods=new FirestormModifiers(mode is"potion"or"receiver-resisted"?10:0,10,20,30,40);
                var raw=fire?new GearDamage(0,16,0,0,0):mixed?new GearDamage(8,8,0,0,0):new GearDamage(16,0,0,0,0);
                var settled=SkillCombat.MitigatePrepared(caster,primary,DruidMoltenBoulder.Potion(mods,raw),periodic?41:0);
                var typed=DirectDamageParts.From(settled);var shared=typed.Fraction(percent);
                int expected=periodic?0:mode=="receiver-resisted"?shared.Physical256*75/100+shared.Magic256+shared.Fire256/2+shared.Cold256/4:checked((int)shared.Total);
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),events=0,mana=NecromancerEffects.ManaBalance256(caster);SkillDamageContact observed=default;
                void Observe(SkillDamageContact c){events++;observed=c;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    DateTime at=TimedEffects.Now.AddDays(1);var aim=new Point3D(primary.X+4,primary.Y,primary.Z);ulong seed=SourceRandom.InitLow(401);ulong expectedSeed=seed;
                    int Roll(int minimum,int maximum)=>WeaponMissileDamage.RollValue(minimum,maximum,0,n=>(int)SourceRandom.Limited(ref expectedSeed,n));
                    if(route=="twister")
                    {
                        long id=DruidTwister.Create(caster,aim,SkillMechanics.Values(caster,CombatAbility.Twister),at)[0];var m=DruidTwister.Snapshot().Missiles.Single(x=>x.Id==id)with{Min256=4096,Max256=4096,Modifiers=mods,Seed=seed};
                        Roll(4096,4096);var result=(TwisterMissile)Call(typeof(DruidTwister),"Impact",caster,primary,m);Require(result.Seed==expectedSeed,"Twister link changed source seed");
                    }
                    else if(route is"tornado"or"tornado-area")
                    {
                        long id=DruidTornado.Create(caster,aim,SkillMechanics.Values(caster,CombatAbility.Tornado),at)[0];var m=DruidTornado.Snapshot().Missiles.Single(x=>x.Id==id)with{Min256=4096,Max256=4096,Modifiers=mods,Seed=seed,X=primary.X,Y=primary.Y};
                        Roll(4096,4096);var result=(TornadoMissile)(route=="tornado"?Call(typeof(DruidTornado),"Impact",caster,primary,m):Call(typeof(DruidTornado),"Pulse",caster,m,1));Require(result.Seed==expectedSeed,"Tornado link changed source seed");
                    }
                    else if(route=="volcano")
                    {
                        long id=DruidVolcano.Create(caster,aim,1,at);var m=DruidVolcano.Snapshot().Actors.Single(x=>x.Id==id)with{Missile=483,PhysicalMin=2048,PhysicalMax=2048,FireMin=2048,FireMax=2048,Modifiers=mods,Seed=seed};
                        Roll(2048,2048);Roll(2048,2048);var result=(VolcanoActor)Call(typeof(DruidVolcano),"Impact",caster,primary,m);Require(result.Seed==expectedSeed,"Volcano link changed source seed");
                    }
                    else if(route=="shockwave")
                    {
                        long id=DruidShockWave.Create(caster,aim,SkillMechanics.Values(caster,CombatAbility.ShockWave),at)[0];var m=DruidShockWave.Snapshot().Missiles.Single(x=>x.Id==id)with{Min256=4096,Max256=4096,Modifiers=mods,Seed=seed};
                        Roll(4096,4096);var result=DruidShockWave.Impact(caster,primary,m);Require(result.Seed==expectedSeed&&result.Fault==null,"Shock Wave link changed seed or recipient rules");
                    }
                    else if(route=="fissure")
                    {
                        long id=checked(fissures.NextId+cases+1);var crack=new FissureCrack(id,id-1,caster.Serial.Value,caster.Map.MapID,1,primary.X*2,primary.Y*2,primary.Z,0,4096,4096,mods,seed,0,at,at.AddMilliseconds(83*40));
                        DruidFissure.Restore(new([], [crack], [],id));Require(DruidFissure.Recipient(caster,crack)==primary,"Fissure link receiver selection");
                        Roll(4096,4096);Call(typeof(DruidFissure),"Burn",crack,true);Require(DruidFissure.Snapshot().Cracks.Single(x=>x.Id==id).Seed==expectedSeed,"Fissure link changed source seed");
                    }
                    else
                    {
                        long id=DruidMoltenBoulder.Create(caster,aim,1,at);var m=DruidMoltenBoulder.Snapshot().Missiles.Single(x=>x.Id==id)with{Missile=periodic?455:route=="boulder"?452:453,Modifiers=mods,Seed=seed};
                        var model=DruidMoltenBoulder.Model(primary);
                        if(route=="boulder"&&(model?.Small==true||model?.Large!=true)){SourceRandom.Roll(ref expectedSeed);}
                        object[] arguments=[caster,primary,m,raw,route=="boulder",seed,false];Call(typeof(DruidMoltenBoulder),"Impact",arguments);Require((ulong)arguments[5]==expectedSeed,"Boulder link changed source knockback draw");
                    }
                    Require(body.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==NamedElements.To256(settled.Total)&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(!periodic&&settled.Total>0?1:0)
                        &&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==typed)&&NecromancerEffects.ManaBalance256(caster)==mana,
                        $"Druid {route}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");
                    if(settled.Poison>0){Require(typed.Total<NamedElements.To256(settled.Total),"Druid poison entered typed lanes");}cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Require(cases==36,"Incomplete Druid element link matrix");
        }
        finally
        {
            DruidTwister.Restore(twisters);DruidTornado.Restore(tornadoes);DruidMoltenBoulder.Restore(boulders);DruidVolcano.Restore(volcanoes);DruidFissure.Restore(fissures);DruidShockWave.Restore(waves);
            DruidTwister.Uninterruptible=twisterGuard;DruidTornado.Uninterruptible=tornadoGuard;DruidVolcano.Uninterruptible=volcanoGuard;DruidMoltenBoulder.Uninterruptible=boulderGuard;
            primary.MoveToWorld(origin,owner.Map);receiver.MoveToWorld(received,owner.Map);foreach(var type in Enum.GetValues<ResistanceType>()){primary.SetResistance(type,0);receiver.SetResistance(type,0);}TimedEffects.Clear(primary);caster.Delete();
        }
    }
}
