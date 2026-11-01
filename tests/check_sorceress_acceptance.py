#!/usr/bin/env python3
"""Reuse existing Sorceress saved-state scopes before filling player-input gaps.

This baseline does not enable Inferno or certify the29registered handlers.
Prepared Inferno primitives/rate models remain separate from learned input.
"""
import json
import os
from pathlib import Path
import sys

from check_iron_golem_birth import ROOT, check


def configuration():
    families=('sorceress-skills','enchant','fire-ground','meteor','blizzard','frozen-orb',
              'hydra','inferno-primitives','chain-lightning','thunderstorm','teleport')
    waits={'sorceress-skills-check':'sorceress-skills-result',
           'charged-bolt-check':'charged-bolt-result',
           'sorceress-lightning-check':'sorceress-lightning-result'}
    for name in ('fire-ground','meteor','blizzard','frozen-orb','hydra','inferno-primitives',
                 'chain-lightning','thunderstorm'):
        waits[name+'-check']=name+'-result'
        waits[name+'-reload-check']=name+'-reload-result'
    probes={name+'.json':ROOT/'.local/class-skills'/(name+'.json') for name in (
        'player-inferno-channel-source-probe','player-inferno-sequence-source-probe',
        'player-inferno-rate-source-probe')}
    for path in probes.values():assert path.is_file(),path
    manifests={name:name+'-checks.json' for name in families}
    manifests['inferno-primitives']='inferno-primitives.json'
    return dict(stage=ROOT/'.local/class-skills/sorceress-acceptance',extra_families=families,
        extra_checks=('charged-bolt-check','sorceress-lightning-check','player-inferno-check',
                      'player-inferno-sequence-check','arctic-mode-rate-check'),
        extra_wait_results=waits,extra_files=probes,extra_manifest_names=manifests,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tests/check_class_skills.py',
                      ROOT/'tests/check_necromancer_acceptance.py'))


if __name__=='__main__':
    config=configuration()
    if '--plan' in sys.argv:
        print(json.dumps(dict(fixtureFamilies=len(config['extra_families']),
            sharedChecks=len(config['extra_checks']),sourceProbes=len(config['extra_files']),
            fixtures=list(config['extra_families']),asyncWaits=config['extra_wait_results'],
            graphicalClientVerified=False,learnedInfernoEnabled=False),indent=2))
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        check(**config)
