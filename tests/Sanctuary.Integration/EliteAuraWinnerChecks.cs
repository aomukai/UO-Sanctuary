// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EliteAuraWinnerChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EliteAuraWinnerChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("elite-aura-winner-check","Aura winner shares recipient and floor admission",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("elite-aura-winner-check: passed");}catch(Exception e){Log.Error(e,"elite-aura-winner-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static EncounterCreature Emitter(PlayerMobile p,EliteAura aura,int dz,bool stronger=false)
    {
        var profile=EncounterProfiles.Profile("crypt-sentinel-90");var b=new EncounterCreature();
        b.Assign(profile,stronger?profile.MaximumLevel:profile.MinimumLevel,MonsterDifficulty.Hell,EncounterGrade.Unique,ChampionKind.Champion,[EliteModifier.AuraEnchanted],aura,null,new Random(173));
        b.Frozen=b.CantWalk=b.NoKillAwards=true;b.Home=p.Location;b.HomeMap=p.Map;
        b.MoveToWorld(new(p.X+3,p.Y+1,p.Z+dz),p.Map);b.AIObject?.AITimer.Stop();EliteAuras.Observe(b);return b;
    }
    private static void Check()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureAuraWinner";FixtureSpace.Corridor(p,7100,24,3400);
        var pal=new PlayerMobile{Name="PoisonDaggerFixtureAuraWinnerPaladin",Body=0x190,Player=true};pal.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(pal,DiabloClass.Paladin);ClassCreation.Profiles.AdvanceLevel(pal,99);pal.MoveToWorld(p.Location,p.Map);
        var conversions=PaladinConversion.Snapshot();int ordinal=0;
        try
        {
            foreach(var aura in new[]{EliteAura.HolyFire,EliteAura.HolyFreeze,EliteAura.HolyShock})
            {
                var t=new MageMissileWitness{Frozen=true,CantWalk=true,NoKillAwards=true};
                t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();t.SetHits(50000);t.Hits=t.HitsMax;
                // Lower GUID wins equal-level ties. This emitter is visible
                // through open air, but on the excluded floor boundary.
                var high=Emitter(p,aura,16);var low=Emitter(p,aura,0);EncounterCreature strongest=null;
                void Pulse(Mobile expected,string why)
                {
                    int calls=0;Mobile from=null;t.BeforeDamage=(_,source)=>{calls++;from=source;};int before=SkillHealth.Balance256(t);
                    EliteAuras.Tick(Core.TickCount+100000+(ordinal++)*10000);
                    Require(expected==null?calls==0&&SkillHealth.Balance256(t)==before:calls==1&&from==expected&&SkillHealth.Balance256(t)<before,why);
                    t.BeforeDamage=null;
                }
                try
                {
                    Require(PaladinConversion.ApplyAllegiance(pal,t,1000),"Converted recipient admission");
                    Require(!t.Controlled&&!t.Summoned&&high.Serial.Value<low.Serial.Value&&high.InLOS(t)
                        &&high.CanSee(t)&&high.InRange(t,EliteAuras.Radius(aura,EliteAuras.SkillLevel(high))),"Floor/tie preconditions");
                    Require(EliteAuras.Level(t,aura,true)==EliteAuras.SkillLevel(low)&&EliteCombat.Enemy(low,t,18),"Lower-floor hostile emitter missing");
                    Pulse(low,"Excluded-floor emitter suppressed eligible "+aura+" damage pulse");
                    high.MoveToWorld(new(high.X,high.Y,t.Z),high.Map);
                    Pulse(high,"Equal-level lower-GUID winner missing or duplicate "+aura+" pulse");
                    strongest=Emitter(p,aura,0,true);Require(EliteAuras.SkillLevel(strongest)>EliteAuras.SkillLevel(low),"Stronger rank precondition");
                    Pulse(strongest,"Stronger eligible "+aura+" lost or stacked pulses");strongest.Internalize();
                    Pulse(high,"Internalized stronger "+aura+" suppressed surviving winner");
                    // Putting both emitters outside the floor boundary must
                    // leave the target undamaged, despite the forced pulse.
                    low.MoveToWorld(new(low.X,low.Y,t.Z+16),low.Map);high.MoveToWorld(new(high.X,high.Y,t.Z+16),high.Map);
                    Require(EliteAuras.Level(t,aura,true)==0,"Excluded-floor aura exposed a rank");Pulse(null,"Excluded-floor aura delivered damage");
                }
                finally{strongest?.Delete();high.Delete();low.Delete();t.Delete();}
            }
        }
        finally{PaladinConversion.Restore(conversions);pal.Delete();p.Delete();}
    }
}
