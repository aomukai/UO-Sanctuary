#!/usr/bin/env python3
"""Bound aura providers with selected Paladin, Consume and native-affix controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    connected=('consume-aura','bound-demon-affix','bound-demon-aura')
    names=tuple(n for f in connected for n in(f+'-check',f+'-reload-check'))
    waits={n:n+'-result'for n in names};waits.update({'paladin-aura-check':'paladin-aura-result','paladin-aura-reload-check':'paladin-aura-reload-result'})
    check(stage=ROOT/'.local/class-skills/bind-demon-auras',extra_families=('paladin-aura',*connected),extra_connected_checks=names,
        extra_wait_results=waits,extra_connection_settle=.05,extra_manifest_names={'paladin-aura':'paladin-aura-checks.json'},
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/bound-demon-affix-source-review.json'))
