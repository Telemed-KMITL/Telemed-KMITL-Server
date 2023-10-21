using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace KmitlTelemedicineServer.Models;

public class CreateUserRequest
{
    [Required] [EmailAddress] public string Email { get; set; }

    [Required] [MinLength(6)] public string Password { get; set; }

    [DefaultValue(false)] public bool? EmailVerified { get; set; }

    [Required] public User User { get; set; }
}