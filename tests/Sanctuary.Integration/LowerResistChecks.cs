// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class LowerResistChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(LowerResistChecks));
    private sealed record Saved(uint Owner,uint Target,uint Legacy,uint Partial,NecromancerCurse Curse,NecromancerCurse Old,NecromancerCurse PartialCurse,int Mana,int Map,Point3D Point,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"lower-resist-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("lower-resist-check",Check);Register("lower-resist-reload-check",Reload);
        foreach(bool quick in new[]{false,true})
        {bool captured=quick;string label=quick?"lower-resist-quick":"lower-resist-right";Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,captured)));}
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"LowerResist reference, learned casting and saved state",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Owner(int rank=1)
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureLowerResist";
        void Train(string id,int wanted)
        {
            TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var talent);
            foreach(string prior in talent.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(prior,1);}}
            while(ClassCreation.Profiles.GetRank(p.Serial,id)<wanted){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
        }
        Train("necromancer.lowerresist",rank);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.LowerResist)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.LowerResist),"Lower Resist right/quick bindings");
        FixtureSpace.Corridor(p,6600,24,3500);return p;
    }
    private static GraveyardSkeleton Enemy(PlayerMobile p,int x=1,int y=0)
    {var t=PoisonDaggerChecks.Target(p);t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;}
    private static void Ready(PlayerMobile p){PoisonDaggerChecks.Ready(p);ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static readonly int[][] Bases=[[0,99,100,110],[100,110,0,99],[99,100,110,0],[110,0,99,100],[-100,200,120,0],[0,0,0,0],[100,100,100,100],[200,200,200,200]];
    private static int Amount(int rank)=>new SkillValues(SkillMechanics.For(CombatAbility.LowerResist),_=>rank,_=>0,99).Diminishing(5,6);
    private static LowerResistDeltas Previous(int pattern,int rank)
    {int full=-Amount(rank);int Delta(int i)=>Bases[pattern][i]>=100?full:full/5;return new(Delta(0),Delta(1),Delta(2),Delta(3));}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"lower-resist-source-cases.json")));
        DateTime at=DateTime.UnixEpoch.AddSeconds(4);int count=0;
        foreach(var r in doc.RootElement.EnumerateArray())
        {
            int type=r[0].GetInt32(),hire=r[1].GetInt32(),pattern=r[2].GetInt32(),oldRank=r[3].GetInt32(),rank=r[4].GetInt32(),other=r[5].GetInt32(),resistance=r[6].GetInt32(),attract=r[7].GetInt32();
            NecromancerCurse old=oldRank==0?null:new(other==1?CombatAbility.Weaken:CombatAbility.LowerResist,999,oldRank,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0,ElementDeltas:other==1?null:Previous(pattern,oldRank));
            bool duration=SourceCurseDuration.Reduce(500+50*(rank-1),true,resistance,out int frames);var bases=Bases[pattern];
            var elements=NecromancerCurseCasts.ElementPenalties(-Amount(rank),bases[0],bases[1],bases[2],bases[3],type==1&&hire==0);
            var next=duration&&attract==0?NecromancerCurseCasts.Replacement(old,CombatAbility.LowerResist,7,rank,null,at.AddMilliseconds(frames*40L),at,0,0,elements):null;
            var current=next??old;var delta=current?.ElementDeltas;
            Require((next!=null)==(r[8].GetInt32()!=0)&&(current?.Rank??0)==r[9].GetInt32()&&(current?.Source??0)==r[10].GetUInt32()
                &&(current?.UntilUtc??DateTime.UnixEpoch)==DateTime.UnixEpoch.AddMilliseconds(r[11].GetInt32()*40L)
                &&(delta?.Fire??0)==r[12].GetInt32()&&(delta?.Lightning??0)==r[13].GetInt32()&&(delta?.Cold??0)==r[14].GetInt32()&&(delta?.Poison??0)==r[15].GetInt32(),"Four-element source receiver/factory differential");count++;
        }
        Require(count==12288,"Incomplete Lower Resist fixture");
        foreach(int rank in new[]{1,2,3,8,20,21,255})
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.LowerResist),_=>rank,_=>0,99);Require(v.Mana256==5632&&v.Eval("aurarangecalc")==7+rank-1&&v.Eval("auralencalc")==500+50*(rank-1),"Pinned Lower Resist rank values");}
        Require(Amount(1)==31&&Amount(8)==52&&Amount(20)==62&&Amount(21)==63,"Retained shared curve changed");
        var sample=new NecromancerCurse(CombatAbility.LowerResist,1,1,at,0,0);
        Require(NecromancerEffects.ValidElementDeltas(sample,new(-31,-6,-31,-6))&&NecromancerEffects.ValidElementDeltas(sample,new(null,-31,-6,-31))
            &&!NecromancerEffects.ValidElementDeltas(sample,new(-32,-6,-31,-6))&&!NecromancerEffects.ValidElementDeltas(sample with{Ability=CombatAbility.Weaken},new(-31,-6,-31,-6)),"Saved elemental domain");
    }
    private static void Hit(PlayerMobile p,Mobile t,string element)
    {
        // SkillCombat.Hit is deliberately a direct physical/fire/cold/lightning/magic helper;
        // exercise poison through the actual native elemental mitigation hook.
        if(element=="pois"){AOS.Damage(t,p,100,false,0,0,0,100,0,spellDamage:true);}
        else{SkillCombat.Hit(p,t,100,element,recover:false);}
    }
    private static void Resist(BaseCreature t,int value)
    {foreach(var type in new[]{ResistanceType.Fire,ResistanceType.Energy,ResistanceType.Cold,ResistanceType.Poison}){t.SetResistance(type,value);}}
    private static void Mechanics(PlayerMobile p,GraveyardSkeleton t)
    {
        foreach(int basis in new[]{-100,0,99,100,105,106,110,120,200})
        {
            NecromancerEffects.Profiles.Clear(t);Resist(t,basis);
            Require(NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,1),"Lower Resist application");int delta=basis>=100?-6:-31;
            Require(NecromancerEffects.Curse(t).ElementDeltas==new LowerResistDeltas(delta,delta,delta,delta),"Independent captured immune penalties");
            foreach(string element in new[]{"fire","ltng","cold","pois"})
            {
                int before=SkillHealth.Balance256(t);Hit(p,t,element);
                Require(before-SkillHealth.Balance256(t)==Math.Clamp(100-basis-delta,0,200)*256,"Element immunity break boundary: "+element);
                Require(NamedElements.Resistance(p,t,element,basis,20)==Math.Clamp(basis+delta-(basis+delta<100?20:0),-100,200),"Ordinary pierce immunity order");
            }
            Require(NamedElements.Resistance(p,t,"physical",0)==0&&NamedElements.Resistance(p,t,"mag",0)==0&&NecromancerEffects.DamageStatPercent(t)==0&&NecromancerEffects.Slow(t)==0,"Lower Resist changed unrelated stats");
        }
        NecromancerEffects.Profiles.Clear(t);t.SetResistance(ResistanceType.Fire,100);t.SetResistance(ResistanceType.Energy,99);t.SetResistance(ResistanceType.Cold,110);t.SetResistance(ResistanceType.Poison,0);
        Require(NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,1)&&NecromancerEffects.Curse(t).ElementDeltas==new LowerResistDeltas(-6,-31,-6,-31),"Mixed channel immunity");
        Resist(t,0);Require(NecromancerEffects.Resistance(t,"fire",0)==-6,"Captured fire was reclassified or divided twice");
        var other=Owner();
        try
        {
            var first=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddMinutes(1)};NecromancerEffects.Profiles.Store(t,new(Curse:first));
            Require(NecromancerEffects.Apply(other,t,CombatAbility.LowerResist,1),"Equal Lower Resist");var equal=NecromancerEffects.Curse(t);
            Require(equal.Source==p.Serial.Value&&equal.ElementDeltas==new LowerResistDeltas(-6,-31,-31,-31)&&equal.UntilUtc<first.UntilUtc,"Equal refresh must retain fire/caster and rewrite secondary elements");
            Resist(t,100);Require(NecromancerEffects.Apply(other,t,CombatAbility.LowerResist,1)&&NecromancerEffects.Curse(t).ElementDeltas==new LowerResistDeltas(-6,-6,-6,-6),"Reverse secondary refresh");
            Require(NecromancerEffects.Apply(other,t,CombatAbility.LowerResist,3)&&NecromancerEffects.Curse(t).Source==other.Serial.Value,"Higher rank did not replace caster");
            first=NecromancerEffects.Curse(t);Require(!NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,1)&&NecromancerEffects.Curse(t)==first,"Weaker rank rewrote state");
            other.Delete();Require(NecromancerEffects.Curse(t)==first,"Caster deletion erased Lower Resist");
        }
        finally{other.Delete();}
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Weaken,1)&&NecromancerEffects.Curse(t).ElementDeltas==null,"Different curse retained elemental payload");
        Require(NecromancerEffects.Apply(p,t,CombatAbility.Attract,1)&&!NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,20),"Lower Resist overwrote Attract");NecromancerEffects.Profiles.Clear(t);
        var possessed=new EncounterCreature{Frozen=true,CantWalk=true};possessed.Assign(EncounterProfiles.Profile("crypt-sentinel-60"),60,MonsterDifficulty.Normal,EncounterGrade.Champion,ChampionKind.Possessed,[],EliteAura.None,null,new Random(173));possessed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);possessed.AIObject.AITimer.Stop();
        try{Require(!NecromancerEffects.Apply(p,possessed,CombatAbility.LowerResist,1),"Possessed accepted Lower Resist");}finally{possessed.Delete();}
        // Apply a real class Conviction lease, then independently classify Lower Resist from the same original pools.
        var paladin=ClassSkillChecks.Player(DiabloClass.Paladin);paladin.MoveToWorld(new(p.X+2,p.Y+1,p.Z),p.Map);var leases=PaladinConviction.Snapshot();
        try
        {
            t.SetResistance(ResistanceType.Fire,100);t.SetResistance(ResistanceType.Energy,130);t.SetResistance(ResistanceType.Cold,80);t.SetResistance(ResistanceType.Poison,100);
            Require(PaladinConviction.Apply(paladin,t,SkillMechanics.Values(paladin,CombatAbility.Conviction,20),TimedEffects.Now.AddMinutes(1)),"Conviction fixture");
            Require(NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,20)&&NecromancerEffects.Curse(t).ElementDeltas==new LowerResistDeltas(-12,-12,-62,-12),"Conviction changed Lower Resist immunity classification");
            Require(NamedElements.Resistance(p,t,"fire",100)==63&&NamedElements.Resistance(p,t,"cold",80)==-100&&NamedElements.Resistance(p,t,"ltng",130,7)==86&&NamedElements.Resistance(p,t,"pois",100)==88,"Independent reductions or post-break pierce order");
            int before=SkillHealth.Balance256(t);AOS.Damage(t,p,100,false,0,100,0,0,0,spellDamage:true);Require(before-SkillHealth.Balance256(t)==37*256,"Combined native fire mitigation");
        }
        finally{PaladinConviction.Restore(leases);paladin.Delete();NecromancerEffects.Profiles.Clear(t);Resist(t,0);}
    }
    private static void Casting(PlayerMobile p)
    {
        GraveyardSkeleton near=Enemy(p,8),edge=Enemy(p,11,1),far=Enemy(p,11,2);var center=new Point3D(p.X+8,p.Y,p.Z);
        try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(NecromancerCombat.Release(p,CombatAbility.LowerResist,Serial.Zero,center),"Ground area release");
            Require(NecromancerEffects.ManaBalance256(p)==mana-5632&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)!=null&&NecromancerEffects.Curse(far)==null,"Area center,radius or once-only payment");
            foreach(var t in new[]{near,edge,far}){NecromancerEffects.Profiles.Clear(t);}
            Ready(p);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.LowerResist,near,near.Location,Core.TickCount),"Targeted windup");
            Require(NecromancerEffects.ManaBalance256(p)==mana&&NecromancerEffects.Curse(near)==null,"Curse applied/paid during windup");
            near.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);
            Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.Curse(near)!=null&&NecromancerEffects.Curse(edge)==null,"Release did not follow selected target position");
            Ready(p);NecromancerEffects.Profiles.Clear(near);mana=NecromancerEffects.ManaBalance256(p);
            Require(ClassCombat.Perform(p,CombatAbility.LowerResist,null,center,Core.TickCount),"Cancelled windup");long effect=ClassCombat.For(p).EffectAt;
            var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Movement cancellation spent mana");p.MoveToWorld(origin,p.Map);
            Ready(p);NecromancerEffects.SetMana256(p,5631);Require(!NecromancerCombat.Begin(p,CombatAbility.LowerResist,Serial.Zero,center,Core.TickCount),"Insufficient mana began a cast");
            near.Delete();edge.Delete();far.Delete();Ready(p);mana=NecromancerEffects.ManaBalance256(p);Require(NecromancerCombat.Release(p,CombatAbility.LowerResist,Serial.Zero,new(p.X+16,p.Y,p.Z))&&NecromancerEffects.ManaBalance256(p)==mana-5632,"Empty area did not pay once");
        }
        finally{ClassCombat.Stop(p);near.Delete();edge.Delete();far.Delete();}
    }
    private static void LineOfSight()
    {
        var p=Owner(3);var t=Enemy(p,10);var center=new Point3D(p.X+8,p.Y,p.Z);var wall=new Static(0x80);
        try
        {
            wall.MoveToWorld(new(p.X+9,p.Y,p.Z),p.Map);
            Require(!p.Map.LineOfSight(new Point3D(center.X,center.Y,center.Z+12),new Point3D(t.X,t.Y,t.Z+12)),"Recipient LOS obstruction fixture");
            Ready(p);Require(NecromancerCombat.Release(p,CombatAbility.LowerResist,Serial.Zero,center)&&NecromancerEffects.Curse(t)!=null,"Filter3 gained an unrequested recipient LOS gate");
        }
        finally{wall.Delete();t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Differential();var p=Owner();var t=Enemy(p);Mechanics(p,t);Casting(p);LineOfSight();
        while(ClassCreation.Profiles.GetRank(p.Serial,"necromancer.lowerresist")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"necromancer.lowerresist",out _),"Train rank20");}
        PoisonDaggerChecks.Named(p,"Wormskull");Require(SkillMechanics.Rank(p,CombatAbility.LowerResist)==21,"Real effective item rank");
        NecromancerEffects.Profiles.Clear(t);Require(NecromancerEffects.Apply(p,t,CombatAbility.LowerResist,21),"Effective-rank curse");
        var curse=NecromancerEffects.Curse(t) with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(t,new(Curse:curse));
        var legacy=Enemy(p,3);Resist(legacy,100);var old=new NecromancerCurse(CombatAbility.LowerResist,p.Serial.Value,1,TimedEffects.Now.AddDays(1),p.X,p.Y);
        Require(!JsonSerializer.Serialize(old).Contains("ElementDeltas"),"Legacy nullable field was manufactured");NecromancerEffects.Profiles.Store(legacy,new(Curse:old));
        var partial=Enemy(p,5);Resist(partial,100);NecromancerEffects.Profiles.Store(partial,new(Curse:old));
        Require(NecromancerEffects.Apply(p,partial,CombatAbility.LowerResist,1),"Equal legacy refresh");
        var partialCurse=NecromancerEffects.Curse(partial) with{UntilUtc=TimedEffects.Now.AddDays(1)};
        Require(partialCurse.ElementDeltas==new LowerResistDeltas(null,-6,-6,-6),"Legacy equal recast guessed fire or missed secondary writes");NecromancerEffects.Profiles.Store(partial,new(Curse:partialCurse));
        Ready(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,legacy.Serial.Value,partial.Serial.Value,curse,old,partialCurse,NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location,0)));
        Log.Information("Lower Resist:12288four-element comparisons,native damage,independent immunity/Conviction/pierce,primary retention/secondary refresh,area/payment/LOS/tracking/cancel and saved full/legacy/partial controls pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(BaseCreature)World.FindMobile((Serial)s.Target);var legacy=(BaseCreature)World.FindMobile((Serial)s.Legacy);var partial=(BaseCreature)World.FindMobile((Serial)s.Partial);
        Require(NecromancerEffects.Curse(t)==s.Curse&&NecromancerEffects.Curse(legacy)==s.Old&&NecromancerEffects.Curse(partial)==s.PartialCurse&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Reload altered captured/legacy/partial state,mana or original deadline");
        Require(NecromancerEffects.Curse(legacy).ElementDeltas==null&&NecromancerEffects.Resistance(legacy,"fire",100)==94,"Legacy migration guessed a captured penalty");
        Require(NecromancerEffects.Curse(partial).ElementDeltas.Fire==null&&NecromancerEffects.Resistance(partial,"fire",0)==-31&&NecromancerEffects.Resistance(partial,"ltng",0)==-6,"Partial legacy state lost unknown primary or captured secondary");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);Require(SkillMechanics.Rank(p,CombatAbility.LowerResist)==21&&SkillBindings.Instance.Get(p).Right==CombatAbility.LowerResist,"Reload changed item rank or binding");
        // GraveyardUndead.AfterLoad deliberately restores its species resistance profile.
        // Preserve the captured curse across that real base-pool change rather than
        // pretending the fixture's temporary zero resistances were serialized.
        var profile=((GraveyardSkeleton)t).CombatProfile;
        Require(t.FireResistance==profile.Fire&&t.EnergyResistance==profile.Lightning&&t.ColdResistance==profile.Cold&&t.PoisonResistance==profile.Poison,"Native species profile restoration");
        foreach(var (element,basis) in new[]{("fire",profile.Fire),("ltng",profile.Lightning),("cold",profile.Cold),("pois",profile.Poison)})
        {int before=SkillHealth.Balance256(t);Hit(p,t,element);int expected=Math.Clamp(100-basis+63,0,200)*256;Require(before-SkillHealth.Balance256(t)==expected,"Restored Lower Resist element: "+element+" base="+basis+" expected="+expected+" actual="+(before-SkillHealth.Balance256(t)));}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
    private static void Input(NetState network,bool quick)
    {
        var p=Owner();var t=Enemy(p,6);Ready(p);int mana=NecromancerEffects.ManaBalance256(p);string label=quick?"lower-resist-quick":"lower-resist-right";
        network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var protocol=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);protocol.SetValue(network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        bool began=false;long deadline=Core.TickCount+30000;
        void Fail(Exception e){t.Delete();p.Delete();Log.Error(e,"{Label}-result: failed",label);}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"LowerResist input timeout");var session=ClassCombat.For(p);
                if(session.PendingAbility==CombatAbility.LowerResist&&!began){Require(NecromancerEffects.Curse(t)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Input paid/applied during windup");began=true;Log.Information("{Label}: windup",label);}
                if(NecromancerEffects.Curse(t) is not{} curse){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require(began&&curse.ElementDeltas==new LowerResistDeltas(-31,-31,-31,-31)&&curse.Rank==1&&NecromancerEffects.ManaBalance256(p)==mana-5632,"Real input capture/payment");ClassCombat.Stop(p);
                Require((curse.UntilUtc-TimedEffects.Now).TotalMilliseconds is >19900 and <=20000,"Rank1duration is not500frames");
                Timer.StartTimer(curse.UntilUtc-TimedEffects.Now+TimeSpan.FromMilliseconds(100),()=>
                {try{Require(NecromancerEffects.Curse(t)==null,"Natural rank1curse did not expire");int before=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,10,"fire",recover:false);Require(before-SkillHealth.Balance256(t)==10*256,"Expired curse still affected damage");t.Delete();p.Delete();Log.Information("{Label}-result: passed",label);}catch(Exception e){Fail(e);}});
            }
            catch(Exception e){Fail(e);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.LowerResist,(int)DiabloClass.Necromancer,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
