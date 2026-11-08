#!/usr/bin/env python3
"""Current Death Mark composition over explicit services in the native host."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-cast',extra_families=('death-mark-cast',),
  extra_files={'death-mark-cast-cases.json':ROOT/'.local/class-skills/death-mark-cast-source/cases.json'},
  extra_inputs=(Path(__file__).resolve(),ROOT/'tools/probe_death_mark_cast.py',ROOT/'tools/reference/death-mark-cast-current-evidence.json'))
