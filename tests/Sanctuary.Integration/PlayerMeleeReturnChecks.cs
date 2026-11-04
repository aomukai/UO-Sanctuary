// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PlayerMeleeReturnChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerMeleeReturnChecks));
    private sealed record Saved(uint Owner,uint Enemy,int Life,int EnemyLife,int Mana,Point3D Point,int Map,ColdArmorState Armor,int Generation=0);
    private sealed class Ending(Action run):IDisposable{public void Dispose()=>run();}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-melee-return-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {Register("player-melee-return-check",Check);Register("player-melee-return-reload-check",Reload);}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Fixed NPC melee player HP,reaction and death return",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int row=2050)
    {
        var p=ColdArmorChecks.Owner(CombatAbility.ShiverArmor);p.Name="ColdArmorFixturePlayerReturn";FixtureSpace.Corridor(p,7000,24,row);
        Require(SorceressCombat.Release(p,CombatAbility.ShiverArmor,Serial.Zero,p.Location),"Paid Shiver setup");return p;
    }
    private static MageMissileWitness Enemy(PlayerMobile p)
    {
        var enemy=new MageMissileWitness{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        enemy.SetHits(5000);enemy.Hits=enemy.HitsMax;enemy.SetDamage(100,100);enemy.SetResistance(ResistanceType.Cold,0);TimedEffects.PreventHealing(enemy,TimedEffects.Now);return enemy;
    }
    private static void Clean(PlayerMobile p,MageMissileWitness enemy)
    {enemy.BeforeDamage=null;enemy.Corpse?.Delete();enemy.Delete();p.Corpse?.Delete();p.Delete();}
    private static void NativeLethal(bool enabled)
    {
        var p=Owner();var enemy=Enemy(p);var hook=ClassCombatHooks.WeaponDamageHealth;int reactions=0;
        try
        {
            SkillHealth.SetBalance256(p,256);int life=SkillHealth.Balance256(enemy),mana=NecromancerEffects.ManaBalance256(p);var armor=SorceressEffects.Armor(p);
            enemy.BeforeDamage=(_,from)=>{Require(from==p&&p.Alive&&p.Hits==0&&SorceressEffects.Armor(p)==armor&&NecromancerEffects.ManaBalance256(p)==mana,"Late reaction did not observe committed zero HP and retained armor/mana");reactions++;};
            if(!enabled){ClassCombatHooks.WeaponDamageHealth=null;}
            ((BaseWeapon)enemy.Weapon).OnHit(enemy,p);
            Require(!p.Alive&&p.Corpse!=null&&p.LastKiller==enemy&&SorceressEffects.Armor(p)==null,"Native lethal hit failed to complete player death");
            Require(reactions==(enabled?1:0)&&(SkillHealth.Balance256(enemy)<life)==enabled,"Native lethal Shiver/disabled-hook negative control differed");
        }
        finally{ClassCombatHooks.WeaponDamageHealth=hook;Clean(p,enemy);}
    }
    private static void FixedAndMiss()
    {
        var p=Owner();var enemy=Enemy(p);int reactions=0;
        try
        {
            int life=SkillHealth.Balance256(p);enemy.BeforeDamage=(_,_)=>{Require(p.Alive&&p.Hits>0,"Miss reaction altered player health");reactions++;};
            ((BaseWeapon)enemy.Weapon).OnMiss(enemy,p);Require(reactions==1&&SkillHealth.Balance256(p)==life,"Native miss changed HP or missed reaction");
            SkillHealth.SetBalance256(p,256);int enemyLife=SkillHealth.Balance256(enemy);enemy.BeforeDamage=(_,_)=>{Require(p.Alive&&p.Hits==0,"Fixed reaction ran outside pre-death return");reactions++;};
            var attempt=PaladinThorns.Attempt(enemy,p,false);
            AOS.Damage(p,enemy,10000,false,100,0,0,0,0,weaponDamage:true);
            Require(p.Alive&&p.Hits==0&&SkillHealth.Balance256(enemy)==enemyLife&&SorceressEffects.Armor(p)!=null,"HP/death deferred together or reaction ran before outer return");
            attempt.Dispose();int after=SkillHealth.Balance256(enemy);Require(!p.Alive&&reactions==2&&after<enemyLife,"Fixed melee return failed reaction then death");attempt.Dispose();Require(SkillHealth.Balance256(enemy)==after&&reactions==2,"Repeated disposal replayed reaction");
        }
        finally{Clean(p,enemy);}
    }
    private static void Exclusions()
    {
        foreach(string mode in new[]{"outside","spell","ranged","mismatch"})
        {
            var p=Owner();var enemy=Enemy(p);var other=Enemy(p);IDisposable attempt=null;
            try
            {
                SkillHealth.SetBalance256(p,256);int life=SkillHealth.Balance256(enemy),peer=SkillHealth.Balance256(other);
                if(mode!="outside"){attempt=PaladinThorns.Attempt(enemy,p,mode=="ranged");}
                AOS.Damage(p,mode=="mismatch"?other:enemy,10000,false,100,0,0,0,0,archer:mode=="ranged",weaponDamage:mode!="spell",spellDamage:mode=="spell");
                Require(!p.Alive,"Nonadmitted damage acquired deferred death: "+mode);attempt?.Dispose();attempt=null;
                Require(SkillHealth.Balance256(enemy)==life&&SkillHealth.Balance256(other)==peer,"Excluded damage created a melee reaction: "+mode);
            }
            finally{attempt?.Dispose();other.BeforeDamage=null;other.Corpse?.Delete();other.Delete();Clean(p,enemy);}
        }
    }
    private static void NestedAndException()
    {
        var a=Owner(2000);var b=Owner(1950);var first=Enemy(a);var second=Enemy(b);
        try
        {
            SkillHealth.SetBalance256(a,256);SkillHealth.SetBalance256(b,256);
            using(var outer=PaladinThorns.Attempt(first,a,false))
            {
                using(var inner=PaladinThorns.Attempt(second,b,false)){AOS.Damage(b,second,10000,false,100,0,0,0,0,weaponDamage:true);Require(b.Alive&&b.Hits==0&&a.Alive&&a.Hits==1,"Nested HP scope crossed player identities");}
                Require(!b.Alive&&a.Alive,"Inner return completed outer player");AOS.Damage(a,first,10000,false,100,0,0,0,0,weaponDamage:true);Require(a.Alive&&a.Hits==0,"Outer scope was not restored");
            }
            Require(!a.Alive&&!b.Alive,"Nested player deaths did not complete");
        }
        finally{Clean(a,first);Clean(b,second);}
        foreach(bool throwing in new[]{false,true})
        {
            var p=Owner();var enemy=Enemy(p);bool ended=false,caught=false;
            try
            {
                SkillHealth.SetBalance256(p,256);var attempt=PlayerMeleeReturn.Attempt(enemy,p,new Ending(()=>
                {Require(p.Alive&&p.Hits==0,"Return callback did not precede death");ended=true;SkillHealth.Heal(p,2560);if(throwing){throw new InvalidOperationException("deliberate return fixture");}}));
                AOS.Damage(p,enemy,10000,false,100,0,0,0,0,weaponDamage:true);try{attempt.Dispose();}catch(InvalidOperationException e)when(e.Message=="deliberate return fixture"){caught=true;}
                Require(ended&&caught==throwing&&!p.Alive,"Committed lethal result was canceled by reaction healing/exception");attempt.Dispose();
            }
            finally{Clean(p,enemy);}
        }
    }
    private static void BoneArmor()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixturePlayerReturn";FixtureSpace.Corridor(p,7000,24,1900);ClassSkillChecks.Train(p,"necromancer.bonearmor");var enemy=Enemy(p);
        try
        {
            Require(NecromancerCombat.Release(p,CombatAbility.BoneArmor,Serial.Zero,p.Location),"Native Bone Armor setup");SkillHealth.SetBalance256(p,256);int armor=NecromancerEffects.Armor256(p);
            using(PaladinThorns.Attempt(enemy,p,false)){AOS.Damage(p,enemy,1,false,100,0,0,0,0,weaponDamage:true);Require(p.Alive&&p.Hits==1&&NecromancerEffects.Armor256(p)==armor-256,"Fully absorbed melee acquired HP/death or double-consumed pool");}
            using(PaladinThorns.Attempt(enemy,p,false)){AOS.Damage(p,enemy,10000,false,100,0,0,0,0,weaponDamage:true);Require(p.Alive&&p.Hits==0&&NecromancerEffects.Armor256(p)==0,"Breaking Bone Armor did not retain immediate HP and deferred death");}
            Require(!p.Alive,"Breaking-hit death did not complete");
        }
        finally{Clean(p,enemy);}
    }
    private static void Check()
    {
        NativeLethal(false);NativeLethal(true);FixedAndMiss();Exclusions();NestedAndException();BoneArmor();
        var p=Owner();var enemy=Enemy(p);SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)*2/3+17);((BaseWeapon)enemy.Weapon).OnMiss(enemy,p);
        var row=SorceressEffects.Profiles.Find(p);var armor=row.Armor with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};SorceressEffects.Profiles.Store(p,row with{Armor=armor});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,enemy.Serial.Value,SkillHealth.Balance256(p),SkillHealth.Balance256(enemy),NecromancerEffects.ManaBalance256(p),p.Location,p.Map.MapID,armor)));p.Internalize();
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var enemy=(MageMissileWitness)World.FindMobile((Serial)saved.Enemy);
        Require(SkillHealth.Balance256(p)==saved.Life&&SkillHealth.Balance256(enemy)==saved.EnemyLife&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&SorceressEffects.Armor(p)==saved.Armor,"Original player/attacker wounds,mana or armor changed on reload");
        p.MoveToWorld(saved.Point,Map.Maps[saved.Map]);int life=SkillHealth.Balance256(enemy);((BaseWeapon)enemy.Weapon).OnMiss(enemy,p);
        Require(SkillHealth.Balance256(enemy)<life&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original saved actor did not react without HP/mana reset");p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{EnemyLife=SkillHealth.Balance256(enemy),Generation=saved.Generation+1}));NativeLethal(true);
    }
}
