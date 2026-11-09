#!/usr/bin/env python3
"""Authenticate older packet-only context without certifying protected current code."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
    tree_path=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(tree_path.read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    files={};texts={}
    for relative in ['source/D2Game/src/PLAYER/PlayerPets.cpp','source/D2Game/src/UNIT/SUnit.cpp']:
        path=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=path.read_bytes();blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()
        assert blob==next(row['sha']for row in tree['tree']if row['path']==relative)
        files[str(path.relative_to(ROOT))]=dict(gitBlob=blob,sha256=sha(path));texts[relative]=raw.decode()
    callback=texts['source/D2Game/src/PLAYER/PlayerPets.cpp'].split('void __fastcall PLAYERPETS_PlayerIterate_SynchronizeWithClient(',1)[1].split('\n}',1)[0]
    assert callback.count('SUNIT_GetClientFromPlayer(')==1 and callback.count('D2GAME_PACKETS_SendPacket0x7A_6FC3E100(')==1
    iterator=texts['source/D2Game/src/UNIT/SUnit.cpp'].split('void __fastcall SUNIT_IterateLivingPlayers(',1)[1].split('\n}',1)[0]
    assert 'i < 128'in iterator and 'pGame->pUnitList[0][i]'in iterator and 'STATE_PLAYERBODY'in iterator
    result=dict(referenceVersion='3.3.93847',olderSourceCommit=tree['sha'],treeSha256=sha(tree_path),files=files,
        reviewOnly=True,currentCallbackVerified=False,currentCallbackRva='0x35b3a0',currentIteratorRva='0x3722a0',
        conclusion='Older corresponding callback obtains the player client and sends packet0x7A. Current fully captured iterator walks player buckets and additionally excludes sharedstash. This supports classifying the unresolved current callback as a client-sync boundary;it does not establish its payload or lack of other current effects.',
        limits=['Current172byte callback remains protected;no unchanged retry,no invented packet or no-op adapter.','Actual registry birth/order and native state producers are outside the supplied iterator contract.'],
        scriptSha256=sha(Path(__file__)))
    path=ROOT/'tools/reference/pet-client-sync-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Older pet-sync context authenticated;current protected callback remains unresolved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
