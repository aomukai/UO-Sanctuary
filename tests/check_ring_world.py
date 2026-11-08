#!/usr/bin/env python3
"""Candidate Ring world damage/knockback/lifetime and original final-frame saves."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
 check(stage=ROOT/'.local/class-skills/ring-world',extra_families=('ring-world',),extra_inputs=(ROOT/'tests/check_ring_world.py',))
