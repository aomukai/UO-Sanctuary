// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace Sanctuary;

internal sealed record ItemQualityBase(uint Code,bool Quest,int AutoPrefix);
internal sealed record ItemQualityUnique(int Version,bool Enabled,bool Ladder,uint Code,int Level,int Rarity);
internal sealed record ItemQualityRatio(int Unique,int Rare,int Set,int Magic,int Superior,int Normal,
    int UniqueDivisor,int RareDivisor,int SetDivisor,int MagicDivisor,int SuperiorDivisor,int NormalDivisor);
internal abstract class ItemQualityServices
{
    internal abstract bool HasItem{get;}
    internal abstract bool IsItem{get;}
    internal abstract ItemQualityBase Basis{get;}
    internal abstract int Quality{get;set;}
    internal abstract int DropQuality{get;set;}
    internal abstract int Format{get;}
    internal abstract bool CanMagic{get;}
    internal abstract bool Quest{get;}
    internal abstract bool MagicOrBetter{get;}
    internal abstract bool CanRare{get;}
    internal abstract bool UniqueColumn{get;}
    internal abstract bool CanNormal{get;}
    internal abstract int RollQuality();
    internal abstract uint LowSeed{get;}
    internal abstract void StartSeed(uint seed);
    internal abstract void InitLow(uint seed);
    internal abstract void AutoAffix(int id);
    internal abstract void Affix(bool prefix,int slot,int id);
    internal abstract void RareName(bool prefix,int id);
    internal abstract void FileIndex(int id);
    internal abstract int Inferior();
    internal abstract void Normal(bool legacy);
    internal abstract void NormalFallback();
    internal abstract int Superior();
    internal abstract int Rare();
    internal abstract int Magic();
    internal abstract int Craft();
    internal abstract int Tempered(bool prefix);
    internal abstract int Set();
    internal abstract bool HasDurability{get;}
    internal abstract int FullStat(int stat);
    internal abstract int BaseStat(int stat);
    internal abstract void WriteStat(int stat,int value);
    internal abstract bool Force{get;}
    internal abstract bool GameAllowsLadder{get;}
    internal abstract int ItemLevel{get;}
    internal abstract int ForcedIndex{get;}
    internal abstract int UniqueCount{get;}
    internal abstract ItemQualityUnique UniqueAt(int index);
    internal abstract ItemQualityUnique FindUnique(int index);
    internal abstract bool Dropped(int index);
    internal abstract bool CanDropUnique();
    internal abstract int Limited(int bound);
    internal abstract void Identify(bool value);
    internal abstract void AssignProperties(int mode,object record);
    internal abstract void MakeEthereal();
    internal abstract void FinishNormal();
    internal abstract int PickAuto(int spawn,int force,int properties,int prefix,int preferred,int family);
    internal abstract object MagicRecord(int id);
}
internal abstract class ItemQualityRollServices
{
    internal abstract int Format{get;}
    internal abstract int DropQuality{get;}
    internal abstract bool Quest{get;}
    internal abstract bool Miscellaneous{get;}
    internal abstract int DropLevel{get;}
    internal abstract int BaseLevel{get;}
    internal abstract bool SuperiorFlag{get;}
    internal abstract ItemQualityRatio Ratio{get;}
    internal abstract uint Limited(int bound);
}

// Complete older quality roll,outer orchestration/finalizer and three reset
// helpers. Nested quality/property/world services are explicit prerequisites;
// this does not admit a complete generated NPC item or filter valid affixes.
internal static class SourceItemQuality
{
    internal static int Roll(ItemQualityRollServices s)
    {
        if(s.Format>=1&&s.DropQuality!=0){return s.DropQuality;}
        var r=s.Ratio??throw new InvalidOperationException("Missing source item ratio");
        if(s.Quest){return 2;}
        int level=s.DropLevel;
        if(s.Format>=1){level=s.Miscellaneous?1:Math.Max(level-s.BaseLevel,1);}
        int[] qualities=[7,6,5,4,3,2],values=[r.Unique,r.Rare,r.Set,r.Magic,r.Superior,r.Normal];
        int[] divisors=[r.UniqueDivisor,r.RareDivisor,r.SetDivisor,r.MagicDivisor,r.SuperiorDivisor,r.NormalDivisor];
        for(int i=0;i<qualities.Length;i++)
        {
            int chance=s.Format<1&&i<3?values[i]-level:values[i]-level/divisors[i];
            if(s.Limited(s.Format<1?Math.Max(chance,1):chance)==0){return qualities[i];}
        }
        return s.SuperiorFlag?3:1;
    }
    private static void Names(ItemQualityServices s)
    {for(int i=0;i<3;i++){s.Affix(true,i,0);s.Affix(false,i,0);}s.RareName(true,0);s.RareName(false,0);}
    internal static void Reset(ItemQualityServices s){Names(s);s.FileIndex(-1);}
    private static void Transition(ItemQualityServices s,uint seed,int quality)
    {Reset(s);s.StartSeed(seed);s.InitLow(seed);s.RollQuality();s.Quality=quality;s.DropQuality=quality;}
    internal static void NormalFallback(ItemQualityServices s,uint seed)
    {Transition(s,seed,2);s.NormalFallback();}
    internal static int SuperiorFallback(ItemQualityServices s,ref uint seed)
    {Transition(s,seed,3);seed=s.LowSeed;return s.Superior();}
    private static void Durability(ItemQualityServices s,int factor)
    {s.WriteStat(72,Math.Min(checked(factor*s.FullStat(72)),255));s.WriteStat(73,Math.Min(checked(factor*s.BaseStat(73)),255));}
    private static void MagicChain(ItemQualityServices s,uint seed)
    {
        if(s.Magic()!=0){return;}
        Transition(s,seed,3);seed=s.LowSeed;
        if(s.Superior()==0){NormalFallback(s,seed);}
    }
    private static void RareChain(ItemQualityServices s,uint seed)
    {
        if(s.Rare()!=0){return;}
        Transition(s,seed,4);seed=s.LowSeed;MagicChain(s,seed);
    }
    internal static int Finish(ItemQualityServices s,ItemQualityBase basis)
    {
        int quality=s.Quality;if(quality<=0||quality>=10){return 0;}
        if(s.Format>=100){s.MakeEthereal();}
        if(quality<=3){s.FinishNormal();}
        if(s.Format>=100&&(quality is 1 or 2 or 3 or 4 or 6 or 8 or 9)&&basis.AutoPrefix!=0)
        {
            int affix=s.PickAuto(0,1,0,1,0,basis.AutoPrefix);
            if(affix!=0){s.AutoAffix(affix);s.AssignProperties(0,s.MagicRecord(affix));}
        }
        return 1;
    }
    private static bool Ladder(ItemQualityServices s,ItemQualityUnique row)=>s.GameAllowsLadder||!row.Ladder;
    private static bool Unique(ItemQualityServices s,ItemQualityBase basis)
    {
        int format=s.Format;
        if(s.HasDurability&&format==0){Durability(s,5);}
        if(!s.Force)
        {
            if(format==0)
            {
                for(int i=0;i<s.UniqueCount;i++)
                {
                    var u=s.UniqueAt(i);
                    if(u.Version<100&&u.Enabled&&Ladder(s,u)&&basis.Code==u.Code&&(!s.Dropped(i)||basis.Quest))
                    {s.FileIndex(i);s.Identify(false);s.AssignProperties(3,null);return true;}
                }
            }
            else
            {
                var candidates=new List<(int Index,int Weight)>();int id=-1,total=0;
                for(int i=0;i<s.UniqueCount;i++)
                {
                    var u=s.UniqueAt(i);
                    if((u.Version<100||format>=100)&&u.Enabled&&basis.Code==u.Code&&Ladder(s,u)&&u.Level<=s.ItemLevel)
                    {
                        if(s.ForcedIndex!=0&&s.ForcedIndex-1==i){id=i;}
                        candidates.Add((i,total));total=checked(total+Math.Max(u.Rarity,1));
                    }
                }
                if(candidates.Count>0)
                {
                    if(id<0)
                    {int chance=s.Limited(total),i=1;while(i<candidates.Count&&chance>=candidates[i].Weight){i++;}id=candidates[i-1].Index;}
                    if(!basis.Quest&&s.Dropped(id)){s.FileIndex(-1);}
                    else
                    {
                        var u=s.FindUnique(id);
                        if(u!=null&&basis.Code==u.Code)
                        {
                            s.FileIndex(id);
                            if(s.CanDropUnique()){s.Identify(false);s.AssignProperties(3,null);return true;}
                            s.FileIndex(-1);
                        }
                    }
                }
                else
                {if(s.UniqueColumn){return true;}s.FileIndex(-1);}
            }
        }
        else
        {
            var u=s.FindUnique(s.ForcedIndex);
            if(u!=null)
            {
                s.FileIndex(s.ForcedIndex);
                if(u.Code==basis.Code){s.Identify(false);s.AssignProperties(3,null);}
                return true;
            }
        }
        return false;
    }
    internal static int Run(ItemQualityServices s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(!s.HasItem||!s.IsItem||s.Basis is not{} basis){return 0;}
        s.AutoAffix(0);Names(s);
        int quality=s.RollQuality();if(s.DropQuality!=0){quality=s.DropQuality;}s.Quality=quality;
        if(s.CanMagic){if(s.Quest){s.Quality=7;}else if(!s.MagicOrBetter){s.Quality=4;}}
        if(!s.CanRare&&s.Quality==6){s.Quality=4;}
        if(s.UniqueColumn){s.Quality=7;}if(s.CanNormal){s.Quality=2;}
        uint seed;
        switch(s.Quality)
        {
            case 1:seed=s.LowSeed;if(s.Inferior()==0){NormalFallback(s,seed);}break;
            case 2:s.Normal(s.Format==0);break;
            case 3:s.FileIndex(-1);seed=s.LowSeed;if(s.Superior()==0){NormalFallback(s,seed);}break;
            case 6:s.RareName(true,0);s.RareName(false,0);seed=s.LowSeed;RareChain(s,seed);break;
            case 4:seed=s.LowSeed;MagicChain(s,seed);break;
            case 7:
                s.FileIndex(-1);seed=s.LowSeed;
                if(Unique(s,basis)){return Finish(s,basis);}
                if(s.HasDurability&&s.Format>=1){Durability(s,3);}
                Transition(s,seed,6);seed=s.LowSeed;RareChain(s,seed);break;
            case 8:
                s.RareName(true,0);s.RareName(false,0);seed=s.LowSeed;if(s.Craft()==0){NormalFallback(s,seed);}break;
            case 9:
                s.RareName(true,0);s.RareName(false,0);seed=s.LowSeed;
                int prefix=s.Tempered(true),suffix=s.Tempered(false);
                if(prefix!=0&&suffix!=0){s.RareName(true,prefix);s.RareName(false,suffix);}else{NormalFallback(s,seed);}break;
            case 5:
                s.FileIndex(-1);seed=s.LowSeed;
                if(s.Set()==0)
                {
                    if(s.HasDurability&&s.Format>=1){Durability(s,2);}
                    Transition(s,seed,4);seed=s.LowSeed;
                    if(s.Magic()==0&&SuperiorFallback(s,ref seed)==0){NormalFallback(s,seed);}
                }
                break;
            default:return 0;
        }
        return Finish(s,basis);
    }
}
