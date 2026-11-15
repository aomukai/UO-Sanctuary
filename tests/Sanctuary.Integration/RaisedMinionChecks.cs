// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class RaisedMinionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(RaisedMinionChecks));
    private const string CasterName="RaisedMinionFixtureCaster";
    private sealed record Saved(int Generation,uint Owner,uint Caster,uint Child,int Life256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"raised-minions-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    // Qualify the helper prerequisite independently of unimplemented native
    // Lich Revive admission. No substitute Revive handler is used or claimed.
    private sealed class CasterBehavior(GraveyardLich body):IPersistentMinion
    {
        public DiabloClass SummonClass=>DiabloClass.Necromancer;
        public void SuspendActions(){body.StopCombatActions();body.AIObject.AITimer.Stop();body.Blessed=true;}
        public void ResumeActions(){body.Blessed=false;}
    }
    public static void Configure()
    {
        PersistentMinions.RegisterBehavior(body=>body is GraveyardLich lich&&body.Name==CasterName?new CasterBehavior(lich):null);
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string label=reload?"raised-minions-reload-check":"raised-minions-check";
            ConsoleInputHandler.RegisterCommand(label,"Raised helper ownership and saved wounds",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Label}: passed",label);}catch(Exception e){Log.Error(e,"{Label}: failed",label);}}));
        }
        ConsoleInputHandler.RegisterCommand("raised-minions-live-check","Actual Lich raise timer with player allegiance",_=>Core.LoopContext.Post(()=>
        {FixtureNetwork.Connected("raised-minions-live-result",Live);Log.Information("raised-minions-live-check: passed");}));
    }
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="RaisedMinionFixtureOwner",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,50);ClassSkillChecks.Train(p,"necromancer.revive");
        FixtureSpace.Corridor(p,6745,30,2500);return p;
    }
    private static GraveyardLich Caster(PlayerMobile p,int offset=2)
    {
        var caster=new GraveyardLich{Name=CasterName,ControlSlots=0,NoKillAwards=true};Require(caster.SetControlMaster(p),"Fixture caster owner binding");
        caster.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);caster.AIObject.AITimer.Stop();PersistentMinions.Profiles.Track(caster);return caster;
    }
    private static void Clean(PlayerMobile p)
    {foreach(var body in p.AllFollowers?.ToArray()??Array.Empty<BaseCreature>()){body.Delete();}p.Delete();}
    private static void Check()
    {
        var p=Owner();var caster=Caster(p);var child=new GraveyardSkeleton();
        // Binding requires a connected owner; this fixture temporarily uses a
        // real NetState only in Live. A saved graph is captured there instead.
        Require(!RaisedMinions.Attach(caster,child)&&!RaisedMinions.CanRaise(caster),"Offline caster created an active helper");
        Require(!child.Controlled&&RaisedMinions.State(child)==null,"Rejected helper retained ownership");child.Delete();Clean(p);
        foreach(var bad in new[]{new RaisedMinionState(1,1,2,DiabloClass.Necromancer,MonsterDifficulty.Normal),
            new RaisedMinionState(1,2,3,(DiabloClass)0,MonsterDifficulty.Normal)})
        {
            var before=RaisedMinions.Snapshot();bool rejected=false;try{RaisedMinions.Restore([bad]);}catch(InvalidDataException){rejected=true;}
            Require(rejected&&RaisedMinions.Snapshot().SequenceEqual(before),"Invalid child graph changed live state");
        }
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        var caster=(GraveyardLich)World.FindMobile((Serial)saved.Caster);var child=(GraveyardUndead)World.FindMobile((Serial)saved.Child);
        Require(caster is{Deleted:false,Alive:true}&&child is{Deleted:false,Alive:true}&&child.Map==Map.Internal&&child.ControlMaster==p
            &&RaisedMinions.State(child)is{} row&&row.Caster==saved.Caster&&row.Owner==saved.Owner&&row.SavedLife256==null
            &&SkillHealth.Balance256(child)==saved.Life256&&PersistentMinions.Profiles.Find(child).ExpiresUtc==null,"Saved helper identity,owner,wounds or lifetime changed");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
    private static Corpse Body(PlayerMobile p,int offset)
    {
        var victim=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};victim.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);victim.AIObject.AITimer.Stop();
        SkillCombat.HitParts(p,victim,new(10000,0,0,0,0),recover:false);var corpse=(Corpse)victim.Corpse;corpse.DropItem(new Gold(17));return corpse;
    }
    private static void Live(NetState network)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"raised-minions-wire-");var caster=Caster(p);var corpse=Body(p,5);
        var wild=new GraveyardLich{NoKillAwards=true};wild.MoveToWorld(new(p.X+14,p.Y,p.Z),p.Map);wild.AIObject.AITimer.Stop();var wildCorpse=Body(p,18);
        long deadline=Core.TickCount+12000;bool started=false,done=false;uint[] before=RaisedMinions.Snapshot().Select(row=>row.Actor).ToArray();
        void Cleanup(){wild.Delete();corpse.Delete();wildCorpse.Delete();Clean(p);account.Delete();}
        void Inspect()
        {
            try
            {
                if(done){return;}Require(Core.TickCount<deadline,$"Actual Lich raise timeout:started={started},canRaise={caster.CanRaise(corpse)},ownerGate={RaisedMinions.CanRaise(caster)},bodyGate={RaisedMinions.CorpseAllowed(caster,corpse)},owner={p.Alive}/{p.Map}/{p.NetState!=null},caster={caster.Alive}/{caster.Location}/{caster.Map}/{caster.Frozen}/{caster.Paralyzed}/{caster.ControlMaster==p},named={NamedControl.SpecialAllowed(caster)},corpse={corpse.Location}/{corpse.Map}/{CorpseSkillRules.BodyAvailable(corpse)}/{corpse.SanctuaryUndeadKind}/{corpse.CanLoot(p,null)}/{corpse.IsCriminalAction(p)},select={CorpseSkillRules.Available(corpse)},see={caster.CanSee(corpse)}/{caster.InLOS(corpse)},spawn={p.Map.CanSpawnMobile(corpse.Location)},rows={JsonSerializer.Serialize(RaisedMinions.Snapshot())}");
                if(!started&&caster.BeginRaise(corpse)){Require(wild.BeginRaise(wildCorpse),"Wild control did not start");started=true;Log.Information("raised-minions-live: raise animations started at {Caster}/{Corpse}",caster.Location,corpse.Location);}
                var child=RaisedMinions.Snapshot().Where(row=>row.Caster==caster.Serial.Value).Select(row=>World.FindMobile((Serial)row.Actor)).OfType<GraveyardUndead>().SingleOrDefault();
                if(child!=null)
                {
                    Require(started&&child.ControlMaster==p&&child.Controlled&&!child.Summoned&&child.Raised&&child.NoKillAwards&&child.ControlSlots==0
                        &&ClassCombat.Friendly(child,p)&&!child.CanBeHarmful(p,false)&&corpse.SanctuaryConsumed&&corpse.Items.OfType<Gold>().Single().Amount==17,"Actual raise lost allegiance,loot or owner");
                    GraveyardUndead wildChild=null;foreach(var body in wild.Map.GetMobilesInRange<GraveyardUndead>(wildCorpse.Location,0))
                    {if(body!=wild&&body.Alive){Require(wildChild==null,"Wild control produced duplicate helpers");wildChild=body;}}
                    if(wildChild==null){Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));return;}
                    Require(!wildChild.Controlled&&RaisedMinions.State(wildChild)==null&&wildCorpse.SanctuaryConsumed,"Wild raise behavior changed");wildChild.Delete();
                    Require(child.IsEnemy(wild)&&!child.IsEnemy(p)&&child.CanBeHarmful(wild,false),"Owned helper retained wild allegiance");
                    var facets=SummonDifficulties.Facets();int wounded=SkillHealth.Maximum256(child)-257;SkillHealth.SetBalance256(child,wounded);int oldMaximum=SkillHealth.Maximum256(child);
                    try
                    {
                        SummonDifficulties.SetFacets([new(p.Map.MapID,MonsterDifficulty.Nightmare)]);SummonDifficulties.Sync(p);
                        Require(MonsterVariants.Difficulty(child)==MonsterDifficulty.Nightmare&&SkillHealth.Maximum256(child)!=oldMaximum
                            &&SkillHealth.Balance256(child)==SummonDifficulties.Life256(wounded,oldMaximum,SkillHealth.Maximum256(child)),"Facet rescale lost helper wounds");
                    }
                    finally{SummonDifficulties.SetFacets(facets);SummonDifficulties.Sync(p);}
                    int life=SkillHealth.Maximum256(child)-257;SkillHealth.SetBalance256(child,life);PersistentMinions.Suspend(p);
                    Require(child.Map==Map.Internal&&SkillHealth.Balance256(child)==life,"Parking healed helper");PersistentMinions.Resume(p);
                    Require(child.Map==p.Map&&SkillHealth.Balance256(child)==life,"Resume reset helper");
                    // A second helper's deletion must follow its actual caster,
                    // without changing an unrelated caster's saved child.
                    var other=Caster(p,8);var disposable=new GraveyardZombie();Require(RaisedMinions.Attach(other,disposable),"Second helper bind");
                    disposable.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);other.Delete();Require(disposable.Deleted&&!child.Deleted,"Caster removal lost dependency isolation");
                    caster.Delete();Require(child.Deleted&&!RaisedMinions.Snapshot().Any(row=>!before.Contains(row.Actor)),"Caster deletion left an orphan");
                    // Retain one independently bound original helper to test
                    // the actual save/load and offline parking graph twice.
                    caster=Caster(p);child=new GraveyardSkeleton();Require(RaisedMinions.Attach(caster,child),"Saved helper bind");child.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
                    life=SkillHealth.Maximum256(child)-257;SkillHealth.SetBalance256(child,life);PersistentMinions.Suspend(p);
                    var old=File.Exists(Manifest)?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):null;
                    if(old==null){File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,caster.Serial.Value,child.Serial.Value,life)));}
                    else{caster.Delete();Clean(p);account.Delete();}
                    wild.Delete();corpse.Delete();wildCorpse.Delete();done=true;Log.Information("raised-minions-live-result: passed");return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"raised-minions-live-result: failed");}
        }
        Inspect();
    }
}
