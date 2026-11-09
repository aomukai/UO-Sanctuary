#!/usr/bin/env python3
"""Learned Ward plus automatic native hits and actual right/quick packet input."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/psychic-ward-cast',extra_families=('ward-cast',),
          extra_target_input_commands=('ward-cast-right','ward-cast-quick'),
          extra_inputs=(ROOT/'tests/check_psychic_ward_cast.py',ROOT/'tools/reference/psychic-ward-reaction-approval.json'))
