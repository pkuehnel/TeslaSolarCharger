# EEBUS §14a EnWG Integration Guide (Limitation of Power Consumption)

**Audience:** a junior developer who has never worked with EEBUS before.
**Scope:** ONLY the §14a EnWG use case — TeslaSolarCharger (TSC) acts as a *Controllable System* that a grid
operator's control box can send consumption limits to. No EVSE control via EEBUS, no meters, no heat pumps.
**Architecture:** a small Go program (using `github.com/enbility/eebus-go`) runs **inside the existing TSC
container** as a child process of the .NET app. Users do NOT need a second container.

Read this document completely before writing any code. Every section builds on the previous one.

---

## Table of contents

1. [Background: what §14a EnWG and EEBUS are](#1-background)
2. [Architecture overview](#2-architecture-overview)
3. [Part 1 — The Go bridge (`TeslaSolarCharger.EebusBridge`)](#3-part-1--the-go-bridge)
4. [Part 2 — Dockerfile integration](#4-part-2--dockerfile-integration)
5. [Part 3 — The .NET side](#5-part-3--the-net-side)
6. [Part 4 — User-facing networking documentation](#6-part-4--user-facing-networking-documentation)
7. [Part 5 — Testing](#7-part-5--testing)
8. [Acceptance criteria](#8-acceptance-criteria)
9. [Common pitfalls — READ THIS TWICE](#9-common-pitfalls)
10. [Reference: evcc as the working example](#10-reference-evcc)

---

## 1. Background

### 1.1 What is §14a EnWG?

German law (§14a Energiewirtschaftsgesetz) gives grid operators the right to temporarily reduce ("dimmen") the
power consumption of controllable devices (steuerbare Verbrauchseinrichtungen, "SteuVE") — e.g. wallboxes —
when the local grid is overloaded. In exchange the owner pays reduced grid fees. The grid operator may reduce
the consumption to a minimum of **4,200 W**, never to zero.

There are two variants. The one we implement is the **EMS variant**: the grid operator's **control box**
(FNN Steuerbox, connected behind the Smart Meter Gateway) sends a power limit to a **home energy management
system (HEMS)** — that's TSC — and the HEMS is responsible for keeping the consumption of the controlled
devices at or below that limit. The protocol used between the control box and the HEMS is **EEBUS**.

### 1.2 The EEBUS protocol stack (30-second version)

| Layer | Name | What it does |
|---|---|---|
| Transport | **SHIP** | TLS 1.2 WebSocket connections between devices on the LAN, discovered via mDNS. Every device has a self-signed certificate; the SHA-1 hash of its public key is called the **SKI** and is the device's identity. Two devices trust each other by exchanging/confirming SKIs ("pairing"). |
| Data model | **SPINE** | JSON messages describing devices, entities, features, and data (limits, measurements, heartbeats…). |
| Application | **Use cases** | Standardized scenarios. The one we need is **LPC — Limitation of Power Consumption**. |

In LPC there are two roles:

- **Energy Guard (EG)** — the control box. It *sends* limits.
- **Controllable System (CS)** — us (TSC). It *receives and enforces* limits.

### 1.3 The four LPC scenarios (all mandatory for us)

| Scenario | Name | What TSC must do |
|---|---|---|
| 1 | Consumption limit | Receive an active-power limit (watts, may be `0`), approve it, and enforce it until it is deactivated or its duration expires. |
| 2 | Failsafe values | Store a "failsafe consumption limit" (watts) and "failsafe duration" that the EG may write. These are used in failsafe state. |
| 3 | Heartbeat | Both sides send heartbeats (~every 60 s, handled by the library). If we receive **no heartbeat from the EG for 2 minutes**, we must enter **failsafe state**: consumption is limited to the failsafe limit until heartbeats return. |
| 4 | Constraints | Announce our nominal maximum consumption (the house connection power, e.g. 3×35 A×230 V = 24,150 W). |

`enbility/eebus-go` implements all the SPINE/SHIP plumbing and the LPC use case (`usecases/cs/lpc` package).
What we implement on top: the failsafe state machine, the HTTP API toward .NET, and the enforcement inside
TSC's charging loop.

### 1.4 Glossary

| Term | Meaning |
|---|---|
| SKI | Subject Key Identifier — 40 hex chars identifying a device's certificate. Shown to the user, exchanged with the grid operator during commissioning. |
| SMGW / MsB | Smart Meter Gateway / Messstellenbetreiber (metering point operator). The MsB registers our SKI so the control box trusts us. |
| Failsafe state | State entered on heartbeat loss: apply the failsafe consumption limit. |
| EG / CS | Energy Guard (control box) / Controllable System (TSC). |
| CEM | Customer Energy Manager — the SPINE entity type we present ourselves as. |

---

## 2. Architecture overview

```
 LAN                                    │ TSC docker container
                                        │
 ┌───────────────┐   SHIP (TLS WSS,     │  ┌───────────────────┐   HTTP        ┌─────────────────────┐
 │ FNN control   │   port 4712, mDNS)   │  │  eebus-bridge     │  127.0.0.1:   │  TSC .NET app       │
 │ box (EG)      │◄────────────────────►│  │  (Go binary,      │◄─────7401────►│  - starts/supervises│
 │ behind SMGW   │                      │  │  child process)   │               │    the bridge       │
 └───────────────┘                      │  │  - LPC CS role    │               │  - polls /api/state │
                                        │  │  - failsafe state │               │    every 5 s        │
                                        │  │    machine        │               │  - caps charging    │
                                        │  └───────────────────┘               │    power            │
                                        │                                      └─────────────────────┘
```

Design decisions (do not change these without talking to the maintainer):

1. **All EEBUS complexity lives in the Go bridge.** The .NET side only ever sees one simple JSON state object.
   The single most important field is `effectiveConsumptionLimitWatts` — `null` means "no restriction",
   a number means "total charging power must stay at or below this many watts".
2. **TSC owns all persistent data** (certificate, key, configured SKIs, failsafe values). The bridge is
   stateless: it receives its full configuration on stdin at startup and keeps everything else in memory.
   This way TSC's existing backup/restore covers the EEBUS identity.
3. **The bridge is shipped in the same Docker image** and started by the .NET app only when the feature is
   enabled. Version compatibility is guaranteed because both are built from the same commit.
4. **Defense in depth for failsafe:** the bridge enters failsafe when the EG heartbeat is lost; the .NET side
   *additionally* falls back to the failsafe limit when it cannot reach the bridge. A dead bridge can never
   result in unlimited consumption.
5. **Conservative limit enforcement:** we cap the *total charging power controlled by TSC* at the received
   limit. (A future optimization could allow more consumption when PV production offsets it —
   "netzwirksamer Leistungsbezug" — but v1 stays conservative and therefore always compliant.)

---

## 3. Part 1 — The Go bridge

### 3.0 Prerequisites

- Install Go (version ≥ the `go` directive in eebus-go's `go.mod`; as of writing use the latest stable Go).
- Clone https://github.com/enbility/eebus-go somewhere and **read `examples/hems/main.go`** — that example
  is exactly our role (a HEMS acting as LPC Controllable System). When anything in this guide does not compile
  against the eebus-go version you pinned, the example and the `usecases/cs/lpc` package source are the truth.

> ⚠️ **API drift warning.** eebus-go's API changes between versions, and evcc (the reference implementation)
> runs its own forks (`github.com/evcc-io/eebus-go`). We use **upstream `enbility/eebus-go`**, pinned to the
> latest tagged release. Two known naming differences you may hit:
> - Limit approval event: `lpc.WriteApprovalRequired` (≤ v0.7.0) vs `lpc.LimitWriteApprovalRequired` (newer).
> - `lpc.ConfigurationWriteApprovalRequired` only exists in versions newer than v0.7.0; older versions apply
>   configuration writes automatically — if the constant doesn't exist, simply omit that case.
> Let the compiler guide you; do not "fix" compile errors by commenting logic out.

### 3.1 Project layout

Create a new top-level folder in the repo (sibling of `TeslaSolarCharger/`):

```
TeslaSolarCharger.EebusBridge/
├── go.mod
├── go.sum
├── main.go          // flag parsing, config from stdin, wiring, shutdown
├── config.go        // Config struct + validation
├── certificate.go   // generate-cert subcommand + SKI helper
├── eebus.go         // SHIP/SPINE service setup, LPC use case, event handling
├── state.go         // failsafe/limit state machine  (MOST IMPORTANT FILE)
├── state_test.go    // unit tests for the state machine
└── httpserver.go    // localhost JSON API for the .NET side
```

Initialize:

```bash
cd TeslaSolarCharger.EebusBridge
go mod init github.com/pkuehnel/TeslaSolarCharger/eebusbridge
go get github.com/enbility/eebus-go@latest
go get github.com/enbility/ship-go@latest
go get github.com/enbility/spine-go@latest
```

### 3.2 Configuration contract (stdin)

The bridge reads **one JSON document from stdin** at startup, then starts. It never reads or writes files.
The private key therefore never touches the disk.

```go
// config.go
package main

import (
	"encoding/json"
	"errors"
	"io"
)

type Config struct {
	// PEM-encoded certificate and private key. Generated once via `eebus-bridge generate-cert`
	// and stored by the .NET side. NEVER regenerated automatically.
	CertificatePem string `json:"certificatePem"`
	PrivateKeyPem  string `json:"privateKeyPem"`

	ShipPort int `json:"shipPort"` // SHIP server port, default 4712
	ApiPort  int `json:"apiPort"`  // localhost HTTP API port, default 7401

	// Stable serial for the SHIP id, e.g. TSC's installation id. Must not change between restarts.
	Serial string `json:"serial"`

	// SKI of the grid operator's control box. Only this SKI is trusted.
	RemoteSki string `json:"remoteSki"`
	// Optional IP address hint if mDNS discovery is not possible.
	RemoteIp string `json:"remoteIp"`

	// LPC scenario 4: nominal maximum consumption of the grid connection in watts (e.g. 24150).
	ConsumptionNominalMaxW float64 `json:"consumptionNominalMaxW"`
	// LPC scenario 2 initial values (the EG may overwrite them at runtime; the .NET side
	// persists overwritten values and passes them back in here on the next start).
	FailsafeConsumptionLimitW float64 `json:"failsafeConsumptionLimitW"`
	FailsafeDurationSeconds   int     `json:"failsafeDurationSeconds"`
}

func ReadConfig(r io.Reader) (*Config, error) {
	var c Config
	if err := json.NewDecoder(r).Decode(&c); err != nil {
		return nil, err
	}
	if c.CertificatePem == "" || c.PrivateKeyPem == "" {
		return nil, errors.New("certificatePem and privateKeyPem are required")
	}
	if c.RemoteSki == "" {
		return nil, errors.New("remoteSki is required")
	}
	if c.ShipPort == 0 {
		c.ShipPort = 4712
	}
	if c.ApiPort == 0 {
		c.ApiPort = 7401
	}
	if c.ConsumptionNominalMaxW == 0 {
		c.ConsumptionNominalMaxW = 24150 // 3 x 35A x 230V standard house connection
	}
	if c.FailsafeConsumptionLimitW == 0 {
		c.FailsafeConsumptionLimitW = 4200 // §14a minimum
	}
	if c.FailsafeDurationSeconds == 0 {
		c.FailsafeDurationSeconds = 7200 // 2h
	}
	return &c, nil
}
```

> ⚠️ Never log the parsed config — it contains the private key. Log individual fields if needed.

### 3.3 Certificate generation (`generate-cert` subcommand)

The EEBUS certificate must have the right shape (ECDSA P-256, SubjectKeyId set). `ship-go` provides a helper
that guarantees this — use it, do not hand-roll certificates:

```go
// certificate.go
package main

import (
	"bytes"
	"crypto/ecdsa"
	"crypto/tls"
	"crypto/x509"
	"encoding/json"
	"encoding/pem"
	"errors"
	"fmt"
	"os"

	"github.com/enbility/ship-go/cert"
)

type generatedIdentity struct {
	CertificatePem string `json:"certificatePem"`
	PrivateKeyPem  string `json:"privateKeyPem"`
	Ski            string `json:"ski"`
}

// runGenerateCert prints a new identity as JSON to stdout and exits.
// Called by the .NET side exactly once, when the user enables the feature.
func runGenerateCert() error {
	certificate, err := cert.CreateCertificate("", "TeslaSolarCharger", "DE", "TSC-EEBUS-01")
	if err != nil {
		return err
	}

	public, private, err := pemFromKeyPair(certificate)
	if err != nil {
		return err
	}

	ski, err := skiFromCert(certificate)
	if err != nil {
		return err
	}

	return json.NewEncoder(os.Stdout).Encode(generatedIdentity{
		CertificatePem: public,
		PrivateKeyPem:  private,
		Ski:            ski,
	})
}

func pemFromKeyPair(c tls.Certificate) (string, string, error) {
	var out bytes.Buffer
	if err := pem.Encode(&out, &pem.Block{Type: "CERTIFICATE", Bytes: c.Certificate[0]}); err != nil {
		return "", "", err
	}
	public := out.String()

	key, ok := c.PrivateKey.(*ecdsa.PrivateKey)
	if !ok {
		return "", "", errors.New("unexpected private key type")
	}
	keyBytes, err := x509.MarshalECPrivateKey(key)
	if err != nil {
		return "", "", err
	}
	out.Reset()
	if err := pem.Encode(&out, &pem.Block{Type: "EC PRIVATE KEY", Bytes: keyBytes}); err != nil {
		return "", "", err
	}
	return public, out.String(), nil
}

func skiFromCert(c tls.Certificate) (string, error) {
	leaf, err := x509.ParseCertificate(c.Certificate[0])
	if err != nil {
		return "", err
	}
	if len(leaf.SubjectKeyId) == 0 {
		return "", errors.New("certificate has no SubjectKeyId")
	}
	return fmt.Sprintf("%0x", leaf.SubjectKeyId), nil
}
```

### 3.4 EEBUS service setup and event handling

This mirrors `examples/hems/main.go` from eebus-go and evcc's `server/eebus/eebus.go` + `hems/eebus/*.go`.

```go
// eebus.go
package main

import (
	"crypto/tls"
	"time"

	eebusapi "github.com/enbility/eebus-go/api"
	"github.com/enbility/eebus-go/service"
	ucapi "github.com/enbility/eebus-go/usecases/api"
	cslpc "github.com/enbility/eebus-go/usecases/cs/lpc"
	shipapi "github.com/enbility/ship-go/api"
	"github.com/enbility/ship-go/mdns"
	spineapi "github.com/enbility/spine-go/api"
	"github.com/enbility/spine-go/model"
)

type EebusService struct {
	service eebusapi.ServiceInterface
	lpc     ucapi.CsLPCInterface
	state   *StateMachine // see state.go
	cfg     *Config
	ski     string
}

func NewEebusService(cfg *Config, state *StateMachine) (*EebusService, error) {
	certificate, err := tls.X509KeyPair([]byte(cfg.CertificatePem), []byte(cfg.PrivateKeyPem))
	if err != nil {
		return nil, err
	}

	ski, err := skiFromCert(certificate)
	if err != nil {
		return nil, err
	}

	e := &EebusService{state: state, cfg: cfg, ski: ski}

	// NOTE: verify this signature against the pinned eebus-go version (see examples/hems).
	// Older versions have no deviceCategories parameter; evcc's fork has two extra pairing args.
	configuration, err := eebusapi.NewConfiguration(
		"TeslaSolarCharger",              // vendor code
		"TeslaSolarCharger",              // brand
		"HEMS",                            // model
		cfg.Serial,                        // serial -> part of the SHIP id, must be stable
		[]shipapi.DeviceCategoryType{shipapi.DeviceCategoryTypeEnergyManagementSystem},
		model.DeviceTypeTypeEnergyManagementSystem,
		[]model.EntityTypeType{model.EntityTypeTypeCEM},
		cfg.ShipPort,
		certificate,
		time.Second*60, // heartbeat timeout announced to the EG; LPC uses 60 s
	)
	if err != nil {
		return nil, err
	}
	configuration.SetMdnsProviderSelection(mdns.MdnsProviderSelectionAll)

	e.service = service.NewService(configuration, e)
	if err := e.service.Setup(); err != nil {
		return nil, err
	}

	localEntity := e.service.LocalDevice().EntityForType(model.EntityTypeTypeCEM)
	e.lpc = cslpc.NewLPC(localEntity, e.OnLpcEvent)
	e.service.AddUseCase(e.lpc)

	// Scenario 4: announce nominal maximum consumption.
	if err := e.lpc.SetConsumptionNominalMax(cfg.ConsumptionNominalMaxW); err != nil {
		return nil, err
	}
	// Scenario 2: initial failsafe values ("true" = changeable by the EG).
	if err := e.lpc.SetFailsafeConsumptionActivePowerLimit(cfg.FailsafeConsumptionLimitW, true); err != nil {
		return nil, err
	}
	if err := e.lpc.SetFailsafeDurationMinimum(time.Duration(cfg.FailsafeDurationSeconds)*time.Second, true); err != nil {
		return nil, err
	}

	// Trust exactly the configured control box.
	e.service.RegisterRemoteSKI(cfg.RemoteSki)

	e.service.Start()
	return e, nil
}

func (e *EebusService) Shutdown() { e.service.Shutdown() }
func (e *EebusService) Ski() string { return e.ski }

// OnLpcEvent is the use-case event callback. It only translates events into state machine calls.
func (e *EebusService) OnLpcEvent(ski string, device spineapi.DeviceRemoteInterface,
	entity spineapi.EntityRemoteInterface, event eebusapi.EventType) {

	switch event {
	// Scenario 1: a limit was updated. Read it and hand it to the state machine.
	case cslpc.DataUpdateLimit:
		if limit, err := e.lpc.ConsumptionLimit(); err == nil {
			e.state.SetConsumptionLimit(limit)
		}

	// Scenario 1: an incoming limit write must be approved or denied.
	// Approve everything except physically impossible (negative) limits. A 0 W limit IS valid.
	// NOTE: constant is named WriteApprovalRequired in eebus-go <= v0.7.0.
	case cslpc.LimitWriteApprovalRequired:
		for msgCounter, limit := range e.lpc.PendingConsumptionLimits() {
			if limit.Value < 0 {
				e.lpc.ApproveOrDenyConsumptionLimit(msgCounter, false, "negative limit not allowed")
				continue
			}
			e.lpc.ApproveOrDenyConsumptionLimit(msgCounter, true, "")
			e.state.SetConsumptionLimit(limit)
		}

	// Scenario 2: the EG wrote new failsafe values. Approve, then read them.
	// NOTE: this event only exists in eebus-go > v0.7.0; older versions apply writes automatically.
	case cslpc.ConfigurationWriteApprovalRequired:
		for msgCounter := range e.lpc.PendingDeviceConfigurations() {
			e.lpc.ApproveOrDenyDeviceConfiguration(msgCounter, true, "")
		}

	case cslpc.DataUpdateFailsafeConsumptionActivePowerLimit:
		if limit, _, err := e.lpc.FailsafeConsumptionActivePowerLimit(); err == nil {
			e.state.SetFailsafeConsumptionLimit(limit)
		}

	case cslpc.DataUpdateFailsafeDurationMinimum:
		if duration, _, err := e.lpc.FailsafeDurationMinimum(); err == nil {
			e.state.SetFailsafeDuration(duration)
		}

	// Scenario 3: heartbeat from the EG arrived.
	case cslpc.DataUpdateHeartbeat:
		e.state.HeartbeatReceived()
	}
}

// ---- shipapi/eebusapi ServiceReaderInterface (required by service.NewService) ----
// Verify exact method names/signatures against the pinned version; implement the interface
// the compiler asks for. The important logic is in ServicePairingDetailUpdate.

func (e *EebusService) RemoteSKIConnected(_ eebusapi.ServiceInterface, ski string) {
	if ski == e.cfg.RemoteSki {
		e.state.SetControlBoxConnected(true)
	}
}

func (e *EebusService) RemoteSKIDisconnected(_ eebusapi.ServiceInterface, ski string) {
	if ski == e.cfg.RemoteSki {
		e.state.SetControlBoxConnected(false)
	}
}

func (e *EebusService) VisibleRemoteServicesUpdated(_ eebusapi.ServiceInterface, _ []shipapi.RemoteService) {
}

func (e *EebusService) ServiceShipIDUpdate(_ string, _ string) {}

// Deny pairing requests from anything that is not the configured control box.
func (e *EebusService) ServicePairingDetailUpdate(ski string, detail *shipapi.ConnectionStateDetail) {
	if detail.State() == shipapi.ConnectionStateReceivedPairingRequest && ski != e.cfg.RemoteSki {
		e.service.CancelPairingWithSKI(ski)
	}
}
```

### 3.5 The state machine (`state.go`) — the heart of the bridge

This is a direct port of evcc's proven §14a logic (`hems/eebus/eebus.go`, function `run()`); the LPC-xxx codes
in comments refer to the EEBUS LPC Use Case Technical Specification test cases, same as in evcc.

Rules:

1. **Heartbeat monitoring (LPC-911/921):** if no heartbeat was received for 2 minutes → status = `failsafe`,
   effective limit = failsafe limit. At startup, pretend a heartbeat was just received (otherwise we would
   start in failsafe before the control box had a chance to connect).
2. **Leaving failsafe (LPC-918/919/920):** the moment a heartbeat arrives again → status = `normal`.
3. **Limit lifecycle (LPC-914/1):** an active limit becomes effective immediately. It stops being effective
   when the EG deactivates it, or when its duration (if > 0) has elapsed.
4. **Effective limit** exposed to .NET: failsafe → failsafe limit; active limit → limit value; otherwise `nil`.

```go
// state.go
package main

import (
	"sync"
	"time"

	ucapi "github.com/enbility/eebus-go/usecases/api"
)

const heartbeatTimeout = 2 * time.Minute // LPC-031

type StateMachine struct {
	mu sync.Mutex

	now func() time.Time // injectable for tests; defaults to time.Now

	controlBoxConnected bool
	lastHeartbeat       time.Time

	consumptionLimit    ucapi.LoadLimit // Value (W), IsActive, Duration
	limitActivatedAt    time.Time       // zero = not active

	failsafeConsumptionLimitW float64
	failsafeDuration          time.Duration
}

func NewStateMachine(cfg *Config) *StateMachine {
	s := &StateMachine{
		now:                       time.Now,
		failsafeConsumptionLimitW: cfg.FailsafeConsumptionLimitW,
		failsafeDuration:          time.Duration(cfg.FailsafeDurationSeconds) * time.Second,
	}
	// Simulate an initial heartbeat so we do not boot straight into failsafe (same as evcc).
	s.lastHeartbeat = s.now()
	return s
}

func (s *StateMachine) HeartbeatReceived() {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.lastHeartbeat = s.now()
}

func (s *StateMachine) SetControlBoxConnected(connected bool) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.controlBoxConnected = connected
}

func (s *StateMachine) SetConsumptionLimit(limit ucapi.LoadLimit) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.consumptionLimit = limit
	if limit.IsActive {
		s.limitActivatedAt = s.now()
	} else {
		s.limitActivatedAt = time.Time{}
	}
}

func (s *StateMachine) SetFailsafeConsumptionLimit(w float64) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.failsafeConsumptionLimitW = w
}

func (s *StateMachine) SetFailsafeDuration(d time.Duration) {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.failsafeDuration = d
}

// Snapshot is what the HTTP API serves to the .NET side.
type Snapshot struct {
	ControlBoxConnected             bool     `json:"controlBoxConnected"`
	Status                          string   `json:"status"` // "normal" | "failsafe"
	HeartbeatOk                     bool     `json:"heartbeatOk"`
	ConsumptionLimitActive          bool     `json:"consumptionLimitActive"`
	ConsumptionLimitWatts           float64  `json:"consumptionLimitWatts"`
	LimitActivatedAt                *string  `json:"limitActivatedAt,omitempty"` // RFC3339
	LimitDurationSeconds            *int64   `json:"limitDurationSeconds,omitempty"`
	EffectiveConsumptionLimitWatts  *float64 `json:"effectiveConsumptionLimitWatts"` // null = unrestricted
	FailsafeConsumptionLimitWatts   float64  `json:"failsafeConsumptionLimitWatts"`
	FailsafeDurationSeconds         int64    `json:"failsafeDurationSeconds"`
}

func (s *StateMachine) Snapshot() Snapshot {
	s.mu.Lock()
	defer s.mu.Unlock()

	now := s.now()
	heartbeatOk := now.Sub(s.lastHeartbeat) < heartbeatTimeout

	// LPC-914/1: limit duration expiry deactivates the limit.
	if s.consumptionLimit.IsActive && s.consumptionLimit.Duration > 0 &&
		!s.limitActivatedAt.IsZero() && now.Sub(s.limitActivatedAt) > s.consumptionLimit.Duration {
		s.consumptionLimit.IsActive = false
		s.limitActivatedAt = time.Time{}
	}

	snap := Snapshot{
		ControlBoxConnected:           s.controlBoxConnected,
		HeartbeatOk:                   heartbeatOk,
		ConsumptionLimitActive:        s.consumptionLimit.IsActive,
		ConsumptionLimitWatts:         s.consumptionLimit.Value,
		FailsafeConsumptionLimitWatts: s.failsafeConsumptionLimitW,
		FailsafeDurationSeconds:       int64(s.failsafeDuration.Seconds()),
	}

	if !s.limitActivatedAt.IsZero() {
		ts := s.limitActivatedAt.Format(time.RFC3339)
		snap.LimitActivatedAt = &ts
		d := int64(s.consumptionLimit.Duration.Seconds())
		snap.LimitDurationSeconds = &d
	}

	switch {
	case !heartbeatOk:
		// LPC-911/921: failsafe state - apply the failsafe limit.
		snap.Status = "failsafe"
		v := s.failsafeConsumptionLimitW
		snap.EffectiveConsumptionLimitWatts = &v
	case s.consumptionLimit.IsActive:
		snap.Status = "normal"
		v := s.consumptionLimit.Value
		snap.EffectiveConsumptionLimitWatts = &v
	default:
		snap.Status = "normal"
		snap.EffectiveConsumptionLimitWatts = nil
	}

	return snap
}
```

> ℹ️ Note there is deliberately **no ticker/goroutine**: the state is evaluated lazily whenever `.NET` polls
> `Snapshot()`. Since .NET polls every 5 s, failsafe entry is detected within 5 s of the 2-minute deadline.

### 3.6 HTTP API (`httpserver.go`)

Bind to **127.0.0.1 only**. Never bind to 0.0.0.0 — this API is unauthenticated by design and must not be
reachable from outside the container.

| Method & path | Response |
|---|---|
| `GET /api/health` | `{"ok":true}` — used by .NET to detect the process is up |
| `GET /api/identity` | `{"ski":"<40 hex chars>"}` |
| `GET /api/state` | the `Snapshot` JSON from section 3.5 |

```go
// httpserver.go
package main

import (
	"encoding/json"
	"fmt"
	"net/http"
)

func StartHttpServer(cfg *Config, e *EebusService, state *StateMachine) error {
	mux := http.NewServeMux()

	mux.HandleFunc("GET /api/health", func(w http.ResponseWriter, r *http.Request) {
		writeJson(w, map[string]bool{"ok": true})
	})
	mux.HandleFunc("GET /api/identity", func(w http.ResponseWriter, r *http.Request) {
		writeJson(w, map[string]string{"ski": e.Ski()})
	})
	mux.HandleFunc("GET /api/state", func(w http.ResponseWriter, r *http.Request) {
		writeJson(w, state.Snapshot())
	})

	server := &http.Server{
		Addr:    fmt.Sprintf("127.0.0.1:%d", cfg.ApiPort),
		Handler: mux,
	}
	return server.ListenAndServe()
}

func writeJson(w http.ResponseWriter, v any) {
	w.Header().Set("Content-Type", "application/json")
	_ = json.NewEncoder(w).Encode(v)
}
```

### 3.7 `main.go`

```go
// main.go
package main

import (
	"log"
	"os"
	"os/signal"
	"syscall"
)

func main() {
	if len(os.Args) > 1 && os.Args[1] == "generate-cert" {
		if err := runGenerateCert(); err != nil {
			log.Fatalf("generate-cert failed: %v", err)
		}
		return
	}

	cfg, err := ReadConfig(os.Stdin)
	if err != nil {
		log.Fatalf("reading config from stdin failed: %v", err)
	}

	state := NewStateMachine(cfg)

	eebus, err := NewEebusService(cfg, state)
	if err != nil {
		log.Fatalf("eebus service setup failed: %v", err)
	}

	go func() {
		if err := StartHttpServer(cfg, eebus, state); err != nil {
			log.Fatalf("http server failed: %v", err)
		}
	}()

	log.Printf("eebus-bridge running. SKI: %s, SHIP port: %d, API port: %d",
		eebus.Ski(), cfg.ShipPort, cfg.ApiPort)

	// Graceful shutdown on SIGTERM/SIGINT so the SHIP connection closes cleanly.
	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGTERM, syscall.SIGINT)
	<-sig
	eebus.Shutdown()
}
```

### 3.8 Running locally (Windows dev machine)

```bash
cd TeslaSolarCharger.EebusBridge
go build -o eebus-bridge.exe .
type dev-config.json | eebus-bridge.exe        # PowerShell: Get-Content dev-config.json | .\eebus-bridge.exe
```

Create `dev-config.json` manually for local testing (generate the cert first with
`eebus-bridge.exe generate-cert > identity.json` and copy the fields). **Add `dev-config.json` and
`identity.json` to `.gitignore` — they contain a private key.**

---

## 4. Part 2 — Dockerfile integration

Edit [TeslaSolarCharger/Server/Dockerfile](../TeslaSolarCharger/Server/Dockerfile). Add a Go build stage and
copy the binary into the final image. The `--platform=$BUILDPLATFORM` + `GOARCH=$TARGETARCH` pattern makes
multi-arch builds (amd64 + arm64) work without emulation:

```dockerfile
# ---- NEW: Go build stage for the EEBUS bridge ----
FROM --platform=$BUILDPLATFORM golang:1.24 AS gobuild
ARG TARGETOS
ARG TARGETARCH
WORKDIR /src
COPY TeslaSolarCharger.EebusBridge/go.mod TeslaSolarCharger.EebusBridge/go.sum ./
RUN go mod download
COPY TeslaSolarCharger.EebusBridge/ ./
RUN CGO_ENABLED=0 GOOS=$TARGETOS GOARCH=$TARGETARCH go build -ldflags="-s -w" -o /out/eebus-bridge .
```

In the `final` stage add:

```dockerfile
COPY --from=gobuild /out/eebus-bridge /app/eebus-bridge
EXPOSE 4712
```

Check the CI workflow (GitHub Actions) builds with buildx and passes `TARGETOS`/`TARGETARCH` automatically —
it does if `docker buildx build --platform linux/amd64,linux/arm64` is used (standard for this repo).
Verify the docker build locally: `docker build -f TeslaSolarCharger/Server/Dockerfile .` from the repo root
and confirm `/app/eebus-bridge` exists in the image (`docker run --rm --entrypoint ls <image> -la /app`).

---

## 5. Part 3 — The .NET side

### 5.1 New configuration properties

**(a) User-editable settings** go into
[TeslaSolarCharger/Shared/Dtos/BaseConfiguration/BaseConfigurationBase.cs](../TeslaSolarCharger/Shared/Dtos/BaseConfiguration/BaseConfigurationBase.cs)
(follow the style of the existing properties, e.g. `MaxCombinedCurrent` around line 119):

```csharp
public bool UseEebus14aGridControl { get; set; }
public string? Eebus14aControlBoxSki { get; set; }
public string? Eebus14aControlBoxIpAddress { get; set; }
[Postfix("W")]
public int Eebus14aContractualConsumptionNominalMax { get; set; } = 24150;
[Postfix("W")]
public int Eebus14aFailsafeConsumptionLimit { get; set; } = 4200;
[Postfix("min")]
public int Eebus14aFailsafeDurationMinutes { get; set; } = 120;
```

Add accessors to `IConfigurationWrapper` + `ConfigurationWrapper`
([TeslaSolarCharger/Shared/Wrappers/ConfigurationWrapper.cs](../TeslaSolarCharger/Shared/Wrappers/ConfigurationWrapper.cs),
copy the `MaxCombinedCurrent()` pattern) and add English + German texts to
`BaseConfigurationBasePropertyLocalization.cs` (copy the `Register(x => x.MaxCombinedCurrent, ...)` pattern —
every new property MUST get a localization entry, the UI renders from it).

German translation hints: §14a EnWG = "§14a EnWG Steuerung", control box = "Steuerbox",
failsafe limit = "Failsafe-Leistungsgrenze", nominal max = "Vertragliche Anschlussleistung".

**(b) Machine-managed values** (never user-edited) go into the existing `TscConfiguration` key-value table
via `ITscConfigurationService`. Define the keys once:

```csharp
// TeslaSolarCharger/Server/Services/Eebus/EebusConstants.cs
namespace TeslaSolarCharger.Server.Services.Eebus;

public static class EebusConstants
{
    public const string CertificateKey = "EebusCertificatePem";
    public const string PrivateKeyKey = "EebusPrivateKeyPem";
    public const string SkiKey = "EebusSki";
    // Failsafe values written by the control box at runtime (override the BaseConfiguration values):
    public const string EgWrittenFailsafeLimitKey = "EebusEgWrittenFailsafeConsumptionLimitW";
    public const string EgWrittenFailsafeDurationKey = "EebusEgWrittenFailsafeDurationSeconds";
}
```

**Precedence rule (important):** when building the bridge config, use the EG-written failsafe values from
`TscConfiguration` if present, otherwise the user's `BaseConfiguration` values. The LPC spec requires
EG-written failsafe values to survive restarts — this is how we persist them.

**(c) appsettings** — add to `appsettings.json`:

```json
"EebusBridge": {
    "ExecutablePath": "/app/eebus-bridge",
    "ApiPort": 7401,
    "ShipPort": 4712
}
```

In `appsettings.Development.json` point `ExecutablePath` at your locally built
`TeslaSolarCharger.EebusBridge/eebus-bridge.exe`.

### 5.2 Shared DTO (used by server, client UI, and JSON deserialization)

```csharp
// TeslaSolarCharger/Shared/Dtos/Eebus/DtoEebusLpcState.cs
using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Shared.Dtos.Eebus;

public class DtoEebusLpcState
{
    [JsonPropertyName("controlBoxConnected")] public bool ControlBoxConnected { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "normal"; // "normal" | "failsafe"
    [JsonPropertyName("heartbeatOk")] public bool HeartbeatOk { get; set; }
    [JsonPropertyName("consumptionLimitActive")] public bool ConsumptionLimitActive { get; set; }
    [JsonPropertyName("consumptionLimitWatts")] public double ConsumptionLimitWatts { get; set; }
    [JsonPropertyName("limitActivatedAt")] public DateTimeOffset? LimitActivatedAt { get; set; }
    [JsonPropertyName("limitDurationSeconds")] public long? LimitDurationSeconds { get; set; }
    [JsonPropertyName("effectiveConsumptionLimitWatts")] public double? EffectiveConsumptionLimitWatts { get; set; }
    [JsonPropertyName("failsafeConsumptionLimitWatts")] public double FailsafeConsumptionLimitWatts { get; set; }
    [JsonPropertyName("failsafeDurationSeconds")] public long FailsafeDurationSeconds { get; set; }
}
```

### 5.3 Bridge process supervision (`EebusBridgeProcessService`)

A hosted service (register with `.AddHostedService<...>()` in
[ServiceCollectionExtensions.cs](../TeslaSolarCharger/Server/ServiceCollectionExtensions.cs), next to the
existing `DatabaseValueBufferFlushService`). Behavior:

- Loop forever (until app shutdown), checking every 30 s:
  - Feature disabled or identity/SKI not configured → make sure the process is stopped.
  - Feature enabled → make sure the process is running; if the effective bridge config changed → restart it.
- Start = `Process.Start` with `RedirectStandardInput/Output/Error = true`, write the config JSON to stdin,
  close stdin, pipe stdout/stderr lines to `ILogger`.
- On crash: log error, raise a `LoggedError` via `IErrorHandlingService`, retry after 10 s.
- On app shutdown: `process.Kill(entireProcessTree: true)`.

```csharp
// TeslaSolarCharger/Server/Services/Eebus/EebusBridgeProcessService.cs
using System.Diagnostics;
using System.Text.Json;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services.Eebus;

public class EebusBridgeProcessService(
    ILogger<EebusBridgeProcessService> logger,
    IConfiguration configuration,
    IServiceProvider serviceProvider) : BackgroundService
{
    private Process? _process;
    private string? _lastConfigJson;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureDesiredProcessState(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while managing EEBUS bridge process");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
        }
        StopProcess();
    }

    private async Task EnsureDesiredProcessState(CancellationToken cancellationToken)
    {
        // Resolve scoped services per iteration (BackgroundService is a singleton).
        using var scope = serviceProvider.CreateScope();
        var configurationWrapper = scope.ServiceProvider.GetRequiredService<IConfigurationWrapper>();
        var tscConfigurationService = scope.ServiceProvider.GetRequiredService<ITscConfigurationService>();

        var enabled = configurationWrapper.UseEebus14aGridControl();
        var certificate = await tscConfigurationService.GetConfigurationValueByKey(EebusConstants.CertificateKey);
        var privateKey = await tscConfigurationService.GetConfigurationValueByKey(EebusConstants.PrivateKeyKey);
        var remoteSki = configurationWrapper.Eebus14aControlBoxSki();

        if (!enabled || string.IsNullOrEmpty(certificate) || string.IsNullOrEmpty(privateKey)
            || string.IsNullOrEmpty(remoteSki))
        {
            StopProcess();
            return;
        }

        var configJson = await BuildBridgeConfigJson(configurationWrapper, tscConfigurationService, certificate,
            privateKey, remoteSki).ConfigureAwait(false);

        var processAlive = _process is { HasExited: false };
        if (processAlive && configJson == _lastConfigJson)
        {
            return; // running with current config - nothing to do
        }

        StopProcess();
        StartProcess(configJson);
        _lastConfigJson = configJson;
    }

    private async Task<string> BuildBridgeConfigJson(IConfigurationWrapper configurationWrapper,
        ITscConfigurationService tscConfigurationService, string certificate, string privateKey, string remoteSki)
    {
        // EG-written failsafe values (persisted by EebusStatePollService) take precedence over user config.
        var egFailsafeLimit = await tscConfigurationService
            .GetConfigurationValueByKey(EebusConstants.EgWrittenFailsafeLimitKey).ConfigureAwait(false);
        var egFailsafeDuration = await tscConfigurationService
            .GetConfigurationValueByKey(EebusConstants.EgWrittenFailsafeDurationKey).ConfigureAwait(false);
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);

        var config = new
        {
            certificatePem = certificate,
            privateKeyPem = privateKey,
            shipPort = configuration.GetValue<int?>("EebusBridge:ShipPort") ?? 4712,
            apiPort = configuration.GetValue<int?>("EebusBridge:ApiPort") ?? 7401,
            serial = installationId.ToString("N")[..12],
            remoteSki = remoteSki.Replace(" ", "").Replace("-", "").ToLowerInvariant(),
            remoteIp = configurationWrapper.Eebus14aControlBoxIpAddress(),
            consumptionNominalMaxW = (double)configurationWrapper.Eebus14aContractualConsumptionNominalMax(),
            failsafeConsumptionLimitW = double.TryParse(egFailsafeLimit, out var limit)
                ? limit
                : configurationWrapper.Eebus14aFailsafeConsumptionLimit(),
            failsafeDurationSeconds = long.TryParse(egFailsafeDuration, out var duration)
                ? duration
                : configurationWrapper.Eebus14aFailsafeDurationMinutes() * 60L,
        };
        return JsonSerializer.Serialize(config);
    }

    private void StartProcess(string configJson)
    {
        var executablePath = configuration.GetValue<string>("EebusBridge:ExecutablePath") ?? "/app/eebus-bridge";
        logger.LogInformation("Starting EEBUS bridge {path}", executablePath);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException("Could not start EEBUS bridge process");

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != default) { logger.LogInformation("eebus-bridge: {line}", e.Data); }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != default) { logger.LogWarning("eebus-bridge: {line}", e.Data); }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // NEVER log configJson itself - it contains the private key.
        process.StandardInput.Write(configJson);
        process.StandardInput.Close();

        _process = process;
    }

    private void StopProcess()
    {
        if (_process is { HasExited: false })
        {
            logger.LogInformation("Stopping EEBUS bridge process");
            try { _process.Kill(entireProcessTree: true); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not kill EEBUS bridge process"); }
        }
        _process?.Dispose();
        _process = default;
        _lastConfigJson = default;
    }
}
```

### 5.4 Bridge HTTP client

```csharp
// TeslaSolarCharger/Server/Services/Eebus/Contracts/IEebusBridgeClient.cs
using TeslaSolarCharger.Shared.Dtos.Eebus;

namespace TeslaSolarCharger.Server.Services.Eebus.Contracts;

public interface IEebusBridgeClient
{
    Task<DtoEebusLpcState> GetState(CancellationToken cancellationToken);
}
```

```csharp
// TeslaSolarCharger/Server/Services/Eebus/EebusBridgeClient.cs
using System.Net.Http.Json;
using TeslaSolarCharger.Server.Services.Eebus.Contracts;
using TeslaSolarCharger.Shared.Dtos.Eebus;

namespace TeslaSolarCharger.Server.Services.Eebus;

public class EebusBridgeClient(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    : IEebusBridgeClient
{
    public async Task<DtoEebusLpcState> GetState(CancellationToken cancellationToken)
    {
        var apiPort = configuration.GetValue<int?>("EebusBridge:ApiPort") ?? 7401;
        var client = httpClientFactory.CreateClient(nameof(EebusBridgeClient));
        client.Timeout = TimeSpan.FromSeconds(3);
        var state = await client
            .GetFromJsonAsync<DtoEebusLpcState>($"http://127.0.0.1:{apiPort}/api/state", cancellationToken)
            .ConfigureAwait(false);
        return state ?? throw new InvalidOperationException("EEBUS bridge returned empty state");
    }
}
```

### 5.5 State holder with watchdog (`EebusLpcStateService`) — SAFETY CRITICAL

Registered as a **singleton**. This is the only class the charging loop talks to.

```csharp
// TeslaSolarCharger/Server/Services/Eebus/Contracts/IEebusLpcStateService.cs
using TeslaSolarCharger.Shared.Dtos.Eebus;

namespace TeslaSolarCharger.Server.Services.Eebus.Contracts;

public interface IEebusLpcStateService
{
    DtoEebusLpcState? LastState { get; }
    DateTimeOffset? LastSuccessfulPoll { get; }
    void UpdateState(DtoEebusLpcState state, DateTimeOffset polledAt);

    /// <summary>
    /// The consumption limit the charging loop must enforce right now.
    /// null = no restriction. SAFETY: returns the failsafe limit when the bridge
    /// is unreachable (no successful poll within the last 60 seconds) while the feature is enabled.
    /// </summary>
    int? GetEffectiveConsumptionLimitInWatts(DateTimeOffset now);
}
```

```csharp
// TeslaSolarCharger/Server/Services/Eebus/EebusLpcStateService.cs
using TeslaSolarCharger.Server.Services.Eebus.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Eebus;

namespace TeslaSolarCharger.Server.Services.Eebus;

public class EebusLpcStateService(IConfigurationWrapper configurationWrapper) : IEebusLpcStateService
{
    private static readonly TimeSpan BridgeUnreachableTimeout = TimeSpan.FromSeconds(60);
    private readonly object _lock = new();

    public DtoEebusLpcState? LastState { get; private set; }
    public DateTimeOffset? LastSuccessfulPoll { get; private set; }

    public void UpdateState(DtoEebusLpcState state, DateTimeOffset polledAt)
    {
        lock (_lock)
        {
            LastState = state;
            LastSuccessfulPoll = polledAt;
        }
    }

    public int? GetEffectiveConsumptionLimitInWatts(DateTimeOffset now)
    {
        if (!configurationWrapper.UseEebus14aGridControl())
        {
            return default;
        }

        lock (_lock)
        {
            // Watchdog: bridge dead or never seen -> failsafe limit. NEVER return null here.
            if (LastSuccessfulPoll == default || (now - LastSuccessfulPoll.Value) > BridgeUnreachableTimeout)
            {
                return configurationWrapper.Eebus14aFailsafeConsumptionLimit();
            }

            if (LastState?.EffectiveConsumptionLimitWatts == default)
            {
                return default;
            }
            return (int)LastState.EffectiveConsumptionLimitWatts.Value;
        }
    }
}
```

### 5.6 Poll job (Quartz, every 5 s)

Create the job following the existing pattern (see
[PvValueJob.cs](../TeslaSolarCharger/Server/Scheduling/Jobs/PvValueJob.cs)):

```csharp
// TeslaSolarCharger/Server/Scheduling/Jobs/EebusStatePollJob.cs
using Quartz;
using TeslaSolarCharger.Server.Services.Eebus.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class EebusStatePollJob(ILogger<EebusStatePollJob> logger, IEebusStatePollService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.PollBridgeState(context.CancellationToken).ConfigureAwait(false);
    }
}
```

The poll service does the actual work:

```csharp
// TeslaSolarCharger/Server/Services/Eebus/EebusStatePollService.cs
using Quartz;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.Eebus.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services.Eebus;

public class EebusStatePollService(
    ILogger<EebusStatePollService> logger,
    IConfigurationWrapper configurationWrapper,
    IEebusBridgeClient bridgeClient,
    IEebusLpcStateService stateService,
    ITscConfigurationService tscConfigurationService,
    IErrorHandlingService errorHandlingService,
    IDateTimeProvider dateTimeProvider,
    ISchedulerFactory schedulerFactory) : IEebusStatePollService
{
    private const string BridgeUnreachableIssueKey = "EebusBridgeUnreachable";
    private const string FailsafeActiveIssueKey = "EebusFailsafeActive";

    public async Task PollBridgeState(CancellationToken cancellationToken)
    {
        if (!configurationWrapper.UseEebus14aGridControl())
        {
            return;
        }

        var now = dateTimeProvider.DateTimeOffSetUtcNow();
        var previousLimit = stateService.GetEffectiveConsumptionLimitInWatts(now);

        try
        {
            var state = await bridgeClient.GetState(cancellationToken).ConfigureAwait(false);
            stateService.UpdateState(state, now);
            await errorHandlingService.HandleErrorResolved(BridgeUnreachableIssueKey, null).ConfigureAwait(false);

            if (state.Status == "failsafe")
            {
                await errorHandlingService.HandleError(nameof(EebusStatePollService), nameof(PollBridgeState),
                    "§14a failsafe state active",
                    "No heartbeat from the grid operator control box. Charging power is limited to the failsafe limit.",
                    FailsafeActiveIssueKey, null, null).ConfigureAwait(false);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(FailsafeActiveIssueKey, null).ConfigureAwait(false);
            }

            // Persist failsafe values the control box wrote so they survive restarts (LPC scenario 2).
            await tscConfigurationService.SetConfigurationValueByKey(EebusConstants.EgWrittenFailsafeLimitKey,
                state.FailsafeConsumptionLimitWatts.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .ConfigureAwait(false);
            await tscConfigurationService.SetConfigurationValueByKey(EebusConstants.EgWrittenFailsafeDurationKey,
                state.FailsafeDurationSeconds.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not poll EEBUS bridge state");
            await errorHandlingService.HandleError(nameof(EebusStatePollService), nameof(PollBridgeState),
                "EEBUS bridge unreachable",
                "The §14a EEBUS bridge process is not responding. The failsafe consumption limit is applied.",
                BridgeUnreachableIssueKey, null, ex.StackTrace).ConfigureAwait(false);
        }

        // If the effective limit changed, apply it immediately instead of waiting for the next
        // regular charging value run.
        var newLimit = stateService.GetEffectiveConsumptionLimitInWatts(dateTimeProvider.DateTimeOffSetUtcNow());
        if (newLimit != previousLimit)
        {
            logger.LogInformation("§14a effective consumption limit changed from {old}W to {new}W",
                previousLimit, newLimit);
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            await scheduler.TriggerJob(new JobKey(nameof(ChargingValueJob)), cancellationToken).ConfigureAwait(false);
        }
    }
}
```

(Create `IEebusStatePollService` with the single `PollBridgeState` method in `Contracts/`.)

**Register the job** in [JobManager.cs](../TeslaSolarCharger/Server/Scheduling/JobManager.cs) exactly like the
existing jobs: build the job (`JobBuilder.Create<EebusStatePollJob>()...`), build a trigger with
`SimpleScheduleBuilder.RepeatSecondlyForever(5)` starting at `currentDate.AddSeconds(10)`, and add both to the
job/trigger dictionary. Also register the job class itself in `ServiceCollectionExtensions.cs` the same way
the other job classes are registered (search for how `PvValueJob` is registered and mirror it).

**DI registrations** to add in `ServiceCollectionExtensions.cs`:

```csharp
.AddSingleton<IEebusLpcStateService, EebusLpcStateService>()
.AddTransient<IEebusBridgeClient, EebusBridgeClient>()
.AddTransient<IEebusStatePollService, EebusStatePollService>()
.AddHostedService<EebusBridgeProcessService>()
```

and `services.AddHttpClient(nameof(EebusBridgeClient));` if not covered by an existing generic
`AddHttpClient()` call.

### 5.7 Enforcing the limit in the charging loop — THE ACTUAL §14a COMPLIANCE

All changes go into
[ChargingServiceV2.SetNewChargingValues](../TeslaSolarCharger/Server/Services/ChargingServiceV2.cs).

**Why two mechanisms are needed:** `powerToControl` only steers the *solar surplus* distribution. Scheduled
charging (price-optimized, `chargingSchedulePower` in `TargetChargingValueCalculationService`) can command
full power regardless of `powerToControl`. The only budget that constrains **every** code path is the
combined-current budget (`maxCombinedCurrent`), which `AppendTargetValues` already supports via its
`reduceMaxCombinedCurrentBy` parameter (currently always called with `0`). So we cap both.

1. Inject `IEebusLpcStateService` into `ChargingServiceV2` (constructor + field, like the other services).

2. Directly **after** the line
   `var powerToControl = _powerToControlCalculationService.CalculatePowerToControl(chargingLoadPoints);`
   (currently line ~94) insert:

```csharp
var lpcLimitWatts = _eebusLpcStateService.GetEffectiveConsumptionLimitInWatts(currentDate);
var reduceMaxCombinedCurrentBy = 0;
if (lpcLimitWatts != default)
{
    _logger.LogInformation("§14a consumption limit active: {limit}W. Capping charging power.", lpcLimitWatts.Value);
    if (powerToControl > lpcLimitWatts.Value)
    {
        powerToControl = lpcLimitWatts.Value;
    }

    // Convert the watt limit into a combined-current budget. Worst case: every loadpoint
    // charges on three phases at 230V, so budgetAmps * 3 * 230 <= limitWatts is always safe.
    // This also caps price-optimized scheduled charging, which ignores powerToControl.
    var allowedCombinedCurrent = lpcLimitWatts.Value / (230 * 3);
    var configuredMaxCombinedCurrent = _configurationWrapper.MaxCombinedCurrent();
    if (configuredMaxCombinedCurrent > allowedCombinedCurrent)
    {
        reduceMaxCombinedCurrentBy = configuredMaxCombinedCurrent - allowedCombinedCurrent;
    }
    _notChargingWithExpectedPowerReasonHelper.AddGenericReason(
        new(TranslationKeys.NotChargingReasonGridOperatorLimit));
}
```

3. In the same method, find the call
   `await _targetChargingValueCalculationService.AppendTargetValues(targetChargingValues, activeChargingSchedules, currentDate, powerToControl, 0, cancellationToken);`
   and replace the hardcoded `0` with `reduceMaxCombinedCurrentBy`.

4. Add the translation key `NotChargingReasonGridOperatorLimit` to
   [TranslationKeys.cs](../TeslaSolarCharger/Shared/Localization/TranslationKeys.cs) and register English +
   German texts following the existing `NotChargingReason*` entries. Suggested texts:
   - EN: "The grid operator limits the charging power (§14a EnWG)."
   - DE: "Der Netzbetreiber begrenzt aktuell die Ladeleistung (§14a EnWG)."

**Notes on correctness:**
- The insertion point is *after* the stale-solar-value fallback inside `CalculatePowerToControl`, so the cap
  also applies when solar values are outdated.
- `MaxCombinedCurrent()` returns `int.MaxValue` when unconfigured; the arithmetic above still yields the
  correct budget (`int.MaxValue - (int.MaxValue - allowed) = allowed`) without overflow because
  `reduceMaxCombinedCurrentBy` is only the difference. Do not "simplify" this.
- A limit of `0` W results in a combined-current budget of `0` A → all charging stops. That is intended and
  spec-compliant (limits below 4,200 W are unusual but legal at the protocol level).
- Do NOT skip enforcement when `IsCharging == false` — new charge starts must also respect the budget (they
  do automatically because the budget applies inside `AppendTargetValues`).

### 5.8 API controller for the UI

```csharp
// TeslaSolarCharger/Server/Controllers/EebusController.cs
// Follow the conventions of the existing controllers (e.g. BaseConfigurationController):
```

Endpoints:

| Route | Action |
|---|---|
| `GET api/Eebus/State` | returns `IEebusLpcStateService.LastState` + `LastSuccessfulPoll` (+ enabled flag) |
| `GET api/Eebus/Ski` | returns the stored local SKI from `TscConfiguration` (empty if not generated) |
| `POST api/Eebus/GenerateIdentity` | **only if no identity exists** (return 400 otherwise): run the bridge executable with argument `generate-cert`, parse the stdout JSON (`certificatePem`, `privateKeyPem`, `ski`), store all three via `ITscConfigurationService`, return the SKI. Add an optional `force=true` query parameter that allows overwriting, and make the UI show a very loud warning before using it (see pitfalls #1). |

For `GenerateIdentity`, reuse `Process.Start` with `RedirectStandardOutput = true` and
`configuration.GetValue<string>("EebusBridge:ExecutablePath")`.

### 5.9 UI page

Create `TeslaSolarCharger/Client/Pages/GridControl.razor` (route `@page "/grid-control"`, MudBlazor components,
copy structural conventions from an existing simple page). Content, top to bottom:

1. **Intro text** explaining §14a (one paragraph, EN/DE via the localization system).
2. **Configuration hint**: the actual settings (enable toggle, control box SKI/IP, nominal max, failsafe
   values) live in Base Configuration — link there. (They are rendered automatically once added to
   `BaseConfigurationBase` + localization registry; verify they show up, otherwise add them to the page
   markup following neighboring fields.)
3. **Identity card**: shows the local SKI (from `GET api/Eebus/Ski`) with a copy button — the user gives this
   SKI to their metering point operator. If no identity exists: a "Generate identity" button calling
   `POST api/Eebus/GenerateIdentity`, then displaying the SKI.
4. **Live status card**: polls `GET api/Eebus/State` every 5 s and shows: bridge reachable (yes/no),
   control box connected (yes/no), heartbeat OK, status (Normal/Failsafe), active limit
   ("Unlimited" or "Limited to X W since HH:mm"), failsafe limit + duration.
   Use a red banner when status is failsafe or a limit is active.
5. Add the page to the navigation menu next to the existing pages.

### 5.10 Startup crash safety

`EebusBridgeProcessService` and `EebusStatePollJob` must never crash TSC startup. Both already swallow
exceptions per iteration — keep it that way. The feature being misconfigured must degrade to "error banner +
failsafe limit", never to an unhandled exception.

---

## 6. Part 4 — User-facing networking documentation

Add a documentation section (README / docs site) with this content:

- The EEBUS/SHIP server inside the TSC container listens on TCP port **4712**, and device discovery uses
  **mDNS (UDP 5353 multicast)**.
- With Docker's default bridge network, the control box **cannot reach TSC**. Users enabling §14a must either:
  1. **Recommended:** run the TSC service with `network_mode: host` (and remove the `ports:` mapping,
     since the app then listens on 80/7190 directly), or
  2. add `- 4712:4712` to the `ports:` section — this makes direct connections work but mDNS discovery will
    still fail; the control box then needs the TSC IP configured manually (not all control boxes support this).
- The user must send the **SKI shown on the Grid Control page** to their metering point operator / installer
  and enter the **control box's SKI** in TSC's Base Configuration.
- After changing certificates (identity regeneration) all pairings become invalid and the commissioning with
  the metering point operator must be repeated — this can take weeks. Do not regenerate.

---

## 7. Part 5 — Testing

### 7.1 Go unit tests (`state_test.go`)

Inject a fake clock (`s.now = func() time.Time {...}`). Required cases:

| # | Test | Expected |
|---|---|---|
| 1 | Fresh state machine, no events | status `normal`, effective limit `nil` |
| 2 | Active limit 4200 W received | effective limit 4200 |
| 3 | Limit with `IsActive=false` received after an active one | effective limit `nil` |
| 4 | Active limit with duration 60 s, clock advanced 61 s | effective limit `nil`, `consumptionLimitActive=false` |
| 5 | Clock advanced 2 min + 1 s without heartbeat | status `failsafe`, effective limit = failsafe limit |
| 6 | Heartbeat after failsafe | status `normal` again; a still-active limit becomes effective again |
| 7 | Active limit of exactly `0` W | effective limit `0` (NOT nil!) |
| 8 | EG writes new failsafe limit, then heartbeat lost | failsafe uses the NEW value |

Run with `go test ./...` — also add this to the CI workflow next to the .NET test step.

### 7.2 C# unit tests (xunit, in `TeslaSolarCharger.Tests`)

1. `EebusLpcStateService`:
   - feature disabled → always `null`.
   - enabled, never polled → failsafe limit (watchdog).
   - enabled, last poll 61 s ago → failsafe limit.
   - enabled, fresh poll, state says `effectiveConsumptionLimitWatts = 4200` → `4200`.
   - enabled, fresh poll, state says `null` → `null`.
2. The combined-current reduction math from section 5.7 (extract into a small pure helper if easier to test):
   - limit 4200 W, `MaxCombinedCurrent` unset (`int.MaxValue`) → budget 6 A (4200/690 = 6.08 → 6).
   - limit 4200 W, `MaxCombinedCurrent` = 32 → reduce by 26.
   - limit 0 W → budget 0 A.
   - no limit → reduce by 0.

### 7.3 Manual integration test with a simulated control box

The eebus-go repository contains a **control box example** (`examples/controlbox`) that implements the EG
side of LPC — use it as the test counterpart. Run it on a second machine in the same LAN (or on the Docker
host with host networking):

1. Clone `github.com/enbility/eebus-go`, look at the example's usage output/README
   (`go run ./examples/controlbox -h`). It generates/uses its own certificate and prints its SKI.
2. In TSC (running with `network_mode: host` for this test): generate the identity, note TSC's SKI.
3. Start the control box example with TSC's SKI as the trusted remote; enter the control box's SKI in TSC's
   Base Configuration and enable §14a.
4. Wait for "control box connected: yes" on the Grid Control page (check bridge logs in TSC's log output,
   lines prefixed `eebus-bridge:`).
5. Use the control box example's console commands to write a consumption limit and toggle it.

### 7.4 Manual test protocol (execute all before merging)

| # | Action | Expected result |
|---|---|---|
| 1 | Fresh install, feature disabled | No bridge process running (`ps aux` in container), no errors, charging unchanged |
| 2 | Enable §14a without generating identity | Error banner asking to generate identity; no crash |
| 3 | Generate identity | SKI (40 hex chars) shown; survives TSC restart and container recreation (stored in DB) |
| 4 | Enable with control box SKI configured | Bridge process starts within 30 s; `GET api/Eebus/State` returns data |
| 5 | Control box connects | Grid Control page shows connected + heartbeat OK |
| 6 | Control box sends active limit 4200 W while a car charges at 11 kW | Within ~10 s TSC reduces charging; combined charging power settles ≤ 4200 W; "not charging with expected power" reason shown |
| 7 | Control box deactivates the limit | Charging power returns to normal within one charging interval |
| 8 | Limit with duration 2 min, then control box says nothing | Limit auto-releases after 2 min |
| 9 | Stop the control box (heartbeat loss) | After ≤ 2 min: failsafe banner, charging capped to failsafe limit |
| 10 | Restart control box | Failsafe cleared, normal operation |
| 11 | `kill` the bridge process inside the container | Error banner "bridge unreachable"; charging capped to failsafe limit (watchdog); process auto-restarts ≤ 30 s; banner clears |
| 12 | Restart TSC container during an active limit | After startup, before first successful poll the failsafe limit applies; after poll the real limit applies |
| 13 | Change failsafe limit from the control box, restart container | New failsafe value is used (persisted via TscConfiguration) |
| 14 | Second EEBUS device on the LAN tries to pair | Pairing denied (only the configured SKI is trusted) |
| 15 | Limit 0 W | All managed charging stops; resumes when limit released |

---

## 8. Acceptance criteria

- [ ] `TeslaSolarCharger.EebusBridge` builds with `go build` and `go vet` is clean; `go test ./...` passes.
- [ ] Docker image builds for amd64 and arm64 and contains `/app/eebus-bridge`.
- [ ] The bridge binary is started/stopped/restarted exclusively by the .NET app; no compose change needed to run it.
- [ ] All items of the manual test protocol (7.4) pass.
- [ ] Failsafe behavior is triple-covered: EG heartbeat loss (bridge), bridge unreachable (.NET watchdog), TSC restart (pre-first-poll default).
- [ ] Every new user-facing property/text has English and German localization.
- [ ] No secret (private key, config JSON) appears in any log output.
- [ ] Documentation section from Part 4 published.
- [ ] Unit tests from 7.1 and 7.2 exist and run in CI.

---

## 9. Common pitfalls

1. **NEVER regenerate the certificate once the SKI is registered with the metering point operator.** The SKI
   *is* the identity. Regenerating means re-doing the commissioning with the grid operator, which can take
   weeks. This is why `GenerateIdentity` must refuse to overwrite without `force=true` + UI warning.
2. **A 0 W limit is a valid active limit.** Only `IsActive`/`consumptionLimitActive` decides whether a limit
   applies — never treat `Value == 0` as "no limit".
3. **Units are watts everywhere** in LPC. Do not convert to kW anywhere in the pipeline.
4. **Do not bind the bridge HTTP API to 0.0.0.0** — it is unauthenticated localhost IPC.
5. **eebus-go API drift**: expect small compile fixes against the pinned version; use `examples/hems` in the
   eebus-go repo as ground truth. evcc pins *forks* of these libraries — when comparing with evcc code,
   signatures may differ from upstream.
6. **Never log the bridge config JSON** (contains the private key). Log field names, not payloads.
7. **The `reduceMaxCombinedCurrentBy` mechanism is what makes scheduled (price-optimized) charging comply.**
   Capping only `powerToControl` is NOT sufficient — solar surplus is only one of the charging modes.
8. **Do not add a Quartz job with an await-less `async void`** or block on `.Result` — copy an existing job.
9. **Keep the poll job at 5 s and the bridge state lazy.** Do not "optimize" by polling less often; the
   watchdog and limit-response times depend on it.
10. **The stale-solar-values fallback** in `CalculatePowerToControl` returns current charging power as
    `powerToControl` — the §14a cap must still be applied after it (which the insertion point in 5.7
    guarantees; don't move the code above that line).
11. **`MaxCombinedCurrent()` returns `int.MaxValue` when unset** — the difference arithmetic in 5.7 handles
    it; don't replace it with `Math.Min(configured, allowed)` style logic on the *budget* value passed as
    `reduceMaxCombinedCurrentBy` (that parameter is a *reduction*, not a budget).
12. **Home battery:** TSC does not control the home battery's charging power, so it cannot be part of the
    §14a-controlled load. Only the wallboxes/cars managed by TSC are covered. Mention this in the user docs;
    the wallbox is the registered SteuVE.
13. **Windows development:** the bridge runs fine on Windows (`go build`), mDNS may behave differently than
    in the container — do final verification in Docker on Linux.
14. **SKI input normalization:** users paste SKIs with spaces/dashes (`1234-5678-...`). Normalize (strip
    separators, lowercase) before passing to the bridge — done in `BuildBridgeConfigJson`, keep it.

---

## 10. Reference: evcc

evcc (https://github.com/evcc-io/evcc, local checkout at `C:\Users\patri\repos\evcc`) has a production-proven
implementation of exactly this feature. When in doubt, read:

| File | What to learn from it |
|---|---|
| `server/eebus/eebus.go` | SHIP service setup, trust handling, use case registration |
| `hems/eebus/eebus.go` | §14a state machine (`run()`), failsafe handling, initial value setup |
| `hems/eebus/events.go` | Which LPC events exist and how to react (approval logic!) |
| `server/eebus/certificate.go` | Certificate/SKI helpers |
| `server/eebus/scenarios.go` | LPC scenario numbers and their meaning |

Differences to remember: evcc links eebus-go in-process (Go app) and uses evcc-io forks of the libraries;
we use upstream `enbility/eebus-go` in a child process with an HTTP API.
