#!/usr/bin/env python3
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/revive-event-focus',extra_families=('revive-equipment-combat','revive-equipment-pools','frozen-equipment-combat','revive-equipment-life','revive-equipment-offense','revive-random','revive-impact','revive-defense','revive-event'),extra_inputs=(Path(__file__).resolve(),))
