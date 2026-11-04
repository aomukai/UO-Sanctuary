#!/usr/bin/env python3
"""Approved Creeper curve with full retained field/body/cast and source controls."""
import check_creeper_stats as retained

if __name__=='__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/creeper-balance'
    retained.check(extra_commands=('creeper-balance-check','vine-cast-windup-check','recycler-casting-check'),
        extra_reload_commands=('creeper-balance-reload-check','vine-cast-windup-reload-check','recycler-casting-reload-check'),
        extra_fixtures=('creeper-balance-fixtures.json','vine-cast-windup-fixtures.json','recycler-casting-fixtures.json'),
        extra_live_commands=('vine-cast-windup-live',),readyless_live_commands=('vine-cast-windup-live',))
