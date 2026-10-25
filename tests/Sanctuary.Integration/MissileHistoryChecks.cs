// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class MissileHistoryChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MissileHistoryChecks));
    private sealed class Minimum:Random{public override int Next(int maximum)=>0;}
    private sealed class Maximum:Random{public override int Next(int maximum)=>Math.Max(0,maximum-1);}
    private sealed record Saved(uint Owner,uint First,uint Other,int Map,Point3D Origin,Point3D Point,
        WeaponFlight Flight=null,PoisonCloud Poison=null,PlagueCloud Plague=null);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"missile-history-fixtures.json");
    public static void Configure(){Register("missile-history-check",Check);Register("missile-history-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Overlapping missile victims and actual saved previous-collider history",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action run)
    {
        var random=AmazonBow.LaunchRandom;var damage=WeaponMissiles.DamageRoll;var hit=WeaponMissiles.HitRoll;var block=SourceBlocking.BlockRoll;var item=WeaponMissiles.ItemEffects;
        try{AmazonBow.LaunchRandom=new Minimum();WeaponMissiles.DamageRoll=_=>0;WeaponMissiles.HitRoll=()=>0;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.ItemEffects=new Maximum();run();}
        finally{AmazonBow.LaunchRandom=random;WeaponMissiles.DamageRoll=damage;WeaponMissiles.HitRoll=hit;SourceBlocking.BlockRoll=block;WeaponMissiles.ItemEffects=item;}
    }
    private static void Train(PlayerMobile p,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var t),"Talent");
        foreach(string id in t.Prerequisites){Train(p,SkillMechanics.All.Single(x=>x.TalentId==id).Ability);}
        if(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train");}
    }
    private static PlayerMobile Fresh(CombatAbility ability,bool pierce=false)
    {
        var p=MagicArrowChecks.Fresh("LightningFuryOwner");Train(p,ability);if(pierce){Train(p,CombatAbility.Pierce);}
        var w=ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive());ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));
        p.AddToBackpack(w);Require(p.EquipItem(w),"Ordinary javelin equip");ClassGear.Refresh(p);return p;
    }
    private static BowLaunchTarget Target(PlayerMobile p)
    {
        var t=MagicArrowChecks.Target(p);t.SetResistance(ResistanceType.Poison,0);t.SetResistance(ResistanceType.Energy,0);
        t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);return t;
    }
    private static WeaponFlight Cast(PlayerMobile p,CombatAbility ability,Mobile target)
    {
        MagicArrowChecks.Ready(p);MagicArrowChecks.Mana(p);
        Require(ClassCombat.Perform(p,ability,target,target.Location,Core.TickCount),"Learned javelin start");
        Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Learned javelin release");
        return WeaponMissiles.Snapshot().Flights.Single(x=>x.Owner==p.Serial.Value);
    }
    private static void Keep(PlayerMobile p,WeaponFlight flight=null,PoisonCloud poison=null,PlagueCloud plague=null)
    {
        var all=WeaponMissiles.Snapshot();WeaponMissiles.Restore(all with
        {
            Flights=all.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flight==null?[]:[flight]).ToArray(),
            PoisonClouds=all.PoisonClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(poison==null?[]:[poison]).ToArray(),
            PlagueClouds=all.PlagueClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(plague==null?[]:[plague]).ToArray()
        });
    }
    private static void Parents()
    {
        foreach(var ability in new[]{CombatAbility.PoisonJavelin,CombatAbility.PlagueJavelin,CombatAbility.LightningBolt,CombatAbility.LightningFury})
        {
            var p=Fresh(ability,true);var a=Target(p);var b=Target(p);
            try
            {
                int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);var f=Cast(p,ability,a);
                Require(f.Pierces==4,"Actual Pierce continuation supplier");
                for(int frame=0;frame<20&&SkillHealth.Balance256(a)==ah&&SkillHealth.Balance256(b)==bh;frame++)
                {
                    var current=WeaponMissiles.State(f.Id);Require(current!=null,"Parent disappeared before its first contact");
                    Keep(p,current);WeaponMissiles.Tick(current.NextUtc);
                }
                var first=SkillHealth.Balance256(a)<ah?a:b;var other=first==a?b:a;
                Require(SkillHealth.Balance256(first)<(first==a?ah:bh)&&SkillHealth.Balance256(other)==(other==a?ah:bh),"Parent did not select exactly one overlapping victim");
                var next=WeaponMissiles.State(f.Id);Require(next is{Pierces:3}&&next.LastCollision==first.Serial.Value,"First parent history/Pierce");
                int firstLife=SkillHealth.Balance256(first),otherLife=SkillHealth.Balance256(other);
                int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));
                Keep(p,next);WeaponMissiles.Tick(next.NextUtc);
                Require(SkillHealth.Balance256(first)==firstLife&&SkillHealth.Balance256(other)<otherLife
                    &&WeaponMissiles.State(f.Id) is{Pierces:2} after&&after.LastCollision==other.Serial.Value,
                    "Repeated first collider hid overlapping second victim: "+ability);
                Require(NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Overlap charged another release");
            }
            finally{AmazonJavelins.Cancel(p);a.Delete();b.Delete();p.Delete();}
        }
    }
    private static TimedEffectState Poison(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);
    private static Saved Cloud(bool plague,bool persist)
    {
        var ability=plague?CombatAbility.PlagueJavelin:CombatAbility.PoisonJavelin;
        var p=Fresh(ability);var a=Target(p);var b=Target(p);bool retained=false;
        try
        {
            var parent=Cast(p,ability,a);var at=TimedEffects.Now;
            PoisonCloud pc=null;PlagueCloud qc=null;Mobile first;
            if(plague)
            {
                PlagueJavelins.Burst(p,parent with{X=a.X,Y=a.Y},a,at);
                qc=PlagueJavelins.Snapshot().Where(x=>x.Owner==p.Serial.Value).ElementAt(2);Keep(p,plague:qc);
                first=PlagueJavelins.Recipient(p,qc);PlagueJavelins.Tick(qc.NextUtc);
                qc=PlagueJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value);
            }
            else
            {
                pc=PoisonJavelins.Trail(p,parent with{PathChanged=true,X=a.X,Y=a.Y,NextUtc=at});Keep(p,poison:pc);
                first=PoisonJavelins.Recipient(p,pc);PoisonJavelins.Tick(pc.NextUtc);
                pc=PoisonJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value);
            }
            var other=first==a?b:a;
            Require(first==a||first==b,"Cloud first collider was not one of the overlapping victims");
            Require(Poison(first)!=null&&Poison(other)==null,"Cloud first contact did not select one victim");
            if(persist)
            {
                var held=DateTime.UtcNow.AddDays(1);if(plague){qc=qc with{NextUtc=held};Keep(p,plague:qc);}else{pc=pc with{NextUtc=held};Keep(p,poison:pc);}
                retained=true;return new(p.Serial.Value,first.Serial.Value,other.Serial.Value,p.Map.MapID,p.Location,first.Location,Poison:pc,Plague:qc);
            }
            if(plague){PlagueJavelins.Tick(qc.NextUtc);}else{PoisonJavelins.Tick(pc.NextUtc);}
            Require(Poison(other)!=null,"Repeated first cloud collider hid overlapping second victim: "+ability);
            uint last=plague?PlagueJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value).LastCollision:PoisonJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value).LastCollision;
            Require(last==other.Serial.Value,"Cloud history did not become the second victim");
            if(plague){PlagueJavelins.Tick(qc.NextUtc.AddMilliseconds(40));}else{PoisonJavelins.Tick(pc.NextUtc.AddMilliseconds(40));}
            last=plague?PlagueJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value).LastCollision:PoisonJavelins.Snapshot().Single(x=>x.Owner==p.Serial.Value).LastCollision;
            Require(last==first.Serial.Value,"Cloud history became a permanent already-hit set");return null;
        }
        finally{if(!retained){AmazonJavelins.Cancel(p);a.Delete();b.Delete();p.Delete();}}
    }
    private static Saved Child(bool persist)
    {
        var p=Fresh(CombatAbility.LightningFury);var a=Target(p);var b=Target(p);bool retained=false;
        try
        {
            var parent=Cast(p,CombatAbility.LightningFury,a);long id=WeaponMissiles.LaunchFuryChild(p,parent,a,TimedEffects.Now);
            Require(id>0,"Actual Fury child factory");var child=WeaponMissiles.State(id);Keep(p,child);
            int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b);
            for(int frame=0;frame<20&&SkillHealth.Balance256(a)==ah&&SkillHealth.Balance256(b)==bh;frame++)
            {child=WeaponMissiles.State(id);Require(child!=null,"Fury child disappeared before contact");WeaponMissiles.Tick(child.NextUtc);}
            var first=SkillHealth.Balance256(a)<ah?a:b;var other=first==a?b:a;child=WeaponMissiles.State(id);
            Require(child is{Pierces:0}&&child.LastCollision==first.Serial.Value&&SkillHealth.Balance256(other)==(other==a?ah:bh),"Fury child first single contact/history");
            if(persist)
            {
                child=child with{NextUtc=DateTime.UtcNow.AddDays(1)};Keep(p,child);retained=true;
                return new(p.Serial.Value,first.Serial.Value,other.Serial.Value,p.Map.MapID,p.Location,first.Location,Flight:child);
            }
            int hp=SkillHealth.Balance256(other);WeaponMissiles.Tick(child.NextUtc);
            Require(SkillHealth.Balance256(other)<hp&&WeaponMissiles.State(id) is{Pierces:0} next&&next.LastCollision==other.Serial.Value,"Fury child history hid overlapping second victim or used a Pierce charge");return null;
        }
        finally{if(!retained){AmazonJavelins.Cancel(p);a.Delete();b.Delete();p.Delete();}}
    }
    private static void Check()
    {
        Parents();Cloud(false,false);Cloud(true,false);Child(false);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Cloud(false,true),Cloud(true,true),Child(true)}));
        Log.Information("Missile history:actual four learned javelin parents, stationary/moving poison clouds and penetrating Fury child skip prior collider before selecting overlap; resource/Pierce/history and pending saved contacts pass");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));var next=new Saved[saved.Length];
        for(int i=0;i<saved.Length;i++)
        {
            var s=saved[i];var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var first=(BowLaunchTarget)World.FindMobile((Serial)s.First);var other=(BowLaunchTarget)World.FindMobile((Serial)s.Other);
            p.MoveToWorld(s.Origin,Map.Maps[s.Map]);var all=WeaponMissiles.Snapshot();var now=TimedEffects.Now;
            Require((s.Flight==null||all.Flights.Single(x=>x.Id==s.Flight.Id)==s.Flight)
                &&(s.Poison==null||all.PoisonClouds.Single(x=>x.Id==s.Poison.Id)==s.Poison)
                &&(s.Plague==null||all.PlagueClouds.Single(x=>x.Id==s.Plague.Id)==s.Plague),"Saved missile/cloud history/path/clock changed");
            var point=s.Flight is{} f?new Point3D((int)Math.Round(f.X+f.Vx),(int)Math.Round(f.Y+f.Vy),f.Z):s.Point;
            first.MoveToWorld(point,p.Map);other.MoveToWorld(point,p.Map);int firstHp=SkillHealth.Balance256(first),otherHp=SkillHealth.Balance256(other);
            int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(CombatTiming.Equipped(p));uint history;
            WeaponFlight flight=null;PoisonCloud poison=null;PlagueCloud plague=null;
            if(s.Flight!=null)
            {
                Keep(p,s.Flight with{NextUtc=now});WeaponMissiles.Tick(now);flight=WeaponMissiles.State(s.Flight.Id);
                Require(flight!=null&&SkillHealth.Balance256(first)==firstHp&&SkillHealth.Balance256(other)<otherHp,"Reload forgot prior child collider or hid other victim");history=flight.LastCollision;
            }
            else
            {
                TimedEffects.Clear(other);
                if(s.Poison!=null){Keep(p,poison:s.Poison with{NextUtc=now});PoisonJavelins.Tick(now);poison=PoisonJavelins.Snapshot().Single(x=>x.Owner==s.Owner);history=poison.LastCollision;}
                else{Keep(p,plague:s.Plague with{NextUtc=now});PlagueJavelins.Tick(now);plague=PlagueJavelins.Snapshot().Single(x=>x.Owner==s.Owner);history=plague.LastCollision;}
                Require(Poison(other)?.PoisonSource==s.Owner,"Reload cloud hid overlapping second victim");
            }
            Require(history==s.Other&&NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(CombatTiming.Equipped(p))==quantity,"Reload history/resource transaction changed");
            var held=DateTime.UtcNow.AddDays(1);flight=flight==null?null:flight with{NextUtc=held};poison=poison==null?null:poison with{NextUtc=held};plague=plague==null?null:plague with{NextUtc=held};Keep(p,flight,poison,plague);
            next[i]=s with{First=s.Other,Other=s.First,Point=point,Flight=flight,Poison=poison,Plague=plague};
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(next));Parents();
        Log.Information("Missile history reload:actual saved stationary/moving clouds and Fury child retain prior collider, select overlapping other victim, advance history once and do not repay mana/ammunition; four learned parent overlap checks repeat");
    }
}
