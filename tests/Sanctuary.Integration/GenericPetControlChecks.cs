// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

internal static class GenericPetControlChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetControlChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Refused(Action action,string why)
    {bool refused=false;try{action();}catch(NotSupportedException){refused=true;}Require(refused,why);}
    internal static void Seed(SkeletonGuardian[] bodies)
    {
        for(int i=0;i<bodies.Length;i++)
        {
            var body=bodies[i];var before=DeathMarkWorld.State(body);
            Require(before.Control==null,"Legacy fixture accidentally gained default AI words");
            string legacy=JsonSerializer.Serialize(before);
            Require(!legacy.Contains("\"Control\"")&&JsonSerializer.Deserialize<DeathMarkSavedActor>(legacy).Control==null,"Old seven-field controller format no longer remains unknown");
            Refused(()=>DeathMarkWorld.InitializePrepared(body,checked((int)body.GetMaster().Serial.Value),new GenericPetWorldIdentity(71),null),"Unknown control was invented at initialization");
            Require(DeathMarkWorld.State(body)==before,"Refused unknown control consumed command or changed controller");
            var seed=new GenericPetSavedControl(unchecked(int.MinValue+i),-1-i,int.MaxValue-i);
            DeathMarkWorld.RegisterControl(body,seed);
            Refused(()=>DeathMarkWorld.RegisterControl(body,new(0,0,0)),"Duplicate control registration overwrote state");
            Require(DeathMarkWorld.State(body)==before with{Control=seed},"Explicit control registration changed command/mode/next-AI");
        }
    }
    internal static void Root(SkeletonGuardian body,int owner,GenericPetWorldIdentity world,GenericPetInitializationServices leaves)
    {
        var before=DeathMarkWorld.State(body);var words=before.Control;Require(words!=null,"Loaded original lost control words");
        var after=new GenericPetSavedControl(unchecked(words.Value0+1),unchecked(words.Value1-7),words.Value2^0x5a5a5a5a);
        var trace=new List<string>();GenericPetInitializedFrame initialized=null;
        var pet=new GenericPetDecisionUnit(1,true,20,30);var player=new GenericPetDecisionUnit(0,true,20,30,true);
        var root=new GenericPetRootServices(()=>player,resolved=>
        {
            Require(ReferenceEquals(resolved,player),"Root changed owner");
            initialized=DeathMarkWorld.InitializePrepared(body,owner,world,leaves);var memory=initialized.Decision.Memory;
            Require(new GenericPetSavedControl(memory.Value0,memory.Value1,memory.Value2)==words,"Owned control did not reach initializer snapshot");
            Require(DeathMarkWorld.State(body).Control==words,"Initialization changed persistent control");trace.Add("init");return initialized.Decision;
        },i=>trace.Add("remove"+i),(_,_,_)=>0,_=>2,(_,_)=>throw new InvalidOperationException("Unexpected follow"),(_,frame)=>
        {
            Require(ReferenceEquals(frame,initialized.Decision),"Root used detached decision memory");
            frame.Memory.Value0=after.Value0;frame.Memory.Value1=after.Value1;frame.Memory.Value2=after.Value2;
            Require(DeathMarkWorld.State(body).Control==words,"Decision frame aliases persistent AI control");trace.Add("combat");return true;
        },_=>throw new InvalidOperationException("Unexpected idle"),memory=>
        {
            Require(DeathMarkWorld.State(body).Control==words,"AI words published before finishing");
            Require(DeathMarkWorld.FinishPrepared(body,(before.Type4c-1,before.Target48))==1,"Original root finishing rejected typed target");trace.Add("finish");
        },(_,_,_)=>throw new InvalidOperationException("Persistent control writer was not bound"));
        root=DeathMarkWorld.BindControlPersistence(body,root);
        var events=GenericPetTimedEventChecks.Bind(body,new(pet,true,true,true,Array.Empty<byte>()),root);
        GenericPetDecisions.Think(events.Context,events.Services);
        Require(trace.SequenceEqual(new[]{"init","remove0","remove2","combat","finish"})&&DeathMarkWorld.State(body)==before with{Control=after},"Root failed to publish exactly three AI words after finishing");
        // Source frame is transient. Subsequent writes cannot rewrite the saved
        // controller;the next initialization reads its last published values.
        initialized.Decision.Memory.Value0=123;
        Require(DeathMarkWorld.State(body).Control==after,"Transient frame leaked into persistent control");
    }
    internal static void Report()=>Log.Information("AI control:explicit three-word registration,legacy unknown refusal,snapshot isolation and root finish-before-publication pass on original three-species controllers;birth defaults and automatic scheduling remain gated.");
}
