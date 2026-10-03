// Reproduction of a go-ble defect measured at a user's site, where the BLE worker could neither scan nor connect for
// 28 hours until it was restarted.
//
// This is NOT part of the worker or the image. It has to live inside go-ble's own linux/hci package because it needs
// the package private socket and error fields, so it is kept here only as evidence and as a regression check for a
// go-ble upgrade. Run it against the go-ble version vehicle-command pins (see the vehicle-command go.mod):
//
//	git clone https://github.com/go-ble/ble.git && cd ble && git checkout 8c5522f54333
//	cp <this file> linux/hci/
//	docker run --rm -v "$PWD":/src -w /src golang:1.22 go test -run Sticky -v ./linux/hci/
//
// The defect, in go-ble linux/hci/hci.go:
//
//  1. A scan response is matched against a 128 entry history of advertisements that Scan() clears on every call.
//     A scan response without its advertisement in that history makes handleLEAdvertisingReport return an error.
//  2. handleEvt stores every event handler's result in h.err. The next advertising report that parses resets it, which
//     is why the error is harmless while a scan runs.
//  3. send() returns h.err before writing anything to the adapter, and Command Complete / Command Status events never
//     touch h.err. When the stray scan response is the last advertising report - the scan was just stopped, e.g. to
//     hand the radio to a command - nothing can ever reset it: every later Scan() and Dial() fails with the stored
//     error without reaching the adapter, so no advertising report can arrive to clear it. Only a new HCI instance,
//     i.e. binding the adapter again, recovers.
//
// The address used is the one from the user's logs. 0x52 starts with the bits 01, a resolvable private address as
// used by phones, watches and earbuds: the device did nothing wrong.
package hci

import (
	"context"
	"io"
	"net"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/go-ble/ble"
)

const (
	strayAddress = "52:65:f3:00:b2:99"
	otherAddress = "11:22:33:44:55:66"
)

// fakeController stands in for the Bluetooth adapter: it records every packet go-ble writes and acknowledges every
// command with a successful Command Complete, and it delivers events the test injects.
type fakeController struct {
	events chan []byte
	mu     sync.Mutex
	writes [][]byte
	closed chan struct{}
}

func newFakeController() *fakeController {
	return &fakeController{events: make(chan []byte), closed: make(chan struct{})}
}

func (f *fakeController) Read(b []byte) (int, error) {
	select {
	case event := <-f.events:
		return copy(b, event), nil
	case <-f.closed:
		return 0, io.EOF
	}
}

func (f *fakeController) Write(b []byte) (int, error) {
	packet := append([]byte(nil), b...)
	f.mu.Lock()
	f.writes = append(f.writes, packet)
	f.mu.Unlock()
	if packet[0] == pktTypeCommand {
		go f.deliver(commandComplete(packet[1], packet[2]))
	}
	return len(b), nil
}

func (f *fakeController) Close() error {
	select {
	case <-f.closed:
	default:
		close(f.closed)
	}
	return nil
}

func (f *fakeController) writeCount() int {
	f.mu.Lock()
	defer f.mu.Unlock()
	return len(f.writes)
}

func (f *fakeController) deliver(event []byte) {
	select {
	case f.events <- event:
	case <-f.closed:
	}
}

// inject delivers an event and returns once go-ble has fully processed it: the event channel is unbuffered, so the
// marker can only be read after the loop finished the event before it. The marker is a NOP Command Complete, which
// leaves h.err alone.
func (f *fakeController) inject(event []byte) {
	f.deliver(event)
	f.deliver(commandComplete(0x00, 0x00))
}

func commandComplete(opcodeLow byte, opcodeHigh byte) []byte {
	//HCI event packet: type, event code, parameter length, then allowed command packets, opcode and status.
	return []byte{pktTypeEvent, 0x0E, 0x04, 0x01, opcodeLow, opcodeHigh, 0x00}
}

// advertisingReport builds an LE Advertising Report with one report from a random address and no data.
func advertisingReport(eventType byte, address string) []byte {
	mac := mustParseAddress(address)
	report := []byte{0x02, 0x01, eventType, 0x01}
	//The address travels little endian.
	for index := 5; index >= 0; index-- {
		report = append(report, mac[index])
	}
	report = append(report, 0x00, 0xC0) // no data, RSSI -64
	return append([]byte{pktTypeEvent, 0x3E, byte(len(report))}, report...)
}

func mustParseAddress(address string) [6]byte {
	var mac [6]byte
	parts := strings.Split(address, ":")
	for index, part := range parts {
		var value byte
		for _, character := range part {
			value <<= 4
			switch {
			case character >= '0' && character <= '9':
				value |= byte(character - '0')
			case character >= 'a' && character <= 'f':
				value |= byte(character-'a') + 10
			}
		}
		mac[index] = value
	}
	return mac
}

// newTestHCI wires an HCI to the fake controller exactly like Init does, minus the real socket and the adapter
// initialization sequence.
func newTestHCI(t *testing.T) (*HCI, *fakeController) {
	t.Helper()
	h, err := NewHCI()
	if err != nil {
		t.Fatalf("NewHCI: %v", err)
	}
	h.evth[0x3E] = h.handleLEMeta
	h.evth[0x0E] = h.handleCommandComplete
	h.evth[0x0F] = h.handleCommandStatus
	h.subh[0x02] = h.handleLEAdvertisingReport
	controller := newFakeController()
	h.skt = controller
	h.setAllowedCommands(1)
	go h.sktLoop()
	t.Cleanup(func() { _ = controller.Close() })
	if err := h.SetAdvHandler(func(ble.Advertisement) {}); err != nil {
		t.Fatalf("SetAdvHandler: %v", err)
	}
	return h, controller
}

func assertStrayError(t *testing.T, err error) {
	t.Helper()
	if err == nil || !strings.Contains(err.Error(), "received scan response "+strayAddress+" with no associated Advertising Data packet") {
		t.Fatalf("expected the stray scan response error, got %v", err)
	}
}

// Control case: while a scan runs, the stored error lives only until the next advertising report.
func TestStickyScanErrorIsHarmlessWhileTheScanRuns(t *testing.T) {
	h, controller := newTestHCI(t)
	if err := h.Scan(true); err != nil {
		t.Fatalf("Scan: %v", err)
	}

	controller.inject(advertisingReport(evtTypScanRsp, strayAddress))
	assertStrayError(t, h.Error())

	controller.inject(advertisingReport(evtTypAdvInd, otherAddress))
	if h.Error() != nil {
		t.Fatalf("the next advertising report should have reset the error, got %v", h.Error())
	}
	before := controller.writeCount()
	if err := h.StopScanning(); err != nil {
		t.Fatalf("StopScanning: %v", err)
	}
	if controller.writeCount() != before+1 {
		t.Fatalf("StopScanning should have reached the adapter")
	}
}

// The measured failure: the stray scan response is the last advertising report, because the scan was just stopped.
func TestStickyScanErrorBlocksEveryLaterCommandAfterTheScanStopped(t *testing.T) {
	h, controller := newTestHCI(t)
	if err := h.Scan(true); err != nil {
		t.Fatalf("Scan: %v", err)
	}
	if err := h.StopScanning(); err != nil {
		t.Fatalf("StopScanning: %v", err)
	}
	//Already queued when the scan was disabled, processed right after.
	controller.inject(advertisingReport(evtTypScanRsp, strayAddress))

	before := controller.writeCount()
	for attempt := 0; attempt < 5; attempt++ {
		assertStrayError(t, h.Scan(true))
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	_, err := h.Dial(ctx, RandomAddress{net.HardwareAddr(mustHardwareAddress(otherAddress))})
	assertStrayError(t, err)
	if after := controller.writeCount(); after != before {
		t.Fatalf("no command may reach the adapter while the error is stored, but %d did", after-before)
	}

	//Nothing but a new HCI instance - binding the adapter again - recovers.
	fresh, freshController := newTestHCI(t)
	if err := fresh.Scan(true); err != nil {
		t.Fatalf("a fresh HCI must scan again: %v", err)
	}
	if freshController.writeCount() != 1 {
		t.Fatalf("the fresh HCI must reach the adapter")
	}
}

// Why a scan response loses its advertisement in the first place: Scan() clears the history on every call.
func TestScanResponseAfterAReArmHasNoAdvertisement(t *testing.T) {
	h, controller := newTestHCI(t)
	if err := h.Scan(true); err != nil {
		t.Fatalf("Scan: %v", err)
	}
	controller.inject(advertisingReport(evtTypAdvInd, strayAddress))
	if err := h.Scan(true); err != nil {
		t.Fatalf("re-arm: %v", err)
	}
	controller.inject(advertisingReport(evtTypScanRsp, strayAddress))
	assertStrayError(t, h.Error())
}

// The other way at a busy site: the history keeps only 128 advertisements.
func TestScanResponseAfterTheHistoryWrappedHasNoAdvertisement(t *testing.T) {
	h, controller := newTestHCI(t)
	if err := h.Scan(true); err != nil {
		t.Fatalf("Scan: %v", err)
	}
	controller.inject(advertisingReport(evtTypAdvInd, strayAddress))
	for index := 0; index < 128; index++ {
		controller.inject(advertisingReport(evtTypAdvInd, otherAddress))
	}
	controller.inject(advertisingReport(evtTypScanRsp, strayAddress))
	assertStrayError(t, h.Error())
}

func mustHardwareAddress(address string) []byte {
	mac := mustParseAddress(address)
	return mac[:]
}
