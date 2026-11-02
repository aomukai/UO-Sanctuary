// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class Meteors
{
    internal static bool ValidAim(PlayerMobile owner,Serial selected,Point3D point,out Point3D aim)
        =>FireWalls.ValidAim(owner,selected,point,out aim); // Explicit existing native world/grid adapter.
    internal static void Route()
    {
        var s=SkillMechanics.For(CombatAbility.Meteor);int M(string key,string field)=>NecromancerSummons.Source(key,field);
        if(s.SourceId!=56||s.Number("srvdofunc")!=28||s.Text("srvmissilea")!="meteorcenter"||s.Left||!s.Right||s.Number("localdelay")!=30
            ||M("meteorcenter","pSrvDoFunc")!=1||M("meteorcenter","pSrvHitFunc")!=14||M("meteorcenter","Range")!=60||M("meteorcenter","CollideType")!=0
            ||M("meteorcenter","Vel")!=0||M("meteorcenter","ResultFlags")!=5||M("meteorcenter","HitFlags")!=2||M("meteorcenter","sHitPar1")!=0||M("meteorcenter","sHitPar2")!=1
            ||M("meteorcenter","AlwaysExplode")!=1||M("meteorfire","pSrvDoFunc")!=5||M("meteorfire","pSrvDmgFunc")!=3||M("meteorfire","dParam1")!=19
            ||M("meteorfire","Size")!=2||M("meteorfire","DamageRate")!=41||M("meteorfire","GetHit")!=0||M("meteorfire","CanSlow")!=0)
        {throw new NotSupportedException("Unsupported pinned Meteor route");}
    }
    internal static MeteorDrop Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        Route();if(values.Ability!=CombatAbility.Meteor||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc||!ValidAim(owner,Serial.Zero,point,out _))
        {throw new NotSupportedException("Unsupported prepared Meteor");}
        var mods=DruidFirestorm.Modifiers(owner);var ends=DruidFirestorm.Endpoints(values,mods.FireMastery,SlowMissilePayload.Normal);
        return new(0,owner.Serial.Value,values.Rank,owner.Map.MapID,point.X,point.Y,point.Z,ends.Min,ends.Max,SkillGeometry.Tiles(values.Eval("aurarangecalc")),at.AddMilliseconds(2400))
            {MeteorModifiers=mods};
    }
    internal static bool Release(PlayerMobile owner,Serial selected,Point3D point,SkillValues values)
    {
        if(!ValidAim(owner,selected,point,out var aim)||!FireGround.Ready(owner,CombatAbility.Meteor)){return false;}
        var at=TimedEffects.Now;var prepared=Prepare(owner,aim,values,at);
        if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        FireGround.CommitMeteor(owner,prepared);FireGround.Delay(owner,CombatAbility.Meteor,at);owner.PlaySound(0x1E5);return true; // Identified cast sound placeholder.
    }
    internal static PreparedMonsterDamagePacket Roll(MeteorDrop meteor,ref ulong seed)
    {
        int raw=SorceressSpellMissiles.Roll(meteor.Min256,meteor.Max256,seed,out seed);
        // hit14 fills one zeroed packet, then ORs row flags. Its area path
        // does not run the direct-collision hit-class/return-fire callbacks.
        return new(0,raw,0,0,5,Fire256:raw,HitFlags:2);
    }
    internal static bool Supported(PlayerMobile owner,Mobile target,out string reason)
    {
        if(!SourceFireFields.Supported(owner,target,out reason)){return false;}
        try
        {
            var metadata=NativeMonsterReturn.Metadata((BaseCreature)target);
            if(!metadata.Killable){reason="Meteor recipient is not source-killable";return false;}
            if(metadata.Base==78){reason="Meteor sand-leaper get-hit/knockback completion needs its actor adapter";return false;}return true;
        }
        catch(NotSupportedException e){reason=e.Message;return false;}
    }
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at)
    {
        if(!Supported(owner,target,out string reason)||packet.Physical256!=0||packet.Total256!=packet.Fire256||packet.HitFlags!=2||packet.ResultFlags!=5
            ||packet.StunFrames!=0||packet.ColdFrames!=0||packet.HitClass!=0||packet.HitClassActive||packet.Magic256!=0||packet.Cold256!=0||packet.Lightning256!=0||packet.Poison256!=0)
        {throw new NotSupportedException(reason??"Unsupported Meteor area packet");}
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,mods,at,"Meteor",()=>TelekinesisCombat.DefenderRoll());
        return PreparedMonsterDamage.ReleaseMissile(owner,target,packet,new(NamedCombat.Elite(target),()=>false,()=>metadata.Velocity,()=>false),
            _=>throw new InvalidOperationException("Meteor has no timed stun"),services,at);
    }
    internal static bool Valid(MeteorDrop m)=>m.MeteorModifiers==null||m.NpcCaster==0&&m.Rank is >=1 and <=255&&m.Radius==3&&m.MeteorModifiers.Potion>=0
        &&m.ImpactUtc.Kind==DateTimeKind.Utc&&m.Owner!=0&&((Serial)m.Owner).IsMobile&&m.Map>=0&&m.Map<Map.Maps.Length&&Map.Maps[m.Map]!=null&&Map.Maps[m.Map]!=Map.Internal
        &&m.X>0&&m.Y>0&&m.X<Map.Maps[m.Map].Width&&m.Y<Map.Maps[m.Map].Height;
    internal static bool Valid(FirePatch p)=>p.MeteorModifiers==null||p.BlazeModifiers==null&&p.FireWallModifiers==null&&p.Ability==CombatAbility.Meteor&&p.NpcCaster==0
        &&p.SkillRank is >=1 and <=255&&p.Size==2&&p.DamageRate==41&&p.MeteorModifiers.Potion>=0&&p.X*2==Math.Floor(p.X*2)&&p.Y*2==Math.Floor(p.Y*2)
        &&p.NextUtc.Kind==DateTimeKind.Utc&&p.UntilUtc.Kind==DateTimeKind.Utc;
}

public static partial class FireGround
{
    private static readonly HashSet<long> _missedMeteors=new(),_loadedMeteorFields=new();
    internal static void CommitMeteor(PlayerMobile owner,MeteorDrop prepared)
    {
        var m=prepared with{Id=checked(++_nextId),Seed=SourceRandom.InitLow(Blazes.SeedLow())};if(!Meteors.Valid(m)){throw new NotSupportedException("Invalid Meteor placement");}_meteors.Add(m.Id,m);
        Effects.SendLocationEffect(new Point3D(m.X,m.Y,m.Z),owner.Map,0x3709,60,10,0x489,0); // Identified warning placeholder.
    }
    internal static void AdvanceMeteor(PlayerMobile owner,DateTime at,bool actions)
    {
        while(true)
        {
            var m=_meteors.Values.Where(x=>x.Owner==owner.Serial.Value&&x.MeteorModifiers!=null&&x.ImpactUtc<=at).OrderBy(x=>x.ImpactUtc).ThenBy(x=>x.Id).FirstOrDefault();
            var p=_patches.Values.Where(x=>x.Owner==owner.Serial.Value&&x.MeteorModifiers!=null&&x.NextUtc<=at).OrderBy(x=>x.NextUtc).ThenBy(x=>x.Id).FirstOrDefault();
            if(m==null&&p==null){return;}
            if(m!=null&&(p==null||m.ImpactUtc<p.NextUtc||m.ImpactUtc==p.NextUtc&&m.Id<p.Id)){TickPreparedMeteor(m,actions);}else{TickBlaze(p,at,actions);}
        }
    }
    private static FirePatch[] TickPreparedMeteor(MeteorDrop m,bool? actions)
    {
        _meteors.Remove(m.Id);bool missed=_missedMeteors.Remove(m.Id);var owner=Owner(m.Owner);
        if(missed||owner==null||owner.Map==Map.Internal||owner.Map?.MapID!=m.Map||!(actions??owner.NetState is{Running:true})){return [];}
        var center=new Point3D(m.X,m.Y,m.Z);if(SpellHelper.IsTown(center,owner)){return [];}
        ulong seed=m.Seed;var packet=Meteors.Roll(m,ref seed);
        SkillCombat.CopiedArea(owner,center,m.Radius,target=>
        {
            if(!Meteors.Supported(owner,target,out string reason)){SourceFireFields.Gap(reason);return;}
            Meteors.Deliver(owner,(BaseCreature)target,packet,m.MeteorModifiers,m.ImpactUtc);
        });
        var children=new List<FirePatch>();var values=SkillMechanics.Values(owner,CombatAbility.Meteor,m.Rank);var mods=DruidFirestorm.Modifiers(owner);
        var ends=DruidFirestorm.Endpoints(MeteorFireDamage256(values,false),MeteorFireDamage256(values,true),mods.FireMastery,SlowMissilePayload.Normal);
        int frames=30+15*(m.Rank-1);
        foreach(var offset in MeteorOffsets())
        {
            double x=m.X+offset.X/2.0,y=m.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),m.Z);
            // Per-child native allocation/terrain adapter; one failed child
            // does not suppress the remaining source producer requests.
            if(x<=0||y<=0||x>=owner.Map.Width||y>=owner.Map.Height||SpellHelper.IsTown(point,owner)||!owner.Map.CanFit(point,1,false,false,false)){continue;}
            var p=new FirePatch(checked(++_nextId),m.Owner,CombatAbility.Meteor,m.Map,x,y,m.Z,ends.Min,ends.Max,41,m.ImpactUtc.AddMilliseconds(frames*40L),m.ImpactUtc.AddMilliseconds(40),2)
                {SkillRank=m.Rank,MeteorModifiers=mods,Seed=SourceRandom.InitLow(Blazes.SeedLow())};
            if(!Meteors.Valid(p)){throw new NotSupportedException("Invalid Meteor field");}_patches.Add(p.Id,p);children.Add(p);
        }
        Effects.SendLocationEffect(center,owner.Map,0x36BD,20,10,0x489,0);return children.ToArray(); // Identified impact placeholder.
    }
}
