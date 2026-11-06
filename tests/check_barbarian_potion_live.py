#!/usr/bin/env python3
"""Natural Find Potion release and original save controls on the class build."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration():
 return dict(stage=ROOT/'.local/class-skills/barbarian-current-potion-live',
  extra_families=('find-potion',),
  extra_files={'find-potion-native-source-probe.json':ROOT/'.local/class-skills/find-potion-native-source-probe.json'},
  extra_live_commands=('find-potion-live',),checked_live_commands=('find-potion-live',),
  extra_inputs=(Path(__file__).resolve(),))
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
