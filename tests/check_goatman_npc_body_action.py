#!/usr/bin/env python3
"""Original Goatman speed-state persistence and approved action regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/goatman-npc-body-action',
          extra_families=('goatman-npc-body-action','goatman-body'),
          extra_checks=('goatman-npc-action-check',),
          extra_inputs=(Path(__file__).resolve(),))
