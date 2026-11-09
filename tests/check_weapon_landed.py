#!/usr/bin/env python3
"""Landed weapon, prepared Purge and ordered/generic damage regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/weapon-landed',extra_families=('weapon-landed','skill-damage-event'),
          extra_inputs=(ROOT/'tests/check_weapon_landed.py',))
