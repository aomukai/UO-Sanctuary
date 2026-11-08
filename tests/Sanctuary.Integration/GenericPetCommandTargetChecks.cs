// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GenericPetCommandTargetChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetCommandTargetChecks));
    private sealed record Entry(uint Body,string Birth,int Life,int Mana,DeathMarkSavedActor Command);
    private sealed record Saved(uint[] Owners,uint Target,Entry[] Entries,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"pet-command-target-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int Id(IEntity entity)=>checked((int)entity.Serial.Value);
    private static string Birth(SkeletonGuardian body)=>body switch
    {WarlockGoatmanBody g=>g.SavedText,WarlockTaintedBody t=>t.SavedText,WarlockDefilerBody d=>d.SavedText,_=>throw new InvalidOperationException()};
    public static void Configure(){Register("pet-command-target-check",Check);Register("pet-command-target-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current command getter,live registry and owner-survey composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int N(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static bool B(JsonElement e,string key)=>e.GetProperty(key).GetBoolean();
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-command-target-cases.json")));int count=0,assertions=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<int[]>();
            var data=new GenericPetCommandData(N(p,"target"),N(p,"type"));
            var unit=B(p,"present")?new GenericPetCommandUnit(N(p,"kind"),742,1,B(p,"data")?data:null):null;
            var chain=p.GetProperty("chain").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var registry=new GenericPetRegistryServices((game,kind,id)=>
                {trace.Add([kind,game,id]);return B(p,"bucket")&&chain.Length>0?chain[0]:0;},_=>throw new InvalidOperationException("Tile registry used"),
                id=>N(p.GetProperty("ids"),id.ToString()),id=>{int at=Array.IndexOf(chain,id);return at+1<chain.Length?chain[at+1]:0;});
            int target=0;bool refused=false;
            try{target=GenericPetTargetCommand.Get(B(p,"game")?1:0,unit,(game,kind,id)=>GenericPetRegistry.Resolve(game,kind,id,registry));}
            catch(NotSupportedException){refused=true;assertions++;}
            Require(refused==B(expected,"unsupported")&&target==N(expected,"target"),"Current getter guard/type/registry chain result differs");
            var sourceTrace=expected.GetProperty("trace").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToArray();
            Require(trace.Count==sourceTrace.Length&&trace.Where((t,i)=>!t.SequenceEqual(sourceTrace[i])).Any()==false,"Current getter registry call order/arguments differ");
            Require(data.Target48==N(p,"target")&&data.Type4c==N(p,"type"),"Getter rewrote command fields");count++;
        }
        Require(count==35&&assertions==2,"Missing complete command-getter contexts");
    }
    // Install explicit fixture commands through the actual saved controller
    // representation. This is not a new live command/AI producer.
    private static void Command(SkeletonGuardian body,int type,int target)
    {
        var image=DeathMarkWorld.Capture();int id=Id(body);
        Require(image.Actors.Count(a=>a.Handle==id)==1,"Original controller registration missing");
        DeathMarkWorld.Restore(image with{Actors=image.Actors.Select(a=>a.Handle==id?a with{Type4c=type,Target48=target}:a).ToArray()});
    }
    private static GenericPetOwnerSurveyServices Binding(SkeletonGuardian[] bodies)
    {
        var metadata=bodies.ToDictionary(Id,b=>new GenericPetSurveyUnit(1,Id(b),true,true,true,null));
        var original=new GenericPetOwnerSurveyServices(id=>metadata[id],(_,_)=>true,
            _=>throw new InvalidOperationException("Unbound target service used"),(_,_,_)=>1,
            _=>throw new InvalidOperationException("Unexpected path fallback"));
        // Unit/validation/path services are explicit fixture inputs;only the
        // actual saved-command and live registry producers are admitted here.
        return DeathMarkWorld.BindTargets(original,new GenericPetWorldIdentity(71));
    }
    private static void Live(PlayerMobile[] owners,BaseCreature target,SkeletonGuardian[] bodies)
    {
        var binding=Binding(bodies);foreach(var body in bodies){Command(body,2,Id(target));}
        string before=JsonSerializer.Serialize(DeathMarkWorld.Capture());
        foreach(var body in bodies){Require(binding.CurrentTarget(Id(body))==Id(target),"Live original command did not resolve monster");}
        Require(JsonSerializer.Serialize(DeathMarkWorld.Capture())==before,"Read-only lookup changed controller state");
        var survey=new GenericPetOwnerSurveyState(Id(bodies[0]),2);
        GenericPetOwnerSurvey.Enumerate(1,survey,visit=>{foreach(var body in bodies){visit(Id(body));}},binding);
        Require(survey.Visits==3&&survey.Targets.SequenceEqual(new[]{new GenericPetSearchSwarm(Id(target),2)}),"Actual controller commands did not feed current owner survey");
        Command(bodies[1],1,Id(owners[1]));
        Require(binding.CurrentTarget(Id(bodies[1]))==Id(owners[1]),"Binding retained stale command dictionary or wrong player registry");
        Command(bodies[1],2,Id(owners[1]));Require(binding.CurrentTarget(Id(bodies[1]))==0,"Player resolved from monster registry");
        Command(bodies[1],1,Id(target));Require(binding.CurrentTarget(Id(bodies[1]))==0,"Monster resolved from player registry");
        foreach(int type in new[]{0,3,4})
        {Command(bodies[1],type,Id(target));Require(binding.CurrentTarget(Id(bodies[1]))==0,"Unsupported stored command type resolved");}
        var temporary=FireBoltChecks.Target(owners[0],6);int missing=Id(temporary);temporary.Delete();
        Command(bodies[1],2,missing);var stale=DeathMarkWorld.State(bodies[1]);
        Require(binding.CurrentTarget(Id(bodies[1]))==0&&DeathMarkWorld.State(bodies[1])==stale,"Absent command target resolved or stale command was cleared");
        Require(binding.CurrentTarget(Id(target))==0,"Unregistered actor inherited another command");
        // Keep both command kinds on the original controller across saves.
        Command(bodies[1],1,Id(owners[1]));
        foreach(var body in bodies.Where((_,i)=>i!=1)){Command(body,2,Id(target));}
        GenericPetInitializationChecks.Originals(owners,target,bodies);
        Require(!WarlockCombat.Implemented(CombatAbility.DeathMark),"Lookup admitted unfinished learned cast");
    }
    private static void Save(PlayerMobile[] owners,BaseCreature target,SkeletonGuardian[] bodies,int generation)
    {
        var entries=bodies.Select(b=>new Entry(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),b.Mana,DeathMarkWorld.State(b))).ToArray();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owners.Select(p=>p.Serial.Value).ToArray(),target.Serial.Value,entries,generation)));
    }
    private static void Check()
    {
        Source();GenericPetOwnerIteratorChecks.Source();var owners=new[]{ClassSkillChecks.Player(DiabloClass.Warlock),ClassSkillChecks.Player(DiabloClass.Warlock)};
        owners[1].Female=true;owners[1].Body=0x191;
        FixtureSpace.Corridor(owners[0],6400,40,3680);owners[1].MoveToWorld(new(6400,3682,0),Map.Trammel);
        var target=FireBoltChecks.Target(owners[0],12);
        SkeletonGuardian[] bodies=[WarlockGoatmanBody.CreatePrepared(owners[0],new(6401,3680,0),TimedEffects.Now),
            WarlockTaintedBody.CreatePrepared(owners[1],new(6403,3680,0),TimedEffects.Now),
            WarlockDefilerBody.CreatePrepared(owners[0],new(6405,3680,0),TimedEffects.Now)];
        foreach(var body in bodies){DeathMarkWorldChecks.Register(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-333);}
        var births=bodies.Select(Birth).ToArray();var life=bodies.Select(SkillHealth.Balance256).ToArray();
        Live(owners,target,bodies);
        Require(bodies.Select(Birth).SequenceEqual(births)&&bodies.Select(SkillHealth.Balance256).SequenceEqual(life),"Command reads healed or recaptured original demons");
        Save(owners,target,bodies,0);
        Log.Information("Command target:35complete-current getter contexts/two assertion boundaries;live original Goatman/Tainted/Defiler commands feed owner survey through actual role-qualified registry. Changing command dictionary is immediately observed;no target predicate/payment/movement/AI admission added.");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Extra command-target reload");
        var owners=saved.Owners.Select(id=>(PlayerMobile)World.FindMobile((Serial)id)).ToArray();
        var target=(BaseCreature)World.FindMobile((Serial)saved.Target);var bodies=saved.Entries.Select(e=>(SkeletonGuardian)World.FindMobile((Serial)e.Body)).ToArray();
        foreach(var e in saved.Entries)
        {
            var body=(SkeletonGuardian)World.FindMobile((Serial)e.Body);
            Require(Birth(body)==e.Birth&&SkillHealth.Balance256(body)==e.Life&&body.Mana==e.Mana&&DeathMarkWorld.State(body)==e.Command,"Original body/snapshot/wounds/command changed on load");
        }
        var binding=Binding(bodies);
        for(int i=0;i<bodies.Length;i++){Require(binding.CurrentTarget(Id(bodies[i]))==(i==1?Id(owners[1]):Id(target)),"Saved typed command did not resolve original live unit");}
        GenericPetInitializationChecks.Originals(owners,target,bodies);
        // Continue exercising registry reads after reload without moving the
        // saved actors or recapturing their prepared birth snapshots.
        Require(owners[1].Female&&!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Witch identity or unfinished summon gate changed");
        Save(owners,target,bodies,saved.Generation+1);
    }
}
