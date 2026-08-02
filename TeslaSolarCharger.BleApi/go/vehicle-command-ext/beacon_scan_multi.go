// Multi-VIN beacon scan for the TeslaSolarCharger BLE worker.
//
// This file is copied into pkg/connector/ble/ of the teslamotors/vehicle-command module during the Docker image
// build (see TeslaSolarCharger.BleApi/Dockerfile): it needs the package private adapter singleton (device/mu), which
// is why it cannot live in the worker's own package. If an upstream change breaks it, the image build fails loudly -
// that is intended, a silent runtime surprise at a user's site would be worse.
//
// The upstream ScanVehicleBeacon scans for one VIN and drops duplicates, so it can neither share a scan window
// between cars nor count the other advertisements the radio heard. Both matter to TeslaSolarCharger: an absent car
// costs the window once instead of once per car, and the advertisement counters are the only evidence that a silent
// scan came from a working radio.
package ble

import (
	"context"
	"errors"
	"sync"
	"time"

	"github.com/go-ble/ble"
)

// VehicleBeacon is the per-VIN result of ScanBeacons.
type VehicleBeacon struct {
	Vin          string
	BeaconFound  bool
	Rssi         int
	Address      string
	Connectable  bool
	FoundAfterMs int64
}

type BeaconScanSummary struct {
	ScanDurationMs          int64
	OtherAdvertisementsSeen int
	DistinctDevicesSeen     int
	Vehicles                []VehicleBeacon
}

// ScanBeacons listens for the advertisements of all given VINs at once without connecting to any car, so it can
// never wake one. Duplicates are allowed so every received advertisement counts: the OtherAdvertisementsSeen total
// is the caller's evidence that the radio actually received something during the scan window. The scan ends early
// once every VIN was heard; an absent car makes the scan run to the context deadline, which is the normal "car is
// away" answer and not an error.
func ScanBeacons(ctx context.Context, vins []string) (*BeaconScanSummary, error) {
	mu.Lock()
	defer mu.Unlock()
	if err := initAdapter(nil); err != nil {
		return nil, err
	}

	aggregator := newBeaconAggregator(vins)
	scanCtx, cancel := context.WithCancel(ctx)
	defer cancel()

	var handlerMu sync.Mutex
	handler := func(a ble.Advertisement) {
		handlerMu.Lock()
		defer handlerMu.Unlock()
		if aggregator.observe(a.LocalName(), a.Addr().String(), a.RSSI(), a.Connectable()) {
			//Every VIN was heard; the deadline only exists for absent cars.
			cancel()
		}
	}

	err := device.Scan(scanCtx, true, handler)
	handlerMu.Lock()
	defer handlerMu.Unlock()
	summary := aggregator.finish()
	if err != nil && !errors.Is(err, context.Canceled) && !errors.Is(err, context.DeadlineExceeded) {
		return nil, err
	}
	return summary, nil
}

// beaconAggregator collects one scan window's advertisements. It is separated from the go-ble handler so the
// matching and counting logic stays testable without a radio; the caller provides the locking.
type beaconAggregator struct {
	byLocalName   map[string]int
	summary       *BeaconScanSummary
	seenAddresses map[string]bool
	remaining     int
	start         time.Time
}

func newBeaconAggregator(vins []string) *beaconAggregator {
	aggregator := &beaconAggregator{
		byLocalName:   make(map[string]int, len(vins)),
		summary:       &BeaconScanSummary{Vehicles: make([]VehicleBeacon, len(vins))},
		seenAddresses: make(map[string]bool),
		remaining:     len(vins),
		start:         time.Now(),
	}
	for i, vin := range vins {
		aggregator.summary.Vehicles[i] = VehicleBeacon{Vin: vin}
		aggregator.byLocalName[VehicleLocalName(vin)] = i
	}
	return aggregator
}

// observe records one advertisement and reports whether every scanned-for VIN has now been heard.
func (agg *beaconAggregator) observe(localName string, address string, rssi int, connectable bool) bool {
	if agg.remaining == 0 {
		//The scan is already being cancelled; ignore stragglers.
		return false
	}
	agg.seenAddresses[address] = true
	if index, ok := agg.byLocalName[localName]; ok {
		beacon := &agg.summary.Vehicles[index]
		if !beacon.BeaconFound {
			beacon.BeaconFound = true
			beacon.Rssi = rssi
			beacon.Address = address
			beacon.Connectable = connectable
			beacon.FoundAfterMs = time.Since(agg.start).Milliseconds()
			agg.remaining--
		}
		return agg.remaining == 0
	}
	agg.summary.OtherAdvertisementsSeen++
	return false
}

func (agg *beaconAggregator) finish() *BeaconScanSummary {
	agg.summary.ScanDurationMs = time.Since(agg.start).Milliseconds()
	agg.summary.DistinctDevicesSeen = len(agg.seenAddresses)
	return agg.summary
}
