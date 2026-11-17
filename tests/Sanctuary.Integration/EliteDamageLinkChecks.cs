// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class EliteDamageLinkChecks
{
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    internal static void Run(PlayerMobile owner,WarlockDefilerBody body,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var original=primary.Location;var received=receiver.Location;var oldAlignment=PaladinConversion.Snapshot();
        var others=body.Prepared.Links.Marks.Where(m=>m.Target!=primary.Serial.Value&&m.Target!=receiver.Serial.Value).Select(m=>(BaseCreature)World.FindMobile((Serial)m.Target)).Select(t=>(Actor:t,Position:t.Location)).ToArray();
        var pulse=typeof(EliteAuras).GetField("_nextPulse",BindingFlags.NonPublic|BindingFlags.Static);long oldPulse=(long)pulse.GetValue(null);EncounterCreature source=null;int cases=0;
        try
        {
            foreach(var t in others){t.Actor.MoveToWorld(new(owner.X-40,owner.Y+Array.IndexOf(others,t),owner.Z),owner.Map);}
            source=Create(EliteAura.None);
            foreach(string route in new[]{"direct","melee","ranged","death"})
            foreach(string mode in new[]{"normal","primary-resisted","receiver-resisted","immune"})
            {
                Reset();if(mode=="primary-resisted"){Resist(primary);}if(mode=="receiver-resisted"){Resist(receiver);}if(mode=="immune"){foreach(var r in Enum.GetValues<ResistanceType>()){primary.SetResistance(r,500);}}
                var raw=new GearDamage(17.25,5.25,3.75,2.5,0,Magic:1.125);
                // Original receiver order: shield,physical component,then fire/cold/lightning/magic.
                // These ordinary native NPCs have no shields; retain the independent native helper oracle.
                var settled=new GearDamage(GemCombat.Component(primary,raw.Physical,primary.PhysicalResistance,false),NamedElements.Damage(source,primary,raw.Fire,"fire"),
                    NamedElements.Damage(source,primary,raw.Cold,"cold"),NamedElements.Damage(source,primary,raw.Lightning,"ltng"),0,Magic:NamedElements.Damage(source,primary,raw.Magic,"mag"));
                var typed=DirectDamageParts.From(settled);var split=typed.Fraction(percent);int total=NamedElements.To256(settled.Total),expected=mode=="receiver-resisted"?split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256/4:checked((int)split.Total);
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(owner),events=0;SkillDamageContact observed=default;
                void Observe(SkillDamageContact c){events++;observed=c;Require(SkillHealth.Balance256(primary)==before,"Elite notification arrived after primary health");}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    EliteCombat.Damage(source,primary,raw.Physical,raw.Fire,raw.Cold,raw.Lightning,raw.Magic,weapon:route is"melee"or"ranged",ranged:route=="ranged",death:route=="death");
                    Require(body.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(total>0?1:0)
                        &&(events==0||observed.Source==source&&observed.Target==primary&&observed.Parts==typed)&&NecromancerEffects.ManaBalance256(owner)==mana,
                        $"Elite {route}/{mode} typed link:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            Forget();
            foreach(var aura in new[]{EliteAura.HolyFire,EliteAura.HolyFreeze,EliteAura.HolyShock})
            {
                source=Create(aura);Reset();Require(EliteAuras.Level(primary,aura,true)==EliteAuras.SkillLevel(source),"Actual hostile aura did not admit linked recipient");
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(owner),events=0;
                void Observe(SkillDamageContact c){events++;}SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    pulse.SetValue(null,Core.TickCount);EliteAuras.Tick(Core.TickCount);
                    Require(body.Prepared.Links.Fault==null&&SkillHealth.Balance256(primary)<before&&SkillHealth.Balance256(receiver)==secondary&&events==0&&NecromancerEffects.ManaBalance256(owner)==mana,
                        $"Elite {aura} periodic exclusion:fault={body.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)};secondary={secondary-SkillHealth.Balance256(receiver)};events={events}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}Forget();
            }
            Require(cases==19,"Incomplete elite link/direct-periodic matrix");
        }
        finally
        {
            Forget();PaladinConversion.Restore(oldAlignment);pulse.SetValue(null,oldPulse);primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var t in others){t.Actor.MoveToWorld(t.Position,owner.Map);}foreach(var t in new[]{primary,receiver}){foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}}
        }
        EncounterCreature Create(EliteAura aura)
        {
            var e=new EncounterCreature();e.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Ordinary,ChampionKind.Champion,[],aura,null,new Random(173));
            e.Frozen=e.CantWalk=e.NoKillAwards=true;e.Home=new(owner.X+15,owner.Y+2,owner.Z);e.HomeMap=owner.Map;e.MoveToWorld(e.Home,owner.Map);e.AIObject?.AITimer.Stop();
            Require(PaladinConversion.ApplyAllegiance(owner,e,1000),"Real allegiance for native NPC-to-NPC elite contact");EliteAuras.Observe(e);return e;
        }
        void Reset()
        {
            primary.MoveToWorld(new(source.X+1,source.Y,source.Z),source.Map);receiver.MoveToWorld(new(source.X+40,source.Y,source.Z),source.Map);
            foreach(var t in new[]{primary,receiver}){foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}}
            Require(EliteCombat.Enemy(source,primary,18),"Real elite NPC-to-NPC admission failed");
        }
        void Forget(){if(source!=null){EliteAuras.Forget(source);EliteBolts.Cancel(source);source.Delete();source=null;}PaladinConversion.Restore(oldAlignment);}
    }
    private static void Resist(BaseCreature t){t.SetResistance(ResistanceType.Physical,25);t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,75);t.SetResistance(ResistanceType.Energy,100);}
}
