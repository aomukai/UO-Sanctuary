#!/usr/bin/env python3
"""Prepared Consume owner buff,shared pool and town roster UI in disposable native world."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('consume-world-check','consume-world-reload-check')
    check(stage=ROOT/'.local/class-skills/consume-world',extra_families=('consume-world',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/d2r-consume/sources.json'))
