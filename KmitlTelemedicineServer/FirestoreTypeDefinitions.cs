using Google.Cloud.Firestore;

namespace KmitlTelemedicineServer;

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