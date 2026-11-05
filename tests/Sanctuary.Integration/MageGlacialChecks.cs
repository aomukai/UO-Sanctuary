// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class MageGlacialChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MageGlacialChecks));
    private sealed record Saved(uint Owner,uint Mage,uint First,uint Peer,int Life,int FirstLife,ulong Seed,Point3D Origin,MageMissileState Flight,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"mage-glacial-fixtures.json");
    public static void Configure()
    {
        Register("mage-glacial-check",Check);Register("mage-glacial-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("mage-glacial-live","Natural fixed10 Mage Glacial flight and frozen area",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("mage-glacial-live-result",n=>Live(n,false))));
        ConsoleInputHandler.RegisterCommand("glacial-player-live","Real Sorceress Glacial projectile and shared frozen area",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("glacial-player-live-result",n=>Live(n,true))));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Mage Glacial shared flight/area/freeze/save checks",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(int rank=10)=>MagePetBuffChecks.Owner(rank,6890,3000);
    private static GraveyardZombie Enemy(PlayerMobile p,Mobile caster,int dx,int dy=0)
    {var t=new GraveyardZombie{Name="PoisonDaggerFixtureGlacialTarget",NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(caster.X+dx,caster.Y+dy,caster.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(20000);t.Hits=t.HitsMax;t.SetResistance(ResistanceType.Cold,0);TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static MageMissileState Flight(long id)=>NecromancerMageMissiles.Snapshot().Bolts.SingleOrDefault(x=>x.Id==id);
    private static MageMissileState For(MageWorldBody b)=>NecromancerMageMissiles.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==b.Serial.Value);
    private static void Clear(MageWorldBody b)
    {var s=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(s with{Bolts=s.Bolts.Where(x=>x.Actor!=b.Serial.Value).ToArray()});}
    private static void Replace(MageMissileState f)
    {var s=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(s with{NextId=Math.Max(s.NextId,f.Id),Bolts=s.Bolts.Where(x=>x.Id!=f.Id).Append(f).ToArray()});}
    private static void Step(long id,bool active=true){if(Flight(id) is{} f){NecromancerMageMissiles.Tick(f.NextUtc,active,id);}}
    private static ulong NoCriticals()
    {for(ulong n=1;n<10000;n++){ulong seed=n;if(SourceRandom.Roll(ref seed)%100>=5&&SourceRandom.Roll(ref seed)%100>=5){return n;}}throw new InvalidOperationException("No area critical control");}
    private static void Delivery()
    {
        var p=Owner(9);var mage=MagePetBuffChecks.Pet(p,1);NecroSnapshotChecks.MissingMetadata(mage);var first=Enemy(p,mage,4);var peer=Enemy(p,mage,4,1);var far=Enemy(p,mage,8);var oldSlow=AmazonSlowMissiles.Snapshot();
        try
        {
            var helm=PoisonDaggerChecks.Named(p,"Wormskull");ulong seed=MageStaticFieldChecks.Seed(0,4,false);NecromancerMageMissiles.SetActorSeed(mage,seed);
            Require(SkillMechanics.Rank(p,CombatAbility.RaiseSkeletalMage)==10&&MagePetBuffs.HardRank(mage)==9&&mage.Launch(p,first,TimedEffects.Now)
                &&For(mage).Packet.Spell==CombatAbility.IceBolt&&NecromancerMageMissiles.Snapshot().Bolts.Count(x=>x.Actor==mage.Serial.Value)==3&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Glacial soft9+1 unlocked or drew");Clear(mage);p.Backpack.DropItem(helm);ClassGear.Refresh(p);MagePetBuffChecks.Train(p,"necromancer.skeletalmage",10);
            NecromancerMageMissiles.SetActorSeed(mage,seed);ulong after=seed;SourceRandom.Roll(ref after);int mana=NecromancerEffects.ManaBalance256(p),npcMana=mage.Mana,hp=SkillHealth.Balance256(first);
            Require(mage.Launch(p,first,TimedEffects.Now)&&NecromancerEffects.ManaBalance256(p)==mana&&mage.Mana==npcMana&&NecromancerMageMissiles.ActorSeed(mage).Seed==after,"Glacial proc draw or mana");
            var f=For(mage);var values=MagePetBuffs.Values(CombatAbility.GlacialSpike,10,mage.Level);
            Require(f.Packet is{Spell:CombatAbility.GlacialSpike,Rank:10,ElementFrames:77}&&f.Frames==40&&f.VelocityFp8==3072&&f.Vx==.375
                &&f.Packet.Minimum256==values.Damage256(false)&&f.Packet.Maximum256==values.Damage256(true)&&SkillHealth.Balance256(first)==hp,"Fixed10 Glacial packet/freeze/velocity or early damage");
            seed=NoCriticals();NecromancerMageMissiles.SetActorSeed(mage,seed);for(int i=0;i<40&&Flight(f.Id)!=null;i++){Step(f.Id);}
            int loss=hp-SkillHealth.Balance256(first);Require(loss>0&&hp-SkillHealth.Balance256(peer)==loss&&SkillHealth.Balance256(far)==hp
                &&FreezeStates.Shattered(first)&&FreezeStates.Shattered(peer)&&!FreezeStates.Shattered(far),"Glacial did not share one raw explosion or froze/damaged outside radius");
            ulong critical=seed;SourceRandom.Roll(ref critical);SourceRandom.Roll(ref critical);Require(NecromancerMageMissiles.ActorSeed(mage).Seed==critical,"Glacial recipient copies lost independent critical draws");
            var victim=Enemy(p,mage,12);SkillHealth.SetBalance256(victim,256);
            try{GlacialSpikes.NpcExplosion(mage,p,f,victim.Location);Require(!victim.Alive||victim.Deleted,"Lethal Glacial survived");var corpse=victim.Corpse as Corpse;Require(corpse==null||CorpseSkillRules.Shattered(corpse),"Glacial freeze was committed after lethal damage");corpse?.Delete();}finally{victim.Delete();}
            AmazonSlowMissiles.Restore(oldSlow.Append(new SlowMissilesState(mage.Serial.Value,p.Serial.Value,1,50,50,TimedEffects.Now.AddHours(1))).ToArray());
            long id=NecromancerMageMissiles.LaunchGlacial(mage,first,TimedEffects.Now);var slowed=Flight(id);Require(slowed.Vx==.1875&&slowed.Packet.Minimum256==f.Packet.Minimum256/2&&slowed.Packet.Maximum256==f.Packet.Maximum256/2&&slowed.Packet.ElementFrames==77,"Glacial slow snapshot changed freeze or omitted damage/velocity");Clear(mage);AmazonSlowMissiles.Restore(oldSlow);
            id=NecromancerMageMissiles.LaunchGlacial(mage,first,TimedEffects.Now);hp=SkillHealth.Balance256(first);seed=NecromancerMageMissiles.ActorSeed(mage).Seed;for(int i=0;i<45&&Flight(id)!=null;i++){Step(id,false);}
            Require(Flight(id)==null&&SkillHealth.Balance256(first)==hp&&NecromancerMageMissiles.ActorSeed(mage).Seed==seed,"Inactive Glacial exploded/replayed impact RNG or missed expiry");
            id=NecromancerMageMissiles.LaunchGlacial(mage,first,TimedEffects.Now);f=Flight(id);string before=JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot());bool invalid=false;
            try{Replace(f with{Packet=f.Packet with{Spell=CombatAbility.Meteor}});}catch(InvalidDataException){invalid=true;}Require(invalid&&JsonSerializer.Serialize(NecromancerMageMissiles.Snapshot())==before,"Invalid new spell context cleared existing flights/seeds");Clear(mage);
            // A stationary final-frame packet isolates the active expiry
            // callback from collision; ordinary wall collision uses the same
            // existing do1 terminal callback at the actual flight point.
            victim=Enemy(p,mage,16);int terminalLife=SkillHealth.Balance256(victim);
            try
            {
                id=NecromancerMageMissiles.LaunchGlacial(mage,victim,TimedEffects.Now);var expiry=Flight(id) with{X=victim.X,Y=victim.Y,Vx=0,Vy=0,VelocityFp8=0,Frame=39,Steps=new([new(victim.X*2,victim.Y*2)])};Replace(expiry);Step(id);
                Require(Flight(id)==null&&SkillHealth.Balance256(victim)<terminalLife&&FreezeStates.Shattered(victim),"Active Glacial expiry missed its terminal explosion");
            }
            finally{victim.Delete();}
            Static wall=null;victim=Enemy(p,mage,2,1);terminalLife=SkillHealth.Balance256(victim);
            try
            {
                wall=new Static(0x80);wall.MoveToWorld(new(mage.X+2,mage.Y,mage.Z),p.Map);
                id=NecromancerMageMissiles.LaunchGlacial(mage,first,TimedEffects.Now);int wallFrame=0;for(int i=0;i<40&&Flight(id)!=null;i++){wallFrame=Flight(id).Frame+1;Step(id);}
                // The native area LOS begins inside the wall here and rejects
                // this recipient. Do not turn a wall impact into through-wall
                // damage to make an area assertion pass. Exact source room/LOS
                // mapping remains separate from the terminal callback.
                Require(Flight(id)==null&&wallFrame<10&&SkillHealth.Balance256(victim)==terminalLife&&!FreezeStates.Shattered(victim),$"Glacial wall removal/occlusion frame={wallFrame},life={SkillHealth.Balance256(victim)}/{terminalLife}");
            }
            finally{wall?.Delete();victim.Delete();}
            // The player consumer uses the same area preparation and commits
            // freeze before death while retaining normal player enhancement.
            var sorc=ClassSkillChecks.Player(DiabloClass.Sorceress);sorc.MoveToWorld(new(mage.X+12,mage.Y,mage.Z),p.Map);victim=Enemy(p,sorc,1);SkillHealth.SetBalance256(victim,256);
            try{GlacialSpikes.PlayerExplosion(sorc,victim.Location,SkillMechanics.Values(sorc,CombatAbility.GlacialSpike));Require(!victim.Alive||victim.Deleted,"Player Glacial lethal receiver");var corpse=victim.Corpse as Corpse;Require(corpse==null||CorpseSkillRules.Shattered(corpse),"Player Glacial freeze followed death");corpse?.Delete();}finally{victim.Delete();sorc.Delete();}
        }
        finally{AmazonSlowMissiles.Restore(oldSlow);Clear(mage);first.Delete();peer.Delete();far.Delete();mage.Delete();p.Delete();}
    }
    private static void Check()
    {
        Delivery();var p=Owner();var mage=MagePetBuffChecks.Pet(p,1);var first=Enemy(p,mage,4);var peer=Enemy(p,mage,4,1);
        long id=NecromancerMageMissiles.LaunchGlacial(mage,first,TimedEffects.Now);for(int i=0;i<3;i++){Step(id);}var f=Flight(id);Require(f is{Frame:3}&&SkillHealth.Balance256(first)==20000*256,"Saved Glacial pre-hit phase");
        f=f with{NextUtc=TimedEffects.Now.AddHours(1)};Replace(f);SkillHealth.SetBalance256(mage,SkillHealth.Maximum256(mage)-257);TimedEffects.PreventHealing(mage,TimedEffects.Now);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,mage.Serial.Value,first.Serial.Value,peer.Serial.Value,SkillHealth.Balance256(mage),SkillHealth.Balance256(first),NecromancerMageMissiles.ActorSeed(mage).Seed,mage.Location,f,0)));
        Log.Information("Mage Glacial:hard9+real soft1 refusal/hard10 fixed10,one proc/no mana,shared saved do1 flight/one raw area roll/recipient criticals,freeze-before-lethal NPC/player,slow snapshot,active expiry/wall removal and occlusion,offline no explosion,atomic restore and held packet transport pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var mage=(MageWorldBody)World.FindMobile((Serial)s.Mage);var first=(GraveyardZombie)World.FindMobile((Serial)s.First);var peer=(GraveyardZombie)World.FindMobile((Serial)s.Peer);
        Require(mage.ControlMaster==p&&SkillHealth.Balance256(mage)==s.Life&&SkillHealth.Balance256(first)==s.FirstLife&&NecromancerMageMissiles.ActorSeed(mage).Seed==s.Seed
            &&JsonSerializer.Serialize(Flight(s.Flight.Id))==JsonSerializer.Serialize(s.Flight),$"Saved Glacial owner={mage.ControlMaster?.Serial}/{s.Owner},life={SkillHealth.Balance256(mage)}/{s.Life},target={SkillHealth.Balance256(first)}/{s.FirstLife},seed={NecromancerMageMissiles.ActorSeed(mage).Seed}/{s.Seed},flight={JsonSerializer.Serialize(Flight(s.Flight.Id))}/{JsonSerializer.Serialize(s.Flight)}");
        p.MoveToWorld(new(s.Origin.X-3,s.Origin.Y,s.Origin.Z),Map.Maps[s.Flight.Map]);mage.MoveToWorld(s.Origin,p.Map);
        var save=NecromancerMageMissiles.Snapshot();var copy=s.Flight with{Id=save.NextId+1,NextUtc=TimedEffects.Now};Replace(copy);for(int i=0;i<40&&Flight(copy.Id)!=null;i++){Step(copy.Id);}
        Require(SkillHealth.Balance256(first)<s.FirstLife&&FreezeStates.Shattered(first)&&FreezeStates.Shattered(peer)&&Flight(copy.Id)==null
            &&JsonSerializer.Serialize(Flight(s.Flight.Id))==JsonSerializer.Serialize(s.Flight),"Copied saved Glacial did not resume frozen area collision independently");
        TimedEffects.Clear(first);TimedEffects.PreventHealing(first,TimedEffects.Now);TimedEffects.Clear(peer);TimedEffects.PreventHealing(peer,TimedEffects.Now);
        SkillHealth.SetBalance256(first,s.FirstLife);SkillHealth.SetBalance256(peer,s.FirstLife);NecromancerMageMissiles.SetActorSeed(mage,s.Seed);mage.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Live(NetState network,bool player)
    {
        var p=player?ClassSkillChecks.Player(DiabloClass.Sorceress):Owner();if(player){FixtureSpace.Corridor(p,7100,40,3500);}FixtureNetwork.Authenticate(network,p,"glacial");
        MageWorldBody mage=player?null:MagePetBuffChecks.Pet(p,1,future:false);Mobile caster=player?p:mage;var first=Enemy(p,caster,4);var peer=Enemy(p,caster,4,1);int mana=NecromancerEffects.ManaBalance256(p);bool launched=player;long deadline=Core.TickCount+7000;
        string label=player?"glacial-player-live":"mage-glacial-live";
        if(player){Require(SorceressCombat.Release(p,CombatAbility.GlacialSpike,first.Serial,first.Location)&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.GlacialSpike).Mana256&&SkillHealth.Balance256(first)==20000*256,"Player Glacial release/payment/early hit");}
        else{NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4));}
        void End(){if(mage!=null){Clear(mage);mage.Delete();}first.Delete();peer.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline&&(mage==null||mage.WorldState.Controller.Fault==null),"Natural Glacial timeout/fault");
                if(!player&&!launched&&For(mage) is{} f){Require(mage.WorldState.Controller.Frame>=35&&f.Packet is{Spell:CombatAbility.GlacialSpike,Rank:10}&&SkillHealth.Balance256(first)==20000*256,"Natural Glacial source/packet/early hit");launched=true;mage.SuspendActions();}
                if(SkillHealth.Balance256(peer)==20000*256){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);return;}
                Require(launched&&SkillHealth.Balance256(first)<20000*256&&FreezeStates.Shattered(first)&&FreezeStates.Shattered(peer)&&network.Running
                    &&(player||NecromancerEffects.ManaBalance256(p)==mana),"Natural Glacial area/freeze/mana");End();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){End();Log.Error(e,"{Label}-result: failed",label);}
        }
        Log.Information("{Label}: ready",label);Timer.StartTimer(TimeSpan.FromMilliseconds(20),Wait);
    }
}
