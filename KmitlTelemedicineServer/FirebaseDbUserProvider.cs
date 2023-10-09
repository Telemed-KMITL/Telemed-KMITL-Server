using System.Security.Claims;
using Google.Cloud.Firestore;

namespace KmitlTelemedicineServer;

public class FirebaseDbUserProvider
{
    private readonly FirestoreDb _firestore;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ILogger<FirebaseDbUserProvider> _logger;

    private DocumentSnapshot? _data;

    private DocumentReference? _reference;

    public FirebaseDbUserProvider(
        IHttpContextAccessor httpContextAccessor,
        FirestoreDb firestore,
        ILogger<FirebaseDbUserProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _firestore = firestore;
        _logger = logger;
    }

    public DocumentReference Reference => _reference!;

    public DocumentSnapshot Snapshot => _data!;

    public string Id => _data!.Id;

    public Dictionary<string, object> Data => _data!.ToDictionary();

    public async Task<bool> FetchAsync()
    {
        var user = _httpContextAccessor.HttpContext!.User;
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(uid))
        {
            _logger.LogTrace("Failed to get uid");
            return false;
        }

        var documentRef = _firestore.Collection("users").Document(uid);
        _logger.LogTrace("User: {}", documentRef.Path);

        var snapshot = await documentRef.GetSnapshotAsync();

        if (!snapshot.Exists)
        {
            _logger.LogTrace("User \"{}\" is not exist", uid);
            return false;
        }

        _reference = documentRef;
        _data = snapshot;
        return true;
    }
}