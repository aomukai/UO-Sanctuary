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

public static class BladeFuryResourceChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BladeFuryResourceChecks));
    private sealed record Saved(uint Owner,int Mana256,BladeFuryChannelState Channel,SourceAnimationState Animation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blade-fury-resource-fixture.json");
    private static bool _creating;
    private static void Rates()
    {
        var old=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="BladeFuryResourceFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("blade-fury-resource-check",Check);Register("blade-fury-resource-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,
        "Blade Fury resource/sequence components, real player payments and saved mana; learned input disabled",_=>Core.LoopContext.Post(()=>
        {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}

    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-resource-source-probe.json")));
        int count=0,rankOneCorrections=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int N(int i)=>row[i].GetInt32();bool B(int i)=>N(i)!=0;
            int mana=N(3),emits=0,made=0,paid=0,rewinds=0;string order="";
            var services=new BladeFuryResourceServices(()=>mana,cost=>
            {if(mana<cost){return false;}mana-=cost;paid++;order+="M";return true;},()=>true,()=>B(5),()=>
            {emits++;order+="E";if(B(6)){made++;}return B(6);},()=>rewinds++);
            var state=new BladeFuryChannelState(B(1),B(1)?1020:0,N(2));
            var start=BladeFuryResources.Start(state,1000,N(0),services);
            int corrected=N(0)==1?256:0;
            Require(start.Success==B(7)&&mana==N(8)-N(9)*corrected&&emits==N(9)&&paid==N(10),"Complete source start/payment differential "+count);
            mana=N(4);var end=start;int result=-1;
            if(start.Success){end=BladeFuryResources.Do(start.State,1000,N(0),services);result=end.Success?1:0;}
            Require(result==N(11)&&mana==N(12)-(N(13)-N(9))*corrected&&emits==N(13)&&made==N(14)&&paid==N(15)
                &&end.State.Active==B(16)&&(!end.State.Active||end.State.Expires==N(17))&&end.State.NextShot==N(18)
                &&rewinds==N(19)&&end.State.Flags==row[20].GetUInt32()&&order==row[21].GetString(),"Complete source do/channel/payment/order differential "+count);
            if(corrected>0&&emits>0){rankOneCorrections++;}count++;
        }
        Require(count==7200&&rankOneCorrections>0,"Resource source inventory/rank-one policy not exercised");
        Require(BladeFuryResources.Cost256(1)==256&&BladeFuryResources.Cost256(3)==320&&BladeFuryResources.Cost256(20)==864,"Current table fractional rank costs");
        Timing();
        Log.Information("Blade Fury resources: {Count} complete source compositions match with explicit current-table rank1 correction in {Corrected} shot contexts; failed factory attempts pay, same-frame reentry cannot double-shoot; world/input/charges/Blood Mana remain caller scope",count,rankOneCorrections);
    }

    private static void Events(SourceAnimationEvent[] events,JsonElement expected)
    {
        Require(events.Length==expected.GetArrayLength(),"Animation event count");int i=0;
        foreach(var row in expected.EnumerateArray())
        {var e=events[i++];Require(e==new SourceAnimationEvent(row[0].GetInt32(),row[1].GetInt32(),row[2].GetInt32(),row[3].GetInt32()),"Source animation event/frame/kind/ordinal differential");}
    }
    private static void Timing()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"blade-fury-timing-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("initial").EnumerateArray())
        {
            var state=BladeFuryAnimation.Create(900*256,row[1].GetInt32());var result=SourceAnimationEvents.Initial(state,1000);
            Require(row[2].GetInt32()==0&&result.Epoch256==row[3].GetInt32(),"SQ zero-bonus/initial epoch");Events(result.Events,row[4]);count++;
        }
        foreach(var row in doc.RootElement.GetProperty("rewind").EnumerateArray())
        {
            var state=BladeFuryAnimation.Create((1000-row[2].GetInt32())*256,row[1].GetInt32())
                with{Events=[new(1,1009,4),new(2,1020,0),new(3,1021,0)]};
            var result=SourceAnimationEvents.Renew(state,1000,row[3].GetInt32());
            Require(result.Epoch256==row[4].GetInt32(),"Source renewal epoch");Events(result.Events,row[5]);
            var saved=JsonSerializer.Deserialize<SourceAnimationState>(JsonSerializer.Serialize(result));
            Require(JsonSerializer.Serialize(saved)==JsonSerializer.Serialize(result),"Animation component JSON round-trip");count++;
        }
        Require(count==490,"Initial/renewal source inventory");
        Log.Information("Blade Fury SQ23:490 complete initial/rewind controls match native event planning; state-removal events preserved; event dispatch/input/rate producer remain separate");
    }

    private static void Check()
    {
        Differential();PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Assassin);}finally{_creating=false;}
        p.Name="BladeFuryResourceFixture";bool keep=false;
        try
        {
            p.AddItem(new Backpack());Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Fractional resource fixture regen policy was replaced");
            Require(SkillMechanics.Rank(p,CombatAbility.BladeFury)==1,"Native base rank");
            int frame=1000,attempts=0;bool factory=true;
            var animation=BladeFuryAnimation.Create(frame*256,256);
            var services=BladeFuryResources.Player(p,()=>true,()=>true,()=>{attempts++;return factory;},
                ()=>animation=SourceAnimationEvents.Renew(animation,frame));
            var state=new BladeFuryChannelState(false,0,0);
            NecromancerEffects.SetMana256(p,1535);
            Require(!BladeFuryResources.Start(state,frame,1,services).Success&&NecromancerEffects.ManaBalance256(p)==1535,"Fresh threshold spent/created below six mana");
            NecromancerEffects.SetMana256(p,1663);var start=BladeFuryResources.Start(state,frame,1,services);state=start.State;
            Require(start.Success&&state is{Active:true,Expires:1021,NextShot:0}&&NecromancerEffects.ManaBalance256(p)==1663,"Fresh state debited startmana or lost fractional balance");
            animation=SourceAnimationEvents.Initial(animation,frame);
            Require(animation.Events.Single(e=>e.Type==SourceAnimationEvents.ModeChange).Frame==1012
                &&animation.Events.Single(e=>e.Type==SourceAnimationEvents.EndAnimation).Frame==1019,"SQ initial action/end events");
            frame=1012;var shot=BladeFuryResources.Do(state,frame,1,services);state=shot.State;
            Require(shot.Success&&attempts==1&&NecromancerEffects.ManaBalance256(p)==1407&&state.NextShot==1016
                &&animation.Epoch256==1001*256,"Actual rank1 do payment or renewal binding");
            frame=1013;var held=BladeFuryResources.Start(state,frame,1,services);state=held.State;
            var noShot=BladeFuryResources.Do(state,frame,1,services);state=noShot.State;
            Require(held.Success&&!noShot.Success&&attempts==1&&NecromancerEffects.ManaBalance256(p)==1407&&state.Expires==1020,"Ready-future reentry or same-frame do double-paid/fired");

            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
            p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual soft-rank equipment");ClassGear.Refresh(p);
            Require(SkillMechanics.Rank(p,CombatAbility.BladeFury)==2,"Actual soft rank");
            frame=1017;shot=BladeFuryResources.Start(state,frame,SkillMechanics.Rank(p,CombatAbility.BladeFury),services);state=shot.State;
            Require(shot.Success&&attempts==2&&NecromancerEffects.ManaBalance256(p)==1119,"Live-rank invocation did not pay288FP");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,CombatAbility.BladeFury)==1,"Removed soft rank");
            frame=1022;shot=BladeFuryResources.Start(state,frame,1,services);state=shot.State;
            Require(shot.Success&&attempts==3&&NecromancerEffects.ManaBalance256(p)==863,"Active state reapplied six-mana threshold or paid wrong rank");
            frame=1027;NecromancerEffects.SetMana256(p,255);var before=state;var oldAnimation=animation;
            var rejected=BladeFuryResources.Start(state,frame,1,services);
            Require(!rejected.Success&&rejected.State==before&&animation==oldAnimation
                &&attempts==3&&NecromancerEffects.ManaBalance256(p)==255,"Unaffordable active start renewed/emitted/paid");
            NecromancerEffects.SetMana256(p,863);factory=false;shot=BladeFuryResources.Start(state,frame,1,services);state=shot.State;
            Require(shot.Success&&attempts==4&&NecromancerEffects.ManaBalance256(p)==607&&state.NextShot==1031,"Failed factory attempt refunded or repeated a source shot");
            Require(!AssassinCombat.Implemented(CombatAbility.BladeFury)&&!ClassCombat.Available(p,CombatAbility.BladeFury)
                &&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Component exposed unfinished learned input");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,607,state,animation)));keep=true;
        }
        finally{if(!keep){p.Delete();}}
    }
    private static void Reload()
    {
        Differential();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(p!=null&&!p.Deleted&&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&p.Mana==2
            &&SkillMechanics.Rank(p,CombatAbility.BladeFury)==1,"Saved native owner/rank/fractional mana changed");
        Require(saved.Channel is{Active:true,Expires:1034,NextShot:1031}&&saved.Animation.Epoch256>1000*256
            &&ClassCombat.For(p).PendingAbility==CombatAbility.None&&!ClassCombat.Available(p,CombatAbility.BladeFury),"Fixture component snapshot or gated input changed");
        Log.Information("Blade Fury resource reload: real character/equipment and607FP mana retained; component snapshot is fixture data, not a persisted production channel; no automatic action/payment replay");
    }
}
