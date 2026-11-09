#!/usr/bin/env python3
"""Learned Bane with actual result hooks,approved groups and Siphon regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/hex-bane-cast',extra_families=('bane-cast','siphon-cast','hex-group'),
          extra_target_input_commands=('bane-cast-right','bane-cast-quick','siphon-cast-right','siphon-cast-quick'),
          extra_files={'bane-zero-legacy.json':ROOT/'.local/class-skills/bane-damage-proposal/bane-damage-proposal-fixtures.json'},
          extra_inputs=(ROOT/'tests/check_hex_bane_cast.py',ROOT/'tools/reference/hex-bane-damage-approval.json'))
