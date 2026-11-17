// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class HostileBoltLinkChecks
{
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var source=new GraveyardLich{Frozen=true,CantWalk=true,NoKillAwards=true};source.AIObject?.AITimer.Stop();source.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);
        var slowCaster=ClassSkillChecks.Player(DiabloClass.Amazon);var original=primary.Location;var received=receiver.Location;var old=WeaponMissiles.DamageRoll;int cases=0;
        try
        {
            primary.MoveToWorld(new(source.X+1,source.Y,source.Z),source.Map);receiver.MoveToWorld(new(source.X+40,source.Y,source.Z),source.Map);
            foreach(string mode in new[]{"normal","primary-resisted","receiver-resisted","immune","slow-captured"})
            {
                foreach(var t in new[]{primary,receiver}){foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}}
                if(mode=="primary-resisted"){primary.SetResistance(ResistanceType.Fire,50);}if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Fire,50);}if(mode=="immune"){primary.SetResistance(ResistanceType.Fire,500);}
                AmazonSlowMissiles.Clear(source);if(mode=="slow-captured"){Require(AmazonSlowMissiles.Apply(slowCaster,source,20,TimedEffects.Now),"Hostile slow capture");}
                long id=WeaponMissiles.LaunchHostileFirebolt(source,primary.Location,2049,2049);Require(id>0,"Actual hostile producer refused");var flight=WeaponMissiles.State(id);WeaponMissiles.Cancel(source);AmazonSlowMissiles.Clear(source);
                int raw=mode=="slow-captured"?2049*67/100:2049;Require(flight.Damage.Elements.FireMin==raw&&flight.Damage.Elements.FireMax==raw&&flight.HostileSpell&&flight.Missile=="firebolt","Actual producer capture/clear");
                var parts=DirectDamageParts.From(SkillCombat.MitigatePrepared(source,primary,new(0,raw/256.0,0,0,0)));int total=checked((int)parts.Total),expected=parts.Fire256*percent/100;if(mode=="receiver-resisted"){expected/=2;}
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(owner),draws=0,events=0;SkillDamageContact observed=default;
                WeaponMissiles.DamageRoll=span=>{Require(span==0,"Unexpected hostile draw span");draws++;return 0;};
                void Observe(SkillDamageContact c){events++;observed=c;Require(draws==1,"Hostile typed notification preceded the damage draw");}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    typeof(WeaponMissiles).GetMethod("HostileImpact",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{source,primary,flight});
                    Require(body.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(total>0?1:0)&&draws==1
                        &&(events==0||observed.Source==source&&observed.Target==primary&&observed.Parts==parts)&&NecromancerEffects.ManaBalance256(owner)==mana,
                        $"Hostile firebolt/{mode} typed link:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};draws={draws}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Require(cases==5,"Incomplete hostile firebolt link matrix");
        }
        finally
        {
            WeaponMissiles.DamageRoll=old;WeaponMissiles.Cancel(source);AmazonSlowMissiles.Clear(source);primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var t in new[]{primary,receiver}){foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}}source.Delete();slowCaster.Delete();
        }
    }
}
