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

public static class GoatmanNpcContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNpcContactChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string State,int Life,NpcEquipmentSeed Random);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-npc-contact-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:GraveyardZombie
    {
        internal int Notifications;
        [Constructible]public Recipient(){}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnThink(){}
        public override void OnDamage(int amount,Mobile from,bool willKill){Notifications++;base.OnDamage(amount,from,willKill);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-npc-contact-reload-check":"goatman-npc-contact-check";
            ConsoleInputHandler.RegisterCommand(name,"Connected owned Goatman ordinary NPC damage/event binding",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static ulong SeedFor(bool crushing,int chance)
    {
        for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),next=seed;if((SourceRandom.Limited(ref next,100)<chance)==crushing){return seed;}}
        throw new InvalidOperationException("No declared Crushing Blow control seed");
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=new PlayerMobile{Name="Goatman ordered contact fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",5);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);ClassSkillChecks.Train(p,"warlock.bloodoath",5);
        FixtureSpace.Corridor(p,6800,24,3740);PoisonDaggerChecks.Named(p,"Tarnhelm");
        var account=FixtureNetwork.Authenticate(network,p,"goatman-contact-");WarlockGoatmanBody b=null;Recipient t=null;bool kept=false;
        var stuns=CombatStuns.Snapshot();
        try
        {
            var at=TimedEffects.Now;b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);
            t=new Recipient{CantWalk=true,Frozen=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            var birth=b.Prepared.Snapshot;var physical=((ISourceUnarmedNpc)b).UnarmedPhysical;
            Require(physical.Minimum*256+SourcePercentage.Calculate(physical.Minimum*256,physical.DamagePercent,100)==birth.Birth.MinimumDamage256
                &&physical.Maximum*256+SourcePercentage.Calculate(physical.Maximum*256,physical.DamagePercent,100)==birth.Birth.MaximumDamage256,"Unarmed adapter enhanced captured display endpoints twice");
            Require(NpcEquipmentCritical.Capture(b)==new NpcCriticalChances(0,0,0,5)&&NpcCrushing.Chance(b)==birth.Birth.Suppliers.CrushingChance
                &&NpcCombatEvents.ForSource(b).Subscriptions().Contains(136),"Own intrinsic Crit/captured Crushing registration missing");
            // Change the owner's live skills and add real item CB/deadly.
            p.Backpack.DropItem(p.FindItemOnLayer(Layer.Helm));
            Require(ClassCreation.Profiles.AdvanceLevel(p,60),"Exceptional helmet level floor");
            while(p.RawStr<120){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Owner fixture helmet Strength");}
            PoisonDaggerChecks.Named(p,"Guillaume's Face");ClassGear.Refresh(p);
            ClassSkillChecks.Train(p,"warlock.deathmark");
            Require(ClassGear.Bonus(p,"crush")>0&&NpcEquipmentCritical.Capture(b).Deadly==0&&NpcCrushing.Chance(b)==birth.Birth.Suppliers.CrushingChance
                &&((ISourceUnarmedNpc)b).UnarmedPhysical==physical&&b.Prepared.Snapshot==birth,"Owner item/skill changes leaked into the captured demon");
            NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));var noMissileSeed=NpcEquipmentRandom.State(b);int noMissileLife=SkillHealth.Balance256(t);
            Require(NpcCrushing.Apply(b,t,true)==0&&NpcEquipmentRandom.State(b)==noMissileSeed&&SkillHealth.Balance256(t)==noMissileLife,"Melee-only Goatman aura gained a missile proc");
            foreach(int skill in new[]{416,417,418,419})
            {
                CombatStuns.Restore(stuns);SkillHealth.SetBalance256(t,20000*256);t.Notifications=0;
                int before=SkillHealth.Balance256(t),cb=NamedCombat.CrushingLoss256(t,false);int rank=GoatmanNpcContact.Rank(b,skill);
                var prepared=GoatmanNpcContact.Prepare(b,p,t,skill,at,_=>0,()=>99);var packet=prepared.Impact.Packet;
                Require((packet.ResultFlags&1)!=0&&packet.Total256>0&&t.Notifications==0&&SkillHealth.Balance256(t)==before,"Allocation committed HP/events or missed deterministic control");
                if(skill==418){Require(packet.Physical256==0&&packet.Magic256>0&&GoatmanBerserkEffects.At(b,at)is{Flags:2},"Berserk conversion/timed startup missing");}
                NpcEquipmentRandom.Set(b,SeedFor(true,NpcCrushing.Chance(b)));ulong expected=NpcEquipmentRandom.State(b).Seed;_=SourceRandom.Limited(ref expected,100);
                var released=GoatmanNpcContact.Release(b,p,t,skill,prepared,at);
                Require(released.Commit!=null&&prepared.Consumed&&SkillHealth.Balance256(t)==before-cb-packet.Total256&&t.Notifications==1
                    &&NpcEquipmentRandom.State(b).Seed==expected,"Ordered CB/ordinary HP/native notification/RNG did not commit once");
                Require(skill!=416||CombatStuns.Remaining(t)>0,"Actual NPC Stun did not reach the surviving recipient");
                bool duplicate=false;try{GoatmanNpcContact.Release(b,p,t,skill,prepared,at);}catch(InvalidOperationException){duplicate=true;}Require(duplicate,"Ordinary contact replayed");
            }
            CombatStuns.Restore(stuns);SkillHealth.SetBalance256(t,20000*256);
            var missed=GoatmanNpcContact.Prepare(b,p,t,417,at,n=>n>0?n-1:0,()=>99);int missedLife=SkillHealth.Balance256(t);
            Require((missed.Impact.Packet.ResultFlags&1)==0,"Deterministic miss control hit");GoatmanNpcContact.Release(b,p,t,417,missed,at);
            Require(SkillHealth.Balance256(t)==missedLife,"Miss dealt CB or ordinary damage");
            var stale=GoatmanNpcContact.Prepare(b,p,t,417,at,_=>0,()=>99);var targetPoint=t.Location;t.Location=new(t.X+10,t.Y,t.Z);
            var rangedOut=GoatmanNpcContact.Release(b,p,t,417,stale,at);Require(rangedOut.Exit==MonsterContactExit.OutOfRangeFreed&&SkillHealth.Balance256(t)==missedLife,"Release-range recheck failed");t.Location=targetPoint;
            // Rank1/default attack uses the same unarmed allocator/event path.
            for(int low=0;low<10000;low++){ulong seed=SourceRandom.InitLow(low),draw=seed;if(SourceRandom.Limited(ref draw,100)==0){NpcEquipmentRandom.Set(b,seed);break;}}
            var basic=GoatmanNpcContact.PrepareBasic(b,p,t,at);Require((basic.Impact.Packet.ResultFlags&1)!=0&&basic.Impact.Packet.Physical256>0,"Basic attack did not use captured unarmed offense");
            _=GoatmanNpcContact.ReleaseBasic(b,p,t,basic,at);
            var world=new GoatmanNpcContact.World(b,p,()=>at);Require(world.Active(b)&&world.Eligible(b,t),"Connected actual world services refused own context");
            var far=GoatmanNpcBodyActions.OpenPrepared(b,t,417,20000000,at);Require(far.ReleaseFrame>20000000&&far.EndFrame>far.ReleaseFrame,"Long-running world frame overflowed animation epoch");
            var action=GoatmanNpcBodyActions.OpenPrepared(b,t,417,500,at);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(0));
            GoatmanNpcActionProposal.Advance(action,action.ReleaseFrame,world);Require(action.Contacts==1&&action.Released,"Actual world action did not use ordered contact");
            network.Mobile=null;p.NetState=null;Require(!world.Active(b),"Offline world host remained active");network.Mobile=p;p.NetState=network;
            string state=b.SavedText;ulong seedBefore=NpcEquipmentRandom.State(b).Seed;bool refused=false;
            try{GoatmanNpcBodyActions.OpenPrepared(b,t,417,SourceFrameTimeline.MaximumFrame,at);}catch(NotSupportedException){refused=true;}
            Require(refused&&b.SavedText==state&&NpcEquipmentRandom.State(b).Seed==seedBefore,"Exhausted clock mutated body/RNG");
            Require(WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Learned Goatman route missing after approved host integration");
            if(retain)
            {
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))));kept=true;
            }
            Log.Information("Goatman ordered contacts:actual own-body Stun/Berserk/Frenzy/Cleave+basic;CB event/native HP/notifications/once-only cleanup,miss/range,own gear isolation and20million-frame action;connected world leaves,automatic AI still unfinished");
            Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            CombatStuns.Restore(stuns);t?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){b?.Delete();p.Delete();}
        }
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)saved.Body)as WarlockGoatmanBody;var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
        Require(b!=null&&p!=null&&b.ControlMaster==p&&b.SavedText==saved.State&&b.Map==Map.Internal&&SkillHealth.Balance256(b)==saved.Life
            &&NpcEquipmentRandom.State(b)==saved.Random,"Original Goatman own RNG/birth/wound/owner changed across save");
        ulong expected=saved.Random.Seed;uint next=SourceRandom.Roll(ref expected);
        Require(NpcEquipmentRandom.Raw(b)==next&&NpcEquipmentRandom.State(b).Seed==expected,"Restored unarmed RNG did not continue its next roll");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,Random=NpcEquipmentRandom.State(b)}));
    }
}
