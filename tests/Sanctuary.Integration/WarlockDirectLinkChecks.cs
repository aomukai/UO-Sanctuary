// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
internal static class WarlockDirectLinkChecks
{
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static object Call(Type t,string method,params object[] args)=>t.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
    internal static void Run(PlayerMobile owner,WarlockDefilerBody defiler,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Warlock);caster.AddItem(new Backpack());while(caster.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(caster,CharacterStat.Strength),"Link caster Strength");}
        var sword=new Broadsword();caster.AddToBackpack(sword);Require(caster.EquipItem(sword),"Purge weapon");
        var origin=new Point3D(owner.X+15,owner.Y+2,owner.Z);caster.MoveToWorld(origin,owner.Map);var original=primary.Location;var received=receiver.Location;int cases=0;
        var others=defiler.Prepared.Links.Marks.Where(m=>m.Target!=primary.Serial.Value&&m.Target!=receiver.Serial.Value).Select(m=>(BaseCreature)World.FindMobile((Serial)m.Target)).Select(t=>(Actor:t,Position:t.Location)).ToArray();
        foreach(var other in others){other.Actor.MoveToWorld(new(caster.X-40,caster.Y+Array.IndexOf(others,other),caster.Z),caster.Map);}
        try
        {
            foreach(string route in new[]{"apocalypse","blade-warp","purge","sigil-death"})
            foreach(string mode in new[]{"normal","potion","receiver-resisted","primary-resisted"}){Exercise(route,mode);}
            Exercise("apocalypse","immune");Exercise("apocalypse","allocation-refused");Require(cases==18,"Incomplete Warlock direct link matrix");
        }
        finally
        {
            HexPurge.ClearDebuff(primary);HexPurge.ClearBuff(caster);BladeWarp.Clear(caster);primary.MoveToWorld(original,owner.Map);receiver.MoveToWorld(received,owner.Map);
            foreach(var other in others){other.Actor.MoveToWorld(other.Position,owner.Map);}foreach(var t in new[]{primary,receiver}){foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}CombatStuns.Clear(t);}caster.Delete();
        }
        void Exercise(string route,string mode)
        {
            caster.MoveToWorld(origin,owner.Map);primary.MoveToWorld(new(caster.X+1,caster.Y,caster.Z),caster.Map);receiver.MoveToWorld(new(caster.X+40,caster.Y,caster.Z),caster.Map);
            TimedEffects.Clear(caster);foreach(var t in new[]{primary,receiver}){foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}}
            bool potion=mode is"potion"or"receiver-resisted"or"primary-resisted";if(potion){Require(CombatPotions.Apply(caster,3),"Direct link potion");}
            if(mode=="receiver-resisted"){receiver.SetResistance(ResistanceType.Physical,25);receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
            if(mode=="primary-resisted"){primary.SetResistance(ResistanceType.Physical,25);primary.SetResistance(ResistanceType.Fire,50);primary.SetResistance(ResistanceType.Cold,75);primary.SetResistance(ResistanceType.Energy,100);primary.SetResistance(ResistanceType.Poison,50);}
            if(mode=="immune"){primary.SetResistance(ResistanceType.Fire,500);}
            var mods=new FirestormModifiers(potion?10:0,0,0,0,0);GearDamage raw;HexPurgeTarget purge=null;Corpse corpse=null;CleaveActionTarget donor=null;Gold loot=null;SigilDeathMark mark=null;object job=null;ulong expectedSeed=0;int magic=0;
            try
            {
                if(route=="apocalypse"){raw=new(0,16,0,0,0);}
                else if(route=="blade-warp"){raw=new(0,0,0,0,0,Magic:16);}
                else if(route=="purge")
                {
                    purge=(HexPurgeTarget)Call(typeof(HexPurgeReactionChecks),"Attach",caster,primary,0,0,mods,20);expectedSeed=purge.Reaction.Seed;
                    Require(SourceRandom.Limited(ref expectedSeed,100)<Math.Clamp(purge.Plan.ChanceRaw,0,100),"Purge deterministic chance");magic=SourceSpellPacketRoll.Range(ref expectedSeed,purge.Plan.Minimum256,purge.Plan.Maximum256);raw=new(0,0,0,0,0,Magic:magic/256.0);
                }
                else
                {
                    donor=new CleaveActionTarget{Frozen=true,CantWalk=true,NoKillAwards=true};donor.MoveToWorld(primary.Location,caster.Map);donor.SetHits(400);donor.AIObject?.AITimer.Stop();
                    var previous=ClassLootHooks.CreatureDied;ClassLootHooks.CreatureDied=(a,c)=>{previous?.Invoke(a,c);if(a==donor){corpse=c as Corpse;}};
                    try{donor.Kill();}finally{ClassLootHooks.CreatureDied=previous;}
                    Require(corpse!=null&&CorpseSkillRules.Profiles.Find(corpse)is{} ,"Native Sigil corpse capture");var row=CorpseSkillRules.Profiles.Find(corpse);
                    Require(row.BaseLife!=null&&row.BaseLife.OriginalMonsterSerial==donor.Serial.Value,"Original corpse serial");CorpseSkillRules.Profiles.Store(row with{BaseLife=row.BaseLife with{Base256=102400}});
                    loot=new Gold(1);corpse.DropItem(loot);mark=new(donor.Serial.Value,caster.Serial.Value,caster.Map.MapID,1,TimedEffects.Now.AddMinutes(1),mods,SourceRandom.InitLow(403));expectedSeed=mark.Seed;
                    var packet=SigilDeathProposal.Packet(102400,ref expectedSeed);raw=new(packet.Physical256/256.0,packet.Fire256/256.0,0,0,0);
                    job=Activator.CreateInstance(typeof(SigilDeathProposal).GetNestedType("Job",BindingFlags.NonPublic),mark,corpse.Serial.Value,TimedEffects.Now);
                }
                raw=DruidMoltenBoulder.Potion(route=="apocalypse"?DruidFirestorm.Modifiers(caster):mods,raw);
                var settled=route=="apocalypse"?SkillCombat.MitigatePrepared(caster,primary,raw,out _,skillResistanceDelta:e=>e=="fire"?ApocalypseImpact.Reduction(1):0):SkillCombat.MitigatePrepared(caster,primary,raw);
                var typed=DirectDamageParts.From(settled);int total=mode=="allocation-refused"?0:NamedElements.To256(settled.Total);if(total==0){typed=default;}
                var split=typed.Fraction(percent);int expected=mode=="receiver-resisted"?split.Physical256*75/100+split.Magic256+split.Fire256/2+split.Cold256/4:checked((int)split.Total);
                int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),mana=NecromancerEffects.ManaBalance256(caster),events=0;SkillDamageContact observed=default;bool suppressed=false;
                void Observe(SkillDamageContact c)
                {
                    events++;observed=c;suppressed=!CombatItemEvents.Allowed(c.Source,c.Target);
                    if(route=="purge"){var state=HexPurge.Debuff(primary);var ignored=HexPurgeReactionProposal.OnDamagePrepared(c,TimedEffects.Now,true);Require(!ignored.Triggered&&HexPurge.Debuff(primary)==state,"Suppressed Purge root recursively reacted");}
                }
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    if(route=="apocalypse"){var result=ApocalypseImpact.ReleasePrepared(caster,primary,4096,1,mode!="allocation-refused");Require(result.Applied==(mode!="allocation-refused"),"Apocalypse allocation edge");}
                    else if(route=="blade-warp")
                    {
                        var flights=(Dictionary<long,BladeWarpFlight>)typeof(BladeWarp).GetField("Flights",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);long id=flights.Count==0?1:flights.Keys.Max()+1;
                        var flight=new BladeWarpFlight(id,caster.Serial.Value,caster.Map.MapID,1,caster.X,caster.Y,caster.Z,primary.Location,0,0,1,TimedEffects.Now,4096,4096,mods);flights.Add(id,flight);Call(typeof(BladeWarp),"Impact",caster,flight,primary.Location);
                        int life=SkillHealth.Balance256(primary);Call(typeof(BladeWarp),"Impact",caster,flight,primary.Location);Require(!flights.ContainsKey(id)&&SkillHealth.Balance256(primary)==life,"Blade Warp replayed claimed flight");
                    }
                    else if(route=="purge")
                    {
                        var result=HexPurgeReactionProposal.OnDamagePrepared(new(caster,primary,1,new(1,0,0,0,0)),TimedEffects.Now,true);
                        Require(result.Triggered&&result.Recipients==1&&result.RemainingCharges==purge.Reaction.RemainingCharges-1&&result.Magic256==magic&&HexPurge.Debuff(primary).Reaction.Seed==expectedSeed,"Purge charge/packet/source seed");
                    }
                    else
                    {
                        var result=(SigilDeathBurst)Call(typeof(SigilDeathProposal),"Explode",job,true);Require(result.Exploded&&result.Recipients==1&&loot.Parent==corpse,"Sigil corpse/loot/recipient control");
                        int life=SkillHealth.Balance256(primary);var again=(SigilDeathBurst)Call(typeof(SigilDeathProposal),"Explode",job,true);Require(!again.Exploded&&SkillHealth.Balance256(primary)==life,"Sigil corpse reused");
                    }
                    Require(defiler.Prepared.Links.Fault==null&&before-SkillHealth.Balance256(primary)==total&&secondary-SkillHealth.Balance256(receiver)==expected&&events==(total>0?1:0)
                        &&(events==0||observed.Source==caster&&observed.Target==primary&&observed.Parts==typed&&suppressed==(route is"purge"or"sigil-death"))&&NecromancerEffects.ManaBalance256(caster)==mana,
                        $"Warlock {route}/{mode} typed link:fault={defiler.Prepared.Links.Fault};primary={before-SkillHealth.Balance256(primary)}/{total};secondary={secondary-SkillHealth.Balance256(receiver)}/{expected};events={events};typed={observed.Parts!=null};suppressed={suppressed}");cases++;
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
            finally{HexPurge.ClearDebuff(primary);HexPurge.ClearBuff(caster);if(corpse!=null&&!corpse.Deleted){corpse.Delete();}donor?.Delete();}
        }
    }
}
