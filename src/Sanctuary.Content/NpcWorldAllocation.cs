// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public sealed record NpcWorldAllocationState(int Version,string Profile,uint InitialLow,ulong Seed);
public sealed class NpcWorldAllocationProfiles:GenericPersistence
{
    private NpcWorldAllocationState Snapshot;
    private static readonly JsonSerializerOptions Json=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public NpcWorldAllocationProfiles():base("SanctuaryNpcWorldAllocation",79){}
    // Capture on the simulation thread,not while a save worker is encoding.
    public override void Serialize(){Snapshot=NpcWorldAllocation.Capture();base.Serialize();}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(Snapshot));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown NPC world allocation persistence version");}
        NpcWorldAllocation.Restore(JsonSerializer.Deserialize<NpcWorldAllocationState>(reader.ReadString(),Json));
    }
}

// One saved allocation domain for this persistent Sanctuary world. Its initial
// 32-bit seed is host entropy,not a historical D2 game/map seed. Subsequent
// supplied item/unit draws use the shared source algorithm. Actor RNG stays
// separate. This does not claim current D2R whole-engine allocation chronology.
internal static class NpcWorldAllocation
{
    internal const string Profile="sanctuary-world-explicit-seed-v1";
    private static NpcWorldAllocationState Origin;
    private static SourceSeedStream Game;
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    public static NpcWorldAllocationProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()
    {
        NpcOriginalItemProcs.Allocator=Root;
        ChainLightning.OriginalNpcAllocator=Child;
        // Targetless first-path data still requires its actual AI adapter.
    }
    internal static NpcWorldAllocationState Capture()=>Origin==null?null:Origin with{Seed=Game.Value};
    internal static void Restore(NpcWorldAllocationState state)
    {
        if(state!=null&&(state.Version!=0||state.Profile!=Profile||state.Seed==0))
        {throw new InvalidDataException("Invalid NPC world allocation context");}
        var stream=state==null?null:new SourceSeedStream(state.Seed);
        Origin=state;Game=stream;
    }
    internal static SourceSeedStream RequireStream()
    {
        if(World.Loading){throw new NotSupportedException("NPC world allocation cannot generate during load");}
        if(Game==null)
        {
            int low=SeedLow();var stream=new SourceSeedStream(SourceRandom.InitLow(low));
            Origin=new(0,Profile,unchecked((uint)low),stream.Value);Game=stream;
        }
        return Game;
    }
    internal static ulong UnitSeed()
    {
        var game=RequireStream();uint low=SourceRandom.Roll(ref game.Value);
        return SourceRandom.InitLow(unchecked((int)low));
    }
    private static void Source(AmazonValkyrieBody body,DateTime at)
    {
        if(World.Loading||body?.ControlMaster is not PlayerMobile owner||!NpcItemSpellFlights.Attached(body,owner,body.Map?.MapID??-1)
            ||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-2000))
        {throw new NotSupportedException("NPC world missile allocation needs its attached original source and UTC frame");}
    }
    internal static NpcItemSpellAllocation Root(AmazonValkyrieBody body,NpcItemMissileRequest request,DateTime at)
    {
        Source(body,at);
        if(request==null||request.Rank is<1 or>63){throw new NotSupportedException("NPC world root lost its explicit proc rank");}
        _=NpcItemSpellDamage.Require(request.Skill);var row=SkillMechanics.For(NpcItemSpellDamage.Ability(request.Skill));
        string missile=row.Text(request.Skill==36?"srvmissile":"srvmissilea");bool ring=request.Skill is 44 or 48;
        if(request.Missile!=ItemSkillExecution.MissileId(missile)||request.X<=0||request.Y<=0||request.X>=body.Map.Width*2||request.Y>=body.Map.Height*2
            ||(request.X&1)!=0||(request.Y&1)!=0||request.AttackRating<0
            ||ring&&(request.Flags!=7||request.Velocity!=24||request.AttackRating!=0)
            ||!ring&&((request.Flags&~0x1000)!=33||request.Velocity!=0)
            ||(request.Skill==38?(request.ChargedOrdinal<0||request.ChargedOrdinal>=Math.Min(24,request.Rank+2)):request.ChargedOrdinal!=-1))
        {throw new NotSupportedException("NPC world root lost its qualified factory request");}
        // Origin/room/range validation and native factory refusal occur in the
        // shared emitter before this successful allocation boundary is called.
        return new(true,UnitSeed());
    }
    internal static NpcChainAllocation Child(NpcChainRequest request)
    {
        if(request==null){throw new NotSupportedException("Missing NPC world child request");}
        var body=World.FindMobile((Serial)request.Actor)as AmazonValkyrieBody;Source(body,request.At);
        if(request.Skill!=53||request.Rank is<1 or>63||request.Cast<=0||request.Ordinal<0||request.Remaining<1||request.Flags!=33
            ||request.Owner!=body.ControlMaster.Serial.Value||request.Map!=body.Map.MapID||!double.IsFinite(request.X+request.Y)
            ||request.X<=0||request.Y<=0||request.X>=body.Map.Width||request.Y>=body.Map.Height)
        {throw new NotSupportedException("NPC world child lost its qualified factory request");}
        return new(true,UnitSeed());
    }
}
