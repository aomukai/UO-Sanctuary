#!/usr/bin/env python3
"""Focused unapproved Ward reaction and SC review, preserving original pools."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/psychic-ward-proposal',extra_families=('psychic-ward-proposal',),
          extra_inputs=(ROOT/'tests/check_psychic_ward_proposal.py',))
