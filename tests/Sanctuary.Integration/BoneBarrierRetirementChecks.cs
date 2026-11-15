// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoneBarrierRetirementChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoneBarrierRetirementChecks));
    private sealed record Actor(uint Id,string Type,uint Root,int Life,BarrierThinkState Clock,BarrierRetirementState Retirement);
    private sealed record Saved(int Generation,uint Owner,Actor[] Actors,Actor[] Expiring=null);
    private static readonly Dictionary<uint,BarrierRetirementState> Loaded=new();
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bone-barrier-retirement-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>!(m.Name?.StartsWith("BarrierRetirement",StringComparison.Ordinal)==true)&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>!(m.Name?.StartsWith("BarrierRetirement",StringComparison.Ordinal)==true)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        BoneBarrierRetirement.Restored=(body,state)=>Loaded[body.Serial.Value]=state;
        ConsoleInputHandler.RegisterCommand("bone-barrier-retirement-save","Save actual in-progress collapse alongside inspected states",_=>Core.LoopContext.Post(()=>
        {try{Save();Log.Information("bone-barrier-retirement-save: passed");}catch(Exception e){Log.Error(e,"bone-barrier-retirement-save: failed");}}));
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"bone-barrier-retirement-reload-check":"bone-barrier-retirement-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared barrier formation retirement and saved deadlines",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Scene();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        }
        foreach(string choice in new[]{"prison-right","wall-quick"})
        {
            string mode=choice,label="barrier-retirement-"+mode;
            ConsoleInputHandler.RegisterCommand(label+"-check","Real barrier cast and automatic collapse completion",_=>Core.LoopContext.Post(()=>
            {FixtureNetwork.Connected(label+"-result",n=>Input(n,mode));Log.Information("{Name}: passed",label+"-check");}));
        }
    }
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="BarrierRetirementFixture",Player=true,Body=0x190};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"necromancer.bonewall",20);ClassSkillChecks.Train(p,"necromancer.boneprison",20);
        FixtureSpace.Corridor(p,6400,40,2700);return p;
    }
    private static BoneBarrierSegment[] Group(PlayerMobile p,CombatAbility ability,int x)
    {
        var root=BoneBarrierSegment.SpawnRetiring(p,ability,new(p.X+x,p.Y,p.Z),SkillMechanics.Values(p,ability),null);
        var child=BoneBarrierSegment.SpawnRetiring(p,ability,new(p.X+x+1,p.Y,p.Z),null,root);
        var far=BoneBarrierSegment.SpawnRetiring(p,ability,new(p.X+x+20,p.Y,p.Z),null,root);
        return new[]{root,child,far};
    }
    private static BoneBarrierSegment[] Parts(PlayerMobile p)
    {var result=new List<BoneBarrierSegment>();foreach(var b in p.Map.GetMobilesInRange<BoneBarrierSegment>(p.Location,40)){if(b.SummonMaster==p){result.Add(b);}}return result.ToArray();}
    private static void Fire(GraveyardLich caster,PlayerMobile p)
    {
        long id=WeaponMissiles.LaunchHostileFirebolt(caster,p.Location,256,256);Require(id>0,"Real hostile firebolt failed to launch");
        var flight=WeaponMissiles.Snapshot().Flights.Single(n=>n.Id==id);
        WeaponMissiles.Tick(flight.NextUtc.AddMilliseconds(flight.Frames*40L),onlyOwner:caster.Serial.Value);
        Require(!WeaponMissiles.Snapshot().Flights.Any(n=>n.Id==id),"Real hostile flight failed to complete");
    }
    private static void Retired(BoneBarrierSegment[] group,DateTime at,string cause)
    {
        foreach(var b in group)
        {Require(!b.Deleted&&!b.Alive&&!b.BlocksFit&&!b.CanBeDamaged()&&b.Tiles.Count==0&&b.Corpse==null&&((IBarrierRetirement)b).Retirement==new BarrierRetirementState(group[0].Serial.Value,at,at.AddMilliseconds(600),cause),"Formation death/collision/deadline mismatch");}
    }
    private static void Scene()
    {
        Require(!BoneBarrierRetirement.Approved&&BoneBarrierRetirement.DurationMs==600,"Unapproved barrier rule enabled or DT15 clock changed");
        var p=Fresh();var groups=new List<BoneBarrierSegment[]>();bool kept=false;
        try
        {
            var original=BoneBarrierSegment.Spawn(p,CombatAbility.BoneWall,new(p.X+2,p.Y,p.Z),SkillMechanics.Values(p,CombatAbility.BoneWall),null);
            Require(original is BoneWallThinkingBody and not IBarrierRetirement,"Prepared rule changed ordinary learned allocation");original.Delete();
            foreach(var ability in new[]{CombatAbility.BoneWall,CombatAbility.BonePrison})
            {
                var group=Group(p,ability,5);groups.Add(group);var root=group[0];var child=group[1];var at=TimedEffects.Now;
                bool refused=false;try{BoneBarrierRetirement.Begin(child,DateTime.SpecifyKind(at,DateTimeKind.Unspecified),"damage");}catch(InvalidDataException){refused=true;}
                Require(refused&&group.All(b=>b.Alive&&b.Tiles.Count==1&&((IBarrierRetirement)b).Retirement==null),"Invalid clock partly killed a formation");
                var caster=new GraveyardLich{Name="BarrierRetirementCaster",Frozen=true,CantWalk=true,NoKillAwards=true};caster.MoveToWorld(new(p.X+14,p.Y,p.Z),p.Map);caster.AIObject?.AITimer.Stop();
                int hp=SkillHealth.Balance256(p);var lives=group.Select(SkillHealth.Balance256).ToArray();
                Fire(caster,p);Require(SkillHealth.Balance256(p)==hp&&group.Where((b,i)=>SkillHealth.Balance256(b)<lives[i]).Any(),"Live candidate formation failed to intercept real hostile damage");
                var tiles=group.SelectMany(b=>b.Tiles).ToArray();
                Require(!Server.Movement.Movement.CheckMovement(p,p.Map,new(root.X-1,root.Y,root.Z),Direction.East,out _),"Living barrier did not block actual movement");
                child.Kill();var state=((IBarrierRetirement)child).Retirement;Require(state!=null,"Actual child Kill did not enter prepared death");Retired(group,state.EnteredUtc,"damage");
                BoneBarrierRetirement.Begin(root,state.EnteredUtc.AddMilliseconds(100),"source-ai");Retired(group,state.EnteredUtc,"damage");
                Require(tiles.All(t=>t.Deleted),"Retirement retained actual collision items");
                Require(group.All(b=>p.Map.CanFit(b.Location,16,true,true,false))&&Server.Movement.Movement.CheckMovement(p,p.Map,new(root.X-1,root.Y,root.Z),Direction.East,out _),"Collapse did not immediately release native movement/placement");
                Fire(caster,p);Require(SkillHealth.Balance256(p)<hp,"Retiring formation still intercepted real hostile damage");caster.Delete();
                Require(BoneBarrierRetirement.Advance(root,(IBarrierRetirement)root,state.CompletesUtc.AddTicks(-1))&&!root.Deleted,"Collapse removed before completion");
                Require(BoneBarrierRetirement.Advance(root,(IBarrierRetirement)root,state.CompletesUtc)&&group.All(b=>b.Deleted),"Formation completion leaked root/near/far child");
            }
            // Inspect saved in-progress payloads at a future timestamp, rather
            // than extending gameplay retirement or racing a save against600ms.
            groups.Clear();var actors=new List<Actor>();var held=TimedEffects.Now.Add(FixtureSpace.Retention);
            foreach(var ability in new[]{CombatAbility.BoneWall,CombatAbility.BonePrison})
            {
                var group=Group(p,ability,ability==CombatAbility.BoneWall?5:10);groups.Add(group);
                foreach(var b in group){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);}
                BoneBarrierRetirement.Begin(group[0],held,"source-ai");Retired(group,held,"source-ai");
                actors.AddRange(group.Select(b=>new Actor(b.Serial.Value,b.GetType().Name,b.Root?.Serial.Value??0,SkillHealth.Balance256(b),((IBarrierThinkClock)b).Clock,((IBarrierRetirement)b).Retirement)));
            }
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,actors.ToArray())));kept=true;
            Log.Information("Barrier retirement:default gate retained,DT15uses shared scheduler,actual child death collapses whole root/near/far formation,immediate collision release,idempotent deadline,strict completion and UTC refusal pass;save payloads are inspected future fixtures");
        }
        finally{if(!kept){foreach(var group in groups)foreach(var b in group){b.Delete();}p.Delete();}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Actors.Length==6,"Missing saved formations");
        foreach(var a in saved.Actors)
        {
            var b=World.FindMobile((Serial)a.Id) as BoneBarrierSegment;
            Require(b is{Deleted:false,Alive:false}&&b.GetType().Name==a.Type&&(b.Root?.Serial.Value??0)==a.Root
                &&b.SummonMaster?.Serial.Value==saved.Owner&&SkillHealth.Balance256(b)==a.Life&&b.Tiles.Count==0
                &&((IBarrierThinkClock)b).Clock==a.Clock&&((IBarrierRetirement)b).Retirement==a.Retirement,$"Saved formation mismatch: id={a.Id},exists={b!=null},type={b?.GetType().Name}/{a.Type},alive={b?.Alive},root={b?.Root?.Serial.Value??0}/{a.Root},owner={b?.SummonMaster?.Serial.Value}/{saved.Owner},life={(b==null?-1:SkillHealth.Balance256(b))}/{a.Life},tiles={b?.Tiles.Count},clock={JsonSerializer.Serialize((b as IBarrierThinkClock)?.Clock)}/{JsonSerializer.Serialize(a.Clock)},retirement={JsonSerializer.Serialize((b as IBarrierRetirement)?.Retirement)}/{JsonSerializer.Serialize(a.Retirement)}");
        }
        Require(saved.Expiring is{Length:6}&&saved.Expiring.All(a=>Loaded.GetValueOrDefault(a.Id)==a.Retirement&&World.FindMobile((Serial)a.Id)==null),"Naturally saved pending collapse failed exact load/overdue cleanup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
        Log.Information("Barrier retirement:original inspected roots,near/far links,wounds,AI clocks/deadlines retain;actual600ms in-progress save states load unchanged and automatically finish overdue without collision/growth reset");
    }
    private static void Save()
    {
        // Prepare retained fixtures first, then save actual newly entered
        //600ms collapse. No future timestamp or extended deadline here.
        BoneWallMakerChecks.StageSave();BoneBarrierBirthChecks.PrepareSave();
        var p=Fresh();var actors=new List<Actor>();
        foreach(var ability in new[]{CombatAbility.BoneWall,CombatAbility.BonePrison})
        {
            var group=Group(p,ability,ability==CombatAbility.BoneWall?5:10);
            foreach(var b in group){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);}
            BoneBarrierRetirement.Begin(group[0],TimedEffects.Now,"source-ai");
            actors.AddRange(group.Select(b=>new Actor(b.Serial.Value,b.GetType().Name,b.Root?.Serial.Value??0,SkillHealth.Balance256(b),((IBarrierThinkClock)b).Clock,((IBarrierRetirement)b).Retirement)));
        }
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Expiring=actors.ToArray()}));World.Save();
    }
    private static void Input(NetState network,string mode)
    {
        bool wall=mode=="wall-quick";var ability=wall?CombatAbility.BoneWall:CombatAbility.BonePrison;var p=Fresh();
        var account=FixtureNetwork.Authenticate(network,p,"barrier-retire-wire-");
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Barrier binding");
        var target=new GraveyardSkeleton{Name="BarrierRetirementTarget",Frozen=true,CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,200*256+17);
        var allocation=BoneBarriers.Allocate;BoneBarriers.Allocate=(owner,a,point,values,root)=>owner==p?BoneBarrierSegment.SpawnRetiring(owner,a,point,values,root):allocation(owner,a,point,values,root);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;string label="barrier-retirement-"+mode;
        bool began=false,released=false,done=false;BoneBarrierSegment[] parts=null;BarrierRetirementState death=null;long deadline=Core.TickCount+35000;
        void Cleanup(){BoneBarriers.Allocate=allocation;ClassCombat.Stop(p);if(parts!=null)foreach(var b in parts){b.Delete();}target.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Natural barrier retirement timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==ability){Require(NecromancerEffects.ManaBalance256(p)==mana,"Barrier paid before release");began=true;Log.Information("{Label}: windup",label);}
                if(!released&&Parts(p).Length>0)
                {
                    parts=Parts(p);
                    Require(began&&parts.All(b=>b is IBarrierRetirement)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Candidate release/payment");released=true;BoneBarriers.Allocate=allocation;
                    if(!wall){parts.First(b=>b.Root!=null).Kill();}
                }
                if(released&&death==null)
                {
                    // Wall must get its naturally grown children before expiry.
                    if(wall){parts=Parts(p);}
                    death=parts.Select(b=>((IBarrierRetirement)b).Retirement).FirstOrDefault(x=>x!=null);
                    if(death!=null)
                    {Require(parts.Length>1&&parts.All(b=>!b.Alive&&b.Tiles.Count==0&&((IBarrierRetirement)b).Retirement==death),"Natural formation collapse mismatch");Require(wall?death.Cause=="source-ai":death.Cause=="damage","Wrong natural death cause");Log.Information("{Label}: collapse",label);}
                }
                if(death!=null&&parts.All(b=>b.Deleted))
                {Require(TimedEffects.Now>=death.CompletesUtc&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Natural collapse early/payment replay");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Necromancer,target.Serial.Value,target.X,target.Y,target.Z);Inspect();}));
    }
}
