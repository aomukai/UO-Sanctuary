#!/usr/bin/env python3
"""Native Death Mark world bridge proposal with actual followers/targeting."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/death-mark-native',extra_families=('death-mark-debuff','death-mark-native'),
  extra_connected_checks=('death-mark-native-check','death-mark-native-reload-check'),
  extra_wait_results={n:n+'-result'for n in ['death-mark-native-check','death-mark-native-reload-check']},
  extra_inputs=(ROOT/'tests/check_death_mark_native.py',ROOT/'tools/reference/death-mark-recipient-approval.json'))
