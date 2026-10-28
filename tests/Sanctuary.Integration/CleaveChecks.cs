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
public static class CleaveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(CleaveChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,bool Female,CleaveValues Values);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cleave-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Cleave prepared suppliers,SQ24 data and qualified contacts",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("cleave-check",Check);Register("cleave-reload-check",Reload);}
    private static SkillValues Values(int rank,int mirror=0,int purge=0,int blast=0)=>new(SkillMechanics.For(CombatAbility.Cleave),k=>k=="Cleave"?rank:255,k=>k=="Mirrored Blades"?mirror:k=="Hex Purge"?purge:k=="Eldritch Blast"?blast:0,99);
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int mirror in new[]{0,1,20})foreach(int purge in new[]{0,1,20})foreach(int blast in new[]{0,1,20})
        {
            var v=CleavePrepared.Capture(Values(rank,mirror,purge,blast));int slices=rank<20?rank+10:20;
            Require(v==new CleaveValues(rank,20+5*(rank-1)+10*(mirror+purge+blast),50+7*(rank-1),10+20*Math.Min(100,110*rank/(rank+6))/100,slices,slices*12,768),$"Cleave independent suppliers rank={rank} hards={mirror},{purge},{blast} actual={v}");count++;
        }
        Require(count==6885&&!WarlockCombat.Implemented(CombatAbility.Cleave),"Incomplete Cleave learned handler enabled");
        string[] codes=["HTH","1HT","2HT","1HS","2HS","BOW","XBW","STF","1JS","1JT","1SS","1ST","HT1","HT2"];
        int[] points=[19,16,20,16,18,19,19,22,19,19,19,19,19,19],hits=[12,9,10,9,10,12,12,10,12,12,12,12,12,12];
        for(int i=0;i<14;i++)
        {
            var row=CleavePrepared.Sequence(codes[i]);Require(row.Points==points[i]&&row.Frames==points[i]&&row.RawAuxiliary==256&&row.Rows[hits[i]][4]==1&&row.Rows.All(x=>x[1]==10),"Current player SQ24 source row");
            // Detached copies cannot corrupt the loaded source profile.
            row.Rows[hits[i]][4]=0;Require(CleavePrepared.Sequence(codes[i]).Rows[hits[i]][4]==1,"Mutable sequence escaped");
            foreach(int advance in new[]{64,128,256,384,512})foreach(int bonus in new[]{0,1,3})
            {
                var plan=CleavePrepared.Schedule(codes[i],advance,bonus,100);var hit=plan.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange);
                int expectedHit=100+Math.Max(1,(hits[i]*256-bonus*256+advance-1)/advance);
                int end=100+(points[i]*256-bonus*256+advance-1)/advance;
                Require(hit.Kind==1&&hit.Frame==expectedHit&&plan.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame==end,"Supplied-rate event schedule");
            }
        }
        try{CleavePrepared.Sequence("???");throw new InvalidOperationException("Unknown Cleave class accepted");}catch(NotSupportedException){}
        Require(CleavePrepared.Capture(Values(19)).TooltipArcDegrees==348&&CleavePrepared.Capture(Values(20)).TooltipArcDegrees==240,"Raw arc conflict was silently rewritten");
        Log.Information("Cleave:6885supplier controls,14current SQ24profiles,210supplied clocks;no arc or modern start body inferred");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Cleave talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Cleave train "+id);}}
    private static PlayerMobile Fresh(bool female)
    {
        var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Player=true,Name="CleaveFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Cleave Strength");}Train(p,"warlock.cleave",20);Train(p,"warlock.mirroredblades");Train(p,"warlock.hexpurge");Train(p,"warlock.eldritchblast");var weapon=new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Cleave weapon");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static ShockWaveTarget Enemy(PlayerMobile p)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(10000);SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);return t;}
    private sealed class QuietRandom:Random{public override int Next(int max)=>max>0?max-1:0;public override double NextDouble()=>0.999999;}
    private static void Contacts(PlayerMobile p,CleaveValues v)
    {
        var t=Enemy(p);var second=Enemy(p);try
        {
            int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(t),calls=0;var weapon=(BaseWeapon)CombatTiming.Equipped(p);int durability=weapon.HitPoints;
            bool Contact(Mobile victim,bool eligible,double hit)=>CleavePrepared.Contact(p,victim,v,eligible,false,()=>{calls++;return hit;},0,_=>0,new QuietRandom());
            Require(!Contact(t,false,0)&&calls==0&&hp==SkillHealth.Balance256(t),"Unqualified supplied contact hit");
            Require(Contact(t,true,1)&&calls==1&&hp==SkillHealth.Balance256(t),$"Cleave miss contract calls={calls} hp={hp}/{SkillHealth.Balance256(t)} alive={p.Alive} map={p.Map} targetmap={t.Map}");
            int physical=ClassStats.SkillPhysicalRange256(p,v.DamagePercent,0).Min;
            Require(Contact(t,true,0)&&calls==2&&hp-SkillHealth.Balance256(t)==physical,"Cleave physical SrcDam128contact");
            int hp2=SkillHealth.Balance256(second);Require(Contact(second,true,0)&&calls==3&&hp2-SkillHealth.Balance256(second)==physical,"Independent supplied contact/hit check");
            var state=new EnchantState(TimedEffects.Now.AddMinutes(1),1,3*256,3*256,0,p.Serial.Value);SorceressEffects.Profiles.Store(p,new(Enchantment:state));
            hp=SkillHealth.Balance256(t);Require(Contact(t,true,0)&&hp-SkillHealth.Balance256(t)==physical+3*256,"Cleave scaled or lost ordinary Enchant element");SorceressEffects.Profiles.Clear(p);
            Require(NecromancerEffects.ManaBalance256(p)==mana&&weapon.HitPoints==durability,"Contact service invented start mana/durability cadence");
            var held=CombatTiming.Equipped(p);p.AddToBackpack(held);ClassGear.Refresh(p);
            try{Contact(t,true,0);throw new InvalidOperationException("Unarmed Cleave accepted");}catch(NotSupportedException){}
            Require(p.EquipItem(held),"Cleave re-equip");ClassGear.Refresh(p);
        }
        finally{SorceressEffects.Profiles.Clear(p);t.Delete();second.Delete();}
    }
    private static void Check()
    {
        Model();var saved=new Saved[2];int i=0;foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(27),true);Require(helm!=null,"Tarnhelm missing");p.AddToBackpack(helm);Require(p.EquipItem(helm),"Cleave Tarnhelm");ClassGear.Refresh(p);
            var v=CleavePrepared.Capture(SkillMechanics.Values(p,CombatAbility.Cleave));Require(v.Rank==21&&v.AttackPercent==190&&v.DamagePercent==150&&v.SpeedPercent==27,"Cleave trained rank21/hard-only synergies");Contacts(p,v);
            saved[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,female,v);
        }
        // Inspection DTOs and ordinary characters persist; no in-flight Cleave cast is registered.
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&CleavePrepared.Capture(SkillMechanics.Values(p,CombatAbility.Cleave))==s.Values,"Cleave retained actor/gear/rank supplier");FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);Contacts(p,s.Values);}
    }
}
