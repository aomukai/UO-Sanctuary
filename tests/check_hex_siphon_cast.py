#!/usr/bin/env python3
"""Learned Siphon and approved shared groups with native events and packet input."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/hex-siphon-cast',extra_families=('siphon-cast','hex-group'),
          extra_target_input_commands=('siphon-cast-right','siphon-cast-quick'),
          extra_inputs=(ROOT/'tests/check_hex_siphon_cast.py',ROOT/'tools/reference/weapon-hex-groups-approval.json'))
