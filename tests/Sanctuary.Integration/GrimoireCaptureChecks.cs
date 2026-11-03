// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GrimoireCaptureChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GrimoireCaptureChecks));
    private sealed record Saved(uint Owner,uint Item,uint Helm,bool Female,int Mana,int Life,string Capture,int Durability);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"grimoire-capture-fixtures.json");
    public static void Configure(){Register("grimoire-capture-check",Check);Register("grimoire-capture-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Owned Grimoire captures and original saved items",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool condition,string why){if(!condition){throw new InvalidOperationException(why);}}
    private static Dictionary<string,string>[] Rows(string filename)
    {
        var lines=File.ReadAllLines(Path.Join(Core.BaseDirectory,filename));var header=lines[0].Split('\t');
        return lines.Skip(1).Select(line=>line.Split('\t')).Where(cells=>cells.Length==header.Length)
            .Select(cells=>header.Select((key,i)=>(key,cells[i])).ToDictionary(p=>p.key,p=>p.Item2)).ToArray();
    }
    private static void Refuses(Action action)
    {try{action();throw new InvalidOperationException("Invalid Grimoire capture accepted");}catch(InvalidDataException){}}
    private static void Model()
    {
        var armor=Rows("grimoire-armor-source.txt").Where(r=>r["type"]=="grim").ToArray();
        var automatic=Rows("grimoire-automatic-source.txt").Where(r=>r["group"]=="306").ToArray();
        Require(armor.Length==15&&automatic.Length==8&&GrimoirePrepared.AllBases.Count==15&&GrimoirePrepared.AllAutomatic.Count==8,"Incomplete source Grimoire catalogue");
        int captures=0;
        foreach(var source in armor)
        {
            int N(string key)=>int.Parse(string.IsNullOrEmpty(source[key])?"0":source[key]);var basis=GrimoirePrepared.Base(source["code"]);
            Require(basis.Name==source["name"]&&basis.Normal==source["normcode"]&&basis.QualityLevel==N("level")&&basis.RequiredLevel==N("levelreq")
                &&basis.Strength==N("reqstr")&&basis.MinimumDefense==N("minac")&&basis.MaximumDefense==N("maxac")
                &&basis.Block==N("block")&&basis.Durability==N("durability")&&basis.MaximumSockets==N("gemsockets"),"Grimoire base differs from owned armor TXT");
            foreach(var auto in automatic)
            {
                var row=GrimoirePrepared.Auto(auto["Name"]);bool magic=auto["mod1code"]=="dmg-mag";
                Require(row.AffixLevel==int.Parse(auto["level"])&&row.RequiredLevel==int.Parse(auto["levelreq"])
                    &&row.Frequency==int.Parse(auto["frequency"])&&row.Rare==(auto["rare"]=="1"),"Grimoire automatic metadata differs from owned TXT");
                foreach(int defense in new[]{basis.MinimumDefense,basis.MaximumDefense})foreach(int count in new[]{0,1,3})
                {
                    var grants=new[]{new GrimoireGrant(383,1),new GrimoireGrant(374,2),new GrimoireGrant(377,3)}.Take(count).ToArray();
                    var snapshot=new GrimoireSnapshot(basis.Code,99,defense,row.Name,grants);GrimoirePrepared.Validate(snapshot);
                    var properties=GrimoirePrepared.Properties(snapshot);Require(properties.Length==(magic?1:2)+count,"Inherent property count includes blank mod3 or lost staffmods");
                    var first=properties[0];
                    if(magic){Require(first.Property.Code=="dmg-mag"&&first.Roll.Value==int.Parse(auto["mod1min"])&&first.Roll.Maximum==int.Parse(auto["mod1max"]),"Magic range was rerolled or converted to fire");}
                    else{Require(first.Property.Code=="fire-min"&&first.Roll.Value==int.Parse(auto["mod1min"])
                        &&properties[1].Property.Code=="fire-max"&&properties[1].Roll.Value==int.Parse(auto["mod2min"]),"Separate fixed fire endpoints changed");}
                    foreach(var grant in grants)
                    {Require(properties.Count(p=>p.Property.Code=="skill"&&NamedPropertyRuntime.Parameter(p.Property)==grant.Skill&&p.Roll.Value==grant.Rank)==1,"Captured staffmod duplicated or lost");}
                    captures++;
                }
            }
        }
        Require(captures==720,"Missing Grimoire capture contexts");
        var valid=new GrimoireSnapshot("wa1",99,GrimoirePrepared.Base("wa1").MinimumDefense,"of Shadows",[new(383,3)]);
        foreach(var invalid in new[]{valid with{Profile="unknown"},valid with{Code="buc"},valid with{ItemLevel=0},valid with{ItemLevel=100},
            valid with{Defense=-1},valid with{Automatic="unknown"},valid with{Grants=null},valid with{Grants=[new(383,0)]},
            valid with{Grants=[new(383,4)]},valid with{Grants=[new(70,1)]},valid with{Grants=[new(383,1),new(383,2)]},
            valid with{ItemLevel=1,Automatic="of Nightmares"}})
        {Refuses(()=>GrimoirePrepared.Validate(invalid));}
        Log.Information("Grimoire:15owned bases/eight automatic rows/720capture contexts/12malformed controls;no random generation or equip certificate");
    }
    private static Saved Scene(bool female)
    {
        var owner=TaintedFireBallChecks.Owner();owner.Female=female;if(female){owner.Body=0x191;}
        WarlockGrimoire item=null;
        try
        {
            string code=female?"waf":"wa1";var basis=GrimoirePrepared.Base(code);
            var snapshot=new GrimoireSnapshot(code,99,basis.MaximumDefense,female?"of Inferno":"of Shadows",[new(383,3),new(374,2)]);
            item=WarlockGrimoire.CreatePrepared(snapshot);Require(owner.AddToBackpack(item),"Grimoire backpack placement failed");item.HitPoints=17;
            string original=item.SavedText;var returned=item.Capture;returned.Grants[0]=new(70,99);snapshot.Grants[0]=new(70,99);
            Require(item.SavedText==original&&item.Capture.Grants[0]==new GrimoireGrant(383,3),"Grimoire capture aliases caller/reader arrays");
            Refuses(()=>item.RestorePrepared(item.Capture with{Defense=-1}));
            Require(item.SavedText==original&&item.Parent==owner.Backpack&&item.ItemID==0xEFA&&item.Layer==Layer.TwoHanded
                &&item.HitPoints==17&&item.MaxHitPoints==20&&!item.CanEquip(owner)&&!ClassGear.Converted(item),"Rejected Grimoire changed state or incomplete native equipment path enabled");
            var saved=new Saved(owner.Serial.Value,item.Serial.Value,owner.FindItemOnLayer(Layer.Helm).Serial.Value,female,
                NecromancerEffects.ManaBalance256(owner),SkillHealth.Balance256(owner),original,item.HitPoints);
            owner.Internalize();owner=null;item=null;return saved;
        }
        finally{item?.Delete();owner?.Delete();}
    }
    private static void Check(){Model();File.WriteAllText(Manifest,JsonSerializer.Serialize(new[]{Scene(false),Scene(true)}));}
    private static void Reload()
    {
        foreach(var saved in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var item=World.FindItem((Serial)saved.Item)as WarlockGrimoire;
            Require(owner!=null&&item!=null&&item.Parent==owner.Backpack&&owner.Female==saved.Female&&item.SavedText==saved.Capture
                &&item.HitPoints==saved.Durability&&item.MaxHitPoints==20&&NecromancerEffects.ManaBalance256(owner)==saved.Mana
                &&SkillHealth.Balance256(owner)==saved.Life&&World.FindItem((Serial)saved.Helm)?.Parent==owner,"Original Warlock/Witch Grimoire/helm/resources lost on reload");
            GrimoirePrepared.Validate(item.Capture);Require(!item.CanEquip(owner)&&!ClassGear.Converted(item),"Saved incomplete Grimoire equip path enabled");
        }
    }
}
