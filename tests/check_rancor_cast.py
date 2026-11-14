#!/usr/bin/env python3
"""Approved learned Rancor: shared Sigil casting and authenticated inputs."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    commands=tuple(n+s for n in ['rancor-cast','rancor-pulse','sigil-family']for s in ['-check','-reload-check'])
    check(stage=ROOT/'.local/class-skills/rancor-cast',extra_families=('rancor-cast','rancor-pulse','rancor-mind','sigil-family'),
          extra_connected_checks=commands,extra_wait_results={n:n+'-result'for n in commands},extra_connection_settle=.05,
          extra_target_input_commands=('rancor-cast-left','rancor-cast-right','rancor-cast-quick'),
          extra_inputs=(ROOT/'tests/check_rancor_cast.py',ROOT/'tools/reference/rancor-pulse-approval.json'))
