// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
namespace Sanctuary;

internal sealed record MiasmaChainRoute(string ReferenceVersion,string SourceSha256,int MissileCount,SummonSource[] Missiles);
internal sealed record MiasmaChainCast(uint Caster,int Kind,bool HasPath,int X,int Y,int Skill,int Rank,int Flags);
internal sealed record MiasmaChainRequest(int Flags,uint Caster,int Missile,int X,int Y,int TargetX,int TargetY,int Skill,int Rank);
internal sealed record MiasmaChainServices(Func<bool> Row,Func<int> Progressive,Func<(bool Found,int X,int Y)> Target,Action<int> WriteFlags,Func<MiasmaChainRequest,bool> Create);
internal sealed record MiasmaChainResult(int Result,int Flags);

// Current do167 request producer only. The generic factory and new maker43,
// moving-chain45,hit41/cloud41 bodies remain distinct;no bolt is substituted.
internal static class MiasmaChainLaunch
{
    private static readonly Lazy<MiasmaChainRoute> Data=new(()=>BalanceCatalog.ReadObject<MiasmaChainRoute>("miasma-chain-route"));
    internal static int PinnedMaker(SkillValues values)
    {
        var row=SkillMechanics.For(CombatAbility.MiasmaChain);var data=Data.Value;
        if(values?.Ability!=CombatAbility.MiasmaChain||values.Rank is <1 or >255||row.SourceId!=399||row.Number("srvstfunc")!=0
            ||row.Number("srvdofunc")!=167||row.Number("ItemEffect")!=167||row.Text("srvmissilea")!="miasmachainsmaker"
            ||row.Text("EType")!="mag"||data.ReferenceVersion!="3.3.93847"||data.SourceSha256!=ItemSkillExecution.MissileSourceHash||data.MissileCount!=742||data.Missiles.Length!=4)
        {throw new NotSupportedException("Unqualified current Miasma Chain profile");}
        int[] ids=[711,712,713,696],functions=[43,45,43,41];string[] keys=["miasmachainsmaker","miasmachains","miasmachainscloudmaker","miasmachainscloud"];
        for(int i=0;i<4;i++)
        {
            var m=new MissileValues(data.Missiles.Single(m=>m.Key==keys[i]).Fields,values);
            if(m.Number("*ID")!=ids[i]||m.Number("pSrvDoFunc")!=functions[i]){throw new InvalidDataException("Miasma Chain stage identity changed");}
            if(i==0&&(m.Text("SubMissile1")!=keys[1]||m.Number("Vel")!=0||m.Number("Range")!=1||m.Number("CollideType")!=6)
                ||i==1&&(m.Number("pSrvHitFunc")!=41||m.Text("HitSubMissile1")!=keys[2]||m.Number("Vel")!=35||m.Number("InitSteps")!=12)
                ||i==2&&m.Text("SubMissile1")!=keys[3]||i==3&&(m.Number("NextHit")!=1||m.Number("NextDelay")!=25||m.Text("EType")!="mag"))
            {throw new InvalidDataException("Miasma Chain stage dependency changed");}
        }
        return 711; // Pinned profile value,not a claim about protected progressive-helper execution.
    }
    internal static MiasmaChainResult Run(MiasmaChainCast context,MiasmaChainServices services)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(services);int flags=context.Flags;
        if(!services.Row()){return new(0,flags);}
        int missile=services.Progressive();if(missile<=0){return new(0,flags);}
        var target=services.Target();if(!target.Found){return new(0,flags);}
        if(context.Caster!=0){flags|=0x40;services.WriteFlags(flags);}
        if(context.Caster==0||context.Kind is 2 or 4 or 5&&!context.HasPath){throw new NotSupportedException("Unqualified Miasma Chain source/path");}
        bool stationary=context.Kind is 2 or 4 or 5;
        int x=stationary?context.X:context.HasPath?unchecked((ushort)context.X):0;
        int y=stationary?context.Y:context.HasPath?unchecked((ushort)context.Y):0;
        bool created=services.Create(new(33,context.Caster,missile,x,y,target.X,target.Y,context.Skill,context.Rank));
        return new(created?1:0,flags);
    }
}
