// tesla-bled: a long living BLE worker for the TeslaSolarCharger BLE container.
//
// tesla-control has to be started once per command, and every start re-initializes the Bluetooth adapter
// (HCIDEVDOWN/HCIDEVUP plus an exclusive HCI user channel bind). That adapter thrashing is a well known source of
// instability on Linux and costs about 1.8 s per command. This daemon is started once, keeps the adapter open and
// executes all commands over it, which brings a command down to about 60 ms while a connection is established.
//
// Tesla vehicles terminate a BLE connection after roughly 30 seconds no matter how much traffic runs over it
// (measured, and documented by other projects), so the connection is deliberately closed and rebuilt after
// -connection-window seconds. The adapter itself is never reset in between.
//
// The daemon never wakes a car on its own: if an infotainment session cannot be started because the car is asleep,
// the error is reported and TeslaSolarCharger decides whether to send a wake command. This keeps the BLE sleep
// window logic in TeslaSolarCharger effective.
//
// This file is copied into cmd/tesla-bled/ of the teslamotors/vehicle-command module during the Docker image build
// (see TeslaSolarCharger.BleApi/Dockerfile).
//
// Protocol: one JSON request per line on stdin, one JSON response per line on stdout.
//
//	<- {"kind":"ready"}
//	-> {"id":1,"vin":"5YJ...","command":"body-controller-state"}
//	<- {"kind":"result","id":1,"ok":true,"result":{...},"durationMs":58,"connectMs":0,"reconnected":false}
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

type request struct {
	Id      int      `json:"id"`
	Vin     string   `json:"vin"`
	Command string   `json:"command"`
	Params  []string `json:"params"`
}

type response struct {
	Kind         string          `json:"kind"`
	Id           int             `json:"id"`
	Ok           bool            `json:"ok"`
	Result       json.RawMessage `json:"result,omitempty"`
	Error        string          `json:"error,omitempty"`
	NotInRange   bool            `json:"notInRange,omitempty"`
	DurationMs   int64           `json:"durationMs"`
	ConnectMs    int64           `json:"connectMs"`
	Reconnected  bool            `json:"reconnected,omitempty"`
	TimestampUtc string          `json:"timestampUtc"`
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

// errNotInRange is reported separately so the caller can tell a missing car from a real failure. The wording keeps
// the word "beacon" because TeslaSolarCharger recognizes an out of range car by that word in the result message.
var errNotInRange = errors.New("failed to find BLE beacon: car is not in BLE range")

// errCarAsleep is returned when the infotainment system can not be reached because the car is asleep. Deliberately
// worded without "beacon" or "context deadline exceeded" so it is never mistaken for a car that is out of range.
var errCarAsleep = errors.New("car is asleep: its infotainment system can not be reached, wake it first")

type daemon struct {
	config           *cli.Config
	connectionWindow time.Duration
	scanTimeout      time.Duration
	commandTimeout   time.Duration

	car                 *vehicle.Vehicle
	connectedVin        string
	connectionDeadline  time.Time
	infotainmentStarted bool
}

func main() {
	os.Exit(run())
}

func run() int {
	var (
		debug            bool
		connectionWindow time.Duration
		scanTimeout      time.Duration
		commandTimeout   time.Duration
		connectTimeout   time.Duration
	)
	//Only BLE, VIN and private key: without the OAuth flag no token is required, which is what makes this work in a
	//BLE only container. Reusing the upstream config also keeps key handling and the session cache identical.
	config, err := cli.NewConfig(cli.FlagBLE | cli.FlagVIN | cli.FlagPrivateKey)
	if err != nil {
		writeLine(response{Kind: "error", Error: fmt.Sprintf("failed to load configuration: %s", err), TimestampUtc: nowUtc()})
		return 1
	}
	flag.BoolVar(&debug, "debug", false, "Enable verbose debugging messages")
	flag.DurationVar(&connectionWindow, "connection-window", 25*time.Second, "Close and rebuild the vehicle connection after this duration. Vehicles terminate connections after about 30 seconds.")
	flag.DurationVar(&scanTimeout, "scan-timeout", 2*time.Second, "Timeout for the beacon scan that decides whether a car is in range")
	flag.DurationVar(&commandTimeout, "command-timeout", 10*time.Second, "Timeout for a single command sent to the vehicle")
	flag.DurationVar(&connectTimeout, "connect-timeout", 20*time.Second, "Timeout for establishing a vehicle connection")
	config.RegisterCommandLineFlags()
	flag.Parse()
	if debug {
		log.SetLevel(log.LevelDebug)
	}
	config.ReadFromEnvironment()
	if err := config.LoadCredentials(); err != nil {
		writeLine(response{Kind: "error", Error: fmt.Sprintf("failed to load credentials: %s", err), TimestampUtc: nowUtc()})
		return 1
	}
	//Initializing the adapter here makes startup failures visible immediately instead of on the first command, and
	//from now on every connection reuses this adapter.
	if err := ble.InitAdapterWithID(config.BtAdapterID); err != nil {
		message := err.Error()
		if ble.IsAdapterError(err) {
			message = ble.AdapterErrorHelpMessage(err)
		}
		writeLine(response{Kind: "error", Error: fmt.Sprintf("failed to initialize BLE adapter: %s", message), TimestampUtc: nowUtc()})
		return 1
	}

	d := &daemon{
		config:           config,
		connectionWindow: connectionWindow,
		scanTimeout:      scanTimeout,
		commandTimeout:   commandTimeout,
	}
	//The connect timeout is applied per connection attempt inside ensureConnection.
	d.config.Domains = cli.DomainList{protocol.DomainVCSEC}
	defer d.disconnect()

	writeLine(response{Kind: "ready", Ok: true, TimestampUtc: nowUtc()})

	scanner := bufio.NewScanner(os.Stdin)
	scanner.Buffer(make([]byte, 0, 64*1024), 1024*1024)
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			continue
		}
		var req request
		if err := json.Unmarshal([]byte(line), &req); err != nil {
			writeLine(response{Kind: "result", Error: fmt.Sprintf("could not parse request: %s", err), TimestampUtc: nowUtc()})
			continue
		}
		if req.Command == "exit" {
			return 0
		}
		writeLine(d.handle(req, connectTimeout))
	}
	if err := scanner.Err(); err != nil {
		writeLine(response{Kind: "error", Error: fmt.Sprintf("failed to read requests: %s", err), TimestampUtc: nowUtc()})
		return 1
	}
	return 0
}

func (d *daemon) handle(req request, connectTimeout time.Duration) response {
	result := response{Kind: "result", Id: req.Id, TimestampUtc: nowUtc()}
	start := time.Now()

	if req.Command == "disconnect" {
		d.disconnect()
		result.Ok = true
		result.DurationMs = time.Since(start).Milliseconds()
		return result
	}
	if req.Vin == "" {
		result.Error = "vin is required"
		return result
	}
	//A beacon scan must not touch any connection: it only answers whether the car is reachable at all.
	if req.Command == "beacon-scan" {
		payload, err := d.scan(req.Vin)
		result.DurationMs = time.Since(start).Milliseconds()
		if err != nil {
			result.Error = err.Error()
			return result
		}
		result.Ok = true
		result.Result = payload
		return result
	}

	needsInfotainment, err := commandNeedsInfotainment(req.Command)
	if err != nil {
		result.Error = err.Error()
		return result
	}

	connectMs, reconnected, err := d.ensureConnection(req.Vin, needsInfotainment, connectTimeout)
	result.ConnectMs = connectMs
	result.Reconnected = reconnected
	if err != nil {
		result.Error = err.Error()
		result.NotInRange = errors.Is(err, errNotInRange)
		result.DurationMs = time.Since(start).Milliseconds()
		return result
	}

	message, err := d.execute(req)
	if err != nil && isDeadConnection(err) {
		//The vehicle hung up (it does so after about 30 s). Rebuild the connection once and retry the command.
		d.disconnect()
		retryConnectMs, _, connectErr := d.ensureConnection(req.Vin, needsInfotainment, connectTimeout)
		result.ConnectMs += retryConnectMs
		result.Reconnected = true
		if connectErr != nil {
			result.Error = connectErr.Error()
			result.NotInRange = errors.Is(connectErr, errNotInRange)
			result.DurationMs = time.Since(start).Milliseconds()
			return result
		}
		message, err = d.execute(req)
	}
	result.DurationMs = time.Since(start).Milliseconds()
	if err != nil {
		result.Error = err.Error()
		return result
	}
	if message == nil {
		//Commands without a payload (charge start/stop, set amps, ...) only report success.
		result.Ok = true
		return result
	}
	//Marshaled without indentation so a result never spans multiple lines.
	payload, err := protojson.MarshalOptions{UseEnumNumbers: false}.Marshal(message)
	if err != nil {
		result.Error = fmt.Sprintf("could not serialize answer: %s", err)
		return result
	}
	result.Ok = true
	result.Result = payload
	return result
}

// scan reports whether the car currently advertises, without connecting to it. This never wakes the car.
func (d *daemon) scan(vin string) (json.RawMessage, error) {
	ctx, cancel := context.WithTimeout(context.Background(), d.scanTimeout)
	defer cancel()
	beacon, err := ble.ScanVehicleBeacon(ctx, vin)
	if err != nil {
		//Not finding the beacon within the scan timeout is the normal "car is away" answer, not an error.
		return json.Marshal(map[string]any{"beaconFound": false})
	}
	return json.Marshal(map[string]any{
		"beaconFound": true,
		"rssi":        beacon.RSSI,
		"address":     beacon.Address,
		"connectable": beacon.Connectable,
	})
}

// ensureConnection makes sure a usable connection to vin exists, rebuilding it when the vehicle changed or the
// connection window elapsed. Returns how long connecting took and whether a new connection was built.
func (d *daemon) ensureConnection(vin string, needsInfotainment bool, connectTimeout time.Duration) (int64, bool, error) {
	if d.car != nil && (d.connectedVin != vin || time.Now().After(d.connectionDeadline)) {
		d.disconnect()
	}
	var connectMs int64
	reconnected := false
	if d.car == nil {
		start := time.Now()
		//Check presence first: dialing a car that is not there would block until the connect timeout.
		scanCtx, cancelScan := context.WithTimeout(context.Background(), d.scanTimeout)
		_, err := ble.ScanVehicleBeacon(scanCtx, vin)
		cancelScan()
		if err != nil {
			return time.Since(start).Milliseconds(), false, errNotInRange
		}
		ctx, cancel := context.WithTimeout(context.Background(), connectTimeout)
		defer cancel()
		d.config.VIN = vin
		//VCSEC only: the infotainment session is started lazily so sleeping cars are not disturbed.
		d.config.Domains = cli.DomainList{protocol.DomainVCSEC}
		_, car, err := d.config.Connect(ctx)
		if err != nil {
			return time.Since(start).Milliseconds(), false, fmt.Errorf("failed to connect: %s", err)
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
		if asleep, err := d.isAsleep(); err == nil && asleep {
			connectMs += time.Since(start).Milliseconds()
			return connectMs, reconnected, errCarAsleep
		}
		ctx, cancel := context.WithTimeout(context.Background(), d.commandTimeout)
		defer cancel()
		//Deliberately no Wakeup() here: TeslaSolarCharger decides when a car may be woken.
		if err := d.car.StartSession(ctx, []universal.Domain{protocol.DomainInfotainment}); err != nil {
			connectMs += time.Since(start).Milliseconds()
			if isDeadConnection(err) {
				return connectMs, reconnected, err
			}
			//The underlying error is a context deadline, which must not leak into the message: the caller looks for
			//that wording to detect a car that is out of range, and an asleep car is not out of range.
			return connectMs, reconnected, errCarAsleep
		}
		d.infotainmentStarted = true
		connectMs += time.Since(start).Milliseconds()
	}
	return connectMs, reconnected, nil
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
// the connection (after roughly 30 seconds) or when it drives out of range.
func isDeadConnection(err error) bool {
	message := strings.ToLower(err.Error())
	return strings.Contains(message, "closed pipe") ||
		strings.Contains(message, "input channel closed") ||
		strings.Contains(message, "not connected")
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
