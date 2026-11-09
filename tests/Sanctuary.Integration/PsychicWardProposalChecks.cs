// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PsychicWardProposalChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PsychicWardProposalChecks));
    private sealed record Saved(uint Owner,bool Female,int Life256,int Mana256,PsychicWardState State);
    private sealed record Manifest(int Generation,Saved[] Owners);
    private static string FileName=>Path.Join(Core.BaseDirectory,"psychic-ward-proposal-fixtures.json");
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {bool again=reload;string name=again?"psychic-ward-proposal-reload-check":"psychic-ward-proposal-check";ConsoleInputHandler.RegisterCommand(name,"Unapproved Ward input/reaction proposal with original pool reloads",_=>Core.LoopContext.Post(()=>
        {try{Check(again);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    // Freeze ordinary native regeneration only for named persistence fixtures,
    // so the reload comparison measures storage rather than elapsed healing.
    public static void Initialize()
    {
        var hits=ClassGearHooks.CanRegenerateHits;var mana=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=actor=>actor.Name!="WardProposalSaved"&&hits?.Invoke(actor)!=false;
        ClassGearHooks.CanRegenerateMana=actor=>actor.Name!="WardProposalSaved"&&mana?.Invoke(actor)!=false;
    }
    private static PlayerMobile Fresh(bool female=false)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=female;p.Body=female?0x191:0x190;
        FixtureSpace.Corridor(p,7080,24,3650);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void Pool(PlayerMobile p,int amount)
    {PsychicWard.Restore(PsychicWard.Snapshot().Select(r=>r.Owner==p.Serial.Value?r with{Remaining256=amount}:r).ToArray());}
    private static void Windup(PlayerMobile p)
    {
        PsychicWard.Clear(p);Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.PsychicWard).Mana256;
        Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward review windup start");var s=ClassCombat.For(p);long effect=s.EffectAt;
        Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&PsychicWard.State(p)==null,"Ward windup timing/debit");
        Require(!PsychicWardCastingProposal.ResolveReview(p,effect-1)&&PsychicWard.State(p)==null,"Ward early release");ClassCombat.Stop(p);
        Require(!PsychicWardCastingProposal.ResolveReview(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Stopped Ward cast paid");
        Ready(p);Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward movement start");effect=s.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!PsychicWardCastingProposal.ResolveReview(p,effect)&&PsychicWard.State(p)==null,"Moved Ward cast released");p.MoveToWorld(origin,p.Map);
        Ready(p);Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward frozen start");effect=s.EffectAt;p.Frozen=true;
        Require(!PsychicWardCastingProposal.ResolveReview(p,effect)&&PsychicWard.State(p)==null,"Frozen Ward cast released");p.Frozen=false;
        Ready(p);Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward resource-loss start");effect=s.EffectAt;NecromancerEffects.SetMana256(p,cost-1);
        Require(!PsychicWardCastingProposal.ResolveReview(p,effect)&&PsychicWard.State(p)==null&&NecromancerEffects.ManaBalance256(p)==cost-1,"Underfunded Ward release paid/allocated");
        Ready(p);Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward one-shot release start");effect=s.EffectAt;
        Require(PsychicWardCastingProposal.ResolveReview(p,effect)&&PsychicWard.State(p)!=null&&NecromancerEffects.ManaBalance256(p)==mana-cost
            &&!PsychicWardCastingProposal.ResolveReview(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Ward release not once-only");
        var oldAllocate=PsychicWard.Allocate;
        try
        {
            Ready(p);Require(PsychicWardCastingProposal.BeginReview(p,Core.TickCount),"Ward allocation failure start");effect=s.EffectAt;PsychicWard.Allocate=()=>false;
            Require(!PsychicWardCastingProposal.ResolveReview(p,effect)&&PsychicWard.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Ward allocation failure group/debit changed");
        }
        finally{PsychicWard.Allocate=oldAllocate;}
        Log.Information("Ward proposal windup:shared WKSC clock,unpaid windup,Stop/movement/freeze/late mana cancellation,once-only release and group-clear allocation failure pass;public input remains gated.");
    }
    private static void Reactions(PlayerMobile p)
    {
        var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();int cases=0;
        try
        {
            foreach(var c in new[]{(Physical:1,Fire:0,Pool:641,Melee:true,Contact:true,React:true),(Physical:640,Fire:0,Pool:640,Melee:true,Contact:true,React:true),
                (Physical:768,Fire:0,Pool:640,Melee:true,Contact:true,React:true),(Physical:0,Fire:512,Pool:641,Melee:true,Contact:true,React:false),
                (Physical:512,Fire:0,Pool:641,Melee:false,Contact:true,React:false),(Physical:768,Fire:0,Pool:641,Melee:true,Contact:false,React:false),
                (Physical:0,Fire:0,Pool:641,Melee:true,Contact:true,React:false)})
            {
                Ready(p);Require(PsychicWard.CastPrepared(p),"Ward reaction fixture cast");Pool(p,c.Pool);SkillHealth.SetBalance256(p,p.HitsMax*256);int hp=SkillHealth.Balance256(p);CombatStuns.Clear(enemy);
                var ticket=PsychicWardReactionProposal.Capture(enemy,p,c.Physical,c.Melee,c.Contact);
                var parts=c.Contact?SkillCombat.MitigatePrepared(enemy,p,new(c.Physical/256.0,c.Fire/256.0,0,0,0)):new GearDamage(0,0,0,0,0);
                if(c.Contact){PreparedWeaponEvents.BeforeReturn(enemy,p,NamedElements.To256(parts.Physical),!c.Melee,ward:ticket);}
                Require((CombatStuns.Remaining(enemy)>0)==c.React&&SkillHealth.Balance256(p)==hp,"Ward reaction eligibility or event-before-HP order differs");
                if(ticket!=null)
                {
                    var stun=CombatStuns.Snapshot().Single(r=>r.Target==enemy.Serial.Value);
                    Require(!ticket.Deliver(enemy,p,false,TimedEffects.Now.AddMilliseconds(40))&&CombatStuns.Snapshot().Single(r=>r.Target==enemy.Serial.Value)==stun,"Ward ticket repeated/refreshed stun");
                }
                int expectedPool=c.Contact?Math.Max(0,c.Pool-c.Physical):c.Pool;
                Require((PsychicWard.State(p)?.Remaining256??0)==expectedPool,"Ward pool changed by wrong component/contact");
                int loss=NamedElements.To256(parts.Total);if(loss>0){SkillHealth.Damage(enemy,p,loss);}
                Require(SkillHealth.Balance256(p)==hp-loss,"Ward overflow/elemental HP commit differs");cases++;
            }
            Ready(p);PsychicWard.CastPrepared(p);var mismatch=PsychicWardReactionProposal.Capture(enemy,p,256,true,true);CombatStuns.Clear(enemy);
            Require(!mismatch.Deliver(p,p,false,TimedEffects.Now)&&!mismatch.Deliver(enemy,p,false,TimedEffects.Now)&&CombatStuns.Remaining(enemy)==0,"Rejected ticket context remained reusable");
            Require(PsychicWardReactionProposal.Capture(p,p,256,true,true)==null,"Player/friendly Ward source admitted");PsychicWard.Clear(p);
            Require(PsychicWardReactionProposal.Capture(enemy,p,256,true,true)==null,"Missing Ward captured reaction");
            Log.Information("Ward proposal reaction:7native packet cases retain full/fractional absorption,exact depletion/overflow stun before HP,elemental/ranged/miss/zero exclusions and once-only captured registration through shared return events. Wrong context consumes ticket;no automatic production capture.");
        }
        finally{enemy.Delete();CombatStuns.Clear(enemy);PsychicWard.Clear(p);}
        Require(cases==7,"Missing Ward proposal packet cases");
    }
    private static void Check(bool reload)
    {
        bool refused=false;try{PsychicWardReactionProposal.RequireApproval();}catch(NotSupportedException){refused=true;}
        Require(refused&&!WarlockCombat.Implemented(CombatAbility.PsychicWard),"Unapproved Ward admitted");
        var temporary=Fresh();try{Windup(temporary);Reactions(temporary);Require(!SkillBindings.Instance.Assign(temporary,false,CombatAbility.PsychicWard)&&!SkillBindings.Instance.AssignQuick(temporary,0,CombatAbility.PsychicWard),"Unapproved Ward bindings accepted");}
        finally{temporary.Delete();PsychicWard.Prune();}
        Manifest manifest;
        if(!reload)
        {
            var saved=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {var p=Fresh(female);p.Name="WardProposalSaved";Require(PsychicWard.CastPrepared(p),"Saved Ward cast");Pool(p,641+i);SkillHealth.SetBalance256(p,p.HitsMax*256-333-i);NecromancerEffects.SetMana256(p,p.ManaMax*256-517-i);
                saved[i++]=new(p.Serial.Value,female,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),PsychicWard.State(p));}
            manifest=new(0,saved);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Unexpected Ward proposal reload");
            foreach(var row in old.Owners)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);Require(p is{Alive:true,Deleted:false},"Saved Ward owner missing");
                var actual=new Saved(p.Serial.Value,p.Female,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),PsychicWard.State(p));
                Require(actual==row,"Original saved Ward mismatch:expected="+JsonSerializer.Serialize(row)+" actual="+JsonSerializer.Serialize(actual));
                Require(ClassCombat.For(p).PendingAbility==CombatAbility.None,"Transient Ward cast replayed after reload");
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));
        Log.Information("Ward proposal original:saved Warlock/Witch fractional pools,wounds,mana and captured rank/stun persist;no transient input replay and no recovery/reset.");
    }
}
