// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
namespace Sanctuary;

internal static class PlayerBlizzards
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PlayerBlizzards));private static readonly HashSet<string> Warned=new();
    internal static void Gap(string reason){if(Warned.Add(reason)){Log.Warning("Blizzard unsupported context: {Reason}",reason);}}
    internal static bool ValidAim(PlayerMobile owner,Serial selected,Point3D point,out Point3D aim)=>FireWalls.ValidAim(owner,selected,point,out aim);
    internal static void Route()
    {
        var s=SkillMechanics.For(CombatAbility.Blizzard);int M(string key,string field)=>NecromancerSummons.Source(key,field);
        if(s.SourceId!=59||s.Number("srvdofunc")!=28||s.Text("srvmissilea")!="blizzardcenter"||s.Left||!s.Right||s.Number("localdelay")!=45||s.Parameter(1)!=7||s.Parameter(3)!=4
            ||M("blizzardcenter","pSrvDoFunc")!=10||M("blizzardcenter","Range")!=100||M("blizzardcenter","CollideType")!=0||M("blizzardcenter","CanSlow")!=0
            ||M("blizzard1","pSrvDoFunc")!=3||M("blizzard1","Range")!=9||M("blizzard1","CollideType")!=3||M("blizzard1","Size")!=2||M("blizzard1","LastCollide")!=1
            ||M("blizzard1","NextHit")!=0||M("blizzard1","ToHit")!=0||M("blizzard1","CollideKill")!=0||M("blizzard1","pSrvHitFunc")!=0||M("blizzard1","pSrvDmgFunc")!=0
            ||M("blizzard1","GetHit")!=1||M("blizzard1","ReturnFire")!=1||M("blizzard1","ResultFlags")!=4||M("blizzard1","HitFlags")!=0||M("blizzard1","CanSlow")!=0)
        {throw new NotSupportedException("Unsupported pinned Blizzard route");}
    }
    internal static BlizzardStorm Prepare(PlayerMobile owner,Point3D point,SkillValues values,DateTime at)
    {
        Route();if(values.Ability!=CombatAbility.Blizzard||values.Rank is <1 or >255||at.Kind!=DateTimeKind.Utc||!ValidAim(owner,Serial.Zero,point,out _))
        {throw new NotSupportedException("Unsupported prepared Blizzard");}
        return new(0,owner.Serial.Value,values.Rank,owner.Map.MapID,point.X,point.Y,point.Z,0,at.AddMilliseconds(4000),at.AddMilliseconds(40)){Source=true};
    }
    internal static bool Release(PlayerMobile owner,Serial selected,Point3D point,SkillValues values)
    {
        if(!ValidAim(owner,selected,point,out var aim)||!FireGround.Ready(owner,CombatAbility.Blizzard)){return false;}
        var at=TimedEffects.Now;var prepared=Prepare(owner,aim,values,at);if(!NecromancerEffects.Pay(owner,values.Mana256)){return false;}
        Blizzards.Commit(prepared);FireGround.Delay(owner,CombatAbility.Blizzard,at);owner.PlaySound(0x1E5);return true; // Identified cast audio placeholder.
    }
    internal static PreparedMonsterDamagePacket Roll(BlizzardShard shard,ref ulong seed)
    {int raw=SorceressSpellMissiles.Roll(shard.Min256,shard.Max256,seed,out seed);return new(0,raw,0,shard.ColdFrames,5,Cold256:raw);}
    internal static PreparedMonsterDamageResult Deliver(PlayerMobile owner,BaseCreature target,PreparedMonsterDamagePacket packet,FirestormModifiers mods,DateTime at)
    {
        if(!Meteors.Supported(owner,target,out string reason)||packet.Physical256!=0||packet.Total256!=packet.Cold256||packet.ResultFlags!=5||packet.HitFlags!=0
            ||packet.StunFrames!=0||packet.HitClass!=0||packet.HitClassActive||packet.Fire256!=0||packet.Lightning256!=0||packet.Poison256!=0||packet.Magic256!=0)
        {throw new NotSupportedException(reason??"Unsupported Blizzard cold packet");}
        var metadata=NativeMonsterReturn.Metadata(target);NativeMonsterReturn.Session(target);
        var services=new InstantSpellNpcDamage(owner,target,metadata,mods,at,"Blizzard",()=>TelekinesisCombat.DefenderRoll(),cold:true);
        return PreparedMonsterDamage.ReleaseMissile(owner,target,packet,new(NamedCombat.Elite(target),()=>false,()=>metadata.Velocity,()=>false),
            _=>throw new InvalidOperationException("Blizzard has no timed stun"),services,at);
    }
    private static bool Position(uint owner,int map,double x,double y,int z)=>owner!=0&&((Serial)owner).IsMobile&&map>=0&&map<Map.Maps.Length&&Map.Maps[map]!=null&&Map.Maps[map]!=Map.Internal
        &&double.IsFinite(x+y)&&x>0&&y>0&&x<Map.Maps[map].Width&&y<Map.Maps[map].Height&&z is >=-128 and <=127;
    internal static bool Valid(BlizzardStorm s)=>!s.Source||s.Rank is >=1 and <=255&&s.Frame is >=0 and <100&&Position(s.Owner,s.Map,s.X,s.Y,s.Z)
        &&s.NextUtc.Kind==DateTimeKind.Utc&&s.UntilUtc.Kind==DateTimeKind.Utc&&s.UntilUtc-s.NextUtc==TimeSpan.FromMilliseconds((99-s.Frame)*40);
    internal static bool Valid(BlizzardShard s)=>s.Modifiers==null||s.Rank is >=1 and <=255&&s.Modifiers.Potion>=0&&Position(s.Owner,s.Map,s.X,s.Y,s.Z)
        &&s.X*2==Math.Floor(s.X*2)&&s.Y*2==Math.Floor(s.Y*2)&&s.NextUtc.Kind==DateTimeKind.Utc&&s.UntilUtc.Kind==DateTimeKind.Utc
        &&s.UntilUtc-s.NextUtc==TimeSpan.FromMilliseconds((s.Remaining-1)*40);
    internal static PlayerMobile Owner(uint id)=>World.FindMobile((Serial)id) is PlayerMobile{Alive:true,Deleted:false} p
        &&ClassCreation.Profiles.TryGet(p.Serial,out var profile)&&profile.Class==DiabloClass.Sorceress?p:null;
}

public static partial class Blizzards
{
    private static readonly HashSet<long> _loadedStorms=new(),_loadedShards=new();
    internal static void Commit(BlizzardStorm prepared)
    {var s=prepared with{Id=checked(++_nextId)};if(!PlayerBlizzards.Valid(s)){throw new NotSupportedException("Invalid source Blizzard center");}_storms.Add(s.Id,s);}
    internal static void Advance(PlayerMobile owner,DateTime at,bool actions)=>TickSource(at,actions,owner.Serial.Value);
    private static void TickSource(DateTime at,bool? actions,uint? onlyOwner=null)
    {
        var queue=new PriorityQueue<(long Id,bool Storm),(DateTime At,long Id)>();
        foreach(var s in _storms.Values){if(s.Source&&s.NextUtc<=at&&(!onlyOwner.HasValue||s.Owner==onlyOwner)){queue.Enqueue((s.Id,true),(s.NextUtc,s.Id));}}
        foreach(var s in _shards.Values){if(s.Modifiers!=null&&s.NextUtc<=at&&(!onlyOwner.HasValue||s.Owner==onlyOwner)){queue.Enqueue((s.Id,false),(s.NextUtc,s.Id));}}
        while(queue.TryDequeue(out var due,out var priority))
        {
            if(due.Storm)
            {
                if(!_storms.TryGetValue(due.Id,out var s)||s.NextUtc!=priority.At){continue;}
                if(TickStorm(s,at,actions) is{} child&&child.NextUtc<=at){queue.Enqueue((child.Id,false),(child.NextUtc,child.Id));}
                if(_storms.TryGetValue(s.Id,out var next)&&next.NextUtc<=at){queue.Enqueue((next.Id,true),(next.NextUtc,next.Id));}
            }
            else
            {
                if(!_shards.TryGetValue(due.Id,out var s)||s.NextUtc!=priority.At){continue;}TickShard(s,at,actions);
                if(_shards.TryGetValue(s.Id,out var next)&&next.NextUtc<=at){queue.Enqueue((next.Id,false),(next.NextUtc,next.Id));}
            }
        }
    }
    private static BlizzardShard TickStorm(BlizzardStorm s,DateTime at,bool? actions)
    {
        var p=PlayerBlizzards.Owner(s.Owner);bool loaded=_loadedStorms.Remove(s.Id);
        if(p==null||p.Map!=Map.Internal&&p.Map?.MapID!=s.Map){_storms.Remove(s.Id);return null;}
        if(loaded||p.Map==Map.Internal||!(actions??p.NetState is{Running:true}))
        {
            int skip=(int)Math.Min(100-s.Frame,Math.Max(1,(at-s.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1));
            if(s.Frame+skip>=100||s.UntilUtc<=at){_storms.Remove(s.Id);}else{_storms[s.Id]=s with{Frame=s.Frame+skip,NextUtc=s.NextUtc.AddMilliseconds(skip*40L)};}return null;
        }
        BlizzardShard child=null;
        if(s.Frame%4==0)
        {
            var offset=GroundMissileScatter.Sample(s.X*2,s.Frame,7);double x=s.X+offset.X/2.0,y=s.Y+offset.Y/2.0;var point=new Point3D((int)Math.Round(x),(int)Math.Round(y),s.Z);
            if(x>0&&y>0&&x<p.Map.Width&&y<p.Map.Height&&!SpellHelper.IsTown(point,p)&&p.Map.CanFit(point,1,false,false,false))
            {
                var v=SkillMechanics.Values(p,CombatAbility.Blizzard,s.Rank);var mods=DruidFirestorm.Modifiers(p);var ends=DruidFirestorm.Endpoints(v,mods.ColdMastery,SlowMissilePayload.Normal);
                child=new(checked(++_nextId),s.Owner,s.Map,x,y,s.Z,ends.Min,ends.Max,v.DurationFrames,9,0,s.NextUtc.AddMilliseconds(360),s.NextUtc.AddMilliseconds(40))
                    {Rank=s.Rank,Modifiers=mods,Seed=SourceRandom.InitLow(Blazes.SeedLow())};
                if(!PlayerBlizzards.Valid(child)){throw new NotSupportedException("Invalid source Blizzard shard");}_shards.Add(child.Id,child);
                Effects.SendLocationEffect(point,p.Map,0x376A,9,10,0x480,0); // Identified falling ice placeholder.
            }
        }
        // The source do10 emits before the shared lifetime decrement.
        if(s.Frame==99){_storms.Remove(s.Id);}else{_storms[s.Id]=s with{Frame=s.Frame+1,NextUtc=s.NextUtc.AddMilliseconds(40)};}return child;
    }
    private static void TickShard(BlizzardShard s,DateTime at,bool? actions)
    {
        var p=PlayerBlizzards.Owner(s.Owner);bool loaded=_loadedShards.Remove(s.Id);
        if(p==null||p.Map!=Map.Internal&&p.Map?.MapID!=s.Map){_shards.Remove(s.Id);return;}
        if(loaded||p.Map==Map.Internal||!(actions??p.NetState is{Running:true}))
        {
            int skip=(int)Math.Min(s.Remaining,Math.Max(1,(at-s.NextUtc).Ticks/TimeSpan.TicksPerMillisecond/40+1));
            if(skip>=s.Remaining||s.UntilUtc<=at){_shards.Remove(s.Id);}else{_shards[s.Id]=s with{Remaining=s.Remaining-skip,NextUtc=s.NextUtc.AddMilliseconds(skip*40L)};}return;
        }
        int remaining=s.Remaining-1;if(remaining<=0){_shards.Remove(s.Id);return;} // Collision follows decrement; no final-frame hit.
        var point=new Point3D((int)Math.Round(s.X),(int)Math.Round(s.Y),s.Z);
        if(SpellHelper.IsTown(point,p)||!p.Map.CanFit(point,1,false,false,false)){_shards.Remove(s.Id);return;}
        var probe=new FirePatch(s.Id,s.Owner,CombatAbility.Blizzard,s.Map,s.X,s.Y,s.Z,s.Min256,s.Max256,0,s.UntilUtc,s.NextUtc,2);
        var target=ImmolationArrows.Recipient(p,probe,s.LastCollision);var next=s with{Remaining=remaining,NextUtc=s.NextUtc.AddMilliseconds(40)};
        if(target!=null)
        {
            next=next with{LastCollision=target.Serial.Value};SorceressEffects.MissileHit(p,target,"blizzard1");
            if(!Meteors.Supported(p,target,out string reason)){PlayerBlizzards.Gap(reason);}
            else
            {
                ulong seed=s.Seed;var packet=PlayerBlizzards.Roll(s,ref seed);next=next with{Seed=seed};
                if(!ClassAvoidance.Missile(target,"blizzard1")){PlayerBlizzards.Deliver(p,(BaseCreature)target,packet,s.Modifiers,s.NextUtc);}
            }
        }
        if(_shards.ContainsKey(s.Id)){_shards[s.Id]=next;}
    }
}
