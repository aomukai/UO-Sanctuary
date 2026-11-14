#!/usr/bin/env python3
"""Engorge numeric capture in the existing disposable native fixture."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('engorge-suppliers-check','engorge-suppliers-reload-check')
    check(stage=ROOT/'.local/class-skills/engorge-suppliers',extra_families=('engorge-suppliers',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/engorge-source-review.json'))
