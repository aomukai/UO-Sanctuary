#!/usr/bin/env python3
"""Bind Demon imported monster and captured stat suppliers in disposable world."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('bind-demon-suppliers-check','bind-demon-suppliers-reload-check')
    check(stage=ROOT/'.local/class-skills/bind-demon-suppliers',extra_families=('bind-demon-suppliers',),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-monsters-source.json'))
