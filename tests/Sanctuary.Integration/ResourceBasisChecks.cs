// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class ResourceBasisChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ResourceBasisChecks));
    private sealed record Saved(uint Owner,uint Shield,uint Helm,uint Gloves,int Life,int Mana,int Stamina,long Life256,long Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"resource-basis.json");
    public static void Configure(){Register("resource-basis-check",Check);Register("resource-basis-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Source resource operation order and real named stat bases",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Goldens()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(root,".local/class-skills/battle-orders-source-probe.json")));
        foreach(var row in source.RootElement.GetProperty("statCases").EnumerateArray())
        {
            int[] x=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();int kind=x[0],type=x[1],basis=x[2],bonus=x[3],percent=x[4],scaled=x[5],expected=x[6];
            var parts=new ClassResourceParts(basis,type==0?(long)bonus*(kind is 9 or 11?4:16)*64:0,37L*scaled*32,percent,0);
            Require(parts.Maximum256==expected && parts.NativeMaximum==expected/256,"Independent compiled resource operation cases");
        }
    }
    private static Item Equip(PlayerMobile owner,string source)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==source).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Resource fixture item "+source);ClassGear.Refresh(owner);return item;
    }
    private static void Check()
    {
        Goldens();var owner=new PlayerMobile{Body=0x190,Name="ResourceBasisFixture",Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        FixtureSpace.Place(owner,new(6150,1820,0),Map.Trammel);
        for(int i=0;i<40;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Resource legal strength");}
        Require(owner.HitsMax==251 && owner.ManaMax==108 && owner.StamMax==190,"Independent level99 Barbarian resource baseline");
        var shield=Equip(owner,"Lidless Wall");
        // +10 Energy and10% mana: percentage applies to108, not118.
        Require(owner.Int==20 && owner.ManaMax==128 && ClassResources.Parts(owner,2).Maximum256==32972,"Lidless energy was multiplied by percentage");
        var gloves=Equip(owner,"Frostburn");
        Require(owner.ManaMax==172 && ClassResources.Parts(owner,2).Maximum256==44032,"Additive50% mana before bonus Energy");
        var helm=Equip(owner,"Harlequin Crest");
        var hp=ClassResources.Parts(owner,0);var mana=ClassResources.Parts(owner,2);
        // Harlequin's +2 vitality/+2 energy and1.5 resource/level are post-percent ops.
        Require(ClassStats.Vitality(owner)==27 && owner.Int==22 && hp.Maximum256==104320 && owner.HitsMax==407,"Harlequin exact fractional life");
        Require(mana.Maximum256==82560 && owner.ManaMax==322 && owner.StamMax==192,"Harlequin scaled mana/bonus Energy must not receive50% again");
        var withOrders=ClassResources.Parts(owner,2,35);
        Require(withOrders.Maximum256==92236 && withOrders.NativeMaximum==360,"Future BO percentage eligibility against108 base only");
        owner.AddToBackpack(gloves);ClassGear.Refresh(owner);
        Require(owner.ManaMax==279 && ClassResources.Parts(owner,2).Maximum256==71500,"Percentage gear removal changed excluded scaled/attribute contributions");
        Require(owner.EquipItem(gloves),"Resource glove restore");ClassGear.Refresh(owner);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,shield.Serial.Value,helm.Serial.Value,gloves.Serial.Value,owner.HitsMax,owner.ManaMax,owner.StamMax,hp.Maximum256,mana.Maximum256)));
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(World.FindItem((Serial)saved.Shield).Parent==owner && World.FindItem((Serial)saved.Helm).Parent==owner && World.FindItem((Serial)saved.Gloves).Parent==owner,"Saved resource gear identity");
        Require(owner.HitsMax==saved.Life && owner.ManaMax==saved.Mana && owner.StamMax==saved.Stamina && ClassResources.Parts(owner,0).Maximum256==saved.Life256 && ClassResources.Parts(owner,2).Maximum256==saved.Mana256,"Resource stat ordering/fractions changed across actual reload");
    }
}
