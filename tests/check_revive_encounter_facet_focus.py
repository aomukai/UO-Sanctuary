#!/usr/bin/env python3
"""Original encounter birth,facet projection and two real reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/revive-encounter-facet-focus',extra_families=('revive-encounter-facet','revive-facet'),
          extra_checks=('revive-encounter-facet-live-check','revive-facet-live-check'),
          extra_connected_checks=('revive-encounter-facet-live-check','revive-facet-live-check'),
          extra_wait_results={'revive-encounter-facet-live-check':'revive-encounter-facet-live-result','revive-facet-live-check':'revive-facet-live-result'},
          extra_inputs=(Path(__file__).resolve(),))
