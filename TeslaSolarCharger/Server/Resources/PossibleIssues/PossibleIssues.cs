using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Resources.PossibleIssues;

public class PossibleIssues(IIssueKeys issueKeys) : IPossibleIssues
{
    private readonly Dictionary<string, DtoIssue> _issues = new()
    {
        { issueKeys.VersionNotUpToDate, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Warning,
                IsTelegramEnabled = false,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.NoFleetApiToken, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = false,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.FleetApiTokenUnauthorized, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.FleetApiTokenMissingScopes, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.FleetApiTokenRequestExpired, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = false,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.CrashedOnStartup, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.RestartNeeded, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = false,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.GetVehicle, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 15,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.GetVehicleData, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 10,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.CarStateUnknown, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.FleetApiNonSuccessStatusCode, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = true,
            }
        },
        { issueKeys.FleetApiNonSuccessResult, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = true,
            }
        },
        { issueKeys.UnsignedCommand, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.FleetApiTokenRefreshNonSuccessStatusCode, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.BleCommandNoSuccess, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = true,
            }
        },
        { issueKeys.SolarValuesNotAvailable, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.UsingFleetApiAsBleFallback, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Warning,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = false,
            }
        },
        { issueKeys.BleVersionCompatibility, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.NoBackendApiToken, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.BackendTokenUnauthorized, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 1,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.FleetApiTokenExpired, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = false,
                HideOccurrenceCount = true,
            }
        },
        { issueKeys.Solar4CarSideFleetApiNonSuccessStatusCode, new DtoIssue
            {
                IssueSeverity = IssueSeverity.Error,
                IsTelegramEnabled = true,
                ShowErrorAfterOccurrences = 2,
                HasPlaceHolderIssueKey = true,
                HideOccurrenceCount = true,
            }
        },
    };

    public DtoIssue GetIssueByKey(string key)
    {
        return _issues[GetKeyToSearchFor(key)];
    }

    private string GetKeyToSearchFor(string key)
    {
        if (key.Contains('_'))
        {
            var keyToSearchFor = key.Split('_')[0] + "_";
            return keyToSearchFor;
        }
        return key;
    }
}
