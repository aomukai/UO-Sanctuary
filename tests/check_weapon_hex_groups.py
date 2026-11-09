#!/usr/bin/env python3
"""Focused prepared Hex groups;public routing stays disabled pending approval."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/weapon-hex-groups',extra_families=('hex-group',),
          extra_inputs=(ROOT/'tests/check_weapon_hex_groups.py',))
