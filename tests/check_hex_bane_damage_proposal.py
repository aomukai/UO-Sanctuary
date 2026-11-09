#!/usr/bin/env python3
"""Prepared Bane mastery/timing policy;no public Bane handler admitted."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/bane-damage-proposal',extra_families=('bane-damage-proposal',),
          extra_inputs=(ROOT/'tests/check_hex_bane_damage_proposal.py',))
