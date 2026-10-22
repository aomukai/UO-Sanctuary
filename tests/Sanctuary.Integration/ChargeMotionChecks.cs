// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class ChargeMotionChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(ChargeMotionChecks));
    private static PlayerMobile _owner;
    private static Point3D _origin;
    private static int _stamina;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"charge-motion-network.json");
    public static void Configure()
    {
        Register("charge-motion-check",Geometry);
        Register("charge-motion-network-setup",NetworkSetup);
        Register("charge-motion-network-step",NetworkStep);
        Register("charge-motion-network-finish",NetworkFinish);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Charge native player movement and connected owning-socket checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static PlayerMobile Player()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.Name="ChargeMotionFixture";
        for(int x=2200;x<=2500;x+=50)for(int y=1000;y<=1300;y+=50)
        {
            Point3D point;
            try{point=ClassSkillChecks.Corridor(owner,x,y);}catch(InvalidOperationException){continue;}
            bool clear=true;
            foreach(var actor in owner.Map.GetMobilesInRange<Mobile>(point,12)){if(actor!=owner){clear=false;break;}}
            if(clear && Server.Spells.SpellHelper.CheckTown(point,owner))
            {owner.MoveToWorld(point,owner.Map);return owner;}
        }
        owner.Delete();throw new InvalidOperationException("No independent Charge movement fixture area");
    }
    private static void Geometry()
    {
        var owner=Player();var map=owner.Map;var origin=owner.Location;
        try
        {
            int stamina=owner.Stam;int accepted=0;var moved=ClassCombatHooks.Moved;
            try
            {
                ClassCombatHooks.Moved=(actor,direction)=>{moved?.Invoke(actor,direction);if(actor==owner){accepted++;}};
                owner.Direction=Direction.North;
                Require(ChargeMotion.TryStep(owner,Direction.East) && owner.X==origin.X+1 && accepted==1,"Native charge step only turned or skipped movement events");
                Require(owner.Stam==stamina && !ChargeMotion.Active(owner),"Owned movement drained stamina or leaked scope");
            }
            finally{ClassCombatHooks.Moved=moved;}
            owner.MoveToWorld(origin,map);
            var wall=new Blocker();wall.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),map);
            try{Require(!ChargeMotion.TryStep(owner,Direction.East) && owner.Location==origin,"Charge passed an impassable native item");}finally{wall.Delete();}
            var enemy=new Ogre{NoKillAwards=true,Frozen=true,CantWalk=true};enemy.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),map);enemy.AIObject?.AITimer.Stop();
            try{Require(!ChargeMotion.TryStep(owner,Direction.East) && owner.Location==origin && owner.Stam==stamina,"Charge pushed through a hostile or spent push stamina");}finally{enemy.Delete();}
            var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(origin.X+1,origin.Y,origin.Z),map);
            try{Require(ChargeMotion.TryStep(owner,Direction.East) && owner.Location==ally.Location,"Charge adapter removed approved allied pass-through");}finally{ally.Delete();}
            owner.MoveToWorld(origin,map);
            Require(!ChargeMotion.TryStep(owner,Direction.East,_=>false) && owner.Location==origin,"Charge ignored caller destination policy");
            owner.Frozen=true;Require(!ChargeMotion.TryStep(owner,Direction.East),"Frozen player moved through charge adapter");owner.Frozen=false;
            owner.Paralyzed=true;Require(!ChargeMotion.TryStep(owner,Direction.East),"Paralyzed player moved through charge adapter");owner.Paralyzed=false;
            ClassCombat.For(owner).RecoveryUntil=Core.TickCount+1000;
            Require(!ChargeMotion.TryStep(owner,Direction.East),"Charge adapter bypassed hit recovery");ClassCombat.For(owner).RecoveryUntil=Core.TickCount;
            var before=ClassCombat.For(owner).PendingAbility;ClassCombat.For(owner).PendingAbility=CombatAbility.Zeal;
            Require(!ChargeMotion.TryStep(owner,Direction.East),"Charge adapter bypassed Zeal lock");ClassCombat.For(owner).PendingAbility=before;
            owner.MoveToWorld(origin,Map.Internal);Require(!ChargeMotion.TryStep(owner,Direction.East),"Charge moved on Internal");
        }
        finally{owner.Delete();}
    }
    private static void NetworkSetup()
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));
        _owner=Player();_origin=_owner.Location;_stamina=_owner.Stam;
        network.Seeded=true;network.CompressionEnabled=false;network.Mobile=_owner;_owner.NetState=network;
        // This fixture attaches its actor without account/character selection. Put only this
        // disposable socket into the game parser state; ordinary movement decoding stays real.
        // Account login and a graphical client's prediction are separate acceptance checks.
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Native game-protocol fixture bootstrap field changed");
        protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));
        network.ResetMovementState();network.SendMobileUpdate(_owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new{Serial=_owner.Serial.Value,X=_origin.X,Y=_origin.Y,Z=_origin.Z}));
    }
    private static void NetworkStep()
    {
        Require(_owner?.NetState!=null,"Owning socket was not attached");
        int mana=NecromancerEffects.ManaBalance256(_owner);
        Require(ClassCombat.Perform(_owner,CombatAbility.Charge,null,new(_origin.X+4,_origin.Y,_origin.Z),Core.TickCount),"Connected Charge start failed");
        var action=ClassCombat.For(_owner);int frames=0;
        while(action.Charge is{Phase:PaladinCharge.Phase.Moving})
        {Require(++frames<30 && ClassCombat.ResolveAction(_owner,action.Charge.NextFrame),"Connected Charge phase failed");}
        Require(action.Charge is{Phase:PaladinCharge.Phase.Recovery},"Connected ground Charge invented an impact");
        ClassCombat.ResolveAction(_owner,action.Charge.Until);
        Require(action.Charge==null && mana-NecromancerEffects.ManaBalance256(_owner)==2304,"Connected Charge did not finish/pay once");
        var state=_owner.NetState;var stats=MovementThrottle.GetMovementStats(state);
        Require(_owner.X==_origin.X+4 && _owner.Y==_origin.Y && _owner.Stam==_stamina,"Connected charge steps changed resources or position");
        Require(state.Sequence==0 && stats.QueueDepth==0 && stats.MovementCredit==0,"Owned steps left queued input or network movement debt");
    }
    private static void NetworkFinish()
    {
        Require(_owner.X==_origin.X+5 && _owner.Y==_origin.Y,"Normal client movement did not resume immediately after owned steps");
        Require(MovementThrottle.GetMovementStats(_owner.NetState).QueueDepth==0,"Ordinary movement remained queued after owned steps");
        var state=_owner.NetState;state.Mobile=null;_owner.NetState=null;_owner.Delete();_owner=null;
    }
}
