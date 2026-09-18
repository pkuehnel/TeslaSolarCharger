using Newtonsoft.Json;
using PkSoftwareService.Custom.Backend.Ble;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;


/// <summary>
/// Covers how a failed BLE request is turned into something the user can act on. Getting this wrong sends people
/// looking for the wrong problem, e.g. re-pairing a key for a car that simply is not at home.
/// </summary>
public class TeslaBleServiceTests : TestBase
{
    private const string TestVin = "TESTVIN123456789A";

    public TeslaBleServiceTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    [Fact]
    public void ReadableChargeStateMeansEverythingWorks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = true, });

        Assert.Equal(BleConnectionTestResultType.Success, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.AdapterNotFound)]
    [InlineData(BleCommandOutcome.AdapterUnavailable)]
    [InlineData(BleCommandOutcome.WorkerError)]
    [InlineData(BleCommandOutcome.WorkerTimeout)]
    [InlineData(BleCommandOutcome.InvalidRequest)]
    public void LocalProblemsAreReportedAsContainerProblem(BleCommandOutcome outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void SleepingCarNeedsNoFurtherChecks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarAsleep,
        });

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    /// <summary>
    /// The car's own rejection is the only proof that TSC's key is missing, and it used to be thrown away: reported as
    /// a link failure it ended up as "the key works, please test again" while the car said the opposite.
    /// </summary>
    [Fact]
    public void CarRejectingTheKeyNeedsNoFurtherChecks()
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.KeyNotPaired,
        });

        Assert.Equal(BleConnectionTestResultType.KeyNotPaired, result);
    }

    [Theory]
    [InlineData(BleCommandOutcome.CarAbsent)]
    [InlineData(BleCommandOutcome.LinkFailed)]
    [InlineData(BleCommandOutcome.CarRefused)]
    [InlineData(null)]
    public void UnclearOutcomesAreNarrowedDownFurther(BleCommandOutcome? outcome)
    {
        var result = TeslaBleService.ClassifyChargeState(new DtoBleCommandResult { Success = false, Outcome = outcome, });

        Assert.Null(result);
    }

    [Fact]
    public void CarThatWasNotHeardIsReportedAsNotFound()
    {
        var presence = CreatePresence(heard: false);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void WarmingUpScanNeverDeclaresACarAway()
    {
        var presence = CreatePresence(heard: false);
        presence.WarmingUp = true;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void HeardCarIsNarrowedDownFurther()
    {
        var presence = CreatePresence(heard: true);

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Null(result);
    }

    [Fact]
    public void StoppedScannerCarriesNoPresenceInformation()
    {
        var presence = CreatePresence(heard: false);
        presence.ScannerRunning = false;

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void UnreachableContainerIsNoStatementAboutTheCar()
    {
        var presence = CreatePresence(heard: false);
        presence.ErrorMessage = "container not reachable";

        var result = TeslaBleService.ClassifyPresence(presence, TestVin);

        Assert.Equal(BleConnectionTestResultType.ContainerProblem, result);
    }

    [Fact]
    public void PresentCarRejectingTheBodyControllerReadMeansMissingKey()
    {
        var bodyControllerState = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.KeyNotPaired,
            ResultMessage = "vehicle rejected request: your public key has not been paired with the vehicle",
        };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.KeyNotPaired, result);
    }

    /// <summary>
    /// The body controller answers without any key, so its failure is a link problem and blaming the key would send
    /// the user pairing a key that is already there.
    /// </summary>
    [Fact]
    public void BodyControllerFailingOnTheLinkIsNotBlamedOnTheKey()
    {
        var bodyControllerState = new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.LinkFailed,
            ResultMessage = "failed to read body controller state: the car hung up",
        };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.Unknown, result);
    }

    [Fact]
    public void CarThatLeftBetweenTheRequestsIsNotBlamedOnTheKey()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = false, Outcome = BleCommandOutcome.CarAbsent, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarNotFound, result);
    }

    [Fact]
    public void AnsweringBodyControllerOfASleepingCarMeansAsleep()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: false);

        Assert.Equal(BleConnectionTestResultType.CarAsleep, result);
    }

    [Fact]
    public void AwakeCarWithFailedChargeStateStaysUnknown()
    {
        var bodyControllerState = new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, };

        var result = TeslaBleService.ClassifyBodyControllerState(bodyControllerState, isAwake: true);

        Assert.Equal(BleConnectionTestResultType.Unknown, result);
    }

    public static TheoryData<string, string> CommandsWithoutParameters => new()
    {
        { nameof(TeslaBleService.FlashLights), "flash-lights" },
        { nameof(TeslaBleService.StartCharging), "charging-start" },
        { nameof(TeslaBleService.StopCharging), "charging-stop" },
        { nameof(TeslaBleService.GetBodyControllerState), "body-controller-state" },
    };

    [Theory]
    [MemberData(nameof(CommandsWithoutParameters))]
    public async Task CommandWithoutParametersIsSentToTheContainerOfTheCar(string methodName, string expectedCommand)
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, Outcome = BleCommandOutcome.Ok, });
        var service = Mock.Create<TeslaBleService>();

        var result = await InvokeCommand(service, methodName);

        Assert.True(result.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://ble-container:7210/api/" + BleApiRoutes.ExecuteCommand, request.Uri.GetLeftPart(UriPartial.Path));
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal(TestVin, query[BleApiRoutes.VinQueryParam]);
        Assert.Equal(expectedCommand, query[BleApiRoutes.CommandQueryParam]);
        Assert.Null(query[BleApiRoutes.DomainQueryParam]);
        Assert.Null(query[BleApiRoutes.AdapterQueryParam]);
        Assert.Null(query[BleApiRoutes.KeepWarmSecondsQueryParam]);
        Assert.Equal("[]", request.Body);
    }

    [Fact]
    public async Task CommandUsesTheAdapterPinnedToTheCar()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, }, adapter: "AA:BB:CC:DD:EE:FF");
        var service = Mock.Create<TeslaBleService>();

        await service.FlashLights(TestVin);

        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).Uri.Query);
        Assert.Equal("AA:BB:CC:DD:EE:FF", query[BleApiRoutes.AdapterQueryParam]);
    }

    [Fact]
    public async Task CommandPassesTheRefusalOfTheCarOn()
    {
        SetupContainer(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.CarRefused,
            CarErrorMessage = "is_charging",
        });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.StartCharging(TestVin);

        Assert.False(result.Success);
        Assert.Equal(BleCommandOutcome.CarRefused, result.Outcome);
        Assert.Equal("is_charging", result.CarErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CommandWithoutBleUrlIsAConfigurationError(string? bleApiBaseUrl)
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, }, bleApiBaseUrl: bleApiBaseUrl);
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        //The url is a per car setting, so sending the user to the base configuration would send them looking for a
        //setting that is not there.
        Assert.Contains("car", result.ResultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base configuration", result.ResultMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CommandOnAFailingContainerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom"),
        }));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task CommandOnAnUnreachableContainerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => throw new HttpRequestException("No route to host")));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
        Assert.Equal("No route to host", result.ResultMessage);
    }

    [Fact]
    public async Task CommandOnAnUnparsableAnswerIsUnsuccessful()
    {
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null"),
        }));
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights(TestVin);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task CommandForAnUnknownCarIsAConfigurationErrorNamingTheVin()
    {
        //A VIN TSC does not know used to throw out of the service, which reached the user as a bare
        //"Sequence contains no matching element" and told them nothing about what to do.
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights("UNKNOWNVIN1234567");

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Contains("UNKNOWNVIN1234567", result.ResultMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ConnectionTestForAnUnknownCarIsReportedAsContainerProblemInsteadOfThrowing()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.TestConnection("UNKNOWNVIN1234567");

        //Not Success, and nothing is asked of the container: the car is not known, so there is nothing to ask.
        Assert.NotEqual(BleConnectionTestResultType.Success, result.ResultType);
        Assert.Contains("UNKNOWNVIN1234567", result.ErrorDetails);
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// Measured on a real car: the charge state is rejected with the car's "public key has not been paired" message.
    /// That is the whole answer, so the test must not ask anything else and must not end up claiming the key works.
    /// </summary>
    [Fact]
    public async Task ConnectionTestReportsTheCarsKeyRejectionWithoutAskingAnythingElse()
    {
        var handler = SetupContainer(new DtoBleCommandResult
        {
            Success = false,
            Outcome = BleCommandOutcome.KeyNotPaired,
            Phase = BleCommandPhase.Command,
            ResultMessage = "vehicle rejected request: your public key has not been paired with the vehicle",
        });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.TestConnection(TestVin);

        Assert.Equal(BleConnectionTestResultType.KeyNotPaired, result.ResultType);
        Assert.Contains("paired", result.ErrorDetails);
        Assert.Single(handler.Requests);
    }

    /// <summary>
    /// Pairing only sends the request; the key is added when the user taps a key card on the center console. The
    /// answer used to be overwritten with "not successful", so every successful request was reported as a failure.
    /// </summary>
    [Fact]
    public async Task PairKeyReportsASentRequestAsSuccess()
    {
        var handler = SetupContainer(new DtoBleCommandResult
        {
            Success = true,
            ResultMessage = $"Sent add-key request to {TestVin}. Confirm by tapping NFC card on center console.",
        });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.PairKey(TestVin, "charging_manager");

        Assert.True(result.Success);
        Assert.Contains("Confirm by tapping NFC card", result.ResultMessage);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://ble-container:7210/api/" + BleApiRoutes.PairCar, request.Uri.GetLeftPart(UriPartial.Path));
        var query = HttpUtility.ParseQueryString(request.Uri.Query);
        Assert.Equal(TestVin, query[BleApiRoutes.VinQueryParam]);
        Assert.Equal("charging_manager", query[BleApiRoutes.ApiRoleQueryParam]);
    }

    [Fact]
    public async Task PairKeyPassesAFailedRequestOn()
    {
        SetupContainer(new DtoBleCommandResult
        {
            Success = false,
            ResultMessage = "failed to connect: context deadline exceeded",
        });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.PairKey(TestVin, "charging_manager");

        Assert.False(result.Success);
        Assert.Contains("failed to connect", result.ResultMessage);
    }

    [Fact]
    public async Task PairKeyForAnUnknownCarIsAConfigurationErrorNamingTheVin()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.PairKey("UNKNOWNVIN1234567", "charging_manager");

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Contains("UNKNOWNVIN1234567", result.ResultMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CommandForACarWhoseVinIsSpelledDifferentlyStillReachesIt()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.FlashLights(TestVin.ToLowerInvariant());

        Assert.True(result.Success);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PresenceForAnUnknownCarNamesTheVin()
    {
        SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.GetPresenceForVin("UNKNOWNVIN1234567");

        Assert.Contains("UNKNOWNVIN1234567", result.ErrorMessage);
    }

    [Fact]
    public async Task SetAmpForAnUnknownCarIsAConfigurationErrorInsteadOfThrowing()
    {
        var handler = SetupContainer(new DtoBleCommandResult { Success = true, });
        var service = Mock.Create<TeslaBleService>();

        var result = await service.SetAmp("UNKNOWNVIN1234567", 8);

        Assert.False(result.Success);
        Assert.Equal(ErrorType.TscConfiguration, result.ErrorType);
        Assert.Empty(handler.Requests);
    }

    private static Task<DtoBleCommandResult> InvokeCommand(TeslaBleService service, string methodName) => methodName switch
    {
        nameof(TeslaBleService.FlashLights) => service.FlashLights(TestVin),
        nameof(TeslaBleService.StartCharging) => service.StartCharging(TestVin),
        nameof(TeslaBleService.StopCharging) => service.StopCharging(TestVin),
        nameof(TeslaBleService.GetBodyControllerState) => service.GetBodyControllerState(TestVin),
        _ => throw new ArgumentOutOfRangeException(nameof(methodName), methodName, null),
    };

    private CapturingHandler SetupContainer(DtoBleCommandResult containerResult,
        string? bleApiBaseUrl = "http://ble-container:7210", string? adapter = null) =>
        SetupContainer(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(containerResult)),
        }), bleApiBaseUrl, adapter);

    private CapturingHandler SetupContainer(CapturingHandler handler,
        string? bleApiBaseUrl = "http://ble-container:7210", string? adapter = null)
    {
        var car = new DtoCar
        {
            Vin = TestVin,
            UseBle = true,
            BleApiBaseUrl = bleApiBaseUrl,
            BleAdapterAddress = adapter,
        };
        Mock.Mock<ISettings>().Setup(s => s.Cars).Returns(new List<DtoCar> { car, });
        Mock.Mock<IHttpClientFactory>().Setup(f => f.CreateClient(StaticConstants.HttpClientNameBle))
            .Returns(() => new HttpClient(handler, disposeHandler: false));
        return handler;
    }

    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? Body);

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, body));
            return respond(request);
        }
    }

    private static DtoBlePresenceResult CreatePresence(bool heard) => new()
    {
        ScannerRunning = true,
        Vehicles = new List<DtoBlePresenceVehicle>
        {
            new() { Vin = TestVin, Heard = heard, },
        },
    };
}
