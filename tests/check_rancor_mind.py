#!/usr/bin/env python3
"""Rancor shared-selector lease, primary consumers and existing Confuse regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/rancor-mind',extra_families=('rancor-mind','rancor-primary','confuse-cast'),
          extra_inputs=(ROOT/'tests/check_rancor_mind.py',))
