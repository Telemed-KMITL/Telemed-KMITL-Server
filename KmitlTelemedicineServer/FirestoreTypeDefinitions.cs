using Google.Cloud.Firestore;

namespace KmitlTelemedicineServer;

[FirestoreData(ConverterType = typeof(FirestoreEnumNameConverter<VisitStatus>))]
public enum VisitStatus
{
    Created,
    Ready,
    Finished,
    Unknown
}

[FirestoreData]
class Visit
{
    [FirestoreProperty("status")] public VisitStatus Status { get; set; }

    [FirestoreProperty("jitsiRoomName")] public string? JitsiRoomName { get; set; }

    [FirestoreProperty("createdAt")] public Timestamp CreatedAt { get; set; }
}

[FirestoreData(ConverterType = typeof(FirestoreEnumNameConverter<WaitingUserStatus>))]
public enum WaitingUserStatus
{
    Waiting,
    OnCall,
    WaitingAgain,
    Finished,
    Unknown
}

[FirestoreData]
class WaitingUser
{
    [FirestoreProperty("userId")] public string UserId { get; set; }

    [FirestoreProperty("visitId")] public string VisitId { get; set; }

    [FirestoreProperty("user")] public Dictionary<string, object> User { get; set; }

    [FirestoreProperty("status")] public WaitingUserStatus Status { get; set; }

    [FirestoreProperty("jitsiRoomName")] public string? JitsiRoomName { get; set; }

    [FirestoreProperty("createdAt")]
    [FirestoreDocumentCreateTimestamp]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    [FirestoreDocumentUpdateTimestamp]
    public Timestamp UpdatedAt { get; set; }
}
