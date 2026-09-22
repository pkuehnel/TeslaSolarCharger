using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using TeslaSolarCharger.Shared.Dtos.Https;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class HttpsController(IHttpsCertificateService httpsCertificateService, IServer server) : ApiBaseController
{
    public const string RootCertificateFileName = "Solar4Car-root.cer";

    /// <summary>
    /// The root certificate to install on devices. Served inline, so iOS offers to install it and Firefox to import
    /// it, while other browsers save it under the given file name.
    /// </summary>
    [HttpGet]
    public IActionResult RootCertificate()
    {
        Response.Headers.ContentDisposition = $"inline; filename=\"{RootCertificateFileName}\"";
        return File(httpsCertificateService.GetRootCertificateDer(), "application/x-x509-ca-cert");
    }

    [HttpGet]
    public DtoHttpsInformation GetHttpsInformation() =>
        httpsCertificateService.GetHttpsInformation(server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [],
            Request.Host.Host);
}
