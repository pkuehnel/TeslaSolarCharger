using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Scheduling;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class JobRegistrationTests : TestBase
{
    private const string JobNamespace = "TeslaSolarCharger.Server.Scheduling.Jobs";

    public JobRegistrationTests(ITestOutputHelper outputHelper)
        : base(outputHelper)
    {
    }

    /// <summary>
    /// Quartz's job factory resolves jobs from the service provider. It still builds a job that is not registered, but
    /// only a registration makes the job's lifetime and its dependencies part of the container's configuration.
    /// </summary>
    [Fact]
    public void AllJobsAreRegisteredForDependencyInjection()
    {
        var jobTypes = GetJobTypes();
        Assert.NotEmpty(jobTypes);
        var services = new ServiceCollection().AddMyDependencies();
        var registeredTypes = services.Select(s => s.ServiceType).ToHashSet();
        var unregisteredJobTypes = jobTypes.Where(t => !registeredTypes.Contains(t)).Select(t => t.Name).ToList();
        Assert.True(unregisteredJobTypes.Count == 0,
            $"Add the following jobs to {nameof(ServiceCollectionExtensions.AddMyDependencies)}: {string.Join(", ", unregisteredJobTypes)}");
    }

    private static List<Type> GetJobTypes()
    {
        return typeof(JobManager).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, }
                        && t.Namespace == JobNamespace
                        && typeof(IJob).IsAssignableFrom(t))
            .ToList();
    }
}
