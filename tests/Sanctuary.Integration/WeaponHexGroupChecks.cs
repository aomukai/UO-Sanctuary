// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class WeaponHexGroupChecks
{
    private static readonly CombatAbility[] Abilities=[CombatAbility.HexBane,CombatAbility.HexPurge,CombatAbility.HexSiphon];
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WeaponHexGroupChecks));
    private sealed record Saved(uint Owner,uint Target,bool Female,CombatAbility Buff,CombatAbility Debuff,uint Weapon,int Mana);
    private sealed record Manifest(int Generation,Saved[] Rows);
    private static string FileName=>Path.Join(Core.BaseDirectory,"hex-group-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Hex group replacement",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("hex-group-check",false);Register("hex-group-reload-check",true);
    }
    public static void Initialize()
    {var mana=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="HexGroupSaved"&&mana?.Invoke(p)!=false;}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static PlayerMobile Fresh(bool female,int x)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="HexGroupFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var a in Abilities){ClassSkillChecks.Train(p,SkillMechanics.For(a).TalentId);}
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Hex group strength");}
        FixtureSpace.Corridor(p,x,24,3500);var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Hex group weapon");ClassGear.Refresh(p);Mana(p);return p;
    }
    private static int Buffs(Mobile p)=>(HexBane.Buff(p)!=null?1:0)+(HexPurge.Buff(p)!=null?1:0)+(HexSiphon.Buff(p)!=null?1:0);
    private static int Debuffs(Mobile p)=>(HexBane.Debuff(p)!=null?1:0)+(HexPurge.Debuff(p)!=null?1:0)+(HexSiphon.Debuff(p)!=null?1:0);
    private static bool HasBuff(Mobile p,CombatAbility a)=>a==CombatAbility.HexBane?HexBane.Buff(p)!=null:a==CombatAbility.HexPurge?HexPurge.Buff(p)!=null:HexSiphon.Buff(p)!=null;
    private static bool HasDebuff(Mobile p,CombatAbility a)=>a==CombatAbility.HexBane?HexBane.Debuff(p)!=null:a==CombatAbility.HexPurge?HexPurge.Debuff(p)!=null:HexSiphon.Debuff(p)!=null;
    private static bool Cast(PlayerMobile p,CombatAbility a,bool replace=true)=>a==CombatAbility.HexBane?HexBane.CastPrepared(p,replace):a==CombatAbility.HexPurge?HexPurge.CastPrepared(p,replace):HexSiphon.CastPrepared(p,replace);
    private static void Clear(Mobile p){HexBane.Clear(p);HexPurge.Clear(p);HexSiphon.Clear(p);}
    private static void Apply(PlayerMobile p,BaseCreature t,CombatAbility a)
    {
        Mana(p);Require(Cast(p,a),"Hex group preparing registration");bool ok=a==CombatAbility.HexBane?HexBane.ApplyPrepared(p,t,HexBane.Buff(p),true,TimedEffects.Now,true)
            :a==CombatAbility.HexPurge?HexPurge.ApplyPrepared(p,t,HexPurge.Buff(p),HexPurge.Plan(p),true,TimedEffects.Now,true)
            :HexSiphon.ApplyPrepared(p,t,HexSiphon.Buff(p),true,TimedEffects.Now,true);Require(ok,"Hex group target application");
    }
    private static void Exercise(PlayerMobile p,ShockWaveTarget t)
    {
        int transitions=0;
        foreach(var first in Abilities)foreach(var next in Abilities)
        {
            Clear(p);Clear(t);Mana(p);Require(Cast(p,first)&&Buffs(p)==1&&HasBuff(p,first),"Hex first buff");
            int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,next).Mana256;
            Require(Cast(p,next)&&Buffs(p)==1&&HasBuff(p,next)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Hex buff replacement/payment");
            var active=next;NecromancerEffects.SetMana256(p,0);Require(!Cast(p,first)&&Buffs(p)==1&&HasBuff(p,active)&&NecromancerEffects.ManaBalance256(p)==0,"Low mana cleared an old Hex");
            if(first!=next)
            {
                Clear(p);Clear(t);Apply(p,t,first);Require(Debuffs(t)==1&&HasDebuff(t,first),"First target Hex");
                Apply(p,t,next);Require(Debuffs(t)==1&&HasDebuff(t,next)&&Buffs(p)==1&&HasBuff(p,next),"Cross-family target Hex group");
            }
            transitions++;
        }
        Require(transitions==9,"Hex group transitions incomplete");
        Clear(p);Clear(t);Mana(p);Require(Cast(p,CombatAbility.HexBane),"Hex default guard setup");
        try{Cast(p,CombatAbility.HexSiphon,false);throw new InvalidOperationException("Default unapproved cross-Hex guard bypassed");}catch(NotSupportedException){}
        Require(Buffs(p)==1&&HexBane.Buff(p)!=null,"Default guard mutated old group");
        try{WeaponHexGroups.ReplaceBuff(p,CombatAbility.BoneArmor);throw new InvalidOperationException("Non-Hex group admitted");}catch(ArgumentException){}
        Require(Buffs(p)==1&&HexBane.Buff(p)!=null,"Invalid group call mutated state");
        Clear(p);Clear(t);Apply(p,t,CombatAbility.HexPurge);var purge=HexPurge.Debuff(t);Mana(p);Require(Cast(p,CombatAbility.HexSiphon)&&HexPurge.Debuff(t)==purge,"Self buff replacement cleared recipient state");
        HexSiphon.ClearBuff(p);Require(HexPurge.Debuff(t)==purge,"Owner-only clear changed target");
        // A supplied non-event must leave the existing recipient group intact.
        Require(!HexSiphon.ApplyPrepared(p,t,new(p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),HexSiphon.Capture(SkillMechanics.Values(p,CombatAbility.HexSiphon))),false,TimedEffects.Now,true)
            &&HexPurge.Debuff(t)==purge,"Ineligible event cleared prior target Hex");
        Log.Information("Hex group candidate:9buff transitions,6cross-family target transitions,one debit,failed-payment/event preservation,independent self/recipient groups and default refusal pass.");
    }
    private static void Retain(PlayerMobile p,Mobile t)
    {
        var until=TimedEffects.Now.Add(FixtureSpace.Retention);var b=HexBane.Snapshot();HexBane.Restore(new(b.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),b.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));
        var q=HexPurge.Snapshot();HexPurge.Restore(new(q.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),q.Targets.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));
        var s=HexSiphon.Snapshot();HexSiphon.Restore(new(s.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),s.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));
    }
    private static void Check(bool reload)
    {
        Require(WeaponHexGroups.Approved&&Abilities.Where(a=>a!=CombatAbility.HexSiphon).All(a=>!WarlockCombat.Handles(a)&&!WarlockCombat.Implemented(a))&&WarlockCombat.Handles(CombatAbility.HexSiphon),"Approved group/learned Siphon registration mismatch");Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6560:6480);var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();Exercise(p,t);
                Clear(p);Clear(t);var a=female?CombatAbility.HexSiphon:CombatAbility.HexBane;var target=female?CombatAbility.HexBane:CombatAbility.HexPurge;
                Apply(p,t,target);Mana(p);Require(Cast(p,a),"Saved Hex candidate buff");Retain(p,t);p.Name="HexGroupSaved";
                rows[i++]=new(p.Serial.Value,t.Serial.Value,female,a,target,CombatTiming.Equipped(p).Serial.Value,NecromancerEffects.ManaBalance256(p));
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Hex group reload generation");
            foreach(var row in old.Rows)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)row.Target);
                Require(p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon&&NecromancerEffects.ManaBalance256(p)==row.Mana
                    &&Buffs(p)==1&&HasBuff(p,row.Buff)&&Debuffs(t)==1&&HasDebuff(t,row.Debuff),"Original Hex group did not persist");
                var b=HexBane.Snapshot();var q=HexPurge.Snapshot();var s=HexSiphon.Snapshot();FixtureSpace.Corridor(p,row.Female?6560:6480,24,3500);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
                Exercise(p,t);HexBane.Restore(b);HexPurge.Restore(q);HexSiphon.Restore(s);NecromancerEffects.SetMana256(p,row.Mana);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Log.Information("Hex group original:Warlock/Witch independent owner/target Hex family,original weapon and mana persist across two reloads;Bane/Purge input remains gated;Siphon has its dedicated learned route.");
    }
}
