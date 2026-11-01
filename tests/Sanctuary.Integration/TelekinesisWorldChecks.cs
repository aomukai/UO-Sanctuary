// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;

namespace Sanctuary.Integration;

public static class TelekinesisWorldChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(TelekinesisWorldChecks));
    private sealed record Saved(uint Owner,uint Gold,uint Door,int Map,Point3D Point,int Mana,int Life,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"telekinesis-world-fixtures.json");
    public static void Configure()
    {
        var old=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="TelekinesisWorldFixture"&&old?.Invoke(p)!=false;
        Register("telekinesis-world-check",Check);
        Register("telekinesis-world-reload-check",()=>FixtureClock.NextLoop("telekinesis-world-reload-result",()=>FixtureNetwork.Connected("telekinesis-world-reload-result",Reload)));
        foreach(bool quick in new[]{false,true})foreach(bool item in new[]{false,true})
        {bool q=quick,i=item;string label="telekinesis-"+(i?"item-":"mobile-")+(q?"quick":"right");Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,q,i,label)));}
    }
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        // Compose after TimedEffects installs the normal recovery policy.
        var hits=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="TelekinesisWorldFixture"&&hits?.Invoke(p)!=false;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Telekinesis native object permissions and serial-preserving input",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,5870);p.Name="TelekinesisWorldFixture";
        Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.telekinesis",out _),"Learn Telekinesis");
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Telekinesis)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Telekinesis),"TK bindings");return p;
    }
    private static T Ground<T>(PlayerMobile p,T item,int dx=5,int dy=0) where T:Item
    {item.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);return item;}
    private static bool Cast(PlayerMobile p,Item item)=>SorceressCombat.Release(p,CombatAbility.Telekinesis,item.Serial,item.Location);
    // Deleted before any world save; only observes the real pickup callback.
    private sealed class PickupProbe:Gold
    {
        private readonly bool _deny;
        public PickupProbe(bool deny=false):base(3){_deny=deny;}
        // The native save type dictionary retains every observed type, even
        // after all its instances are deleted. Its factory still needs this.
        public PickupProbe(Serial serial):base(serial){}
        internal int Lifted;internal bool GroundAtLift;
        public override bool CheckLift(Mobile from,Item item,ref LRReason reject)=>!_deny&&base.CheckLift(from,item,ref reject);
        public override void OnItemLifted(Mobile from,Item item){Lifted++;GroundAtLift=Parent==null;base.OnItemLifted(from,item);}
    }
    private static void Check()
    {
        var row=SkillMechanics.For(CombatAbility.Telekinesis);
        for(int rank=1;rank<=255;rank++)
        {
            var v=new SkillValues(row,k=>k==row.SourceKey?rank:255,_=>20,99);
            Require(v.Mana256==1792&&v.Damage256(false)==rank*256&&v.Damage256(true)==(rank+1)*256&&v.Eval("aurarangecalc")==25&&v.Param(2)==35,"Independent TK ranks");
        }
        var p=Owner();var gold=Ground(p,new Gold(37));var potion=Ground(p,new GreaterHealPotion());var scroll=Ground(p,new TownPortalScroll());var weapon=Ground(p,new Dagger());var bag=Ground(p,new Backpack());var key=Ground(p,new Key());var door=Ground(p,new MetalDoor(DoorFacing.WestCW));
        var allowed=Ground(p,new PickupProbe());var denied=Ground(p,new PickupProbe(true));
        try
        {
            int mana=NecromancerEffects.ManaBalance256(p);Require(Cast(p,gold)&&gold.IsChildOf(p.Backpack)&&gold.Amount==37&&NecromancerEffects.ManaBalance256(p)==mana-1792,"Ground gold TK pickup/payment");
            Require(Cast(p,allowed)&&allowed.IsChildOf(p.Backpack)&&allowed.Lifted==1&&allowed.GroundAtLift,"TK omitted/misordered ground lift notification");mana=NecromancerEffects.ManaBalance256(p);Require(Cast(p,denied)&&denied.Parent==null&&denied.Lifted==0&&NecromancerEffects.ManaBalance256(p)==mana-1792,"TK bypassed lift rejection or refunded attempted pickup");
            p.Hits=Math.Max(1,p.HitsMax-10);int life=p.Hits;Require(Cast(p,potion)&&potion.IsChildOf(p.Backpack)&&p.Hits==life,"TK drank the pickup potion");
            Require(Cast(p,scroll)&&scroll.IsChildOf(p.Backpack)&&TownPortals.For(p)==null,"TK used portal scroll instead of pickup");
            Require(Cast(p,key)&&key.IsChildOf(p.Backpack),"TK key pickup");
            var place=weapon.Location;Require(Cast(p,weapon)&&weapon.Parent==null&&weapon.Location==place,"TK collected ordinary weapon");
            Require(!door.Open&&Cast(p,door)&&door.Open,"TK failed native remote door use");
            door.Open=false;door.Locked=true;door.KeyValue=0xBEEF;mana=NecromancerEffects.ManaBalance256(p);Require(Cast(p,door)&&!door.Open&&NecromancerEffects.ManaBalance256(p)==mana-1792,"TK bypassed native door lock or refunded attempted operation");door.Locked=false;
            Require(Cast(p,bag)&&bag.Parent==null,"TK failed ground container operation");
            var nested=new Gold(2);bag.DropItem(nested);mana=NecromancerEffects.ManaBalance256(p);Require(!Cast(p,nested)&&nested.Parent==bag&&NecromancerEffects.ManaBalance256(p)==mana,"TK stole a nested item");
            var point=door.Location;door.Internalize();bag.Internalize();
            weapon.MoveToWorld(new(p.X+12,p.Y+3,p.Z),p.Map);Require(TelekinesisWorld.Valid(p,weapon.Serial),"TK inclusive squared radius rejected12,3; see="+p.CanSee(weapon)+" LOS="+p.InLOS(weapon));weapon.MoveToWorld(new(p.X+12,p.Y+4,p.Z),p.Map);Require(!TelekinesisWorld.Valid(p,weapon.Serial),"TK retained Chebyshev corner12,4");
            mana=NecromancerEffects.ManaBalance256(p);Require(!SorceressCombat.Release(p,CombatAbility.Telekinesis,door.Serial,point)&&NecromancerEffects.ManaBalance256(p)==mana,"TK trusted forged target point/map");
            NecromancerEffects.SetMana256(p,1791);weapon.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);Require(!Cast(p,weapon)&&NecromancerEffects.ManaBalance256(p)==1791,"Insufficient TK mana");
            NecromancerEffects.SetMana256(p,p.ManaMax*256);ClassCombat.Stop(p);Require(SorceressCombat.Begin(p,CombatAbility.Telekinesis,weapon.Serial,weapon.Location,Core.TickCount),"TK cancellation windup");long effect=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);mana=NecromancerEffects.ManaBalance256(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana,"Canceled TK paid");
            p.MoveToWorld(new(1497,1628,10),Map.Trammel);weapon.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Require(TelekinesisWorld.Valid(p,weapon.Serial)&&Cast(p,weapon),"Town item TK rejected");
        }
        finally{foreach(var item in new Item[]{gold,potion,scroll,weapon,bag,key,door,allowed,denied}){item.Delete();}FireBoltChecks.Clean(p);}
        Save();Log.Information("Telekinesis world:255independent rank contexts,ground pickup without use,remote door/container,non-whitelist no-pickup,nested/map/squared-range/mana/cancel/town controls pass;combat return and travel objects remain separate");
    }
    private static void Save()
    {
        var p=Owner();var gold=Ground(p,new Gold(43));var door=Ground(p,new MetalDoor(DoorFacing.WestCW),7);Require(Cast(p,gold)&&Cast(p,door)&&door.Open,"Saved TK operations");
        SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-257);var s=new Saved(p.Serial.Value,gold.Serial.Value,door.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var gold=(Gold)World.FindItem((Serial)s.Gold);var door=(BaseDoor)World.FindItem((Serial)s.Door);
        Log.Information("TK reload:gold={Gold}/{Amount} binding={Binding} mana={Mana}/{ExpectedMana} life={Life}/{ExpectedLife}",gold.IsChildOf(p.Backpack),gold.Amount,SkillBindings.Instance.Get(p).Right,NecromancerEffects.ManaBalance256(p),s.Mana,SkillHealth.Balance256(p),s.Life);
        // Ordinary doors autoclose on their own timer. Persisted pickup,
        // binding, resources and wounds are the invariants, not open state.
        Require(s.Generation<2&&gold.IsChildOf(p.Backpack)&&gold.Amount==43&&SkillBindings.Instance.Get(p).Right==CombatAbility.Telekinesis&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Saved TK pickup/resources/wounds/binding");
        var account=FixtureNetwork.Authenticate(network,p,"telekinesis-world-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);bool open=door.Open;Require(Cast(p,door)&&door.Open!=open&&NecromancerEffects.ManaBalance256(p)==s.Mana-1792,"Reload original caster/door paid operation");
        p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Mana=NecromancerEffects.ManaBalance256(p),Generation=s.Generation+1}));Log.Information("telekinesis-world-reload-result: passed");
    }
    private static void Input(NetState network,bool quick,bool item,string label)
    {
        var p=Owner();var gold=item?Ground(p,new Gold(17)):null;var enemy=item?null:FireBoltChecks.Target(p,5);IEntity target=(IEntity)gold??enemy;
        var account=FixtureNetwork.Authenticate(network,p,"telekinesis-world-input-");int mana=NecromancerEffects.ManaBalance256(p),life=enemy==null?0:SkillHealth.Balance256(enemy);bool began=false;long deadline=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"TK input timeout");var state=ClassCombat.For(p);
                if(state.PendingAbility==CombatAbility.Telekinesis&&!began){Require(state.PendingTarget==target.Serial&&NecromancerEffects.ManaBalance256(p)==mana,"TK lost serial/paid at windup");began=true;Log.Information("{Label}: windup",label);}
                bool done=item?gold.IsChildOf(p.Backpack):SkillHealth.Balance256(enemy)<life;
                if(!done){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-1792,"TK actual input payment");gold?.Delete();FireBoltChecks.Clean(p,enemy==null?Array.Empty<Mobile>():[enemy]);account.Delete();Log.Information("{Label}-result: passed",label);
            }catch(Exception e){gold?.Delete();FireBoltChecks.Clean(p,enemy==null?Array.Empty<Mobile>():[enemy]);account.Delete();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Telekinesis,(int)DiabloClass.Sorceress,target.Serial.Value,target.X,target.Y,target.Z);Observe();});
    }
}
