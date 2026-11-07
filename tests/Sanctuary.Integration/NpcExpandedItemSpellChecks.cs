// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

internal static class NpcExpandedItemSpellChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    internal static void Packets()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-expanded-spells-source-cases.json")));int count=0;
        foreach(var r in source.RootElement.GetProperty("Cases").EnumerateArray())
        {
            var p=NpcItemSpellDamage.Calculate(r.GetProperty("Skill").GetInt32(),r.GetProperty("Rank").GetInt32(),99,r.GetProperty("Mastery").GetInt32());
            Require(p.Minimum256==r.GetProperty("Minimum256").GetInt32()&&p.Maximum256==r.GetProperty("Maximum256").GetInt32()&&p.ColdFrames==r.GetProperty("ColdFrames").GetInt32()&&p.Emissions==r.GetProperty("Emissions").GetInt32(),"Expanded NPC spell differs from complete creator/getter/writer oracle");count++;
        }
        Require(count==3566,"Expanded spell numeric matrix missing");
    }
    private static ValkyrieContactChecks.Recipient Target(AmazonValkyrieBody b,int distance)
    {
        var t=new ValkyrieContactChecks.Recipient{Frozen=false};t.SetHits(65000);t.MoveToWorld(new(b.X+distance,b.Y,b.Z),b.Map);t.AIObject.AITimer.Stop();
        foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    internal static void Flights(AmazonValkyrieBody b)
    {
        var owner=(PlayerMobile)b.ControlMaster;
        foreach(int skill in new[]{39,49})
        {
            var near=Target(b,4);var far=skill==49?Target(b,8):null;
            try
            {
                var at=TimedEffects.Now.AddHours(3);int life=SkillHealth.Balance256(near),farLife=far==null?0:SkillHealth.Balance256(far),mana=NecromancerEffects.ManaBalance256(owner);string before=b.SavedText;
                var old=SorceressSpellMissiles.Snapshot().Flights.Select(f=>f.Id).ToArray();ulong seed=NpcWorldAllocation.RequireStream().Value,expected=seed;SourceRandom.Roll(ref expected);
                Require(NpcItemSpellCasts.CastOriginal(b,NpcItemTarget.Capture(near),skill,3,0,at,r=>NpcOriginalItemProcs.Allocator(b,r,at)),"Expanded direct item spell failed");
                var flight=SorceressSpellMissiles.Snapshot().Flights.Single(f=>!old.Contains(f.Id));
                Require(NpcWorldAllocation.RequireStream().Value==expected&&flight.Npc?.Packet.Skill==skill&&flight.X==b.X&&flight.Y==b.Y&&SkillHealth.Balance256(near)==life,"Expanded incoming item route lost origin/allocation or hit before flight");
                for(int i=0;i<80&&SorceressSpellMissiles.Snapshot().Flights.Any(f=>f.Id==flight.Id);i++)
                {SorceressSpellMissiles.Tick(at.AddMilliseconds(40L*(i+1)),true,flight.Id);}
                Require(SkillHealth.Balance256(near)<life&&near.Notifications>0,"Expanded item projectile did not naturally damage its recipient");
                if(skill==39){Require(TimedEffects.TryActor(near,out var state)&&state.Cold?.Source==b.Serial.Value,"Original Ice Bolt lost cold application/source");}
                if(far!=null){Require(SkillHealth.Balance256(far)<farLife,"Original Lightning failed its continuing line hit");}
                Require(b.SavedText==before&&NecromancerEffects.ManaBalance256(owner)==mana,"Expanded projectile borrowed owner mana or rewrote caster/controller");
                SorceressSpellMissiles.Cancel(b);old=SorceressSpellMissiles.Snapshot().Flights.Select(f=>f.Id).ToArray();
                Require(NpcItemSpellCasts.CastOriginal(b,NpcItemTarget.Capture(near),skill,3,1,at,r=>NpcOriginalItemProcs.Allocator(b,r,at)),"Expanded outgoing direct route failed");
                flight=SorceressSpellMissiles.Snapshot().Flights.Single(f=>!old.Contains(f.Id));Require(flight.X==near.X&&flight.Y==near.Y,"Expanded outgoing missile lost source direct-effect origin");
            }
            finally{SorceressSpellMissiles.Cancel(b);near.Delete();far?.Delete();}
        }
        var target=Target(b,1);
        try
        {
            string state=b.SavedText;int life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner);var rng=NpcEquipmentRandom.Require(b);var world=NpcWorldAllocation.Capture();
            Require(NpcItemSpellCasts.CastOriginal(b,NpcItemTarget.Capture(target),66,1,1,TimedEffects.Now,_=>throw new InvalidOperationException("Curse allocated a projectile")),"Original Amplify Damage frame failed");
            Require(NecromancerEffects.Resistance(target,"physical",0)==-100&&SkillHealth.Balance256(target)==life&&b.SavedText==state&&NpcEquipmentRandom.Require(b)==rng&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(owner)==mana,"Original Amplify Damage lost do30/no-payment/no-allocation/protected controller context");
        }
        finally{target.Delete();}
    }
}
