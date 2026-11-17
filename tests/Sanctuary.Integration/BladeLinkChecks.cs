// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class BladeLinkChecks
{
    private sealed class High:Random{public override int Next(int n)=>Math.Max(0,n-1);}
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static readonly MethodInfo SentinelImpact=typeof(BladeSentinelMissiles).GetMethod("Impact",BindingFlags.Static|BindingFlags.NonPublic);
    private static void Sentinel(BladeSentinelBody blade,BaseCreature target,BladeMissileState missile)=>SentinelImpact.Invoke(null,[blade,target,missile,(Func<int,int>)(_=>0)]);
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Assassin);caster.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(caster,99);caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        while(caster.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(caster,CharacterStat.Strength),"Blade link strength");}while(caster.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(caster,CharacterStat.Dexterity),"Blade link dexterity");}
        foreach(var ability in new[]{CombatAbility.BladeSentinel,CombatAbility.BladeShield,CombatAbility.BladeFury}){DeathSentryExplosionChecks.Train(caster,ability,3);}
        var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));caster.AddToBackpack(claw);Require(caster.EquipItem(claw),"Blade link original claw");ClassGear.Refresh(caster);
        var original=primary.Location;var received=receiver.Location;var oldHit=BladeShields.HitRoll;var oldSkill=BladeShields.SkillRoll;var oldWeapon=BladeShields.WeaponRoll;var oldEffects=BladeShields.Effects;var oldWear=BladeShields.WearRoll;
        var furyHit=WeaponMissiles.HitRoll;var furyDamage=WeaponMissiles.DamageRoll;var furyItems=WeaponMissiles.ItemEffects;var block=SourceBlocking.BlockRoll;BladeSentinelBody blade=null;int cases=0;
        BladeShields.HitRoll=WeaponMissiles.HitRoll=()=>0;BladeShields.SkillRoll=BladeShields.WeaponRoll=WeaponMissiles.DamageRoll=_=>0;BladeShields.Effects=BladeShields.WearRoll=WeaponMissiles.ItemEffects=new High();SourceBlocking.BlockRoll=_=>99;
        try
        {
            primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+13,caster.Y,caster.Z),caster.Map);
            blade=BladeMissileChecks.Body(caster,TimedEffects.Now.AddHours(2),false);
            for(int i=0;i<30&&!BladeSentinelMissiles.Snapshot().Missiles.Any(x=>x.Owner==blade.Serial.Value);i++){BladeMissileChecks.Frames(blade,1);}
            var missile=BladeSentinelMissiles.Snapshot().Missiles.Single(x=>x.Owner==blade.Serial.Value);
            var mixed=new MissileSourceStats(FireMin:256,FireMax:512,LightningMin:256,LightningMax:768,MagicMin:256,MagicMax:512,ColdMin:256,ColdMax:512,ColdFrames:100,PoisonMin:256,PoisonMax:512,PoisonFrames:100,PoisonCount:1);
            missile=missile with{Damage=missile.Damage with{Elements=mixed}};
            foreach(string route in new[]{"shield","sentinel","fury"})foreach(string mode in new[]{"normal","receiver-resisted","primary-physical-immune"})
            {
                primary.SetResistance(ResistanceType.Physical,mode=="primary-physical-immune"?100:0);bool resisted=mode=="receiver-resisted";
                receiver.SetResistance(ResistanceType.Physical,resisted?25:0);receiver.SetResistance(ResistanceType.Fire,resisted?50:0);receiver.SetResistance(ResistanceType.Cold,resisted?75:0);receiver.SetResistance(ResistanceType.Energy,resisted?100:0);
                WeaponFlight flight=null;GearDamage raw;Mobile source=route=="sentinel"?blade:caster;
                if(route=="shield"){int skill=SkillMechanics.Values(caster,CombatAbility.BladeShield,3).PhysicalDamage256(false);raw=BladeShieldDamage.Capture(caster,primary,skill,_=>0,new High());}
                else if(route=="sentinel"){raw=missile.Damage.Roll(primary,_=>0,out _);}
                else
                {
                    long id=BladeFuryMissiles.Launch(caster,new(caster.X+12,caster.Y,caster.Z),3,new High());Require(id>0,"Blade link real Fury allocation");flight=WeaponMissiles.State(id);flight=flight with{Damage=flight.Damage with{Elements=mixed}};var pool=WeaponMissiles.Snapshot();WeaponMissiles.Restore(pool with{Flights=pool.Flights.Where(x=>x.Id!=id).Append(flight).ToArray()});raw=flight.Damage.Roll(primary,_=>0,out _);
                }
                var settled=SkillCombat.MitigatePrepared(source,primary,raw with{Poison=0});var parts=DirectDamageParts.From(settled);var share=parts.Fraction(percent);
                int expected=resisted?share.Physical256*75/100+share.Magic256+share.Fire256/2+share.Cold256/4:checked((int)share.Total),aLife=SkillHealth.Balance256(primary),bLife=SkillHealth.Balance256(receiver),events=0,mana=NecromancerEffects.ManaBalance256(caster);SkillDamageContact observed=default;
                void Observe(SkillDamageContact contact){events++;observed=contact;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    if(route=="shield"){Require(BladeShields.Pulse(caster,3)==1,"Blade link isolated Shield pulse");}
                    else if(route=="sentinel"){Sentinel(blade,primary,missile);}
                    else{for(int i=0;i<45&&WeaponMissiles.State(flight.Id)is{} current;i++){WeaponMissiles.Tick(current.NextUtc);}Require(WeaponMissiles.State(flight.Id)==null,"Blade link Fury did not terminate");}
                    Require(body.Prepared.Links.Fault==null&&aLife-SkillHealth.Balance256(primary)==NamedElements.To256(settled.Total)&&bLife-SkillHealth.Balance256(receiver)==expected&&events==(settled.Total>0?1:0)
                        &&(events==0||observed.Source==source&&observed.Target==primary&&observed.Parts==parts)&&NecromancerEffects.ManaBalance256(caster)==mana,
                        $"Blade {route}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={aLife-SkillHealth.Balance256(primary)}/{NamedElements.To256(settled.Total)};secondary={bLife-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;TimedEffects.Clear(primary);}
            }
            primary.SetResistance(ResistanceType.Physical,0);foreach(var type in Enum.GetValues<ResistanceType>()){receiver.SetResistance(type,0);}
            var state=body.Prepared;var unknown=missile with{Damage=missile.Damage with{Elements=missile.Damage.Elements with{LifeDrain=7}}};int before=SkillHealth.Balance256(receiver),hp=SkillHealth.Balance256(primary);var expectedRaw=unknown.Damage.Roll(primary,_=>0,out _);var expectedParts=SkillCombat.MitigatePrepared(blade,primary,expectedRaw with{Poison=0});
            try
            {
                var typed=DirectDamageParts.From(expectedParts);int shared=checked((int)typed.Fraction(percent).Total);
                Sentinel(blade,primary,unknown);Require(body.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(receiver)==shared&&hp-SkillHealth.Balance256(primary)==NamedElements.To256(expectedParts.Total)+7,"Known Sentinel components failed to share or flat NPC drain changed its primary total/classification");
            }
            finally{body.RestorePrepared(state);TimedEffects.Clear(primary);}
            Require(cases==9,"Incomplete Blade link matrix");
        }
        finally
        {
            BladeShields.HitRoll=oldHit;BladeShields.SkillRoll=oldSkill;BladeShields.WeaponRoll=oldWeapon;BladeShields.Effects=oldEffects;BladeShields.WearRoll=oldWear;WeaponMissiles.HitRoll=furyHit;WeaponMissiles.DamageRoll=furyDamage;WeaponMissiles.ItemEffects=furyItems;SourceBlocking.BlockRoll=block;
            WeaponMissiles.Cancel(caster);blade?.Delete();primary.SetResistance(ResistanceType.Physical,0);foreach(var type in Enum.GetValues<ResistanceType>()){receiver.SetResistance(type,0);}primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);TimedEffects.Clear(primary);caster.Delete();
        }
    }
}
