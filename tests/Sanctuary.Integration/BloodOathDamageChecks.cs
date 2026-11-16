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

public static class BloodOathDamageChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodOathDamageChecks));
    private static bool Creating;
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,uint Enemy,int Life,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"blood-oath-damage-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static void Rates(){var old=Mobile.HitsRegenRateHandler;Mobile.HitsRegenRateHandler=p=>Creating||p.Name=="BloodOathFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultHitsRate;}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    public static void Configure()
    {
        Rates();foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"blood-oath-damage-reload-check":"blood-oath-damage-check";
            ConsoleInputHandler.RegisterCommand(name,"Approved Blood Oath settled damage,world eligibility and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static PlayerMobile Owner(int rank,int x,int y)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="BloodOathFixture";p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.bloodoath",rank);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,x,34,y);return p;
    }
    private static SkeletonGuardian Summon(PlayerMobile p,CombatAbility ability,int x)
    {var b=WarlockGoatmanWorld.Create(p,new(p.X+x,p.Y,p.Z),TimedEffects.Now,ability);WarlockGoatmanWorld.Admit(b);return b;}
    private static int[] Life(SkeletonGuardian[] pets)=>pets.Select(SkillHealth.Balance256).ToArray();
    private static void Fill(PlayerMobile p,SkeletonGuardian[] pets)
    {SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));foreach(var b in pets){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b));}}
    private static int OwnerLoss(PlayerMobile p,Action hit){int before=SkillHealth.Balance256(p);hit();return before-SkillHealth.Balance256(p);}
    private static void Scene(NetState network)
    {
        var p=Owner(20,6900,3300);var account=FixtureNetwork.Authenticate(network,p,"blood-oath-");bool kept=false;
        var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.SetHits(50000);enemy.Hits=enemy.HitsMax;enemy.MoveToWorld(new(p.X+20,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        try
        {
            Require(BloodOathDamageProposal.Approved&&WarlockCombat.Implemented(CombatAbility.BloodOath),"Approved learned reaction was not enabled");
            Require(BloodOathDamageProposal.Numbers(1)==(3,3)&&BloodOathDamageProposal.Numbers(20)==(25,3)&&BloodOathDamageProposal.Numbers(60)==(30,3),"Pinned effective-rank curve/explicit recipient limit");
            {
                Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1001))==1001,"No-demon reaction invented protection");
                var legacy=WarlockGoatmanBody.CreatePrepared(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now);
                try
                {
                    int life=SkillHealth.Balance256(legacy);var refused=BloodOathDamageProposal.ReducePrepared(enemy,p,1000,SkillDamageKind.Direct);
                    Require(refused is{Remaining256:1000,Fault:not null,Transfers.Length:0}&&SkillHealth.Balance256(legacy)==life,"Unsupported legacy recipient silently gained cover or took damage");
                }
                finally{legacy.Delete();}
            }
            SkeletonGuardian[] pets=[Summon(p,CombatAbility.SummonGoatman,2),Summon(p,CombatAbility.SummonTainted,4),Summon(p,CombatAbility.SummonDefiler,6)];
            Fill(p,pets);var before=Life(pets);
            Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1001))==751&&before.Zip(Life(pets),(a,b)=>a-b).SequenceEqual(new[]{84,83,83}),"Learned live player did not receive automatic reaction");

            {
                Fill(p,pets);before=Life(pets);int ownerEvents=0,petEvents=0;
                void Notify(SkillDamageContact c){if(c.Target==p){ownerEvents++;}if(pets.Contains(c.Target)){petEvents++;}}
                SkillDamageEvents.BeforeDirectDamage+=Notify;
                try{Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1001))==751,"Scalar final damage did not redirect one total25percent budget");}
                finally{SkillDamageEvents.BeforeDirectDamage-=Notify;}
                var loss=before.Zip(Life(pets),(a,b)=>a-b).ToArray();
                Require(loss.SequenceEqual(new[]{84,83,83})&&ownerEvents==1&&petEvents==0,"FP8 split/creation-order remainder or recursive/proc notification mismatch");
                // Class players use D2 named resistance pools,not arbitrary
                // UO resistance mods. Use an actual35res-all Goldskin.
                while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Goldskin fixture strength");}
                var armor=PoisonDaggerChecks.Named(p,"Goldskin");ClassGear.Refresh(p);Require(ClassGear.Bonus(p,"res-all")==35,"Goldskin resistance control");
                Fill(p,pets);before=Life(pets);int cold=OwnerLoss(p,()=>AOS.Damage(p,enemy,100,false,0,0,100,0,0,spellDamage:true));
                Require(cold==12480&&before.Zip(Life(pets),(a,b)=>a-b).Sum()==4160,$"Native post-resistance cold route mismatch:{cold}");p.AddToBackpack(armor);ClassGear.Refresh(p);
                Fill(p,pets);before=Life(pets);
                int bypass=OwnerLoss(p,()=>AOS.Damage(p,enemy,40,true,100,0,0,0,0));
                Require(bypass==7680&&before.Zip(Life(pets),(a,b)=>a-b).Sum()==2560,$"Native armor-ignore bridge mismatch:{bypass}");
                Fill(p,pets);NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(PsychicWard.CastPrepared(p),"Ward zero-contact control setup");before=Life(pets);
                Require(OwnerLoss(p,()=>AOS.Damage(p,enemy,5,false,100,0,0,0,0))==0&&Life(pets).SequenceEqual(before),"Fully shielded hit redirected damage");PsychicWard.Clear(p);
                foreach(var kind in new[]{SkillDamageKind.Periodic,SkillDamageKind.Return,SkillDamageKind.SelfCost})
                {Fill(p,pets);before=Life(pets);Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,777,kind))==777&&Life(pets).SequenceEqual(before),"Excluded damage kind redirected:"+kind);}
                Fill(p,pets);before=Life(pets);Require(OwnerLoss(p,()=>SkillHealth.PayLife(p,777))==777&&Life(pets).SequenceEqual(before),"Self life cost redirected");
                Fill(p,pets);before=Life(pets);Require(OwnerLoss(p,()=>SkillHealth.Damage(null,p,777))==777&&Life(pets).SequenceEqual(before),"Anonymous/environmental damage redirected");
                var immune=(WarlockTaintedBody)pets[1];immune.SetResistance(ResistanceType.Fire,200);before=Life(pets);
                SkillCombat.HitParts(enemy,immune,new(0,100,0,0,0),false,enhanceBase:false);
                Require(immune.FireResistance>=100&&Life(pets).SequenceEqual(before),"Immune ordinary-fire control failed");
                Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1200))==900&&before[1]-SkillHealth.Balance256(immune)==100,"Forwarded settled damage reapplied demon immunity");
                Fill(p,pets);var point=pets[2].Location;pets[2].MoveToWorld(point,Map.Felucca);before=Life(pets);
                Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1000))==750&&before[2]==SkillHealth.Balance256(pets[2])
                    &&before[0]-SkillHealth.Balance256(pets[0])==125&&before[1]-SkillHealth.Balance256(pets[1])==125,"Other-facet recipient entered group or changed total budget");pets[2].MoveToWorld(point,p.Map);
                Fill(p,pets);SkillHealth.SetBalance256(pets[0],512);before=Life(pets);
                int clipped=OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,51200));
                Require(pets[0].Deleted||!pets[0].Alive,"Low-life recipient did not die");
                Require(clipped==42155&&before[1]-SkillHealth.Balance256(pets[1])==4267&&before[2]-SkillHealth.Balance256(pets[2])==4266
                    &&pets[0].LastKiller==enemy,$"Capped share/death credit/redistribution differs:{clipped}");
                pets[0]=Summon(p,CombatAbility.SummonGoatman,8);
                foreach(var b in pets){SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-513);}PersistentMinions.Suspend(p);before=Life(pets);
                SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,777))==777&&Life(pets).SequenceEqual(before),"Parking granted cover/healing or replayed damage");
            }
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,enemy.Serial.Value,SkillHealth.Balance256(p),pets.Select(b=>new Pet(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))).ToArray())));kept=true;
            Log.Information("Approved Blood Oath:one pinned budget/FP8 split;actual scalar/native resisted/armor-ignore/shield routes;no recursive attack events;pet immunity bypass;facet/park exclusions;low-life cap/no redistribution/death credit;automatic learned reaction enabled");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){enemy.Delete();foreach(var b in WarlockGoatmanWorld.Demons(p)){b.Delete();}p.Delete();}}
        SoftRank(network);
    }
    private static void SoftRank(NetState network)
    {
        var p=Owner(4,7000,3600);p.Female=true;var account=FixtureNetwork.Authenticate(network,p,"blood-oath-witch-");var enemy=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+15,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        var pet=Summon(p,CombatAbility.SummonGoatman,3);string birth=Birth(pet);
        try
        {
            int before=SkillHealth.Balance256(pet);
            Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1000))==880&&before-SkillHealth.Balance256(pet)==120,"Hard4 curve control");
            PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);before=SkillHealth.Balance256(pet);
            Require(SkillMechanics.Rank(p,CombatAbility.BloodOath)==5&&OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1000))==860
                &&before-SkillHealth.Balance256(pet)==140&&Birth(pet)==birth,"Current soft rank did not update owner reaction independently of saved pet birth");
            Log.Information("Approved Blood Oath:Witch/Tarnhelm current effective4to5redirect12to14percent;existing pet life/resistance snapshot unchanged");
        }
        finally{pet.Delete();enemy.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many Blood Oath reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var enemy=World.FindMobile((Serial)s.Enemy);
        var pets=s.Pets.Select(b=>(SkeletonGuardian)World.FindMobile((Serial)b.Serial)).ToArray();
        Require(SkillHealth.Balance256(p)==s.Life,"Saved owner fractional wounds changed");
        for(int i=0;i<pets.Length;i++){Require(pets[i].Map==Map.Internal&&Birth(pets[i])==s.Pets[i].Birth&&SkillHealth.Balance256(pets[i])==s.Pets[i].Life
            &&NpcEquipmentRandom.State(pets[i])==s.Pets[i].Random,"Original recipient snapshot/wounds/RNG changed on reload");}
        Require(!BloodOathDamageProposal.ManagedPrepared(p),"Offline owner unexpectedly gained automatic protection");
        var offline=BloodOathDamageProposal.ReducePrepared(enemy,p,1001,SkillDamageKind.Direct);
        Require(offline is{Remaining256:1001,Transfers.Length:0},"Offline roster redirected damage");
        var account=FixtureNetwork.Authenticate(network,p,"blood-oath-reload-");
        try
        {
            Require(BloodOathDamageProposal.ManagedPrepared(p),"Connected learned owner did not regain automatic protection");PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);
            var before=Life(pets);
            Require(OwnerLoss(p,()=>SkillHealth.Damage(enemy,p,1001))==751&&before.Zip(Life(pets),(a,b)=>a-b).Sum()==250,"Reloaded original roster did not redirect again");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Life=SkillHealth.Balance256(p),Pets=pets.Select(b=>new Pet(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))).ToArray()}));
            Log.Information("Approved Blood Oath:original owner/three recipients retain fractional wounds,birth,RNG/host through reload,then react again without offline replay;automatic learned reaction uses no admission lease");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
