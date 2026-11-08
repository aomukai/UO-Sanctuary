// SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace Sanctuary;

internal sealed record GenericPetInitializationContext(int Game,byte Selector,int Pet,int Owner,
    GenericPetCommandUnit Command,GenericPetDecisionMemory Control,GenericPetParameters Parameters);
internal sealed record GenericPetInitializationServices(Func<int,int,int,int> Resolve,Func<int,int> Property,
    Func<int,int> Kind,Action<int,Action<int>> OwnerUnits,GenericPetOwnerSurveyServices Survey);
internal sealed record GenericPetInitializedFrame(GenericPetDecisionFrame Decision,GenericPetOwnerSurveyState Survey,
    int Property64,int Property68,GenericPetGatherState Gather);

// Current4b0b90 composition over the existing decoder,getter and owner callback.
// A fresh managed frame replaces its empty-default copy/vector lifetime;
// properties,owner membership/order and survey leaves remain explicit services.
internal static class GenericPetInitialization
{
    internal static GenericPetInitializedFrame Run(GenericPetInitializationContext c,GenericPetInitializationServices s)
    {
        ArgumentNullException.ThrowIfNull(c);ArgumentNullException.ThrowIfNull(c.Control);ArgumentNullException.ThrowIfNull(s);
        var parameters=c.Parameters??GenericPetParameters.Decode(null);
        int target=GenericPetTargetCommand.Get(c.Game,c.Command,s.Resolve);
        var memory=new GenericPetDecisionMemory{Target=target,Value0=c.Control.Value0,Value1=c.Control.Value1,Value2=c.Control.Value2};
        GenericPetTargetCommand.Clear(c.Command);
        int property64=s.Property(c.Pet),property68=0;
        var survey=new GenericPetOwnerSurveyState(c.Pet,parameters.SwarmRaw);
        if(c.Owner!=0)
        {
            property68=s.Property(c.Owner);
            if(s.Kind(c.Owner)==0)
            {GenericPetOwnerSurvey.Enumerate(c.Selector,survey,visit=>s.OwnerUnits(c.Owner,visit),s.Survey);}
        }
        return new(new(parameters,memory),survey,property64,property68,new(0));
    }
    internal static GenericPetRootServices Bind(GenericPetRootServices root,
        Func<GenericPetDecisionUnit,GenericPetInitializationContext> context,GenericPetInitializationServices services,
        Action<GenericPetInitializedFrame> prepared)
    {
        ArgumentNullException.ThrowIfNull(root);ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(prepared);
        return root with{Initialize=owner=>{var frame=Run(context(owner),services);prepared(frame);return frame.Decision;}};
    }
}
