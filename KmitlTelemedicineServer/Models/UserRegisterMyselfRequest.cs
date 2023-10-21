using System.ComponentModel.DataAnnotations;

namespace KmitlTelemedicineServer.Models;

public class UserRegisterMyselfRequest
{
    [Required] [MinLength(1)] public string FirstName { get; set; }

    [Required] [MinLength(1)] public string LastName { get; set; }
}