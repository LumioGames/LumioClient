# Fixture catalog

This directory stores **references** to upstream generated fixtures (pin, corpus root,
SHA-256) and local fault scripts.

It must not copy Schema field layouts, Envelope definitions, or a second Codec. The
upstream corpus itself lives in `contract-mirror/upstream/fixtures`, vendored
byte-for-byte and hash-locked; `index.json` only points at it.

`upstreamCorpusPin.hashes` carries a single entry — the sha256 of
`contract-mirror/contract-mirror.sha256`, which is itself the per-file lock over every
mirrored file. Re-listing all mirrored hashes here would be a second copy of the same
truth and would drift the moment the mirror is re-vendored. One pointer, one lock, one
place to update.

`status` is `mirrored` while the corpus is vendored. It reads `unpublished` only when no
mirror exists, and then `hashes` must be empty.
