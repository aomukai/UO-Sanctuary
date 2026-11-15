#!/usr/bin/env python3
"""Native Bind roster/quality audit and shared original-Revive facet control."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=('bind-demon-host-audit-check','bind-demon-host-audit-reload-check')
    check(stage=ROOT/'.local/class-skills/bind-demon-host-audit',extra_families=('revive-facet','bind-demon-host-audit'),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),))
