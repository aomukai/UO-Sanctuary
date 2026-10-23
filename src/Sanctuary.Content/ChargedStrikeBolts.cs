// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ChargedStrikeFlight(long Id,uint Owner,int Map,int Rank,int OriginX,int OriginY,int Z,uint Seed,int Direction,
    double X,double Y,int NextPoint,int Frame,int Min256,int Max256,DateTime UntilUtc,DateTime NextUtc);

public sealed class ChargedStrikeProfiles:GenericPersistence
{
    public ChargedStrikeProfiles():base("SanctuaryChargedStrike",121){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(ChargedStrikeBolts.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Charged Strike version");}
        ChargedStrikeBolts.Restore(JsonSerializer.Deserialize<ChargedStrikeFlight[]>(reader.ReadString()));
    }
}

public static class ChargedStrikeBolts
{
    private static readonly Dictionary<long,ChargedStrikeFlight> _flights=new();
    private static readonly Dictionary<long,Point2D[]> _paths=new();
    private static readonly List<long> _keys=new();
    private static long _next;
    internal static Func<int,int> DamageRoll=Utility.Random;
    public static ChargedStrikeProfiles Profiles{get;private set;}
    public static int Frames=>Math.Min(77,NecromancerSummons.Source("chargedstrikebolt","Range"));
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    internal static Point3D Aim(PlayerMobile owner,Point3D origin)=>new(2*origin.X-owner.X,2*origin.Y-owner.Y,origin.Z);
    public static void Cast(PlayerMobile owner,Point3D origin,SkillValues values)
    {
        var aim=Aim(owner,origin);int direction=ChargedBolts.Heading(aim.X-origin.X,aim.Y-origin.Y);
        var damage=AmazonMelee.LightningRange(values,NamedElements.MasteryPercent(owner,"ltng"));var now=TimedEffects.Now;
        for(int i=0;i<values.Eval("calc1");i++)
        {
            long id=++_next;uint seed=ChargedBolts.Seed(aim,i);
            var flight=new ChargedStrikeFlight(id,owner.Serial.Value,owner.Map.MapID,values.Rank,origin.X,origin.Y,origin.Z,seed,direction,
                origin.X,origin.Y,1,0,damage.Min,damage.Max,now.AddMilliseconds(Frames*40),now);
            _flights.Add(id,flight);_paths.Add(id,ChargedBolts.Path(seed,direction,Frames));
        }
    }
    private static void Remove(long id){_flights.Remove(id);_paths.Remove(id);}
    public static void Cancel(Mobile owner)
    {
        var ids=new List<long>();foreach(var flight in _flights.Values){if(flight.Owner==owner.Serial.Value){ids.Add(flight.Id);}}
        foreach(long id in ids){Remove(id);}
    }
    private static PlayerMobile Owner(ChargedStrikeFlight flight)=>World.FindMobile((Serial)flight.Owner) is PlayerMobile{Alive:true,Deleted:false} owner
        && ClassCreation.Profiles.TryGet(owner.Serial,out var profile) && profile.Class==DiabloClass.Amazon
        && (owner.Map==Map.Internal || owner.Map.MapID==flight.Map && !SpellHelper.IsTown(owner.Location,owner))?owner:null;
    private static void Tick()
    {
        var now=TimedEffects.Now;_keys.Clear();_keys.AddRange(_flights.Keys);
        foreach(long id in _keys)
        {
            if(!_flights.TryGetValue(id,out var flight)){continue;}var owner=Owner(flight);
            if(owner==null || flight.UntilUtc<=now){Remove(id);continue;}
            if(owner.Map==Map.Internal || flight.NextUtc>now){continue;}
            var path=_paths[id];var next=ChargedBolts.Advance(path,flight.OriginX,flight.OriginY,flight.X,flight.Y,flight.NextPoint,
                SkillGeometry.Velocity(NecromancerSummons.Source("chargedstrikebolt","Vel")));
            var before=new Point3D((int)Math.Round(flight.X),(int)Math.Round(flight.Y),flight.Z);
            var after=new Point3D((int)Math.Round(next.X),(int)Math.Round(next.Y),flight.Z);
            if(flight.Frame+1>=Frames || next.Next>=path.Length || after.X<0 || after.Y<0 || after.X>=owner.Map.Width || after.Y>=owner.Map.Height
                || !owner.Map.LineOfSight(new Point3D(before.X,before.Y,flight.Z+12),new Point3D(after.X,after.Y,flight.Z+12)))
            {Remove(id);continue;}
            Mobile first=null;double nearest=double.MaxValue;
            // Reuse the current native charged-bolt sweep. Source subtile/Size
            // collision and small/large-target shotgun parity remain acceptance
            // work; this adapter is not an approved geometry deviation.
            foreach(var target in SkillCombat.Targets(owner,after,2))
            {
                if(CombatHitboxes.Intersect(before,after,target.Location,out double entry) && entry<nearest)
                {first=target;nearest=entry;}
            }
            Effects.SendLocationEffect(after,owner.Map,0x3818,3,10,0x482,0); // Identified travelling lightning placeholder.
            if(first==null)
            {
                _flights[id]=flight with{X=next.X,Y=next.Y,NextPoint=next.Next,Frame=flight.Frame+1,NextUtc=flight.NextUtc.AddMilliseconds(40)};
                continue;
            }
            Remove(id);SorceressEffects.MissileHit(owner,first,"chargedstrikebolt");
            int span=Math.Max(1,flight.Max256-flight.Min256),rolled=flight.Min256+Math.Clamp(DamageRoll(span),0,span-1);
            SkillCombat.LightningImpact(owner,first,rolled);
        }
    }
    public static ChargedStrikeFlight[] Snapshot()=>new List<ChargedStrikeFlight>(_flights.Values).ToArray();
    public static void Restore(ChargedStrikeFlight[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Charged Strike flights");}
        var checkedFlights=new Dictionary<long,ChargedStrikeFlight>();var checkedPaths=new Dictionary<long,Point2D[]>();long nextId=0;
        foreach(var flight in saved)
        {
            if(flight.Id<=0 || flight.Owner==0 || flight.Rank is <1 or >255 || flight.Direction is <0 or >7 || flight.Frame<0 || flight.Frame>=Frames
                || flight.OriginX<0 || flight.OriginY<0 || !double.IsFinite(flight.X+flight.Y) || flight.Min256<0 || flight.Max256<flight.Min256
                || flight.NextPoint<1 || flight.NextPoint>Frames/2 || !checkedFlights.TryAdd(flight.Id,flight))
            {throw new InvalidDataException("Invalid Charged Strike flight");}
            checkedPaths.Add(flight.Id,ChargedBolts.Path(flight.Seed,flight.Direction,Frames));nextId=Math.Max(nextId,flight.Id);
        }
        _flights.Clear();_paths.Clear();foreach(var pair in checkedFlights){_flights.Add(pair.Key,pair.Value);_paths.Add(pair.Key,checkedPaths[pair.Key]);}_next=nextId;
    }
}
