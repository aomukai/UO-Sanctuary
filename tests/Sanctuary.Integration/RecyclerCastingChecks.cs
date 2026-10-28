// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
namespace Sanctuary.Integration;

public static class RecyclerCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RecyclerCastingChecks));
    private sealed record Saved(uint Owner,uint Body,uint Wolf,uint Raven,RecyclerBodyState State,int Mana,int Map,Point3D Point);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"recycler-casting-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Prepared recycler casting transaction",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("recycler-casting-check",Check);Register("recycler-casting-reload-check",Reload);
        Register("recycler-casting-live-check",()=>FixtureClock.NextLoop("recycler-casting-live-result",
            ()=>FixtureNetwork.Connected("recycler-casting-live-result",Live)));
    }
    private static void End(PlayerMobile p){DruidCreeperGround.Cancel(p);DruidRecyclerRecovery.Cancel(p);PersistentMinions.Dismiss(p);p.Corpse?.Delete();p.Delete();}
    private static DruidRecyclerBody Cast(PlayerMobile p,CombatAbility ability,Serial selected,Point3D point,DateTime at,RecyclerRecoveryModel model)
    {
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        Require(DruidRecyclerCasting.Commit(p,ability,selected,point,at,model,out var body),"Prepared recycler cast");
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.VineAbility==ability&&body.Prepared.Controller.RecoveryModel==model
            &&NecromancerEffects.ManaBalance256(p)==mana-cost,"Actual once-only mana/species/profile");return body;
    }
    private static void Transaction(CombatAbility ability)
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var aim=new Point3D(p.X+3,p.Y,p.Z);
        var enemy=new GraveyardZombie{CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(aim,p.Map);enemy.AIObject.AITimer.Stop();
        var spawn=DruidRecyclerCasting.Spawn;var model=ability==CombatAbility.CarrionVine?RecyclerRecoveryModel.LegacySource:RecyclerRecoveryModel.CurrentTableSolarProvisional;
        try
        {
            Require(!ClassCombat.Available(p,ability)&&!DruidCombat.Implemented(ability),"Prepared cast enabled unresolved learned route");
            Require(!p.Map.CanSpawnMobile(aim)&&PetGroundWorld.ZeroFootprint(p.Map,aim),"Zero-footprint occupied point fixture");
            var first=Cast(p,ability,enemy.Serial,p.Location,at,model);Require(first.Location==aim,"Selected-unit vine placement");SkillHealth.SetBalance256(first,1201);
            int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
            DruidRecyclerCasting.Spawn=(_,_,_,_,_,_)=>null;
            Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,model,out _)&&!first.Deleted&&SkillHealth.Balance256(first)==1201
                &&NecromancerEffects.ManaBalance256(p)==mana,"Allocation failure charged/replaced/healed original vine");
            DruidRecyclerBody failed=null;DruidRecyclerCasting.Spawn=(owner,skill,point,time,seed,profile)=>
            {failed=spawn(owner,skill,point,time,seed,profile);NecromancerEffects.SetMana256(owner,cost-1);return failed;};
            Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,model,out _)&&failed is{Deleted:true}&&!first.Deleted
                &&SkillHealth.Balance256(first)==1201,"Payment failure did not discard only the new body");DruidRecyclerCasting.Spawn=spawn;
            Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,model,out _)&&!first.Deleted&&NecromancerEffects.ManaBalance256(p)==cost-1,"Fractional low mana changed old vine");
            NecromancerEffects.SetMana256(p,cost);var second=Cast(p,ability,enemy.Serial,aim,at,model);
            Require(first.Deleted&&NecromancerEffects.ManaBalance256(p)==0,"Exact-mana successful replacement");NecromancerEffects.SetMana256(p,p.ManaMax*256);
            mana=NecromancerEffects.ManaBalance256(p);
            Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,new(65530,65530,0),at,model,out _)&&!second.Deleted
                &&NecromancerEffects.ManaBalance256(p)==mana,"Bad placement paid/replaced");
            p.Frozen=true;Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,model,out _),"Frozen recycler cast");p.Frozen=false;
            p.Paralyzed=true;Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,model,out _),"Paralyzed recycler cast");p.Paralyzed=false;
            Require(!DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,DateTime.SpecifyKind(at,DateTimeKind.Unspecified),model,out _),"Unspecified clock accepted");
            bool refused=false;try{DruidRecyclerCasting.Commit(p,ability,Serial.Zero,aim,at,(RecyclerRecoveryModel)99,out _);}catch(NotSupportedException){refused=true;}
            Require(refused&&!second.Deleted&&NecromancerEffects.ManaBalance256(p)==mana,"Unknown recovery model mutated cast");
            enemy.MoveToWorld(new(aim.X+1,aim.Y,aim.Z),p.Map);var third=Cast(p,ability,enemy.Serial,aim,at,model);
            Require(second.Deleted&&third.Location==enemy.Location,"Moving selected target lost release point");enemy.Delete();
            var fourth=Cast(p,ability,enemy.Serial,aim,at,model);Require(third.Deleted&&fourth.Location==aim,"Deleted target lost cached destination");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.Name=="Tarnhelm").Id,99,new Random(876),true);
            Require(p.EquipItem(helm),"Equip actual rank item");ClassGear.Refresh(p);mana=NecromancerEffects.ManaBalance256(p);
            var boosted=Cast(p,ability,Serial.Zero,aim,at,model);int boostedCost=ability==CombatAbility.CarrionVine?10:15;
            Require(fourth.Deleted&&boosted.Prepared.Snapshot.Birth.Rank==2&&mana-NecromancerEffects.ManaBalance256(p)==boostedCost*256,"Effective-rank birth/payment mismatch");
            helm.Delete();ClassGear.Refresh(p);
        }
        finally{DruidRecyclerCasting.Spawn=spawn;enemy.Delete();End(p);}
    }
    private static void ReplacementAndTown()
    {
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);var aim=new Point3D(p.X+3,p.Y,p.Z);var corpse=RecyclerRecoveryChecks.Corpse(p);
        try
        {
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);int life=SkillHealth.Balance256(p);
            var first=Cast(p,CombatAbility.CarrionVine,Serial.Zero,aim,at,RecyclerRecoveryModel.LegacySource);
            DruidRecyclerWorld.Advance(first,at.AddMilliseconds(25*40),true);long effect=first.Prepared.Controller.LastRecovery;
            Require(effect>0&&CorpseSkillRules.NoSelect(corpse),"Cast body did not feed");
            var second=Cast(p,CombatAbility.SolarCreeper,Serial.Zero,aim,at,RecyclerRecoveryModel.CurrentTableSolarProvisional);
            Require(first.Deleted&&DruidRecyclerRecovery.Get(effect)!=null,"Vine replacement canceled independent recovery");
            var pending=DruidRecyclerRecovery.Get(effect);DruidRecyclerRecovery.Tick(pending.NextUtc.AddMilliseconds(45*40L),true,p.Serial.Value);
            int expected=Math.Min(SkillHealth.Maximum256(p)/256,life/256+(SkillHealth.Maximum256(p)/256)*4/100)*256;
            Require(SkillHealth.Balance256(p)==expected&&corpse.GetAmount(typeof(Gold))==1,"Replaced vine lost delayed healing/loot");
            Require(DruidCreeperCasting.Commit(p,Serial.Zero,aim,at,out var poison)&&second.Deleted,"Poison Creeper did not replace recycler family");
            var third=Cast(p,CombatAbility.CarrionVine,Serial.Zero,aim,at,RecyclerRecoveryModel.LegacySource);Require(poison.Deleted,"Recycler did not replace Poison Creeper");
            p.MoveToWorld(new(1420,1698,0),Map.Trammel);p.Z=p.Map.GetAverageZ(p.X,p.Y);Require(SpellHelper.IsTown(p.Location,p),"Town fixture");
            var town=Cast(p,CombatAbility.SolarCreeper,Serial.Zero,p.Location,at,RecyclerRecoveryModel.CurrentTableSolarProvisional);
            Require(third.Deleted&&SpellHelper.IsTown(town.Location,p),"InTown1summon refused");
        }
        finally{corpse.Delete();End(p);}
    }
    private static void Check()
    {
        Transaction(CombatAbility.CarrionVine);Transaction(CombatAbility.SolarCreeper);ReplacementAndTown();
        var p=RecyclerVineChecks.Fresh();var at=TimedEffects.Now.AddDays(2);RecyclerVineChecks.Train(p,"druid.raven");RecyclerVineChecks.Train(p,"druid.summonspiritwolf");
        var wolf=DruidSpiritWolfWorld.Create(p,new(p.X+5,p.Y,p.Z),at,152);var raven=DruidRavenBody.CreatePrepared(p,new(p.X+5,p.Y,p.Z),at,153,enforce:false);
        var first=Cast(p,CombatAbility.CarrionVine,Serial.Zero,new(p.X+3,p.Y,p.Z),at,RecyclerRecoveryModel.LegacySource);
        var second=Cast(p,CombatAbility.SolarCreeper,Serial.Zero,new(p.X+4,p.Y,p.Z),at,RecyclerRecoveryModel.CurrentTableSolarProvisional);
        Require(first.Deleted&&!wolf.Deleted&&!raven.Deleted,"Vine category replacement removed another summon family");
        PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,second.Serial.Value,wolf.Serial.Value,raven.Serial.Value,
            second.Prepared,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location)));
        Log.Information("Recycler prepared cast:10mana Carrion/14rank1Solar/15soft-rank2Solar,once-only payment,allocation/payment rollback,fractional mana,occupied and moving/deleted targets,town,three-vine replacement,independent recovery and wolf/Raven coexistence pass; learned SC/input and surface-window remain gated");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(DruidRecyclerBody)World.FindMobile((Serial)saved.Body);
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.SavedText==JsonSerializer.Serialize(saved.State)
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Paid recycler body/profile/mana lost across reload");
        Require(World.FindMobile((Serial)saved.Wolf) is{Alive:true,Deleted:false}&&World.FindMobile((Serial)saved.Raven) is{Alive:true,Deleted:false},"Coexisting summon lost on reload");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);var next=Cast(p,CombatAbility.SolarCreeper,Serial.Zero,new(p.X+4,p.Y,p.Z),TimedEffects.Now.AddDays(2),RecyclerRecoveryModel.CurrentTableSolarProvisional);
        Require(body.Deleted,"Post-load paid replacement retained old vine");PersistentMinions.Suspend(p);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Body=next.Serial.Value,State=next.Prepared,Mana=NecromancerEffects.ManaBalance256(p)}));
    }
    private static void Live(NetState network)
    {
        var p=RecyclerVineChecks.Fresh();network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();
        var corpse=RecyclerRecoveryChecks.Corpse(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);
        var body=Cast(p,CombatAbility.CarrionVine,Serial.Zero,corpse.Location,TimedEffects.Now,RecyclerRecoveryModel.LegacySource);long deadline=Core.TickCount+10000;
        void Inspect()
        {
            try
            {
                var s=body.Prepared.Controller;Require(network.Running&&Core.TickCount<deadline&&s.Fault==null,"Automatic cast/feed timeout/disconnect/fault");
                Require(NecromancerEffects.ManaBalance256(p)==mana-2560,"Prepared cast paid again during automatic controller");
                if(SkillHealth.Balance256(p)==life){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                int expected=Math.Min(SkillHealth.Maximum256(p)/256,life/256+(SkillHealth.Maximum256(p)/256)*4/100)*256;
                Require(s.Allocations==1&&SkillHealth.Balance256(p)==expected&&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,"Natural prepared cast/feed/heal value");
                Log.Information("recycler-casting-live-result: passed");corpse.Delete();End(p);
            }
            catch(Exception e){Log.Error(e,"recycler-casting-live-result: failed");corpse.Delete();End(p);}
        }
        Log.Information("recycler-casting-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);
    }
}
