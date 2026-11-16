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

public static class BasicMeleeChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BasicMeleeChecks));
    private sealed record Saved(uint Owner,uint Item,uint Helm,uint Target,Point3D Point,string Gear,int Life,int Mana,int TargetLife,int Loss,DateTime Until,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"basic-melee-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static DateTime Until(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Human=PlayerBasicContact.HitRoll,Form=DruidFormAttacks.HitRoll;
        private readonly Func<int,int> Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        internal Rolls(){Hit(true);SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        internal static void Hit(bool yes){PlayerBasicContact.HitRoll=DruidFormAttacks.HitRoll=()=>yes?0:1;}
        public void Dispose(){PlayerBasicContact.HitRoll=Human;DruidFormAttacks.HitRoll=Form;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"basic-melee-reload-check":"basic-melee-check";ConsoleInputHandler.RegisterCommand(name,"Eight-class ordinary melee without proc gear and exact saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var type in Enum.GetValues<DiabloClass>())foreach(string side in new[]{"left","right"})
        {var c=type;string label="basic-melee-"+type.ToString().ToLowerInvariant()+"-"+side;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated ordinary melee with no proc provider",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,c));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static void Ready(PlayerMobile p,int mana=17)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,mana);}
    private static bool Attack(PlayerMobile p,BaseCreature t,CombatAbility ability=CombatAbility.BasicAttack,int mana=17)
    {
        Ready(p,mana);Require(!PlayerItemProcs.ActiveProvider(p)&&!PlayerItemProcs.Initialized(p),"Ordinary actor acquired a proc provider/stream");var world=NpcWorldAllocation.Capture();long packets=NativeMonsterReturn.Session(t).Packets;int life=SkillHealth.Balance256(t);
        Require(PlayerBasicContact.Handles(p,t)&&ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Ordinary melee start");var s=ClassCombat.For(p);Require(s.PendingAbility==CombatAbility.BasicAttack,"Low-mana skill did not become ordinary Attack");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&SkillHealth.Balance256(t)==life&&!PlayerItemProcs.Initialized(p)&&NpcWorldAllocation.Capture()==world,"Ordinary windup changed life/proc RNG");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&NativeMonsterReturn.Session(t).Packets==packets+1&&NecromancerEffects.ManaBalance256(p)==mana&&!PlayerItemProcs.Initialized(p)&&NpcWorldAllocation.Capture()==world,"Ordinary release skipped contact,spent mana or allocated proc RNG");
        life=SkillHealth.Balance256(t);Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&SkillHealth.Balance256(t)==life&&NativeMonsterReturn.Session(t).Packets==packets+1,"Ordinary contact replayed");return true;
    }
    private static BaseWeapon Plain(PlayerMobile p,BaseWeapon w)
    {
        if(CombatTiming.Equipped(p)is{} old){p.AddToBackpack(old);}ClassGear.Profiles.Convert(w,99,GearRarity.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Equip ordinary carrier");ClassGear.Refresh(p);return w;
    }
    private static void Play(PlayerMobile p)
    {
        var t=DruidSetupItemChecks.Target(p);
        try
        {
            Rolls.Hit(true);int before=SkillHealth.Balance256(t);Attack(p,t);Require(SkillHealth.Balance256(t)<before,"Ordinary melee caused no native damage");
            Rolls.Hit(false);before=SkillHealth.Balance256(t);int life=SkillHealth.Balance256(p);
            PaladinAuras.ElementalWeapon(new(t.Serial.Value,p.Serial.Value,CombatAbility.Thorns,1,1,TimedEffects.Now.AddMinutes(5)));
            Attack(p,t);Require(SkillHealth.Balance256(t)==before&&life-SkillHealth.Balance256(p)==256,"Ordinary miss caused damage or lost/duplicated flat return");PaladinAuras.Clear(t);
            Rolls.Hit(true);SourceBlocking.BlockRoll=_=>0;Attack(p,t);Require(SkillHealth.Balance256(t)==before,"Ordinary shield block allowed damage");SourceBlocking.BlockRoll=n=>n-1;
        }
        finally{ClassCombat.Stop(p);PaladinAuras.Clear(t);t.Delete();}
    }
    private static void Forms()
    {
        var p=HellslayerChecks.Owner(DiabloClass.Druid,1300);
        foreach(var a in new[]{CombatAbility.Werewolf,CombatAbility.Werebear,CombatAbility.Maul,CombatAbility.FeralRage,CombatAbility.FireClaws,CombatAbility.Hunger,CombatAbility.Rabies,CombatAbility.Fury}){ClassSkillChecks.Train(p,SkillMechanics.For(a).TalentId);}ClassSkillChecks.Train(p,"druid.lycanthropy",20);
        try
        {
            foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
            {
                Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Ordinary form entry");
                foreach(BaseWeapon w in new BaseWeapon[]{new Dagger(),new Bow(),new Crossbow(),ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive())})
                {
                    if(w is SoulGlaive)
                    {if(CombatTiming.Equipped(p)is{} old){p.AddToBackpack(old);}ClassGear.Profiles.FromMonster(w,30,GearRarity.Normal,"glv",GearTier.Normal,new Random(173));p.AddToBackpack(w);Require(p.EquipItem(w),"Equip form thrower");}
                    else{Plain(p,w);}
                    var t=DruidSetupItemChecks.Target(p);int quantity=NamedAmmunition.Remaining(w),before=SkillHealth.Balance256(t);
                    try{Attack(p,t);Require(SkillHealth.Balance256(t)<before&&NamedAmmunition.Remaining(w)==quantity&&ClassCombat.For(p).Timing==DruidFormAttacks.Timing(p),"Ordinary form changed clock,damage or ammunition");}
                    finally{t.Delete();p.AddToBackpack(w);w.Delete();}
                }
                var weapon=DruidSetupItemChecks.Named(p,"Woestave");var helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");
                foreach(var a in form==CombatAbility.Werewolf?new[]{CombatAbility.FeralRage,CombatAbility.FireClaws,CombatAbility.Hunger,CombatAbility.Rabies,CombatAbility.Fury}:new[]{CombatAbility.Maul,CombatAbility.FireClaws,CombatAbility.Hunger})
                {
                    var t=DruidSetupItemChecks.Target(p);
                    try{Attack(p,t,a,0);Require(Until(t)>TimedEffects.Now&&TimedEffects.DefenseLoss(t)>0&&DruidAttackCharges.State(p,CombatAbility.Maul)==null&&DruidAttackCharges.State(p,CombatAbility.FeralRage)==null,"Low-mana ordinary fallback omitted setup or gained a skill charge");}
                    finally{ClassCombat.Stop(p);t.Delete();}
                }
                p.AddToBackpack(weapon);weapon.Delete();p.AddToBackpack(helm);helm.Delete();DruidPlayerForms.Clear(p);
            }
        }
        finally{DruidPlayerForms.Clear(p);p.Delete();}
    }
    private static void Exclusions()
    {
        var p=HellslayerChecks.Owner(DiabloClass.Barbarian,1650);var t=DruidSetupItemChecks.Target(p);
        try
        {
            foreach(BaseWeapon w in new BaseWeapon[]{new Bow(),new Crossbow(),ItemOrigins.Create(ItemOrigin.Drop,()=>new SoulGlaive())})
            {Plain(p,w);Require(!PlayerBasicContact.Handles(p,t),"Human projectile route was captured as melee");p.AddToBackpack(w);w.Delete();}
        }
        finally{t.Delete();p.Delete();}
        p=HellslayerChecks.Owner(DiabloClass.Assassin,1700);t=DruidSetupItemChecks.Target(p);
        try{ClassSkillChecks.Train(p,"assassin.tigerstrike");AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));Require(AssassinCharges.Any(p)&&!PlayerBasicContact.Handles(p,t),"Ordinary route captured an Assassin charge finisher");}
        finally{AssassinCharges.Clear(p);t.Delete();p.Delete();}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var types=Enum.GetValues<DiabloClass>();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[types.Length];
        if(!reload){Forms();Exclusions();}
        for(int i=0;i<types.Length;i++)
        {
            PlayerMobile p;Item item,helm;BaseCreature target;int generation=0;
            if(!reload)
            {
                p=HellslayerChecks.Owner(types[i],900+i*36);Play(p);var dagger=Plain(p,new Dagger());Play(p);p.AddToBackpack(dagger);dagger.Delete();
                item=DruidSetupItemChecks.Named(p,"Woestave");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");target=DruidSetupItemChecks.Target(p);Attack(p,target);Require(Until(target)>TimedEffects.Now&&TimedEffects.DefenseLoss(target)>0,"Ordinary item setup absent");PoisonDaggerChecks.Freeze(target);
            }
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);helm=World.FindItem((Serial)s.Helm);target=(BaseCreature)World.FindMobile((Serial)s.Target);
                Require(generation<=2&&item.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(item),ClassGear.State(helm)})==s.Gear&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(target)==s.TargetLife&&TimedEffects.DefenseLoss(target)==s.Loss&&Until(target)==s.Until&&!PlayerItemProcs.Initialized(p),"Saved ordinary originals/wounds/resources/setup or absent proc stream changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Play(p);ClassCombat.Stop(p);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,item.Serial.Value,helm.Serial.Value,target.Serial.Value,point,Json(new[]{ClassGear.State(item),ClassGear.State(helm)}),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),TimedEffects.DefenseLoss(target),Until(target),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,DiabloClass type)
    {
        var rolls=new Rolls();var p=HellslayerChecks.Owner(type,1500);DruidSetupItemChecks.Named(p,"Woestave");DruidSetupItemChecks.Named(p,"Blackhorn's Face");var t=DruidSetupItemChecks.Target(p);Ready(p);
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BasicAttack),"Bind ordinary input");var account=FixtureNetwork.Authenticate(n,p,"basic-melee-");var binding=SkillBindings.Instance.Get(p);var world=NpcWorldAllocation.Capture();int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;long packets=NativeMonsterReturn.Session(t).Packets;
        void Cleanup(){ClassCombat.Stop(p);rolls.Dispose();t.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Ordinary input timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==CombatAbility.BasicAttack){Require(SkillHealth.Balance256(t)==before&&Until(t)<=TimedEffects.Now&&TimedEffects.DefenseLoss(t)==0&&!PlayerItemProcs.Initialized(p),"Ordinary input windup applied setup/life/proc state");began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==packets){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&NativeMonsterReturn.Session(t).Packets==packets+1&&SkillHealth.Balance256(t)<before&&Until(t)>TimedEffects.Now&&TimedEffects.DefenseLoss(t)>0&&NecromancerEffects.ManaBalance256(p)==mana&&s.PendingTarget==t.Serial&&SkillBindings.Instance.Get(p)==binding&&!PlayerItemProcs.Initialized(p)&&NpcWorldAllocation.Capture()==world,"Ordinary wire lost contact/setup/bindings/resources or created proc RNG");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BasicAttack,(int)type,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
