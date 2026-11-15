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

public static class BindDemonHostAuditChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonHostAuditChecks));
    private sealed record NativeRow(string Type,string Reference,bool? ExplicitDemon,bool SlayerDemon,bool Boss,string Issue);
    private sealed record SunderRow(string Id,string Base,bool RandomSpawnable,bool Available);
    private sealed record Audit(NativeRow[] Demons,SunderRow[] SunderSources,int CreatureDefinitions);
    private sealed record Saved(int Generation,uint Owner,uint Bound,uint Paragon,BoundDemonState State,int Life,int Mana,Audit Inventory);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bind-demon-host-audit-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BindHostAuditFixture"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Creating||m.Name=="BindHostAuditFixture"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bind-demon-host-audit-reload-check":"bind-demon-host-audit-check";
            ConsoleInputHandler.RegisterCommand(name,"Actual native demon-source inventory/excluded charms/elite refusal",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static Audit Inventory()
    {
        var slayer=SlayerGroup.GetEntryByName(SlayerName.Exorcism);var rows=CreatureProfiles.All.OrderBy(r=>r.Type,StringComparer.Ordinal)
            .Select(row=>(Row:row,Type:typeof(Daemon).Assembly.GetType(row.Type)))
            .Where(x=>x.Type is{IsAbstract:false}&&typeof(BaseCreature).IsAssignableFrom(x.Type))
            .Select(x=>
            {
                Require(CreatureProfiles.TryVariant(x.Row.Type,MonsterDifficulty.Normal,out var p),"Missing native Normal profile");
                bool native=slayer.Types.Any(t=>t.IsAssignableFrom(x.Type));
                if(!(p.Traits.Demon??native)){return null;}
                var pet=BindDemonPrepared.Catalog().SingleOrDefault(r=>r.Monster==p.Rewards.Reference);string issue=null;
                if(p.Traits.Boss==true){issue="Native elite quality requires an adapter";}
                else if(pet==null){issue="No pinned Bind species row";}
                else if(pet.IndexScope!="MonsterClass"){issue="Named source requires an adapter";}
                else if(pet.UnderlingCount!=0){issue="Underling ownership requires an adapter";}
                else foreach(var d in Enum.GetValues<MonsterDifficulty>())
                {if(!CreatureProfiles.TryVariant(x.Row.Type,d,out var v)||!v.Rewards.Enabled||v.Rewards.Reference!=pet.Monster||d!=MonsterDifficulty.Normal&&v is not{Enabled:true,ApplyStats:true,Life:>0}){issue="Incomplete difficulty projection";break;}}
                return new NativeRow(x.Row.Type,p.Rewards.Reference,p.Traits.Demon,native,p.Traits.Boss==true,issue);
            }).Where(x=>x!=null).ToArray();
        Require(rows.Any(r=>r.Type==typeof(Daemon).FullName&&r.Issue==null)&&rows.Any(r=>r.Type==typeof(Gargoyle).FullName&&r.Issue=="No pinned Bind species row"),"Native roster controls differ");
        var sunder=NamedGear.Catalog.Items.Where(i=>i.Properties.Any(p=>p.Code.StartsWith("pierce-immunity-",StringComparison.Ordinal))).ToArray();
        Require(sunder.Length==18,$"Expected eighteen Sunder source variants,found {sunder.Length}");
        Require(!sunder.Any(i=>NamedGear.Available(i.Id)),"Available named Sunder source: "+string.Join(',',sunder.Where(i=>NamedGear.Available(i.Id)).Select(i=>i.Id)));
        Require(sunder.Count(i=>i.BaseCode=="cm3")==12&&sunder.Count(i=>i.BaseCode=="cs2"&&!i.SourceSpawnable)==6,"Sunder charm versus recipe-base classification changed");
        var encounter=new EncounterCreature();try{Require(CreatureProfiles.For(encounter).Traits is{Undead:true,Demon:false}&&!NamedCombat.Demon(encounter),"Current encounter class became a demon");}finally{encounter.Delete();}
        Log.Information("Bind host audit:native type inventory uses current profiles and exact Slayer type rules without constructing roster;source compatibility is not live eligibility;all eighteen named Sunder providers unavailable,twelvecm3andsixrecipe-onlycs2;existing encounters explicitly undead/not demon");
        return new(rows,sunder.Select(i=>new SunderRow(i.Id,i.BaseCode,i.SourceSpawnable,NamedGear.Available(i.Id))).OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray(),CreatureProfiles.All.Count);
    }
    private static Daemon Body(PlayerMobile p,int offset)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}
        b.Name="BindHostAuditFixture";b.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();return b;
    }
    private static void Refuse(PlayerMobile p,Daemon target,Daemon existing)
    {
        var normal=MonsterVariantBaselines.Snapshot();int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);var ai=target.AIObject;var point=target.Location;var serial=target.Serial;
        Require(!BoundDemonHosts.Qualify(p,target,out _,out var why)&&why.Contains("Paragon")
            &&!BoundDemonHosts.BindPrepared(p,target,out why)&&why.Contains("Paragon")
            &&!BindDemonChannel.BeginPrepared(p,target,TimedEffects.Now,out why)&&why.Contains("Paragon"),"Native Paragon silently treated as ordinary Bind quality");
        _=ReviveCasts.NativeStatus(target,out var revive);Require(revive=="Paragon Revive classification is not configured.","Shared existing Revive classification changed");
        Require(!target.Controlled&&target.IsParagon&&target.Serial==serial&&ReferenceEquals(target.AIObject,ai)&&target.Location==point
            &&SkillHealth.Balance256(target)==life&&NecromancerEffects.ManaBalance256(p)==mana&&MonsterVariantBaselines.Snapshot().SequenceEqual(normal)
            &&BoundDemonHosts.For(p).Single()==existing,"Rejected native quality mutated resources/body/baselines or replaced existing demon");
    }
    private static void Scene(NetState network)
    {
        var inventory=Inventory();Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="BindHostAuditFixture";p.AddItem(new Backpack());FixtureSpace.Corridor(p,6950,24,3500);var account=FixtureNetwork.Authenticate(network,p,"bind-host-audit-");
        Daemon body=null,paragon=null;bool kept=false;
        try
        {
            body=Body(p,4);Require(BoundDemonHosts.BindPrepared(p,body,out var why),"Ordinary native control: "+why);body.AIObject.AITimer.Stop();
            paragon=Body(p,8);paragon.IsParagon=true;Require(paragon.IsParagon,"Real native Paragon transform failed");SkillHealth.SetBalance256(paragon,SkillHealth.Maximum256(paragon)/2-7);Refuse(p,paragon,body);
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,paragon.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),NecromancerEffects.ManaBalance256(p),inventory)));kept=true;
            Log.Information("Bind host audit:real Paragon flag/transform refuses ordinary-quality host/channel without payment,AI/body/baseline mutation or existing pet replacement;shared Revive issue unchanged;ordinary original demon remains supported");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){body?.Delete();paragon?.Delete();p.Delete();BoundDemonHosts.Tick();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many host-audit reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(Daemon)World.FindMobile((Serial)saved.Bound);var paragon=(Daemon)World.FindMobile((Serial)saved.Paragon);
        Require(BoundDemonHosts.State(body)==saved.State&&SkillHealth.Balance256(body)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&paragon.IsParagon&&!paragon.Controlled,"Original bound state/resources or wild Paragon flag changed on reload");
        var account=FixtureNetwork.Authenticate(network,p,"bind-host-audit-reload-");
        try
        {
            PersistentMinions.Resume(p);body.AIObject.AITimer.Stop();paragon.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);paragon.AIObject.AITimer.Stop();Refuse(p,paragon,body);PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,State=BoundDemonHosts.State(body),Life=SkillHealth.Balance256(body),Mana=NecromancerEffects.ManaBalance256(p)}));
            Log.Information("Bind host audit:original binding/resources and real Paragon flag persist;reload refusal retains previous pet/identity with no debit;wild regeneration is not claimed to be parked");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
