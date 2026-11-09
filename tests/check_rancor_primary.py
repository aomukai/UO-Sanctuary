#!/usr/bin/env python3
"""Isolated Rancor primary receiver plus ordered/direct damage regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/rancor-primary',extra_families=('rancor-primary','skill-damage-event'),
          extra_inputs=(ROOT/'tests/check_rancor_primary.py',))
