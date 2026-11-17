// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class GearStrikeLinkChecks
{
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Warlock);caster.AddItem(new Backpack());while(caster.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(caster,CharacterStat.Strength),"Gear strike Strength");}
        var weapon=new Broadsword();caster.AddToBackpack(weapon);Require(caster.EquipItem(weapon),"Gear strike equip");caster.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        var original=primary.Location;var received=receiver.Location;int cases=0;
        try
        {
            primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+40,caster.Y,caster.Z),caster.Map);
            foreach(string route in new[]{"prepared","factory"}){foreach(string mode in new[]{"normal","primary-resisted","receiver-resisted","immune"}){Exercise(route,mode);}}
            foreach(string mode in new[]{"equal-total","allocated-poison","pure-poison","unknown-extra","truncated","zero","cancel","suppressed"}){Exercise("prepared",mode);}
            Require(cases==16,"Incomplete general weapon packet link matrix");
        }
        finally
        {
            GearEffects.Clear(primary);primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var t in new[]{primary,receiver}){foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}}caster.Delete();
        }
        void Exercise(string route,string mode)
        {
            GearEffects.Clear(primary);foreach(var t in new[]{primary,receiver}){foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}}
            if(mode=="primary-resisted"){Resist(primary);}if(mode=="receiver-resisted"){Resist(receiver);}if(mode=="immune"){foreach(var r in Enum.GetValues<ResistanceType>()){primary.SetResistance(r,500);}}
            var raw=new GearDamage(10.25,4.125,2.5,1.75,0,ColdMs:1000,Magic:1.125);
            var packet=GearEffects.PrepareWeaponImpact(caster,primary,.5,new Random(173),raw,projectileCritical:false);int baseTotal=NamedElements.To256(packet.Parts.Total);
            if(mode=="equal-total"){packet=packet with{LifeDamage256=baseTotal};}
            if(mode is"allocated-poison"or"pure-poison")
            {
                var poison=PoisonJavelins.PreparePoison(caster,primary,79,25);Require(poison==new PreparedPoison(79,25),"Prepared poison fixture changed");
                if(mode=="pure-poison"){packet=new(default,default,0);baseTotal=0;}
                packet=packet with{Damage=packet.Damage with{Poison=79*25/256.0},LifeDamage256=baseTotal+79,AllocatedPoison=poison};
            }
            if(mode=="unknown-extra"){packet=packet with{LifeDamage256=baseTotal+83};}if(mode=="truncated"){packet=packet with{LifeDamage256=baseTotal-1};}if(mode=="zero"){packet=packet with{LifeDamage256=0};}
            int total=mode=="cancel"?0:packet.LifeDamage256??baseTotal;bool unclassified=mode is"unknown-extra"or"truncated",suppressed=mode=="suppressed";
            var typed=DirectDamageParts.From(packet.Parts);var split=typed.Fraction(percent);int expected=unclassified||suppressed||total==0?0:mode=="receiver-resisted"?split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256/4:checked((int)split.Total);
            int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0,commits=0,wear=weapon.HitPoints;var state=body.Prepared;SkillDamageContact observed=default;
            void Observe(SkillDamageContact c){events++;observed=c;Require(SkillHealth.Balance256(primary)==before,"Gear notification arrived after health");}
            bool Commit(int physical){commits++;Require(physical==NamedElements.To256(packet.Parts.Physical),"Commit physical changed");return mode!="cancel";}
            SkillDamageEvents.BeforeDirectDamage+=Observe;
            try
            {
                using var scope=suppressed?CombatItemEvents.Suppress(caster,primary):null;
                GearEffects.Strike(caster,primary,.5,new Random(173),wearWeapon:false,preparedDamage:route=="factory"?raw:null,preparedImpact:route=="prepared"?packet:null,projectileCritical:false,sourceDrain:default(PlayerWeaponDrain),beforeCommit:Commit);
                string fault=unclassified?"Direct damage lacks settled typed components":null;
                Require(body.Prepared.Links.Fault==fault&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(total>0&&!suppressed?1:0)&&commits==1&&weapon.HitPoints==wear
                    &&(events==0||observed.Source==caster&&observed.Target==primary&&(unclassified?observed.Parts==null:observed.Parts==typed))&&NecromancerEffects.ManaBalance256(caster)==mana,
                    $"Gear {route}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};commit={commits}");
                if(mode is"allocated-poison"or"pure-poison"){Require(PoisonDaggerChecks.Poison(primary).PoisonClock?.Rate256==79,"Prepared poison scheduler changed");}cases++;
            }
            finally{SkillDamageEvents.BeforeDirectDamage-=Observe;GearEffects.Clear(primary);if(unclassified){body.RestorePrepared(state);}}
        }
    }
    private static void Resist(BaseCreature t){t.SetResistance(ResistanceType.Physical,25);t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,75);t.SetResistance(ResistanceType.Energy,100);}
}
