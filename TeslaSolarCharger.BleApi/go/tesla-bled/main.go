// tesla-bled: the long living BLE worker of the TeslaSolarCharger BLE container.
//
// tesla-control has to be started once per command, and every start re-initializes the Bluetooth adapter
// (HCIDEVDOWN/HCIDEVUP plus an exclusive HCI user channel bind). That adapter cycling is the only remaining hard
// failure class (measured: 33 % "can't init hci" at 0 s command gap, 0 % at 2 s). This worker is started once per
// adapter, opens it exactly once via ble.InitAdapterWithID and never cycles it.
//
// Tesla vehicles terminate a BLE connection after roughly 30 seconds no matter how much traffic runs over it, so the
// vehicle connection is deliberately closed and rebuilt after -connection-window seconds. The adapter itself is never
// reset in between. The worker never wakes a car on its own: TeslaSolarCharger decides when a car may be woken.
//
// This file is copied into cmd/tesla-bled/ of the teslamotors/vehicle-command module during the Docker image build
// (see TeslaSolarCharger.BleApi/Dockerfile). It needs ble.ScanBeacons from the injected
// pkg/connector/ble/beacon_scan_multi.go.
//
// Protocol: one JSON request per line on stdin, one JSON response per line on stdout. stdout carries protocol lines
// only; all logging goes to stderr. Every result carries a machine readable "outcome" and "phase"; the "error" text
// is for humans and is never parsed anywhere.
//
//	<- {"kind":"ready","protocolVersion":1,"adapterId":"hci0"}
//	-> {"id":1,"kind":"command","vin":"5YJ...","command":"body-controller-state"}
//	<- {"kind":"result","id":1,"ok":true,"outcome":"ok","result":{...},"durationMs":58,"connectMs":0}
package main

import (
	"bufio"
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"

	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"

	"github.com/teslamotors/vehicle-command/internal/log"
	"github.com/teslamotors/vehicle-command/pkg/cli"
	"github.com/teslamotors/vehicle-command/pkg/connector/ble"
	"github.com/teslamotors/vehicle-command/pkg/protocol"
	universal "github.com/teslamotors/vehicle-command/pkg/protocol/protobuf/universalmessage"
	"github.com/teslamotors/vehicle-command/pkg/protocol/protobuf/vcsec"
	"github.com/teslamotors/vehicle-command/pkg/vehicle"
)

const protocolVersion = 1

// Outcome values are the wire contract with the C# container, which maps them onto the shared BleCommandOutcome
// enum. Presence decisions are made on the outcome alone, never on any message text.
const (
	outcomeOk                 = "ok"
	outcomeCarAbsent          = "carAbsent"
	outcomeLinkFailed         = "linkFailed"
	outcomeCarAsleep          = "carAsleep"
	outcomeCarRefused         = "carRefused"
	outcomeAdapterUnavailable = "adapterUnavailable"
	outcomeInvalidRequest     = "invalidRequest"
)

const (
	phaseScan    = "scan"
	phaseConnect = "connect"
	phaseSession = "session"
	phaseCommand = "command"
)

type request struct {
	Id       int      `json:"id"`
	Kind     string   `json:"kind"`
	Vin      string   `json:"vin"`
	Command  string   `json:"command"`
	Params   []string `json:"params"`
	Vins     []string `json:"vins"`
	WindowMs int      `json:"windowMs"`
}

type scanInfo struct {
	BeaconFound             bool    `json:"beaconFound"`
	Rssi                    *int    `json:"rssi,omitempty"`
	Address                 *string `json:"address,omitempty"`
	Connectable             *bool   `json:"connectable,omitempty"`
	OtherAdvertisementsSeen int     `json:"otherAdvertisementsSeen"`
	DistinctDevicesSeen     int     `json:"distinctDevicesSeen"`
	ScanDurationMs          int64   `json:"scanDurationMs"`
}

type beaconScanVehicle struct {
	Vin          string  `json:"vin"`
	BeaconFound  bool    `json:"beaconFound"`
	Rssi         *int    `json:"rssi,omitempty"`
	Address      *string `json:"address,omitempty"`
	Connectable  *bool   `json:"connectable,omitempty"`
	FoundAfterMs *int64  `json:"foundAfterMs,omitempty"`
}

type beaconScanInfo struct {
	WindowMs                int                 `json:"windowMs"`
	ScanDurationMs          int64               `json:"scanDurationMs"`
	OtherAdvertisementsSeen int                 `json:"otherAdvertisementsSeen"`
	DistinctDevicesSeen     int                 `json:"distinctDevicesSeen"`
	Vehicles                []beaconScanVehicle `json:"vehicles"`
}

type response struct {
	Kind            string          `json:"kind"`
	ProtocolVersion int             `json:"protocolVersion,omitempty"`
	AdapterId       string          `json:"adapterId,omitempty"`
	Id              int             `json:"id,omitempty"`
	Ok              bool            `json:"ok"`
	Outcome         string          `json:"outcome,omitempty"`
	Phase           string          `json:"phase,omitempty"`
	Result          json.RawMessage `json:"result,omitempty"`
	Error           string          `json:"error,omitempty"`
	CarErrorMessage string          `json:"carErrorMessage,omitempty"`
	Scan            *scanInfo       `json:"scan,omitempty"`
	BeaconScan      *beaconScanInfo `json:"beaconScan,omitempty"`
	DurationMs      int64           `json:"durationMs"`
	ConnectMs       int64           `json:"connectMs"`
	Reconnected     bool            `json:"reconnected,omitempty"`
	TimestampUtc    string          `json:"timestampUtc"`
}

// classifiedFailure carries a failure with the outcome already decided at the failure site, so no caller ever has to
// re-derive it from message text.
type classifiedFailure struct {
	Outcome  string
	Phase    string
	Message  string
	DeadLink bool
	// AdapterDead requests a worker self-exit after the response is written: a dead HCI socket cannot recover
	// without a fresh adapter bind, which only a restart (one adapter init) provides.
	AdapterDead bool
}

// Categories of the "state" command. GetCategory of tesla-control lives in its main package and can not be reused.
var stateCategories = map[string]vehicle.StateCategory{
	"charge":                vehicle.StateCategoryCharge,
	"climate":               vehicle.StateCategoryClimate,
	"drive":                 vehicle.StateCategoryDrive,
	"location":              vehicle.StateCategoryLocation,
	"closures":              vehicle.StateCategoryClosures,
	"charge-schedule":       vehicle.StateCategoryChargeSchedule,
	"precondition-schedule": vehicle.StateCategoryPreconditioningSchedule,
	"tire-pressure":         vehicle.StateCategoryTirePressure,
	"media":                 vehicle.StateCategoryMedia,
	"media-detail":          vehicle.StateCategoryMediaDetail,
	"software-update":       vehicle.StateCategorySoftwareUpdate,
	"parental-controls":     vehicle.StateCategoryParentalControls,
}

type daemon struct {
	config           *cli.Config
	connectionWindow time.Duration
	commandTimeout   time.Duration
	connectTimeout   time.Duration
	scanWindow       time.Duration

	car                 *vehicle.Vehicle
	connectedVin        string
	connectionDeadline  time.Time
	infotainmentStarted bool
	exitAfterReply      bool
}

func main() {
	os.Exit(run())
}

func run() int {
	var (
		debug            bool
		connectionWindow time.Duration
		commandTimeout   time.Duration
		connectTimeout   time.Duration
		scanWindow       time.Duration
	)
	//Only BLE, VIN and private key: without the OAuth flag no token is required, which is what makes this work in a
	//BLE only container. Reusing the upstream config also keeps key handling and the session cache identical.
	config, err := cli.NewConfig(cli.FlagBLE | cli.FlagVIN | cli.FlagPrivateKey)
	if err != nil {
		writeLine(response{Kind: "fatal", Outcome: outcomeAdapterUnavailable, Error: fmt.Sprintf("failed to load configuration: %s", err), TimestampUtc: nowUtc()})
		return 1
	}
	flag.BoolVar(&debug, "debug", false, "Enable verbose debugging messages")
	flag.DurationVar(&connectionWindow, "connection-window", 25*time.Second, "Close and rebuild the vehicle connection after this duration. Vehicles terminate connections after about 30 seconds.")
	flag.DurationVar(&commandTimeout, "command-timeout", 10*time.Second, "Timeout for a single command sent to the vehicle")
	flag.DurationVar(&connectTimeout, "connect-timeout", 10*time.Second, "Timeout for finding and connecting to a vehicle. A present but slow to advertise car occasionally needs the full budget.")
	flag.DurationVar(&scanWindow, "scan-window", 3*time.Second, "Default window of a beaconScan request when the request carries no windowMs")
	config.RegisterCommandLineFlags()
	flag.Parse()
	if debug {
		log.SetLevel(log.LevelDebug)
	}
	config.ReadFromEnvironment()
	if err := config.LoadCredentials(); err != nil {
		writeLine(response{Kind: "fatal", Outcome: outcomeAdapterUnavailable, Error: fmt.Sprintf("failed to load credentials: %s", err), TimestampUtc: nowUtc()})
		return 1
	}
	//Initializing the adapter here makes startup failures visible immediately instead of on the first command, and
	//from now on every request reuses this adapter. This is the only adapter init in the worker's whole lifetime.
	if err := ble.InitAdapterWithID(config.BtAdapterID); err != nil {
		message := err.Error()
		if ble.IsAdapterError(err) {
			message = ble.AdapterErrorHelpMessage(err)
		}
		writeLine(response{Kind: "fatal", Outcome: outcomeAdapterUnavailable, Error: fmt.Sprintf("failed to initialize BLE adapter: %s", message), TimestampUtc: nowUtc()})
		return 1
	}

	d := &daemon{
		config:           config,
		connectionWindow: connectionWindow,
		commandTimeout:   commandTimeout,
		connectTimeout:   connectTimeout,
		scanWindow:       scanWindow,
	}
	defer d.disconnect()
	defer func() { _ = ble.CloseAdapter() }()

	adapterId := config.BtAdapterID
	if adapterId == "" {
		adapterId = "default"
	}
	writeLine(response{Kind: "ready", Ok: true, ProtocolVersion: protocolVersion, AdapterId: adapterId, TimestampUtc: nowUtc()})

	scanner := bufio.NewScanner(os.Stdin)
	scanner.Buffer(make([]byte, 0, 64*1024), 1024*1024)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		var req request
		if err := json.Unmarshal([]byte(line), &req); err != nil {
			writeLine(response{Kind: "result", Ok: false, Outcome: outcomeInvalidRequest, Error: fmt.Sprintf("could not parse request: %s", err), TimestampUtc: nowUtc()})
			continue
		}
		if requestKind(req) == "exit" {
			writeLine(response{Kind: "result", Id: req.Id, Ok: true, Outcome: outcomeOk, TimestampUtc: nowUtc()})
			return 0
		}
		writeLine(d.handle(req))
		if d.exitAfterReply {
			//The HCI socket is dead; only a fresh adapter bind helps, and that needs a process restart.
			return 1
		}
	}
	if err := scanner.Err(); err != nil {
		fmt.Fprintf(os.Stderr, "failed to read requests: %s\n", err)
		return 1
	}
	return 0
}

// requestKind infers the kind for hand written requests that only carry a command, so the worker stays usable from a
// terminal like the tesla-control shell.
func requestKind(req request) string {
	if req.Kind != "" {
		return req.Kind
	}
	if req.Command != "" {
		return "command"
	}
	return ""
}

func (d *daemon) handle(req request) response {
	result := response{Kind: "result", Id: req.Id, TimestampUtc: nowUtc()}
	start := time.Now()
	switch requestKind(req) {
	case "ping":
		result.Ok = true
		result.Outcome = outcomeOk
	case "beaconScan":
		result = d.beaconScan(req, result)
	case "command":
		result = d.handleCommand(req, result)
	default:
		result.Outcome = outcomeInvalidRequest
		result.Error = fmt.Sprintf("unknown request kind '%s'", req.Kind)
	}
	result.DurationMs = time.Since(start).Milliseconds()
	return result
}

// beaconScan answers which of the given cars currently advertise, without connecting to any of them. This never
// wakes a car. All VINs share one scan window: a present car is heard within milliseconds, an absent car costs the
// window once instead of once per car.
func (d *daemon) beaconScan(req request, result response) response {
	if len(req.Vins) == 0 {
		result.Outcome = outcomeInvalidRequest
		result.Error = "vins is required"
		return result
	}
	window := d.scanWindow
	if req.WindowMs > 0 {
		window = time.Duration(req.WindowMs) * time.Millisecond
	}
	ctx, cancel := context.WithTimeout(context.Background(), window)
	defer cancel()
	summary, err := ble.ScanBeacons(ctx, req.Vins)
	if err != nil {
		d.exitAfterReply = ble.IsAdapterError(err)
		result.Outcome = outcomeAdapterUnavailable
		result.Phase = phaseScan
		result.Error = sanitizeErrorText(fmt.Sprintf("scan failed: %s", err))
		return result
	}
	info := beaconScanInfo{
		WindowMs:                int(window.Milliseconds()),
		ScanDurationMs:          summary.ScanDurationMs,
		OtherAdvertisementsSeen: summary.OtherAdvertisementsSeen,
		DistinctDevicesSeen:     summary.DistinctDevicesSeen,
		Vehicles:                make([]beaconScanVehicle, 0, len(summary.Vehicles)),
	}
	for _, beacon := range summary.Vehicles {
		entry := beaconScanVehicle{Vin: beacon.Vin, BeaconFound: beacon.BeaconFound}
		if beacon.BeaconFound {
			rssi := beacon.Rssi
			address := beacon.Address
			connectable := beacon.Connectable
			foundAfterMs := beacon.FoundAfterMs
			entry.Rssi = &rssi
			entry.Address = &address
			entry.Connectable = &connectable
			entry.FoundAfterMs = &foundAfterMs
		}
		info.Vehicles = append(info.Vehicles, entry)
	}
	result.Ok = true
	result.Outcome = outcomeOk
	result.BeaconScan = &info
	return result
}

func (d *daemon) handleCommand(req request, result response) response {
	if req.Vin == "" {
		result.Outcome = outcomeInvalidRequest
		result.Error = "vin is required"
		return result
	}
	if req.Command == "disconnect" {
		d.disconnect()
		result.Ok = true
		result.Outcome = outcomeOk
		return result
	}
	needsInfotainment, err := commandNeedsInfotainment(req.Command)
	if err != nil {
		result.Outcome = outcomeInvalidRequest
		result.Error = err.Error()
		return result
	}

	retriedDeadLink := false
	var si *scanInfo
	var connectMs int64
	var reconnected bool
	for {
		var failure *classifiedFailure
		si, connectMs, reconnected, failure = d.ensureConnection(req.Vin, needsInfotainment)
		result.Scan = si
		result.ConnectMs += connectMs
		result.Reconnected = result.Reconnected || reconnected
		if failure == nil {
			break
		}
		if failure.DeadLink && !retriedDeadLink {
			//The vehicle hung up (it does so after about 30 s). Rebuild the connection once.
			retriedDeadLink = true
			d.disconnect()
			continue
		}
		d.exitAfterReply = failure.AdapterDead
		result.Outcome = failure.Outcome
		result.Phase = failure.Phase
		result.Error = failure.Message
		return result
	}

	message, err := d.execute(req)
	if err != nil && isDeadConnection(err) && !retriedDeadLink {
		//The vehicle hung up mid command. Rebuild the connection once and retry the command.
		d.disconnect()
		retryConnectMs, _, failure := d.reconnectForRetry(req.Vin, needsInfotainment)
		result.ConnectMs += retryConnectMs
		result.Reconnected = true
		if failure != nil {
			d.exitAfterReply = failure.AdapterDead
			result.Outcome = failure.Outcome
			result.Phase = failure.Phase
			result.Error = failure.Message
			return result
		}
		message, err = d.execute(req)
	}
	if err != nil {
		outcome, text, carError := classifyExecuteError(err)
		result.Outcome = outcome
		result.Phase = phaseCommand
		result.Error = text
		result.CarErrorMessage = carError
		return result
	}
	result.Ok = true
	result.Outcome = outcomeOk
	if message == nil {
		//Commands without a payload (charge start/stop, set amps, ...) only report success.
		return result
	}
	//Marshaled without indentation so a result never spans multiple lines.
	payload, err := protojson.MarshalOptions{UseEnumNumbers: false}.Marshal(message)
	if err != nil {
		result.Ok = false
		result.Outcome = outcomeLinkFailed
		result.Phase = phaseCommand
		result.Error = fmt.Sprintf("could not serialize answer: %s", err)
		return result
	}
	result.Result = payload
	return result
}

// reconnectForRetry rebuilds the connection for the dead link retry without touching the already recorded scan info.
func (d *daemon) reconnectForRetry(vin string, needsInfotainment bool) (int64, bool, *classifiedFailure) {
	_, connectMs, reconnected, failure := d.ensureConnection(vin, needsInfotainment)
	return connectMs, reconnected, failure
}

// ensureConnection makes sure a usable connection to vin exists, rebuilding it when the vehicle changed or the
// connection window elapsed. The scan gate runs with the full connect timeout: a present but slow to advertise car
// occasionally needs it, and shortening it here would silently lower the presence budget of one-shot commands.
func (d *daemon) ensureConnection(vin string, needsInfotainment bool) (*scanInfo, int64, bool, *classifiedFailure) {
	if d.car != nil && (d.connectedVin != vin || time.Now().After(d.connectionDeadline)) {
		d.disconnect()
	}
	var si *scanInfo
	var connectMs int64
	reconnected := false
	if d.car == nil {
		start := time.Now()
		//Scan first: dialing a car that is not there would block until the connect timeout, and only a scan miss may
		//ever classify a car as absent. The scan also collects the advertisement counters as radio evidence.
		scanCtx, cancelScan := context.WithTimeout(context.Background(), d.connectTimeout)
		summary, err := ble.ScanBeacons(scanCtx, []string{vin})
		cancelScan()
		if err != nil {
			return nil, time.Since(start).Milliseconds(), false, &classifiedFailure{
				Outcome:     outcomeAdapterUnavailable,
				Phase:       phaseScan,
				Message:     sanitizeErrorText(fmt.Sprintf("scan failed: %s", err)),
				AdapterDead: ble.IsAdapterError(err),
			}
		}
		si = scanInfoFromSummary(summary)
		if !si.BeaconFound {
			return si, time.Since(start).Milliseconds(), false, &classifiedFailure{Outcome: outcomeCarAbsent, Phase: phaseScan, Message: absentMessage(vin, si)}
		}
		ctx, cancel := context.WithTimeout(context.Background(), d.connectTimeout)
		defer cancel()
		d.config.VIN = vin
		//VCSEC only: the infotainment session is started lazily so sleeping cars are not disturbed. config.Connect
		//scans again internally (~50 ms, the car provably advertises) and keeps the upstream key and session cache
		//handling identical to tesla-control.
		d.config.Domains = cli.DomainList{protocol.DomainVCSEC}
		_, car, err := d.config.Connect(ctx)
		if err != nil {
			return si, time.Since(start).Milliseconds(), false, &classifiedFailure{
				Outcome:     outcomeLinkFailed,
				Phase:       phaseConnect,
				Message:     sanitizeErrorText(fmt.Sprintf("failed to connect: %s", err)),
				AdapterDead: ble.IsAdapterError(err),
			}
		}
		d.car = car
		d.connectedVin = vin
		d.infotainmentStarted = false
		d.connectionDeadline = time.Now().Add(d.connectionWindow)
		connectMs = time.Since(start).Milliseconds()
		reconnected = true
	}
	if needsInfotainment && !d.infotainmentStarted {
		start := time.Now()
		//Asking the (cheap) body controller first turns a pointless 10 s handshake timeout into a 60 ms answer:
		//a sleeping car's infotainment system does not answer at all.
		asleep, err := d.isAsleep()
		if err != nil {
			connectMs += time.Since(start).Milliseconds()
			return si, connectMs, reconnected, &classifiedFailure{
				Outcome:  outcomeLinkFailed,
				Phase:    phaseSession,
				Message:  sanitizeErrorText(fmt.Sprintf("failed to read body controller state: %s", err)),
				DeadLink: isDeadConnection(err),
			}
		}
		if asleep {
			connectMs += time.Since(start).Milliseconds()
			return si, connectMs, reconnected, &classifiedFailure{
				Outcome: outcomeCarAsleep,
				Phase:   phaseSession,
				Message: "car is asleep: its infotainment system can not be reached, wake it first",
			}
		}
		ctx, cancel := context.WithTimeout(context.Background(), d.commandTimeout)
		defer cancel()
		//Deliberately no Wakeup() here: TeslaSolarCharger decides when a car may be woken. The probe just said the
		//car is awake, so a failure here is link or car trouble, not sleep (a car falling asleep within the ~60 ms
		//between probe and handshake is possible but rare, and both classifications confirm presence).
		if err := d.car.StartSession(ctx, []universal.Domain{protocol.DomainInfotainment}); err != nil {
			connectMs += time.Since(start).Milliseconds()
			return si, connectMs, reconnected, &classifiedFailure{
				Outcome:  outcomeLinkFailed,
				Phase:    phaseSession,
				Message:  sanitizeErrorText(fmt.Sprintf("failed to start infotainment session: %s", err)),
				DeadLink: isDeadConnection(err),
			}
		}
		d.infotainmentStarted = true
		connectMs += time.Since(start).Milliseconds()
	}
	return si, connectMs, reconnected, nil
}

// absentMessage is the human text of a carAbsent result. The word "beacon" is kept in this message (and appears in
// no other) so an old TSC server's substring classifier keeps working during a rollout window. New servers only ever
// look at the outcome.
func absentMessage(vin string, si *scanInfo) string {
	return fmt.Sprintf(
		"failed to find BLE beacon for %s: car is not in BLE range (scanned %d ms, heard %d advertisements from %d other devices)",
		vin, si.ScanDurationMs, si.OtherAdvertisementsSeen, si.DistinctDevicesSeen)
}

func scanInfoFromSummary(summary *ble.BeaconScanSummary) *scanInfo {
	si := &scanInfo{
		OtherAdvertisementsSeen: summary.OtherAdvertisementsSeen,
		DistinctDevicesSeen:     summary.DistinctDevicesSeen,
		ScanDurationMs:          summary.ScanDurationMs,
	}
	if len(summary.Vehicles) > 0 && summary.Vehicles[0].BeaconFound {
		beacon := summary.Vehicles[0]
		rssi := beacon.Rssi
		address := beacon.Address
		connectable := beacon.Connectable
		si.BeaconFound = true
		si.Rssi = &rssi
		si.Address = &address
		si.Connectable = &connectable
	}
	return si
}

// isAsleep asks the body controller (VCSEC) whether the car is asleep. That works while the car sleeps and does not
// wake it, unlike anything that talks to the infotainment system.
func (d *daemon) isAsleep() (bool, error) {
	ctx, cancel := context.WithTimeout(context.Background(), d.commandTimeout)
	defer cancel()
	state, err := d.car.BodyControllerState(ctx)
	if err != nil {
		return false, err
	}
	return state.GetVehicleSleepStatus() != vcsec.VehicleSleepStatus_E_VEHICLE_SLEEP_STATUS_AWAKE, nil
}

func (d *daemon) disconnect() {
	if d.car == nil {
		return
	}
	//Persist the sessions so the next connection can skip the handshake.
	d.config.UpdateCachedSessions(d.car)
	d.car.Disconnect()
	d.car = nil
	d.connectedVin = ""
	d.infotainmentStarted = false
}

func (d *daemon) execute(req request) (proto.Message, error) {
	ctx, cancel := context.WithTimeout(context.Background(), d.commandTimeout)
	defer cancel()
	fields := strings.Fields(req.Command)
	switch fields[0] {
	case "body-controller-state":
		return d.car.BodyControllerState(ctx)
	case "state":
		category, err := stateCategory(req)
		if err != nil {
			return nil, err
		}
		return d.car.GetState(ctx, category)
	case "charging-start":
		return nil, d.car.ChargeStart(ctx)
	case "charging-stop":
		return nil, d.car.ChargeStop(ctx)
	case "charging-set-amps":
		amps, err := singleIntParam(req, "amps")
		if err != nil {
			return nil, err
		}
		return nil, d.car.SetChargingAmps(ctx, amps)
	case "charging-set-limit":
		percent, err := singleIntParam(req, "percent")
		if err != nil {
			return nil, err
		}
		return nil, d.car.ChangeChargeLimit(ctx, percent)
	case "wake":
		return nil, d.car.Wakeup(ctx)
	case "flash-lights":
		return nil, d.car.FlashLights(ctx)
	default:
		return nil, fmt.Errorf("unknown command '%s'", fields[0])
	}
}

// classifyExecuteError decides the outcome of a failed command on the typed final error. A car refusal surfaces as
// protocol.NominalError (infotainment) or protocol.NominalVCSECError (VCSEC); everything else on an established link
// is link or car trouble and therefore confirms presence.
func classifyExecuteError(err error) (outcome string, text string, carError string) {
	var nominal *protocol.NominalError
	if errors.As(err, &nominal) {
		return outcomeCarRefused, sanitizeErrorText(err.Error()), stripRefusalPrefix(nominal.Details.Error())
	}
	var vcsecNominal *protocol.NominalVCSECError
	if errors.As(err, &vcsecNominal) {
		return outcomeCarRefused, sanitizeErrorText(err.Error()), stripRefusalPrefix(vcsecNominal.Error())
	}
	return outcomeLinkFailed, sanitizeErrorText(err.Error()), ""
}

// stripRefusalPrefix reduces a refusal message to the reason the car gave, matching what TeslaSolarCharger's
// CarErrorMessage contained when tesla-control printed the refusal.
func stripRefusalPrefix(message string) string {
	message = strings.TrimPrefix(message, "car could not execute command: ")
	message = strings.TrimPrefix(message, "vcsec could not execute command: ")
	return message
}

func commandNeedsInfotainment(command string) (bool, error) {
	fields := strings.Fields(command)
	if len(fields) == 0 {
		return false, errors.New("command is required")
	}
	switch fields[0] {
	case "body-controller-state", "wake":
		//VCSEC commands: work while the car is asleep and do not wake it.
		return false, nil
	case "state", "charging-start", "charging-stop", "charging-set-amps", "charging-set-limit", "flash-lights":
		return true, nil
	default:
		return false, fmt.Errorf("unknown command '%s'", fields[0])
	}
}

// stateCategory reads the category of a "state" request. TeslaSolarCharger sends it as a parameter
// ({"command":"state","params":["charge"]}), a category appended to the command ("state charge") is accepted as well
// so requests can be issued by hand like on the tesla-control command line.
func stateCategory(req request) (vehicle.StateCategory, error) {
	name := ""
	if fields := strings.Fields(req.Command); len(fields) > 1 {
		name = fields[1]
	} else if len(req.Params) > 0 {
		name = req.Params[0]
	}
	if name == "" {
		return 0, errors.New("state requires a category, e.g. 'state charge'")
	}
	category, ok := stateCategories[name]
	if !ok {
		return 0, fmt.Errorf("unknown state category '%s'", name)
	}
	return category, nil
}

func singleIntParam(req request, name string) (int32, error) {
	if len(req.Params) < 1 {
		return 0, fmt.Errorf("%s is required", name)
	}
	value, err := strconv.ParseInt(req.Params[0], 10, 32)
	if err != nil {
		return 0, fmt.Errorf("invalid %s '%s'", name, req.Params[0])
	}
	return int32(value), nil
}

// isDeadConnection reports whether an error means the BLE link is gone, which happens when the vehicle terminates
// the connection (after roughly 30 seconds) or when it drives out of range. The strings come from go-ble and are the
// only string matching in the whole worker; they are pinned by tests against the vendored vehicle-command version.
func isDeadConnection(err error) bool {
	message := strings.ToLower(err.Error())
	return strings.Contains(message, "closed pipe") ||
		strings.Contains(message, "input channel closed") ||
		strings.Contains(message, "not connected")
}

// sanitizeErrorText rewrites wordings an old TSC server would misread as "car is not at home": its substring
// classifier matches "beacon" and "context deadline exceeded" anywhere in the message. Only the deliberately crafted
// carAbsent message may contain the word "beacon".
func sanitizeErrorText(message string) string {
	message = strings.ReplaceAll(message, "context deadline exceeded", "timed out")
	message = strings.ReplaceAll(message, "beacon", "advertisement")
	message = strings.ReplaceAll(message, "Beacon", "Advertisement")
	return message
}

func writeLine(message response) {
	payload, err := json.Marshal(message)
	if err != nil {
		fmt.Fprintf(os.Stderr, "could not serialize message: %s\n", err)
		return
	}
	fmt.Fprintln(os.Stdout, string(payload))
}

func nowUtc() string {
	return time.Now().UTC().Format(time.RFC3339Nano)
}
