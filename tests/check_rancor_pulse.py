#!/usr/bin/env python3
"""Prepared Rancor pulses, shared selector and old Sigil family/save regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    commands=tuple(n+s for n in ['rancor-pulse','sigil-family']for s in ['-check','-reload-check'])
    check(stage=ROOT/'.local/class-skills/rancor-pulse',extra_families=('rancor-pulse','rancor-mind','sigil-family'),
          extra_connected_checks=commands,extra_wait_results={n:n+'-result'for n in commands},extra_connection_settle=.05,
          extra_inputs=(ROOT/'tests/check_rancor_pulse.py',))
