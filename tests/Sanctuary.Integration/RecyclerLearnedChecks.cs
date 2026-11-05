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

public static class RecyclerLearnedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(RecyclerLearnedChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.CarrionVine,CombatAbility.SolarCreeper];
    private sealed record Actor(uint Owner,uint Body,CombatAbility Ability,RecyclerBodyState State,int Life,int Mana,int OwnerLife,RecyclerRecovery Recovery,int? FinalLife=null,int? FinalMana=null);
    private sealed record Saved(Actor[] Actors,uint Pending,int PendingMana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"recycler-learned-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    public static void Configure()
    {
        Register("recycler-learned-check",Check);Register("recycler-learned-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("recycler-learned-live","Natural learned casting/feeding/recovery for both vines",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("recycler-learned-live-result",Live)));
        foreach(var a in Skills)foreach(bool quick in new[]{false,true})
        {var ability=a;bool q=quick;string label=Label(a,quick);Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,ability,q)));}
    }
    private static string Label(CombatAbility a,bool quick)=>(a==CombatAbility.CarrionVine?"carrion":"solar")+(quick?"-quick":"-right");
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned recycler vines and approved policy",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh()
    {
        var p=RecyclerVineChecks.Fresh();while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Vine gear Strength");}
        NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static PlayerMobile Commander(PlayerMobile owner)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="Recycler Command supplier"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"barbarian.battlecommand");p.MoveToWorld(owner.Location,owner.Map);return p;
    }
    private static DruidRecyclerBody[] Bodies(PlayerMobile p)=>(p.AllFollowers??[]).OfType<DruidRecyclerBody>().Where(b=>!b.Deleted).ToArray();
    private static void End(PlayerMobile p){ClassCombat.Stop(p);DruidCreeperGround.Cancel(p);DruidRecyclerRecovery.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=Core.TickCount-1;}
    private static void Hold(DruidRecyclerBody b)
    {var s=b.Prepared;DateTime at=TimedEffects.Now.AddDays(3);b.RestorePrepared(s with{NextRecoveryUtc=at,Controller=s.Controller with{NextUtc=at}});}
    private static DruidRecyclerBody Cast(PlayerMobile p,CombatAbility a,Mobile selected=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var old=Bodies(p);var aim=new Point3D(p.X+2,p.Y,p.Z);
        Require(ClassCombat.Perform(p,a,selected,selected?.Location??aim,Core.TickCount),"Public vine cast start");var s=ClassCombat.For(p);
        Require(s.VineCast?.Ability==a&&NecromancerEffects.ManaBalance256(p)==mana&&old.All(b=>!b.Deleted),"Public windup changed resources/roster");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1),"Early public vine release");
        Require(ClassCombat.ResolveAction(p,s.EffectAt),"Public vine cast release");var b=Bodies(p).Single();Hold(b);
        Require(old.All(x=>x.Deleted)&&b.VineAbility==a&&s.VineCast==null&&s.PendingAbility==CombatAbility.None
            &&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,a).Mana256,"Public once-only payment/replacement/species");
        Require(b.Prepared.Controller.RecoveryModel==(a==CombatAbility.SolarCreeper?RecyclerRecoveryModel.SanctuaryApprovedSolar:RecyclerRecoveryModel.LegacySource),"Public recovery policy");return b;
    }
    private static void Models()
    {
        int count=0;
        foreach(int rank in new[]{1,2,7,20,21,60,255})foreach(int whole in new[]{0,1,9,95,99,100})foreach(int fraction in new[]{0,17,255})foreach(int maxFraction in new[]{0,211})
        {
            int current=whole*256+fraction,maximum=100*256+maxFraction;
            var actual=DruidRecyclerRecovery.Gain(CombatAbility.SolarCreeper,rank,45,current,maximum,true,true,RecyclerRecoveryModel.SanctuaryApprovedSolar);
            bool changes=whole<100;int expected=changes?Math.Min(100,whole+rank+3)*256:current;
            Require(actual.Changed==changes&&actual.After256==expected,"Approved table/whole-point Solar recovery");count++;
        }
        Require(count==252,"Incomplete approved Solar arithmetic controls");
        Require(DruidCombat.Implemented(CombatAbility.PoisonCreeper)&&Skills.All(DruidCombat.Implemented),"Wrong learned vine mask");
    }
    private static void Transactions()
    {
        var p=Fresh();var enemy=PoisonDaggerChecks.Target(p);enemy.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            foreach(var a in Skills)
            {
                Require(ClassCombat.Available(p,a)&&SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Learned right/quick availability");
                NecromancerEffects.SetMana256(p,p.ManaMax*256-17);var body=Cast(p,a,enemy);Require(body.InRange(enemy,2),"Selected-target public placement");
                SkillHealth.SetBalance256(body,1111);int mana=NecromancerEffects.ManaBalance256(p);Ready(p);
                Require(ClassCombat.Perform(p,a,null,p.Location,Core.TickCount),"Lost-mana start");NecromancerEffects.SetMana256(p,1);
                Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&Bodies(p).Single()==body&&SkillHealth.Balance256(body)==1111&&NecromancerEffects.ManaBalance256(p)==1,"Rejected release replaced/healed/charged old vine");
                NecromancerEffects.SetMana256(p,mana);Ready(p);Require(ClassCombat.Perform(p,a,null,p.Location,Core.TickCount),"Stop start");ClassCombat.Stop(p);
                Require(!ClassCombat.ResolveAction(p,Core.TickCount+1000)&&Bodies(p).Single()==body&&NecromancerEffects.ManaBalance256(p)==mana,"Stopped cast committed");
                Ready(p);Require(ClassCombat.Perform(p,a,null,p.Location,Core.TickCount),"Move start");p.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
                Require(ClassCombat.For(p).VineCast==null&&!ClassCombat.ResolveAction(p,Core.TickCount+1000)&&Bodies(p).Single()==body,"Movement retained public vine cast");
                Ready(p);Require(ClassCombat.Perform(p,a,null,p.Location,Core.TickCount),"Cross-ability release setup");
                var other=a==CombatAbility.CarrionVine?CombatAbility.SolarCreeper:CombatAbility.CarrionVine;
                Require(!DruidCombat.Release(p,other,ClassCombat.For(p).EffectAt)&&ClassCombat.For(p).VineCast?.Ability==a,"Wrong ability released another vine");ClassCombat.Stop(p);
                var glove=PoisonDaggerChecks.Named(p,"Magefist");Ready(p);Require(ClassCombat.Perform(p,a,null,p.Location,Core.TickCount)&&ClassCombat.For(p).Timing==new ActionTiming(16,9),"Public FCR gear cast clock");ClassCombat.Stop(p);glove.Delete();ClassGear.Refresh(p);
            }
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);PoisonDaggerChecks.Named(p,"Tarnhelm");
            Require(Skills.All(a=>!ClassCombat.Available(p,a)&&!SkillBindings.Instance.Assign(p,false,a)),"Soft-only vine unlocked without hard point");
        }
        finally{enemy.Delete();End(p);}
    }
    private static void Vulnerability()
    {
        var p=Fresh();var enemy=PoisonDaggerChecks.Target(p);var poison=DruidCreeperBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now.AddDays(2));
        try
        {
            int hp=SkillHealth.Balance256(poison);Require(poison.CanBeDamaged()&&SkillHealth.Damage(enemy,poison,256)>0&&SkillHealth.Balance256(poison)<hp,"Always-vulnerable Poison Creeper backend");poison.Delete();
            foreach(var a in Skills)
            {
                NecromancerEffects.SetMana256(p,p.ManaMax*256);var b=Cast(p,a);int before=SkillHealth.Balance256(b);
                Require(b.CanBeDamaged()&&SkillHealth.Damage(enemy,b,256)>0&&SkillHealth.Balance256(b)<before,"Always-vulnerable learned recycler");
            }
        }
        finally{poison.Delete();enemy.Delete();End(p);}
    }
    private static void Check()
    {
        Models();Transactions();Vulnerability();var saved=new Actor[2];
        for(int i=0;i<Skills.Length;i++)
        {
            var p=Fresh();var a=Skills[i];var b=Cast(p,a);Require(SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Saved bindings");
            var commander=Commander(p);BattleCommand.Apply(commander,b,1,TimedEffects.Now);Require(DruidRecyclerRecovery.Rank(b)==2,"Live innate Battle Command rank");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)/2+17);NecromancerEffects.SetMana256(p,p.ManaMax*128+17);
            var corpse=RecyclerRecoveryChecks.Corpse(p);var result=DruidRecyclerRecovery.Commit(b,corpse,TimedEffects.Now.AddDays(2),b.Prepared.Controller.RecoveryModel);
            Require(result is{Reserved:true,Allocated:true}&&corpse.GetAmount(typeof(Gold))==1,"Saved actual corpse reservation/loot");var effect=DruidRecyclerRecovery.Get(result.Effect);
            Require(effect.Rank==2,"Delayed recovery missed live NPC skill bonus");BattleCommand.Clear(b);commander.Delete();Require(DruidRecyclerRecovery.Rank(b)==1&&DruidRecyclerRecovery.Get(effect.Id).Rank==2,"Existing recovery changed after buff expiry");
            SkillHealth.SetBalance256(b,1111);Hold(b);PersistentMinions.Suspend(p);p.Internalize();
            saved[i]=new(p.Serial.Value,b.Serial.Value,a,b.Prepared,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),effect);
        }
        var pending=Fresh();Require(ClassCombat.Perform(pending,CombatAbility.CarrionVine,null,pending.Location,Core.TickCount+300000),"Public unpaid staged cast");
        File.WriteAllText(Manifest,Json(new Saved(saved,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending))));
        Log.Information("Learned recycler vines:252approved Solar whole-point controls;public SC/FCR,target/ground,hard-point/binding,once-only payment/replacement,cancel/move/low release mana,all3always-vulnerable bodies,live innate Command/captured delayed rank and original saved actors/resources/bindings/pending recovery pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Extra recycler reload");
        foreach(var row in s.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var b=(DruidRecyclerBody)World.FindMobile((Serial)row.Body);
            Require(b.ControlMaster==p&&Json(b.Prepared)==Json(row.State)&&SkillHealth.Balance256(b)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana&&SkillHealth.Balance256(p)==row.OwnerLife,"Saved original recycler state/wounds/resources changed");
            Require(SkillBindings.Instance.Get(p).Right==row.Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==row.Ability&&ClassCombat.For(p).VineCast==null&&DruidRecyclerRecovery.Get(row.Recovery.Id)==row.Recovery,"Saved binding/delayed rank or pending cast replay");
            if(s.Generation==1)
            {
                var e=row.Recovery;DruidRecyclerRecovery.Tick(e.NextUtc,true,p.Serial.Value);Require(DruidRecyclerRecovery.Get(e.Id)is{Remaining:46,Triggered:false},"Reload replayed overdue queue immediately");
                var next=DruidRecyclerRecovery.Get(e.Id);DruidRecyclerRecovery.Tick(next.NextUtc.AddMilliseconds(44*40L),true,p.Serial.Value);
                int current=row.Ability==CombatAbility.CarrionVine?row.OwnerLife:row.Mana;
                int max=row.Ability==CombatAbility.CarrionVine?SkillHealth.Maximum256(p):p.ManaMax*256;
                int expected=Math.Min(max/256,current/256+(max/256)*5/100)*256;
                Require(DruidRecyclerRecovery.Get(e.Id)is{Triggered:true,Remaining:1}&&(row.Ability==CombatAbility.CarrionVine?SkillHealth.Balance256(p):NecromancerEffects.ManaBalance256(p))==expected,"Original saved recovery did not fire once with captured NPCrank2");
                int i=Array.IndexOf(s.Actors,row);s.Actors[i]=row with{FinalLife=SkillHealth.Balance256(p),FinalMana=NecromancerEffects.ManaBalance256(p)};
            }
        }
        var pending=(PlayerMobile)World.FindMobile((Serial)s.Pending);Require(Bodies(pending).Length==0&&ClassCombat.For(pending).VineCast==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==s.PendingMana,"Unpaid public windup replayed after load");
        File.WriteAllText(Manifest,Json(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,CombatAbility ability,bool quick)
    {
        string label=Label(ability,quick);var p=Fresh();Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Public input bindings");bool began=false;long deadline=Core.TickCount+6000;
        void Poll()
        {
            try
            {
                Require(Core.TickCount<deadline,"Recycler input timeout");var list=Bodies(p);
                if(ClassCombat.For(p).VineCast?.Ability==ability)
                {Require(list.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input charged/allocated before effect");if(!began){began=true;Log.Information("{Label}: windup",label);}}
                if(list.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);return;}
                Require(began&&list.Length==1&&list[0].VineAbility==ability&&list[0].Location==new Point3D(p.X+2,p.Y,p.Z)&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,ability).Mana256,"Actual input payment/placement/species");End(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Poll();});
    }
    private static void Live(NetState network)
    {
        var p=Fresh();var commander=Commander(p);FixtureNetwork.Authenticate(network,p,"recycler-learned");int index=0,life=0,mana=0;DruidRecyclerBody body=null;Corpse corpse=null;RecyclerRecovery effect=null;bool hurt=false;long deadline=0;
        void EndAll(){corpse?.Delete();commander.Delete();End(p);}
        void Start()
        {
            body=null;effect=null;hurt=false;deadline=Core.TickCount+12000;var a=Skills[index];SkillHealth.SetBalance256(p,a==CombatAbility.CarrionVine?SkillHealth.Maximum256(p)/2+17:SkillHealth.Maximum256(p));NecromancerEffects.SetMana256(p,a==CombatAbility.SolarCreeper?p.ManaMax*128+17:p.ManaMax*256);
            corpse?.Delete();corpse=RecyclerRecoveryChecks.Corpse(p);Ready(p);Require(ClassCombat.Perform(p,a,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Natural public cast");
        }
        void Poll()
        {
            try
            {
                Require(Core.TickCount<deadline&&network.Running,"Natural learned vine timeout/disconnect");
                if(body==null&&Bodies(p).SingleOrDefault()is{} found&&found.VineAbility==Skills[index])
                {body=found;BattleCommand.Apply(commander,body,1,TimedEffects.Now);Log.Information("Recycler live initial Command:actor={Actor},alive={Alive},deleted={Deleted},bondedDead={BondedDead},state={State},controller={Controller}",body.Serial.Value,body.Alive,body.Deleted,body.IsDeadBondedPet,Json(BattleCommand.State(body)),Json(body.Prepared.Controller));life=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);}
                if(body?.Prepared.Controller is{Fault:not null} failed){throw new InvalidOperationException(failed.Fault);}
                if(body!=null&&effect==null&&body.Prepared.Controller.LastRecovery>0)
                {effect=DruidRecyclerRecovery.Get(body.Prepared.Controller.LastRecovery);Require(effect is{Rank:2}&&effect.Actor==body.Serial.Value&&effect.Owner==p.Serial.Value,$"Natural live innate rank/delayed creator:effect={Json(effect)},actor={body.Serial.Value},owner={p.Serial.Value},birth={body.Prepared.Snapshot.Birth.RecyclerRank},bonus={BattleCommand.Bonus(body)},mode={Json(body.Prepared.Controller)}");}
                if(body!=null&&effect!=null&&!hurt)
                {var enemy=PoisonDaggerChecks.Target(p);try{int hp=SkillHealth.Balance256(body);Require(body.CanBeDamaged()&&SkillHealth.Damage(enemy,body,256)>0&&SkillHealth.Balance256(body)<hp,"Feeding vine was protected");hurt=true;}finally{enemy.Delete();}}
                int current=Skills[index]==CombatAbility.CarrionVine?SkillHealth.Balance256(p):NecromancerEffects.ManaBalance256(p);
                int before=Skills[index]==CombatAbility.CarrionVine?life:mana;
                if(effect!=null&&current!=before)
                {
                    int max=Skills[index]==CombatAbility.CarrionVine?SkillHealth.Maximum256(p):p.ManaMax*256;
                    Require(current==Math.Min(max/256,before/256+(max/256)*5/100)*256&&hurt&&CorpseSkillRules.NoSelect(corpse)&&corpse.GetAmount(typeof(Gold))==1,"Natural delayed whole-point gain/loot/vulnerability");
                    if(++index==Skills.Length){EndAll();Log.Information("recycler-learned-live-result: passed");return;}Start();
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){EndAll();Log.Error(e,"recycler-learned-live-result: failed");}
        }
        try{Start();Log.Information("recycler-learned-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);}catch{EndAll();throw;}
    }
}
