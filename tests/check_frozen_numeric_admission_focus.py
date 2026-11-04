#!/usr/bin/env python3
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/frozen-numeric-admission-focus',extra_families=('revive-equipment-pools','revive-equipment-life','frozen-equipment-combat'),extra_inputs=(Path(__file__).resolve(),))
