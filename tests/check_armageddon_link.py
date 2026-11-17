#!/usr/bin/env python3
"""Armageddon direct/periodic Health Link with existing source/live/save scopes."""
import os,sys,struct,time
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_class_skills import fixture_connection
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/armageddon-link'
def wires(session,port,boot):
    with fixture_connection(session,port)as client:
        time.sleep(.15);session.send('armageddon-input-check')
        assert session.expect(r'armageddon-input-check: (passed|failed)').group(1)=='passed'
        ready=session.expect(r'armageddon-input: ready ability=(\d+) x=(\d+) y=(\d+) z=(-?\d+)')
        ability,x,y,z=map(int,ready.groups())
        client.sendall(struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,3,1,ability,0,x,y,z))
        assert session.expect(r'armageddon-input-result: (passed|failed)',timeout=25).group(1)=='passed'
    return 2
def configuration():
    files={n+'.json':ROOT/'.local/class-skills'/(n+'.json')for n in ('armageddon-damage-source-probe','armageddon-emitter-source-probe')}
    files['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    names=('defiler-health-link-check','defiler-health-link-reload-check')
    inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/druid-pet-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    return dict(stage=STAGE,extra_files=files,extra_families=('defiler-health-link','hellslayer-readiness','armageddon'),extra_inputs=tuple(dict.fromkeys((*inputs,*helpers))),extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,extra_live_commands=('armageddon-live',),checked_live_commands=('armageddon-live',),extra_wire_callbacks=(wires,))
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
