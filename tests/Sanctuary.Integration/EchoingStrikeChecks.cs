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

public static class EchoingStrikeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EchoingStrikeChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,bool Female,EchoingStrikeValues Values,int Life256,int Mana256);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"echoing-strike-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Echoing Strike suppliers and pinned calculation aliases",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("echoing-strike-check",false);Register("echoing-strike-reload-check",true);
    }
    private static SkillValues Values(int rank,int mirror,int warp,int softMirror=255)
        =>new(SkillMechanics.For(CombatAbility.EchoingStrike),k=>k=="Echoing Strike"?rank:k=="Mirrored Blades"?softMirror:255,
            k=>k=="Mirrored Blades"?mirror:k=="Blade Warp"?warp:0,99);
    private static int Damage(int rank,bool maximum,int synergy)
    {
        int left=rank-1,basis=maximum?12:8;int[] lengths=[7,8,6,6,255],steps=maximum?[5,6,9,9,9]:[3,4,6,6,6];
        for(int i=0;i<steps.Length;i++){int n=Math.Min(left,lengths[i]);basis+=n*steps[i];left-=n;}
        return (basis+basis*synergy/100)*256;
    }
    private static void Model()
    {
        using var aliases=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"echoing-calc-aliases.json")));
        var shape=SkillMechanics.For(CombatAbility.EchoingStrike);var fields=new Dictionary<string,JsonElement>(shape.SourceFields);
        for(int i=1;i<=10;i++){fields["calc"+i]=JsonSerializer.SerializeToElement(i*100+37);}
        var sentinel=new SkillValues(shape with{SourceFields=fields},_=>1,_=>0,99);
        int count=0;foreach(var row in aliases.RootElement.EnumerateArray())
        {Require(sentinel.Formula(row.GetProperty("alias").GetString())==row.GetProperty("field").GetInt32()*100+37,"Pinned clc alias mapped to the wrong calc field");count++;}
        Require(count==10,"Missing registered calculation aliases");
        foreach(string alias in new[]{"clc10","clc01","clc11"})
        {bool refused=false;try{sentinel.Formula(alias);}catch(NotSupportedException){refused=true;}Require(refused,"Invented unregistered calculation alias "+alias);}
        fields["calc1"]=JsonSerializer.SerializeToElement("clc1");bool recursive=false;
        try{new SkillValues(shape with{SourceFields=fields},_=>1,_=>0,99).Eval("calc1");}catch(InvalidDataException){recursive=true;}
        Require(recursive,"Actual cyclic formulas lost the recursion guard");
        int cases=0;
        foreach(int rank in new[]{1,8,9,16,17,22,23,28,29,40,255})foreach(int mirror in new[]{0,1,4,5,9,10,15,20})foreach(int warp in new[]{0,20})
        {
            int synergy=(mirror+warp)*5,attacks=mirror==0?1:1+mirror/5;
            var v=EchoingStrikePrepared.Capture(Values(rank,mirror,warp));
            Require(v==new EchoingStrikeValues(rank,mirror,warp,(rank+13)*128,rank*10,30+(rank-1)*5+synergy,
                Damage(rank,false,synergy),Damage(rank,true,synergy),synergy,attacks,(100/attacks)/2,0,1,116),"Echoing Strike independent tier/synergy/count/cost suppliers");
            Require(v==EchoingStrikePrepared.Capture(Values(rank,mirror,warp,mirror)),"Soft Mirrored ranks changed hard-point synergy/count");cases++;
        }
        Require(cases==176,"Missing Echoing boundary controls");
        // This cross-skill alias previously read absent calc7 instead of calc6.
        var warpValues=new SkillValues(SkillMechanics.For(CombatAbility.BladeWarp),k=>k=="Mirrored Blades"?20:1,_=>0,99);
        Require(warpValues.Eval("calc6")==MirroredBladesPrepared.Capture(new(SkillMechanics.For(CombatAbility.MirroredBlades),k=>k=="Mirrored Blades"?20:1,_=>0,99)).DuplicatePercent,"Blade Warp cross-skill calc6 lookup");
        foreach(var ability in new[]{CombatAbility.Cleave,CombatAbility.MirroredBlades})
        {var v=new SkillValues(SkillMechanics.For(ability),_=>20,_=>0,99);Require(v.Eval("cltcalc1")==v.Eval("calc3"),"Client attack-speed alias differed from the skill supplier");}
        Require(!WarlockCombat.Implemented(CombatAbility.EchoingStrike),"Suppliers enabled unrecovered Echoing launch/hit behavior");
        Log.Information("Echoing suppliers:10pinned aliases/176rank-hard-synergy controls,cross-skill/client lookup and real-cycle refusal pass;learned route remains gated.");
    }
    private static void Check(bool reload)
    {
        Model();if(reload)
        {
            foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
            {
                var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;
                Require(p!=null&&p.Female==s.Female&&World.FindItem((Serial)s.Weapon)?.Parent==p&&World.FindItem((Serial)s.Helm)?.Parent==p,"Original Echoing Warlock/Witch equipment changed");
                Require(EchoingStrikePrepared.Capture(SkillMechanics.Values(p,CombatAbility.EchoingStrike))==s.Values,"Original saved hard/effective rank context changed");
                Require(SkillHealth.Balance256(p)==s.Life256&&NecromancerEffects.ManaBalance256(p)==s.Mana256,"Original Echoing fractional resources changed");
            }
            return;
        }
        var saved=new List<Saved>();foreach(bool female in new[]{false,true})
        {
            var p=CleaveActionChecks.Fresh(female);ClassSkillChecks.Train(p,"warlock.echoingstrike",7);ClassSkillChecks.Train(p,"warlock.mirroredblades",4);ClassSkillChecks.Train(p,"warlock.bladewarp",3);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(31),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Echoing helm");ClassGear.Refresh(p);
            var values=EchoingStrikePrepared.Capture(SkillMechanics.Values(p,CombatAbility.EchoingStrike));Require(values.Rank==8&&values.HardMirroredBlades==4&&values.HardBladeWarp==3&&values.TotalAttacks==1,"Real gear donated hard synergy or crossed hard5attack threshold");
            p.AddToBackpack(helm);ClassGear.Refresh(p);var bare=EchoingStrikePrepared.Capture(SkillMechanics.Values(p,CombatAbility.EchoingStrike));
            Require(bare.Rank==7&&bare.DamageSynergyPercent==values.DamageSynergyPercent&&bare.TotalAttacks==values.TotalAttacks,"Removing gear changed hard suppliers");
            Require(p.EquipItem(helm),"Echoing re-equip");ClassGear.Refresh(p);
            SkillHealth.SetBalance256(p,p.HitsMax*256-129);NecromancerEffects.SetMana256(p,p.ManaMax*256-17);
            saved.Add(new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,female,values,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p)));
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
}
