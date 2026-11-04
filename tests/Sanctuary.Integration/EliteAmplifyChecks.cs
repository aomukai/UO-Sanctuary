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

public static class EliteAmplifyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EliteAmplifyChecks));
    private sealed class ProcRandom:Random{public override int Next(int maximum)=>0;public override double NextDouble()=>0;}
    private sealed record Saved(uint Owner,uint Target,uint Source,uint Orphan,uint LostSource,uint Legacy,NecromancerCurse Known,NecromancerCurse Orphaned,DateTime LegacyUntil,int Life,int OrphanLife,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"elite-amplify-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("elite-amplify-check",Check);Register("elite-amplify-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Fresh NPC Amplify source/rank,shared curse group and original known/legacy reloads",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static NecromancerCurse Metadata(Mobile target)
    {using var doc=JsonDocument.Parse(JsonSerializer.Serialize(NecromancerEffects.Profiles.Find(target)));return doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty("EliteCurse",out var e)?e.Deserialize<NecromancerCurse>():null;}
    private static PlayerMobile Owner()
    {var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureEliteAmplify";MagePetBuffChecks.Train(p,"necromancer.decrepify",1);FixtureSpace.Corridor(p,6980,26,3100);return p;}
    private static PlayerMobile Paladin(PlayerMobile p)
    {var b=new PlayerMobile{Name="PoisonDaggerFixtureEliteAmpPaladin",Body=0x190,Player=true};b.AddItem(new Backpack());ClassCreation.Profiles.Assign(b,DiabloClass.Paladin);ClassCreation.Profiles.AdvanceLevel(b,99);b.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);return b;}
    private static GraveyardSkeleton Target(PlayerMobile p,int dx=3)
    {var t=new GraveyardSkeleton{Name="PoisonDaggerFixtureEliteAmpTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;return t;}
    private static EncounterCreature Source(PlayerMobile p,int dx=3)
    {var profile=EncounterProfiles.Profile("crypt-sentinel-90");var e=new EncounterCreature();e.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Hell,EncounterGrade.Unique,ChampionKind.Champion,[EliteModifier.Cursed],EliteAura.None,null,new Random(173));e.Frozen=e.CantWalk=e.NoKillAwards=true;e.Home=p.Location;e.HomeMap=p.Map;e.MoveToWorld(new(p.X+dx,p.Y+1,p.Z),p.Map);e.AIObject?.AITimer.Stop();return e;}
    private static void Reset(GraveyardSkeleton t)
    {EliteAuras.Curses.Clear(t.Serial);EliteAuras.Refresh(t);NecromancerEffects.Profiles.Clear(t);t.Hits=t.HitsMax;}
    private static void Proc(PlayerMobile pal,EncounterCreature e,GraveyardSkeleton t)
    {
        Require(PaladinConversion.ApplyAllegiance(pal,t,1000),"Qualified converted proc recipient");
        EliteCombat.Hit(e,t,1,ResistanceType.Fire,ranged:true,random:new ProcRandom());
        PaladinConversion.Restore(PaladinConversion.Snapshot().Select(s=>s.Target==t.Serial.Value?s with{UntilUtc=TimedEffects.Now}:s).ToArray());PaladinConversion.Prune();
    }
    private static void Cast(PlayerMobile p,GraveyardSkeleton t,CombatAbility ability)
    {
        PoisonDaggerChecks.Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        Require(NecromancerCombat.Begin(p,ability,t.Serial,t.Location,Core.TickCount),"Learned curse start");long at=ClassCombat.For(p).EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&ClassCombat.ResolveAction(p,at)&&!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==before-cost,"Curse early/replay/payment");
    }
    private static void Cases()
    {
        var p=Owner();var pal=Paladin(p);var t=Target(p);var e=Source(p);Item helm=null;
        try
        {
            int rank=CreatureProfiles.Level(e)/5+1;Require(rank is>1 and<20,"Source rank fixture");
            foreach(int basis in new[]{0,100,110})
            {
                Reset(t);t.SetResistance(ResistanceType.Physical,basis);Proc(pal,e,t);var meta=Metadata(t);
                Cast(p,t,CombatAbility.Decrepify);
                Require(NecromancerEffects.Curse(t)?.Ability==CombatAbility.Decrepify,"Fresh elite Amplify blocked learned Decrepify");
                Require(meta is{Ability:CombatAbility.AmplifyDamage}&&meta.Source==e.Serial.Value&&meta.Rank==rank&&meta.PhysicalDelta==(basis>=100?-20:-100),"Fresh elite source/rank/primary penalty missing");
                Require(!EliteAuras.Curses.Active(t.Serial,TimedEffects.Now)&&Metadata(t)==null&&t.PhysicalResistance==basis
                    &&NecromancerEffects.Curse(t).PhysicalDelta==(basis>=100?-10:-50),"Native offset stacked or changed replacement immunity base");
            }
            Reset(t);t.SetResistance(ResistanceType.Physical,0);MagePetBuffChecks.Train(p,"necromancer.amplifydamage",rank-1);Proc(pal,e,t);var original=Metadata(t);
            Cast(p,t,CombatAbility.AmplifyDamage);Require(Metadata(t)==original&&NecromancerEffects.Curse(t)==null,"Lower AMP replaced stronger NPC AMP");
            helm=PoisonDaggerChecks.Named(p,"Wormskull");Cast(p,t,CombatAbility.AmplifyDamage);var equal=NecromancerEffects.Curse(t);
            Require(equal is{Ability:CombatAbility.AmplifyDamage}&&equal.Rank==rank&&equal.Source==e.Serial.Value&&equal.PhysicalDelta==original.PhysicalDelta
                &&!EliteAuras.Curses.Active(t.Serial,TimedEffects.Now)&&t.PhysicalResistance==0&&NamedElements.Resistance(p,t,"physical",0)==-100,"Equal rank lost original creator/primary or doubled native projection");
            Proc(pal,e,t);var renewed=Metadata(t);Require(renewed.Source==equal.Source&&renewed.PhysicalDelta==equal.PhysicalDelta&&NecromancerEffects.Curse(t)==null,"NPC equal refresh changed retained primary");
            EliteAuras.Amplify(t,TimeSpan.FromHours(2));Require(Metadata(t)==renewed&&EliteAuras.Curses.Expires(t.Serial)==renewed.UntilUtc,"Duration-only helper invented an incoming rank or changed known deadline");
            DateTime at=TimedEffects.Now,until=EliteAuras.Curses.Expires(t.Serial);Require(PaladinCleansing.Apply(t,50,at),"Cleansing did not shorten known elite curse");
            Require(Metadata(t)==renewed with{UntilUtc=PaladinCleansing.Shorten(until,at,50)}&&EliteAuras.Curses.Expires(t.Serial)==Metadata(t).UntilUtc,"Cleansing discarded source/rank or split deadlines");
            MagePetBuffChecks.Train(p,"necromancer.amplifydamage",rank);Cast(p,t,CombatAbility.AmplifyDamage);var higher=NecromancerEffects.Curse(t);
            Require(higher.Source==p.Serial.Value&&higher.Rank==rank+1&&Metadata(t)==null,"Higher real item/hard rank did not replace NPC caster");Proc(pal,e,t);
            Require(NecromancerEffects.Curse(t)==higher&&Metadata(t)==null&&!EliteAuras.Curses.Active(t.Serial,TimedEffects.Now),"Weaker NPC AMP overwrote stronger ordinary AMP");
            Reset(t);Proc(pal,e,t);Require(AttractCasts.Apply(p,t,1)&&Metadata(t)==null&&!EliteAuras.Curses.Active(t.Serial,TimedEffects.Now),"Attract could not replace known AMP");var attract=NecromancerEffects.Curse(t);
            Proc(pal,e,t);Require(NecromancerEffects.Curse(t)==attract&&Metadata(t)==null,"NPC AMP overwrote Attract");
            Reset(t);EliteAuras.Amplify(t,TimeSpan.FromMinutes(5));until=EliteAuras.Curses.Expires(t.Serial);
            Require(Metadata(t)==null&&!NecromancerCurseCasts.Apply(p,t,CombatAbility.Decrepify,1)&&EliteAuras.Curses.Expires(t.Serial)==until,"Unknown legacy metadata was invented or overwritten");
        }
        finally{helm?.Delete();Reset(t);e.Delete();t.Delete();pal.Delete();p.Delete();}
    }
    private static void Check()
    {
        Cases();var p=Owner();var pal=Paladin(p);var t=Target(p);var e=Source(p);Proc(pal,e,t);
        var orphan=Target(p,14);var lost=Source(p,14);Proc(pal,lost,orphan);uint lostSerial=lost.Serial.Value;lost.Delete();
        var legacy=Target(p,23);EliteAuras.Amplify(legacy,FixtureSpace.Retention);
        DateTime until=TimedEffects.Now.Add(FixtureSpace.Retention);EliteAuras.Curses.Apply(t.Serial,until);EliteAuras.Curses.Apply(orphan.Serial,until);
        var known=Metadata(t);var orphaned=Metadata(orphan);Require(known!=null&&orphaned.Source==lostSerial&&Metadata(legacy)==null,"Saved known/orphan/unknown setup");
        foreach(var b in new[]{t,orphan,legacy}){b.Internalize();SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-257);TimedEffects.PreventHealing(b,TimedEffects.Now);}e.Internalize();p.Internalize();pal.Internalize();NecromancerEffects.SetMana256(p,22784);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,e.Serial.Value,orphan.Serial.Value,lostSerial,legacy.Serial.Value,known,orphaned,EliteAuras.Curses.Expires(legacy.Serial),SkillHealth.Balance256(t),SkillHealth.Balance256(orphan),22784)));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(GraveyardSkeleton)World.FindMobile((Serial)s.Target);var orphan=(GraveyardSkeleton)World.FindMobile((Serial)s.Orphan);var legacy=(GraveyardSkeleton)World.FindMobile((Serial)s.Legacy);
        Require(World.FindMobile((Serial)s.Source)is EncounterCreature{Alive:true,Deleted:false}&&World.FindMobile((Serial)s.LostSource)==null&&Metadata(t)==s.Known&&Metadata(orphan)==s.Orphaned
            &&EliteAuras.Curses.Expires(t.Serial)==s.Known.UntilUtc&&EliteAuras.Curses.Expires(orphan.Serial)==s.Orphaned.UntilUtc&&Metadata(legacy)==null&&EliteAuras.Curses.Expires(legacy.Serial)==s.LegacyUntil
            &&NecromancerEffects.Curse(t)==null&&t.PhysicalResistance==-100&&orphan.PhysicalResistance==-100&&legacy.PhysicalResistance==-100
            &&SkillHealth.Balance256(t)==s.Life&&SkillHealth.Balance256(orphan)==s.OrphanLife&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Known/orphan/legacy original curse reload changed payload,projection,wounds or mana");
    }
}
