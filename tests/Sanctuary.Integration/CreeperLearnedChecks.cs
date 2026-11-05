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

public static class CreeperLearnedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CreeperLearnedChecks));
    private sealed record Saved(uint Owner,uint Body,CreeperBodyState State,int Life,int Mana,CreeperGround[] Grounds,uint Pending,int PendingMana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"creeper-learned-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        Register("creeper-learned-check",Check);Register("creeper-learned-reload-check",Reload);
        Register("creeper-learned-right-check",()=>FixtureNetwork.Connected("creeper-learned-right-result",n=>Input(n,false)));
        Register("creeper-learned-quick-check",()=>FixtureNetwork.Connected("creeper-learned-quick-result",n=>Input(n,true)));
        ConsoleInputHandler.RegisterCommand("creeper-learned-live","Natural learned cast/AI/field/poison",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("creeper-learned-live-result",Live)));
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Creeper snapshot/input/save checks",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh()
    {
        var p=RecyclerVineChecks.Fresh();while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Gear strength");}
        NecromancerEffects.SetMana256(p,p.ManaMax*256-17);return p;
    }
    private static DruidCreeperBody[] Bodies(PlayerMobile p)=>(p.AllFollowers??[]).OfType<DruidCreeperBody>().Where(b=>!b.Deleted).ToArray();
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=Core.TickCount-1;}
    private static void End(PlayerMobile p){ClassCombat.Stop(p);DruidCreeperGround.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static void Hold(DruidCreeperBody b)
    {var s=b.Prepared;DateTime at=TimedEffects.Now.AddDays(3);b.RestorePrepared(s with{NextRecoveryUtc=at,Controller=s.Controller with{NextUtc=at}});}
    private static DruidCreeperBody Cast(PlayerMobile p,Mobile selected=null)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var old=(p.AllFollowers??[]).OfType<SkeletonGuardian>().Where(x=>DruidVines.Source(x.SourceMonsterId)).ToArray();
        Require(ClassCombat.Perform(p,CombatAbility.PoisonCreeper,selected,selected?.Location??new(p.X+2,p.Y,p.Z),Core.TickCount),"Public Creeper begin");var action=ClassCombat.For(p);
        Require(action.VineCast?.Ability==CombatAbility.PoisonCreeper&&action.VineCast.Recovery==null&&NecromancerEffects.ManaBalance256(p)==mana&&old.All(b=>!b.Deleted),"Early payment/replacement or wrong species policy");
        Require(!ClassCombat.ResolveAction(p,action.EffectAt-1)&&ClassCombat.ResolveAction(p,action.EffectAt),"Early/missing effect");var b=Bodies(p).Single();Hold(b);
        Require(old.All(x=>x.Deleted)&&NecromancerEffects.ManaBalance256(p)==mana-8*256&&!ClassCombat.ResolveAction(p,action.EffectAt),"Public once-only payment/category replacement");return b;
    }
    private static void Transactions()
    {
        var p=Fresh();var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        try
        {
            Require(ClassCombat.Available(p,CombatAbility.PoisonCreeper)&&SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonCreeper)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonCreeper),"Learned bindings");
            var b=Cast(p,target);Require(b.InRange(target,2),"Selected-target placement");SkillHealth.SetBalance256(b,1111);Ready(p);
            Require(ClassCombat.Perform(p,CombatAbility.PoisonCreeper,null,p.Location,Core.TickCount),"Low-release-mana setup");NecromancerEffects.SetMana256(p,2047);
            Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&Bodies(p).Single()==b&&SkillHealth.Balance256(b)==1111&&NecromancerEffects.ManaBalance256(p)==2047,"Failed release changed paid roster/resources");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonCreeper,null,p.Location,Core.TickCount),"Movement setup");p.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            Require(ClassCombat.For(p).VineCast==null&&Bodies(p).Single()==b,"Movement did not cancel unpaid cast");
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);PoisonDaggerChecks.Named(p,"Tarnhelm");
            Require(!ClassCombat.Available(p,CombatAbility.PoisonCreeper)&&!SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonCreeper),"Soft-only Creeper unlocked");
        }
        finally{target.Delete();End(p);}
    }
    private static void Check()
    {
        Transactions();var p=Fresh();var b=Cast(p);var birth=b.Prepared.Snapshot;var context=DruidCreeperPoison.Capture(birth);
        Require(context.Profile==DruidCreeperPoison.SnapshotProfile&&birth.Birth.Rank==1&&context.RabiesHardRank==0,"Approved summon-time context");
        PoisonDaggerChecks.Named(p,"Deaths's Web");Require(NamedElements.PoisonPierce(p)>0,"Real poison pierce gear control");
        ClassSkillChecks.Train(p,"druid.rabies");PoisonDaggerChecks.Named(p,"Tarnhelm");
        Require(SkillMechanics.Rank(p,CombatAbility.PoisonCreeper)>1&&DruidCreeperPoison.Capture(b.Prepared.Snapshot)==context,"Existing summon reread owner training/gear");
        var at=TimedEffects.Now.AddDays(2);long id=DruidCreeperGround.Create(b,1,DruidCreeperVines.Heads(1,(p.X+4)*2,p.Y*2),at);
        DruidCreeperGround.Tick(at.AddMilliseconds(12*40),true,p.Serial.Value);var ground=DruidCreeperGround.Get(id);
        Require(ground.PoisonContext==context&&ground.Fields.Mats.Length>0&&ground.Fields.Mats.All(m=>m.Poison.Damage==DruidCreeperPoison.Damage(1,context,100)),"Field recaptured owner gear/synergy");
        // Retain a real historical balanced-profile packet beside the new one.
        var historical=context with{Profile=DruidCreeperPoison.BalancedProfile};var oldMat=DruidCreeperPoison.Initialize(new(1,1,p.X*2+6,p.Y*2,125,125){Z=p.Z},historical,100);
        long oldId=DruidCreeperGround.Snapshot().NextId+1;var old=new CreeperGround(oldId,b.Serial.Value,p.Serial.Value,p.Map.MapID,1,new([],[oldMat],1),ground.NextUtc){PoisonContext=historical};
        var pool=DruidCreeperGround.Snapshot();DruidCreeperGround.Restore(pool with{Flights=[..pool.Flights,old],NextId=oldId});
        bool rejected=false;try{DruidCreeperPoison.Capture(birth with{Birth=birth.Birth with{Pierce=300}});}catch(NotSupportedException){rejected=true;}
        Require(rejected&&Bodies(p).Single()==b,"Unsupported sunder was invented or changed roster");
        var commander=new PlayerMobile{Player=true,Body=0x190};commander.AddItem(new Backpack());ClassCreation.Profiles.Assign(commander,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(commander,99);ClassSkillChecks.Train(commander,"barbarian.battlecommand");commander.MoveToWorld(p.Location,p.Map);
        BattleCommand.Apply(commander,b,1,TimedEffects.Now);Require(DruidCreeperStats.AttackRank(b)==2&&DruidCreeperPoison.Capture(b.Prepared.Snapshot)==context,"External Command changed base snapshot or failed innate rank");BattleCommand.Clear(b);commander.Delete();
        Require(DruidCreeperStats.AttackRank(b)==1,"Command did not expire independently");
        // A new summon gets the newer rank/synergy, without changing old fields.
        b=Cast(p);Require(b.Prepared.Snapshot.Birth.Rank==SkillMechanics.Rank(p,CombatAbility.PoisonCreeper)&&b.Prepared.Snapshot.RabiesHardRank==1&&DruidCreeperGround.Get(id).PoisonContext==context,"Resummon missed new ranks or rewrote existing fields");
        SkillHealth.SetBalance256(b,1111);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonCreeper)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonCreeper),"Saved bindings");
        var pending=Fresh();Ready(pending);Require(ClassCombat.Perform(pending,CombatAbility.PoisonCreeper,null,pending.Location,Core.TickCount+300000),"Saved unpaid windup setup");
        PersistentMinions.Suspend(p);p.Internalize();var saved=new Saved(p.Serial.Value,b.Serial.Value,b.Prepared,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),[DruidCreeperGround.Get(id),DruidCreeperGround.Get(oldId)],pending.Serial.Value,NecromancerEffects.ManaBalance256(pending));
        File.WriteAllText(Manifest,Json(saved));
        Log.Information("Learned Creeper:public paid SCcast/rollback/hard bindings;real poison gear and later training excluded from existing snapshots;resummon captures new ranks;historical/new field packets retained;external Command independent;unsupported sunder explicit");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra Creeper reload");var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(DruidCreeperBody)World.FindMobile((Serial)saved.Body);
        Require(b.ControlMaster==p&&Json(b.Prepared)==Json(saved.State)&&SkillHealth.Balance256(b)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original summon snapshot/wounds/mana changed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.PoisonCreeper&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.PoisonCreeper,"Saved binding changed");
        foreach(var ground in saved.Grounds){Require(Json(DruidCreeperGround.Get(ground.Id))==Json(ground),"Existing field/profile/packet/seed recaptured on load");}
        var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);Require(Bodies(pending).Length==0&&ClassCombat.For(pending).VineCast==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Unpaid windup replayed after load");
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        string label=quick?"creeper-learned-quick":"creeper-learned-right";var p=Fresh();Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.PoisonCreeper)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.PoisonCreeper),"Input bindings");bool began=false;long deadline=Core.TickCount+6000;
        void Poll()
        {
            try
            {
                Require(Core.TickCount<deadline,"Creeper input timeout");var bodies=Bodies(p);
                if(ClassCombat.For(p).VineCast?.Ability==CombatAbility.PoisonCreeper){Require(bodies.Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Input charged/allocated early");if(!began){began=true;Log.Information("{Label}: windup",label);}}
                if(bodies.Length==0){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);return;}
                Require(began&&bodies.Length==1&&bodies[0].Location==new Point3D(p.X+2,p.Y,p.Z)&&NecromancerEffects.ManaBalance256(p)==mana-8*256,"Actual input payment/placement");End(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.PoisonCreeper,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Poll();});
    }
    private static void Live(NetState network)
    {
        var p=Fresh();FixtureNetwork.Authenticate(network,p,"creeper-learned-");var target=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};target.SetHits(10000);target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();target.SetResistance(ResistanceType.Poison,0);TimedEffects.PreventHealing(target,TimedEffects.Now);
        int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);long deadline=Core.TickCount+12000;bool hurt=false;
        void Finish(){target.Delete();End(p);}
        void Poll()
        {
            try
            {
                var b=Bodies(p).SingleOrDefault();Require(Core.TickCount<deadline&&network.Running,$"Natural learned Creeper timeout:connected={network.Running},body={b?.Serial},map={b?.Map},controller={Json(b?.Prepared?.Controller)},poisoned={target.Poisoned},life={SkillHealth.Balance256(target)},initial={life},fields={Json(DruidCreeperGround.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value).Select(f=>new{f.Id,f.Rank,f.Fault,Heads=f.Fields.Heads.Length,Mats=f.Fields.Mats.Length,f.Fields.Contacts,f.Fields.LastVictim}))}");
                if(b!=null)
                {
                    Require(b.Prepared.Controller.Fault==null,"Natural Creeper controller fault: "+b.Prepared.Controller.Fault);
                    if(!hurt){int hp=SkillHealth.Balance256(b);Require(SkillHealth.Damage(target,b,256)>0&&SkillHealth.Balance256(b)<hp,"Active Creeper invulnerable");hurt=true;}
                    if(PoisonStates.Active(target,TimedEffects.Now)&&SkillHealth.Balance256(target)<life)
                    {
                        var ground=DruidCreeperGround.Get(b.Prepared.Controller.LastFieldId);Require(hurt&&ground?.PoisonContext.Profile==DruidCreeperPoison.SnapshotProfile&&b.Prepared.Controller.SkillEvents>0&&NecromancerEffects.ManaBalance256(p)==mana-8*256,"Natural public cast/AI/field context/payment");
                        Finish();Log.Information("creeper-learned-live-result: passed");return;
                    }
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){Finish();Log.Error(e,"creeper-learned-live-result: failed");}
        }
        try{Ready(p);Require(ClassCombat.Perform(p,CombatAbility.PoisonCreeper,null,new(p.X+2,p.Y,p.Z),Core.TickCount),"Natural learned start");Log.Information("creeper-learned-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);}catch{Finish();throw;}
    }
}
