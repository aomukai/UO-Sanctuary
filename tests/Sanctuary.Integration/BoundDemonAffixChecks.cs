// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BoundDemonAffixChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BoundDemonAffixChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,uint Weapon,uint Target,BoundDemonState State,int Life,int Mana,int Stamina);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bound-demon-affix-fixtures.json");
    private static bool Creating;
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BoundAffixFixture"?TimeSpan.FromHours(1):old?.Invoke(m)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bound-demon-affix-reload-check":"bound-demon-affix-check";
            ConsoleInputHandler.RegisterCommand(name,"Bound captured Strong/Fast/Spectral native weapons/resists/facets/saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static Daemon Bind(PlayerMobile p,int hard)
    {
        ClassSkillChecks.Train(p,"warlock.binddemon",hard);Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}
        b.Name="BoundAffixFixture";b.SetDamage(100,100);foreach(var t in Enum.GetValues<ResistanceType>()){b.SetResistance(t,0);}
        b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();b.AddItem(new Dagger());
        Require(BoundDemonHosts.BindPrepared(p,b,out var why),"Affix native bind: "+why);b.AIObject.AITimer.Stop();return b;
    }
    private static void Common(Daemon body)
    {
        var s=BoundDemonHosts.State(body);var strong=EncounterProfiles.ExtraStrong(s.Difficulty,false);
        Require(BoundDemonHosts.DamagePercent(body)==s.Birth.DamagePercent+strong.DamagePercent&&BoundDemonUpgrades.AttackPercent(body)==strong.AttackPercent,"Direct strength difficulty values differ");
        int basis=CreatureProfiles.AttackRating(body);Require(ClassStats.AttackRating(body,25)==basis+(int)((long)basis*(strong.AttackPercent+25)/100),"Strong AR did not join shared percentage pool");
        int velocity=s.Birth.VelocityPercent+(s.Birth.Modifier10==6?100:0);
        Require(Math.Abs(ClassGearHooks.MovementDelay(body,1)-100.0/(100+velocity))<.000001,"Fast did not join native movement pool");
        Require(ClassGearHooks.AttackDelay(body,TimeSpan.FromSeconds(1)).Ticks==TimeSpan.TicksPerSecond*100/Math.Clamp(100+s.Birth.AttackSpeed,15,175),"Existing movement-only Extra Fast changed attack delay");
    }
    private static void Clean(BaseCreature target)
    {TimedEffects.CleansePoison(target,0,TimedEffects.Now);ColdStates.Remove(target,TimedEffects.Now);SkillHealth.SetBalance256(target,SkillHealth.Maximum256(target));}
    private static int Packet(Daemon body,BaseCreature target,int channel,bool ignore=false,bool ranged=false,int physical=100,int fire=0)
    {
        var old=BoundDemonUpgrades.Roll;int draws=0;BoundDemonUpgrades.Roll=n=>++draws==1?n-1:channel;
        int before=SkillHealth.Balance256(target);try
        {using var attempt=ClassCombatHooks.MeleeAttempt(body,target,ranged);AOS.Damage(target,body,100,ignore,physical,fire,0,0,0,weaponDamage:true,archer:ranged);}
        finally{BoundDemonUpgrades.Roll=old;}
        Require(draws==2,"Spectral must draw amount then channel once");return before-SkillHealth.Balance256(target);
    }
    private static void Elements(PlayerMobile p,Daemon body,BaseCreature target)
    {
        Require(BoundDemonHosts.State(body).Birth is{Rank:15,MasteryRank:11,FlatDamage:400,DamagePercent:255},"Literal captured Spectral context changed");
        for(int c=0;c<5;c++)
        {
            Clean(target);int loss=Packet(body,target,c);Require(loss==(c==4?2450:2550)*256,"Spectral typed native hit/order/damage scaled incorrectly: "+c);
            Require(c!=3||GearEffects.ChillPercent(target)>0,"Spectral cold lacks survivor chill");
            Require(c!=4||TimedEffects.TryActor(target,out var a)&&a.PoisonMs>0,"Spectral poison lacks survivor aggregate");
        }
        Clean(target);Require(Packet(body,target,0,ignore:true)==2550*256,"Armor-ignore lost spectral addition");
        Clean(target);Require(Packet(body,target,1,ranged:true)==2550*256,"Native ranged attempt lost spectral addition");
        Clean(target);Require(Packet(body,target,2,physical:0,fire:100)==2160*256,"Spectral changed native fire split or flat physical rule");
        Clean(target);int before=SkillHealth.Balance256(target),draws=0;var old=BoundDemonUpgrades.Roll;BoundDemonUpgrades.Roll=n=>{draws++;return 0;};
        try{AOS.Damage(target,body,100,false,0,100,0,0,0,spellDamage:true);Require(before-SkillHealth.Balance256(target)==100*256&&draws==0,"Spectral or strength modified a spell");}
        finally{BoundDemonUpgrades.Roll=old;}
        Clean(target);bool refused=false;try{AOS.Damage(target,body,100,false,100,0,0,0,0,weaponDamage:true);}catch(NotSupportedException){refused=true;}
        Require(refused&&SkillHealth.Balance256(target)==SkillHealth.Maximum256(target),"Unscoped native spectral silently guessed post-hit context");
        Clean(target);target.SetResistance(ResistanceType.Physical,100);refused=false;
        try{Packet(body,target,4);}catch(NotSupportedException){refused=true;}finally{target.SetResistance(ResistanceType.Physical,0);}
        Require(refused&&SkillHealth.Balance256(target)==SkillHealth.Maximum256(target)&&(!TimedEffects.TryActor(target,out var immune)||immune.PoisonMs==0),"Fully absorbed Spectral poison silently applied or changed health");
        Clean(target);Require(Packet(body,target,0)==2550*256,"Refused Spectral context leaked into next native attack");
        // A real native weapon call owns its attempt scope,not a fixture adapter.
        var hook=ClassCombatHooks.SkillResistedDamage;ClassSkillDamage? captured=null;int rolled=0;BoundDemonUpgrades.Roll=n=>n==5?0:n-1;
        ClassCombatHooks.SkillResistedDamage=(to,from,damage,phys,fire,cold,poison,lightning,spell,weapon,ranged)=>
        {var result=hook(to,from,damage,phys,fire,cold,poison,lightning,spell,weapon,ranged);if(from==body&&to==target){captured=result;rolled=damage;}return result;};
        try{Clean(target);((Dagger)body.FindItemOnLayer(Layer.OneHanded)).OnHit(body,target);Require(rolled>0&&captured is{} hit&&hit.Total256==NamedElements.To256((rolled+400)*4.9+100),"Real Dagger OnHit missed shared Spectral capture");}
        finally{ClassCombatHooks.SkillResistedDamage=hook;BoundDemonUpgrades.Roll=old;}
        Log.Information("Bound affixes:all five deterministic Spectral channels/max raw basis,post-direct surviving cold/credited poison,real Dagger OnHit,ranged and armor-ignore paths pass;flat physical/ED applied once,elemental spell unchanged;unscoped native weapon context explicitly refuses");
    }
    private static void Scene(NetState network)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}p.Name="BoundAffixFixture";p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        FixtureSpace.Corridor(p,7040,40,3550);var account=FixtureNetwork.Authenticate(network,p,"bound-affix-");Daemon body=null;bool kept=false;
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);target.SetHits(50000);target.Hits=target.HitsMax;foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}target.AIObject.AITimer.Stop();
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");body=Bind(p,4);Require(BoundDemonHosts.State(body).Birth.Rank==5&&BoundDemonUpgrades.Has(body,EliteModifier.ExtraStrong)&&!BoundDemonUpgrades.Has(body,EliteModifier.ExtraFast),"Soft rank5 threshold failed");Common(body);
            var prior=body;body=Bind(p,9);Require(prior.Deleted&&BoundDemonHosts.State(body).Birth.Rank==10&&BoundDemonUpgrades.Has(body,EliteModifier.ExtraFast)&&!BoundDemonUpgrades.Has(body,EliteModifier.SpectralHit),"Soft rank10 threshold or replacement failed");Common(body);
            prior=body;body=Bind(p,14);Require(prior.Deleted&&BoundDemonUpgrades.Has(body,EliteModifier.SpectralHit)&&!BoundDemonUpgrades.Has(body,EliteModifier.AuraEnchanted)&&body.FireResistance==40&&body.ColdResistance==40&&body.EnergyResistance==40,"Soft rank15/resistance writers failed");Common(body);Elements(p,body,target);
            var state=BoundDemonHosts.State(body);p.AddToBackpack(helm);ClassSkillChecks.Train(p,"warlock.binddemon",20);Require(BoundDemonHosts.State(body)==state&&!BoundDemonUpgrades.Has(body,EliteModifier.AuraEnchanted),"Gear/training rewrote born modifiers");
            SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)/2-17);var facets=SummonDifficulties.Facets();try
            {
                foreach(var d in new[]{MonsterDifficulty.Nightmare,MonsterDifficulty.Hell,MonsterDifficulty.Normal})
                {
                    int life=SkillHealth.Balance256(body),maximum=SkillHealth.Maximum256(body);
                    SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new FacetDifficulty(p.Map.MapID,d)).ToArray());BoundDemonHosts.Sync(p);Common(body);
                    Require(Math.Abs(SkillHealth.Balance256(body)-(long)life*SkillHealth.Maximum256(body)/maximum)<=1&&BoundDemonHosts.State(body).Birth==state.Birth,"Facet affixes reset wounds/birth");
                }
            }finally{SummonDifficulties.SetFacets(facets);BoundDemonHosts.Sync(p);}
            Require(body.FireResistance==40&&body.ColdResistance==40&&body.EnergyResistance==40,"Facet return double-applied Spectral resistance");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,body.Serial.Value,body.FindItemOnLayer(Layer.OneHanded).Serial.Value,target.Serial.Value,BoundDemonHosts.State(body),SkillHealth.Balance256(body),body.Mana,body.Stam)));kept=true;
            Log.Information("Bound affixes:effective rank5Strong/rank10movement-only Fast/rank15Spectral snapshot thresholds,actual shared AR/ED/movement/attack-delay pools,replacement and three difficulty transitions preserve wounds/birth;Spectral resistance returns without accumulation;aura30 still unfinished");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){body?.Delete();target.Delete();p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many affix reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(Daemon)World.FindMobile((Serial)s.Body);var target=(BaseCreature)World.FindMobile((Serial)s.Target);
        Require(BoundDemonHosts.State(b)==s.State&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==s.Life&&b.Mana==s.Mana&&b.Stam==s.Stamina&&b.FindItemOnLayer(Layer.OneHanded)?.Serial.Value==s.Weapon&&b.FireResistance==40&&b.ColdResistance==40&&b.EnergyResistance==40,"Saved original affixes/resources/items changed or resistance accumulated");
        var account=FixtureNetwork.Authenticate(network,p,"bound-affix-reload-");try
        {
            PersistentMinions.Resume(p);b.AIObject.AITimer.Stop();Common(b);Clean(target);Require(Packet(b,target,0)==2550*256,"Reloaded native Spectral packet changed");
            Require(BoundDemonHosts.State(b)==s.State&&SkillHealth.Balance256(b)==s.Life,"Resume recaptured stats or healed bound original");PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));Log.Information("Bound affixes:original rank15birth/modifiers/type/weapon/wounds/resources and native40resistances survive reload without accumulation;native Spectral resumes despite later rank20training");
        }finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
