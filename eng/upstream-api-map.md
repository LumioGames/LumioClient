# Upstream API map

Design aliases `GeneratedContract.*` and `RuntimeContract.*` are not public protocol definitions. Each alias maps to a published type, or records why no published type covers it.

The published surface is no longer out of reach: `contract-mirror/upstream` carries a
byte-exact copy of it, pinned and hash-locked (see `contract-mirror/MIRROR.md`).
Every alias below was re-checked against that mirror, and none of the 13 alias type
names appears anywhere in it — so `blocked-unpublished` ("nothing is readable from
this repo") is no longer the true reason. The rows now read
`blocked-absent-from-published-surface`: the surface is readable, and these types are
simply not in it. Anything that treats the two as the same block will mistake a
vendored mirror for an unblocking event.

This repo must not define a second Envelope, Transaction, ErrorCode, Schema, Codec, or Storage.

```json
{
  "mapVersion": 1,
  "architectureBaseline": "LGE-V1.2-2026-08-27",
  "roomBaseline": "LGE-V1.4-2026-08-27",
  "aliases": [
    {
      "alias": "GeneratedContract.ClientEventRecord",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.EncodedEnvelope",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.ConnectionCloseReason",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.HandshakeCancelReason",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.AuthorityReplicaUpdate",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.CandidateGameplayCommand",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "GeneratedContract.AuthorityPredictionUpdate",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.CommittedPresentationDiff",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.ReplicaApplyPlan",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.AuthorityTransactionOutcome",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.LocalPredictionPlan",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.LocalPredictionOutcome",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    },
    {
      "alias": "RuntimeContract.PredictionReconcilePlan",
      "publishedType": null,
      "packageId": null,
      "packageVersion": null,
      "status": "blocked-absent-from-published-surface",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "The architecture source's published surface is vendored byte-for-byte at contract-mirror/upstream (pin and scope in contract-mirror/MIRROR.md). Searching that surface for this alias's type name returns nothing, so the design alias still maps to no published type. This repo does not invent an Envelope, Transaction or ErrorCode to close the gap.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    }
  ],
  "mirror": {
    "root": "contract-mirror/upstream",
    "lockFile": "contract-mirror/contract-mirror.sha256",
    "sourceRepository": "LumioGameEngineArchitecture",
    "sourceCommit": "a206e2ca29be81a80f143d0251f4d525beadbf44",
    "baselineId": "LGE-V1.4-2026-08-27",
    "verifyCommand": "bash eng/verify-contract-mirror.sh",
    "driftReportCommand": "bash eng/sync-contract-mirror.sh --source <path> --check"
  }
}
```
