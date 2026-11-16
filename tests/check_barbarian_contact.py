#!/usr/bin/env python3
"""Four captured Barbarian attacks with original items,proc events and focused regression."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_hellslayer import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/barbarian-contact'
def worker_snapshot(session,port,boot):
    session.send('berserk-snapshot-check')
    assert session.expect(r'berserk-snapshot-result: (passed|failed)',timeout=30).group(1)=='passed'
    return 1
def configuration():
    c=prior();c.update(stage=STAGE);c.setdefault('extra_manifest_names',{});c.setdefault('extra_wait_results',{})
    for f in ('BarbarianAttack','Concentrate','Berserk'):add_fixture(c,f)
    for f in ('concentrate','berserk'):
        for suffix in ('-check','-reload-check'):c['extra_wait_results'][f+suffix]=f+'-result';c['extra_connected_checks']=(*c.get('extra_connected_checks',()),f+suffix)
    c.update(extra_families=(*c['extra_families'],'barbarian-contact'),extra_pre_save_callbacks=(*c.get('extra_pre_save_callbacks',()),worker_snapshot),extra_target_input_commands=(*c['extra_target_input_commands'],*(f'barbarian-contact-{skill}-{mode}' for skill in ('bash','stun','concentrate','berserk') for mode in ('right','quick'))),extra_inputs=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/dragon-cold-duration-native-evidence.json',ROOT/'tools/reference/barbarian-contact-source-review.json'))
    helpers=tuple(Path(m.__file__).resolve() for m in tuple(sys.modules.values()) if getattr(m,'__file__',None) and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
