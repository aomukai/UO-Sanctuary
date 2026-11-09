#!/usr/bin/env python3
"""Approved Blade Warp mechanics and actual left/right/quick packet input."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/blade-warp',extra_families=('blade-warp',),
          extra_target_input_commands=('blade-warp-left','blade-warp-right','blade-warp-quick'),
          extra_inputs=(ROOT/'tests/check_blade_warp.py',ROOT/'tools/reference/blade-warp-adaptation.json'))
