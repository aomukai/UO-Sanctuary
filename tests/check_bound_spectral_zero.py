#!/usr/bin/env python3
"""Bound zero-direct Spectral bridge with Iron, Ward and typed native controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    connected=('bound-demon-typed','bound-demon-affix')
    names=tuple(n for f in connected for n in(f+'-check',f+'-reload-check'))
    check(stage=ROOT/'.local/class-skills/bind-demon-spectral-zero',extra_families=('iron-golem-spectral','psychic-ward-proposal',*connected),extra_connected_checks=names,
        extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bound-demon-affix-source-review.json'))
