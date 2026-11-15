#!/usr/bin/env python3
"""Bound numeric Consume plus original Consume,suppliers and bound Oath."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    families=('consume-suppliers','consume-world','bound-demon-oath','bound-demon-consume')
    names=tuple(n for f in families for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-consume',extra_families=families,extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-monsters-source.json'))
