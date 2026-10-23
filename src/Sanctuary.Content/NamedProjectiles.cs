// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;

namespace Sanctuary;

public static class NamedProjectiles
{
    public static int PierceBudget(int chance, Random random)
    {
        int budget = 0;
        while (budget < 4 && chance > 0 && random.Next(100) < Math.Min(100, chance)) { budget++; }
        return budget;
    }
    public static int Budget(PlayerMobile source, Random random = null) => NamedCombat.Ranged(source)
        ? PierceBudget(ClassGear.Bonus(source, "pierce"), random ?? Random.Shared) : 0;
    internal static Point3D Endpoint(PlayerMobile source, Point3D aim, int range)
    {
        int dx = aim.X - source.X, dy = aim.Y - source.Y, distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (distance == 0) { return source.Location; }
        var result = source.Location;
        for (int step = 1; step <= range; step++)
        {
            var next = new Point3D(source.X + (int)Math.Round((double)dx * step / distance),
                source.Y + (int)Math.Round((double)dy * step / distance), source.Z);
            if (next.X < 0 || next.Y < 0 || next.X >= source.Map.Width || next.Y >= source.Map.Height
                || !source.InLOS(new Point3D(next.X, next.Y, next.Z + 14))) { break; }
            result = next;
        }
        return result;
    }
    // Continuation belongs to the released projectile: no new attack cycle, ammunition or weapon wear.
    public static int Continue(PlayerMobile source, Mobile first, Point3D aim, int budget, Random random = null,bool? projectileCritical=null,int? skillPhysicalPercent=null,int skillAttackPercent=0,Point3D? collisionPoint=null)
    {
        if (budget <= 0 || first == null || source is not { Alive: true, Deleted: false } || !NamedCombat.Ranged(source)) { return 0; }
        random ??= Random.Shared;
        var start = source.Location; int range = CombatTiming.Range(source, CombatAbility.BasicAttack);
        var end = Endpoint(source, aim, range);
        if (!CombatHitboxes.Intersect(start, end, collisionPoint??aim, out double entry)) { return 0; }
        Span<Serial> visited = stackalloc Serial[5]; visited[0] = first.Serial;
        int attempts = 0; var previous = collisionPoint??aim;
        while (attempts < Math.Min(4, budget))
        {
            Mobile next = null; double nearest = double.MaxValue;
            foreach (var candidate in source.Map.GetMobilesInRange<Mobile>(start, range))
            {
                bool seen = false;
                for (int i = 0; i <= attempts; i++) { if (visited[i] == candidate.Serial) { seen = true; break; } }
                if (seen || !ClassCombat.IsHostile(source, candidate) || !source.InLOS(candidate)
                    || !CombatHitboxes.Intersect(start, end, candidate.Location, out double hit) || hit < entry
                    || !source.InRange(candidate, range)) { continue; }
                if (hit < nearest || hit == nearest && (next == null || candidate.Serial.Value < next.Serial.Value))
                { next = candidate; nearest = hit; }
            }
            if (next == null) { break; }
            visited[++attempts] = next.Serial; entry = nearest;
            var point = next.Location;
            Effects.SendMovingEffect(new Entity(Serial.Zero, previous, source.Map), next, 0xF42, 5, 0, false, false);
            source.DoHarmful(next, true);
            if (ClassStats.CheckHit(source, next,skillAttackPercent) == true) { GearEffects.Strike(source, next, Utility.RandomDouble(), random, false,skillPhysicalPercent:skillPhysicalPercent,projectileCritical:projectileCritical); }
            CombatDamage.Finish(next, source); previous = point;
            if (source.Deleted || !source.Alive) { break; }
        }
        return attempts;
    }
}
