# TeslaSolarCharger.Protobuf

C# types generated from Tesla's original protobuf definitions, used to decode what the BLE container prints.

## Why this exists

`tesla-bled` marshals the car's answers with `protojson`, whose default options **omit every field that holds its
proto3 default value**. Hand written DTOs with string comparisons cannot express that: `CLOSURESTATE_CLOSED` is enum
value `0`, so a closed door is never serialized, and code that tested for the literal string `"CLOSURESTATE_CLOSED"`
concluded that every car had an open door. Generated types make this a non-issue — an absent field simply decodes to
the enum's zero value, which *is* `CLOSURESTATE_CLOSED`.

## Rules

- **Never hand edit `protos/*.proto`.** They are vendored verbatim from
  [teslamotors/vehicle-command](https://github.com/teslamotors/vehicle-command/tree/main/pkg/protocol/protobuf).
- The vendored copies must match the ref pinned as `VEHICLE_COMMAND_REF` in `TeslaSolarCharger.BleApi/Dockerfile`.
  That ARG is the single source of truth for the upstream version: the container's Go build and TSC's generated C#
  have to decode the same wire format, otherwise they can silently disagree.
- `.github/workflows/syncTeslaProtos.yml` checks upstream weekly and opens a PR that updates the protos **and** the
  Dockerfile ARG together. `.github/workflows/protoDriftCheck.yml` fails any PR where the two have drifted apart.
- Generated `.cs` files are **not** committed; `Grpc.Tools` emits them into `obj/` at build time.

## Decoding rules that still need care

`Google.Protobuf.JsonParser` is strict and **throws** on unknown fields. Tesla adds fields to these messages every
couple of months, so every parser must be built with `WithIgnoreUnknownFields(true)` or a firmware update would break
decoding in the field. `BleJsonParser` in the server project is the single place that does this — use it rather than
constructing a `JsonParser` yourself.
