#!/usr/bin/env python3
"""Real intrinsic A2 cancellation and issued missiles across warp/save/reload."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 connected=('death-mark-native-check','death-mark-native-reload-check','death-mark-interrupt-check','death-mark-interrupt-reload-check')
 check(stage=ROOT/'.local/class-skills/death-mark-interrupt',extra_families=('death-mark-native','death-mark-interrupt','tainted-casting'),
  extra_files={'tainted-cast-source-cases.json':ROOT/'.local/class-skills/tainted-fireball-cast-source/cases.json'},
  extra_connected_checks=(*connected,'tainted-casting-reload-check'),
  extra_wait_results={n:n+'-result'for n in connected}|{'tainted-casting-reload-check':'tainted-casting-reload-result'},
  extra_pre_save_commands=('death-mark-interrupt-save-check',),
  extra_inputs=(ROOT/'tests/check_death_mark_interrupt.py',ROOT/'tools/reference/death-mark-native-world-approval.json',ROOT/'tools/reference/tainted-fireball-cast-source-evidence.json'))
