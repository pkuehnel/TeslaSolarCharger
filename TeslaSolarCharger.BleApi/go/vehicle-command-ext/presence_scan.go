// Permanent BLE presence scanner for the TeslaSolarCharger BLE worker.
//
// This file is copied into pkg/connector/ble/ of the teslamotors/vehicle-command module during the Docker image
// build (see TeslaSolarCharger.BleApi/Dockerfile): it needs the package private adapter singleton (device/mu), which
// is why it cannot live in the worker's own package. If an upstream change breaks it, the image build fails loudly -
// that is intended, a silent runtime surprise at a user's site would be worse.
//
// It replaces the windowed multi-VIN scan. A car standing in the garage was heard in only every third 7 s window,
// which made it look absent 69 % of the time while it answered commands the whole while. The radio is therefore no
// longer sampled: one long living scan runs whenever nothing else needs the adapter, every advertisement it hears
// updates a per car "last heard" timestamp, and presence is answered from that memory without touching the radio.
//
// Three properties of the stack below decide the design and must not be undone:
//
//  1. go-ble's Dial does not stop scanning first, and controllers reject LE Create Connection while a scan runs.
//     Scanning has to be off before any connect, which is what the arbiter guarantees. That is a correctness duty,
//     not a fairness one.
//  2. Every call to device.Scan reallocates go-ble's advertisement history, and a scan response whose advertising
//     packet is no longer in that history makes go-ble drop the rest of the HCI event. A car whose name only travels
//     in the scan response is exactly what gets lost, so the scan is never restarted on a timer - only when
//     something else needs the radio, or when the watchdog finds the adapter has gone deaf.
//  3. An established connection holds no lock, so scanning alongside an open vehicle link is possible. Whether it is
//     also harmless is a hardware question, which is what -scan-while-connected exists to answer.
package ble

import (
	"context"
	"errors"
	"sort"
	"strings"
	"sync"
	"time"

	"github.com/go-ble/ble"
)

const (
	//A Tesla advertises as "S" + 16 hex characters + "C" (see VehicleLocalName). Matching that shape instead of a
	//registered VIN list keeps the scanner VIN agnostic: it records every car it hears and a presence request
	//translates VIN to local name only when it is answered, so a car added while the worker runs needs no handshake.
	vehicleLocalNameLength = 18
	//Bounded so a site with a lot of Bluetooth traffic can not grow the registry without limit.
	maxTrackedVehicles = 32
	maxTrackedDevices  = 4096
	//Inter arrival gaps are the evidence for how often a car really advertises. A small ring gives percentiles and
	//keeps the observe path allocation free.
	gapRingSize = 128

	sourceAdvertisement = "advertisement"
	sourceAddress       = "address"
	sourceCommand       = "command"

	presencePollInterval = 100 * time.Millisecond
	watchdogInterval     = 10 * time.Second
	scanErrorBackoff     = time.Second
)

// VehiclePresence is what is known about one car. Ages rather than timestamps: the answer travels through two
// processes and a HTTP hop, and an age stays meaningful across all of them.
type VehiclePresence struct {
	Vin       string `json:"vin,omitempty"`
	LocalName string `json:"localName"`
	//Heard is true when the car was heard within the max age the question was asked with.
	Heard                  bool    `json:"heard"`
	LastHeardMsAgo         *int64  `json:"lastHeardMsAgo,omitempty"`
	LastAdvertisementMsAgo *int64  `json:"lastAdvertisementMsAgo,omitempty"`
	FirstHeardMsAgo        *int64  `json:"firstHeardMsAgo,omitempty"`
	Rssi                   *int    `json:"rssi,omitempty"`
	Address                *string `json:"address,omitempty"`
	Connectable            *bool   `json:"connectable,omitempty"`
	//Count is every advertisement of this car, NamedCount the ones that carried its local name and AddressCount the
	//ones that were only recognized by the learned address. A high AddressCount means the name travels in the scan
	//response and a name only matcher (the old windowed scan) was throwing most of the car's advertisements away.
	Count        int64  `json:"count"`
	NamedCount   int64  `json:"namedCount"`
	AddressCount int64  `json:"addressCount"`
	LastSource   string `json:"lastSource,omitempty"`
	//Gaps between consecutive advertisements, oldest first, at most gapRingSize entries.
	GapsMs      []int64 `json:"gapsMs,omitempty"`
	MedianGapMs int64   `json:"medianGapMs"`
	MaxGapMs    int64   `json:"maxGapMs"`
}

// PresenceSnapshot is the whole scanner state at one moment: the cars that were asked about, every car the radio
// heard, and the counters that tell a working radio from a deaf one.
type PresenceSnapshot struct {
	ScannerRunning         bool              `json:"scannerRunning"`
	ObservingMs            int64             `json:"observingMs"`
	ScanActiveMs           int64             `json:"scanActiveMs"`
	PausedMs               int64             `json:"pausedMs"`
	Restarts               int64             `json:"restarts"`
	ScanErrors             int64             `json:"scanErrors"`
	LastScanError          string            `json:"lastScanError,omitempty"`
	AdvertisementsSeen     int64             `json:"advertisementsSeen"`
	DistinctDevicesSeen    int               `json:"distinctDevicesSeen"`
	LastAdvertisementMsAgo *int64            `json:"lastAdvertisementMsAgo,omitempty"`
	MaxAgeMs               int64             `json:"maxAgeMs"`
	ScanWhileConnected     bool              `json:"scanWhileConnected"`
	Vehicles               []VehiclePresence `json:"vehicles"`
	Tracked                []VehiclePresence `json:"tracked"`
}

// PresenceScannerConfig is the tuning the worker passes through from its command line.
type PresenceScannerConfig struct {
	//MaxAge is how old the newest advertisement of a car may be before it stops counting as present.
	MaxAge time.Duration
	//AddressTtl is how long a learned address keeps counting for a car without another advertisement that carried
	//the car's name. Bounded so a rotated address can not be inherited by a different device for long.
	AddressTtl time.Duration
	//RestartAfter re-arms the scan when the adapter heard nothing at all for this long, which is the observed
	//"adapter is up but deaf" failure. Zero disables the watchdog.
	RestartAfter time.Duration
	//ScanWhileConnected lets the scan continue while a vehicle connection is open. False makes the worker hold the
	//radio for the lifetime of the connection instead.
	ScanWhileConnected bool
}

// IsVehicleLocalName reports whether a local name has the shape VehicleLocalName produces.
func IsVehicleLocalName(localName string) bool {
	if len(localName) != vehicleLocalNameLength || localName[0] != 'S' || localName[len(localName)-1] != 'C' {
		return false
	}
	for _, character := range localName[1 : len(localName)-1] {
		if !strings.ContainsRune("0123456789abcdef", character) {
			return false
		}
	}
	return true
}

// vehicleRecord is one car's observations. Separated from the go-ble handler so all matching, address learning and
// gap accounting stays testable without a radio.
type vehicleRecord struct {
	localName        string
	address          string
	addressConfirmed time.Time
	rssi             int
	connectable      bool
	firstHeard       time.Time
	//lastHeard is any evidence including a command that reached the car, lastAdvertisement only the radio. Gaps are
	//measured from lastAdvertisement so command traffic can not distort the advertising cadence.
	lastHeard         time.Time
	lastAdvertisement time.Time
	count             int64
	named             int64
	viaAddress        int64
	lastSource        string
	gaps              [gapRingSize]int64
	gapNext           int
	gapCount          int
	maxGap            int64
}

func (record *vehicleRecord) addGap(gap int64) {
	record.gaps[record.gapNext] = gap
	record.gapNext = (record.gapNext + 1) % gapRingSize
	if record.gapCount < gapRingSize {
		record.gapCount++
	}
	if gap > record.maxGap {
		record.maxGap = gap
	}
}

// orderedGaps returns the recorded gaps oldest first.
func (record *vehicleRecord) orderedGaps() []int64 {
	if record.gapCount == 0 {
		return nil
	}
	gaps := make([]int64, 0, record.gapCount)
	start := 0
	if record.gapCount == gapRingSize {
		start = record.gapNext
	}
	for i := 0; i < record.gapCount; i++ {
		gaps = append(gaps, record.gaps[(start+i)%gapRingSize])
	}
	return gaps
}

func (record *vehicleRecord) presence(now time.Time, maxAge time.Duration, withGaps bool) VehiclePresence {
	presence := VehiclePresence{
		LocalName:    record.localName,
		Count:        record.count,
		NamedCount:   record.named,
		AddressCount: record.viaAddress,
		LastSource:   record.lastSource,
		MaxGapMs:     record.maxGap,
	}
	if !record.lastHeard.IsZero() {
		age := now.Sub(record.lastHeard).Milliseconds()
		presence.LastHeardMsAgo = &age
		presence.Heard = maxAge <= 0 || now.Sub(record.lastHeard) <= maxAge
	}
	if !record.lastAdvertisement.IsZero() {
		age := now.Sub(record.lastAdvertisement).Milliseconds()
		presence.LastAdvertisementMsAgo = &age
	}
	if !record.firstHeard.IsZero() {
		age := now.Sub(record.firstHeard).Milliseconds()
		presence.FirstHeardMsAgo = &age
	}
	if record.count > 0 {
		rssi := record.rssi
		connectable := record.connectable
		presence.Rssi = &rssi
		presence.Connectable = &connectable
	}
	if record.address != "" {
		address := record.address
		presence.Address = &address
	}
	gaps := record.orderedGaps()
	if len(gaps) > 0 {
		sorted := append([]int64(nil), gaps...)
		sort.Slice(sorted, func(a, b int) bool { return sorted[a] < sorted[b] })
		presence.MedianGapMs = sorted[len(sorted)/2]
		if withGaps {
			presence.GapsMs = gaps
		}
	}
	return presence
}

// presenceRegistry collects what the radio heard. It never talks to the radio itself, so the whole matching and
// counting logic is unit testable; the caller provides the clock.
type presenceRegistry struct {
	mu                sync.Mutex
	addressTtl        time.Duration
	vehicles          map[string]*vehicleRecord
	addressToName     map[string]string
	devices           map[string]struct{}
	advertisements    int64
	lastAdvertisement time.Time
	started           time.Time
}

func newPresenceRegistry(addressTtl time.Duration, started time.Time) *presenceRegistry {
	return &presenceRegistry{
		addressTtl:    addressTtl,
		vehicles:      make(map[string]*vehicleRecord),
		addressToName: make(map[string]string),
		devices:       make(map[string]struct{}),
		started:       started,
	}
}

// observe records one advertisement. Called once per received advertisement (go-ble dispatches each one in its own
// goroutine, so ordering is not guaranteed and the timestamp is taken by the caller).
func (registry *presenceRegistry) observe(localName string, address string, rssi int, connectable bool, at time.Time) {
	registry.mu.Lock()
	defer registry.mu.Unlock()
	registry.advertisements++
	if at.After(registry.lastAdvertisement) {
		registry.lastAdvertisement = at
	}
	if len(registry.devices) < maxTrackedDevices {
		registry.devices[address] = struct{}{}
	}
	name := localName
	viaAddress := false
	if !IsVehicleLocalName(name) {
		//A car whose name only travels in the scan response is heard as a nameless advertisement most of the time.
		//Once an address was confirmed by a named advertisement its bare advertisements count too, until the binding
		//expires - only a named advertisement can ever create or renew one.
		mapped, known := registry.addressToName[address]
		if !known {
			return
		}
		record, exists := registry.vehicles[mapped]
		if !exists || at.Sub(record.addressConfirmed) > registry.addressTtl {
			return
		}
		name = mapped
		viaAddress = true
	}
	record := registry.vehicles[name]
	if record == nil {
		if len(registry.vehicles) >= maxTrackedVehicles {
			return
		}
		record = &vehicleRecord{localName: name, firstHeard: at}
		registry.vehicles[name] = record
	}
	if !record.lastAdvertisement.IsZero() && at.After(record.lastAdvertisement) {
		record.addGap(at.Sub(record.lastAdvertisement).Milliseconds())
	}
	record.count++
	record.rssi = rssi
	record.connectable = connectable
	if at.After(record.lastAdvertisement) {
		record.lastAdvertisement = at
	}
	if at.After(record.lastHeard) {
		record.lastHeard = at
	}
	if record.firstHeard.IsZero() {
		record.firstHeard = at
	}
	if viaAddress {
		record.viaAddress++
		record.lastSource = sourceAddress
		return
	}
	record.named++
	record.lastSource = sourceAdvertisement
	if record.address != address {
		//Rebinding drops the previous mapping so a rotated address can not keep counting for this car.
		delete(registry.addressToName, record.address)
		record.address = address
	}
	record.addressConfirmed = at
	registry.addressToName[address] = name
}

// note records presence proven by something other than an advertisement, e.g. a command the car answered. It leaves
// the advertisement counters and the gap statistics untouched: those are radio evidence and must stay that way.
func (registry *presenceRegistry) note(localName string, source string, at time.Time) {
	registry.mu.Lock()
	defer registry.mu.Unlock()
	record := registry.vehicles[localName]
	if record == nil {
		if len(registry.vehicles) >= maxTrackedVehicles {
			return
		}
		record = &vehicleRecord{localName: localName, firstHeard: at}
		registry.vehicles[localName] = record
	}
	if at.After(record.lastHeard) {
		record.lastHeard = at
	}
	record.lastSource = source
}

func (registry *presenceRegistry) presenceOf(localName string, maxAge time.Duration, now time.Time) VehiclePresence {
	registry.mu.Lock()
	defer registry.mu.Unlock()
	record := registry.vehicles[localName]
	if record == nil {
		return VehiclePresence{LocalName: localName}
	}
	return record.presence(now, maxAge, true)
}

// silence reports how long the radio provably received nothing at all, measured from the scanner start when it never
// received anything.
func (registry *presenceRegistry) silence(now time.Time) time.Duration {
	registry.mu.Lock()
	defer registry.mu.Unlock()
	if registry.lastAdvertisement.IsZero() {
		return now.Sub(registry.started)
	}
	return now.Sub(registry.lastAdvertisement)
}

func (registry *presenceRegistry) fill(snapshot *PresenceSnapshot, vins []string, maxAge time.Duration, now time.Time) {
	registry.mu.Lock()
	defer registry.mu.Unlock()
	snapshot.AdvertisementsSeen = registry.advertisements
	snapshot.DistinctDevicesSeen = len(registry.devices)
	if !registry.lastAdvertisement.IsZero() {
		age := now.Sub(registry.lastAdvertisement).Milliseconds()
		snapshot.LastAdvertisementMsAgo = &age
	}
	for _, vin := range vins {
		localName := VehicleLocalName(vin)
		presence := VehiclePresence{LocalName: localName}
		if record := registry.vehicles[localName]; record != nil {
			presence = record.presence(now, maxAge, true)
		}
		presence.Vin = vin
		snapshot.Vehicles = append(snapshot.Vehicles, presence)
	}
	for _, record := range registry.vehicles {
		snapshot.Tracked = append(snapshot.Tracked, record.presence(now, maxAge, false))
	}
	sort.Slice(snapshot.Tracked, func(a, b int) bool {
		return snapshot.Tracked[a].LocalName < snapshot.Tracked[b].LocalName
	})
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

// interrupt ends the running scan without claiming the radio, which is what the deafness watchdog needs.
func (arbiter *radioArbiter) interrupt() bool {
	arbiter.mu.Lock()
	defer arbiter.mu.Unlock()
	if !arbiter.scanning || arbiter.cancel == nil {
		return false
	}
	arbiter.cancel()
	return true
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
// arbiter handoff and the watchdog can be tested without a radio; the default implementation is the only place that
// touches go-ble.
type scanRunner func(ctx context.Context, observe func(localName string, address string, rssi int, connectable bool)) error

type presenceScanner struct {
	config   PresenceScannerConfig
	registry *presenceRegistry
	arbiter  *radioArbiter
	runner   scanRunner
	now      func() time.Time
	//quit is closed to ask the scan loop and the watchdog to end, done when the scan loop actually released the
	//adapter. Two channels because the loop must be able to wait for the stop request while it is backing off.
	quit chan struct{}
	done chan struct{}

	mu            sync.Mutex
	running       bool
	startedAt     time.Time
	scanActive    time.Duration
	restarts      int64
	scanErrors    int64
	lastScanError string
}

func newPresenceScanner(config PresenceScannerConfig, arbiter *radioArbiter, runner scanRunner, now func() time.Time) *presenceScanner {
	if now == nil {
		now = time.Now
	}
	if config.MaxAge <= 0 {
		config.MaxAge = 90 * time.Second
	}
	if config.AddressTtl <= 0 {
		config.AddressTtl = 10 * time.Minute
	}
	scanner := &presenceScanner{
		config:    config,
		arbiter:   arbiter,
		runner:    runner,
		now:       now,
		quit:      make(chan struct{}),
		done:      make(chan struct{}),
		running:   true,
		startedAt: now(),
	}
	scanner.registry = newPresenceRegistry(config.AddressTtl, scanner.startedAt)
	if scanner.runner == nil {
		scanner.runner = defaultScanRunner
	}
	return scanner
}

func (scanner *presenceScanner) start() {
	go scanner.run()
	if scanner.config.RestartAfter > 0 {
		go scanner.watchdog()
	}
}

func (scanner *presenceScanner) run() {
	defer close(scanner.done)
	for {
		ctx, ok := scanner.arbiter.beginScan()
		if !ok {
			return
		}
		start := scanner.now()
		err := scanner.runner(ctx, scanner.observe)
		scanner.arbiter.endScan()
		scanner.recordScan(scanner.now().Sub(start), err)
		if err == nil || errors.Is(err, context.Canceled) || errors.Is(err, context.DeadlineExceeded) {
			//The normal case: something else wanted the radio, or the watchdog re-armed the scan.
			continue
		}
		if IsAdapterError(err) {
			//A dead HCI socket only recovers with a fresh adapter bind, which needs a worker restart. Stop here so
			//the next command reports the adapter as unavailable instead of spinning.
			scanner.markStopped()
			return
		}
		select {
		case <-time.After(scanErrorBackoff):
		case <-scanner.quit:
			return
		}
	}
}

// watchdog re-arms the scan when the adapter received nothing at all for RestartAfter. That is the observed
// "adapter is up but hears nothing" state, and re-arming is the only recovery the worker can attempt by itself.
func (scanner *presenceScanner) watchdog() {
	//Checking twice per silence budget keeps the reaction time proportional to it, which also keeps the test of this
	//loop fast instead of pinning it to the production interval.
	interval := watchdogInterval
	if half := scanner.config.RestartAfter / 2; half < interval {
		interval = half
	}
	if interval < time.Millisecond {
		interval = time.Millisecond
	}
	ticker := time.NewTicker(interval)
	defer ticker.Stop()
	for {
		select {
		case <-scanner.quit:
			return
		case <-ticker.C:
			if scanner.registry.silence(scanner.now()) < scanner.config.RestartAfter {
				continue
			}
			if scanner.arbiter.interrupt() {
				scanner.mu.Lock()
				scanner.restarts++
				scanner.mu.Unlock()
			}
		}
	}
}

func (scanner *presenceScanner) observe(localName string, address string, rssi int, connectable bool) {
	scanner.registry.observe(localName, address, rssi, connectable, scanner.now())
}

func (scanner *presenceScanner) recordScan(duration time.Duration, err error) {
	scanner.mu.Lock()
	defer scanner.mu.Unlock()
	scanner.scanActive += duration
	if err != nil && !errors.Is(err, context.Canceled) && !errors.Is(err, context.DeadlineExceeded) {
		scanner.scanErrors++
		scanner.lastScanError = err.Error()
	}
}

func (scanner *presenceScanner) markStopped() {
	scanner.mu.Lock()
	defer scanner.mu.Unlock()
	scanner.running = false
}

func (scanner *presenceScanner) snapshot(vins []string, maxAge time.Duration) *PresenceSnapshot {
	now := scanner.now()
	scanner.mu.Lock()
	snapshot := &PresenceSnapshot{
		ScannerRunning:     scanner.running,
		ObservingMs:        now.Sub(scanner.startedAt).Milliseconds(),
		ScanActiveMs:       scanner.scanActive.Milliseconds(),
		Restarts:           scanner.restarts,
		ScanErrors:         scanner.scanErrors,
		LastScanError:      scanner.lastScanError,
		MaxAgeMs:           maxAge.Milliseconds(),
		ScanWhileConnected: scanner.config.ScanWhileConnected,
		Vehicles:           make([]VehiclePresence, 0, len(vins)),
		Tracked:            make([]VehiclePresence, 0),
	}
	scanner.mu.Unlock()
	snapshot.PausedMs = snapshot.ObservingMs - snapshot.ScanActiveMs
	if snapshot.PausedMs < 0 {
		snapshot.PausedMs = 0
	}
	scanner.registry.fill(snapshot, vins, maxAge, now)
	return snapshot
}

// defaultScanRunner is the only code that touches go-ble. Duplicates are allowed so every received advertisement
// counts: the advertisement totals are the caller's evidence that a silent scan came from a working radio.
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

var (
	//The arbiter exists independently of the scanner: AcquireRadio has to work even when the scanner is disabled.
	radio         = newRadioArbiter()
	activeScanner *presenceScanner
	scannerMu     sync.Mutex
)

// StartPresenceScanner starts the permanent background scan. Safe to call once per process; a second call replaces
// nothing and returns immediately.
func StartPresenceScanner(config PresenceScannerConfig) {
	scannerMu.Lock()
	defer scannerMu.Unlock()
	if activeScanner != nil {
		return
	}
	activeScanner = newPresenceScanner(config, radio, nil, nil)
	activeScanner.start()
}

// StopPresenceScanner ends the background scan and waits for the scan loop to release the adapter.
func StopPresenceScanner() {
	scannerMu.Lock()
	scanner := activeScanner
	activeScanner = nil
	scannerMu.Unlock()
	if scanner == nil {
		return
	}
	close(scanner.quit)
	radio.stop()
	select {
	case <-scanner.done:
	case <-time.After(5 * time.Second):
	}
}

// AcquireRadio hands the adapter to the caller, ending a running background scan first. The returned function gives
// it back. Every code path that scans, connects or closes the adapter must hold it.
func AcquireRadio() func() {
	return radio.acquire()
}

// PresenceScannerRunning reports whether presence questions can be answered at all.
func PresenceScannerRunning() bool {
	scannerMu.Lock()
	defer scannerMu.Unlock()
	if activeScanner == nil {
		return false
	}
	activeScanner.mu.Lock()
	defer activeScanner.mu.Unlock()
	return activeScanner.running
}

// Presence answers what is known about the given cars plus the state of the radio. It never touches the adapter, so
// it answers in microseconds and can not delay a command.
func Presence(vins []string, maxAge time.Duration) *PresenceSnapshot {
	scannerMu.Lock()
	scanner := activeScanner
	scannerMu.Unlock()
	if scanner == nil {
		snapshot := &PresenceSnapshot{MaxAgeMs: maxAge.Milliseconds(), Vehicles: make([]VehiclePresence, 0, len(vins)), Tracked: make([]VehiclePresence, 0)}
		for _, vin := range vins {
			snapshot.Vehicles = append(snapshot.Vehicles, VehiclePresence{Vin: vin, LocalName: VehicleLocalName(vin)})
		}
		return snapshot
	}
	if maxAge <= 0 {
		maxAge = scanner.config.MaxAge
	}
	return scanner.snapshot(vins, maxAge)
}

// NoteVehicleHeard records presence that was proven without an advertisement, e.g. by a command the car answered.
// This is what stops a car that talks to the worker constantly from ageing out of the presence map while its own
// traffic keeps the radio busy.
func NoteVehicleHeard(vin string, source string) {
	scannerMu.Lock()
	scanner := activeScanner
	scannerMu.Unlock()
	if scanner == nil {
		return
	}
	scanner.registry.note(VehicleLocalName(vin), source, scanner.now())
}

// WaitForVehicle waits until the car was heard within maxAge, or until ctx expires. The background scan keeps
// running while it waits, so waiting for an absent car costs no adapter time at all - which is the whole point:
// the windowed scan it replaces occupied the radio for its full window exactly when the car was not there.
func WaitForVehicle(ctx context.Context, vin string, maxAge time.Duration) (VehiclePresence, bool) {
	scannerMu.Lock()
	scanner := activeScanner
	scannerMu.Unlock()
	localName := VehicleLocalName(vin)
	if scanner == nil {
		return VehiclePresence{Vin: vin, LocalName: localName}, false
	}
	if maxAge <= 0 {
		maxAge = scanner.config.MaxAge
	}
	ticker := time.NewTicker(presencePollInterval)
	defer ticker.Stop()
	for {
		presence := scanner.registry.presenceOf(localName, maxAge, scanner.now())
		presence.Vin = vin
		if presence.Heard {
			return presence, true
		}
		select {
		case <-ctx.Done():
			return presence, false
		case <-ticker.C:
		}
	}
}
