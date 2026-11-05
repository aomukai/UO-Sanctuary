// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record SuperiorItemRecord(int Row,bool Armor,bool Weapon,bool Shield,bool Scepter,bool Wand,bool Staff,bool Bow,bool Boots,bool Gloves,bool Belt,AffixModifier[] Modifiers);
internal sealed record ItemFinishingBase(int Durability,bool NoDurability);
internal abstract class ItemFinishingServices
{
    internal abstract bool HasItem{get;}
    internal abstract ItemFinishingBase Basis{get;}
    internal abstract int Type{get;}
    internal abstract bool IsType(int type);
    internal abstract int Format{get;}
    internal abstract int Quality{get;}
    internal abstract bool Throwable{get;}
    internal abstract bool HasDurability{get;}
    internal abstract bool HasSuperiorTable{get;}
    internal abstract int SuperiorCount{get;}
    internal abstract SuperiorItemRecord SuperiorRecord(int index);
    internal abstract void SuperiorProperties(SuperiorItemRecord record);
    internal abstract bool HasInferiorTable{get;}
    internal abstract int InferiorCount{get;}
    internal abstract uint ItemLimited(int bound);
    internal abstract uint UnitLimited(int bound);
    internal abstract uint ItemRandom();
    internal abstract int BaseStat(int stat);
    internal abstract int FullStat(int stat);
    internal abstract int ItemOrSkillStat(int stat);
    internal abstract void WriteStat(int stat,int value);
    internal abstract void FileIndex(int id);
    internal abstract void StaffMods();
    internal abstract void Ethereality();
    internal abstract bool Quest{get;}
    internal abstract uint Flags1{get;}
    internal abstract uint Flags2{get;}
    internal abstract bool Socketable{get;}
    internal abstract bool Stackable{get;}
    internal abstract uint MaxSockets{get;}
    internal abstract int Difficulty{get;}
    internal abstract uint StartSeed{get;}
    internal abstract void Flag(uint flag,bool value);
    internal abstract void AddSockets(uint count);
    internal abstract bool HasDropUnit{get;}
    internal abstract int DropUnitClass{get;}
    internal abstract int DropIndex{get;}
    internal abstract void Charm();
    internal abstract int Book(bool scroll);
    internal abstract void Suffix(int slot,int id);
}
// Complete older inferior/superior,eligibility,socket,ethereal and normal
// dispatch bodies. Staffmods/charm/book/property services remain explicit.
internal static class SourceItemFinishing
{
    internal static bool CanBeSuperior(ItemFinishingServices s,SuperiorItemRecord r)
    {
        int type=s.Type;
        if(!r.Weapon||!s.IsType(45)||type is 26 or 27 or 35 or 24 or 25)
        {
            if(!r.Armor||!s.IsType(50)||type is 2 or 15 or 16 or 19)
            {
                return r.Shield&&type==2||r.Scepter&&type==24||r.Wand&&type==25||r.Staff&&type==26||r.Bow&&(type==27||type==35)
                    ||r.Boots&&type==15||r.Gloves&&type==16||r.Belt&&type==19;
            }
        }
        return true;
    }
    internal static int Superior(ItemFinishingServices s)
    {
        if(!s.HasSuperiorTable||s.Basis is not{} basis){return 0;}
        int count=s.Throwable||basis.NoDurability?4:s.SuperiorCount;
        if(count is<0 or>12){throw new NotSupportedException("Superior source checked-array count is outside0..12");}
        var used=new bool[12];
        while(true)
        {
            int id;do{id=(int)s.ItemLimited(count);}while(used[id]);
            var row=s.SuperiorRecord(id);if(row==null){return 0;}
            if(CanBeSuperior(s,row)){s.FileIndex(id);s.SuperiorProperties(row);s.StaffMods();return 1;}
            used[id]=true;if(count<=0){return 0;}int i=0;while(used[i]){if(++i>=count){return 0;}}
        }
    }
    internal static int InferiorCore(ItemFinishingServices s)
    {
        if(!s.HasInferiorTable||s.Basis is not{} basis){return 0;}
        s.FileIndex((int)s.ItemLimited(s.InferiorCount));
        if(s.HasDurability)
        {
            int maximum=Math.Max(basis.Durability/3,1);
            int current=(int)Math.Max(s.UnitLimited(maximum/2)+(uint)(maximum/2),1u);s.WriteStat(72,current);s.WriteStat(73,maximum);
        }
        if(s.IsType(45))
        {
            s.WriteStat(22,Math.Max(75*s.BaseStat(22)/100,2));s.WriteStat(21,Math.Max(75*s.BaseStat(21)/100,1));
            s.WriteStat(24,Math.Max(75*s.BaseStat(24)/100,2));s.WriteStat(23,Math.Max(75*s.BaseStat(23)/100,1));
            if(!s.Throwable){return 1;}
            s.WriteStat(159,Math.Max(75*s.ItemOrSkillStat(159)/100,2));s.WriteStat(160,Math.Max(75*s.ItemOrSkillStat(160)/100,1));return 1;
        }
        if(s.IsType(50)){s.WriteStat(31,Math.Max(75*s.BaseStat(31)/100,1));return 1;}return 0;
    }
    internal static int InferiorModern(ItemFinishingServices s)
    {if(!s.HasItem){return 0;}int result=InferiorCore(s);s.StaffMods();return result;}
    internal static int Inferior(ItemFinishingServices s)=>s.Format<1?InferiorCore(s):InferiorModern(s);
    internal static void Sockets(ItemFinishingServices s)
    {
        if(s.Quality<2||!s.Socketable||s.Stackable){return;}
        uint maximum=s.MaxSockets;maximum=s.Difficulty switch{0=>Math.Min(maximum,3u),1=>Math.Min(maximum,4u),2=>Math.Min(maximum,6u),_=>maximum};
        if(maximum==0){return;}
        if(s.Format>=100||!s.IsType(3))
        {
            uint chance=s.ItemRandom()%100;
            if((s.Flags2&8)==0)
            {
                if((s.Flags2&16)!=0){chance=0;}
                if((s.Flags1&0x800)!=0||chance<33)
                {
                    s.Flag(0x800,true);
                    if(s.Format<1){maximum=Math.Min(maximum,3u);if(s.IsType(37)){maximum=Math.Min(maximum,2u);}s.AddSockets(maximum);}
                    else{s.AddSockets(s.StartSeed%maximum+1);}
                }
            }
        }
    }
    internal static void Ethereal(ItemFinishingServices s)
    {
        if((s.Flags2&2)!=0||!s.IsType(45)&&!s.IsType(50)||!s.HasDurability){return;}
        if(s.Quality is 1 or 5||s.Quest){return;}
        bool apply=s.ItemRandom()%100<5;if((s.Flags1&0x400000)!=0){apply=true;}
        if((s.Flags2&4)!=0||apply)
        {
            s.Ethereality();
            if(s.HasDurability){s.WriteStat(73,s.BaseStat(73)/2+1);s.WriteStat(72,s.FullStat(73));}
        }
    }
    private static void BodyPart(ItemFinishingServices s,bool ear)
    {s.FileIndex(s.HasDropUnit?s.DropUnitClass:s.DropIndex);if(ear){s.Flag(0x10000,true);}}
    internal static void NormalLegacy(ItemFinishingServices s)
    {
        switch(s.Type)
        {
            case 13:s.Charm();break;case 40:BodyPart(s,false);break;case 7:BodyPart(s,true);break;
            case 22:s.Suffix(0,s.Book(true));break;case 18:s.Suffix(0,s.Book(false));break;
            default:s.StaffMods();Sockets(s);break;
        }
    }
    internal static void NormalModern(ItemFinishingServices s)
    {
        if(s.IsType(13)){s.Charm();}if(s.IsType(40)){BodyPart(s,false);}if(s.IsType(7)){BodyPart(s,true);}
        if(s.IsType(22)){s.Suffix(0,s.Book(true));}if(s.IsType(18)){s.Suffix(0,s.Book(false));}s.StaffMods();
    }
    internal static void Normal(ItemFinishingServices s){if(s.Format!=0){NormalModern(s);}else{NormalLegacy(s);}}
}
