using System.Security.Claims;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace KmitlTelemedicineServer;

public static class ServerApi
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost("/createVisit", CreateVisitAsync)
            .RequireAuthorization()
            .WithName("CreateVisit")
            .Produces<CreateVisitSucessResponse>()
            .Produces<BadRequestBody>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return builder;
    }
    
    static async Task<IResult> CreateVisitAsync(
        FirestoreDb firestore,
        IOptions<ServerConfig> config,
        IHttpContextAccessor httpContextAccessor)
    {
        var jwtUser = httpContextAccessor.HttpContext!.User;
        
        string? uid = jwtUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? email = jwtUser.FindFirst(ClaimTypes.Email)?.Value;
        bool emailVerified = jwtUser.FindFirst("email_verified")?.Value.ToLower() == "true";

        if (string.IsNullOrEmpty(uid))
        {
            return Results.BadRequest(new BadRequestBody(
                $"Failed to get UID"
            ));
        }

        if (!string.IsNullOrEmpty(email) && !emailVerified)
        {
            return Results.BadRequest(new BadRequestBody(
                $"Email is not verified"
            ));
        }
        
        var userRef = firestore.Collection("users").Document(uid);
        var user = await userRef.GetSnapshotAsync();
        if (!user.Exists)
        {
            return Results.BadRequest(new BadRequestBody(
                $"User (UID: {uid}) is not registered"
            ));
        }
    
        var currentDateTime = DateTimeOffset.Now;
        var currentTimeStamp = Timestamp.FromDateTimeOffset(currentDateTime);
    
        var defaultWaitingRoomRef = firestore
            .Collection("waitingRooms")
            .Document(config.Value.DefaultWaitingRoomId);
        var visitId = currentDateTime.ToString(config.Value.VisitIdDateFormat);
    
        var newVisitRef = userRef
            .Collection("visits")
            .Document(visitId);
        var newWaitingUserRef = defaultWaitingRoomRef
            .Collection("waitingUsers")
            .Document();
    
        var batch = firestore.StartBatch();
        {
            batch.Create(newVisitRef, new Visit
            {
                CreatedAt = currentTimeStamp,
                JitsiRoomName = null,
                Status = VisitStatus.Created,
            });
            batch.Create(newWaitingUserRef, new WaitingUser
            {
                CreatedAt = currentTimeStamp,
                UpdatedAt = currentTimeStamp,
                UserId = uid,
                VisitId = visitId,
                User = user.ToDictionary(),
                Status = WaitingUserStatus.Waiting,
                JitsiRoomName = null,
            });
        }
        await batch.CommitAsync();
    
        return Results.Ok(new CreateVisitSucessResponse(
            UserId: uid,
            VisitId: visitId
        ));
    }
}

public record BadRequestBody(string Message, string Status = "BadRequest");

public record CreateVisitSucessResponse(string UserId, string VisitId, string Status = "Success");
