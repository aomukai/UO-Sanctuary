#!/usr/bin/env python3
"""Focused current Apocalypse area/filter and existing packet actual-HP checks."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/apocalypse-area',extra_families=('apocalypse-packets','apocalypse-area'),
  extra_files={'apocalypse-area-cases.json':ROOT/'.local/class-skills/apocalypse-area-source/cases.json',
               'apocalypse-maximum-cases.json':ROOT/'.local/class-skills/apocalypse-maximum-source/cases.json',
               'spell-packet-roll-cases.json':ROOT/'.local/class-skills/spell-packet-roll-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_apocalypse_area.py',ROOT/'tools/reference/apocalypse-area-current-evidence.json'))
