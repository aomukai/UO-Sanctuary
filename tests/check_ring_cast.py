#!/usr/bin/env python3
"""Approved learned Ring SC and actual left/right/Witch quick input with world saves."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 commands=tuple(name+suffix for name in ['ring-cast','sigil-cast']for suffix in ['-check','-reload-check'])
 check(stage=ROOT/'.local/class-skills/ring-cast',extra_families=('ring-cast','ring-world','sigil-cast'),
  extra_connected_checks=commands,extra_wait_results={name:name+'-result'for name in commands},extra_connection_settle=.05,
  extra_target_input_commands=('ring-cast-left','ring-cast-right','ring-cast-quick'),
  extra_inputs=(ROOT/'tests/check_ring_cast.py',ROOT/'tools/reference/ring-world-approval.json'))
