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
namespace Sanctuary.Integration;

public static class IronGolemCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(IronGolemCastChecks));
    private sealed record Saved(uint Owner,uint Body,uint Original,uint PendingOwner,uint PendingItem,int Mana,int PendingMana,string Gear,int Life,ulong Seed);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"iron-golem-cast-fixtures.json");
    private static Saved LoadedMana;
    public static void Configure()
    {
        EventSink.WorldSave+=Snapshot;
        EventSink.WorldLoad+=ObserveLoad;
        Register("iron-golem-cast-check",Check);Register("iron-golem-cast-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;Register(captured?"iron-golem-quick-check":"iron-golem-right-check",()=>FixtureNetwork.Connected(captured?"iron-golem-quick-result":"iron-golem-right-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned item-target Iron Golem",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var r);foreach(var prior in r.Prerequisites){Train(p,prior);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="Learned Iron caster [fixture]",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,47);Train(p,"necromancer.irongolem");
        FixtureSpace.Corridor(p,6100,24,2100);
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Persisted casting Energy allocation");}Mana(p);
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.IronGolem)&&SkillBindings.Instance.Assign(p,false,CombatAbility.IronGolem)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IronGolem),"Right/quick binding contract");return p;
    }
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static Item Original(PlayerMobile p)
    {var i=new Broadsword();ClassGear.Profiles.Ensure(i);i.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return i;}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=0;}
    private static bool Begin(PlayerMobile p,Item i)
    {Ready(p);return SkillBindingNetwork.Use(p,DiabloClass.Necromancer,7,CombatAbility.IronGolem,i.Serial,new(0,0,0));}
    private static bool Finish(PlayerMobile p)
    {long at=ClassCombat.For(p).EffectAt;bool yes=ClassCombat.ResolveAction(p,at);int mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana,"Cast replayed");return yes;}
    private static IronGolemBody Cast(PlayerMobile p,Item i)
    {
        int before=NecromancerEffects.ManaBalance256(p);Require(Begin(p,i),"Item serial/quick start failed");var s=ClassCombat.For(p);
        Require(s.PendingTarget==i.Serial&&s.PendingPoint==i.Location&&s.Animation==16&&NecromancerEffects.ManaBalance256(p)==before
            &&i.Parent==null&&!ClassCombat.ResolveAction(p,s.EffectAt-1),"Authoritative item point/unpaid windup");
        Require(Finish(p)&&NecromancerEffects.ManaBalance256(p)==before-35*256,"Exactly35mana successful release");
        var body=NecromancerGolems.For(p).OfType<IronGolemBody>().Single();
        Require(body.Prepared.RosterMember&&body.Prepared.Controller!=null&&i.Parent==body&&!i.Visible&&!i.Movable
            &&IronGolemItems.State(body).Item==i.Serial.Value,"Learned world actor/original/roster binding");return body;
    }
    private static void Drop(PlayerMobile p)
    {foreach(var pet in(p.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}p.Delete();}
    private static void Transactions()
    {
        var p=Owner();var spawn=IronGolemCasting.Spawn;
        try
        {
            var first=Cast(p,Original(p));long order=BattleCommand.PetOrder(first);
            foreach(int mode in new[]{0,1,2})
            {
                Mana(p);int mana=NecromancerEffects.ManaBalance256(p);var item=Original(p);IronGolemBody allocated=null;
                IronGolemCasting.Spawn=(owner,original,point,at,seed)=>mode switch
                {
                    0=>null,
                    1=>throw new ArgumentException("Fixture allocation refusal"),
                    _=>Drain(owner,original,point,at,seed)
                };
                IronGolemBody Drain(PlayerMobile owner,Item original,Point3D point,DateTime at,ulong seed)
                {allocated=spawn(owner,original,point,at,seed);NecromancerEffects.SetMana256(owner,0);return allocated;}
                Require(Begin(p,item)&&!Finish(p)&&!first.Deleted&&BattleCommand.PetOrder(first)==order
                    &&NecromancerGolems.For(p).Single()==first&&item.Parent==null&&item.Map==p.Map&&item.Visible&&item.Movable
                    &&NecromancerEffects.ManaBalance256(p)==(mode==2?0:mana)&&(allocated==null||allocated.Deleted),"Allocation/payment rollback lost original/old golem: "+mode);
                item.Delete();
            }
        }
        finally{IronGolemCasting.Spawn=spawn;Drop(p);}
    }
    private static void Cancellations()
    {
        var p=Owner();
        try
        {
            var old=Cast(p,Original(p));
            foreach(int mode in new[]{0,1,2})
            {
                Mana(p);var i=Original(p);int mana=NecromancerEffects.ManaBalance256(p);Require(Begin(p,i),"Cancellation start");var point=p.Location;
                if(mode==0){p.AddToBackpack(i);}else if(mode==1){p.MoveToWorld(new(point.X,point.Y+1,point.Z),p.Map);}else{NecromancerEffects.SetMana256(p,0);}
                Require(!Finish(p)&&!old.Deleted&&IronGolemItems.State(old)!=null&&i.Parent!=old
                    &&NecromancerEffects.ManaBalance256(p)==(mode==2?0:mana),"Moved item/caster/lost mana consumed original or replaced old");
                p.MoveToWorld(point,p.Map);i.Delete();
            }
            Mana(p);var unsupported=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Goblin Toe").Id,99,new Random(12),true);
            unsupported.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);int before=NecromancerEffects.ManaBalance256(p);
            Require(!Begin(p,unsupported)&&unsupported.Parent==null&&!old.Deleted&&NecromancerEffects.ManaBalance256(p)==before,"Unsupported proc entered learned windup");unsupported.Delete();
        }
        finally{Drop(p);}
    }
    private static void Check()
    {
        Require(NecromancerCombat.Implemented(CombatAbility.IronGolem)&&ClassAbilities.ItemTarget(CombatAbility.IronGolem),"Learned handler/item target registration missing");
        Transactions();Cancellations();var p=Owner();var first=Original(p);var old=Cast(p,first);Mana(p);var second=Original(p);var active=Cast(p,second);
        Require(old.Deleted&&first.Deleted&&NecromancerGolems.For(p).Length==1,"Shared golem replacement failed");
        SkillHealth.SetBalance256(active,SkillHealth.Maximum256(active)/2+17);NecromancerEffects.SetMana256(p,NecromancerEffects.ManaBalance256(p)+17);
        var pending=Owner();var pendingItem=Original(pending);Require(Begin(pending,pendingItem),"Pending save start");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        var saved=new Saved(p.Serial.Value,active.Serial.Value,second.Serial.Value,pending.Serial.Value,pendingItem.Serial.Value,
            NecromancerEffects.ManaBalance256(p),NecromancerEffects.ManaBalance256(pending),JsonSerializer.Serialize(ClassGear.State(second)),SkillHealth.Balance256(active),NpcEquipmentRandom.State(active).Seed);
        PersistentMinions.Suspend(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Learned Iron cast:authoritative item/quick SC windup,35mana once,original/cap1 replacement,3allocation/payment rollback and3movement/resource cancellation controls,unsupported proc preflight and saved active/unpaid pending fixtures pass");
    }
    private static void Snapshot()
    {
        if(!File.Exists(Manifest)){return;}
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        var pending=World.FindMobile((Serial)s.PendingOwner) as PlayerMobile;
        if(p is not{Deleted:false}||pending is not{Deleted:false}){return;}
        // Native mana regenerates even while parked. Observe the actual save
        // boundary, preserving its fractional remainder instead of freezing it
        // or comparing against the much earlier fixture-construction time.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Mana=NecromancerEffects.ManaBalance256(p),PendingMana=NecromancerEffects.ManaBalance256(pending)}));
    }
    private static void ObserveLoad()
    {
        if(!File.Exists(Manifest)){return;}
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;
        var pending=World.FindMobile((Serial)s.PendingOwner) as PlayerMobile;
        if(p!=null&&pending!=null){LoadedMana=s with{Mana=NecromancerEffects.ManaBalance256(p),PendingMana=NecromancerEffects.ManaBalance256(pending)};}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var body=(IronGolemBody)World.FindMobile((Serial)s.Body);
        var original=World.FindItem((Serial)s.Original);var pending=(PlayerMobile)World.FindMobile((Serial)s.PendingOwner);var item=World.FindItem((Serial)s.PendingItem);
        Require(body is{Alive:true,Deleted:false}&&body.ControlMaster==p&&body.Map==Map.Internal&&body.Prepared.RosterMember&&original.Parent==body
            &&JsonSerializer.Serialize(ClassGear.State(original))==s.Gear&&SkillHealth.Balance256(body)==s.Life&&NpcEquipmentRandom.State(body).Seed==s.Seed
            &&LoadedMana?.Mana==s.Mana&&NecromancerEffects.ManaBalance256(p)>=s.Mana&&NecromancerEffects.ManaBalance256(p)<=p.ManaMax*256,
            "Learned reload:body="+body?.Serial+", parked="+(body?.Map==Map.Internal)
            +", life="+SkillHealth.Balance256(body)+"/"+s.Life+", mana="+NecromancerEffects.ManaBalance256(p)+"/"+s.Mana
            +", seed="+NpcEquipmentRandom.State(body)?.Seed+"/"+s.Seed);
        Require(ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerGolems.For(pending).Length==0&&item.Parent==null
            &&item.Visible&&item.Movable&&LoadedMana?.PendingMana==s.PendingMana&&NecromancerEffects.ManaBalance256(pending)>=s.PendingMana,
            "Unpaid pending cast replayed on reload");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.IronGolem&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==CombatAbility.IronGolem,"Learned bindings did not persist");
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"iron-golem-quick":"iron-golem-right";var p=Owner();var item=Original(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+6000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Item-target input timeout");var s=ClassCombat.For(p);
                if(s.PendingAbility==CombatAbility.IronGolem&&!began)
                {Require(item.Parent==null&&NecromancerEffects.ManaBalance256(p)==mana&&s.PendingTarget==item.Serial,"Input windup paid/lost item");began=true;Log.Information("{Label}: windup",label);}
                var golem=NecromancerGolems.For(p).OfType<IronGolemBody>().SingleOrDefault();
                if(golem==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&item.Parent==golem&&golem.Prepared.RosterMember&&NecromancerEffects.ManaBalance256(p)==mana-35*256,"Actual right/quick input payment or original mismatch");
                Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){item.Delete();Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.IronGolem,(int)DiabloClass.Necromancer,item.Serial.Value,item.X,item.Y,item.Z);Inspect();});
    }
}
