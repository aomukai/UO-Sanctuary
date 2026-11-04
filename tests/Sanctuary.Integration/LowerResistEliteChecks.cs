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

public static class LowerResistEliteChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LowerResistEliteChecks));
    private sealed record Saved(uint Owner,uint Paladin,uint Target,uint Emitter,int Map,Point3D Point,int[] Bases,
        NecromancerCurse Curse,ConversionAllegiance Converted,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lower-resist-elite-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("lower-resist-elite-check",Check);Register("lower-resist-elite-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Lower Resist and real elite aura across Conversion allegiance",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureLowerResistElite";MagePetBuffChecks.Train(p,"necromancer.lowerresist",20);FixtureSpace.Corridor(p,7040,24,2800);return p;}
    private static PlayerMobile Paladin(PlayerMobile p)
    {var b=new PlayerMobile{Name="PoisonDaggerFixtureAuraPaladin",Body=0x190,Player=true};b.AddItem(new Backpack());ClassCreation.Profiles.Assign(b,DiabloClass.Paladin);ClassCreation.Profiles.AdvanceLevel(b,99);b.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);return b;}
    private static GraveyardSkeleton Target(PlayerMobile p)
    {var b=new GraveyardSkeleton{Name="PoisonDaggerFixtureLowerResistEliteTarget",Frozen=true,CantWalk=true,NoKillAwards=true};b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();return b;}
    private static EncounterCreature Emitter(PlayerMobile p)
    {
        var profile=EncounterProfiles.Profile("crypt-sentinel-90");var b=new EncounterCreature();
        b.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Hell,EncounterGrade.Unique,ChampionKind.Champion,[EliteModifier.AuraEnchanted],EliteAura.Conviction,null,new Random(173));
        b.Name="PoisonDaggerFixtureConvictionEmitter";b.Frozen=b.CantWalk=b.NoKillAwards=true;b.Home=p.Location;b.HomeMap=p.Map;
        b.MoveToWorld(new(p.X+3,p.Y+1,p.Z),p.Map);b.AIObject?.AITimer.Stop();EliteAuras.Observe(b);return b;
    }
    private static int[] Bases(GraveyardSkeleton t)=>[t.FireResistance,t.EnergyResistance,t.ColdResistance,t.PoisonResistance];
    private static void Base(GraveyardSkeleton t,int[] values)
    {t.SetResistance(ResistanceType.Fire,values[0]);t.SetResistance(ResistanceType.Energy,values[1]);t.SetResistance(ResistanceType.Cold,values[2]);t.SetResistance(ResistanceType.Poison,values[3]);}
    private static void Cast(PlayerMobile p,GraveyardSkeleton t)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,CombatAbility.LowerResist).Mana256;
        Require(NecromancerCombat.Begin(p,CombatAbility.LowerResist,t.Serial,t.Location,Core.TickCount),"Learned Lower Resist start");long at=ClassCombat.For(p).EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&ClassCombat.ResolveAction(p,at)&&!ClassCombat.ResolveAction(p,at),"Lower Resist early/failed/duplicate release");
        Require(NecromancerEffects.ManaBalance256(p)==before-cost&&NecromancerEffects.Curse(t)?.Ability==CombatAbility.LowerResist,"Lower Resist once-only payment/effect");
    }
    private static int Penalty(int basis,int full)=>basis>=100?full/5:full;
    private static void Combined(PlayerMobile p,GraveyardSkeleton t,EncounterCreature e,int[] bases,bool hit)
    {
        int rank=EliteAuras.SkillLevel(e),amount=Math.Min(150,30+5*(rank-1));
        Require(PaladinConversion.Active(t)&&!t.Controlled&&!t.Summoned&&t.GetMaster()==null,"Conversion fixture changed native pet flags");
        Require(EliteAuras.Level(t,EliteAura.Conviction,true)==rank,"Converted recipient missing hostile elite Conviction");EliteAuras.Refresh(t);
        string[] elements=["fire","ltng","cold","pois"];var native=Bases(t);
        for(int i=0;i<4;i++)
        {
            int aura=i==3?0:Penalty(bases[i],-amount),curse=Penalty(bases[i],-62),expected=Math.Clamp(bases[i]+aura+curse,-100,200);
            Require(EliteAuras.ConvictionOffset(t,elements[i])==aura&&native[i]==bases[i]+aura,"Native elite offset/base changed channel "+elements[i]);
            Require(NamedElements.Resistance(p,t,elements[i],native[i],0)==expected,"Independent immunity penalties/double aura channel "+elements[i]);
            Require(NamedElements.Resistance(p,t,elements[i],native[i],7)==Math.Clamp(expected-(expected<100?7:0),-100,200),"Post-break item pierce "+elements[i]);
            if(hit&&i<3)
            {
                int before=SkillHealth.Balance256(t);AOS.Damage(t,e,100,false,0,i==0?100:0,i==2?100:0,0,i==1?100:0,spellDamage:true);
                int damage=100*256*Math.Max(0,100-expected)/100;
                Require(before-SkillHealth.Balance256(t)==damage,"Actual elite elemental HP delivery "+elements[i]);
            }
        }
    }
    private static void Cases()
    {
        var p=Owner();var pal=Paladin(p);var t=Target(p);var e=Emitter(p);var conversions=PaladinConversion.Snapshot();var leases=PaladinConviction.Snapshot();
        try
        {
            foreach(var bases in new[]{new[]{100,130,99,100},new[]{99,100,130,0},new[]{0,99,100,130}})
            {
                NecromancerEffects.Profiles.Clear(t);Base(t,bases);t.SetHits(50000);t.Hits=t.HitsMax;
                Require(EliteAuras.Level(t,EliteAura.Conviction,true)==0,"Unconverted wild ally received hostile aura");Cast(p,t);var curse=NecromancerEffects.Curse(t);
                Require(PaladinConversion.ApplyAllegiance(pal,t,1000)&&NecromancerEffects.Curse(t)==curse,"Conversion removed ordinary Lower Resist");Combined(p,t,e,bases,true);
                Require(PaladinConviction.Apply(pal,t,SkillMechanics.Values(pal,CombatAbility.Conviction,20),TimedEffects.Now.AddMinutes(1)),"Class Conviction comparison lease");
                string[] elements=["fire","ltng","cold","pois"];var native=Bases(t);
                for(int i=0;i<4;i++)
                {
                    int expected=Math.Clamp(bases[i]+(i==3?0:Penalty(bases[i],-125))+Penalty(bases[i],-62),-100,200);
                    Require(NamedElements.Resistance(p,t,elements[i],native[i],0)==expected,"Class/elite Conviction stacked or reused reduced immunity base");
                }
                PaladinConviction.Restore(leases);Combined(p,t,e,bases,false);
                // Explicitly delivered expiry;natural conversion timing is a
                // separate retained suite. No deadline extension is inferred.
                PaladinConversion.Restore(PaladinConversion.Snapshot().Select(s=>s.Target==t.Serial.Value?s with{UntilUtc=TimedEffects.Now}:s).ToArray());PaladinConversion.Prune();EliteAuras.Refresh(t);
                Require(!PaladinConversion.Active(t)&&EliteAuras.Level(t,EliteAura.Conviction,true)==0&&Bases(t).SequenceEqual(bases)&&NecromancerEffects.Curse(t)==curse,"Expiry failed aura cleanup or changed independent curse");
            }
        }
        finally{PaladinConviction.Restore(leases);PaladinConversion.Restore(conversions);e.Delete();t.Delete();pal.Delete();p.Delete();}
    }
    private static void Check()
    {
        Cases();var p=Owner();var pal=Paladin(p);var t=Target(p);var e=Emitter(p);var basis=Bases(t);Cast(p,t);
        // Hold only the inspection leases through the full multi-boot suite.
        var state=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,state with{Curse=state.Curse with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});
        Require(PaladinConversion.ApplyAllegiance(pal,t,FixtureSpace.RetentionMilliseconds/40),"Saved converted original");Combined(p,t,e,basis,false);
        var curse=NecromancerEffects.Curse(t);var converted=PaladinConversion.Snapshot().Single(s=>s.Target==t.Serial.Value);var point=p.Location;int map=p.Map.MapID;
        t.Internalize();EliteAuras.Refresh(t);e.Internalize();p.Internalize();pal.Internalize();
        SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t)-257);TimedEffects.PreventHealing(t,TimedEffects.Now);NecromancerEffects.SetMana256(p,22784);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,pal.Serial.Value,t.Serial.Value,e.Serial.Value,map,point,basis,curse,converted,SkillHealth.Balance256(t),22784)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;var pal=World.FindMobile((Serial)s.Paladin)as PlayerMobile;
        var t=World.FindMobile((Serial)s.Target)as GraveyardSkeleton;var e=World.FindMobile((Serial)s.Emitter)as EncounterCreature;
        Require(p!=null&&pal!=null&&t is{Alive:true,Deleted:false}&&e is{Alive:true,Deleted:false}&&t.Map==Map.Internal&&e.Map==Map.Internal
            &&NecromancerEffects.Curse(t)==s.Curse&&PaladinConversion.Snapshot().Single(r=>r.Target==s.Target)==s.Converted
            &&Bases(t).SequenceEqual(s.Bases)&&SkillHealth.Balance256(t)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved original curse/allegiance/base pool/wounds/resources changed");
        t.MoveToWorld(new(s.Point.X+3,s.Point.Y,s.Point.Z),Map.Maps[s.Map]);e.MoveToWorld(new(s.Point.X+3,s.Point.Y+1,s.Point.Z),Map.Maps[s.Map]);EliteAuras.Observe(e);Combined(p,t,e,s.Bases,false);
        e.Internalize();EliteAuras.Refresh(t);t.Internalize();
        Require(Bases(t).SequenceEqual(s.Bases)&&NecromancerEffects.Curse(t)==s.Curse&&SkillHealth.Balance256(t)==s.Life,"Reload aura teardown changed saved original");
    }
}
