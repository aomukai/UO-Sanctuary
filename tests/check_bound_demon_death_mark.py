#!/usr/bin/env python3
"""Bound Death Mark plus retained original control/world and player actions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    families=('death-mark-world','death-mark-action','bound-demon-death-mark')
    names=tuple(n for f in families for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-death-mark',extra_families=families,extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/death-mark-action-approval.json'))
