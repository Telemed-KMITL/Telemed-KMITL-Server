using System.Security.Claims;
using Google.Cloud.Firestore;

namespace KmitlTelemedicineServer;

public class FirebaseUserAccessor
{
    private readonly FirestoreDb _firestore;

    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly ILogger<FirebaseUserAccessor> _logger;

    public FirebaseUserAccessor(
        IHttpContextAccessor httpContextAccessor,
        FirestoreDb firestore,
        ILogger<FirebaseUserAccessor> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _firestore = firestore;
        _logger = logger;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

    public async Task<DocumentSnapshot?> FetchDbUserAsync()
    {
        var uid = UserId;

        if (string.IsNullOrEmpty(uid))
        {
            _logger.LogTrace("Failed to get uid");
            return null;
        }

        var documentRef = _firestore.Collection("users").Document(uid);
        _logger.LogTrace("User: {}", documentRef.Path);

        var snapshot = await documentRef.GetSnapshotAsync();

        if (!snapshot.Exists)
        {
            _logger.LogTrace("User \"{}\" is not exist", uid);
            return null;
        }

        return snapshot;
    }
}