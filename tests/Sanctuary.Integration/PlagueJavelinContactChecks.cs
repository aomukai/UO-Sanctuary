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

public static class PlagueJavelinContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlagueJavelinContactChecks));
    private const CombatAbility Ability=CombatAbility.PlagueJavelin;
    private sealed class Minimum:Random{public override int Next(int n)=>0;}
    private sealed class NoWeaponEffects:Random{public override int Next(int n)=>throw new InvalidOperationException("Source0 plague cloud requested weapon effects");}
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Weapon,uint[] Items,Point3D Point,string Gear,string Profile,WeaponFlight Flight,PlagueCloud Cloud,DateTime Cooldown,int Life,int PrimaryLife,int SecondaryLife,int Mana,int Quantity,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"plague-javelin-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static WeaponFlight[] Flights(PlayerMobile p)=>MagicArrowContactChecks.Flights(p);
    private static PlagueCloud[] Clouds(PlayerMobile p)=>PlagueJavelins.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PlayerProcSave Own(PlayerMobile p,Item w)=>MagicArrowContactChecks.Own(p,w);
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    private static DateTime Pmh(Mobile t)=>State(t)?.HealBlockedUntilUtc??default;
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"plague-javelin-contact-reload-check":"plague-javelin-contact-check";ConsoleInputHandler.RegisterCommand(name,"Plague Javelin weapon versus source0 cloud phases and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="plague-javelin-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Plague Javelin weapon and poison-only cloud delivery",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,out BaseWeapon w,out Item[] gear)
    {var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(Ability).TalentId);w=(BaseWeapon)PlayerItemProcWorldChecks.Carrier(p,new SoulGlaive());gear=new[]{DruidSetupItemChecks.Named(p,"Venom Grip"),DruidSetupItemChecks.Named(p,"Blackhorn's Face")};ClassGear.Refresh(p);Require(AmazonJavelins.WeaponAllowed(p)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Plague Javelin weapon/bindings");return p;}
    private static BaseCreature Target(PlayerMobile p,int dx=3)
    {var t=new ExplosiveArrowContactChecks.AreaRecipient();t.SetHits(60000);t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;}
    private static void Delay(PlayerMobile p,DateTime? until=null)
    {var s=FireGround.Snapshot();FireGround.Restore(s with{Cooldowns=s.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=Ability).Concat(until is{} u?new[]{new FireCooldown(p.Serial.Value,Ability,u)}:[]).ToArray()});}
    private static void Ready(PlayerMobile p){MagicArrowContactChecks.Ready(p);Delay(p);}
    private static void Clean(PlayerMobile p,params BaseCreature[] targets){MagicArrowContactChecks.Clean(p,targets);PlagueJavelins.Clear(p);}
    private static void Keep(PlayerMobile p,WeaponFlight[] flights,PlagueCloud[] clouds)
    {var s=WeaponMissiles.Snapshot();WeaponMissiles.Restore(s with{Flights=s.Flights.Where(x=>x.Owner!=p.Serial.Value).Concat(flights).ToArray(),PlagueClouds=s.PlagueClouds.Where(x=>x.Owner!=p.Serial.Value).Concat(clouds).ToArray()});}
    private static WeaponFlight Launch(PlayerMobile p,BaseCreature t,BaseWeapon w)
    {
        int mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,t,t.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(w)==quantity,"Plague Javelin start phase");ClassCombat.ReleaseCast(p);Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Plague Javelin release");var f=Flights(p).Single();Require(BowMissileContact.Handles(f)&&BowMissileContact.AccuracyPMH(f)&&f.Damage.LightningJavelinPercent==null&&f.Damage.Elements.PoisonMin==262&&f.Damage.Elements.PoisonFrames==175&&NamedAmmunition.Remaining(w)==quantity-1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Plague Javelin source128 packet or repeated payment");return f;
    }
    // Isolate weapon contact while preserving an independently saved source0 cloud.
    private static int ParentContact(PlayerMobile p,long keep=0)
    {
        int born=0;
        for(int i=0;i<70&&Flights(p).Length>0;i++)
        {WeaponMissiles.Tick(Flights(p).Min(x=>x.NextUtc),onlyOwner:p.Serial.Value);Require(Clouds(p).Where(x=>x.Id!=keep).All(x=>x.Frame==0&&x.Damage.Elements.PoisonMin==109&&x.Damage.Min256==0),"Burst inherited weapon poison");born+=Clouds(p).Count(x=>x.Id!=keep);Keep(p,Flights(p),Clouds(p).Where(x=>x.Id==keep).ToArray());}
        Require(Flights(p).Length==0,"Plague Javelin parent did not terminate");return born;
    }
    private static void ParentCases(PlayerMobile p,BaseWeapon w)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();var expected=new Queue<(BaseCreature Target,int Life)>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&v.Packet.HitFlags==32&&v.Packet.HitClass==6&&v.Packet.Poison256 is 0 or 262&&Clouds(p).Length>0&&expected.Count>0,"Plague parent event/weapon flags");var e=expected.Dequeue();Require(SkillHealth.Balance256(e.Target)==e.Life&&Pmh(e.Target)>TimedEffects.Now,"Plague parent proc/HP/PMH order");visits.Add(v);}};
            foreach(string mode in new[]{"hit","miss","block","immune","removed","physical","lethal","armor","pierce","empty"})
            {
                Ready(p);var launchRandom=AmazonBow.LaunchRandom;var t=Target(p);var second=mode=="pierce"?Target(p,6):null;Item belt=null;if(mode=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(mode=="physical"){t.SetResistance(ResistanceType.Physical,0);}if(mode=="lethal"){SkillHealth.SetBalance256(t,1);}if(mode=="pierce"){belt=DruidSetupItemChecks.Named(p,"Razortail");AmazonBow.LaunchRandom=new Minimum();}WeaponMissiles.HitRoll=()=>mode=="miss"?1:0;SourceBlocking.BlockRoll=n=>mode=="block"?0:n-1;int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),cost=SkillMechanics.Values(p,Ability).Mana256;visits.Clear();expected.Clear();NpcWorldAllocation.RequireStream();var world=NpcWorldAllocation.Capture();var profile=Own(p,w);
                try
                {
                    var f=Launch(p,t,w);if(mode=="armor"){f=f with{Damage=f.Damage with{ArmorDelta=-1}};Keep(p,new[]{f},[]);}if(mode=="removed"){p.AddToBackpack(w);ClassGear.Refresh(p);}if(mode=="empty"){t.Internalize();}int direct=NamedElements.To256(SkillCombat.MitigatePrepared(p,t,f.Damage.Roll(t,_=>0)with{Poison=0}).Total);bool hit=mode is not("miss" or "block" or "empty"),proc=hit&&mode!="removed";if(proc){expected.Enqueue((t,before));if(second!=null){expected.Enqueue((second,SkillHealth.Balance256(second)));}}int born=ParentContact(p);Require(born==(mode=="pierce"?69:23),"Actual hit02 burst count: "+mode);int count=proc?(second!=null?2:1):0;
                    Require(visits.Count==count&&expected.Count==0&&visits.All(v=>v.Outcomes.Single()is{CastAttempts:1,CastResults:1})&&before-SkillHealth.Balance256(t)==(hit?Math.Min(before,direct+(mode=="immune"?0:262)):0)&&NativeMonsterReturn.Session(t).Packets==(hit?1:0),"Plague parent HP/block/provider: "+mode);Require((Pmh(t)!=default)==(mode is not("miss" or "lethal" or "empty"))&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Plague parent PMH/resources");if(mode=="armor"){Require(TimedEffects.DefenseLoss(t)==1,"Plague captured armor");}if(second!=null){Require(second.HitsMax*256-SkillHealth.Balance256(second)==262,"Piercing plague parent");}
                    ulong seed=world.Seed;var actor=profile.Owners.SingleOrDefault();ulong actorSeed=actor?.Seed??0;for(int i=0;i<count;i++){if(i==0&&actor==null){actorSeed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref seed)));}SourceRandom.Roll(ref actorSeed);SourceRandom.Roll(ref seed);}if(count>0){Require(Own(p,w).Owners.Single().Seed==actorSeed,"Plague parent actor draws");}Require(NpcWorldAllocation.Capture().Seed==seed,"Plague parent world draws");
                }
                catch(Exception e){throw new InvalidOperationException("Plague parent "+mode,e);}
                finally{Clean(p,t,second);belt?.Delete();AmazonBow.LaunchRandom=launchRandom;if(w.Parent!=p){Require(p.EquipItem(w),"Restore parent weapon");}ClassGear.Refresh(p);SourceBlocking.BlockRoll=n=>n-1;WeaponMissiles.HitRoll=()=>0;}
            }
        }
        finally{PlayerItemProcWorld.Observed=old;}
    }
    private static PlagueCloud Cloud(PlayerMobile p,BaseWeapon w,BaseCreature t)
    {var f=Launch(p,t,w);Keep(p,[],[]);PlagueJavelins.Burst(p,f,t,TimedEffects.Now);var all=Clouds(p);Require(all.Length==23&&all.All(x=>x.Frames==87&&x.Frame==0&&x.X==t.X&&x.Y==t.Y),"Actual hit02 ring birth");var c=all[2];Require(c.Vx==.0234375&&c.Vy==0,"Original ring motion");Keep(p,[],new[]{c});Require(c.Damage.Min256==0&&c.Damage.Elements.PoisonMin==109&&c.Damage.Elements.PoisonFrames==75,"Source0 cloud packet");return c;}
    private static void CloudCases(PlayerMobile p,BaseWeapon w)
    {
        using var candidate=PlayerItemProcWorld.Candidate(p);
        foreach(string mode in new[]{"hit","immune","resisted","weaker","lethal","cure","alternate","removed"})
        {
            Ready(p);var a=Target(p);var b=mode=="alternate"?Target(p,3):null;var effects=WeaponMissiles.ItemEffects;var old=PlayerItemProcWorld.Observed;
            try
            {
                var c=Cloud(p,w,a);if(mode=="immune"){a.SetResistance(ResistanceType.Poison,100);}if(mode=="resisted"){a.SetResistance(ResistanceType.Poison,50);}if(mode=="lethal"){SkillHealth.SetBalance256(a,1);}if(mode=="weaker"){Require(TimedEffects.PoisonFrames(p,a,218,75,c.NextUtc),"Existing stronger poison");}if(mode=="removed"){p.AddToBackpack(w);ClassGear.Refresh(p);}int before=SkillHealth.Balance256(a),mana=NecromancerEffects.ManaBalance256(p),quantity=NamedAmmunition.Remaining(w),visits=0;var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits++;}};WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Plague cloud requested physical shield");WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Plague cloud requested accuracy");
                PlagueJavelins.Tick(c.NextUtc);
                if(b==null)
                {
                    int rate=mode=="immune"?0:mode=="resisted"?54:109;Require(before-SkillHealth.Balance256(a)==Math.Min(before,rate)&&NativeMonsterReturn.Session(a).Packets==1,"Cloud initial-rate HP: "+mode);
                    if(a.Alive){Require(State(a)?.PoisonClock?.Rate256==(mode=="weaker"?218:rate)||rate==0&&State(a)?.PoisonClock==null,"Cloud strongest poison: "+mode);TimedEffects.Settle(a,c.NextUtc.AddMilliseconds(39));Require(before-SkillHealth.Balance256(a)==rate,"Poison tick before40ms");if(mode=="cure"){Require(TimedEffects.CleansePoison(a,0,c.NextUtc.AddMilliseconds(39)),"Cure cloud poison");}TimedEffects.Settle(a,c.NextUtc.AddMilliseconds(40));int tick=mode is "cure" or "immune"?0:mode=="weaker"?218:rate;Require(before-SkillHealth.Balance256(a)==rate+tick,"Cloud later poison tick/cure: "+mode);}
                }
                else
                {
                    var first=Clouds(p).Single().LastCollision;var hit=first==a.Serial.Value?a:b;var other=hit==a?b:a;Require(NativeMonsterReturn.Session(hit).Packets==1&&NativeMonsterReturn.Session(other).Packets==0,"Cloud first collider");PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(40));PlagueJavelins.Tick(c.NextUtc.AddMilliseconds(80));Require(Clouds(p).Single().LastCollision==first&&NativeMonsterReturn.Session(hit).Packets==2&&NativeMonsterReturn.Session(other).Packets==1&&State(hit)?.PoisonClock?.Rate256==109&&hit.HitsMax*256-SkillHealth.Balance256(hit)==436&&other.HitsMax*256-SkillHealth.Balance256(other)==109,"Cloud alternating LastCollide/initial-rate/old-poison catchup");
                }
                Require(visits==0&&Pmh(a)==default&&TimedEffects.Slow(a,TimedEffects.Now)==0&&TimedEffects.DefenseLoss(a)==0&&State(a)?.SpellPoison==null&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(w)==quantity,"Cloud weapon event/RNG/potion/resource leak: "+mode);
            }
            catch(Exception e){throw new InvalidOperationException("Plague cloud "+mode,e);}
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;WeaponMissiles.HitRoll=()=>0;Clean(p,a,b);if(w.Parent!=p){Require(p.EquipItem(w),"Restore cloud weapon");}ClassGear.Refresh(p);}
        }
    }
    private static void Check(bool reload)
    {
        using var rolls=new MagicArrowContactChecks.Rolls();PlayerMobile p;BaseWeapon w;Item[] gear;int generation=0;
        if(!reload){p=Owner(2800,out w,out gear);ParentCases(p,w);CloudCases(p,w);}
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);w=(BaseWeapon)World.FindItem((Serial)s.Weapon);gear=s.Items.Select(id=>World.FindItem((Serial)id)).ToArray();var t=(BaseCreature)World.FindMobile((Serial)s.Primary);var b=(BaseCreature)World.FindMobile((Serial)s.Secondary);
            Require(generation<=2&&w.Parent==p&&gear.All(x=>x.Parent==p)&&Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State))==s.Gear&&Json(Own(p,w))==s.Profile&&Json(Flights(p).Single())==Json(s.Flight)&&Json(Clouds(p).Single())==Json(s.Cloud)&&FireGround.Snapshot().Cooldowns.Any(x=>x.Owner==s.Owner&&x.Ability==Ability&&x.UntilUtc==s.Cooldown)&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(t)==s.PrimaryLife&&SkillHealth.Balance256(b)==s.SecondaryLife&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity,"Saved Plague Javelin original parent/cloud/gear/proc/wound/resources/delay");p.MoveToWorld(s.Point,Map.Trammel);t.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);foreach(var actor in new[]{t,b}){actor.CantWalk=actor.Frozen=true;actor.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){actor.SetResistance(r,100);}actor.SetResistance(ResistanceType.Poison,0);}int before=SkillHealth.Balance256(t),secondary=SkillHealth.Balance256(b),count=0;var old=PlayerItemProcWorld.Observed;var effects=WeaponMissiles.ItemEffects;PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){Require(v.Phase=="domissiledamage"&&SkillHealth.Balance256(t)==before,"Restored parent phase");count++;}};
            try
            {
                using var candidate=PlayerItemProcWorld.Candidate(p);Keep(p,new[]{s.Flight with{NextUtc=TimedEffects.Now}},new[]{s.Cloud});int born=ParentContact(p,s.Cloud.Id);Require(born==23&&count==1&&before-SkillHealth.Balance256(t)==262&&SkillHealth.Balance256(b)==secondary&&Json(Clouds(p).Single())==Json(s.Cloud),"Restored parent or independent cloud changed");var other=Target(p,12);var profile=Json(Own(p,w));var world=NpcWorldAllocation.Capture();WeaponMissiles.ItemEffects=new NoWeaponEffects();SourceBlocking.BlockRoll=_=>throw new InvalidOperationException("Restored cloud requested shield");WeaponMissiles.HitRoll=()=>throw new InvalidOperationException("Restored cloud requested accuracy");var at=TimedEffects.Now;Keep(p,[],new[]{s.Cloud with{NextUtc=at}});PlagueJavelins.Tick(at);Require(SkillHealth.Balance256(b)==secondary&&Clouds(p).Single().LastCollision==b.Serial.Value,"Saved LastCollide forgotten");b.MoveToWorld(new(p.X+14,p.Y,p.Z),p.Map);other.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);PlagueJavelins.Tick(at.AddMilliseconds(40));other.MoveToWorld(new(p.X+13,p.Y,p.Z),p.Map);b.MoveToWorld(new(p.X+6,p.Y,p.Z),p.Map);PlagueJavelins.Tick(at.AddMilliseconds(80));TimedEffects.Settle(b,at.AddMilliseconds(120));Require(count==1&&secondary-SkillHealth.Balance256(b)==218&&NativeMonsterReturn.Session(b).Packets==1&&Json(Own(p,w))==profile&&NpcWorldAllocation.Capture()==world&&Pmh(b)==default&&State(b)?.SpellPoison==null&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NamedAmmunition.Remaining(w)==s.Quantity,"Restored cloud damage/event/RNG/resource replay");other.Delete();
            }
            finally{PlayerItemProcWorld.Observed=old;WeaponMissiles.ItemEffects=effects;SourceBlocking.BlockRoll=n=>n-1;WeaponMissiles.HitRoll=()=>0;Clean(p,t,b);}
        }
        Ready(p);var primary=Target(p);var secondaryTarget=Target(p,6);secondaryTarget.SetResistance(ResistanceType.Poison,100);var heldCloud=Cloud(p,w,secondaryTarget);PlagueJavelins.Tick(heldCloud.NextUtc);heldCloud=Clouds(p).Single();Require(heldCloud.Frame==1&&heldCloud.LastCollision==secondaryTarget.Serial.Value,"Partial saved cloud");var until=TimedEffects.Now.Add(FixtureSpace.Retention);heldCloud=heldCloud with{NextUtc=until};Keep(p,[],new[]{heldCloud});Ready(p);var heldParent=Launch(p,primary,w);WeaponMissiles.Tick(heldParent.NextUtc,onlyOwner:p.Serial.Value);heldParent=Flights(p).Single()with{NextUtc=until};Keep(p,new[]{heldParent},new[]{heldCloud});Delay(p,until);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,primary.Serial.Value,secondaryTarget.Serial.Value,w.Serial.Value,gear.Select(x=>x.Serial.Value).ToArray(),point,Json(new[]{(Item)w}.Concat(gear).Select(ClassGear.State)),Json(Own(p,w)),heldParent,heldCloud,until,SkillHealth.Balance256(p),SkillHealth.Balance256(primary),SkillHealth.Balance256(secondaryTarget),NecromancerEffects.ManaBalance256(p),NamedAmmunition.Remaining(w),generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new MagicArrowContactChecks.Rolls();var p=Owner(3400,out var w,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"plague-javelin-contact-");var old=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256,quantity=NamedAmmunition.Remaining(w);long deadline=Core.TickCount+6000;bool began=false,sawFlight=false,sawCloud=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=old;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try{Require(Core.TickCount<deadline,"Plague Javelin input timeout");var session=ClassCombat.For(p);if(!began&&session.PendingAbility==Ability){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana&&NamedAmmunition.Remaining(w)==quantity,"Wire Plague start phase");Log.Information("{Label}: windup",label);}if(Flights(p).Length>0){sawFlight=true;}if(Clouds(p).Length>0){sawCloud=true;Require(Clouds(p).All(x=>x.Damage.Elements.PoisonMin==109&&x.Damage.Min256==0),"Wire cloud weapon inheritance");}if(!sawFlight||Flights(p).Length>0||Clouds(p).Length>0){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}Require(began&&sawCloud&&visits.Count==1&&visits[0].Phase=="domissiledamage"&&visits[0].Packet.Poison256==262&&visits[0].Outcomes.Single()is{CastAttempts:1,CastResults:1}&&Pmh(t)!=default&&State(t)?.PoisonClock?.Rate256==262&&State(t)?.SpellPoison==null&&NecromancerEffects.ManaBalance256(p)==mana-cost&&NamedAmmunition.Remaining(w)==quantity-1,"Wire plague parent/cloud/proc/resource split");Close();Log.Information("{Label}-result: passed",label);}catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
