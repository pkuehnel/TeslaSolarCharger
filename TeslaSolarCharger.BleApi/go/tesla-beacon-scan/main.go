// tesla-beacon-scan: passive BLE presence detection for Tesla vehicles.
//
// Tesla vehicles continuously advertise (awake and asleep) with a local name deterministically derived
// from the VIN. This tool scans for that advertisement WITHOUT connecting, so it can never wake the car.
// It also counts every other advertisement heard during the scan window: the caller uses that count to
// distinguish "car is absent" (radio provably received other traffic) from "radio is deaf/starved"
// (nothing received at all), which is impossible to tell apart from a connect timeout of tesla-control.
//
// This file is copied into cmd/tesla-beacon-scan/ of the teslamotors/vehicle-command module during the
// Docker image build (see TeslaSolarCharger.BleApi/Dockerfile), so it compiles inside that module and
// reuses its go-ble dependency.
//
// Output: a single line of JSON on stdout, exit code 0 whenever the scan executed (beacon found or not).
// Adapter/scan errors are written to stderr with exit code 1.
package main

import (
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/go-ble/ble"
	"github.com/go-ble/ble/linux"
	hcicmd "github.com/go-ble/ble/linux/hci/cmd"
	vcble "github.com/teslamotors/vehicle-command/pkg/connector/ble"
)

type scanResult struct {
	BeaconFound             bool    `json:"beaconFound"`
	Rssi                    *int    `json:"rssi"`
	Address                 *string `json:"address"`
	Connectable             *bool   `json:"connectable"`
	OtherAdvertisementsSeen int     `json:"otherAdvertisementsSeen"`
	DistinctDevicesSeen     int     `json:"distinctDevicesSeen"`
	ScanDurationMs          int64   `json:"scanDurationMs"`
}

func main() {
	var (
		vin       string
		timeout   time.Duration
		btAdapter string
	)
	flag.StringVar(&vin, "vin", "", "Vehicle Identification Number (VIN) of the car to scan for")
	flag.DurationVar(&timeout, "timeout", 5*time.Second, "How long to scan before reporting the beacon as not found")
	flag.StringVar(&btAdapter, "bt-adapter", "", "Optional ID of the Bluetooth adapter to use (hciX)")
	flag.Parse()

	if vin == "" {
		fmt.Fprintln(os.Stderr, "Error: -vin is required")
		os.Exit(1)
	}

	device, err := newDevice(btAdapter)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error: failed to enable BLE device: %s\n", err)
		os.Exit(1)
	}
	defer func() { _ = device.Stop() }()

	localName := vcble.VehicleLocalName(vin)
	result := scanResult{}
	seenAddresses := map[string]bool{}
	var mu sync.Mutex

	ctx, cancel := context.WithTimeout(context.Background(), timeout)
	defer cancel()

	start := time.Now()
	handler := func(a ble.Advertisement) {
		mu.Lock()
		defer mu.Unlock()
		if result.BeaconFound {
			// The scan is already being cancelled; ignore stragglers.
			return
		}
		seenAddresses[a.Addr().String()] = true
		if a.LocalName() == localName {
			rssi := a.RSSI()
			address := a.Addr().String()
			connectable := a.Connectable()
			result.BeaconFound = true
			result.Rssi = &rssi
			result.Address = &address
			result.Connectable = &connectable
			cancel()
			return
		}
		result.OtherAdvertisementsSeen++
	}

	// Allow duplicates so every received advertisement counts: the total is the caller's evidence that
	// the radio actually received something during the scan window.
	err = device.Scan(ctx, true, handler)
	if err != nil && !errors.Is(err, context.Canceled) && !errors.Is(err, context.DeadlineExceeded) {
		fmt.Fprintf(os.Stderr, "Error: scan failed: %s\n", err)
		os.Exit(1)
	}

	mu.Lock()
	result.ScanDurationMs = time.Since(start).Milliseconds()
	result.DistinctDevicesSeen = len(seenAddresses)
	mu.Unlock()
	if err := json.NewEncoder(os.Stdout).Encode(result); err != nil {
		fmt.Fprintf(os.Stderr, "Error: failed to encode result: %s\n", err)
		os.Exit(1)
	}
}

// newDevice creates a BLE device with the same options as vehicle-command's BLE connector
// (pkg/connector/ble/device_linux.go) so scanning behaves identically to tesla-control's beacon scan.
func newDevice(btAdapter string) (ble.Device, error) {
	opts := []ble.Option{
		ble.OptDialerTimeout(20 * time.Second),
		ble.OptListenerTimeout(20 * time.Second),
		ble.OptScanParams(hcicmd.LESetScanParameters{
			LEScanType:           1,    // Active scanning
			LEScanInterval:       0x10, // 10ms
			LEScanWindow:         0x10, // 10ms
			OwnAddressType:       0,    // Static
			ScanningFilterPolicy: 2,    // Basic filtered
		}),
	}
	if btAdapter != "" {
		if !strings.HasPrefix(btAdapter, "hci") {
			return nil, fmt.Errorf("invalid bluetooth adapter ID: %s", btAdapter)
		}
		hciID, err := strconv.Atoi(strings.TrimPrefix(btAdapter, "hci"))
		if err != nil || hciID < 0 || hciID > 15 {
			return nil, fmt.Errorf("invalid bluetooth adapter ID: %s", btAdapter)
		}
		opts = append(opts, ble.OptDeviceID(hciID))
	}
	return linux.NewDeviceWithName("tesla-beacon-scan", opts...)
}
