// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcItemSkillChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemSkillChecks));
    private sealed record Saved(uint Actor,uint Owner,uint Item,string Payload,ShadowPassiveFrame Frame,EquipmentStatValue[] Numeric,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-skills-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action run){try{run();}catch(Exception e)when(e is NotSupportedException or InvalidDataException or ArgumentException){return;}throw new Exception("Unsupported NPC skill context accepted");}
    private static ShadowSkillBook Empty()=>ShadowSkillBook.Restore(Json(new ShadowSkillBookState([new(0,1,0,5)],0,0)));
    public static void Configure(){Register("npc-item-skills-check",false);Register("npc-item-skills-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"NPC oskill lifecycle and original unique item/reload",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void SourceControls()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-skill-grant-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var a=c.GetProperty("Args").EnumerateArray().Select(x=>x.GetInt32()).ToArray();int stat=a[0],id=a[1];var book=Empty();
            if(a[2]>=0&&id!=0){book.Assign(id,a[2]);book.SetBonus(id,a[3]);if(id==266){book.SetMode(id,14);}}
            if(book.Find(id)!=null){if((a[9]&1)!=0){book.Select(id,false);}if((a[9]&2)!=0){book.Select(id,true);}if((a[9]&4)!=0){book.Use(id);}}
            var trace=new List<int[]>();int Read(int s,int layer)=>s switch{127=>a[4],107=>a[5],126=>a[6],97=>a[7],_=>throw new Exception("Unexpected item skill rank stat")};
            book.ItemGrant(stat,id,a[8],Read,k=>trace.Add([k,book.Find(k)?.Base??-999]),true);
            var actual=book.Snapshot();Require(Json(actual.Entries.Select(e=>new[]{e.Id,e.Base,e.Bonus,e.Mode}))==c.GetProperty("Entries").GetRawText(),"Complete source NPC item skill entries/order/mode differ");
            Require(Json(new[]{actual.Left??-1,actual.Right??-1,actual.Used??-1})==c.GetProperty("Selection").GetRawText()&&Json(trace)==c.GetProperty("Refresh").GetRawText(),"Complete source NPC refresh/selection trace differs");count++;
        }
        Require(count==4096,"Missing whole callback/source-writer cases");
        Require(ShadowSkillBook.Metadata(362).Name==null,"Unknown current NPC row name was invented");
        foreach(int id in Enumerable.Range(0,429))
        {var b=Empty();b.Assign(id,0);Require(ShadowSkillBook.Restore(b.Save()).Find(id).Mode==(id==0?5:ShadowSkillBook.Metadata(id).Mode),"Current NPC metadata/raw monster mode did not roundtrip");}
        var passive=World.Mobiles.Values.OfType<AssassinShadowBody>().First(b=>b.Alive&&!b.Deleted);
        var graph=new GeneratedNpcStatGraph(passive,[],_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},activeOrder:[]);
        graph.Passives=ShadowPassiveGraph.Initialize(graph,Empty().Save(),()=>0,true);
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,252),5,graph.Actor);
        var frame=graph.Passives.Capture();Require(frame.ItemSkillCallbacks&&ShadowSkillBook.Restore(frame.Book).Find(252)is{Base:0}
            &&frame.Lists.Single().Level==5&&graph.Read(342,68)==70&&graph.Passives.GraphicsNotifications==2,"Base1capped oskill then base0full oskill did not execute actual passive refresh");
        var selected=ShadowSkillBook.Restore(frame.Book);selected.Select(252,false);selected.Select(252,true);selected.Use(252);graph.Passives.Refresh(selected.Save());
        graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,252),0,graph.Actor);var removed=ShadowSkillBook.Restore(graph.Passives.Capture().Book);
        Require(removed.Find(252)==null&&removed.Left==0&&removed.Right==0&&removed.Used==null&&!graph.Passives.State(152)&&graph.Read(342,68)==0,"Real callback removal left passive/selection state");
        string before=removed.Save();Reject(()=>removed.ItemGrant(97,36,2,(_,_)=>0,_=>{},false));Require(removed.Save()==before,"Unsupported active aura/channel context changed the book");
        Reject(()=>graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,37),1,graph.Actor));Require(graph.Passives.Capture().Book==before,"Unsupported foreign passive changed owned skill state");
    }
    internal static void FreshShadow(AssassinShadowBody body)
    {
        var frame=GeneratedNpcLiveResources.State(body).Passives;Require(frame is{ItemSkillCallbacks:true}&&frame.Order.Count(o=>o.Birth)==1,"Fresh Shadow omitted original item callbacks/birth order marker");
        int birth=Array.FindIndex(frame.Order,o=>o.Birth);
        Require(birth==(body.Prepared.Capture.Ability==CombatAbility.ShadowMaster?2:0),"Source Master passive initialization did not precede do49birth helper");
    }
    private static GeneratedNpcStatGraph Build(SkeletonGuardian b,ShadowPassiveFrame restore=null)
    {
        var graph=new GeneratedNpcStatGraph(b,restore==null?[]:[new(EquipmentStatKey.Pack(97,36),5)],_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},beforeReplay:restore==null?g=>g.Passives=ShadowPassiveGraph.Initialize(g,Empty().Save(),()=>0,true):null);
        if(restore!=null){graph.Passives=ShadowPassiveGraph.Restore(graph,restore,()=>0);}return graph;
    }
    private static void Check(bool reload)
    {
        SourceControls();var world=NpcWorldAllocation.Capture();Saved saved;
        if(reload)
        {
            saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)saved.Actor)as SkeletonGuardian;Require(b!=null,"Original NPC skill item actor did not load");b.AIObject?.AITimer.Stop();
            var item=World.FindItem((Serial)saved.Item)as GeneratedNpcItem;Require(item is{Deleted:false}&&item.Parent==b&&item.SavedText==saved.Payload,"Original unique skill item changed on load");
            var graph=Build(b,saved.Frame);Require(Json(graph.Passives.Capture())==Json(saved.Frame)&&Json(graph.Stats)==Json(saved.Numeric),"NPC grant book/cache/original item graph changed on reload");
            saved=saved with{Generation=saved.Generation+1};Require(saved.Generation<=2,"NPC grant reload generation");
        }
        else
        {
            var owner=new PlayerMobile{Name="NpcSkillGrantOwner",Body=0x190,Player=true};ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);ClassSkillChecks.Train(owner,"necromancer.raiseskeleton");
            var b=new SkeletonGuardian{Name="NpcSkillGrantStorage",Frozen=true,CantWalk=true};b.Initialize(owner,1,0);Require(b.SetControlMaster(owner),"Item skill fixture owner");b.Internalize();b.AIObject?.AITimer.Stop();
            Require(GeneratedNpcEquipment.TryCreate(b,owner,EquipMonster.Other,new("gsc",37,2,0,123,456,511),4,out var item),"Original supported Grand Scepter creation");
            var graph=Build(b);Require(graph.Passives.Capture().Lists.Length==0&&ShadowSkillBook.Restore(graph.Passives.Capture().Book).Snapshot().Entries.Length==1,"Original NPC singleskill staffmods materialized skills");
            // The NPC unique producer is unsupported. This explicit source
            // base stat is not claimed to be a generated oskill item.
            graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,36),5,graph.Actor);
            var book=ShadowSkillBook.Restore(graph.Passives.Capture().Book);Require(book.Find(36)is{Base:0}&&book.Rank(36,graph.Read)==5,"Explicit source oskill value did not grant an intrinsic entry");
            string payload=item.SavedText;book.Select(36,false);book.Select(36,true);book.Use(36);graph.Passives.Refresh(book.Save());
            graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,36),0,graph.Actor);var removed=ShadowSkillBook.Restore(graph.Passives.Capture().Book);
            Require(removed.Find(36)==null&&removed.Left==0&&removed.Right==0&&removed.Used==null,"Source stat removal failed intrinsic cleanup/Attack fallback");
            graph.Values.SetBase(graph.Actor.Stats,EquipmentStatKey.Pack(97,36),5,graph.Actor);graph.Transferred(item.Serial.Value);graph.Update(item.Serial.Value);
            book=ShadowSkillBook.Restore(graph.Passives.Capture().Book);Require(book.Find(36)is{Base:0}&&item.SavedText==payload,"Original item transition changed the independent source grant");
            book.Select(36,false);book.Select(36,true);book.Use(36);graph.Passives.Refresh(book.Save());
            saved=new(b.Serial.Value,owner.Serial.Value,item.Serial.Value,payload,graph.Passives.Capture(),graph.Stats);owner.Internalize();
        }
        Require(NpcWorldAllocation.Capture()==world,"NPC item skill callbacks consumed shared world allocation");File.WriteAllText(Manifest,Json(saved));
        Log.Information("NPC item skills:4096whole older branch/writer traces;429raw current rows,zero-base grant/removal/Attack fallback and actual passive callbacks;original supported equipment plus explicit source97input and intrinsic book through two reloads;unique oskill producer,foreign passives,active aura/channel and skill execution remain gated");
    }
}
