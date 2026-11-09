#!/usr/bin/env python3
"""Ordered hit reentrancy, generic damage regression and full Cleave controls."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/ordered-damage-events',extra_families=('skill-damage-event','cleave-action'),
          extra_inputs=(ROOT/'tests/check_ordered_damage_events.py',))
