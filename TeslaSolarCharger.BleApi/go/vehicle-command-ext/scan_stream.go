// Permanent BLE scan stream for the TeslaSolarCharger BLE worker.
//
// This file is copied into pkg/connector/ble/ of the teslamotors/vehicle-command module during the Docker image
// build (see TeslaSolarCharger.BleApi/Dockerfile): it needs the package private adapter singleton (device/mu), which
// is why it cannot live in the worker's own package. If an upstream change breaks it, the image build fails loudly -
// that is intended, a silent runtime surprise at a user's site would be worse.
//
// The worker is a radio device driver and nothing more: it reports what the adapter heard and has no notion of a car
// being present. Matching advertisements to VINs, learning addresses, ageing and every presence decision live in the
// C# container, where they are ordinary unit tests instead of code that can only be exercised with a car in the
// driveway.
//
// Why it scans permanently: a car standing in the garage was heard in only every third 7 s window, because a Tesla
// emits nothing at all while it holds a connection to us - measured, 0 advertisements in 11 of 11 samples with both
// phones' Bluetooth off, while a control car was unaffected. The old poll connected every 13 s and so silenced the
// very car it was looking for. Advertisements are reliable exactly while we hold no link, and command outcomes are
// available exactly while we do.
//
// Three properties of the stack below decide the design and must not be undone:
//
//  1. go-ble's Dial does not stop scanning first, and controllers reject LE Create Connection while a scan runs.
//     Scanning has to be off before any connect, which is what the arbiter guarantees. That is a correctness duty,
//     not a fairness one.
//  2. Every call to device.Scan reallocates go-ble's advertisement history, and a scan response whose advertising
//     packet is no longer in that history makes go-ble drop the rest of the HCI event. A car whose name only travels
//     in the scan response is exactly what gets lost, so the scan is never restarted on a timer - only when
//     something else needs the radio.
//  3. An established connection holds no lock, so the scan keeps running alongside one. That is deliberate: the
//     connected car is silent anyway, and pausing would only blind the adapter to every other car.
package ble

import (
	"context"
	"errors"
	"sync"
	"time"

	"github.com/go-ble/ble"
)

const (
	scanErrorBackoff = time.Second

	// Scanner states reported to the container. They are the only way it can tell "heard nothing because the car is
	// gone" from "heard nothing because the radio was busy".
	ScanStateRunning = "running"
	ScanStatePaused  = "paused"
	ScanStateError   = "error"
	ScanStateStopped = "stopped"
)

// DeviceObservation is what one Bluetooth address emitted during one digest window.
type DeviceObservation struct {
	Addr string `json:"addr"`
	// Name is any local name seen from this address in the window, empty when none of its advertisements carried
	// one. Most do not: 55-61 % of both measured cars' advertisements are nameless, which is why the container
	// learns addresses instead of matching names alone.
	Name        string `json:"name,omitempty"`
	Rssi        int    `json:"rssi"`
	Count       int    `json:"count"`
	Named       int    `json:"named"`
	Connectable bool   `json:"connectable"`
}

// AdvertisementDigest is one window of everything the adapter heard. Aggregated rather than one event per
// advertisement: the measured rate is 60-85/s and this pipe also carries request and response lines.
type AdvertisementDigest struct {
	WindowMs  int64               `json:"windowMs"`
	Total     int                 `json:"total"`
	Truncated bool                `json:"truncated"`
	Devices   []DeviceObservation `json:"devices"`
}

type ScanStreamConfig struct {
	// DigestInterval is how often a digest is emitted while anything is being heard.
	DigestInterval time.Duration
	// IdleInterval emits an empty digest when nothing at all was heard, so a deaf adapter stays distinguishable
	// from a dead worker.
	IdleInterval time.Duration
	// MaxDevices bounds one digest so a busy site cannot produce an unbounded line.
	MaxDevices int
}

// radioArbiter decides who owns the Bluetooth adapter. Priority is deterministic and never relies on the fairness of
// the package mutex: a request registers itself as a waiter BEFORE it cancels the running scan, and the scan loop
// refuses to re-arm while any waiter exists. A command therefore waits for one scan disable round trip at most, no
// matter how long the scan has been running, and can never be starved by a scanner that keeps re-arming.
type radioArbiter struct {
	mu       sync.Mutex
	cond     *sync.Cond
	waiters  int
	scanning bool
	cancel   context.CancelFunc
	stopped  bool
}

func newRadioArbiter() *radioArbiter {
	arbiter := &radioArbiter{}
	arbiter.cond = sync.NewCond(&arbiter.mu)
	return arbiter
}

// acquire takes the radio away from the scanner and returns the release function. Acquiring twice from the same
// goroutine is safe: further acquires return immediately because the scanner can not be scanning while a waiter
// exists.
func (arbiter *radioArbiter) acquire() func() {
	arbiter.mu.Lock()
	arbiter.waiters++
	cancel := arbiter.cancel
	arbiter.mu.Unlock()
	if cancel != nil {
		//Ends the running scan now instead of waiting for it to finish on its own.
		cancel()
	}
	arbiter.mu.Lock()
	for arbiter.scanning {
		arbiter.cond.Wait()
	}
	arbiter.mu.Unlock()
	var once sync.Once
	return func() {
		once.Do(func() {
			arbiter.mu.Lock()
			arbiter.waiters--
			arbiter.cond.Broadcast()
			arbiter.mu.Unlock()
		})
	}
}

// beginScan blocks until the scanner may use the radio. ok is false when the scanner was stopped.
func (arbiter *radioArbiter) beginScan() (context.Context, bool) {
	arbiter.mu.Lock()
	defer arbiter.mu.Unlock()
	for arbiter.waiters > 0 && !arbiter.stopped {
		arbiter.cond.Wait()
	}
	if arbiter.stopped {
		return nil, false
	}
	ctx, cancel := context.WithCancel(context.Background())
	arbiter.cancel = cancel
	arbiter.scanning = true
	return ctx, true
}

func (arbiter *radioArbiter) endScan() {
	arbiter.mu.Lock()
	defer arbiter.mu.Unlock()
	if arbiter.cancel != nil {
		arbiter.cancel()
		arbiter.cancel = nil
	}
	arbiter.scanning = false
	arbiter.cond.Broadcast()
}

func (arbiter *radioArbiter) hasWaiters() bool {
	arbiter.mu.Lock()
	defer arbiter.mu.Unlock()
	return arbiter.waiters > 0
}

func (arbiter *radioArbiter) stop() {
	arbiter.mu.Lock()
	defer arbiter.mu.Unlock()
	arbiter.stopped = true
	if arbiter.cancel != nil {
		arbiter.cancel()
	}
	arbiter.cond.Broadcast()
}

// scanRunner performs one scan and reports every advertisement through observe. Injected so the scan loop, the
// arbiter handoff and the digest shaping can be tested without a radio; the default implementation is the only place
// that touches go-ble.
type scanRunner func(ctx context.Context, observe func(localName string, address string, rssi int, connectable bool)) error

type scanStream struct {
	config    ScanStreamConfig
	arbiter   *radioArbiter
	runner    scanRunner
	now       func() time.Time
	emitDigest func(AdvertisementDigest)
	emitState  func(state string, reason string)
	quit      chan struct{}
	done      chan struct{}

	mu          sync.Mutex
	running     bool
	windowStart time.Time
	lastEmit    time.Time
	devices     map[string]*DeviceObservation
	total       int
	truncated   bool
}

func newScanStream(config ScanStreamConfig, arbiter *radioArbiter, runner scanRunner, now func() time.Time,
	emitDigest func(AdvertisementDigest), emitState func(state string, reason string)) *scanStream {
	if now == nil {
		now = time.Now
	}
	if config.DigestInterval <= 0 {
		config.DigestInterval = 500 * time.Millisecond
	}
	if config.IdleInterval <= 0 {
		config.IdleInterval = 5 * time.Second
	}
	if config.MaxDevices <= 0 {
		config.MaxDevices = 64
	}
	if emitDigest == nil {
		emitDigest = func(AdvertisementDigest) {}
	}
	if emitState == nil {
		emitState = func(string, string) {}
	}
	stream := &scanStream{
		config:     config,
		arbiter:    arbiter,
		runner:     runner,
		now:        now,
		emitDigest: emitDigest,
		emitState:  emitState,
		quit:       make(chan struct{}),
		done:       make(chan struct{}),
		running:    true,
		devices:    make(map[string]*DeviceObservation),
	}
	if stream.runner == nil {
		stream.runner = defaultScanRunner
	}
	stream.windowStart = stream.now()
	stream.lastEmit = stream.windowStart
	return stream
}

func (stream *scanStream) start() {
	go stream.run()
	go stream.flushLoop()
}

func (stream *scanStream) run() {
	defer close(stream.done)
	for {
		ctx, ok := stream.arbiter.beginScan()
		if !ok {
			stream.emitState(ScanStateStopped, "")
			return
		}
		stream.emitState(ScanStateRunning, "")
		err := stream.runner(ctx, stream.observe)
		stream.arbiter.endScan()
		if err == nil || errors.Is(err, context.Canceled) || errors.Is(err, context.DeadlineExceeded) {
			//The normal case: something else wanted the radio.
			stream.emitState(ScanStatePaused, "radio handed over")
			continue
		}
		stream.emitState(ScanStateError, sanitizeScanError(err))
		if IsAdapterError(err) {
			//A dead HCI socket only recovers with a fresh adapter bind, which needs a worker restart. Stop here so
			//the next command reports the adapter as unavailable instead of spinning.
			stream.mu.Lock()
			stream.running = false
			stream.mu.Unlock()
			return
		}
		select {
		case <-time.After(scanErrorBackoff):
		case <-stream.quit:
			return
		}
	}
}

// observe records one advertisement. go-ble dispatches each one in its own goroutine, so this is called
// concurrently and must stay cheap: at the measured 60-85 advertisements per second it runs about five thousand
// times per digest window.
func (stream *scanStream) observe(localName string, address string, rssi int, connectable bool) {
	stream.mu.Lock()
	defer stream.mu.Unlock()
	stream.total++
	device, known := stream.devices[address]
	if !known {
		if len(stream.devices) >= stream.config.MaxDevices {
			stream.truncated = true
			return
		}
		device = &DeviceObservation{Addr: address}
		stream.devices[address] = device
	}
	device.Count++
	device.Rssi = rssi
	device.Connectable = connectable
	if localName != "" {
		device.Named++
		device.Name = localName
	}
}

func (stream *scanStream) flushLoop() {
	ticker := time.NewTicker(stream.config.DigestInterval)
	defer ticker.Stop()
	for {
		select {
		case <-stream.quit:
			return
		case <-ticker.C:
			stream.flush()
		}
	}
}

// flush emits the accumulated window. A window that heard nothing is only emitted every IdleInterval, so a quiet
// site does not produce a line every DigestInterval just to say so.
func (stream *scanStream) flush() {
	now := stream.now()
	stream.mu.Lock()
	if stream.total == 0 && now.Sub(stream.lastEmit) < stream.config.IdleInterval {
		stream.mu.Unlock()
		return
	}
	digest := AdvertisementDigest{
		WindowMs:  now.Sub(stream.windowStart).Milliseconds(),
		Total:     stream.total,
		Truncated: stream.truncated,
		Devices:   make([]DeviceObservation, 0, len(stream.devices)),
	}
	for _, device := range stream.devices {
		digest.Devices = append(digest.Devices, *device)
	}
	stream.devices = make(map[string]*DeviceObservation)
	stream.total = 0
	stream.truncated = false
	stream.windowStart = now
	stream.lastEmit = now
	stream.mu.Unlock()
	stream.emitDigest(digest)
}

// defaultScanRunner is the only code that touches go-ble. Duplicates are allowed so every received advertisement
// counts: the advertisement totals are the container's evidence that a silent radio is still a working one.
func defaultScanRunner(ctx context.Context, observe func(localName string, address string, rssi int, connectable bool)) error {
	mu.Lock()
	defer mu.Unlock()
	if err := initAdapter(nil); err != nil {
		return err
	}
	return device.Scan(ctx, true, func(advertisement ble.Advertisement) {
		observe(advertisement.LocalName(), advertisement.Addr().String(), advertisement.RSSI(), advertisement.Connectable())
	})
}

func sanitizeScanError(err error) string {
	if err == nil {
		return ""
	}
	return err.Error()
}

var (
	//The arbiter exists independently of the stream: AcquireRadio has to work even when scanning is disabled.
	radio        = newRadioArbiter()
	activeStream *scanStream
	streamMu     sync.Mutex
)

// StartScanStream starts the permanent background scan. The two callbacks are how the worker forwards what was heard
// to the container; they are called from the scanner's goroutines, so the writer behind them must be safe to use
// concurrently with the request loop.
func StartScanStream(config ScanStreamConfig, emitDigest func(AdvertisementDigest), emitState func(state string, reason string)) {
	streamMu.Lock()
	defer streamMu.Unlock()
	if activeStream != nil {
		return
	}
	activeStream = newScanStream(config, radio, nil, nil, emitDigest, emitState)
	activeStream.start()
}

// StopScanStream ends the background scan and waits for the scan loop to release the adapter.
func StopScanStream() {
	streamMu.Lock()
	stream := activeStream
	activeStream = nil
	streamMu.Unlock()
	if stream == nil {
		return
	}
	close(stream.quit)
	radio.stop()
	select {
	case <-stream.done:
	case <-time.After(5 * time.Second):
	}
}

// AcquireRadio hands the adapter to the caller, ending a running background scan first. The returned function gives
// it back. Every code path that scans, connects or closes the adapter must hold it.
func AcquireRadio() func() {
	return radio.acquire()
}

// ScanStreamRunning reports whether the adapter is being listened to at all.
func ScanStreamRunning() bool {
	streamMu.Lock()
	defer streamMu.Unlock()
	if activeStream == nil {
		return false
	}
	activeStream.mu.Lock()
	defer activeStream.mu.Unlock()
	return activeStream.running
}
