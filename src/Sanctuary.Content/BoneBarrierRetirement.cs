// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
namespace Sanctuary;

internal sealed record BarrierRetirementState(uint Formation,DateTime EnteredUtc,DateTime CompletesUtc,string Cause);
internal interface IBarrierRetirement
{
    BarrierRetirementState Retirement{get;}
    void StoreRetirement(BarrierRetirementState state);
}

// Approved Sanctuary composition, not a recovered modern hidedead/owner graph.
// Reuse the sentry's scheduler calculation and the barrier's existing 40ms timer.
internal static class BoneBarrierRetirement
{
    internal static bool Approved=>true;
    private static readonly HashSet<BoneBarrierSegment> Tracked=new();
    internal static Action<BoneBarrierSegment,BarrierRetirementState> Restored;
    internal static int DurationMs=>AssassinSentryRetirement.EndDelay(15,256)*40;
    internal static void Validate(BarrierRetirementState state)
    {
        if(state==null||!((Serial)state.Formation).IsMobile||state.EnteredUtc.Kind!=DateTimeKind.Utc
            ||state.CompletesUtc.Kind!=DateTimeKind.Utc||state.CompletesUtc!=state.EnteredUtc.AddMilliseconds(DurationMs)
            ||state.Cause is not("source-ai" or "damage"))
        {throw new InvalidDataException("Invalid saved barrier retirement");}
    }
    internal static void Track(BoneBarrierSegment body)=>Tracked.Add(body);
    internal static void Forget(BoneBarrierSegment body)=>Tracked.Remove(body);
    internal static void DeleteLinked(BoneBarrierSegment root)
    {
        if(root.Root!=null){return;}
        foreach(var child in Tracked.Where(b=>!b.Deleted&&b.Root==root).ToArray()){child.Delete();}
    }
    internal static void Load(BoneBarrierSegment body,IBarrierRetirement retiring)
    {
        if(retiring.Retirement is{} state)
        {Validate(state);body.ReleaseCollision();Restored?.Invoke(body,state);}
        Track(body);
    }
    internal static void Begin(BoneBarrierSegment hit,DateTime at,string cause)
    {
        if(hit.Deleted||hit is not IBarrierRetirement live||live.Retirement!=null){return;}
        var root=hit.Root??hit;
        var state=new BarrierRetirementState(root.Serial.Value,at,at.AddMilliseconds(DurationMs),cause);Validate(state);
        var group=Tracked.Where(b=>!b.Deleted&&(b==root||b.Root==root)).OrderBy(b=>b.Serial.Value).ToArray();
        if(group.Length==0||group.Any(b=>b is not IBarrierRetirement))
        {throw new InvalidOperationException("Unmapped barrier retirement formation");}
        // Publish all death states before releasing collision or presentation.
        // Repeated contacts cannot restart the formation's deadline.
        foreach(var body in group){((IBarrierRetirement)body).StoreRetirement(state);}
        foreach(var body in group)
        {
            body.ReleaseCollision();body.AIObject?.AITimer.Stop();
            body.RefreshAppearance(at);
        }
        Effects.PlaySound(root.Location, root.Map, SpellEffectSounds.BoneBarrierFall);
    }
    internal static bool Advance(BoneBarrierSegment body,IBarrierRetirement retiring,DateTime at)
    {
        if(retiring.Retirement is not{} state){return false;}
        if(at>=state.CompletesUtc)
        {
            // Finalize the explicit formation, not a geometric proximity scan.
            var group=Tracked.Where(b=>!b.Deleted&&b is IBarrierRetirement r&&r.Retirement?.Formation==state.Formation)
                .OrderBy(b=>b.Root==null?1:0).ThenBy(b=>b.Serial.Value).ToArray();
            foreach(var member in group){member.Delete();}
        }
        return true;
    }
}

// New types preserve all existing v0 plain/prepared/thinking save layouts.
[SerializationGenerator(0)]
public partial class BoneBarrierRetiringBody:BoneBarrierThinkingBody,IBarrierRetirement
{
    [SerializableField(0,setter:"private")]private string _retirementJson="";
    private BarrierRetirementState _retirement;
    BarrierRetirementState IBarrierRetirement.Retirement=>_retirement??(string.IsNullOrEmpty(_retirementJson)?null:_retirement=JsonSerializer.Deserialize<BarrierRetirementState>(_retirementJson));
    void IBarrierRetirement.StoreRetirement(BarrierRetirementState state)
    {BoneBarrierRetirement.Validate(state);_retirement=state;_retirementJson=JsonSerializer.Serialize(state);}
    private bool Retiring=>((IBarrierRetirement)this).Retirement!=null;
    public override bool Alive=>!Retiring&&base.Alive;
    public override bool BlocksFit=>!Retiring&&base.BlocksFit;
    public override bool CanBeDamaged()=>!Retiring&&base.CanBeDamaged();
    [Constructible]public BoneBarrierRetiringBody(){BoneBarrierRetirement.Track(this);}
    protected override void RequestRetirement(DateTime at)=>BoneBarrierRetirement.Begin(this,at,"source-ai");
    protected override bool AdvanceRetirement(DateTime at)=>BoneBarrierRetirement.Advance(this,this,at);
    public override bool OnBeforeDeath(){BoneBarrierRetirement.Begin(this,TimedEffects.Now,"damage");return false;}
    public override void OnDelete(){BoneBarrierRetirement.DeleteLinked(this);base.OnDelete();}
    public override void OnAfterDelete(){BoneBarrierRetirement.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterRetirementLoad()=>BoneBarrierRetirement.Load(this,this);
}

[SerializationGenerator(0)]
public partial class BoneWallRetiringBody:BoneWallThinkingBody,IBarrierRetirement
{
    [SerializableField(0,setter:"private")]private string _retirementJson="";
    private BarrierRetirementState _retirement;
    BarrierRetirementState IBarrierRetirement.Retirement=>_retirement??(string.IsNullOrEmpty(_retirementJson)?null:_retirement=JsonSerializer.Deserialize<BarrierRetirementState>(_retirementJson));
    void IBarrierRetirement.StoreRetirement(BarrierRetirementState state)
    {BoneBarrierRetirement.Validate(state);_retirement=state;_retirementJson=JsonSerializer.Serialize(state);}
    private bool Retiring=>((IBarrierRetirement)this).Retirement!=null;
    public override bool Alive=>!Retiring&&base.Alive;
    public override bool BlocksFit=>!Retiring&&base.BlocksFit;
    public override bool CanBeDamaged()=>!Retiring&&base.CanBeDamaged();
    [Constructible]public BoneWallRetiringBody(){BoneBarrierRetirement.Track(this);}
    protected override void RequestRetirement(DateTime at)=>BoneBarrierRetirement.Begin(this,at,"source-ai");
    protected override bool AdvanceRetirement(DateTime at)=>BoneBarrierRetirement.Advance(this,this,at);
    public override bool OnBeforeDeath(){BoneBarrierRetirement.Begin(this,TimedEffects.Now,"damage");return false;}
    public override void OnDelete(){BoneBarrierRetirement.DeleteLinked(this);base.OnDelete();}
    public override void OnAfterDelete(){BoneBarrierRetirement.Forget(this);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterRetirementLoad()=>BoneBarrierRetirement.Load(this,this);
}
