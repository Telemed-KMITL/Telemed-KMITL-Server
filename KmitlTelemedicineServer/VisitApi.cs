﻿using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace KmitlTelemedicineServer;

public static class VisitApiExtension
{
    public static IEndpointRouteBuilder MapVisitApiEndpoints(this IEndpointRouteBuilder builder)
    {
        builder
            .MapPost(VisitApi.BasePath + "/create", VisitApi.CreateVisitAsync)
            .WithCommonSettings()
            .WithName("CreateVisit")
            .Produces<CreateVisitSucessResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        builder
            .MapPost(VisitApi.BasePath + "/finish", VisitApi.FinishVisitAsync)
            .WithCommonSettings()
            .WithName("FinishVisit")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return builder;
    }

    private static RouteHandlerBuilder WithCommonSettings(this RouteHandlerBuilder builder)
    {
        return builder.RequireAuthorization("ValidToken");
    }
}

public class VisitApi
{
    public const string BasePath = "/visits";

    private const string FirestoreDocumentIdRegex = @"^(?!\.\.?$)(?!.*__.*__)([^/]{1,1500})$";

    public static async Task<IResult> CreateVisitAsync(
        FirestoreDb firestore,
        FirebaseDbUserProvider userProvider,
        IOptions<ServerConfig> config,
        ILogger<VisitApi> logger)
    {
        if (!await userProvider.FetchAsync()) return Results.BadRequest();

        var currentDateTime = DateTimeOffset.Now;
        var currentTimeStamp = Timestamp.FromDateTimeOffset(currentDateTime);

        var defaultWaitingRoomRef = firestore
            .Collection("waitingRooms")
            .Document(config.Value.DefaultWaitingRoomId);
        var visitId = currentDateTime.ToString(config.Value.VisitIdDateFormat);

        var newVisitRef = userProvider.Reference
            .Collection("visits")
            .Document(visitId);
        var newWaitingUserRef = defaultWaitingRoomRef
            .Collection("waitingUsers")
            .Document();

        var roomName = GenerateRoomName();

        logger.LogTrace(
            "[CreateVisit] UID: {}, VisitID: {}, WaitingUserID: {}, JitsiRoomName: {}",
            userProvider.Id, visitId, newWaitingUserRef.Id, roomName);

        var batch = firestore.StartBatch();
        {
            batch.Create(newVisitRef, new Visit
            {
                CreatedAt = currentTimeStamp,
                JitsiRoomName = roomName,
                Status = VisitStatus.Waiting,
                IsFinished = false
            });
            batch.Create(newWaitingUserRef, new WaitingUser
            {
                CreatedAt = currentTimeStamp,
                UpdatedAt = currentTimeStamp,
                UserId = userProvider.Id,
                VisitId = visitId,
                User = userProvider.Data,
                Status = WaitingUserStatus.Waiting,
                JitsiRoomName = roomName
            });
        }
        await batch.CommitAsync();

        logger.LogTrace(
            "[CreateVisit] Committed: \"{}\", \"{}\"",
            newVisitRef.Path, newWaitingUserRef.Path);

        return Results.Ok(new CreateVisitSucessResponse(
            userProvider.Id,
            visitId
        ));
    }

    public static async Task<IResult> FinishVisitAsync(
        [RegularExpression(FirestoreDocumentIdRegex)]
        string roomId,
        [RegularExpression(FirestoreDocumentIdRegex)]
        string waitingUserId,
        FirestoreDb firestore,
        ILogger<VisitApi> logger)
    {
        var waitingUserSnapshot = await
            firestore.Collection("waitingRooms")
                .Document(roomId)
                .Collection("waitingUsers")
                .Document(waitingUserId)
                .GetSnapshotAsync();
        if (!waitingUserSnapshot.Exists) return Results.BadRequest();

        var waitingUser = waitingUserSnapshot.ConvertTo<WaitingUser>();

        var visitRef =
            firestore.Collection("users")
                .Document(waitingUser.UserId)
                .Collection("visits")
                .Document(waitingUser.VisitId);

        logger.LogTrace(
            "[FinishVisit] UID: {}, VisitID: {}, WaitingRoomID: {}, WaitingUserID: {}",
            waitingUser.UserId, waitingUser.VisitId, roomId, waitingUserId);

        var batch = firestore.StartBatch();
        {
            var updateTime = waitingUserSnapshot.UpdateTime;

            batch.Update(
                visitRef,
                new Dictionary<string, object>
                {
                    { "isFinished", true }
                });
            batch.Update(
                waitingUserSnapshot.Reference,
                new Dictionary<string, object>
                {
                    { "status", WaitingUserStatus.Finished }
                },
                updateTime == null
                    ? Precondition.None
                    : Precondition.LastUpdated(updateTime.Value));
        }
        await batch.CommitAsync();

        logger.LogTrace(
            "[FinishVisit] Committed: \"{}\", \"{}\"",
            visitRef.Path, waitingUserSnapshot.Reference.Path);

        return Results.Ok();
    }

    private static string GenerateRoomName()
    {
        return Guid.NewGuid().ToString("N");
    }
}

public record CreateVisitSucessResponse(string UserId, string VisitId, string Status = "Success");