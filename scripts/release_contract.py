"""Strict expected-addition contract; an additive release cannot silently omit tracks."""
def check_additions(tracks, old_ids, manifest):
    expected={row['Id']:row for row in manifest['tracks']}
    assert len(expected)==len(manifest['tracks']) and expected, 'Invalid expected additions manifest'
    added=[track for track in tracks if track['Id'] not in old_ids]
    assert {track['Id'] for track in added}==set(expected), 'Missing or unexpected added music'
    for track in added:
        row=expected[track['Id']]
        assert track['File']==row['File'] and track['Category']=='draft', 'Added track does not match release contract'
    return expected
