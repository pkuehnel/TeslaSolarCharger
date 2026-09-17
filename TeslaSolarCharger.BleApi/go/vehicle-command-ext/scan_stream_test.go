// Runs during the Docker image build (go test ./pkg/connector/ble/, with -race on the amd64 leg) together with the
// injected scan_stream.go. The radio is injected, so everything below runs without a Bluetooth adapter.
//
// The arbiter tests are the ones that matter most: they assert the property the whole design rests on, namely that a
// command can never be starved by the background scan and never runs while the adapter is scanning. A failure there
// would look like a command hanging behind a scan, which is invisible until it happens at a user's site.
package ble

import (
	"context"
	"fmt"
	"sync"
	"testing"
	"time"
)

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

// collector captures what the worker would write to stdout.
type collector struct {
	mu      sync.Mutex
	digests []AdvertisementDigest
	states  []string
}

func (c *collector) digest(d AdvertisementDigest) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.digests = append(c.digests, d)
}

func (c *collector) state(state string, _ string) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.states = append(c.states, state)
}

func (c *collector) count() int {
	c.mu.Lock()
	defer c.mu.Unlock()
	return len(c.digests)
}

func (c *collector) last() AdvertisementDigest {
	c.mu.Lock()
	defer c.mu.Unlock()
	if len(c.digests) == 0 {
		return AdvertisementDigest{}
	}
	return c.digests[len(c.digests)-1]
}

func (c *collector) hasState(state string) bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	for _, s := range c.states {
		if s == state {
			return true
		}
	}
	return false
}

func startTestStream(t *testing.T, config ScanStreamConfig, fake *fakeRadio) (*scanStream, *radioArbiter, *collector) {
	t.Helper()
	arbiter := newRadioArbiter()
	sink := &collector{}
	stream := newScanStream(config, arbiter, fake.run, nil, sink.digest, sink.state)
	stream.start()
	t.Cleanup(func() {
		close(stream.quit)
		arbiter.stop()
		select {
		case <-stream.done:
		case <-time.After(5 * time.Second):
			t.Error("the scan loop did not end")
		}
	})
	//Wait for the first scan so the tests do not race the goroutine start.
	waitFor(t, func() bool { return fake.scanCount() > 0 })
	return stream, arbiter, sink
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
	_, arbiter, _ := startTestStream(t, ScanStreamConfig{}, fake)

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
	_, arbiter, _ := startTestStream(t, ScanStreamConfig{}, fake)
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
	_, arbiter, _ := startTestStream(t, ScanStreamConfig{}, fake)

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

// Nested acquires happen when a command runs while the worker already holds the radio for something else.
func TestArbiterAllowsNestedAcquire(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter, _ := startTestStream(t, ScanStreamConfig{}, fake)

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

// Handing the radio over and taking it back has to be visible to the container: it is the only way it can tell
// "heard nothing because the car is gone" from "heard nothing because the radio was busy".
func TestScanStateIsReported(t *testing.T) {
	fake := &fakeRadio{}
	_, arbiter, sink := startTestStream(t, ScanStreamConfig{}, fake)
	waitFor(t, func() bool { return sink.hasState(ScanStateRunning) })

	release := arbiter.acquire()
	waitFor(t, func() bool { return sink.hasState(ScanStatePaused) })
	release()
}

// Most of a car's advertisements carry no local name (55-61 % measured), so the digest has to report the name when
// any packet in the window carried one, and count how many did - that split is what lets the container learn the
// car's address and recognize the nameless packets afterwards.
func TestDigestAggregatesPerAddress(t *testing.T) {
	fake := &fakeRadio{
		emit: func(observe func(string, string, int, bool)) {
			observe("", "aa:aa:aa:aa:aa:aa", -60, true)
			observe("S612fafca57f07c21C", "aa:aa:aa:aa:aa:aa", -62, true)
			observe("", "aa:aa:aa:aa:aa:aa", -64, true)
			observe("some-phone", "bb:bb:bb:bb:bb:bb", -80, false)
		},
	}
	_, _, sink := startTestStream(t, ScanStreamConfig{DigestInterval: 10 * time.Millisecond}, fake)
	waitFor(t, func() bool { return sink.count() > 0 && sink.last().Total > 0 })

	digest := sink.last()
	var car, phone *DeviceObservation
	for i := range digest.Devices {
		switch digest.Devices[i].Addr {
		case "aa:aa:aa:aa:aa:aa":
			car = &digest.Devices[i]
		case "bb:bb:bb:bb:bb:bb":
			phone = &digest.Devices[i]
		}
	}
	if car == nil || phone == nil {
		t.Fatalf("both devices must appear in the digest: %+v", digest.Devices)
	}
	if car.Count != 3 || car.Named != 1 {
		t.Fatalf("expected 3 advertisements of which 1 named, got count=%d named=%d", car.Count, car.Named)
	}
	if car.Name != "S612fafca57f07c21C" {
		t.Fatalf("a name seen anywhere in the window must be reported, got %q", car.Name)
	}
	if car.Rssi != -64 {
		t.Fatalf("the newest rssi wins, got %d", car.Rssi)
	}
	if digest.Total != 4 {
		t.Fatalf("every advertisement counts towards the total, got %d", digest.Total)
	}
	if digest.Truncated {
		t.Fatal("a two device window must not be truncated")
	}
}

// A busy site must not be able to produce an unbounded line on the pipe that also carries request and response.
func TestDigestIsBounded(t *testing.T) {
	fake := &fakeRadio{
		emit: func(observe func(string, string, int, bool)) {
			for i := 0; i < 40; i++ {
				observe("", fmt.Sprintf("aa:aa:aa:aa:aa:%02x", i), -70, false)
			}
		},
	}
	_, _, sink := startTestStream(t, ScanStreamConfig{DigestInterval: 10 * time.Millisecond, MaxDevices: 8}, fake)
	waitFor(t, func() bool { return sink.count() > 0 && sink.last().Total > 0 })

	digest := sink.last()
	if len(digest.Devices) > 8 {
		t.Fatalf("the digest must stay bounded, got %d devices", len(digest.Devices))
	}
	if !digest.Truncated {
		t.Fatal("dropping devices must be reported as truncated")
	}
	if digest.Total != 40 {
		t.Fatalf("dropped devices still count towards the total, got %d", digest.Total)
	}
}

// An adapter that hears nothing has to stay distinguishable from a worker that died.
func TestIdleHeartbeatIsEmitted(t *testing.T) {
	fake := &fakeRadio{}
	_, _, sink := startTestStream(t, ScanStreamConfig{
		DigestInterval: 5 * time.Millisecond,
		IdleInterval:   20 * time.Millisecond,
	}, fake)

	waitFor(t, func() bool { return sink.count() >= 2 })
	if sink.last().Total != 0 {
		t.Fatalf("an idle heartbeat must report nothing heard, got %+v", sink.last())
	}
}

// A quiet site must not get a line every digest interval just to say it heard nothing.
func TestSilentWindowsAreNotEmittedEveryInterval(t *testing.T) {
	fake := &fakeRadio{}
	_, _, sink := startTestStream(t, ScanStreamConfig{
		DigestInterval: 2 * time.Millisecond,
		IdleInterval:   time.Hour,
	}, fake)

	time.Sleep(60 * time.Millisecond)
	if got := sink.count(); got > 1 {
		t.Fatalf("a silent window must not be emitted every interval, got %d digests", got)
	}
}
