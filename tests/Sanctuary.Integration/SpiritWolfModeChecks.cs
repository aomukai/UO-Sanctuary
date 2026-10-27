// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class SpiritWolfModeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SpiritWolfModeChecks));
    private sealed record Saved(uint Owner,uint Wolf,uint Target,uint Helm,int Map,Point3D Point,SpiritWolfBodyState State,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"spirit-wolf-mode-fixtures.json");
    public static void Configure()
    {Register("spirit-wolf-mode-check",Check);Register("spirit-wolf-mode-reload-check",Reload);}
    private static void Register(string command,Action check)=>ConsoleInputHandler.RegisterCommand(command,"Captured wolf A1stats,gear transitions and saved pending-bite cancellation",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Command}: passed",command);}catch(Exception e){Log.Error(e,"{Command}: failed",command);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Through(DruidSpiritWolfBody body,int frame)
    {var c=body.Prepared.Controller;Require(frame>c.Frame,"Non-forward mode test");DruidSpiritWolfWorld.Advance(body,c.NextUtc.AddMilliseconds((frame-c.Frame-1)*40L),true);}
    private static int Expected(DruidSpiritWolfBody wolf,BaseCreature target,int modeAttack,int percent=0)
    {var b=wolf.Prepared.Snapshot.Birth;return AssassinTrapAttack.Chance(b.Level,b.BaseAttack+b.AttackBonus+modeAttack,0,percent,ClassStats.Level(target),ClassStats.Defense(target));}
    private static void Check()
    {
        // This target survives restarts; keep its floor clear of the natural
        // live scenes whose process-local scene counter restarts from zero.
        var p=SpiritWolfWorldChecks.Fresh(-1024);var at=TimedEffects.Now.AddDays(4);
        var target=new BladeMissileTarget{CantWalk=true,NoKillAwards=true};target.SetHits(10000);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+1,p.Y,p.Z),at,901);
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(173),true);
        bool retained=false;var auras=PaladinAuras.Snapshot();
        try
        {
            var initial=wolf.Prepared;Through(wolf,25);
            var entry=wolf.Prepared;Require(entry.Controller is{Mode:4,AttackPacket:{Rank:1,Minimum:0,Maximum:0,Attack:125}},"Actual A1entry capture");
            int rating=entry.Snapshot.Birth.BaseAttack+entry.Snapshot.Birth.AttackBonus+125;
            // A supplied defense-stat context keeps this receiver away from
            // the95percent cap; the real aura consumer computes its defense.
            int defense=ClassStats.Defense(target);Require(defense>0,"Mapped target needs native defense");
            PaladinAuras.Restore(auras with{Leases=auras.Leases.Append(new PaladinAuraLease(target.Serial.Value,p.Serial.Value,
                CombatAbility.Defiance,1,checked((2*rating/defense+1)*100),at.AddDays(1))).ToArray()});
            int chance=DruidSpiritWolfHit.Chance(wolf,entry.Controller,target);
            Require(chance==Expected(wolf,target,125) && chance is >5 and <95 && ClassStats.AttackRating(wolf)==rating,"Mode AR missing or doubled in shared/native hit consumers");
            Require(p.EquipItem(helm),"Actual soft-rank helm");ClassGear.Refresh(p);
            Require(SkillMechanics.Rank(p,CombatAbility.SummonSpiritWolf)==2 && wolf.Prepared.Snapshot==initial.Snapshot
                && wolf.Prepared.Controller.AttackPacket==entry.Controller.AttackPacket && DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target)==chance,"Mid-windup gear rewrote birth or current mode");
            SorceressEffects.Profiles.Store(wolf,new(Enchantment:new(at.AddDays(1),1,256,256,100,p.Serial.Value)));
            Require(ClassStats.AttackRating(wolf)==rating*2 && DruidSpiritWolfHit.Chance(wolf,wolf.Prepared.Controller,target)==Expected(wolf,target,125,100),"Actor AR percent did not include flat mode AR once");
            SorceressEffects.Profiles.Clear(wolf);
            Through(wolf,entry.Controller.ActionAt);
            Require(wolf.Prepared.Controller.PendingStrikeRank==1 && wolf.Prepared.Controller.LastHit is{Outcome:not SpiritWolfHitOutcome.Unsupported}
                && wolf.Prepared.Controller.LastHit.Chance==chance,"Impact reread owner rank or ignored captured AR");
            Through(wolf,wolf.Prepared.Controller.EndAt);Through(wolf,wolf.Prepared.Controller.NextThink);
            var next=wolf.Prepared;Require(next.Controller is{Mode:4,AttackPacket:{Rank:2,Attack:145}} && next.Snapshot==initial.Snapshot,"Next A1did not refresh live owner rank separately from birth");
            string text=wolf.SavedText;bool rejected=false;
            try{wolf.RestorePrepared(next with{Controller=next.Controller with{AttackPacket=new(2,0,0,146)}});}catch(InvalidDataException){rejected=true;}
            Require(rejected && wolf.SavedText==text,"Malformed source packet mutated live save");
            rejected=false;try{wolf.RestorePrepared(next with{Controller=next.Controller with{AttackPacket=null}});}catch(InvalidDataException){rejected=true;}
            Require(rejected && wolf.SavedText==text,"New-format pending attack without packet was accepted");
            // Exact old JSON has no packet property. Its historical owner rank
            // cannot be reconstructed, so migration cancels without RNG/damage.
            var legacy=JsonNode.Parse(text);legacy["Controller"]["Profile"]="source-ai-native-ground-fit-cold-hit-v2";legacy["Controller"].AsObject().Remove("AttackPacket");
            wolf.RestorePrepared(JsonSerializer.Deserialize<SpiritWolfBodyState>(legacy.ToJsonString()));
            Require(wolf.Prepared.Controller is{Mode:1,ActionAt:0,AttackPacket:null,Profile:"source-ai-native-ground-fit-mode-attack-v3"}
                && wolf.Prepared.Controller.Seed==next.Controller.Seed && wolf.Prepared.Controller.StrikeRequests==next.Controller.StrikeRequests,"Legacy pending swing migration invented a packet or replayed combat");
            wolf.RestorePrepared(next);p.AddToBackpack(helm);ClassGear.Refresh(p);
            Save(p,wolf,target,helm);retained=true;
            Log.Information("Wolf A1mode:actual rank1AR125/current rank2AR145 captures,unchanged cold birth,additive AR plus live Enchant percent,impact snapshot,next-entry refresh,atomic validation and legacy pending-bite cancellation pass");
        }
        finally{PaladinAuras.Restore(auras);if(!retained){target.Delete();SpiritWolfWorldChecks.End(p);}}
    }
    private static void Save(PlayerMobile owner,DruidSpiritWolfBody body,BaseCreature target,Item helm)
    {
        Require(body.Prepared.Controller is{Mode:4,AttackPacket:{Rank:2,Attack:145}} && SkillMechanics.Rank(owner,CombatAbility.SummonSpiritWolf)==1,"Pending saved packet must differ from current owner rank");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,owner.Map.MapID,owner.Location,body.Prepared,SkillHealth.Balance256(target))));
    }
    private static void NextEntry(DruidSpiritWolfBody body)
    {
        // Resume uses legal native placement and may first need to approach.
        // Stop at the next real A1entry, before its scheduled action event.
        for(int frame=0;frame<512;frame++)
        {
            DruidSpiritWolfWorld.Advance(body,body.Prepared.Controller.NextUtc,true);
            Require(body.Prepared.Controller.Fault==null,"Reloaded approach fault");
            if(body.Prepared.Controller.Mode==4){return;}
        }
        throw new InvalidOperationException("Reloaded wolf did not enter A1: "+JsonSerializer.Serialize(body.Prepared.Controller));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var p=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var wolf=World.FindMobile((Serial)saved.Wolf) as DruidSpiritWolfBody;
        var target=World.FindMobile((Serial)saved.Target) as BaseCreature;var helm=World.FindItem((Serial)saved.Helm);
        // New-format validation requires the actual serialized rank2packet.
        // PersistentMinions.Restore then cancels every offline pending action.
        Require(wolf is{Alive:true,Deleted:false} && wolf.ControlMaster==p && wolf.Prepared.Snapshot==saved.State.Snapshot
            && wolf.Prepared.Controller is{Mode:1,ActionAt:0,AttackPacket:null} && wolf.Prepared.Controller.Seed==saved.State.Controller.Seed
            && wolf.Prepared.Controller.StrikeRequests==saved.State.Controller.StrikeRequests && SkillHealth.Balance256(target)==saved.Life
            && SkillMechanics.Rank(p,CombatAbility.SummonSpiritWolf)==1,"Actual reload lost actor/birth or replayed pending bite");
        if(p.Map==Map.Internal){p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);}
        NextEntry(wolf);
        Require(wolf.Prepared.Controller is{Mode:4,AttackPacket:{Rank:1,Attack:125}} && SkillHealth.Balance256(target)==saved.Life,"Reloaded next A1did not sample current rank before impact");
        Require(p.EquipItem(helm),"Reloaded actual helm");ClassGear.Refresh(p);
        wolf.RestorePrepared(wolf.Prepared with{Controller=DruidSpiritWolfWorld.Park(wolf.Prepared.Controller)});
        NextEntry(wolf);
        Require(wolf.Prepared.Controller is{Mode:4,AttackPacket:{Rank:2,Attack:145}},"Reloaded higher-rank next-entry capture");
        p.AddToBackpack(helm);ClassGear.Refresh(p);Save(p,wolf,target,helm);
    }
}
