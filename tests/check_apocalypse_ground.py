#!/usr/bin/env python3
"""Prepared Apocalypse ground actor composition and source-state persistence."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/apocalypse-ground',extra_families=('apocalypse-area','apocalypse-potions','apocalypse-ground'),
  extra_files={'apocalypse-area-cases.json':ROOT/'.local/class-skills/apocalypse-area-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_apocalypse_ground.py',ROOT/'tools/reference/warlock-ground-launch-source-evidence.json',ROOT/'tools/reference/apocalypse-gate-current-evidence.json'))
