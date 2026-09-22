using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TeslaSolarCharger.Server.Controllers;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using TeslaSolarCharger.Shared.Dtos.Https;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server.Https;

public class HttpsControllerTests
{
    private readonly Mock<IHttpsCertificateService> _httpsCertificateService = new();
    private readonly FeatureCollection _serverFeatures = new();

    private HttpsController CreateController()
    {
        var server = new Mock<IServer>();
        server.Setup(s => s.Features).Returns(_serverFeatures);
        return new HttpsController(_httpsCertificateService.Object, server.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext(), },
        };
    }

    [Fact]
    public void RootCertificate_ServesTheCertificateSoDevicesOfferToInstallIt()
    {
        var der = new byte[] { 1, 2, 3, };
        _httpsCertificateService.Setup(s => s.GetRootCertificateDer()).Returns(der);
        var controller = CreateController();

        var result = Assert.IsType<FileContentResult>(controller.RootCertificate());

        Assert.Equal(der, result.FileContents);
        Assert.Equal("application/x-x509-ca-cert", result.ContentType);
        //Inline, not as attachment: iOS then offers to install the profile instead of only saving the file
        Assert.Equal($"inline; filename=\"{HttpsController.RootCertificateFileName}\"",
            controller.Response.Headers.ContentDisposition.ToString());
        Assert.True(string.IsNullOrEmpty(result.FileDownloadName));
    }

    [Fact]
    public void GetHttpsInformation_PassesTheAddressesKestrelListensOn()
    {
        var addresses = new ServerAddressesFeature();
        addresses.Addresses.Add("http://[::]:7190");
        addresses.Addresses.Add("https://[::]:7191");
        _serverFeatures.Set<IServerAddressesFeature>(addresses);
        var information = new DtoHttpsInformation { IsEnabled = true, Port = 7191, };
        _httpsCertificateService.Setup(s => s.GetHttpsInformation(It.IsAny<IEnumerable<string>>())).Returns(information);

        var result = CreateController().GetHttpsInformation();

        Assert.Same(information, result);
        _httpsCertificateService.Verify(s => s.GetHttpsInformation(It.Is<IEnumerable<string>>(a =>
            string.Join(";", a) == "http://[::]:7190;https://[::]:7191")));
    }

    [Fact]
    public void GetHttpsInformation_WorksWithoutAddressFeature()
    {
        _httpsCertificateService.Setup(s => s.GetHttpsInformation(It.IsAny<IEnumerable<string>>())).Returns(new DtoHttpsInformation());

        CreateController().GetHttpsInformation();

        _httpsCertificateService.Verify(s => s.GetHttpsInformation(It.Is<IEnumerable<string>>(a => !a.GetEnumerator().MoveNext())));
    }
}
