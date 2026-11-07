// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowOriginalEventChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowOriginalEventChecks));
    private sealed record Held(uint Actor,string Body,string[] Items,NpcEquipmentSeed Random,int Life,string Resources);
    private sealed record Saved(Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-original-events-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-original-events-check",false);Register("shadow-original-events-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Shadow original item-event registration through existing shared graph",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static Held Capture(AssassinShadowBody body)=>new(body.Serial.Value,body.SavedText,ShadowLoadout.Originals(body).Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.Require(body),SkillHealth.Balance256(body),Json(GeneratedNpcLiveResources.State(body)));
    private static void Dispatch(AssassinShadowBody body)
    {
        var state=body.EquipmentNumbers.ItemEvents.Capture();
        foreach(byte phase in state.Events.Entries.Select(r=>r.Event).Distinct())
        {
            var actual=new System.Collections.Generic.List<(int,int,int,int)>();
            body.EquipmentNumbers.ItemEvents.Trigger(phase,(f,p,a,b)=>{actual.Add((f,p,a,b));return 0;});
            Require(actual.SequenceEqual(state.Events.Entries.Where(r=>r.Event==phase).Select(r=>(r.Function,(int)r.Event,r.Guid1,r.Guid2))),"Shadow callback order differs from original shared registry");
        }
        Require(Json(body.EquipmentNumbers.ItemEvents.Capture())==Json(state),"Traversal changed saved registrations");
    }
    private static void Host(AssassinShadowBody body)
    {
        var before=Capture(body);var graph=OriginalNpcEquipment.Graph(body);
        Require(OriginalNpcEquipment.Owner(body)==body.Prepared.Owner&&OriginalNpcEquipment.Level(body)==body.Prepared.Capture.Level
            &&OriginalNpcEquipment.Binding(body)==ShadowLoadout.Binding(body).Identity
            &&body.SourceMonsterId==(body.Prepared.Capture.Ability==CombatAbility.ShadowMaster?"shadowmaster":"shadowwarrior"),"Shared host replaced Shadow source identity");
        foreach(int skill in new[]{36,38,39,44,48,49,53})
        {
            var actual=NpcItemSpellDamage.CaptureOriginal(body,skill,3);
            int mastery=graph.Read(skill==36?329:skill is 39 or 44?331:330);
            var expected=NpcItemSpellDamage.Calculate(skill,3,body.Prepared.Capture.Level,mastery,graph.Read(333),graph.Read(335),graph.Read(334));
            Require(actual==expected,"Shared item spell borrowed owner rank/mastery/piercing");
        }
        bool supported=true;
        foreach(var stat in graph.Stats.Where(s=>s.Value!=0))
        foreach(var row in ItemEventDefinitions.Bindings(stat.Stat)??[])
        {
            if(row.Event<=0){continue;}
            if(row.Function is 20 or 21)
            {
                try{if(stat.Stat is not(195 or 196 or 198 or 201)||(stat.Packed&63)==0){supported=false;}
                    else{_=NpcItemSpellDamage.Require((ushort)stat.Packed>>6);}}
                catch(NotSupportedException){supported=false;}
            }
            else if(row.Function is not(6 or 7 or 8 or 9 or 10 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 28 or 29)){supported=false;}
        }
        bool registered=false;
        try{Require(ReferenceEquals(NpcOriginalItemProcs.RequireRegistration(body),graph),"Registration replaced the actual Shadow graph");registered=true;}
        catch(NotSupportedException)when(!supported){}
        Require(registered==supported,"Shared registration admitted an unsupported callback or rejected supported ones");
        int allocations=0;var owner=(PlayerMobile)body.ControlMaster;var at=TimedEffects.Now;
        Require(!NpcItemSpellFlights.Attached(body,owner,owner.Map.MapID),"Parked Shadow became an attached spell source");
        foreach(Action cast in new Action[]{
            ()=>SorceressSpellMissiles.PrepareOriginalNpc(body,owner.Location,36,3,at,_=>{allocations++;return 1;}),
            ()=>ChainLightning.PrepareOriginalNpc(body,owner.Location,3,at,_=>{allocations++;return new(true,1);}),
            ()=>NpcOriginalItemProcs.Dispatch(body,null,"domeleeattack",new(0,0,0,0,1),at,_=>{allocations++;return new(true,1);})})
        {
            bool refused=false;try{cast();}catch(NotSupportedException){refused=true;}
            Require(refused,"Unqualified Shadow world cast bypassed admission");
        }
        Require(!NpcItemSpellCasts.CastOriginalPosition(body,owner.Location,36,3,1,at,_=>{allocations++;return new(true,1);})
            &&allocations==0&&Json(Capture(body))==Json(before),"Refused host work allocated,rewrote resources/items or consumed RNG");
    }
    private static void Check(bool reload)
    {
        var bodies=new System.Collections.Generic.List<AssassinShadowBody>();int generation=0;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=saved.Generation+1;
            foreach(var held in saved.Bodies)
            {
                var body=(AssassinShadowBody)World.FindMobile((Serial)held.Actor);Require(Json(Capture(body))==Json(held),"Original body/items/resources/passives/events/wounds/RNG changed on reload");bodies.Add(body);
            }
        }
        else
        {
            var owner=BladeBodyChecks.Fresh(6280,16,3500);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);ClassSkillChecks.Train(owner,"assassin.shadowwarrior",20);
            foreach(var ability in new[]{CombatAbility.ShadowWarrior,CombatAbility.ShadowMaster})for(int i=0;i<6;i++)
            {
                var body=AssassinShadowBody.CreateInitialized(owner,ability,TimedEffects.Now);var before=Capture(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)*2/3-1);int wounded=SkillHealth.Balance256(body);
                GeneratedNpcLiveResources.ActivateItemEvents(body);
                Require(body.EquipmentNumbers.ItemEvents!=null&&GeneratedNpcLiveResources.State(body).ItemEvents!=null&&SkillHealth.Balance256(body)==wounded,"Registration missing or healed wounds");
                Require(body.SavedText==before.Body&&ShadowLoadout.Originals(body).Select(x=>x.SavedText).SequenceEqual(before.Items)&&NpcEquipmentRandom.Require(body)==before.Random,"Registration rewrote original graph or RNG");
                bodies.Add(body);
            }
        }
        var world=NpcWorldAllocation.Capture();int entries=0;
        foreach(var body in bodies)
        {
            Require(body.Map==Map.Internal&&!((IPersistentMinion)body).CanActivate&&!NpcOriginalItemProcs.Active(body),"Registration accidentally exposed unfinished world/callback execution");
            var before=Capture(body);Dispatch(body);Require(Json(Capture(body))==Json(before),"Registry traversal consumed body resources or RNG");
            Host(body);
            entries+=body.EquipmentNumbers.ItemEvents.Capture().Events.Entries.Length;
            var item=GeneratedNpcLiveResources.State(body).ActiveItems[0];GeneratedNpcLiveResources.SetItemActive(body,item,false);GeneratedNpcLiveResources.SetItemActive(body,item,true);
            Require(body.EquipmentNumbers.ItemEvents.Capture().ActiveItems.Last()==item,"Original reattachment order not retained");
            var state=GeneratedNpcLiveResources.Snapshot();var original=state.Single(r=>r.Actor==body.Serial.Value);bool refused=false;
            try{GeneratedNpcLiveResources.Restore(state.Select(r=>r.Actor==body.Serial.Value?r with{ItemEvents=r.ItemEvents with{Owner=r.Actor}}:r).ToArray());}catch(InvalidDataException){refused=true;}
            Require(refused&&Json(GeneratedNpcLiveResources.State(body))==Json(original),"Foreign event history partially replaced live resources");
        }
        Require(NpcWorldAllocation.Capture()==world,"Registry transactions consumed world RNG");
        NpcWorldAllocationChecks.Checked(world);
        Require(entries>0,"Original generated Shadow equipment supplied no real event coverage");
        File.WriteAllText(Manifest,Json(new Saved(bodies.Select(Capture).ToArray(),generation)));
        Log.Information("Shadow original events:{Bodies} original bodies/{Entries} genuine registrations;shared traversal,reattachment,saved passives/resources/wounds/RNG;world/learned gates retain",bodies.Count,entries);
    }
}
