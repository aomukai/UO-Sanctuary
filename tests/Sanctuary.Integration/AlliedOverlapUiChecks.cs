using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class AlliedOverlapUiChecks
{
    private static readonly ILogger _logger=LogFactory.GetLogger(typeof(AlliedOverlapUiChecks));
    private static PlayerMobile _p;
    private static SkeletonGuardian _pet;
    private static RestorationPotion _potion;
    private static Point3D _origin,_goal,_stationary;
    private static Action<PlayerMobile,Direction> _moved;
    private static int _passed,_life;
    private static bool _done;
    public static void Configure()
    {
        Register("setup",()=>
        {
            _p=(PlayerMobile)Accounts.GetAccount("combatui")[0]; ClassCombat.Stop(_p); TimedEffects.Clear(_p);
            _origin=new Point3D(5650,1100,0); _goal=new Point3D(5656,1100,0);
            _p.MoveToWorld(_origin,Map.Felucca); _p.RawStr=1000; _p.Stam=1; _p.Mana=0; ClassCombat.For(_p).Running=false;
            _pet=AlliedOverlapChecks.Pet(_p,new Point3D(5652,1100,0));
            _moved=ClassCombatHooks.Moved; ClassCombatHooks.Moved=(p,d)=> { _moved?.Invoke(p,d); if(p==_p && AlliedOverlap.Blocked(p)) { _passed++; } };
            _potion=new RestorationPotion(RestorationKind.Mana,1); _p.Backpack.DropItem(_potion); ImbuingChecks.Require(PotionBelts.Load(_p,0,_potion),"Belt setup failed");
            ClassCombat.SendState(_p,ClassCombat.For(_p));
        });
        Register("walked",()=>ImbuingChecks.Require(_passed>0 && _p.Location==_goal && !AlliedOverlap.Blocked(_p),$"Client failed allied transit: passed={_passed} at={_p.Location}, goal={_goal}"));
        Register("overlap",()=> { _pet.MoveToWorld(new Point3D(_p.X+1,_p.Y,_p.Z),_p.Map); _p.MoveToWorld(_pet.Location,_pet.Map); _stationary=_p.Location;
            ImbuingChecks.Require(AlliedOverlap.Blocked(_p) && AlliedOverlap.Allowed(_pet),"Passing owner did not preserve pet holder"); });
        Register("owner-holds",()=> { _p.MoveToWorld(_goal,_p.Map); _pet.MoveToWorld(_p.Location,_p.Map); _stationary=_p.Location; _life=_pet.Hits;
            ImbuingChecks.Require(AlliedOverlap.Allowed(_p) && AlliedOverlap.Blocked(_pet),"Passing pet disabled owner holder"); });
        Register("blocked",()=>ImbuingChecks.Require(!_potion.Deleted && PotionBelts.Entry(_p,0)==_potion  && _p.Location==_stationary,"Overlapping client drank a potion"));
        Register("regrouped",()=>ImbuingChecks.Require(!AlliedOverlap.Blocked(_p) && !AlliedOverlap.Blocked(_pet) && _pet.Hits==_life && _pet.GetMaster()==_p && _p.Location==_stationary,"Actual five-second regroup failed"));
        Register("opened",()=> { ImbuingChecks.Require(_potion.Deleted && PotionBelts.Entry(_p,0)==null,"Incumbent owner could not use belt"); _done=true; });
        Register("final",()=> { ImbuingChecks.Require(_done,"Ally UI flow incomplete"); ClassCombatHooks.Moved=_moved; _pet?.Delete(); });
    }
    private static void Register(string suffix,Action action)
    {
        string name="allied-ui-"+suffix;
        ConsoleInputHandler.RegisterCommand(name,"Actual client ally path, overlap action block and regroup",_=>Core.LoopContext.Post(()=>
        { try { action(); File.WriteAllText(Environment.GetEnvironmentVariable("SANCTUARY_TEST_UI_STATE"),JsonSerializer.Serialize(new { passed=_passed, x=_p?.X,y=_p?.Y })); _logger.Information("{Command}: passed",name); } catch(Exception e) { _logger.Error(e,"{Command}: failed",name); } }));
    }
}
