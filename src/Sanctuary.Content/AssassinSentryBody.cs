// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record SentryBodyState(uint Owner,SentryCreationStats Creation,int Map,Point3D Position,
    SentryControlState Control,ulong Seed,DateTime NextFrameUtc,string ControllerFault=null)
{
    // Null identifies an older save whose dynamic-path aim was not retained.
    // New actors start with the source allocation's zero-initialized field.
    public Point2D? CachedAim{get;init;}
}

// Saved stationary actor backend. No Constructible or learned input path exists
// until emergence, player casting and remaining mechanical acceptance are complete.
[SerializationGenerator(0)]
public partial class AssassinSentryBody:BaseCreature
{
    [SerializableField(0,setter:"private")] private string _saved="{}";
    private SentryBodyState _prepared;
    internal SentryBodyState Prepared=>_prepared??(string.IsNullOrEmpty(_saved)||_saved=="{}"?null:_prepared=JsonSerializer.Deserialize<SentryBodyState>(_saved));
    public override bool BlocksFit=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanBeDamaged()=>false;
    public override bool Commandable=>false;
    public override bool IsBondable=>false;
    public override bool IsDispellable=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool CanRegenStam=>false;
    public override bool DeleteCorpseOnDeath=>true;
    public override bool OnMoveOver(Mobile mobile)=>true;
    public override bool IsEnemy(Mobile mobile)=>false;
    public override bool CanBeHarmful(Mobile mobile,bool message,bool ignoreOurBlessedness)=>false;
    public AssassinSentryBody():base(AIType.AI_Melee,FightMode.None,0)
    {
        Name="Charged Bolt Sentry [placeholder]";Body=0x9C;Hue=0x482;Blessed=true;CantWalk=true;
        Summoned=true;ControlSlots=0;NoKillAwards=true;SetStr(0);SetDex(0);SetInt(0);SetStam(0);SetMana(0);SetDamage(0,0);
        AIObject?.AITimer.Stop();
    }
    private static void Validate(SentryBodyState s)
    {
        if(s?.Creation is not{Rank:>=1 and <=255,CasterLevel:>=1 and <=99,Level:>=1 and <=99,Life256:25600} c
            || c.Passives is not{Length:3} || c.Skills==null || c.Passives.Select(x=>x.Name).Distinct().Count()!=3
            || !c.Skills.Any(x=>x.Name=="BoltSentry"&&x.Value==c.Rank) || c.Skills.Any(x=>x.Value is <1 or >255)
            || c.Skills.Select(x=>x.Name).Distinct().Count()!=c.Skills.Length
            || c.Difficulty is <MonsterDifficulty.Normal or >MonsterDifficulty.Hell
            || c.Passive("item_pierce_light_immunity")!=0 || !((Serial)s.Owner).IsMobile
            || s.Map<0 || s.Map>=Map.Maps.Length || Map.Maps[s.Map]==null || Map.Maps[s.Map]==Map.Internal
            || s.Position.X<=0 || s.Position.Y<=0 || s.Position.X>=Map.Maps[s.Map].Width || s.Position.Y>=Map.Maps[s.Map].Height
            || s.Position.Z is <-128 or >127 || s.NextFrameUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Unsupported saved sentry body state");}
        AssassinSentryController.Validate(s.Control);
        if(s.CachedAim is{} aim && (aim.X is <0 or >65535 || aim.Y is <0 or >65535))
        {throw new InvalidDataException("Invalid saved sentry target-path coordinate");}
        var expected=AssassinSentryCreation.Rescale(c,c.Difficulty);
        if(c.Level!=expected.Level || c.Defense!=expected.Defense || c.Attack!=expected.Attack
            || c.Passives.Any(x=>x.Name is not("item_pierce_light_immunity" or "passive_ltng_pierce" or "passive_ltng_mastery"))
            || c.Skills.Any(x=>x.Name is not("BoltSentry" or "Fire Trauma" or "Shock Field" or "Lightning Sentry" or "Death Sentry")))
        {throw new InvalidDataException("Saved sentry creation context disagrees with retained source bases");}
    }
    private void Store(SentryBodyState s){Validate(s);_prepared=s;_saved=JsonSerializer.Serialize(s);}
    internal static AssassinSentryBody CreatePrepared(PlayerMobile owner,Point3D position,DateTime at,ulong seed)
        =>Create(owner,position,at,seed,false);
    // Actor-phase entry at a supplied valid point. Player do45 placement,
    // payment/cancellation and the learned handler remain separate gates.
    internal static AssassinSentryBody CreateEmerging(PlayerMobile owner,Point3D position,DateTime at,ulong seed)
        =>Create(owner,position,at,seed,true);
    private static AssassinSentryBody Create(PlayerMobile owner,Point3D position,DateTime at,ulong seed,bool emerging)
    {
        var stats=AssassinSentryCreation.Capture(owner);var map=owner.Map;
        if(at.Kind!=DateTimeKind.Utc || SpellHelper.IsTown(owner.Location,owner) || SpellHelper.IsTown(position,owner))
        {throw new InvalidOperationException("Prepared sentry needs a UTC clock and non-town owner/destination");}
        // Prepared fixtures retain their explicit neutral/+25 initial state.
        // Emerging actors retain S1 ENDANIM+15 and the original AI+25 event.
        at=new DateTime(at.Ticks-at.Ticks%(40*TimeSpan.TicksPerMillisecond),DateTimeKind.Utc);
        var state=new SentryBodyState(owner.Serial.Value,stats,map.MapID,position,emerging?AssassinSentryController.StartEmerging():AssassinSentryController.Start(),seed,at.AddMilliseconds(40)){CachedAim=new(0,0)};Validate(state);
        var body=new AssassinSentryBody();
        try
        {
            body.Store(state);body.SummonMaster=owner;body.SetHits(stats.Life256/256);body.MoveToWorld(position,map);body.AIObject?.AITimer.Stop();
            AssassinTrapRoster.Register(owner,body,checked((int)AssassinSentryCreation.Values(stats).Eval("petmax")));
            AssassinSentryActors.Track(body);return body;
        }
        catch{body.Delete();throw;}
    }
    internal bool TrapTarget(PlayerMobile owner,Mobile target)=>AssassinTrapTargets.Eligible(this,owner,target,()=>base.CanBeHarmful(target,false,true));
    internal int Passive(string name)=>Prepared.Creation.Passive(name);
    internal void RescaleDifficulty(MonsterDifficulty difficulty)
    {
        var before=Prepared;Validate(before);if(before.Creation.Difficulty==difficulty){return;}
        int life=SkillHealth.Balance256(this),maximum=SkillHealth.Maximum256(this);
        var next=AssassinSentryCreation.Rescale(before.Creation,difficulty);Store(before with{Creation=next});
        if(next.Life256!=before.Creation.Life256){SetHits(next.Life256/256);}
        SkillHealth.SetBalance256(this,SummonDifficulties.Life256(life,maximum,SkillHealth.Maximum256(this)));
    }
    internal void AdvanceController(DateTime at,bool allowActions)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Sentry clock must be UTC");}
        while(!Deleted && Prepared is{} before && before.NextFrameUtc<=at)
        {
            Validate(before);
            if(World.FindMobile((Serial)before.Owner) is not PlayerMobile{Alive:true,Deleted:false} owner
                || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin)
            {Delete();return;}
            if(Map?.MapID!=before.Map || Location!=before.Position)
            {throw new NotSupportedException("Stationary sentry was moved outside its saved location");}
            if(!allowActions || before.ControllerFault!=null)
            {
                // No timed expiry is established for this shot-limited sentry.
                // Offline time advances, but no AI/RNG/shot/action is replayed.
                // Discard an unfinished windup without refunding its shot.
                long due=(at.Ticks-before.NextFrameUtc.Ticks)/(40*TimeSpan.TicksPerMillisecond)+1;
                Store(before with{Control=AssassinSentryController.Inactive(before.Control,checked((int)due)),NextFrameUtc=before.NextFrameUtc.AddTicks(checked(due*40*TimeSpan.TicksPerMillisecond))});
                continue;
            }
            ulong seed=before.Seed;
            uint selected=0;Point2D? cached=before.CachedAim;
            // Keep the source owner-town exit while sharing a facet. Across
            // facets, approved old-facet retention uses the trap room instead.
            bool town=owner.Map==Map?SpellHelper.IsTown(owner.Location,owner):SpellHelper.IsTown(Location,this);
            var control=AssassinSentryController.Advance(before.Control,true,true,town,true,true,
                ()=>AssassinSentryCreation.Shots(before.Creation),
                ()=>
                {
                    var target=(AssassinSentryActors.SelectTarget??throw new NotSupportedException("Sentry source target-selection service is not connected"))(this);
                    selected=target?.Serial??0;return target;
                },
                ()=>SourceRandom.Limited(ref seed,100)<100,
                ()=>
                {
                    // Mode14's unit-target path preparation runs before the
                    // start service. The zero target-lead parameter keeps the
                    // raw unit cell; it is distinct from movement velocity.
                    if(World.FindMobile((Serial)selected) is{Deleted:false} target)
                    {
                        if(target.Map!=Map){throw new NotSupportedException("Cross-map sentry start target is not mapped");}
                        cached=AssassinSentryAim.Begin(cached,new(target.X*2,target.Y*2));
                    }
                    return true; // Actor306 has no start function; emergence/input remain gated.
                },
                target=>(AssassinSentryActors.Dispatch??throw new NotSupportedException("Sentry real charged-bolt missile factory is not connected"))(this,target));
            Store(before with{Control=control,Seed=seed,CachedAim=cached,NextFrameUtc=before.NextFrameUtc.AddMilliseconds(40)});
            if(control.Mode==0){Delete();return;}
            AIObject?.AITimer.Stop();
        }
    }
    internal void SuspendController(string reason)
    {if(Prepared is{} before){Store(before with{ControllerFault=reason});}}
    public override void OnAfterDelete(){AssassinSentryActors.Forget(this);AssassinTrapRoster.Forget(this);base.OnAfterDelete();}
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void GenerateLoot(bool spawning){}
    public override bool OnBeforeDeath(){Delete();return false;}
    [AfterDeserialization]private void AfterLoad()
    {
        AIObject?.AITimer.Stop();_prepared=JsonSerializer.Deserialize<SentryBodyState>(_saved);Validate(_prepared);
        if(SummonMaster?.Serial.Value!=_prepared.Owner || Map?.MapID!=_prepared.Map || Location!=_prepared.Position)
        {throw new InvalidDataException("Saved sentry owner/position mismatch");}
        AssassinSentryActors.Track(this);
    }
}

public static class AssassinSentryActors
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AssassinSentryActors));
    private static readonly Dictionary<Serial,AssassinSentryBody> Active=new();
    // The mapped selector and real projectile factory are bound at Initialize.
    // Orchestration fixtures can replace these explicitly scoped services.
    internal static Func<AssassinSentryBody,SentryTarget?> SelectTarget;
    internal static Action<AssassinSentryBody,uint> Dispatch;
    public static void Initialize()
    {
        AssassinSentryWorldTargets.PrimaryPath??=AssassinSentryUnitPath.Probe;
        SelectTarget??=AssassinSentryWorldTargets.Select;Dispatch??=AssassinSentryBolts.Dispatch;
    }
    internal static void Track(AssassinSentryBody body)=>Active[body.Serial]=body;
    internal static void Forget(AssassinSentryBody body)=>Active.Remove(body.Serial);
    internal static AssassinSentryBody[] Actors()=>Active.Values.Where(x=>!x.Deleted).ToArray();
    internal static void AdvanceAt(AssassinSentryBody body,DateTime at)
    {
        try{body.AdvanceController(at,AssassinTrapRoster.Fighting(body.Prepared.Owner));}
        catch(Exception e){body.SuspendController(e.Message);Log.Error(e,"Prepared sentry controller suspended: {Serial}",body.Serial);}
    }
    internal static void Tick(DateTime at)
    {
        AssassinTrapRoster.SweepOwners();
        foreach(var body in Active.Values.Where(x=>!x.Deleted).OrderByDescending(x=>x.Serial.Value).ToArray())
        {
            try{body.AdvanceController(at,AssassinTrapRoster.Fighting(body.Prepared.Owner));}
            catch(Exception e){body.SuspendController(e.Message);Log.Error(e,"Prepared sentry controller suspended: {Serial}",body.Serial);}
        }
    }
}
