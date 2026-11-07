// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

public sealed record FireBombNpc(uint Actor,string Binding,ulong Seed,int FirePierce=0,bool Dormant=false,string Fault=null);
// Own NPC spell packet/credit adapter on Fire Blast's existing persistent lob
// pool. Source removal/offline/reload ages harmlessly, matching the existing
// original-NPC projectile host policy; exact D2R removal behavior stays scoped.
internal static class ShadowFireBlasts
{
    internal static void Validate(FireBomb b)
    {
        if(b.Npc is not{} n||!((Serial)n.Actor).IsMobile||n.Actor==b.Owner||n.Binding is not{Length:64}
            ||n.Binding.Any(c=>c is not(>='0'and<='9'or>='a'and<='f'))||n.Seed==0||n.Fault?.Length>512||b.Potion!=0
            ||b.ColdMastery!=0||b.LightningMastery!=0||b.PoisonMastery!=0||b.NextUtc.Kind!=DateTimeKind.Utc)
        {throw new InvalidDataException("Invalid original Shadow Fire Blast source");}
    }
    internal static FireBomb Prepare(long id,AssassinShadowBody body,Point3D aim,int rank,DateTime at)
    {
        if(!ShadowSkillCasting.Attached(body)||body.OwnedSkills.Used!=251||rank!=body.OwnedSkillRank(251)
            ||at.Kind!=DateTimeKind.Utc||at>DateTime.MaxValue.AddMilliseconds(-20000))
        {throw new NotSupportedException("Shadow Fire Blast needs its actual intrinsic NPC release context");}
        if(NecromancerSummons.Source("bomb on ground","HitFlags")!=2||NecromancerSummons.Source("bomb on ground","ResultFlags")!=5
            ||NecromancerSummons.Source(body.SourceMonsterId,"Crit")!=0)
        {throw new NotSupportedException("Shadow Fire Blast needs its checked missile flags and zero intrinsic critical row");}
        if(!LobProjectiles.Valid(body,aim)||!FireBlasts.Supported(ShadowSkillCasting.Values(body,rank))){return null;}
        int dx=2*(aim.X-body.X),dy=2*(aim.Y-body.Y);if(dx==0&&dy==0){dx=dy=1;}
        var vector=MissileVectors.VelocityFp8(dx,dy,LobProjectiles.Velocity);
        var b=new FireBomb(id,body.ControlMaster.Serial.Value,body.Map.MapID,rank,body.X,body.Y,body.Z,vector.X,vector.Y,
            LobProjectiles.Lifetime(dx,dy),false,at.AddMilliseconds(40))
            {Npc=new(body.Serial.Value,OriginalNpcEquipment.Binding(body),NpcWorldAllocation.UnitSeed())};
        Validate(b);return b;
    }
    private static FireBomb Land(AssassinShadowBody body,FireBomb b)
    {
        if(!LobProjectiles.CanLand(body,b.X,b.Y)){return null;}
        var values=ShadowSkillCasting.Values(body,b.Rank);if(!FireBlasts.Supported(values)){return null;}
        var graph=OriginalNpcEquipment.Graph(body);int mastery=graph.Read(329);
        int Enhance(int damage)=>(int)Math.Clamp((long)damage+(long)damage*mastery/100,0,int.MaxValue);
        return b with{X=Math.Floor(b.X*2+.5)/2,Y=Math.Floor(b.Y*2+.5)/2,Vx=0,Vy=0,Ground=true,Remaining=5,
            Min256=Enhance(values.Damage256(false)),Max256=Enhance(values.Damage256(true)),FireMastery=mastery,
            Npc=b.Npc with{FirePierce=graph.Read(333)},NextUtc=b.NextUtc.AddMilliseconds(40)};
    }
    private static void Impact(AssassinShadowBody body,PlayerMobile owner,FireBomb b)
    {
        var targets=new System.Collections.Generic.List<BaseCreature>();
        foreach(var target in body.Map.GetMobilesInRange<BaseCreature>(FireBlasts.Point(b),4))
        {if(FireBlasts.Recipient(owner,target,b)&&!ClassCombat.Friendly(body,target)&&body.CanBeHarmful(target,false)){targets.Add(target);}}
        targets.Sort((a,c)=>a.Serial.CompareTo(c.Serial));
        foreach(var target in targets)
        {
            _=NpcElementSpellHit.Require(body,owner,target,b.NextUtc,"Shadow Fire Blast");
            if(!SourceBlocking.TryRate(target,out int shield,out _)||shield!=0)
            {throw new NotSupportedException("Shadow Fire Blast shield recipient needs its source defense adapter");}
        }
        int raw=SorceressSpellMissiles.Roll(b.Min256,b.Max256,b.Npc.Seed,out _);
        foreach(var target in targets)
        {
            if(!FireBlasts.Recipient(owner,target,b)||ClassCombat.Friendly(body,target)||ClassAvoidance.Missile(target)){continue;}
            NpcElementSpellHit.Deliver(body,owner,target,"fire",raw,0,b.NextUtc,"Shadow Fire Blast",hitFlags:2,pierce:e=>e=="fire"?b.Npc.FirePierce:0);
        }
        Effects.SendLocationEffect(FireBlasts.Point(b),body.Map,0x36BD,15,10,0x489,0); // Existing identified Fire Blast explosion placeholder.
    }
    private static FireBomb Age(FireBomb b)
    {
        if(b.Ground&&b.Remaining<=1){return null;}
        return b with{Remaining=b.Remaining>1?b.Remaining-1:5,Ground=b.Ground||b.Remaining<=1,
            Vx=b.Remaining<=1?0:b.Vx,Vy=b.Remaining<=1?0:b.Vy,NextUtc=b.NextUtc.AddMilliseconds(40),Npc=b.Npc with{Dormant=true}};
    }
    internal static FireBomb Advance(FireBomb b,bool? actions,bool loaded)
    {
        Validate(b);var body=World.FindMobile((Serial)b.Npc.Actor)as AssassinShadowBody;var owner=World.FindMobile((Serial)b.Owner)as PlayerMobile;
        if(loaded||b.Npc.Dormant||!NpcItemSpellFlights.Attached(body,owner,b.Map,b.Npc.Binding)||!(actions??owner?.NetState is{Running:true}))
        {return Age(b);}
        if(b.Ground)
        {if(b.Remaining<=1){Impact(body,owner,b);return null;}return b with{Remaining=b.Remaining-1,NextUtc=b.NextUtc.AddMilliseconds(40)};}
        var moved=LobProjectiles.Advance(body.Map,b.X,b.Y,b.Z,b.Vx,b.Vy,b.Remaining);var next=b with{X=moved.X,Y=moved.Y};
        next=moved.Landed?Land(body,next):next with{Remaining=b.Remaining-1,NextUtc=b.NextUtc.AddMilliseconds(40)};
        if(next!=null){Effects.SendLocationEffect(FireBlasts.Point(next),body.Map,0x36E4,2,10,0x489,0);} // Existing identified bomb/landing placeholder.
        return next;
    }
}
