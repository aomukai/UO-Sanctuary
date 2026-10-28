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
public static class MirroredBladesChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MirroredBladesChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,bool Female,MirroredBladeValues Values);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mirrored-blades-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Mirrored Blades suppliers and current SQ25",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("mirrored-blades-check",Check);Register("mirrored-blades-reload-check",Reload);}
    private static SkillValues Values(int rank,int warp=0,int echo=0)=>new(SkillMechanics.For(CombatAbility.MirroredBlades),k=>k=="Mirrored Blades"?rank:255,k=>k=="Blade Warp"?warp:k=="Echoing Strike"?echo:0,99);
    private static void Model()
    {
        int count=0,plans=0;for(int rank=1;rank<=255;rank++)foreach(int warp in new[]{0,1,20})foreach(int echo in new[]{0,1,20})
        {
            int curve=Math.Min(100,110*rank/(rank+6)),step=rank>15?5:rank>10?4:rank>5?3:2;
            var v=MirroredBladesPrepared.Capture(Values(rank,warp,echo));
            Require(v==new MirroredBladeValues(rank,100+15*(rank-1),10+20*curve/100,Math.Min(2+rank/5,5),50+70*curve/100,(rank+9)*128,step*4,step*8,step*10,0,0,0,0),$"Mirror independent source suppliers rank={rank}: {v}");
            var attacks=MirroredBladesPrepared.Attacks(v);Require(attacks.Length==v.TotalAttacks&&attacks[0]==new MirroredBladeAttack(0,100,null,null,null),"Primary mirror attack was scaled or got duplicate divisors");
            foreach(var a in attacks)
            {
                Require(a.Ordinal==0||a.DamagePercent==v.DuplicatePercent&&a.RawMeleeDivisor==step*4&&a.RawRangedDivisor==step*8&&a.RawPlayerDivisor==step*10,"Duplicate-specific supplier lost");
                foreach(int amount in new[]{0,1,257,1048575,1048576,1048577,100000001})
                {int expected=a.Ordinal==0?amount:amount>1048576?amount/100*a.DamagePercent:(int)((long)amount*a.DamagePercent/100);Require(MirroredBladesPrepared.ScaleSuppliedDirect256(amount,a)==expected,"Supplied direct-scalar source arithmetic");}plans++;
            }
            count++;
        }
        Require(count==2295&&plans==11232&&!WarlockCombat.Implemented(CombatAbility.MirroredBlades),"Mirror supplier count or learned route");
        foreach(var (rank,total,percent) in new[]{(1,2,60),(4,2,80),(5,3,85),(10,4,97),(12,4,101),(14,4,103),(15,5,104),(16,5,106),(20,5,108),(21,5,109),(255,5,120)})
        {var v=MirroredBladesPrepared.Capture(Values(rank));Require(v.TotalAttacks==total&&v.DuplicatePercent==percent,"Mirror cap/effective-rank damage boundary");}
        foreach(int divisor in new[]{8,12,16,20,24,30,32,40,50})foreach(int life in new[]{0,257,40959,1000001})foreach(int res in new[]{-50,0,33,100})foreach(int bonus in new[]{0,100,350})
        {long d=divisor+(long)divisor*bonus/100,raw=life/d;Require(MirroredBladesPrepared.SuppliedCrushingLoss256(life,divisor,res,bonus)==raw-raw*Math.Clamp(res,0,100)/100,"Supplied raw Crushing Blow denominator/double-ranged-halving");}
        try{MirroredBladesPrepared.Attacks(MirroredBladesPrepared.Capture(Values(1)) with{TotalAttacks=7});throw new InvalidOperationException("Malformed mirror plan accepted");}catch(ArgumentException){}
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        int[] points=[17,16,21,16,19,18,18,17,16,16,16,16,17,17],hits=[12,10,11,10,12,11,11,8,10,10,10,10,12,12];
        for(int i=0;i<14;i++)
        {
            var row=MirroredBladesPrepared.Sequence(codes[i]);Require(row.Sequence==25&&row.Points==points[i]&&row.Frames==points[i]&&row.RawAuxiliary==256&&row.Rows[hits[i]][4]==1,"Current SQ25 profile");row.Rows[hits[i]][4]=0;Require(MirroredBladesPrepared.Sequence(codes[i]).Rows[hits[i]][4]==1,"Mirror sequence mutation escaped");
            foreach(int advance in new[]{64,128,256,384,512})foreach(int frameBonus in new[]{0,1,3})
            {var events=MirroredBladesPrepared.Schedule(codes[i],advance,frameBonus,100).Events;Require(events.Length==2&&events.Single(x=>x.Type==SourceAnimationEvents.ModeChange).Frame==100+Math.Max(1,(hits[i]*256-frameBonus*256+advance-1)/advance)&&events.Single(x=>x.Type==SourceAnimationEvents.EndAnimation).Frame==100+(points[i]*256-frameBonus*256+advance-1)/advance,"SQ25 supplied rate/frame bonus clock invented extra mirror events");}
        }
        Log.Information("Mirrored Blades:2295supplier contexts,11232attack descriptors,14SQ25profiles,210supplied clocks;emission and packet placement remain gated");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Mirror talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Mirror train "+id);}}
    private static PlayerMobile Fresh(bool female)
    {
        var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Player=true,Name="MirroredBladeFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Mirror Strength");}while(p.RawDex<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Mirror Dexterity");}Train(p,"warlock.mirroredblades",14);BaseWeapon weapon=female?new Bow():new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Mirror fixture equip");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static void Check()
    {
        Model();var saved=new Saved[2];int i=0;foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);var before=MirroredBladesPrepared.Capture(SkillMechanics.Values(p,CombatAbility.MirroredBlades));Require(before.Rank==14&&before.TotalAttacks==4,"Mirror pre-item cap boundary");
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(27),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Mirror Tarnhelm");ClassGear.Refresh(p);
            int mana=NecromancerEffects.ManaBalance256(p);var v=MirroredBladesPrepared.Capture(SkillMechanics.Values(p,CombatAbility.MirroredBlades));Require(v is{Rank:15,TotalAttacks:5,DuplicatePercent:104,Mana256:3072,AttackPercent:310}&&MirroredBladesPrepared.Attacks(v).Length==5&&NecromancerEffects.ManaBalance256(p)==mana,"Mirror soft-rank15 plan/raw mana supplier mutated owner");
            saved[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,female,v);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&MirroredBladesPrepared.Capture(SkillMechanics.Values(p,CombatAbility.MirroredBlades))==s.Values,"Retained Mirror character/gear/rank suppliers");Require(MirroredBladesPrepared.Attacks(s.Values).Length==5,"Reload lost early mirror cap");}
    }
}
