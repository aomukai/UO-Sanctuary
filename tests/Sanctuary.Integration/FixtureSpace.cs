using System;
using System.Linq;
using System.Net;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

internal static class FixtureSpace
{
    // Park inspected state only after its actual timing/expiry checks. The
    // full retained suite and two reloads now exceed the old ten-minute hold.
    // Natural expiry controls keep their original short deadlines.
    internal const int RetentionMilliseconds=3_600_000;
    internal static TimeSpan Retention=>TimeSpan.FromMilliseconds(RetentionMilliseconds);
    // Reserve a flat, unoccupied corridor for actual displacement/projectile
    // checks. Reuse the same world-fit rules across blade and spell fixtures.
    internal static int Corridor(PlayerMobile p,int column,int reach,int firstRow=2600)
    {
        int floor=Enumerable.Range(0x500,0x100).First(id=>TileData.ItemTable[id].Surface && !TileData.ItemTable[id].Impassable && TileData.ItemTable[id].CalcHeight==0);
        for(int y=firstRow;y<3800;y+=32)
        {
            bool clear=true;
            for(int x=column-4;x<=column+reach;x++)for(int dy=-3;dy<=3;dy++)
            {if(!Map.Trammel.CanFit(x,y+dy,0,16,true,true,false)){clear=false;}}
            if(!clear){continue;}
            for(int x=column-4;x<=column+reach;x++)for(int dy=-3;dy<=3;dy++){new Static(floor).MoveToWorld(new(x,y+dy,0),Map.Trammel);}
            Place(p,new(column,y,0),Map.Trammel);return floor;
        }
        throw new InvalidOperationException("No independent fixture corridor near "+column+","+firstRow);
    }
    internal static bool ClearOfAllies(PlayerMobile p,Point3D point,Map map)
    {
        foreach(var other in map.GetMobilesInRange<Mobile>(point,0))
        { if(AlliedOverlap.PassThrough(p,other) && point.Z+16>other.Z && other.Z+16>point.Z) { return false; } }
        return true;
    }
    internal static void Place(PlayerMobile p, Point3D anchor, Map map)
    {
        for (int r=0;r<=30;r++)
        {
            for (int dx=-r;dx<=r;dx++)
            {
                for(int dy=-r;dy<=r;dy++)
                {
                    if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=r) { continue; }
                    var point=new Point3D(anchor.X+dx,anchor.Y+dy,anchor.Z);
                    if (Teleportation.Space(map,point,p)) { p.MoveToWorld(point,map); return; }
                }
            }
        }
        throw new InvalidOperationException("No independent fixture position near "+anchor);
    }
}

internal static class FixtureNetwork
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FixtureNetwork));
    internal static Account Authenticate(NetState network,PlayerMobile player,string prefix)
    {
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);
        protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));
        var account=new Account(prefix+Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"));
        account[0]=player;network.Account=account;network.Mobile=player;player.NetState=network;
        network.ResetMovementState();network.SendMobileUpdate(player);return account;
    }
    internal static void Connected(string result,Action<NetState> setup)
    {
        long deadline=Core.TickCount+5000;
        void Try()
        {
            try
            {
                var clients=NetState.Instances.Where(x=>x.Running && x.Mobile==null && IPAddress.IsLoopback(x.Address)).ToArray();
                if(clients.Length==1){setup(clients[0]);return;}
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Expected one fixture connection; found "+clients.Length);}
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Try));
            }
            catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        }
        Try();
    }
}

internal static class FixtureClock
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FixtureClock));
    // A timer may fire during catch-up while Core.TickCount is already ahead
    // of the wheel. Start timed controls after that slice, on the loop queue.
    internal static void NextLoop(string result,Action action)=>Timer.StartTimer(TimeSpan.Zero,()=>Core.LoopContext.Post(()=>
    {try{action();}catch(Exception e){Log.Error(e,"{Result}: failed",result);}}));
}
