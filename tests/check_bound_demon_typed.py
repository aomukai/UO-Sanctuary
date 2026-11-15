#!/usr/bin/env python3
"""Typed original Imp plus retained original bound-host/Revive facet cohort."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('bound-demon-host-check','bound-demon-host-reload-check','bound-demon-typed-check','bound-demon-typed-reload-check')
    check(stage=ROOT/'.local/class-skills/bind-demon-typed',extra_families=('ward-cast','revive-facet','bound-demon-host','bound-demon-typed'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-monsters-source.json'))
