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

public static class ArcticPotionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ArcticPotionChecks));
    private sealed record Saved(uint Owner,uint Target,uint Item,string ItemState,ArcticMissile Shot,int Life,int Mana,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"arctic-potion-fixtures.json");
    private static int _scene;
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static PlayerMobile Fresh()
    {
        var p=new PlayerMobile{Name="ArcticPotionFixture",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Potion fixture strength");}
        FixtureSpace.Corridor(p,6550,20,3200+32*_scene++);return p;
    }
    private static ShockWaveTarget Target(PlayerMobile p)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}
        t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static ArcticMissile One(PlayerMobile p,DateTime at)
    {
        var row=SkillMechanics.For(CombatAbility.ArcticBlast);
        var values=new SkillValues(row,key=>key==row.SourceKey?1:0,_=>0,99);
        long id=DruidArcticStream.Create(p,new(p.X*2+20,p.Y*2),values,at);
        Require(id>0,"Arctic potion emission missing");return DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==id);
    }
    private static ArcticMissile Fixed(ArcticMissile s)=>s with{Damage=s.Damage with{Minimum256=25600,Maximum256=25600}};
    private static int Hit(PlayerMobile p,ShockWaveTarget t,ArcticMissile s)
    {
        int before=SkillHealth.Balance256(t);var next=DruidArcticStream.Impact(p,t,s);
        Require(next.LastCollision==t.Serial.Value&&next.Modifiers==s.Modifiers,"Impact lost history/mastery snapshot");
        return before-SkillHealth.Balance256(t);
    }
    private static Item Helmet(PlayerMobile p)
    {
        // Original supported Nightwing's Veil,not a synthetic mastery stat.
        var item=NamedGear.Create("unique/343",99,new Random(343),true);p.AddToBackpack(item);
        Require(p.EquipItem(item)&&NamedElements.MasteryPercent(p,"cold")>0,"Original cold mastery helmet");return item;
    }
    private static void Policies()
    {
        var held=DruidArcticStream.Snapshot();var p=Fresh();var t=Target(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention);
        try
        {
            var helmet=Helmet(p);var s=One(p,at);int mastery=s.Modifiers?.ColdMastery??0;
            Require(mastery>0&&s.Modifiers.Potion==0,"Emission omitted captured mastery or froze potion bonus");
            Require(s.Damage.Minimum256==256+256*mastery/100&&s.Damage.Maximum256==512+512*mastery/100,"Original gear did not master cold once at birth");
            Require(Hit(p,t,Fixed(s))==25600,"Live gear mastered the captured original cold twice");
            p.AddToBackpack(helmet);Require(NamedElements.MasteryPercent(p,"cold")==0,"Unequipped mastery remained live");
            int mana=NecromancerEffects.ManaBalance256(p);
            Require(Hit(p,t,Fixed(s))==25600,"Original cold was changed or mastered twice");
            Require(CombatPotions.Apply(p,3,at),"Magical Penetration application");
            int bonus=CombatPotions.BonusAt(p,"magical-penetration",s.NextUtc);Require(bonus==10,"Potion fixture amount");
            int coldBonus=2560+2560*mastery/100;
            Require(Hit(p,t,Fixed(s))==25600+3*2560+coldBonus,"Potion lanes or captured mastery differ");
            // A new shot is allowed while the potion is already active.
            var fresh=One(p,at.AddMilliseconds(80));Require(fresh.Modifiers==new FirestormModifiers(0,0,0,0,0),"New shot borrowed removed gear or froze potion");
            Require(Hit(p,t,Fixed(fresh))==35840,"Active potion prevented emission or changed its four additions");
            t.SetResistance(ResistanceType.Cold,100);t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Poison,50);
            if(TimedEffects.TryActor(t,out var chilled)){chilled.ChillUntil=DateTime.MinValue;}
            Require(Hit(p,t,Fixed(s))==5120&&ColdStates.OtherRate(t)==0,"Added elements did not respect independent immunities/resistances");
            var buff=TimedEffects.Buff(p,TimedEffectCategory.Potion,at);
            t.SetResistance(ResistanceType.Cold,0);
            Require(Hit(p,t,Fixed(s) with{NextUtc=buff.ExpiresUtc.AddMilliseconds(40)})==25600,"Expired potion stayed frozen in a shot");
            var legacy=Fixed(s) with{Modifiers=null};int life=SkillHealth.Balance256(t);
            try{DruidArcticStream.Impact(p,t,legacy);throw new InvalidOperationException("Legacy mastery was invented");}catch(NotSupportedException){}
            Require(SkillHealth.Balance256(t)==life,"Unsupported legacy potion contact damaged target");
            TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);
            Require(Hit(p,t,legacy)==25600,"Legacy pure-cold emission changed");
            string legacyJson=Json(legacy);Require(!legacyJson.Contains("Modifiers"),"Legacy V0 output gained a false snapshot");
            var snapshot=DruidArcticStream.Snapshot();string before=Json(snapshot);
            var legacyState=snapshot with{Missiles=snapshot.Missiles.Select(x=>x.Id==s.Id?JsonSerializer.Deserialize<ArcticMissile>(legacyJson):x).ToArray()};
            DruidArcticStream.Restore(JsonSerializer.Deserialize<ArcticStreamSave>(Json(legacyState)));
            Require(DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==s.Id).Modifiers==null,"Legacy restore invented mastery fields");
            DruidArcticStream.Restore(snapshot);
            var invalid=snapshot.Missiles.Select(x=>x.Id==s.Id?x with{Modifiers=x.Modifiers with{Potion=10}}:x).ToArray();
            try{DruidArcticStream.Restore(snapshot with{Missiles=invalid});throw new InvalidOperationException("Frozen potion bonus accepted");}catch(InvalidDataException){}
            Require(Json(DruidArcticStream.Snapshot())==before,"Invalid extension restore changed active pool");
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Emission/impact spent channel mana");
        }
        finally{DruidArcticStream.Restore(held);t.Delete();p.Delete();}
    }
    private static void Create()
    {
        // The retained stream suite advances its global pool at +Retention.
        // Keep this saved shot beyond that separate fixture's action epoch;
        // exact wound comparisons must not be rewritten by unrelated shots.
        Policies();var p=Fresh();var t=Target(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention).Add(FixtureSpace.Retention);var helmet=Helmet(p);
        var shot=One(p,at);p.AddToBackpack(helmet);Require(CombatPotions.Apply(p,3,at),"Saved potion application");
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,helmet.Serial.Value,Json(ClassGear.State(helmet)),shot,
            SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        var t=World.FindMobile((Serial)saved.Target)as ShockWaveTarget;var item=World.FindItem((Serial)saved.Item);
        Require(p is{Alive:true,Deleted:false}&&t is{Alive:true,Deleted:false}&&item!=null&&item.Parent==p.Backpack
            &&Json(ClassGear.State(item))==saved.ItemState,"Saved original owner/target/helmet changed");
        var shot=DruidArcticStream.Snapshot().Missiles.Single(x=>x.Id==saved.Shot.Id);
        Require(shot==saved.Shot&&shot.Modifiers.ColdMastery>0&&NamedElements.MasteryPercent(p,"cold")==0,"Reload lost captured mastery/unequipped state");
        Require(SkillHealth.Balance256(t)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,
            $"Reload changed life {saved.Life}->{SkillHealth.Balance256(t)} or mana {saved.Mana}->{NecromancerEffects.ManaBalance256(p)} (max {p.ManaMax*256})");
        int mastery=shot.Modifiers.ColdMastery;
        // The skeleton fixture restores its canonical poison resistance.
        // Recipient defenses remain live;only the missile's own packet and
        // masteries are frozen. Keep an independent per-lane expectation.
        Require(t.FireResistance==0&&t.ColdResistance==0&&t.EnergyResistance==0&&t.PoisonResistance is 0 or 50,
            "Reloaded receiver escaped the controlled resistance domain");
        int expected=25600+2*2560+2560+2560*mastery/100+2560*(100-t.PoisonResistance)/100;
        int bonus=CombatPotions.BonusAt(p,"magical-penetration",shot.NextUtc);
        string buff=Json(TimedEffects.Buff(p,TimedEffectCategory.Potion,shot.NextUtc));
        int loss=Hit(p,t,Fixed(shot));
        Require(loss==expected,$"Reloaded potion loss {loss},expected {expected},bonus {bonus},buff {buff},resists {t.FireResistance}/{t.ColdResistance}/{t.EnergyResistance}/{t.PoisonResistance}");
        saved=saved with{Life=SkillHealth.Balance256(t),Generation=saved.Generation+1};File.WriteAllText(Manifest,Json(saved));
        Policies();
    }
    public static void Configure()
    {
        Register("arctic-potion-check",Create);Register("arctic-potion-reload-check",Reload);
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Arctic potion delivery and saved mastery",_=>Core.LoopContext.Post(()=>
    {
        try{run();Log.Information("Arctic potion:shared elemental spell delivery;original cold/mastery,live bonus/expiry,independent immunity,legacy refusal,atomic V0 extension and original Nightwing snapshot through reloads;held timing remains separate");Log.Information("{Name}: passed",name);}
        catch(Exception e){Log.Error(e,"{Name}: failed",name);}
    }));
}
