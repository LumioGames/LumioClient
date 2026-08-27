# Upstream API map

Design aliases `GeneratedContract.*` and `RuntimeContract.*` are not public protocol definitions. Wave 0 maps each alias to a published type, or records an unpublished block.

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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-GENERATED-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
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
      "status": "blocked-unpublished",
      "blockId": "UPSTREAM-RUNTIME-CONTRACT-API-MAP",
      "reason": "No published LumioGameRuntime / generated-contract package is readable from this Client repo; Wave 0 stays compile-only and does not invent Envelope/Transaction/ErrorCode.",
      "affectedTasks": [
        "w2-connection-localembedded-transport",
        "w3-handshake-generated-contract-adapter",
        "w4-replica-fullsnapshot-fixtures",
        "w4-prediction-authority-stage",
        "w5-session-authority-transaction-orchestration",
        "w6-localembedded-fidelity-suite"
      ]
    }
  ]
}
```
