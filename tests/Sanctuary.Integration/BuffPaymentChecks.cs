// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BuffPaymentChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BuffPaymentChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Enemy,uint Orb,int Mana,int Life,int AllyLife,int Map,Point3D Point,Point3D AllyPoint,EnchantState Self,EnchantState Other,int Generation,int Refusals);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"buff-payment-fixtures.json");
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name?.StartsWith("BuffPayment",StringComparison.Ordinal)!=true&&prior?.Invoke(p)!=false;
        Register("buff-payment-check",Check);Register("buff-payment-reload-check",Reload);
        foreach(string kind in new[]{"self","ally","hostile"})foreach(string mode in new[]{"right","quick"})
        {string k=kind,label="buff-payment-"+kind+"-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,k,label)));}
    }
    [CallPriority(int.MaxValue)]
    public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name?.StartsWith("BuffPayment",StringComparison.Ordinal)!=true&&prior?.Invoke(p)!=false;}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Enchant failed-release payment and original saved refusal",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner(){var p=EnchantCastChecks.Owner();p.Name="BuffPaymentOwner";return p;}
    private static PlayerMobile Ally(PlayerMobile p){var a=EnchantCastChecks.Ally(p);a.Name="BuffPaymentAlly";return a;}
    private static bool Release(PlayerMobile p,Mobile target)=>SorceressCombat.Release(p,CombatAbility.Enchant,target?.Serial??Serial.Zero,target?.Location??p.Location);
    private static void Check()
    {
        Source();Windup();Save();
        Log.Information("Buff payment:45native self/ally/fallback comparisons with complete generic wrapper,unpaid weaker refusal,equal original-caster refresh,stronger paid replacement,live release rank/hard,cancel/insufficient and original saved refusals pass");
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"buff-payment-source-cases.json")));int compared=0;
        foreach(int rank in new[]{1,10,20})
        {
            var p=Owner();var ally=Ally(p);var enemy=FireBoltChecks.Target(p,3);try
            {
                EnchantCastChecks.Train(p,CombatAbility.Enchant,rank);
                foreach(var r in doc.RootElement.EnumerateArray())
                {
                    int V(int i)=>r[i].GetInt32();if(V(0)!=52||V(2)!=rank||V(3)!=1){continue;}
                    foreach(string kind in new[]{"self","ally","hostile"})
                    {
                        SorceressEffects.Profiles.Clear(p);SorceressEffects.Profiles.Clear(ally);SorceressEffects.Profiles.Clear(enemy);
                        Mobile target=kind=="self"?null:kind=="ally"?ally:enemy;Mobile recipient=kind=="ally"?ally:p;
                        var old=new EnchantState(TimedEffects.Now.AddMinutes(10),V(1),7,8,9,101);
                        if(V(1)>0){SorceressEffects.Profiles.Store(recipient,new(Enchantment:old));}
                        NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p);var values=SkillMechanics.Values(p,CombatAbility.Enchant);
                        bool applied=Release(p,target);var result=SorceressEffects.Enchantment(recipient);
                        Require(applied==(V(4)!=0)&&mana-NecromancerEffects.ManaBalance256(p)==V(5)*values.Mana256,"Complete wrapper payment qualification "+kind);
                        if(!applied){Require(result==old,"Refused Enchant changed original payload/deadline/caster");}
                        else{Require(result.Rank==V(6)&&result.Caster==(V(1)==rank?101U:p.Serial.Value)&&result.Min256==values.Damage256(false),"Equal/stronger source replacement "+kind);}
                        Require(kind!="hostile"||SorceressEffects.Enchantment(enemy)==null,"Hostile fallback enchanted hostile");compared++;
                    }
                }
            }
            finally{FireBoltChecks.Clean(p,ally,enemy);}
        }
        Require(compared==45,"Incomplete source/native payment coverage");
    }
    private static void Windup()
    {
        var p=Owner();var ally=Ally(p);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(Release(p,ally),"Preexisting real higher Enchant");var old=SorceressEffects.Enchantment(ally);
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);int mana=NecromancerEffects.ManaBalance256(p);
            Require(old.Rank>SkillMechanics.Rank(p,CombatAbility.Enchant)&&ClassCombat.ContinueCast(p,CombatAbility.Enchant,ally,ally.Location,Core.TickCount),"Weaker real windup admission");
            long at=ClassCombat.For(p).EffectAt,next=ClassCombat.For(p).NextAction;
            Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Enchantment(ally)==old
                &&ClassCombat.For(p).PendingAbility==CombatAbility.None&&ClassCombat.For(p).NextAction==next,"Failed release paid/refreshed or erased recovery");
            Require(ClassCombat.ContinueCast(p,CombatAbility.Enchant,ally,ally.Location,Math.Max(Core.TickCount,next)),"Live rank windup");at=ClassCombat.For(p).EffectAt;
            EnchantCastChecks.Train(p,CombatAbility.Enchant,old.Rank);EnchantCastChecks.Train(p,CombatAbility.Warmth,20);var values=SkillMechanics.Values(p,CombatAbility.Enchant);
            Require(ClassCombat.ResolveAction(p,at)&&mana-NecromancerEffects.ManaBalance256(p)==values.Mana256
                &&SorceressEffects.Enchantment(ally) is{} current&&current.Rank==old.Rank&&current.Caster==old.Caster&&current.Min256==values.Damage256(false),"Release did not use live hard/rank or preserve equal owner");
            old=SorceressEffects.Enchantment(ally);mana=NecromancerEffects.ManaBalance256(p);next=ClassCombat.For(p).NextAction;
            Require(ClassCombat.ContinueCast(p,CombatAbility.Enchant,ally,ally.Location,Math.Max(Core.TickCount,next)),"Cancellation windup");at=ClassCombat.For(p).EffectAt;ClassCombat.Stop(p);
            Require(!ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Enchantment(ally)==old,"Cancelled recast paid or refreshed");
            NecromancerEffects.SetMana256(p,values.Mana256-1);Require(!Release(p,ally)&&NecromancerEffects.ManaBalance256(p)==values.Mana256-1&&SorceressEffects.Enchantment(ally)==old,"Insufficient recast paid/refreshed");
        }
        finally{ClassCombat.Stop(p);FireBoltChecks.Clean(p,ally);}
    }
    private static void Save()
    {
        var p=Owner();var ally=Ally(p);var enemy=FireBoltChecks.Target(p,3);EnchantCastChecks.Train(p,CombatAbility.Enchant,20);
        var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(Release(p,null)&&Release(p,ally),"Saved real paid high buffs");
        p.Backpack.DropItem(orb);ClassGear.Refresh(p);SkillHealth.Damage(p,p,5*256+13);SkillHealth.Damage(p,ally,3*256+19);
        var self=SorceressEffects.Enchantment(p);var other=SorceressEffects.Enchantment(ally);Require(self.Rank>20&&other.Rank==self.Rank,"Saved higher real rank");
        var saved=new Saved(p.Serial.Value,ally.Serial.Value,enemy.Serial.Value,orb.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),SkillHealth.Balance256(ally),p.Map.MapID,p.Location,ally.Location,self,other,0,0);
        p.Internalize();ally.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var ally=(PlayerMobile)World.FindMobile((Serial)s.Ally);var enemy=(BaseCreature)World.FindMobile((Serial)s.Enemy);
        Require(s.Generation<2&&World.FindItem((Serial)s.Orb)?.Parent==p.Backpack&&SorceressEffects.Profiles.Find(p)?.Enchantment==s.Self&&SorceressEffects.Profiles.Find(ally)?.Enchantment==s.Other
            &&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(ally)==s.AllyLife,"Saved original Enchant actors/orb/resources/payload changed");
        Require(s.Self.UntilUtc>TimedEffects.Now&&s.Other.UntilUtc>TimedEffects.Now,"Natural higher-buff deadline elapsed before refusal inspection");
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);ally.MoveToWorld(s.AllyPoint,p.Map);enemy.AIObject.AITimer.Stop();
        Require(!Release(p,null)&&!Release(p,ally)&&!Release(p,enemy)&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SorceressEffects.Enchantment(p)==s.Self&&SorceressEffects.Enchantment(ally)==s.Other
            &&SorceressEffects.Enchantment(enemy)==null&&SkillHealth.Balance256(p)==s.Life&&SkillHealth.Balance256(ally)==s.AllyLife,"Saved weaker self/ally/fallback release paid/refreshed or restored wounds");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Enchant&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Sorceress,7)==CombatAbility.Enchant,"Saved Enchant bindings changed");
        p.Internalize();ally.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Refusals=s.Refusals+3}));
    }
    private static void Input(NetState network,string kind,string label)
    {
        var p=Owner();Mobile target=kind=="self"?null:kind=="ally"?Ally(p):FireBoltChecks.Target(p,3);Mobile recipient=kind=="ally"?target:p;
        var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");Require(Release(p,target),"Actual preexisting input buff");var old=SorceressEffects.Enchantment(recipient);
        p.Backpack.DropItem(orb);ClassGear.Refresh(p);var account=FixtureNetwork.Authenticate(network,p,"buff-payment-input-");int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long effect=0,deadline=Core.TickCount+5000;
        void Finish(){ClassCombat.Stop(p);FireBoltChecks.Clean(p,target==null?Array.Empty<Mobile>():new[]{target});account.Delete();}
        void Observe()
        {
            try
            {
                Require(Core.TickCount<deadline,"Failed Enchant input timeout");var state=ClassCombat.For(p);
                if(state.PendingAbility==CombatAbility.Enchant&&!began){Require(NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Enchantment(recipient)==old,"Input paid during windup");began=true;effect=state.EffectAt;Log.Information("{Label}: windup",label);}
                if(!began||state.PendingAbility!=CombatAbility.None){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Observe);return;}
                Require(Core.TickCount>=effect&&NecromancerEffects.ManaBalance256(p)==mana&&SorceressEffects.Enchantment(recipient)==old&&(kind!="hostile"||SorceressEffects.Enchantment(target)==null),"Failed input paid/refreshed/cancelled early");
                Finish();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Finish();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{var point=target?.Location??p.Location;Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Enchant,(int)DiabloClass.Sorceress,target?.Serial.Value??0,point.X,point.Y,point.Z);Observe();});
    }
}
