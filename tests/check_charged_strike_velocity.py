#!/usr/bin/env python3
"""Charged Strike motion plus retained melee,rate and slow-state regressions."""
from pathlib import Path
from check_iron_golem_birth import ROOT,check


def configuration(focused=False):
    reference=ROOT/'.local/class-skills'
    files={'charged-strike-velocity-source.json':reference/'charged-strike-velocity-source/cases.json'}
    families=('charged-strike-velocity',)
    waits={}
    checks=()
    if not focused:
        for name in ('jab-source-probe.json','power-strike-scalar-probe.json','power-strike-flow-probe.json',
            'charged-strike-source-probe.json','weapon-sequence-rate-probe.json','slow-missiles-source-probe.json','inner-sight-source-probe.json'):
            files[name]=reference/name
        families=('jab','power-strike','charged-strike','slow-missiles-component','charged-strike-velocity')
        checks=('weapon-sequence-rate-check',)
        waits={'charged-strike-check':('charged-strike-native-bolts','charged-strike-expiry-start','charged-strike-expiry','charged-strike-result'),'charged-strike-reload-check':'charged-strike-reload-result',
            'slow-missiles-component-check':'slow-missiles-component-result'}
    return dict(stage=reference/('charged-strike-velocity-smoke'if focused else'charged-strike-velocity'),extra_files=files,
        extra_families=families,extra_checks=checks,extra_wait_results=waits,
        extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/charged-strike-velocity-source-evidence.json'))


if __name__=='__main__':check(**configuration())
