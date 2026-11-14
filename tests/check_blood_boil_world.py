#!/usr/bin/env python3
"""Prepared Blood Boil in the existing disposable native fixture."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('blood-boil-world-check','blood-boil-world-reload-check')
    check(stage=ROOT/'.local/class-skills/blood-boil-world',extra_families=('blood-boil-world',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/blood-boil-source-review.json'))
