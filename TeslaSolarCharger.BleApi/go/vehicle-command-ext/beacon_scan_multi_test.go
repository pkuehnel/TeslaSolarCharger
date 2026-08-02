// Runs during the Docker image build (go test ./pkg/connector/ble/) together with the injected
// beacon_scan_multi.go. Guards the multi-VIN matching and the advertisement counters TeslaSolarCharger relies on
// for presence and radio evidence.
package ble

import "testing"

func TestBeaconAggregatorMultiVin(t *testing.T) {
	vin1 := "5YJ3E1EA1PF000001"
	vin2 := "5YJ3E1EA1PF000002"
	agg := newBeaconAggregator([]string{vin1, vin2})

	if allFound := agg.observe("some-phone", "11:11:11:11:11:11", -80, false); allFound {
		t.Fatal("an unrelated advertisement must not end the scan")
	}
	if allFound := agg.observe(VehicleLocalName(vin1), "aa:aa:aa:aa:aa:aa", -60, true); allFound {
		t.Fatal("one of two cars must not end the scan")
	}
	//Duplicates are allowed during the scan; a repeated car advertisement must neither end the scan nor count as
	//another device.
	if allFound := agg.observe(VehicleLocalName(vin1), "aa:aa:aa:aa:aa:aa", -61, true); allFound {
		t.Fatal("a duplicate car advertisement must not end the scan")
	}
	if allFound := agg.observe(VehicleLocalName(vin2), "bb:bb:bb:bb:bb:bb", -70, true); !allFound {
		t.Fatal("hearing every car must end the scan early")
	}

	summary := agg.finish()
	if !summary.Vehicles[0].BeaconFound || !summary.Vehicles[1].BeaconFound {
		t.Fatalf("both cars must be reported found: %+v", summary.Vehicles)
	}
	if summary.Vehicles[0].Rssi != -60 {
		t.Fatalf("the first advertisement wins, got rssi %d", summary.Vehicles[0].Rssi)
	}
	if summary.OtherAdvertisementsSeen != 1 {
		t.Fatalf("expected 1 other advertisement, got %d", summary.OtherAdvertisementsSeen)
	}
	if summary.DistinctDevicesSeen != 3 {
		t.Fatalf("expected 3 distinct devices, got %d", summary.DistinctDevicesSeen)
	}
}

func TestBeaconAggregatorAbsentCarCountsRadioEvidence(t *testing.T) {
	agg := newBeaconAggregator([]string{"5YJ3E1EA1PF000001"})
	agg.observe("some-phone", "11:11:11:11:11:11", -80, false)
	agg.observe("some-phone", "11:11:11:11:11:11", -81, false)
	agg.observe("some-tv", "22:22:22:22:22:22", -75, false)

	summary := agg.finish()
	if summary.Vehicles[0].BeaconFound {
		t.Fatal("the car was never heard and must not be reported found")
	}
	if summary.OtherAdvertisementsSeen != 3 {
		t.Fatalf("every received advertisement counts (duplicates included), got %d", summary.OtherAdvertisementsSeen)
	}
	if summary.DistinctDevicesSeen != 2 {
		t.Fatalf("expected 2 distinct devices, got %d", summary.DistinctDevicesSeen)
	}
}

func TestBeaconAggregatorIgnoresStragglersAfterAllFound(t *testing.T) {
	vin := "5YJ3E1EA1PF000001"
	agg := newBeaconAggregator([]string{vin})
	agg.observe(VehicleLocalName(vin), "aa:aa:aa:aa:aa:aa", -60, true)
	agg.observe("late-phone", "33:33:33:33:33:33", -90, false)

	summary := agg.finish()
	if summary.OtherAdvertisementsSeen != 0 {
		t.Fatalf("advertisements after the scan ended must not count, got %d", summary.OtherAdvertisementsSeen)
	}
}
