#!/usr/bin/env python3
"""Validate generated GLB data and the home carrier's launch corridor (stdlib only)."""
import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / 'Assets/Models'
EXPECTED = {'kestrel', 'vanguard', 'atlas', 'raider', 'resolute', 'tyrant', 'cockpit'}

def validate(path):
    raw = path.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', raw)
    assert (magic, version, length) == (b'glTF', 2, len(raw)), path
    json_size, kind = struct.unpack_from('<I4s', raw, 12)
    assert kind == b'JSON'
    doc = json.loads(raw[20:20+json_size])
    offset = 20+json_size
    bin_size, kind = struct.unpack_from('<I4s', raw, offset)
    assert kind == b'BIN\0' and offset+8+bin_size == len(raw)
    binary = raw[offset+8:]
    assert doc['buffers'][0]['byteLength'] == len(binary)
    assert not doc.get('images') and not doc.get('textures'), 'No external texture dependencies'
    assert len(doc['meshes'][0]['primitives']) <= 12, 'Material draw budget'
    positions = []
    count = 0
    for primitive in doc['meshes'][0]['primitives']:
        assert primitive['mode'] == 4
        assert 0 <= primitive['material'] < len(doc['materials'])
        attributes = primitive['attributes']
        counts = set()
        for name, index in attributes.items():
            a = doc['accessors'][index]
            assert a['componentType'] == 5126 and a['type'] in ('VEC3', 'VEC4')
            components = int(a['type'][-1])
            v = doc['bufferViews'][a['bufferView']]
            start = v['byteOffset'] + a.get('byteOffset', 0)
            size = a['count'] * components * 4
            assert start % 4 == 0 and size <= v['byteLength'] and start+size <= len(binary)
            values = list(struct.iter_unpack('<'+'f'*components, binary[start:start+size]))
            assert all(math.isfinite(x) for row in values for x in row)
            counts.add(a['count'])
            if name == 'POSITION':
                positions.extend(values)
                assert all(abs(min(p[i] for p in values)-a['min'][i]) < .001 and
                           abs(max(p[i] for p in values)-a['max'][i]) < .001 for i in range(3))
            if name == 'NORMAL':
                assert all(abs(sum(x*x for x in n)-1) < .001 for n in values)
        assert len(counts) == 1 and next(iter(counts)) % 3 == 0
        count += next(iter(counts))//3
    assert count == doc['extras']['triangles'] <= 12000
    if path.name.startswith('resolute'):
        # Player center is y>=42, half-height <6, crossing the entire central deck.
        # Every center-lane triangle must stay below y=32 or outside the lane.
        for i in range(0, len(positions), 3):
            tri = positions[i:i+3]
            overlaps = min(p[0] for p in tri) < 14 and max(p[0] for p in tri) > -14
            assert not overlaps or max(p[1] for p in tri) < 32, 'Launch corridor obstructed'
    print(f'{path.name}: OK, {count} triangles, {len(doc["materials"])} surfaces')

if __name__ == '__main__':
    files = sorted(ROOT.glob('*_fleet_a.glb'))
    assert {p.name.removesuffix('_fleet_a.glb') for p in files} == EXPECTED
    for file in files:
        validate(file)
    print('ASSET CHECK PASS: 7 models')
