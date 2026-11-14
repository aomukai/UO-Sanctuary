#!/usr/bin/env python3
"""Approved learned Purge, automatic production events and prepared regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    commands=tuple('purge-cast'+s for s in ['-check','-reload-check'])
    check(stage=ROOT/'.local/class-skills/hex-purge-cast',extra_families=('purge-cast','purge-reaction','weapon-landed'),
          extra_connected_checks=commands,extra_wait_results={n:n+'-result'for n in commands},extra_connection_settle=.05,
          extra_target_input_commands=('purge-cast-right','purge-cast-quick'),
          extra_inputs=(ROOT/'tests/check_hex_purge_cast.py',ROOT/'tools/reference/hex-purge-reaction-approval.json'))
