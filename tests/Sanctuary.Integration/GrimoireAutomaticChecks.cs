// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GrimoireAutomaticChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimoireAutomaticChecks));
    private sealed record Saved(uint Owner,uint Item,bool Female,ulong Seed,string Next,ulong NextSeed,int Life,int Mana,string Capture,string Gear);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grimoire-automatic-fixtures.json");
    public static void Configure(){Register("grimoire-automatic-check",Check);Register("grimoire-automatic-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Complete source automatic pool/picker/seed with retained original books",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var cases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grimoire-automatic-source-cases.json")));
        using var mapping=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"grimoire-automatic-source-rows.json")));int count=0;
        string Name(string id)=>mapping.RootElement.GetProperty(id).GetString();
        foreach(var row in cases.RootElement.EnumerateArray())
        {
            string code=row[0].GetString();int level=row[1].GetInt32();ulong seed=ulong.Parse(row[2].GetString());
            var selected=GrimoireAutomaticSelection.Select(code,level,ref seed);
            Require(selected.Name==Name(row[4].GetString())&&seed==ulong.Parse(row[5].GetString()),"Grimoire complete source selected row/seed mismatch case "+count);
            Require(AffixBalance.Level(level,GrimoirePrepared.Base(code).QualityLevel)==row[3].GetInt32(),"Grimoire source affix level mismatch");
            var pool=GrimoireAutomaticSelection.Pool(code,level);var expected=row[7];Require(pool.Length==expected.GetArrayLength(),"Grimoire source pool length");
            for(int i=0;i<pool.Length;i++)
            {Require(pool[i].Name==Name(expected[i][0].GetString())&&pool[i].Frequency==expected[i][1].GetInt32(),"Grimoire source pool order/frequency");}
            Require(pool.Sum(r=>r.Frequency)==row[6].GetInt32(),"Grimoire source total frequency");count++;
        }
        Require(count==11880,"Incomplete automatic selector source comparison");
        ulong random=0;int queries=0;var no=SourceAffixPicker.Select<GrimoireAutomatic>(()=>{queries++;throw new InvalidOperationException("Inhibited attempt resolved pool");},r=>r.Frequency,ref random,false);
        Require(no==null&&queries==0,"Unforced parity gate resolved pool");
        random=SourceRandom.InitLow(173);ulong before=random;
        try{GrimoireAutomaticSelection.Select("wa1",0,ref random);throw new InvalidOperationException("Invalid Grimoire level accepted");}catch(ArgumentOutOfRangeException){}
        Require(random==before,"Invalid prepared context spent RNG");
        try{GrimoireAutomaticSelection.Select("buc",99,ref random);throw new InvalidOperationException("Invalid Grimoire base accepted");}catch(InvalidDataException){}
        Require(random==before,"Invalid prepared base spent RNG");
        Log.Information("Grimoire automatic:11880 complete source pool/weight/selection/final-seed comparisons;normal selector only;staffmod generation remains gated");
    }
    private static Saved Scene(bool female)
    {
        var owner=GrimoireEquipChecks.Fresh(female);WarlockGrimoire book=null;
        try
        {
            ulong seed=female?0x123456789ABCDEF0UL:SourceRandom.InitLow(173);var selected=GrimoireAutomaticSelection.Select("wa1",99,ref seed);
            // Supplied empty captured grants test this independent selector;
            // they are NOT generated staffmods or a public partial factory.
            book=WarlockGrimoire.CreatePrepared(new("wa1",99,5,selected.Name,[]));Require(owner.AddToBackpack(book)&&owner.EquipItem(book),"Selected automatic capture equip");
            book.HitPoints=17;ulong next=seed;string nextName=GrimoireAutomaticSelection.Select("wa1",99,ref next).Name;
            var saved=new Saved(owner.Serial.Value,book.Serial.Value,female,seed,nextName,next,SkillHealth.Balance256(owner),NecromancerEffects.ManaBalance256(owner),book.SavedText,JsonSerializer.Serialize(ClassGear.State(book)));
            owner.Internalize();owner=null;book=null;return saved;
        }
        finally{book?.Delete();owner?.Delete();}
    }
    private static void Check(){Differential();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var book=World.FindItem((Serial)saved.Item)as WarlockGrimoire;
            Require(owner!=null&&book!=null&&book.Parent==owner&&owner.Female==saved.Female&&book.SavedText==saved.Capture&&book.HitPoints==17
                &&JsonSerializer.Serialize(ClassGear.State(book))==saved.Gear&&SkillHealth.Balance256(owner)==saved.Life&&NecromancerEffects.ManaBalance256(owner)==saved.Mana,"Original selected Grimoire capture/gear/durability/resources lost on reload");
            ulong seed=saved.Seed;Require(GrimoireAutomaticSelection.Select("wa1",99,ref seed).Name==saved.Next&&seed==saved.NextSeed,"Saved caller seed did not continue the automatic selector");
        }
    }
}
