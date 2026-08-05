// Runs during the Docker image build (go test -race ./pkg/connector/ble/) together with the injected
// presence_scan.go. The radio is injected, so everything below runs without a Bluetooth adapter: the matching, the
// address learning and the gap accounting are pure, and the scan loop is exercised through a fake radio.
//
// The arbiter tests are the ones that matter most: they assert the property the whole design rests on, namely that a
// command can never be starved by the background scan and never runs while the adapter is scanning.
package ble

import (
	"context"
	"fmt"
	"sync"
	"testing"
	"time"
)

const testVin = "5YJ3E1EA1PF000001"

func TestIsVehicleLocalName(t *testing.T) {
	if !IsVehicleLocalName(VehicleLocalName(testVin)) {
		t.Fatalf("the name produced by VehicleLocalName must be recognized: %q", VehicleLocalName(testVin))
	}
	for _, name := range []string{"", "some-phone", "S0011223344556677", "0011223344556677C", "SZZ11223344556677C"} {
		if IsVehicleLocalName(name) {
			t.Fatalf("%q must not be recognized as a vehicle local name", name)
		}
	}
}

func TestRegistryRecordsNamedAdvertisements(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	name := VehicleLocalName(testVin)

	registry.observe("some-phone", "11:11:11:11:11:11", -80, false, start)
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start.Add(time.Second))
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -62, true, start.Add(41*time.Second))

	presence := registry.presenceOf(name, time.Minute, start.Add(42*time.Second))
	if !presence.Heard {
		t.Fatal("a car heard one second ago must count as present")
	}
	if presence.Count != 2 || presence.NamedCount != 2 || presence.AddressCount != 0 {
		t.Fatalf("unexpected counters: %+v", presence)
	}
	if presence.Rssi == nil || *presence.Rssi != -62 {
		t.Fatalf("the newest advertisement wins: %+v", presence.Rssi)
	}
	if presence.MaxGapMs != 40000 || presence.MedianGapMs != 40000 {
		t.Fatalf("expected a 40 s gap, got max %d median %d", presence.MaxGapMs, presence.MedianGapMs)
	}
	if presence.Address == nil || *presence.Address != "aa:aa:aa:aa:aa:aa" {
		t.Fatalf("unexpected address: %+v", presence.Address)
	}
}

func TestRegistryMaxAge(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	name := VehicleLocalName(testVin)
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start)

	if registry.presenceOf(name, 90*time.Second, start.Add(89*time.Second)).Heard != true {
		t.Fatal("inside the max age the car must count as present")
	}
	if registry.presenceOf(name, 90*time.Second, start.Add(91*time.Second)).Heard != false {
		t.Fatal("outside the max age the car must not count as present")
	}
	if registry.presenceOf(VehicleLocalName("5YJ3E1EA1PF000009"), 90*time.Second, start).Heard != false {
		t.Fatal("a car that was never heard must not count as present")
	}
}

// A car whose local name only travels in the scan response is heard as a nameless advertisement most of the time.
// Counting those is the difference between hearing the car every 40 s and hearing it constantly.
func TestRegistryLearnsTheAddressAndCountsNamelessAdvertisements(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	name := VehicleLocalName(testVin)

	registry.observe("", "aa:aa:aa:aa:aa:aa", -60, true, start)
	if registry.presenceOf(name, time.Minute, start).Heard {
		t.Fatal("an unknown address must not be attributed to a car")
	}
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start.Add(time.Second))
	registry.observe("", "aa:aa:aa:aa:aa:aa", -61, true, start.Add(2*time.Second))

	presence := registry.presenceOf(name, time.Minute, start.Add(2*time.Second))
	if presence.Count != 2 || presence.NamedCount != 1 || presence.AddressCount != 1 {
		t.Fatalf("the nameless advertisement of a learned address must count: %+v", presence)
	}
	if presence.LastSource != sourceAddress {
		t.Fatalf("expected the last source to be the learned address, got %q", presence.LastSource)
	}
}

func TestRegistryAddressBindingExpires(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(time.Minute, start)
	name := VehicleLocalName(testVin)
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start)
	registry.observe("", "aa:aa:aa:aa:aa:aa", -61, true, start.Add(2*time.Minute))

	presence := registry.presenceOf(name, 10*time.Minute, start.Add(2*time.Minute))
	if presence.Count != 1 {
		t.Fatalf("an advertisement after the binding expired must not count: %+v", presence)
	}
}

// A rotated address must not keep counting for the car, otherwise a device that inherits it would be reported as a
// car standing at home.
func TestRegistryRebindingDropsThePreviousAddress(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	name := VehicleLocalName(testVin)
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start)
	registry.observe(name, "bb:bb:bb:bb:bb:bb", -60, true, start.Add(time.Second))
	registry.observe("", "aa:aa:aa:aa:aa:aa", -61, true, start.Add(2*time.Second))

	presence := registry.presenceOf(name, time.Minute, start.Add(2*time.Second))
	if presence.AddressCount != 0 {
		t.Fatalf("the previous address must not count anymore: %+v", presence)
	}
	if presence.Address == nil || *presence.Address != "bb:bb:bb:bb:bb:bb" {
		t.Fatalf("unexpected address: %+v", presence.Address)
	}
}

// Presence proven by a command keeps a car present while its own traffic occupies the radio, but it must not pretend
// the radio heard an advertisement: the gap statistics are the evidence for how often a car actually advertises.
func TestRegistryNoteDoesNotDistortTheRadioEvidence(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	name := VehicleLocalName(testVin)
	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, start)
	registry.note(name, sourceCommand, start.Add(30*time.Second))

	presence := registry.presenceOf(name, time.Minute, start.Add(31*time.Second))
	if !presence.Heard {
		t.Fatal("a car that answered a command must count as present")
	}
	if presence.Count != 1 || presence.MaxGapMs != 0 {
		t.Fatalf("a command must not count as an advertisement: %+v", presence)
	}
	if presence.LastAdvertisementMsAgo == nil || *presence.LastAdvertisementMsAgo != 31000 {
		t.Fatalf("the last advertisement must stay where it was: %+v", presence.LastAdvertisementMsAgo)
	}
}

func TestRegistryCountsRadioEvidence(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	registry.observe("some-phone", "11:11:11:11:11:11", -80, false, start)
	registry.observe("some-phone", "11:11:11:11:11:11", -81, false, start.Add(time.Second))
	registry.observe("some-tv", "22:22:22:22:22:22", -75, false, start.Add(2*time.Second))

	snapshot := &PresenceSnapshot{}
	registry.fill(snapshot, nil, time.Minute, start.Add(3*time.Second))
	if snapshot.AdvertisementsSeen != 3 {
		t.Fatalf("every received advertisement counts, got %d", snapshot.AdvertisementsSeen)
	}
	if snapshot.DistinctDevicesSeen != 2 {
		t.Fatalf("expected 2 distinct devices, got %d", snapshot.DistinctDevicesSeen)
	}
	if snapshot.LastAdvertisementMsAgo == nil || *snapshot.LastAdvertisementMsAgo != 1000 {
		t.Fatalf("unexpected silence: %+v", snapshot.LastAdvertisementMsAgo)
	}
	if registry.silence(start.Add(5*time.Second)) != 3*time.Second {
		t.Fatalf("unexpected silence: %s", registry.silence(start.Add(5*time.Second)))
	}
}

func TestRegistryNeverGrowsWithoutBound(t *testing.T) {
	start := time.Unix(0, 0)
	registry := newPresenceRegistry(10*time.Minute, start)
	for i := 0; i < maxTrackedVehicles*3; i++ {
		//Every name is a syntactically valid vehicle name, so only the cap can stop the registry from growing.
		registry.observe(VehicleLocalName(fmt.Sprintf("VIN%d", i)), fmt.Sprintf("aa:aa:aa:aa:aa:%02x", i), -60, true, start)
	}
	registry.mu.Lock()
	tracked := len(registry.vehicles)
	registry.mu.Unlock()
	if tracked > maxTrackedVehicles {
		t.Fatalf("the registry must stay bounded, tracked %d", tracked)
	}
}

// fakeRadio stands in for the adapter: it records when a scan is running so a test can assert that no scan overlaps
// with a command holding the radio.
type fakeRadio struct {
	mu         sync.Mutex
	active     bool
	scans      int
	overlapped bool
	emit       func(observe func(localName string, address string, rssi int, connectable bool))
}

func (fake *fakeRadio) run(ctx context.Context, observe func(localName string, address string, rssi int, connectable bool)) error {
	fake.mu.Lock()
	if fake.active {
		fake.overlapped = true
	}
	fake.active = true
	fake.scans++
	emit := fake.emit
	fake.mu.Unlock()
	if emit != nil {
		emit(observe)
	}
	<-ctx.Done()
	fake.mu.Lock()
	fake.active = false
	fake.mu.Unlock()
	return ctx.Err()
}

func (fake *fakeRadio) isActive() bool {
	fake.mu.Lock()
	defer fake.mu.Unlock()
	return fake.active
}

func (fake *fakeRadio) scanCount() int {
	fake.mu.Lock()
	defer fake.mu.Unlock()
	return fake.scans
}

func (fake *fakeRadio) didOverlap() bool {
	fake.mu.Lock()
	defer fake.mu.Unlock()
	return fake.overlapped
}

func startTestScanner(t *testing.T, config PresenceScannerConfig, fake *fakeRadio) (*presenceScanner, *radioArbiter) {
	t.Helper()
	arbiter := newRadioArbiter()
	scanner := newPresenceScanner(config, arbiter, fake.run, nil)
	scanner.start()
	t.Cleanup(func() {
		close(scanner.quit)
		arbiter.stop()
		select {
		case <-scanner.done:
		case <-time.After(5 * time.Second):
			t.Error("the scan loop did not end")
		}
	})
	//Wait for the first scan so the tests do not race the goroutine start.
	waitFor(t, func() bool { return fake.scanCount() > 0 })
	return scanner, arbiter
}

func waitFor(t *testing.T, condition func() bool) {
	t.Helper()
	deadline := time.Now().Add(3 * time.Second)
	for time.Now().Before(deadline) {
		if condition() {
			return
		}
		time.Sleep(time.Millisecond)
	}
	t.Fatal("condition was not met in time")
}

// The property the whole design rests on: while a command holds the radio the scanner is not scanning, and it does
// not re-arm behind the command's back either.
func TestArbiterNeverScansWhileTheRadioIsHeld(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter := startTestScanner(t, PresenceScannerConfig{}, fake)

	for i := 0; i < 20; i++ {
		release := arbiter.acquire()
		if fake.isActive() {
			t.Fatal("the scanner must not scan while a command holds the radio")
		}
		time.Sleep(2 * time.Millisecond)
		if fake.isActive() {
			t.Fatal("the scanner must not re-arm while a command holds the radio")
		}
		release()
		waitFor(t, fake.isActive)
	}
	if fake.didOverlap() {
		t.Fatal("two scans must never overlap")
	}
}

// A command must not wait for the scan to end on its own: the scan runs until it is cancelled, so a slow handover
// would be unbounded.
func TestArbiterAcquireIsNotBlockedByARunningScan(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter := startTestScanner(t, PresenceScannerConfig{}, fake)
	//Let the scan run for a while so the test would fail if the handover waited for a scan window to expire.
	time.Sleep(50 * time.Millisecond)

	start := time.Now()
	release := arbiter.acquire()
	waited := time.Since(start)
	release()
	if waited > time.Second {
		t.Fatalf("acquiring the radio took %s, it must not wait for the scan to finish on its own", waited)
	}
}

func TestArbiterServesEveryWaiter(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter := startTestScanner(t, PresenceScannerConfig{}, fake)

	var waitGroup sync.WaitGroup
	for i := 0; i < 50; i++ {
		waitGroup.Add(1)
		go func() {
			defer waitGroup.Done()
			release := arbiter.acquire()
			time.Sleep(time.Millisecond)
			release()
		}()
	}
	done := make(chan struct{})
	go func() {
		waitGroup.Wait()
		close(done)
	}()
	select {
	case <-done:
	case <-time.After(10 * time.Second):
		t.Fatal("concurrent radio users starved each other")
	}
	//The scanner has to come back once the last command released the radio.
	waitFor(t, fake.isActive)
}

// Nested acquires happen when a command runs while the worker already holds the radio for an open connection.
func TestArbiterAllowsNestedAcquire(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter := startTestScanner(t, PresenceScannerConfig{}, fake)

	outer := arbiter.acquire()
	done := make(chan struct{})
	go func() {
		inner := arbiter.acquire()
		inner()
		close(done)
	}()
	select {
	case <-done:
	case <-time.After(5 * time.Second):
		t.Fatal("a nested acquire must not deadlock")
	}
	outer()
}

func TestScannerRecordsObservations(t *testing.T) {
	name := VehicleLocalName(testVin)
	fake := &fakeRadio{
		emit: func(observe func(string, string, int, bool)) {
			observe(name, "aa:aa:aa:aa:aa:aa", -64, true)
		},
	}
	scanner, _ := startTestScanner(t, PresenceScannerConfig{MaxAge: time.Minute}, fake)

	waitFor(t, func() bool { return scanner.snapshot([]string{testVin}, time.Minute).Vehicles[0].Heard })
	snapshot := scanner.snapshot([]string{testVin}, time.Minute)
	if snapshot.AdvertisementsSeen == 0 || !snapshot.ScannerRunning {
		t.Fatalf("unexpected snapshot: %+v", snapshot)
	}
	if len(snapshot.Tracked) != 1 || snapshot.Tracked[0].LocalName != name {
		t.Fatalf("every heard car must be tracked: %+v", snapshot.Tracked)
	}
}

// An adapter that is up but hears nothing is a real, observed failure. Re-arming the scan is the only recovery the
// worker can attempt on its own, so it has to happen without a request coming in.
func TestScannerWatchdogReArmsADeafScan(t *testing.T) {
	fake := &fakeRadio{}
	scanner, _ := startTestScanner(t, PresenceScannerConfig{RestartAfter: 10 * time.Millisecond}, fake)

	waitFor(t, func() bool { return fake.scanCount() > 1 })
	waitFor(t, func() bool { return scanner.snapshot(nil, time.Minute).Restarts > 0 })
}

// testClock hands out a time the test controls, so the scan time accounting can be asserted exactly instead of
// against a stopwatch.
type testClock struct {
	mu  sync.Mutex
	now time.Time
}

func (clock *testClock) get() time.Time {
	clock.mu.Lock()
	defer clock.mu.Unlock()
	return clock.now
}

func (clock *testClock) advance(delta time.Duration) {
	clock.mu.Lock()
	defer clock.mu.Unlock()
	clock.now = clock.now.Add(delta)
}

// The normal state of the scanner is one long uninterrupted scan. Counting only scans that already ended made it
// report a duty cycle falling towards zero while it was in fact scanning the whole time, which is exactly the number
// the rework has to be judged by.
func TestScanTimeCountsTheRunningScan(t *testing.T) {
	clock := &testClock{now: time.Unix(1000, 0)}
	fake := &fakeRadio{}
	arbiter := newRadioArbiter()
	scanner := newPresenceScanner(PresenceScannerConfig{}, arbiter, fake.run, clock.get)
	scanner.start()
	t.Cleanup(func() {
		close(scanner.quit)
		arbiter.stop()
		<-scanner.done
	})
	waitFor(t, fake.isActive)

	clock.advance(10 * time.Second)
	snapshot := scanner.snapshot(nil, time.Minute)
	if snapshot.ScanActiveMs != 10000 {
		t.Fatalf("the running scan must count as scan time, got %d ms", snapshot.ScanActiveMs)
	}
	if snapshot.ObservingMs != 10000 || snapshot.PausedMs != 0 {
		t.Fatalf("an uninterrupted scan must report no paused time: %+v", snapshot)
	}

	//Handing the radio to a command has to show up as paused time, and the ended scan must not be counted twice.
	release := arbiter.acquire()
	clock.advance(2 * time.Second)
	snapshot = scanner.snapshot(nil, time.Minute)
	if snapshot.ScanActiveMs != 10000 || snapshot.PausedMs != 2000 {
		t.Fatalf("time spent on a command must count as paused: %+v", snapshot)
	}
	release()
}

func TestWaitForVehicleReturnsWhenTheCarIsHeard(t *testing.T) {
	name := VehicleLocalName(testVin)
	registry := newPresenceRegistry(10*time.Minute, time.Now())
	scanner := &presenceScanner{registry: registry, now: time.Now, config: PresenceScannerConfig{MaxAge: time.Minute}}
	scannerMu.Lock()
	previous := activeScanner
	activeScanner = scanner
	scannerMu.Unlock()
	defer func() {
		scannerMu.Lock()
		activeScanner = previous
		scannerMu.Unlock()
	}()

	ctx, cancel := context.WithTimeout(context.Background(), 50*time.Millisecond)
	defer cancel()
	if _, found := WaitForVehicle(ctx, testVin, time.Minute); found {
		t.Fatal("a car that was never heard must not be reported as present")
	}

	registry.observe(name, "aa:aa:aa:aa:aa:aa", -60, true, time.Now())
	ctx2, cancel2 := context.WithTimeout(context.Background(), time.Second)
	defer cancel2()
	presence, found := WaitForVehicle(ctx2, testVin, time.Minute)
	if !found || presence.Vin != testVin {
		t.Fatalf("expected the car to be found: %+v", presence)
	}
}
