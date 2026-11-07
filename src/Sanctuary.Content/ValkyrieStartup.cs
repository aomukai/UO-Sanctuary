// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
namespace Sanctuary;

// The complete source stats initializer supplies100/75. The converted Amazon
// running entry adds100*Run/Walk-100 on a temporary list;mode changes remove it.
// Keep these bases separate from original item stats and live external effects.
internal static class ValkyrieStartup
{
    internal const string Profile="source-valkyrie-mode-entry-v1";
    private static readonly Lazy<(int Attack,int Walk,int Run)> Numbers=new(()=>
    {
        var animation=BalanceCatalog.ReadObject<ValkyrieAnimationData>("valkyrie-animation");
        if(animation is not{ReferenceVersion:"3.3.93847",State:93,GraphicsType:2,GraphicsClass:0,WalkVelocity:6,RunVelocity:9})
        {throw new InvalidDataException("Unsupported Valkyrie source startup graphics/character speeds");}
        return(100,75,checked(75+100*animation.RunVelocity/animation.WalkVelocity-100));
    });
    internal static (int Attack,int Walk,int Run) Rates=>Numbers.Value;
    internal static void Require(ValkyrieBodyState body)
    {
        if(body?.Initialization==null){throw new NotSupportedException("Derived Valkyrie startup requires its original source initialization witness");}
        ValkyrieInitializer.Validate(body.Initialization,body.Birth,body.Equipment);
    }
    internal static void Validate(ValkyrieBodyState body)
    {
        if(body.GroundMotion?.StartupProfile==null){return;}
        Require(body);var rates=Rates;var motion=body.GroundMotion;
        if(motion.StartupProfile!=Profile||motion.AttackRate!=rates.Attack||motion.WalkVelocityPercent!=rates.Walk||motion.RunVelocityPercent!=rates.Run)
        {throw new InvalidDataException("Derived Valkyrie startup rates changed or lost their source profile");}
    }
}
