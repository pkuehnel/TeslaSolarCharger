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

// The unsolicited lines share stdout with request answers, so they have to be single line JSON like everything else
// and they must be routable by "kind" alone - they carry no id.
func TestUnsolicitedEventsAreSingleLineAndCarryTheirKind(t *testing.T) {
	digest, err := json.Marshal(advertisementEvent{Kind: "adv", WindowMs: 500, Total: 3})
	if err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if strings.Contains(string(digest), "\n") {
		t.Fatalf("an advertisement digest must serialize to a single line: %q", digest)
	}
	if !strings.Contains(string(digest), "\"kind\":\"adv\"") {
		t.Fatalf("an advertisement digest must be routable by kind: %q", digest)
	}
	state, err := json.Marshal(scanStateEvent{Kind: "scan", State: "paused", Reason: "radio handed over"})
	if err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if !strings.Contains(string(state), "\"kind\":\"scan\"") {
		t.Fatalf("a scan state event must be routable by kind: %q", state)
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
	if kind := requestKind(request{Kind: "ping"}); kind != "ping" {
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
	line := `{"id":3,"kind":"command","vin":"VIN1","command":"state","params":["charge"]}`
	if err := json.Unmarshal([]byte(line), &req); err != nil {
		t.Fatalf("unexpected error: %s", err)
	}
	if req.Id != 3 || req.Vin != "VIN1" || len(req.Params) != 1 {
		t.Fatalf("unexpected parse result: %+v", req)
	}
}
