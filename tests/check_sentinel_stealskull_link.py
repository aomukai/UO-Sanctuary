#!/usr/bin/env python3
"""Two real players,admitted Stealskull and natural learned Sentinel linked hits."""
import os,re,shutil,socket,struct,threading,time
from contextlib import contextmanager
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_class_skills import drain_fixture_client
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sentinel-stealskull-link'

@contextmanager
def connection(session,port,remaining):
    with socket.create_connection(('127.0.0.1',port),timeout=5)as client:
        threading.Thread(target=drain_fixture_client,args=(client,),daemon=True).start()
        try:yield client
        finally:
            try:client.shutdown(socket.SHUT_RDWR)
            except OSError:pass
    session.expect(r'Client: 127\.0\.0\.1: Disconnected\. \['+str(remaining)+r' Online\]')

def wires(session,port,boot):
    for name in ('onehand','twohand','onehand-immune','twohand-immune'):
        label='sentinel-stealskull-'+name
        with connection(session,port,0)as warlock:
            session.send(label+'-check');seen=set()
            for _ in range(2):
                setup=session.expect(re.escape(label)+r'(?:: (warlock-ready)|-check: (passed|failed))')
                if setup.group(1):seen.add('ready')
                else:assert setup.group(2)=='passed';seen.add('checked')
            assert seen=={'ready','checked'}
            with connection(session,port,1)as assassin:
                session.send(label+'-assassin-check');assert session.expect(label+r'-assassin-check: (passed|failed)').group(1)=='passed'
                ready=session.expect(label+r': ready ability=(\d+) class=(\d+) target=(\d+) x=(\d+) y=(\d+) z=(-?\d+)');ability,cls,target,x,y,z=map(int,ready.groups())
                if name.startswith('onehand'):assassin.sendall(struct.pack('>BHHBBIBIHHb',0xBF,21,0xF2,7,4,1,ability,target,x,y,z))
                else:assassin.sendall(struct.pack('>BHHBBIBBBIHHb',0xBF,23,0xF7,2,4,1,cls,7,ability,target,x,y,z))
                session.expect(label+': windup')
                if name.startswith('onehand'):assassin.sendall(struct.pack('>BHHBBI',0xBF,11,0xF2,7,5,2))
                assert session.expect(label+r'-result: (passed|failed)',timeout=15).group(1)=='passed'
        time.sleep(.15)
    shutil.copy2(Path(f'/proc/{session.process.pid}/cwd')/'sentinel-stealskull-results.json',STAGE/f'sentinel-stealskull-boot-{boot}.json')
    return 12

def configuration():
    return dict(stage=STAGE,extra_wire_callbacks=(wires,),extra_families=('hellslayer-readiness',),
        extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_inputs=(Path(__file__).resolve(),
        ROOT/'tests/check_assassin_planned_acceptance.py',ROOT/'tools/reference/defiler-health-link-rule-approval.json',
        ROOT/'tools/reference/blade-sentinel-flat-drain-frontier.json'))

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
