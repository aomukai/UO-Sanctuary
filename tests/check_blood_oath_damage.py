#!/usr/bin/env python3
"""Prepared incoming redirection in the existing disposable native receiver."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('blood-oath-damage-check','blood-oath-damage-reload-check')
    check(stage=ROOT/'.local/class-skills/blood-oath-damage',extra_families=('blood-oath-damage',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/blood-oath-source-review.json'))
