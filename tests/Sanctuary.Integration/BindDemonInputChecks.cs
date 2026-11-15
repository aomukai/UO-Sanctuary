// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BindDemonInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BindDemonInputChecks));
    private sealed record Saved(int Generation,uint Owner,uint Bound,uint Target,BoundDemonState State,int Life,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"bind-demon-input-fixtures.json");
    private static bool Creating;
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=m=>Creating||m.Name=="BindInputFixture"?TimeSpan.FromHours(1):hp?.Invoke(m)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=m=>Creating||m.Name=="BindInputFixture"?TimeSpan.FromHours(1):mp?.Invoke(m)??Mobile.DefaultManaRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"bind-demon-input-reload-check":"bind-demon-input-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared saved held input/shared world tick/cancellation/native saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);Log.Information("{Name}: passed",name+"-result");}else{Scene(n,name+"-result");}}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    // Seed a legitimate historical version1 binding through the existing save
    // reader. Ordinary assignment/admission must remain unavailable. Preserve
    // every other fixture's row;no production availability bypass is installed.
    private static void SavedBindings(PlayerMobile p)
    {
        var reader=new BufferReader(NecromancerBindingChecks.Encode());Require(reader.ReadInt()==1,"Binding version changed");int count=reader.ReadInt();
        var rows=new List<(uint Owner,byte Class,byte Left,byte Right,byte[] Quick)>();
        for(int i=0;i<count;i++)
        {
            uint owner=reader.ReadUInt();byte cls=reader.ReadByte(),left=reader.ReadByte(),right=reader.ReadByte();var quick=new byte[SkillBindingProtocol.Slots];
            for(int j=0;j<quick.Length;j++){quick[j]=reader.ReadByte();}
            if(owner!=p.Serial.Value||cls!=(byte)DiabloClass.Warlock){rows.Add((owner,cls,left,right,quick));}
        }
        var slots=new byte[SkillBindingProtocol.Slots];slots[7]=(byte)CombatAbility.BindDemon;
        rows.Add((p.Serial.Value,(byte)DiabloClass.Warlock,(byte)CombatAbility.BindDemon,(byte)CombatAbility.BindDemon,slots));
        var writer=new BufferWriter(new byte[4*1024*1024],true);writer.Write(1);writer.Write(rows.Count);
        foreach(var row in rows)
        {writer.Write(row.Owner);writer.Write(row.Class);writer.Write(row.Left);writer.Write(row.Right);foreach(byte q in row.Quick){writer.Write(q);}}
        SkillBindings.Instance.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));SkillBindings.Instance.PostDeserialize();
        var pair=SkillBindings.Instance.Get(p);ClassCombat.For(p).Left=pair.Left;ClassCombat.For(p).Right=pair.Right;
        Require(pair.Left==CombatAbility.BasicAttack&&pair.Right==CombatAbility.BindDemon&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==CombatAbility.BindDemon,"Pinned left restriction or historical right/quick binding lost");
    }
    private static Daemon Body(PlayerMobile p,int offset)
    {
        Daemon b;Creating=true;try{b=new Daemon();}finally{Creating=false;}
        b.Name="BindInputFixture";b.SetHits(10000);b.MoveToWorld(new(p.X+offset,p.Y,p.Z),p.Map);b.AIObject.AITimer.Stop();
        MonsterVariantBaselines.Restore(MonsterVariantBaselines.Snapshot().Where(r=>r.Actor!=b.Serial.Value).ToArray());SkillHealth.SetBalance256(b,4000*256-7);return b;
    }
    private static bool Input(PlayerMobile p,CombatRequest action,int source,BaseCreature target,DateTime at)
        =>BindDemonInput.ApplyPrepared(p,action,source,target?.Serial??Serial.Zero,target?.Location??p.Location,at);
    private static void Press(PlayerMobile p,int source,Daemon target,DateTime at)
    {Require(Input(p,CombatRequest.ChannelStart,source,target,at),"Prepared saved input refused");}
    private static void Checks(PlayerMobile p,Daemon first,Daemon other)
    {
        var at=TimedEffects.Now.AddMinutes(1);int mana=NecromancerEffects.ManaBalance256(p);
        Require(!WarlockCombat.Implemented(CombatAbility.BindDemon)&&!ClassCombat.Available(p,CombatAbility.BindDemon)
            &&!SkillBindings.Instance.Assign(p,false,CombatAbility.BindDemon)&&!SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BindDemon),"Preparation opened ordinary learning/assignment");
        Require(!PlayerInfernoInput.Apply(p,CombatRequest.ChannelStart,1,CombatAbility.BindDemon,first.Serial,first.Location,at)&&BindDemonChannel.State(p)==null,"Gated production dispatch admitted Bind");
        Require(!Input(p,CombatRequest.ChannelAim,1,first,at)&&!Input(p,CombatRequest.ChannelStart,0,first,at)
            &&!Input(p,CombatRequest.ChannelStart,14,first,at)&&!BindDemonInput.BoundPrepared(p,-1),"Heartbeat/left/invalid-source admitted input");
        Press(p,1,first,at);var state=BindDemonChannel.State(p);
        Require(state is{InputBound:true,Source:1}&&!Input(p,CombatRequest.ChannelStart,1,other,at)&&BindDemonChannel.State(p)==state,"Duplicate press reset target/deadline");
        Require(!Input(p,CombatRequest.ChannelAim,9,other,at.AddMilliseconds(100))&&BindDemonChannel.State(p)==state,"Wrong-source renewal changed active input");
        Require(Input(p,CombatRequest.ChannelAim,1,other,at.AddMilliseconds(200))&&BindDemonChannel.State(p).Target==first.Serial.Value,"Cursor heartbeat replaced locked original target");
        PlayerInfernoInput.EndHeld(p,9);Require(BindDemonChannel.State(p)!=null,"Wrong-source release ended input");PlayerInfernoInput.EndHeld(p,1);
        Require(!Input(p,CombatRequest.ChannelAim,1,other,at.AddMilliseconds(300))&&BindDemonChannel.State(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Released heartbeat restarted/debited");
        Log.Information("Bind input:historical saved right/quick bind retained with forbidden left normalized;ordinary assignment/production dispatch stay gated;explicit source/duplicate/heartbeat/locked target/release controls are free");
        Press(p,9,first,at);ClassMovement.Moved(p,Direction.East);Require(BindDemonChannel.State(p)==null,"Accepted movement hook failed immediate cancellation");
        Press(p,1,first,at);var dagger=new Dagger();Require(p.EquipItem(dagger),"Equip real interruption weapon");Require(BindDemonChannel.State(p)==null,"Real weapon equip failed immediate cancellation");dagger.Delete();
        Press(p,1,first,at);Require(SkillBindings.Instance.Assign(p,false,CombatAbility.DeathMark)&&BindDemonChannel.State(p)==null,"Ordinary mouse rebinding failed shared Stop");SavedBindings(p);
        Press(p,9,first,at);Require(SkillBindings.Instance.AssignQuick(p,7,CombatAbility.DeathMark),"Change actual quick binding");BindDemonChannel.Tick(at.AddMilliseconds(100));Require(BindDemonChannel.State(p)==null,"Quick rebinding stayed authorized");SavedBindings(p);
        Press(p,1,first,at);ClassCombat.Stop(p);Require(BindDemonChannel.State(p)==null,"Shared Stop left held input");
        ClassCombat.For(p).NextAction=Core.TickCount+500;Require(!Input(p,CombatRequest.ChannelStart,1,first,at),"Recovery boundary bypassed");ClassCombat.For(p).NextAction=Core.TickCount-1;
        ClassCombat.For(p).PendingAbility=CombatAbility.DeathMark;Require(!Input(p,CombatRequest.ChannelStart,1,first,at),"Pending action bypassed");ClassCombat.For(p).PendingAbility=CombatAbility.None;
        Require(!ClassCombat.ContinueCast(p,CombatAbility.BindDemon,first,first.Location,Core.TickCount)&&!ClassCombat.Perform(p,CombatAbility.BindDemon,first,first.Location,Core.TickCount),"Legacy one-shot/held cast bypassed explicit channel");
        Press(p,1,first,at);Require(!BindDemonInput.ApplyPrepared(p,CombatRequest.ChannelAim,1,other.Serial,new Point3D(0,0,0),at.AddMilliseconds(200))&&BindDemonChannel.State(p)==null,"Invalid map aim left input active");
        Press(p,1,first,at);EventSink.InvokeDisconnected(p);Require(BindDemonChannel.State(p)==null,"Native disconnect event left input");EventSink.InvokeConnected(p);
        Press(p,1,first,at);EventSink.InvokeLogout(p);Require(BindDemonChannel.State(p)==null,"Native logout event left input");EventSink.InvokeConnected(p);
        Require(NecromancerEffects.ManaBalance256(p)==mana&&!first.Controlled&&!other.Controlled,"Input guards/deactivation spent mana or captured target");
        Log.Information("Bind input:real move/equip/mouse-quick rebind/shared Stop/recovery/pending/invalid aim and native disconnect/logout cancel;legacy one-shot routes refuse;no mana/ownership mutation");
        var oldRoll=BindDemonChannel.Roll;
        try
        {
            Press(p,1,first,at);foreach(int ms in new[]{200,400,600,800}){Require(Input(p,CombatRequest.ChannelAim,1,first,at.AddMilliseconds(ms)),"Failure fixture lease renewal");}
            var failed=BindDemonChannel.State(p);BindDemonChannel.Roll=n=>throw new NotSupportedException("Prepared input fixture failure");
            Require(BindDemonChannel.UpdateInput(p,failed,at.AddSeconds(1))is NotSupportedException&&BindDemonChannel.State(p)==null&&!first.Controlled&&NecromancerEffects.ManaBalance256(p)==mana-9*256,"Paid callback exception left active input/refunded/bound target");
            BindDemonChannel.Tick(at.AddSeconds(2));Require(NecromancerEffects.ManaBalance256(p)==mana-9*256,"Failed callback continued charging");NecromancerEffects.SetMana256(p,mana);
            Press(p,1,first,at);foreach(int ms in new[]{200,400,600,800}){Require(Input(p,CombatRequest.ChannelAim,1,first,at.AddMilliseconds(ms)),"Fresh failure fixture lease renewal");}
            failed=BindDemonChannel.State(p);long fresh=0;
            BindDemonChannel.Roll=n=>{BindDemonChannel.Cancel(p);Press(p,1,other,at.AddSeconds(1));fresh=BindDemonChannel.State(p).Id;throw new NotSupportedException("Fresh-channel fixture failure");};
            Require(BindDemonChannel.UpdateInput(p,failed,at.AddSeconds(1))is NotSupportedException&&BindDemonChannel.State(p)?.Id==fresh&&!first.Controlled,"Failed old callback removed fresh input");BindDemonChannel.Cancel(p);NecromancerEffects.SetMana256(p,mana);
            Log.Information("Bind input:expected callback failure after payment stops its reserved generation without refund/repeat;fresh callback channel survives failed old attempt;world logging is separate from mechanical cleanup");
        }
        finally{BindDemonChannel.Roll=oldRoll;BindDemonChannel.Cancel(p);}
    }
    private static void Scene(NetState network,string result)
    {
        Creating=true;PlayerMobile p;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="BindInputFixture";p.Female=true;p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.binddemon",20);FixtureSpace.Corridor(p,7000,24,3500);
        var account=FixtureNetwork.Authenticate(network,p,"bind-input-");var oldRoll=BindDemonChannel.Roll;Daemon first=null,other=null;bool kept=false,armed=false;
        void Cleanup()
        {
            BindDemonChannel.Roll=oldRoll;BindDemonChannel.Cancel(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){first?.Delete();other?.Delete();p.Delete();BoundDemonHosts.Tick();}
        }
        try
        {
            SavedBindings(p);NecromancerEffects.SetMana256(p,90*256);first=Body(p,4);other=Body(p,8);Checks(p,first,other);ClassCombat.Stop(p);
            int rolls=0;BindDemonChannel.Roll=n=>{rolls++;return 0;};var start=TimedEffects.Now;int life=SkillHealth.Balance256(first),maximum=SkillHealth.Maximum256(first);Press(p,1,first,start);
            long deadline=Core.TickCount+4000;DateTime lastHeartbeat=start;bool finishedHeartbeats=false;
            void Observe()
            {
                try
                {
                    Require(Core.TickCount<deadline&&network.Running,"Natural shared combat tick did not finish Bind");var now=TimedEffects.Now;double elapsed=(now-start).TotalMilliseconds;
                    if(!finishedHeartbeats&&elapsed<850&&(now-lastHeartbeat).TotalMilliseconds>=180)
                    {Require(Input(p,CombatRequest.ChannelAim,1,other,now),"Natural heartbeat ended input");lastHeartbeat=now;}
                    if(elapsed>=850){finishedHeartbeats=true;}
                    if(BoundDemonHosts.State(first)is{} state)
                    {
                        first.AIObject.AITimer.Stop();Require(finishedHeartbeats&&rolls==1&&NecromancerEffects.ManaBalance256(p)==81*256&&BindDemonChannel.State(p)==null
                            &&!other.Controlled&&Math.Abs(SkillHealth.Balance256(first)-(long)life*SkillHealth.Maximum256(first)/maximum)<=1,"Natural tick duplicated payment,retargeted,healed or skipped windup");
                        Press(p,1,other,now);PersistentMinions.Suspend(p);p.NetState=null;Require(BindDemonChannel.State(p)==null,"Real NetState detach left partial held input");
                        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,first.Serial.Value,other.Serial.Value,BoundDemonHosts.State(first),SkillHealth.Balance256(first),NecromancerEffects.ManaBalance256(p))));kept=true;Cleanup();
                        Log.Information("Bind input:actual existing40ms combat timer completes one native original bind after heartbeats stop before first deadline;one roll/nine mana,locked target/wounds,identified cast pose;real NetState detach cancels next partial channel");Log.Information("{Name}: passed",result);return;
                    }
                    Timer.StartTimer(TimeSpan.FromMilliseconds(40),Observe);
                }
                catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",result);}
            }
            armed=true;Timer.StartTimer(TimeSpan.FromMilliseconds(40),Observe);
        }
        finally{if(!armed){Cleanup();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many held input reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(Daemon)World.FindMobile((Serial)saved.Bound);var target=(Daemon)World.FindMobile((Serial)saved.Target);
        Require(BindDemonChannel.State(p)==null&&BoundDemonHosts.State(body)==saved.State&&SkillHealth.Balance256(body)==saved.Life
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana&&!target.Controlled,"Saved native binding/wounds/mana changed or held input replayed");
        var account=FixtureNetwork.Authenticate(network,p,"bind-input-reload-");
        try
        {
            PersistentMinions.Resume(p);body.AIObject.AITimer.Stop();target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            Require(BindDemonInput.BoundPrepared(p,1)&&BindDemonInput.BoundPrepared(p,9)&&!BindDemonInput.BoundPrepared(p,0),"Saved right/quick input authorization changed");
            Press(p,9,target,TimedEffects.Now);PlayerInfernoInput.EndHeld(p,9);Require(BindDemonChannel.State(p)==null&&BoundDemonHosts.For(p).Single()==body,"Reload quick press/release replaced original bound body");PersistentMinions.Suspend(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,State=BoundDemonHosts.State(body),Life=SkillHealth.Balance256(body),Mana=NecromancerEffects.ManaBalance256(p)}));
            Log.Information("Bind input:saved native identity/wounds/mana and historical right/quick bindings survive reload;partial held intent absent;fresh quick press/release keeps original demon with no extra debit");
        }
        finally{BindDemonChannel.Cancel(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
