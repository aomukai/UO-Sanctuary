// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Engines.Spawners;
using Server.Mobiles;
namespace Sanctuary;

internal static class ReviveWorldControllers
{
    // BaseSpawner.Remove is the ordinary native deletion unlink. Its default
    // defrag removes registry entries without deleting the retained actor.
    // Custom controller/defrag semantics require their own adapter.
    internal static string Issue(ISpawner spawner)=>spawner==null?null:
        spawner is BaseSpawner&&spawner.GetType().GetMethod(nameof(BaseSpawner.OnDefragSpawn),[typeof(ISpawnable),typeof(bool)])?.DeclaringType==typeof(BaseSpawner)?null:
        "Unsupported Revive world controller: "+spawner.GetType().FullName;
    internal static void DetachDeath(BaseCreature actor)
    {
        if(actor is not{Deleted:false,Hits:<=0}){throw new InvalidOperationException("World unlink requires an actual dead original");}
        var spawner=actor.Spawner;
        if(Issue(spawner) is{} issue){throw new NotSupportedException(issue);}
        spawner?.Remove(actor);actor.Spawner=null;
    }
}
