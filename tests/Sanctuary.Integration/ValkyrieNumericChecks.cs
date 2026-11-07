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

public static class ValkyrieNumericChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieNumericChecks));
    private sealed record Held(int Case,uint Body,string Text,uint[] Items,string[] Payloads,int Life,int Mana,int Stamina);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-numeric-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("valkyrie-numeric-check",false);Register("valkyrie-numeric-reload-check",true);}
    private static void Register(string command,bool reload)=>ConsoleInputHandler.RegisterCommand(command,"Valkyrie full item graph and live numeric recipients",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="ValkyrieNumericFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);
        FixtureSpace.Corridor(p,6380,20,3380);p.Mana=p.ManaMax;return p;
    }
    private static AmazonValkyrieBody Body(PlayerMobile p,int rank,MonsterDifficulty difficulty)
    {
        var b=new AmazonValkyrieBody();var state=new ValkyrieBodyState(p.Serial.Value,ValkyrieBirthNumbers.Capture(p,400*256)with{Rank=rank},difficulty,TimedEffects.Now.AddDays(2));
        b.RestorePrepared(state);var facets=SummonDifficulties.Facets();
        try{SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());b.Initialize(p,rank,0);}
        finally{SummonDifficulties.SetFacets(facets);}
        b.RestorePrepared(state);b.SetStr(rank*25);b.SetDex(rank*12);b.ApplyEquipmentResistances();Require(b.SetControlMaster(p),"Numeric owner refused");
        PersistentMinions.Profiles.Track(b);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(b,b.MaximumLife256-257);
        ValkyrieEquipment.Materialize(b,new(SourceRandom.InitLow(100+rank)),new(SourceRandom.InitLow(900+(int)difficulty)));return b;
    }
    private static string Pairs(EquipmentStatValue[] fields)=>Json(fields.Select(v=>new long[]{v.Packed,v.Value}).ToArray());
    private static void Inspect(AmazonValkyrieBody body,JsonElement c,PlayerMobile supplier,bool transitions)
    {
        var g=body.EquipmentNumbers;Require(g!=null&&Pairs(ValkyrieEquipmentStats.Basis(body,body.Prepared.Difficulty))==c.GetProperty("Basis").GetRawText(),"Pinned birth base projection differs from independent source inputs");
        Require(Pairs(g.Stats)==c.GetProperty("Stats").GetRawText(),"Full original operative stat graph differs from complete older source");
        int Read(int stat)=>g.Read(stat);
        int attack=checked(Read(19)+5*Read(2));attack+=SourcePercentage.Calculate(attack,Read(119),100);
        Require(ClassStats.AttackRating(body)==attack&&ClassStats.Defense(body)==c.GetProperty("Defense").GetInt32(),"Native accuracy/defense omitted an item,Dexterity or doubled global percent");
        var r=CreatureProfiles.For(body).Resistances;
        Require(body.PhysicalResistance==Read(36)&&body.FireResistance==Read(39)&&body.EnergyResistance==Read(41)&&body.ColdResistance==Read(43)&&body.PoisonResistance==Read(45)
            &&NamedElements.MagicResistance(body)==Read(37)&&r==new CreatureResistances(Read(36),Read(37),Read(39),Read(41),Read(43),Read(45)),"Actual native/source resistance recipients disagree");
        foreach(var pair in new[]{("fire",39),("cold",43),("ltng",41),("pois",45),("mag",37),("physical",36)})
        {
            var damage=NamedElements.Resolve256(null,body,10000,pair.Item1,enhance:false);
            Require(damage.Damage256==NamedElements.Reduce256(10000,Read(pair.Item2),0,0,0).Damage256,"Native typed mitigation ignored the operative original resistance");
        }
        Require(!GeneratedNpcLiveResources.Handles(body)&&body.SkillWeaponDamage==(0,0),"Numeric recipient enabled resource/weapon/learned consumers");
        // Skill trees,class bonuses,charges and chance-to-cast layers remain
        // present in g.Stats above. They are not granted to the owner or pet.
        var items=ValkyrieEquipment.Originals(body);var expected=c.GetProperty("Items").EnumerateArray().ToArray();Require(items.Length==expected.Length,"Unfiltered source loadout lost items");
        for(int i=0;i<items.Length;i++)
        {
            var s=items[i].Snapshot;
            Require(Json(s.Graph)==expected[i].GetProperty("Graph").GetRawText()&&Json(s.Metadata)==expected[i].GetProperty("Metadata").GetRawText(),"Original item graph or metadata was filtered/rerolled");
        }
        if(!transitions){return;}
        int life=SkillHealth.Balance256(body),mana=NecromancerEffects.ManaBalance256(body),stamina=BattleOrders.Stamina256(body);
        BarbarianShout.Apply(supplier,body,1,TimedEffects.Now);
        Require(BarbarianShout.Defense(body)==100&&ClassStats.Defense(body)==c.GetProperty("Shout100").GetInt32(),"Shout multiplied already-boosted item defense instead of sharing percent pool");
        BarbarianShout.Clear(body);var old=AmazonInnerSight.Snapshot();
        try
        {
            AmazonInnerSight.Restore(old.Append(new(body.Serial.Value,supplier.Serial.Value,3,90,TimedEffects.Now.AddHours(1))).ToArray());
            Require(ClassStats.Defense(body)==c.GetProperty("InnerSight90").GetInt32(),"Inner Sight flat loss used the wrong side of the item/passive percentage");
        }
        finally{AmazonInnerSight.Restore(old);}
        string text=body.SavedText;bool refused=false;
        try{body.RestorePrepared(body.Prepared with{EquipmentStatProfile="unknown-profile"});}catch(InvalidDataException){refused=true;}
        Require(refused&&body.SavedText==text,"Unknown saved numeric profile partially mutated body");
        refused=false;try{ValkyrieEquipmentStats.Activate(body);}catch(NotSupportedException){refused=true;}Require(refused&&body.SavedText==text,"Repeated numeric activation changed original state");
        var last=items[^1];((PlayerMobile)body.ControlMaster).Backpack.DropItem(last);refused=false;
        try{_=ClassStats.Defense(body);}catch(NotSupportedException){refused=true;}Require(refused,"Stale removed original item left cached active defense");
        refused=false;try{_=body.FireResistance;}catch(NotSupportedException){refused=true;}Require(refused,"Removed original item left cached native resistance protection");
        body.AddItem(last);body.RestorePrepared(body.Prepared);body.ApplyEquipmentResistances();
        Require(life==SkillHealth.Balance256(body)&&mana==NecromancerEffects.ManaBalance256(body)&&stamina==BattleOrders.Stamina256(body),"Numeric projection/buff/cache rebuild refilled or rescaled resources");
    }
    private static void Check(bool reload)
    {
        using var input=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-numeric-cases.json")));var cases=input.RootElement.EnumerateArray().ToArray();
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):null;var owner=reload?World.FindMobile((Serial)saved.Owner)as PlayerMobile:Owner();Require(owner is{Alive:true,Deleted:false},"Original numeric owner missing");
        var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);var rows=new List<Held>();
        try
        {
            for(int i=0;i<cases.Length;i++)
            {
                var c=cases[i];AmazonValkyrieBody b;
                if(reload)
                {
                    var held=saved.Bodies.Single(h=>h.Case==i);b=World.FindMobile((Serial)held.Body)as AmazonValkyrieBody;
                    Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==owner&&b.SavedText==held.Text&&SkillHealth.Balance256(b)==held.Life
                        &&NecromancerEffects.ManaBalance256(b)==held.Mana&&BattleOrders.Stamina256(b)==held.Stamina,"Original numeric actor/snapshot/fractional wounds/resources changed on reload");
                    var originals=ValkyrieEquipment.Originals(b);Require(originals.Select(v=>v.Serial.Value).SequenceEqual(held.Items)&&originals.Select(v=>v.SavedText).SequenceEqual(held.Payloads),"Numeric reload replaced original items");
                }
                else
                {
                    b=Body(owner,c.GetProperty("Rank").GetInt32(),(MonsterDifficulty)c.GetProperty("Difficulty").GetInt32());var before=BattleOrders.Capture(b);int ownerMana=NecromancerEffects.ManaBalance256(owner);
                    ValkyrieEquipmentStats.Activate(b);Require(BattleOrders.Capture(b)==before&&NecromancerEffects.ManaBalance256(owner)==ownerMana,"Numeric activation altered resources or charged owner");
                }
                Inspect(b,c,supplier,!reload);var items=ValkyrieEquipment.Originals(b);
                rows.Add(new(i,b.Serial.Value,b.SavedText,items.Select(v=>v.Serial.Value).ToArray(),items.Select(v=>v.SavedText).ToArray(),SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(b),BattleOrders.Stamina256(b)));
            }
            Require(rows.Count==57,"Numeric rank/difficulty coverage");PersistentMinions.Suspend(owner);owner.Internalize();
            File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,rows.ToArray(),reload?saved.Generation+1:0)));
        }
        finally{supplier.Delete();}
        Log.Information("Valkyrie numeric:57full original loadouts/330items match complete older stat composition;171defense contexts,native accuracy/resistances/typed mitigation,additive Shout and flat Inner Sight;unfiltered skill/event layers retained,missing-original refusal,no resource change and original saves;weapon/resource/proc/AI/cast remain separate");
    }
}
