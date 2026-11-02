// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class PlayerTeleportChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerTeleportChecks));
    private sealed record Saved(uint Owner,uint Gear,uint Pack,uint Belt,int Mana,int Life,int Rank,int Map,Point3D Point,int Generation,bool PendingStaged=false,uint PendingScroll=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-teleport-fixtures.json");
    public static void Configure()
    {
        Register("player-teleport-check",Check);Register("player-teleport-reload-check",()=>FixtureClock.NextLoop("player-teleport-reload-result",()=>FixtureNetwork.Connected("player-teleport-reload-result",Reload)));
        Register("player-teleport-save",StageSave);
        foreach(string mode in new[]{"left","right","quick"}){string label="player-teleport-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="PlayerTeleportFixture"&&prior?.Invoke(p)!=false;
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="PlayerTeleportFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Teleport approved landing/input,FP mana and original saved resources/scrolls",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6830);p.Name="PlayerTeleportFixture";
        EnchantCastChecks.Train(p,CombatAbility.Telekinesis,1);EnchantCastChecks.Train(p,CombatAbility.Teleport,1);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Teleport)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Teleport)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Teleport),"Approved Teleport left/right/quick bindings");return p;
    }
    private static Point3D Destination(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static bool Start(PlayerMobile p,Point3D aim,TeleportScroll scroll=null,int cell=-1)=>Teleportation.Begin(p,aim,Math.Max(Core.TickCount,ClassCombat.For(p).NextAction),scroll,cell);
    private static bool Finish(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static TeleportScroll Scroll(PlayerMobile p)
    {var scroll=ItemOrigins.Create(ItemOrigin.Crafted,()=>new TeleportScroll());p.Backpack.DropItem(scroll);return scroll;}
    private static void Check(){Goldens();Mana();Guards();Save();Log.Information("Player Teleport:255pinned rank costs,clamped FP mana/no revived remainder,approved18tile/noLOS/landing/timing,real gear/FCR,cancellation/once payment,original scroll/gear/wounds/resources and actual left/right/quick input");}
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Teleport);Require(row.SourceId==54&&row.Number("srvstfunc")==0&&row.Number("srvdofunc")==27&&row.Number("localdelay")==0,"Pinned Teleport dispatch");
        for(int rank=1;rank<=255;rank++)
        {var v=new SkillValues(row,k=>k==row.SourceKey?rank:0,_=>0,99);Require(v.Mana256==Math.Max(1,25-rank)*256&&v.Mana256==Teleportation.ManaCost(rank)*256,"Pinned Teleport rank mana");}
    }
    private static void Mana()
    {
        var p=Owner();try
        {
            int maximum=p.ManaMax*256;NecromancerEffects.SetMana256(p,maximum-128);p.Mana=p.ManaMax;
            Require(NecromancerEffects.RawManaBalance256(p)==maximum+128&&NecromancerEffects.ManaBalance256(p)==maximum,"Native full-mana remainder control");
            Require(Start(p,Destination(p))&&Finish(p),"Full-mana Teleport failed");
            Require(NecromancerEffects.ManaBalance256(p)==maximum-24*256,"Teleport revived a clamped fractional mana remainder");
            Ready(p);NecromancerEffects.SetMana256(p,24*256+17);Require(Start(p,Destination(p))&&Finish(p)&&NecromancerEffects.ManaBalance256(p)==17,"Teleport lost fractional remainder below maximum");
            Ready(p);var origin=p.Location;NecromancerEffects.SetMana256(p,24*256-1);Require(!Start(p,Destination(p))&&p.Location==origin&&NecromancerEffects.ManaBalance256(p)==24*256-1,"Insufficient Teleport paid/moved");
            Ready(p);NecromancerEffects.SetMana256(p,24*256);Require(Start(p,Destination(p)),"Mana-drain windup");origin=p.Location;NecromancerEffects.SetMana256(p,24*256-1);
            Require(!Finish(p)&&p.Location==origin&&NecromancerEffects.ManaBalance256(p)==24*256-1,"Lost release mana moved/paid");
            Ready(p);NecromancerEffects.SetMana256(p,17);var scroll=Scroll(p);Require(Start(p,Destination(p),scroll)&&Finish(p)&&scroll.Deleted&&NecromancerEffects.ManaBalance256(p)==17,"Scroll consumed mana/fraction or failed at zero whole mana");
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");int rank=Teleportation.Rank(p);Require(rank>1&&CombatTiming.For(p,CombatAbility.Teleport).Frames<13,"Real gear rank/FCR supplier");Ready(p);NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);
            Require(Start(p,Destination(p))&&Finish(p)&&NecromancerEffects.ManaBalance256(p)==mana-Teleportation.ManaCost(rank)*256,"Real gear Teleport price");p.Backpack.DropItem(orb);ClassGear.Refresh(p);
        }
        finally{FireBoltChecks.Clean(p);}
    }
    private static void Guards()
    {
        foreach(string mode in new[]{"cancel","rank","weapon","occupied","late-scroll","moved"})
        {
            var p=Owner();PlayerMobile blocker=null;Item gear=null;TeleportScroll scroll=null;
            try
            {
                int mana=NecromancerEffects.ManaBalance256(p);var origin=p.Location;var aim=Destination(p);
                if(mode=="late-scroll"){scroll=Scroll(p);}
                Require(Start(p,aim,scroll),"Teleport guard windup "+mode);
                switch(mode)
                {
                    case "cancel":ClassCombat.Stop(p);break;
                    case "rank":EnchantCastChecks.Train(p,CombatAbility.Teleport,2);break;
                    case "weapon":gear=PoisonDaggerChecks.Named(p,"Eschuta's temper");break;
                    case "occupied":blocker=new PlayerMobile{Player=true};blocker.MoveToWorld(aim,p.Map);break;
                    case "late-scroll":scroll.MoveToWorld(origin,p.Map);break;
                    case "moved":p.Location=new(p.X-1,p.Y,p.Z);break;
                }
                int releaseMana=NecromancerEffects.ManaBalance256(p);
                Require(!Finish(p)&&p.Location!=aim&&NecromancerEffects.ManaBalance256(p)==releaseMana&&scroll?.Deleted!=true&&!Finish(p),"Teleport guard spent/moved/replayed "+mode);
            }
            finally{blocker?.Delete();scroll?.Delete();gear?.Delete();FireBoltChecks.Clean(p);}
        }
        var caster=Owner();var wall=new Static(0x80);try
        {
            var aim=new Point3D(caster.X+18,caster.Y,caster.Z);wall.MoveToWorld(new(caster.X+2,caster.Y,caster.Z),caster.Map);
            Require(!caster.InLOS(aim)&&Teleportation.Landing(caster,aim)&&!Teleportation.Landing(caster,new(caster.X+19,caster.Y,caster.Z)),"Approved18tile/noLOS gates");
            int mana=NecromancerEffects.ManaBalance256(caster);Require(Start(caster,aim)&&!ClassCombat.ResolveAction(caster,ClassCombat.For(caster).EffectAt-1)&&NecromancerEffects.ManaBalance256(caster)==mana,"Early Teleport cost/move");
            Require(Finish(caster)&&caster.Location==aim&&NecromancerEffects.ManaBalance256(caster)==mana-24*256&&!Finish(caster),"Wall crossing/once payment");
            var route=PetOwnerRoutes.For(caster,TimedEffects.Now);Require(route.LastTeleport==PetOwnerRoutes.Cell(caster)&&route.TeleportUntilUtc>TimedEffects.Now,"Successful Teleport missed shared pet owner trail");
        }
        finally{wall.Delete();FireBoltChecks.Clean(caster);}
    }
    private static Saved Capture(PlayerMobile p,Item gear,TeleportScroll pack,TeleportScroll belt,int generation,int map,Point3D point)=>new(p.Serial.Value,gear.Serial.Value,pack.Serial.Value,belt.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),Teleportation.Rank(p),map,point,generation);
    private static void Save()
    {
        var p=Owner();var gear=PoisonDaggerChecks.Named(p,"Eschuta's temper");var pack=Scroll(p);var belt=Scroll(p);Require(PotionBelts.Load(p,3,belt),"Saved belt Teleport scroll");
        NecromancerEffects.SetMana256(p,100*256+17);SkillHealth.Damage(p,p,5*256+17);Require(Start(p,Destination(p)),"Saved pending Teleport");var saved=Capture(p,gear,pack,belt,0,p.Map.MapID,p.Location);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var gear=World.FindItem((Serial)saved.Gear);var pack=(TeleportScroll)World.FindItem((Serial)saved.Pack);var belt=(TeleportScroll)World.FindItem((Serial)saved.Belt);
        // Native Mobile.Deserialize parks offline players in Internal and
        // retains the original world map/point as LogoutMap/LogoutLocation.
        Require(saved.Generation<2&&saved.PendingStaged&&p.Map==Map.Internal&&p.LogoutMap.MapID==saved.Map&&p.LogoutLocation==saved.Point&&p.Location==saved.Point,"Original Teleport offline logout map/point changed");
        Require(gear?.Parent==p&&pack is{Deleted:false,Amount:1,Origin:ItemOrigin.Crafted}&&pack.Parent==p.Backpack&&belt is{Deleted:false,Amount:1,Origin:ItemOrigin.Crafted}&&PotionBelts.Entry(p,3)==belt
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life&&Teleportation.Rank(p)==saved.Rank,"Original Teleport resources/gear/scrolls changed");
        Require(ClassCombat.For(p).PendingAbility==CombatAbility.None&&ClassCombat.For(p).PendingTeleport==null,"Saved pending Teleport replayed");
        var bindings=SkillBindings.Instance.Get(p);Require(bindings.Left==CombatAbility.Teleport&&bindings.Right==CombatAbility.Teleport&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==CombatAbility.Teleport,"Saved Teleport bindings");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);var account=FixtureNetwork.Authenticate(network,p,"teleport-reload-");var aim=new Point3D(p.X+(saved.Generation==0?4:-4),p.Y,p.Z);Require(Start(p,aim),"Original saved Teleport did not restart a fresh cast");long deadline=Core.TickCount+4000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Original saved Teleport natural callback timeout");if(p.Location!=aim){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(NecromancerEffects.ManaBalance256(p)==saved.Mana-Teleportation.ManaCost(saved.Rank)*256&&SkillHealth.Balance256(p)==saved.Life&&!pack.Deleted&&!belt.Deleted&&PotionBelts.Entry(p,3)==belt,"Original Teleport natural payment changed wounds or consumed saved scrolls");
                var current=Capture(p,gear,pack,belt,saved.Generation+1,p.Map.MapID,p.Location);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(current));Log.Information("player-teleport-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"player-teleport-reload-result: failed");}
        }
        Observe();
    }
    private static void StageSave()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);Ready(p);var scroll=saved.Generation==1?(TeleportScroll)World.FindItem((Serial)saved.Pack):null;
        Require(Start(p,Destination(p),scroll)&&ClassCombat.For(p).PendingTeleport!=null&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SkillHealth.Balance256(p)==saved.Life,"Pending Teleport was not staged unpaid immediately before save");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{PendingStaged=true,PendingScroll=scroll?.Serial.Value??0}));World.Save();
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"teleport-input-");var aim=new Point3D(p.X+18,p.Y,p.Z);var origin=p.Location;var wall=new Static(0x80);wall.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
        int before=NecromancerEffects.ManaBalance256(p);Require(Start(p,Destination(p)),"Connected Teleport windup");network.Mobile=null;p.NetState=null;
        Require(!Finish(p)&&p.Location==origin&&NecromancerEffects.ManaBalance256(p)==before,"Connection change spent/moved pending Teleport");network.Mobile=p;p.NetState=network;Ready(p);
        Require(!p.InLOS(aim),"Teleport input wall does not block LOS");NecromancerEffects.SetMana256(p,100*256+17);int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;
        void FinishInput(){ClassCombat.Stop(p);wall.Delete();FireBoltChecks.Clean(p);account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Actual Teleport input/natural callback timeout");if(ClassCombat.For(p).PendingAbility==CombatAbility.Teleport&&!began){Require(p.Location==origin&&NecromancerEffects.ManaBalance256(p)==mana,"Wire Teleport paid/moved in windup");began=true;Log.Information("{Label}: windup",label);}
                if(p.Location==origin){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&p.Location==aim&&NecromancerEffects.ManaBalance256(p)==mana-24*256,"Actual Teleport input wrong landing/once payment/fraction");FinishInput();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){FinishInput();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)CombatAbility.Teleport,(int)DiabloClass.Sorceress,aim.X,aim.Y,aim.Z);Observe();});
    }
}
