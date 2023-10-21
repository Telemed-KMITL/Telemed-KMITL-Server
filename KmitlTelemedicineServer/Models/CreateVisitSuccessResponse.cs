using System.ComponentModel.DataAnnotations;

namespace KmitlTelemedicineServer.Models;

public class CreateVisitSuccessResponse
{
    [Required] public string UserId { get; set; }

    [Required] public string VisitId { get; set; }

    [Required] public bool Created { get; set; }
}