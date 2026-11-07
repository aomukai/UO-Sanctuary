// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary.Integration;
public static class NpcItemAffixChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcItemAffixChecks));
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-affix-fixtures.json");
    private sealed record WeightRow(int Id,int Weight);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-item-affix-check",false);Register("npc-item-affix-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Current NPC affix pools/rare names;no generated items or Shadows",_=>Core.LoopContext.Post(()=>
    {
        try
        {
            Affixes();RareNames();const string scope="pools-and-names-only:no-generated-item-or-actor";
            if(reload){Require(File.ReadAllText(Manifest)==scope,"Explicit original fixture scope");}else{File.WriteAllText(Manifest,scope);}
            Log.Information("{Name}: passed",name);
        }
        catch(Exception e){Log.Error(e,"{Name}: failed",name);}
    }));
    private static void Affixes()
    {
        using var cases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-affix-source-probe.json")));
        int current=0,weighted=0,unsupported=0,preferredHits=0;
        foreach(var row in cases.RootElement.EnumerateArray())
        {
            string code=row[0].GetString();ulong seed=ulong.Parse(row[4].GetString());bool force=row[12].GetInt32()!=0;int preferred=row[13].GetInt32();
            if(code=="!weighted")
            {
                var choices=Enumerable.Range(1,row[1].GetInt32()).Select(i=>new WeightRow(i,row[2].GetInt32())).ToArray();
                var r=SourceAffixPicker.Select(()=>choices,x=>x.Weight,ref seed,force,preferred>0?x=>x.Id==preferred:null);
                Require((r?.Id.ToString()??"")==row[8].GetString()&&seed==ulong.Parse(row[9].GetString()),"Complete source weighted cap/preferred/seed at "+weighted);weighted++;continue;
            }
            int level=row[1].GetInt32();var quality=row[2].GetInt32()==4?GearRarity.Magic:GearRarity.Rare;var kind=row[3].GetInt32()==0?AffixKind.Suffix:AffixKind.Prefix;
            string previous=row[6].GetString();string[] existing=previous.Length==0?[]:[previous];
            var selected=NpcItemAffixes.Pool.Select(code,level,quality,kind,ref seed,force,existing,preferred);
            Require((selected?.Definition.Id??"")==row[8].GetString()&&seed==ulong.Parse(row[9].GetString()),"Complete current NPC selector result/seed at "+current+":"+code);
            Require(NpcItemAffixes.Pool.Level(code,level)==row[7].GetInt32(),"Current NPC affix level");
            ulong preview=ulong.Parse(row[4].GetString());bool inhibited=SourceRandom.Limited(ref preview,2)==0&&!force;
            bool usedPreferred=preferred>0&&selected?.Row==preferred-1;if(usedPreferred){preferredHits++;}
            if(!inhibited&&!usedPreferred)
            {
                var pool=NpcItemAffixes.Pool.Pool(code,level,quality,kind,existing);var e=row[11];Require(pool.Length==e.GetArrayLength(),"Current complete NPC pool length");
                for(int i=0;i<pool.Length;i++){Require(pool[i].Definition.Id==e[i][0].GetString()&&NpcItemAffixes.Pool.Weight(pool[i],code)==e[i][1].GetInt32(),"Current NPC pool ordered row/weight");}
                Require(pool.Sum(r=>NpcItemAffixes.Pool.Weight(r,code))==row[10].GetInt32(),"Current NPC total weight");
            }
            if(selected!=null&&!ClassGear.Implemented(selected.Definition)){unsupported++;}current++;
        }
        Require(current==22144&&weighted==288&&preferredHits==4248&&unsupported>0,"Incomplete source cases/preferred coverage or player filter leaked");
        Require(NpcItemAffixes.Pool.Basis("ces").Type=="h2h"&&NpcItemAffixes.Pool.Basis("9tw").Code=="9tw","Shadow claw catalogue bases");
        Log.Information("NPC item affixes:22144 current all692base controls/288 weighted-cap controls;4248preferred hits;unsupported valid properties retained;no generated item");
    }
    private static int Ordinal(SummonRareName n)=>n==null?0:n.Row+1+(n.Prefix?155:0);
    private static void RareNames()
    {
        using var cases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-rare-name-cases.json")));int current=0,synthetic=0;
        foreach(var row in cases.RootElement.GetProperty("current").EnumerateArray())
        {
            string code=row[0].GetString();bool prefix=row[1].GetInt32()!=0;int format=row[2].GetInt32();ulong seed=ulong.Parse(row[3].GetString());var b=NpcItemAffixes.RareBasis(code);
            var selected=NpcItemAffixes.RareName(code,prefix,ref seed,format);
            var pool=NpcItemAffixes.RarePool(NpcItemAffixes.Names,prefix,format,b.Stackable,b.Throwable,t=>NpcItemAffixes.Pool.IsType(code,t));
            Require(Ordinal(selected)==row[4].GetInt32()&&seed==ulong.Parse(row[5].GetString()),"Current source rare-name selection/seed at "+current+":"+code);
            Require(pool.Select(Ordinal).SequenceEqual(row[6].EnumerateArray().Select(x=>x.GetInt32())),"Current source rare-name ordered eligibility pool");current++;
        }
        foreach(var row in cases.RootElement.GetProperty("synthetic").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();int size=V(0);bool prefix=V(1)!=0;ulong seed=ulong.Parse(row[7].GetString());
            var names=V(6)!=0?Array.Empty<SummonRareName>():Enumerable.Range(0,size).Select(i=>new SummonRareName(i,prefix,"synthetic",V(3),["weap"],[])).ToArray();
            var selected=NpcItemAffixes.SelectRare(names,prefix,V(2),V(4)!=0,V(5)!=0,_=>true,ref seed);
            var pool=NpcItemAffixes.RarePool(names,prefix,V(2),V(4)!=0,V(5)!=0,_=>true);
            Require((selected?.Row+1??0)==V(8)&&seed==ulong.Parse(row[9].GetString()),"Complete source rare-name cap/format/empty/seed at "+synthetic);
            Require(pool.Select(x=>x.Row+1).SequenceEqual(row[10].EnumerateArray().Select(x=>x.GetInt32())),"Synthetic source ordered cap511");synthetic++;
        }
        Require(current==12456&&synthetic==2016&&NpcItemAffixes.Names.Count==201,"Incomplete rare-name controls");
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Catalogue enabled incomplete summons");
        Log.Information("NPC rare names:12456 current/2016 synthetic complete source eligibility,pool,cap,format and seed controls;no generated item or actor");
    }
}
