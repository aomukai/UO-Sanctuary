// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DemonIncomingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DemonIncomingChecks));
    private sealed record Saved(uint Owner,uint Body,uint Enemy,uint Helm,int Mana,int Life,int Map,Point3D Point,Point3D BodyPoint,Point3D EnemyPoint,string BodyState,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"demon-incoming-fixtures.json");
    public static void Configure(){Register("demon-incoming-check",Check);Register("demon-incoming-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Pinned demon return metadata and original-body incoming packets",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Metadata(SkeletonGuardian b,int id,int velocity)
    {
        var metadata=NativeMonsterReturn.Metadata(b);var identity=NativeMonsterReturn.Identity(b.SourceMonsterId);
        Require(metadata.Class==id&&metadata.Base==id&&metadata.Modes==8192&&metadata.Velocity==velocity&&metadata.Killable
            &&identity.Class==id&&CreatureProfiles.For(b).SourceModes["mGH"]=="0"&&CreatureProfiles.For(b).SourceModes["mBL"]=="0"
            &&CreatureProfiles.For(b).SourceModes["mKB"]=="1"&&CreatureProfiles.For(b).HitRecovery.Enabled==false,"Pinned demon return identity/modes leaked from UO appearance");
    }
    private static int Fire(BaseCreature source,PlayerMobile owner,BaseCreature target,int raw256,DateTime at)
    {
        int before=SkillHealth.Balance256(target);NpcFireSpellHit.Deliver(source,owner,target,raw256,at,"qualified enemy fire");
        return before-SkillHealth.Balance256(target);
    }
    private static void Check()
    {
        var p=TaintedFireBallChecks.Owner();var at=TimedEffects.Now.AddDays(2);
        var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);
        var goat=WarlockGoatmanBody.CreatePrepared(p,new(p.X+3,p.Y,p.Z),at);var enemy=FireBoltChecks.Target(p,8);
        try
        {
            Metadata(b,743,6);Metadata(goat,742,8);int mana=NecromancerEffects.ManaBalance256(p);var captured=b.Prepared;
            int untouched=SkillHealth.Balance256(b);bool refused=false;
            try{Fire(goat,p,b,100*256,at);}catch(NotSupportedException){refused=true;}
            Require(refused&&SkillHealth.Balance256(b)==untouched&&NativeMonsterReturn.Session(b).Packets==0,"Ordered incoming adapter bypassed guardian friendship protection");
            Require(Fire(enemy,p,b,100*256,at)==50*256&&NativeMonsterReturn.Session(b).Packets==1
                &&NativeMonsterReturn.Session(b).LastMode!=PreparedMonsterReturn.GetHit,"Ordered incoming fire did not use source resistance/packet freeing/no-GH metadata");
            Require(Fire(enemy,p,goat,100*256,at)==80*256&&NativeMonsterReturn.Session(goat).Packets==1,"Goatman return mapping or own fire resistance");
            Require(TaintedResistFire.PulsePrepared(b,at)&&Fire(enemy,p,b,100*256,at)==21*256,"Incoming fire ignored shared NPC420 resistance lease");
            PaladinAuras.ClearResistanceState(b);int before=SkillHealth.Balance256(b);
            AOS.Damage(b,enemy,10,false,0,100,0,0,0);Require(before-SkillHealth.Balance256(b)==5*256,"Native resisted-damage entry changed the demon's fire resistance");
            Require(b.Prepared==captured&&NecromancerEffects.ManaBalance256(p)==mana,"Incoming packet rewrote birth or spent owner mana");
            var doomed=WarlockTaintedBody.CreatePrepared(p,new(p.X+5,p.Y,p.Z),at);SkillHealth.SetBalance256(doomed,256);
            Fire(enemy,p,doomed,10*256,at);Require(!doomed.Alive||doomed.Deleted,"Source-lethal incoming packet left its demon alive");
            Require(TaintedResistFire.State(doomed)==null,"Dead demon retained its emitter");
        }
        finally{goat.Delete();enemy.Delete();b.Delete();p.Delete();}
        Save();
    }
    private static void Save()
    {
        var p=TaintedFireBallChecks.Owner();var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));var enemy=FireBoltChecks.Target(p,8);
        SkillHealth.SetBalance256(b,b.MaximumLife256-257);
        var saved=new Saved(p.Serial.Value,b.Serial.Value,enemy.Serial.Value,p.FindItemOnLayer(Layer.Helm).Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(b),p.Map.MapID,p.Location,b.Location,enemy.Location,b.SavedText,0);
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var b=(WarlockTaintedBody)World.FindMobile((Serial)saved.Body);var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);
        Require(saved.Generation<2&&b.ControlMaster==p&&b.SavedText==saved.BodyState&&World.FindItem((Serial)saved.Helm)?.Parent==p
            &&SkillHealth.Balance256(b)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original demon/owner/gear/wounds/mana changed on reload");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);PersistentMinions.Resume(p);b.MoveToWorld(saved.BodyPoint,p.Map);enemy.MoveToWorld(saved.EnemyPoint,p.Map);enemy.AIObject?.AITimer.Stop();
        Metadata(b,743,6);int packets=NativeMonsterReturn.Session(b).Packets;
        Require(Fire(enemy,p,b,512,TimedEffects.Now)==256&&NativeMonsterReturn.Session(b).Packets==packets+1
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&b.SavedText==saved.BodyState,"Original restored demon failed its real incoming packet or mutated captured resources");
        var next=saved with{Life=SkillHealth.Balance256(b),Generation=saved.Generation+1};PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(next));
    }
}
