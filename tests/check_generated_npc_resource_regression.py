#!/usr/bin/env python3
"""Retained native Orders/Oak/Paladin/safe-area resource consumers."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check

def configuration(core_only=False):
    return dict(stage=ROOT/'.local/class-skills/generated-npc-live-resources-regression',
        extra_families=('battle-orders','oak-sage-body','paladin-aura')+(()if core_only else('safe-area-minion',)),
        extra_files={n:ROOT/'.local/class-skills'/n for n in ('oak-sage-source-probe.json','oak-aura-source-probe.json')},
        extra_inputs=(Path(__file__).resolve(),),
        extra_manifest_names={'battle-orders':'battle-orders.json','paladin-aura':'paladin-aura-checks.json'},
        extra_wait_results={'battle-orders-check':'battle-orders-result','battle-orders-reload-check':'battle-orders-result','paladin-aura-check':'paladin-aura-result','paladin-aura-reload-check':'paladin-aura-reload-result'})
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration('--core-only'in sys.argv))
