// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Sanctuary;

internal sealed record ItemInitializationDrop(int Level,int QuantityOverride);
internal sealed record ItemInitializationBase(int Block,int Speed,int Durability,int MinArmor,int MaxArmor,
    int Minimum,int Maximum,int SecondaryMinimum,int SecondaryMaximum,int ThrowMinimum,int ThrowMaximum,bool Quest,bool QuestDifficulty);
internal abstract class ItemInitializationServices
{
    internal abstract int Type{get;}
    internal abstract bool Quiver{get;}
    internal abstract bool Armor{get;}
    internal abstract bool Weapon{get;}
    internal abstract bool Stackable{get;}
    internal abstract bool MagicOrBetter{get;}
    internal abstract int MinStack{get;}
    internal abstract int MaxStack{get;}
    internal abstract int SpawnStack{get;}
    internal abstract int Varieties{get;}
    internal abstract ItemInitializationBase Record{get;}
    internal abstract bool HasItem{get;}
    internal abstract uint Limited(int bound);
    internal abstract void Write(int stat,int value);
    internal abstract void Gold(int value);
    internal abstract void Elixir();
    internal abstract void Variety(int value);
    internal abstract int Quality();
    internal abstract bool HasMagicList{get;}
    internal abstract void AllocateMagicList(bool hasItem);
    internal abstract void PostMagicList();
    internal abstract int Difficulty{get;}
    internal abstract void WriteMagic(int stat,int value);
    internal abstract void Identify();
}

// Complete older D2GAME_InitItemStats_6FC4E520 control flow. Quality creation,
// gold/elixir and quest-list allocation remain mandatory caller services;
// this driver does not substitute for the complete generated-item factory.
internal static class SourceItemInitialization
{
    internal static int Run(ItemInitializationServices s,ItemInitializationDrop drop,bool generateQuality)
    {
        ArgumentNullException.ThrowIfNull(s);
        if(s.Type==4)
        {
            int quantity=1;
            if(drop!=null)
            {
                quantity=unchecked((int)Math.Max(s.Limited(5*drop.Level)+(uint)drop.Level,1u));
                if(drop.QuantityOverride>0){quantity=drop.QuantityOverride;}
            }
            s.Gold(quantity);
        }
        else if(s.Quiver)
        {
            int quantity=unchecked((int)Math.Max((uint)s.MinStack+s.Limited(s.MaxStack-s.MinStack),1u));
            if(drop is{QuantityOverride:>0}){quantity=drop.QuantityOverride;}
            s.Write(70,quantity);
        }
        else
        {
            var b=s.Record??throw new InvalidOperationException("Item initialization requires its source base record");
            if(s.Armor)
            {
                s.Write(20,b.Block);s.Write(67,-b.Speed);
                uint durability=(uint)b.Durability>>1;
                s.Write(72,(int)Math.Min(s.Limited((int)durability)+durability,255u));s.Write(73,Math.Min(b.Durability,255));
                s.Write(31,b.MinArmor+(int)s.Limited(b.MaxArmor-b.MinArmor+1));
            }
            else if(s.Weapon)
            {
                if(s.Stackable)
                {
                    int quantity=unchecked((int)s.Limited(s.MaxStack-s.MinStack)+s.MinStack);
                    if(drop is{QuantityOverride:>0}){quantity=drop.QuantityOverride;}
                    quantity=Math.Max(quantity,1);if(s.MagicOrBetter){quantity=1;}s.Write(70,quantity);
                }
                uint durability=(uint)b.Durability>>1;
                s.Write(72,(int)Math.Min(s.Limited((int)durability)+durability,255u));s.Write(73,Math.Min(b.Durability,255));
                if(b.Maximum!=0){s.Write(22,b.Maximum);}if(b.Minimum!=0){s.Write(21,b.Minimum);}
                if(b.SecondaryMinimum!=0){s.Write(23,b.SecondaryMinimum);}if(b.SecondaryMaximum!=0){s.Write(24,b.SecondaryMaximum);}
                if(b.ThrowMaximum!=0){s.Write(159,b.ThrowMinimum);s.Write(160,b.ThrowMaximum);}
                s.Write(68,-b.Speed);
            }
            else
            {
                if(s.Stackable)
                {
                    int spawn=s.SpawnStack;if(spawn<s.MinStack||spawn==0){spawn=Math.Max(s.MinStack,s.MaxStack);}
                    int quantity=unchecked((int)s.Limited(spawn-s.MinStack)+s.MinStack);
                    if(drop is{QuantityOverride:>0}){quantity=drop.QuantityOverride;}
                    if(s.MagicOrBetter||quantity<1){quantity=1;}s.Write(70,quantity);
                }
                if(s.Type==11){s.Elixir();}
            }
        }
        if(s.Varieties!=0){s.Variety((int)s.Limited(s.Varieties));}
        int result=1;
        if(generateQuality&&drop!=null)
        {
            result=s.Quality();
            if(s.Record is{Quest:true,QuestDifficulty:true})
            {
                if(!s.HasMagicList){s.AllocateMagicList(s.HasItem);s.PostMagicList();}
                s.WriteMagic(356,s.Difficulty);s.Identify();
            }
        }
        return result;
    }
}
