#!/usr/bin/env python3
"""Learned Apocalypse/start14 fee and actual left/right/quick delayed impact."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 commands=tuple(name+suffix for name in ['apocalypse-cast','sigil-cast']for suffix in ['-check','-reload-check'])
 check(stage=ROOT/'.local/class-skills/apocalypse-cast',extra_families=('apocalypse-cast','apocalypse-ground','apocalypse-potions','sigil-cast'),
  extra_connected_checks=commands,extra_wait_results={name:name+'-result'for name in commands},extra_connection_settle=0.05,
  extra_target_input_commands=('apocalypse-cast-left','apocalypse-cast-right','apocalypse-cast-quick'),
  extra_inputs=(ROOT/'tests/check_apocalypse_cast.py',ROOT/'tools/reference/warlock-ground-launch-source-evidence.json'))
