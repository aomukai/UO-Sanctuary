#!/usr/bin/env python3
"""Shared prepared SC vine windups and retained actual learned Grizzly casting."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/vine-cast-windup',
        extra_families=('vine-cast-windup','creeper-casting','recycler-casting'),
        extra_checks=('vine-cast-windup-live-check','grizzly-upgrades-live-check'),
        extra_connected_checks=('vine-cast-windup-live-check','grizzly-upgrades-live-check'),
        extra_wait_results={'vine-cast-windup-live-check':'vine-cast-windup-live-result','grizzly-upgrades-live-check':'grizzly-upgrades-live-result'},
        extra_inputs=(Path(__file__).resolve(),))
