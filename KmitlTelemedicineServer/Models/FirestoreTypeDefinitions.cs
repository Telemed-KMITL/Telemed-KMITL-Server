using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Google.Cloud.Firestore;
using KmitlTelemedicineServer.Utils;

namespace KmitlTelemedicineServer.Models;

[FirestoreData(ConverterType = typeof(EnumNameConverter<UserStatus>))]
public enum UserStatus
{
    Active,
    InActive
}

[FirestoreData(ConverterType = typeof(EnumNameConverter<UserRole>))]
public enum UserRole
{
    Patient,
    Doctor,
    Nurse,
    Admin
}

[FirestoreData]
public class User
{
    [Required]
    [FirestoreProperty("firstName")]
    public string FirstName { get; set; }

    [Required]
    [FirestoreProperty("lastName")]
    public string LastName { get; set; }

    [FirestoreProperty("HN")]
    [JsonPropertyName("HN")]
    public string? Hn { get; set; }

    [Required]
    [FirestoreProperty("status")]
    public UserStatus Status { get; set; }

    [Required] [FirestoreProperty("role")] public UserRole Role { get; set; }
}

[FirestoreData(ConverterType = typeof(EnumNameConverter<VisitStatus>))]
public enum VisitStatus
{
    Waiting,
    Calling,
    Unknown = Waiting
}

[FirestoreData]
internal class Visit
{
    [FirestoreProperty("status")] public VisitStatus Status { get; set; }

    [FirestoreProperty("isFinished")] public bool IsFinished { get; set; }

    [FirestoreProperty("jitsiRoomName")] public string JitsiRoomName { get; set; }

    [FirestoreProperty("createdAt")] public Timestamp CreatedAt { get; set; }
}

[FirestoreData(ConverterType = typeof(EnumNameConverter<WaitingUserStatus>))]
public enum WaitingUserStatus
{
    Waiting,
    OnCall,
    WaitingAgain,
    Finished,
    Unknown
}

[FirestoreData]
internal class WaitingUser
{
    [FirestoreProperty("userId")] public string UserId { get; set; }

    [FirestoreProperty("visitId")] public string VisitId { get; set; }

    [FirestoreProperty("user")] public Dictionary<string, object> User { get; set; }

    [FirestoreProperty("status")] public WaitingUserStatus Status { get; set; }

    [FirestoreProperty("jitsiRoomName")] public string JitsiRoomName { get; set; }

    [FirestoreProperty("createdAt")] public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")] public Timestamp UpdatedAt { get; set; }
}