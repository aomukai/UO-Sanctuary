#!/usr/bin/env python3
"""Focused passive checks;reuse prior source and active-input evidence."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('demonic-mastery-check','demonic-mastery-reload-check')
    check(stage=ROOT/'.local/class-skills/demonic-mastery',extra_families=('demonic-mastery',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/demonic-mastery-source-review.json'))
