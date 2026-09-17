using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Shared.Dtos;

public class DtoBackendLogin()
{
    [Display(Name = "EMail")]
    public string? EMail { get; set; }
    [Display(Name = "Password")]
    public string? Password { get; set; }
}
