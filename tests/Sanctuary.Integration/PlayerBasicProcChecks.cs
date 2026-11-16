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

public static class PlayerBasicProcChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(PlayerBasicProcChecks));
    private sealed record Saved(uint Owner,uint Item,uint Target,Point3D Point,string Gear,string Profile,int Life,int Mana,int TargetLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"player-basic-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"player-basic-proc-reload-check":"player-basic-proc-check";
            ConsoleInputHandler.RegisterCommand(name,"Eight-class ordinary melee proc delivery,retaliation and saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"left","right"})
        {string label="player-basic-proc-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated ordinary melee with approved equipped item procs",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(o=>o.Actor==p.Serial.Value).ToArray()};}
    private static ulong Seed(PlayerMobile p)=>PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed;
    private static PlayerMobile Owner(DiabloClass type)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="PlayerItemFireBallFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity})
        {while((stat==CharacterStat.Strength?p.RawStr:p.RawDex)<100){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Allocate basic attack stats");}}
        FixtureSpace.Corridor(p,6320,24,3300);p.Hits=p.HitsMax;NecromancerEffects.SetMana256(p,17);return p;
    }
    private static Item Carrier(PlayerMobile p)
    {
        var item=new Dagger();ClassGear.Profiles.Convert(item,99,GearRarity.Normal,new Random(173));p.AddToBackpack(item);
        var values=new EquipmentStatValues(_=>4,_=>null,_=>null,(_,_,_,_,_)=>{});var graph=new ItemPropertyCreationContext(item,values,default,false,false,false,SourceRandom.InitLow(17),[]);
        foreach(string code in new[]{"att-skill","hit-skill"}){graph.ApplySkill(EquipmentPropertyWriter.Plan(code),new(100,20,47),ItemSkillProperties.ForLevel(99));}
        PlayerItemProcs.Register(item,4,graph.Capture());Require(p.EquipItem(item),"Equip actual basic proc carrier");return item;
    }
    private static IronGolemFireBallChecks.Recipient Target(PlayerMobile p)
    {
        var t=new IronGolemFireBallChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static int Tick(PlayerMobile p,long first)
    {
        var all=SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>first).ToArray();
        foreach(var f in all){for(int n=0;n<60&&FireBoltChecks.State(f.Id)is{} live;n++){SorceressSpellMissiles.Tick(live.NextUtc,true,live.Id);}}
        return all.Length;
    }
    private static bool Attack(PlayerMobile p,BaseCreature t)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;return ClassCombat.Perform(p,CombatAbility.BasicAttack,t,t.Location,Core.TickCount)&&ClassCombat.ResolveAction(p,s.EffectAt);}
    private static void Check(bool reload)
    {
        var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[8];var types=Enum.GetValues<DiabloClass>();Require(types.Length==8,"Class roster changed");
        var hit=PlayerBasicContact.HitRoll;var block=SourceBlocking.BlockRoll;var observe=PlayerItemProcWorld.Observed;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;
            for(int i=0;i<types.Length;i++)
            {
                PlayerMobile p;Item item;IronGolemFireBallChecks.Recipient t;int generation=0;
                if(!reload){p=Owner(types[i]);item=Carrier(p);t=Target(p);}
                else
                {
                    var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);t=(IronGolemFireBallChecks.Recipient)World.FindMobile((Serial)s.Target);
                    Require(generation<=2&&item.Parent==p&&Json(ClassGear.State(item))==s.Gear&&Json(Own(p,item))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife,"Saved basic original/profile/wounds changed");p.MoveToWorld(s.Point,Map.Trammel);
                }
                Require(PlayerBasicContact.Handles(p,t),"Basic proc route did not recognize its actual melee equipment");var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
                using(PlayerItemProcWorld.Candidate(p))
                {
                    PlayerBasicContact.HitRoll=()=>0;int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t);long first=SorceressSpellMissiles.Snapshot().NextId;
                    Require(Attack(p,t)&&visits.Select(v=>v.Phase).SequenceEqual(new[]{"domeleedamage","domeleeattack"})&&visits.All(v=>v.Outcomes.Single().CastResults==1)
                        &&SkillHealth.Balance256(t)<before&&NecromancerEffects.ManaBalance256(p)==mana,"Ordinary basic attack lost source phases,weapon damage or free proc mana");
                    before=SkillHealth.Balance256(t);Require(Tick(p,first)==2&&SkillHealth.Balance256(t)<before&&t.Source==p.Serial.Value,"Basic proc flights did not cause native player damage");
                    // Existing outer attack scope must not be opened again by
                    // the ordered contact:one missed swing returns one flat HP.
                    visits.Clear();PlayerBasicContact.HitRoll=()=>1;var seed=Seed(p);var world=NpcWorldAllocation.Capture();int life=SkillHealth.Balance256(p);
                    PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1,TimedEffects.Now.AddMinutes(5)));
                    Require(Attack(p,t)&&visits.Count==1&&visits[0].Phase=="domeleeattack"&&visits[0].Outcomes.Single().CastAttempts==0&&Seed(p)==seed&&NpcWorldAllocation.Capture()==world
                        &&life-SkillHealth.Balance256(p)==256,"Basic miss fabricated proc eligibility or duplicated/lost the existing flat return");PaladinAuras.Clear(t);
                }
                ClassCombat.Stop(p);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,item.Serial.Value,t.Serial.Value,point,Json(ClassGear.State(item)),Json(Own(p,item)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),generation);
            }
            File.WriteAllText(Manifest,Json(saved));
        }
        finally{PlayerBasicContact.HitRoll=hit;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observe;}
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner(DiabloClass.Paladin);var item=Carrier(p);var t=Target(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BasicAttack),"Bind ordinary melee");
        var account=FixtureNetwork.Authenticate(network,p,"basic-proc-");var binding=SkillBindings.Instance.Get(p);var hit=PlayerBasicContact.HitRoll;var block=SourceBlocking.BlockRoll;var observed=PlayerItemProcWorld.Observed;PlayerBasicContact.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        var visits=new List<PlayerProcWorldVisit>();var ids=new HashSet<long>();long first=SorceressSpellMissiles.Snapshot().NextId;int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;
        ulong expectedWorld=NpcWorldAllocation.RequireStream().Value;ulong expectedActor=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedActor);SourceRandom.Roll(ref expectedWorld);SourceRandom.Roll(ref expectedWorld);
        PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);foreach(var f in SorceressSpellMissiles.Snapshot().Flights.Where(f=>f.Owner==p.Serial.Value&&f.Id>first)){ids.Add(f.Id);}if(v.Phase=="domeleedamage"){Require(SkillHealth.Balance256(t)==before,"Wire basic proc ran after life");}}};
        void Cleanup(){ClassCombat.Stop(p);PlayerBasicContact.HitRoll=hit;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);t.Delete();item.Delete();p.Delete();account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Wire basic item proc timeout");var state=ClassCombat.For(p);
                if(!began&&state.PendingAbility==CombatAbility.BasicAttack){Require(visits.Count==0&&!PlayerItemProcs.Initialized(p)&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana,"Basic windup rolled procs or spent mana");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(began&&ids.Count==2&&state.PendingAbility==CombatAbility.None&&state.PendingTarget==t.Serial&&SkillBindings.Instance.Get(p)==binding&&Seed(p)==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==mana&&NativeMonsterReturn.Session(t).Packets==1,"Wire basic attack lost target/bindings,exact independent streams or free mana");
                ClassCombat.Stop(p);int life=SkillHealth.Balance256(t);Require(Tick(p,first)==2&&SkillHealth.Balance256(t)<life&&t.Source==p.Serial.Value,"Wire basic proc did not damage under its actual player");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BasicAttack,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Observe();});
    }
}
