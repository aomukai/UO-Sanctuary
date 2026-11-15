#!/usr/bin/env python3
"""Bound affixes with retained typed native damage, Death Mark and Iron Spectral."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    connected=('bound-demon-typed','bound-demon-death-mark','bound-demon-affix')
    names=tuple(n for f in connected for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-affixes',extra_families=('iron-golem-spectral',*connected),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-monsters-source.json'))
