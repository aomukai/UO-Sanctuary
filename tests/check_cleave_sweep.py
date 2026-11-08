#!/usr/bin/env python3
"""Focused current Cleave sweep requests and original gear/save continuation."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/cleave-sweep',extra_families=('cleave-sweep',),
  extra_files={'cleave-sweep-cases.json':ROOT/'.local/class-skills/cleave-sweep-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_cleave_sweep.py',ROOT/'tools/reference/cleave-sweep-current-evidence.json'))
