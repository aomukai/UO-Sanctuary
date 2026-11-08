#!/usr/bin/env python3
"""Apocalypse shared potion mitigation with prior impact and area regressions."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/apocalypse-potions',extra_families=('apocalypse-impact','apocalypse-area','apocalypse-potions'),
  extra_files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json',
               'apocalypse-area-cases.json':ROOT/'.local/class-skills/apocalypse-area-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_apocalypse_potions.py',ROOT/'tools/reference/apocalypse-gate-current-evidence.json'))
