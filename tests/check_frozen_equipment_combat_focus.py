#!/usr/bin/env python3
"""Frozen NPC combat suppliers and actual accepted-death snapshot retention."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/frozen-equipment-combat-focus',extra_families=('frozen-equipment-combat','revive-before-death'),
          extra_inputs=(Path(__file__).resolve(),))
