#!/usr/bin/env python3
"""Approved learned Sigil Death with actual input, corpses and shared regressions."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    commands=tuple(n+s for n in ['death-cast','sigil-death','sigil-family']for s in ['-check','-reload-check'])
    check(stage=ROOT/'.local/class-skills/death-cast',extra_families=('death-cast','sigil-death','sigil-family','corpse-town'),
          extra_connected_checks=commands,extra_wait_results={n:n+'-result'for n in commands},extra_connection_settle=.05,
          extra_target_input_commands=('death-cast-left','death-cast-right','death-cast-quick'),
          extra_inputs=(ROOT/'tests/check_death_cast.py',ROOT/'tools/reference/sigil-death-rule-approval.json'))
