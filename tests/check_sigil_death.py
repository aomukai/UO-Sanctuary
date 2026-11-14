#!/usr/bin/env python3
"""Prepared complete Death proposal with shared Sigil/corpse regressions."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    commands=tuple(n+s for n in ['sigil-death','sigil-family']for s in ['-check','-reload-check'])
    check(stage=ROOT/'.local/class-skills/sigil-death-proposal',extra_families=('sigil-death','sigil-family','corpse-town'),
          extra_connected_checks=commands,extra_wait_results={n:n+'-result'for n in commands},extra_connection_settle=.05,
          extra_inputs=(ROOT/'tests/check_sigil_death.py',))
