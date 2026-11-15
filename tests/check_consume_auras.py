#!/usr/bin/env python3
"""Consume aura providers with original selected Paladin and Consume cohorts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    families=('paladin-aura','consume-world','bound-demon-consume','consume-aura')
    names=tuple(n for f in families[1:] for n in(f+'-check',f+'-reload-check'))
    waits={n:n+'-result'for n in names};waits.update({'paladin-aura-check':'paladin-aura-result','paladin-aura-reload-check':'paladin-aura-reload-result'})
    check(stage=ROOT/'.local/class-skills/consume-auras',extra_families=families,extra_connected_checks=names,extra_wait_results=waits,extra_connection_settle=.05,
        extra_manifest_names={'paladin-aura':'paladin-aura-checks.json'},
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bind-demon-monsters-source.json'))
