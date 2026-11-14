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

public static class DefilerCastChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DefilerCastChecks));
    private static readonly CombatAbility Ability=CombatAbility.SummonDefiler;
    private sealed record Saved(int Generation,uint Owner,uint Body,string Text,DeathMarkSavedActor Source,NpcEquipmentSeed Random,int Life,int Maximum);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"defiler-cast-fixtures.json");
    private static bool Creating;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Rates(){var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name?.StartsWith("DefilerCastFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"defiler-cast-reload-check":"defiler-cast-check";
            Register(name,()=>{Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>{if(reload){Reload(n,name+"-result");}else{Check(n,name+"-result");}});});}
        foreach(var suffix in new[]{"right","quick"}){string kind=suffix;Register("defiler-cast-"+kind+"-check",()=>
            {Log.Information("{Name}: passed","defiler-cast-"+kind+"-check");FixtureNetwork.Connected("defiler-cast-"+kind+"-result",n=>Input(n,kind));});}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Defiler saved host/cap/payment and actual input",_=>Core.LoopContext.Post(()=>
    {try{run();}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner()
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="DefilerCastFixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.summondefiler",6);
        FixtureSpace.Corridor(p,6560,28,3500);Ready(p);return p;
    }
    private static void Ready(PlayerMobile p)
    {
        ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
        var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||c.Ability!=Ability).ToArray()});
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
    }
    private static void Clear(PlayerMobile p){foreach(var b in WarlockGoatmanWorld.Demons(p)){b.Delete();}}
    private static WarlockDefilerBody Cast(PlayerMobile p,Point3D point)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var values=SkillMechanics.Values(p,Ability);
        Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Learned Defiler SC start refused");var s=ClassCombat.For(p);long effect=s.EffectAt;
        Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana,"Summon paid before its SC release");
        Require(ClassCombat.ResolveAction(p,effect)&&!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256,"Summon did not debit once at committed release");
        var body=WarlockGoatmanWorld.ForDefiler(p).OrderBy(BattleCommand.PetOrder).Last();
        Require(body.Prepared is{LearnedCast:true,Controller:{Fault:null}}&&body.Prepared.Snapshot.Birth.Suppliers.Rank==values.Rank
            &&NpcEquipmentRandom.State(body)!=null&&DeathMarkWorld.Header(body).Control.Function==0x4b60c0,"Learned body lost its captured rank/host/random/source header");
        return body;
    }
    private static void Check(NetState network,string result)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"defiler-cast-");bool kept=false;var blocks=new List<Item>();
        try
        {
            var point=new Point3D(p.X+3,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Available(p,Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)
                &&SkillBindings.Instance.AssignQuick(p,7,Ability),"Learned talent/mouse/quick bindings unavailable");
            Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Cancel control start refused");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,effect)&&WarlockGoatmanWorld.ForDefiler(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled windup allocated/paid");
            var a=Cast(p,point);var birth=a.Prepared.Snapshot;Require(WarlockGoatmanWorld.Limit(p)==1,"Hard mastery0 cap differs");
            var b=Cast(p,new(point.X+1,point.Y,point.Z));Require(a.Deleted&&!b.Deleted&&WarlockGoatmanWorld.Demons(p).Length==1,"Cap1 did not replace oldest after successful payment");
            ClassSkillChecks.Train(p,"warlock.demonicmastery",5);var c=Cast(p,point);Require(!b.Deleted&&WarlockGoatmanWorld.Demons(p).Length==2,"Hard mastery5 cap differs");
            var d=Cast(p,new(point.X+2,point.Y,point.Z));Require(b.Deleted&&!c.Deleted&&WarlockGoatmanWorld.Demons(p).Length==2,"Cap2 replacement order differs");
            ClassSkillChecks.Train(p,"warlock.demonicmastery",10);var e=Cast(p,new(point.X+1,point.Y,point.Z));var eBirth=e.Prepared.Snapshot;Require(WarlockGoatmanWorld.Demons(p).Length==3,"Hard mastery10 cap differs");
            ClassSkillChecks.Train(p,"warlock.summongoatman");var goat=WarlockGoatmanBody.CreatePrepared(p,new(p.X+7,p.Y,p.Z),TimedEffects.Now);
            var last=Cast(p,new(point.X+3,point.Y,point.Z));Require(c.Deleted&&d.Deleted&&!e.Deleted&&!goat.Deleted&&!last.Deleted&&WarlockGoatmanWorld.Demons(p).Length==3,"Shared demon pool did not count other species/oldest order");
            var nativeTarget=new GoatmanNpcContactChecks.Recipient{NoKillAwards=true};nativeTarget.MoveToWorld(new(last.X+1,last.Y,last.Z),last.Map);nativeTarget.AIObject?.AITimer.Stop();
            var fists=new Fists();int nativeLife=SkillHealth.Balance256(nativeTarget);var nativeSeed=NpcEquipmentRandom.State(last);
            try{Require(fists.OnSwing(last,nativeTarget)==TimeSpan.FromSeconds(1)&&SkillHealth.Balance256(nativeTarget)==nativeLife&&NpcEquipmentRandom.State(last)==nativeSeed,
                "Native Fists bypassed source-only swing suppression or consumed damage RNG");}finally{fists.Delete();nativeTarget.Delete();}
            var ids=WarlockGoatmanWorld.Demons(p).Select(x=>x.Serial).ToArray();Ready(p);NecromancerEffects.SetMana256(p,1);
            Require(!ClassCombat.Perform(p,Ability,null,point,Core.TickCount)&&WarlockGoatmanWorld.Demons(p).Select(x=>x.Serial).SequenceEqual(ids),"Underfunded start touched existing roster");
            Ready(p);Require(ClassCombat.Perform(p,Ability,null,point,Core.TickCount),"Late mana control refused");effect=ClassCombat.For(p).EffectAt;NecromancerEffects.SetMana256(p,0);
            Require(!ClassCombat.ResolveAction(p,effect)&&WarlockGoatmanWorld.Demons(p).Select(x=>x.Serial).SequenceEqual(ids),"Late mana failure replaced existing demon");
            Ready(p);var blocked=new Point3D(p.X+10,p.Y,p.Z);Require(ClassCombat.Perform(p,Ability,null,blocked,Core.TickCount),"Late placement control refused");effect=ClassCombat.For(p).EffectAt;
            int tile=Enumerable.Range(1,0x4000).First(i=>TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].Height>=16);
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++){var wall=new Static(tile);wall.MoveToWorld(new(blocked.X+x,blocked.Y+y,blocked.Z),p.Map);blocks.Add(wall);}
            mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&WarlockGoatmanWorld.Demons(p).Select(x=>x.Serial).SequenceEqual(ids),"Failed placement paid/replaced roster");
            foreach(var item in blocks){item.Delete();}blocks.Clear();
            Require(e.Prepared.Snapshot==eBirth&&c.Prepared.Snapshot.HardMastery==5&&last.Prepared.Snapshot.Birth.Suppliers.PetMaximum==3&&birth.Birth.Suppliers.PetMaximum==1,"Cast/current cap recaptured original birth snapshot");
            // Old bodies remain byte-compatible and outside automatic hosts.
            var legacy=WarlockDefilerBody.CreatePrepared(p,new(p.X+9,p.Y,p.Z),TimedEffects.Now);string text=legacy.SavedText;
            Require(!text.Contains("Controller")&&!text.Contains("LearnedCast")&&legacy.Prepared.Controller==null,"Legacy JSON gained a default controller/learned flag");legacy.Delete();
            foreach(var pet in WarlockGoatmanWorld.Demons(p).Where(x=>x!=last).ToArray()){pet.Delete();}
            var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;
            enemy.MoveToWorld(new(last.X+6,last.Y,last.Z),last.Map);enemy.AIObject?.AITimer.Stop();
            var second=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};second.SetHits(50000);second.Hits=second.HitsMax;
            second.MoveToWorld(new(last.X+7,last.Y,last.Z),last.Map);second.AIObject?.AITimer.Stop();
            var doomed=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};doomed.SetHits(50000);doomed.Hits=doomed.HitsMax;
            doomed.MoveToWorld(new(last.X+5,last.Y,last.Z),last.Map);doomed.AIObject?.AITimer.Stop();
            DateTime tick=TimedEffects.Now;bool clockChecked=false,refusal=false;
            try
            {
                for(int n=0;n<250&&(last.Prepared.Links?.Marks.Length??0)<2;n++)
                {
                    tick=tick.AddMilliseconds(40);WarlockGoatmanWorld.Tick(tick);
                    if(!clockChecked&&DefilerLinkCasting.State(last)is{Released:false} pending)
                    {
                        Require(pending.Target==doomed.Serial.Value&&last.Prepared.Links==null,"Source type2 did not select nearest unmarked enemy/windup");
                        doomed.Delete();clockChecked=true;
                    }
                    if(DefilerLinkCasting.State(last)is{Refusal:not null}){refusal=true;}
                    Require(last.Prepared.Controller is{Fault:null},"Refused lost target permanently faulted automatic Defiler");
                }
                Require(clockChecked&&refusal&&last.Prepared.Links is{Marks.Length:2}&&SkillHealth.Balance256(enemy)==enemy.HitsMax*256
                    &&SkillHealth.Balance256(second)==second.HitsMax*256,"Automatic source root/S1 did not recover and link without attacking");
                Require(DefilerHealthLink.HasState(enemy,210,tick)&&DefilerHealthLink.HasState(last,213,tick),"Source type2 state producer lost actual marks/caster group");
                bool unknown=false;try{_=DefilerHealthLink.HasState(enemy,42,tick);}catch(NotSupportedException){unknown=true;}Require(unknown,"Unknown source state silently absent");
                int other=SkillHealth.Balance256(second),percent=last.Prepared.Links.Marks.Single(m=>m.Target==enemy.Serial.Value).Percent;
                SkillCombat.FireImpact(p,enemy,25600);Require(other-SkillHealth.Balance256(second)==25600*percent/100,"Automatic link group failed actual typed sharing");
                var fistsAgain=new Fists();var damage=SkillHealth.Balance256(enemy);var ownSeed=NpcEquipmentRandom.State(last);
                try{Require(fistsAgain.OnSwing(last,enemy)==TimeSpan.FromSeconds(1)&&SkillHealth.Balance256(enemy)==damage&&NpcEquipmentRandom.State(last)==ownSeed,"Automatic Defiler allowed native attack damage/RNG");}finally{fistsAgain.Delete();}
            }
            finally{enemy.Delete();second.Delete();doomed.Delete();}
            DefilerLinkCasting.Cancel(last);p.MoveToWorld(new(6586,p.Y,p.Z),p.Map);
            for(int n=0;n<150&&last.GetDistanceToSqrt(p)>4;n++){tick=tick.AddMilliseconds(40);WarlockGoatmanWorld.Tick(tick);}
            Require(last.GetDistanceToSqrt(p)<=4&&(DeathMarkWorld.Flags128(last)&0x10000)!=0&&DeathMarkWorld.Path(last).Size5c==2&&DeathMarkWorld.Path(last).Mask64==0x1804,
                "Automatic Defiler source follow/near-owner warp lost species path or native placement");
            SkillHealth.SetBalance256(last,last.MaximumLife256-257);PersistentMinions.Suspend(p);network.Mobile=null;p.NetState=null;
            WarlockGoatmanWorld.Tick(TimedEffects.Now);Require(last.Map==Map.Internal&&last.Prepared.Controller is{Fault:null},"Offline production host did not park cleanly");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,last.Serial.Value,last.SavedText,DeathMarkWorld.State(last),NpcEquipmentRandom.State(last),SkillHealth.Balance256(last),last.MaximumLife256)));kept=true;
            Log.Information("Learned Defiler:SC/snapshot/mana once,unpaid cancel,hard5/10shared pool/oldest replacement,failure roster preservation,legacy omission and actual saved production host;automatic input scenes verify linking/sharing");
            Log.Information("{Result}: passed",result);
        }
        catch(Exception ex){Log.Error(ex,"{Result}: failed",result);}
        finally{foreach(var item in blocks){item.Delete();}network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){Clear(p);p.Delete();}}
    }
    private static void Reload(NetState network,string result)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner)as PlayerMobile;var b=World.FindMobile((Serial)s.Body)as WarlockDefilerBody;
        Server.Accounting.Account account=null;
        try
        {
            // Automatic offline ticks may advance only the clock and recovery
            // deadline. Original birth/control/resources stay unchanged.
            var old=JsonSerializer.Deserialize<DefilerBodyState>(s.Text);var current=b?.Prepared;
            Require(p!=null&&b is{Deleted:false,Map:not null}&&b.Map==Map.Internal&&b.ControlMaster==p&&current is{LearnedCast:true,Controller:{Fault:null}}
                &&current.Snapshot==old.Snapshot&&SkillHealth.Balance256(b)==s.Life&&b.MaximumLife256==s.Maximum
                &&NpcEquipmentRandom.State(b)==s.Random&&DeathMarkWorld.State(b)==s.Source,"Original learned summon replayed or changed its captured body/control on load");
            Require(SkillBindings.Instance.Get(p).Left!=Ability&&SkillBindings.Instance.Get(p).Right==Ability&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Learned Defiler bindings lost on reload");
            account=FixtureNetwork.Authenticate(network,p,"defiler-reload-");WarlockGoatmanWorld.Tick(TimedEffects.Now);
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==s.Life&&b.Prepared.Controller is{Fault:null}&&NpcEquipmentRandom.State(b)==s.Random,
                "Original saved learned host failed real login/reactivation without healing or reroll");
            network.Mobile=null;p.NetState=null;WarlockGoatmanWorld.Tick(TimedEffects.Now);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Text=b.SavedText,Source=DeathMarkWorld.State(b)}));Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally{network.Mobile=null;if(p!=null){p.NetState=null;}if(account!=null){account[0]=null;network.Account=null;account.Delete();}}
    }
    private static void Input(NetState network,string kind)
    {
        var p=Owner();p.Female=kind=="quick";ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        if(p.Female){PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);}
        var account=FixtureNetwork.Authenticate(network,p,"defiler-wire-");Ready(p);
        Require(SkillBindings.Instance.Assign(p,kind=="left",Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Defiler socket binding");
        var values=SkillMechanics.Values(p,Ability);int mana=NecromancerEffects.ManaBalance256(p);var point=new Point3D(p.X+3,p.Y,p.Z);
        var target=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
        var adjacent=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};adjacent.SetHits(50000);adjacent.Hits=adjacent.HitsMax;adjacent.MoveToWorld(new(p.X+7,p.Y,p.Z),p.Map);adjacent.AIObject?.AITimer.Stop();
        string label="defiler-cast-"+kind;long deadline=Core.TickCount+10000;bool began=false,finished=false;
        void Cleanup(){ClassCombat.Stop(p);target.Delete();adjacent.Delete();Clear(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();network.Disconnect("Defiler input fixture complete");}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException($"Defiler input timeout began={began},pets={WarlockGoatmanWorld.ForDefiler(p).Length},pending={ClassCombat.For(p).PendingAbility},damage={target.Notifications},fault={WarlockGoatmanWorld.ForDefiler(p).FirstOrDefault()?.Prepared.Controller?.Fault}");}
                if(ClassCombat.For(p).PendingAbility==Ability&&!began)
                {Require(ClassCombat.For(p).Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana&&WarlockGoatmanWorld.ForDefiler(p).Length==0,"Wire windup paid/allocated early");began=true;Log.Information("{Label}: windup",label);}
                var body=WarlockGoatmanWorld.ForDefiler(p).SingleOrDefault();
                if(body!=null&&body.Prepared.Links is{Marks.Length:2})
                {
                    Require(began&&body.Prepared.Controller is{Fault:null}&&body.Prepared.Controller.Clock.Frame>0
                        &&body.Prepared.Snapshot.Birth.Suppliers.Rank==values.Rank&&body.Prepared.Snapshot.HardMastery==10
                        &&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256&&SkillHealth.Balance256(target)==target.HitsMax*256&&SkillHealth.Balance256(adjacent)==adjacent.HitsMax*256,"Wire summon/automatic combat/captured gear rank/one debit mismatch");
                    int other=SkillHealth.Balance256(adjacent),percent=body.Prepared.Links.Marks.Single(m=>m.Target==target.Serial.Value).Percent;
                    SkillCombat.FireImpact(p,target,25600);Require(other-SkillHealth.Balance256(adjacent)==25600*percent/100,"Wire automatic Defiler group did not share actual damage");
                    Log.Information("{Label}: automatic original Defiler linked/shared through saved source host;rank{Rank}/cost{Cost}",label,values.Rank,values.Mana256);
                    finished=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,point.X,point.Y,point.Z);Inspect();}));
    }
}
