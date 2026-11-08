#!/usr/bin/env python3
"""Current resistance helper and prepared Apocalypse actual HP/resistance lifecycle."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/apocalypse-impact',extra_families=('death-mark-debuff','apocalypse-impact'),
  extra_files={'skill-resistance-cases.json':ROOT/'.local/class-skills/skill-resistance-source/cases.json'},
  extra_inputs=(ROOT/'tests/check_apocalypse_impact.py',ROOT/'tools/reference/skill-resistance-current-evidence.json'))
