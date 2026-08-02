// These tests run during the Docker image build (go test ./cmd/tesla-bled/) and guard the protocol the BLE API
// sends to the worker plus the outcome classification: a mismatch there is invisible until a car stops answering or
// a present car is reported as away, so it must fail the image build instead.
package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"strings"
	"testing"

	"github.com/teslamotors/vehicle-command/pkg/protocol"
	"github.com/teslamotors/vehicle-command/pkg/vehicle"
)

// TeslaSolarCharger sends the state category as a parameter. Reading it from the command string only made every
// charge state read fail with "state requires a category", which froze the whole car state in TeslaSolarCharger.
func TestStateCategoryFromParams(t *testing.T) {
	category, err := stateCategory(request{Command: "state", Params: []string{"charge"}})
	if err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if category != vehicle.StateCategoryCharge {
		t.Fatalf("expected charge category, got %v", category)
	}
}

func TestStateCategoryFromCommandSuffix(t *testing.T) {
	category, err := stateCategory(request{Command: "state drive"})
	if err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if category != vehicle.StateCategoryDrive {
		t.Fatalf("expected drive category, got %v", category)
	}
}

func TestStateCategoryMissing(t *testing.T) {
	if _, err := stateCategory(request{Command: "state"}); err == nil {
		t.Fatal("expected an error for a missing category")
	}
}

func TestCommandNeedsInfotainment(t *testing.T) {
	cases := []struct {
		command string
		needs   bool
		wantErr bool
	}{
		{"body-controller-state", false, false},
		{"wake", false, false},
		{"state charge", true, false},
		{"charging-start", true, false},
		{"charging-stop", true, false},
		{"charging-set-amps", true, false},
		{"charging-set-limit", true, false},
		{"flash-lights", true, false},
		{"self-destruct", false, true},
		{"", false, true},
	}
	for _, c := range cases {
		needs, err := commandNeedsInfotainment(c.command)
		if c.wantErr != (err != nil) {
			t.Fatalf("command %q: unexpected error state: %v", c.command, err)
		}
		if err == nil && needs != c.needs {
			t.Fatalf("command %q: expected needsInfotainment=%v", c.command, c.needs)
		}
	}
}

func TestClassifyExecuteErrorCarRefusal(t *testing.T) {
	refusal := &protocol.NominalError{Details: protocol.NewError("car could not execute command: is_charging", false, false)}
	outcome, _, carError := classifyExecuteError(fmt.Errorf("wrapped: %w", refusal))
	if outcome != outcomeCarRefused {
		t.Fatalf("expected carRefused, got %s", outcome)
	}
	if carError != "is_charging" {
		t.Fatalf("expected the refusal reason without prefix, got %q", carError)
	}
}

func TestClassifyExecuteErrorVcsecRefusal(t *testing.T) {
	outcome, _, _ := classifyExecuteError(fmt.Errorf("wrapped: %w", &protocol.NominalVCSECError{}))
	if outcome != outcomeCarRefused {
		t.Fatalf("expected carRefused, got %s", outcome)
	}
}

func TestClassifyExecuteErrorDefaultIsLinkFailedWithSanitizedText(t *testing.T) {
	outcome, text, carError := classifyExecuteError(errors.New("failed to send message: context deadline exceeded"))
	if outcome != outcomeLinkFailed {
		t.Fatalf("expected linkFailed, got %s", outcome)
	}
	if carError != "" {
		t.Fatalf("expected no car error, got %q", carError)
	}
	if strings.Contains(text, "context deadline exceeded") {
		t.Fatalf("text must not contain the wording an old server classifies as out of range: %q", text)
	}
	if !strings.Contains(text, "timed out") {
		t.Fatalf("expected the sanitized wording, got %q", text)
	}
}

func TestSanitizeErrorTextNeverContainsBeaconOrDeadline(t *testing.T) {
	text := sanitizeErrorText("ble: failed to scan beacon Beacon: context deadline exceeded")
	if strings.Contains(strings.ToLower(text), "beacon") {
		t.Fatalf("sanitized text must not contain 'beacon': %q", text)
	}
	if strings.Contains(text, "context deadline exceeded") {
		t.Fatalf("sanitized text must not contain 'context deadline exceeded': %q", text)
	}
}

// The carAbsent message is the only one that may (and must) contain the word "beacon": an old TSC server recognizes
// an out of range car by that word during a rollout window.
func TestAbsentMessageKeepsTheBeaconWord(t *testing.T) {
	message := absentMessage("5YJ3E1EA1PF000000", &scanInfo{ScanDurationMs: 3000, OtherAdvertisementsSeen: 12, DistinctDevicesSeen: 4})
	if !strings.Contains(message, "beacon") {
		t.Fatalf("absent message must contain 'beacon' for old server compatibility: %q", message)
	}
	if strings.Contains(message, "context deadline exceeded") {
		t.Fatalf("absent message must not contain 'context deadline exceeded': %q", message)
	}
}

// One JSON object per line is the framing contract with the C# supervisor; a multi line response would desynchronize
// the protocol.
func TestResponseMarshalsToSingleLine(t *testing.T) {
	payload, err := json.Marshal(response{Kind: "result", Id: 7, Error: "line one\nline two"})
	if err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if strings.Contains(string(payload), "\n") {
		t.Fatalf("response must serialize to a single line: %q", payload)
	}
}

func TestRequestKindInference(t *testing.T) {
	if kind := requestKind(request{Kind: "beaconScan"}); kind != "beaconScan" {
		t.Fatalf("explicit kind must win, got %q", kind)
	}
	if kind := requestKind(request{Command: "wake"}); kind != "command" {
		t.Fatalf("a request with only a command is a command request, got %q", kind)
	}
	if kind := requestKind(request{}); kind != "" {
		t.Fatalf("an empty request has no kind, got %q", kind)
	}
}

func TestRequestParsing(t *testing.T) {
	var req request
	line := `{"id":3,"kind":"beaconScan","vins":["VIN1","VIN2"],"windowMs":3000}`
	if err := json.Unmarshal([]byte(line), &req); err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if req.Id != 3 || len(req.Vins) != 2 || req.WindowMs != 3000 {
		t.Fatalf("unexpected parse result: %+v", req)
	}
}
