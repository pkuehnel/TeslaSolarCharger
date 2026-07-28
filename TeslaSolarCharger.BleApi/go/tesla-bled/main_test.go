package main

import (
	"testing"

	"github.com/teslamotors/vehicle-command/pkg/vehicle"
)

// TeslaSolarCharger sends the state category as a parameter. Reading it from the command string only made every
// charge state read fail with "state requires a category", which froze the whole car state in TeslaSolarCharger.
func TestStateCategory(t *testing.T) {
	testCases := []struct {
		name     string
		request  request
		expected vehicle.StateCategory
		wantErr  bool
	}{
		{
			name:     "category as parameter (as sent by TeslaSolarCharger)",
			request:  request{Command: "state", Params: []string{"charge"}},
			expected: vehicle.StateCategoryCharge,
		},
		{
			name:     "category appended to the command",
			request:  request{Command: "state charge"},
			expected: vehicle.StateCategoryCharge,
		},
		{
			name:    "no category at all",
			request: request{Command: "state"},
			wantErr: true,
		},
		{
			name:    "unknown category",
			request: request{Command: "state", Params: []string{"unknown"}},
			wantErr: true,
		},
	}

	for _, testCase := range testCases {
		t.Run(testCase.name, func(t *testing.T) {
			category, err := stateCategory(testCase.request)
			if testCase.wantErr {
				if err == nil {
					t.Fatalf("expected an error, got category %v", category)
				}
				return
			}
			if err != nil {
				t.Fatalf("unexpected error: %s", err)
			}
			if category != testCase.expected {
				t.Fatalf("expected category %v, got %v", testCase.expected, category)
			}
		})
	}
}
