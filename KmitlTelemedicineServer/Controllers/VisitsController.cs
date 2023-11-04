using System.ComponentModel;
using Google.Cloud.Firestore;
using KmitlTelemedicineServer.Models;
using KmitlTelemedicineServer.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KmitlTelemedicineServer.Controllers;

[ApiController]
[Authorize(Policy = "RequireEmailVerified")]
[Route("[controller]")]
public class VisitsController : ControllerBase
{
    private readonly ServerConfig _config;
    private readonly FirestoreDb _firestore;
    private readonly ILogger<VisitsController> _logger;

    public VisitsController(ILogger<VisitsController> logger, FirestoreDb firestore, ServerConfig config)
    {
        _logger = logger;
        _firestore = firestore;
        _config = config;
    }

    [HttpPost]
    [Authorize(Roles = "patient,admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateVisitSuccessResponse>> Create(
        [FromQuery] [DefaultValue(false)] bool ignoreUnfinishedVisits,
        [FromServices] FirebaseUserAccessor fbUser)
    {
        var userSnapshot = await fbUser.FetchDbUserAsync();
        if (userSnapshot == null) return NotFound("User Record not found");

        if (ignoreUnfinishedVisits is false)
        {
            // If user's visit is not finished, return that visit

            var userVisitNotFinished = await userSnapshot.Reference
                .Collection("visits")
                .WhereEqualTo("isFinished", false)
                .OrderBy("createdAt")
                .Limit(1)
                .GetSnapshotAsync(HttpContext.RequestAborted);
            if (userVisitNotFinished.Any())
                return Ok(new CreateVisitSuccessResponse
                {
                    UserId = userSnapshot.Id,
                    VisitId = userVisitNotFinished.First().Id,
                    Created = false
                });
        }

        // Create new waitingUser/visit

        var currentDateTime = DateTimeOffset.Now;
        var currentTimeStamp = Timestamp.FromDateTimeOffset(currentDateTime);

        var defaultWaitingRoomRef = _firestore
            .Collection("waitingRooms")
            .Document(_config.DefaultWaitingRoomId);
        var newVisitId = currentDateTime.ToString(_config.VisitIdDateFormat);

        var newVisitRef = userSnapshot.Reference
            .Collection("visits")
            .Document(newVisitId);
        var newWaitingUserRef = defaultWaitingRoomRef
            .Collection("waitingUsers")
            .Document();

        var roomName = GenerateRoomName();

        _logger.LogTrace(
            "[CreateVisit] UID: {}, VisitID: {}, WaitingUserID: {}, JitsiRoomName: {}",
            userSnapshot.Id, newVisitId, newWaitingUserRef.Id, roomName);

        var batch = _firestore.StartBatch();
        {
            batch.Create(newVisitRef, new Visit
            {
                CreatedAt = currentTimeStamp,
                JitsiRoomName = roomName,
                CallerIds = new(),
                IsFinished = false
            });
            batch.Create(newWaitingUserRef, new WaitingUser
            {
                CreatedAt = currentTimeStamp,
                UpdatedAt = currentTimeStamp,
                UserId = userSnapshot.Id,
                VisitId = newVisitId,
                User = userSnapshot.ToDictionary(),
                Status = WaitingUserStatus.Waiting,
                JitsiRoomName = roomName
            });
        }
        await batch.CommitAsync(HttpContext.RequestAborted);

        if (HttpContext.RequestAborted.IsCancellationRequested)
        {
            return NoContent();
        }

        _logger.LogTrace(
            "[CreateVisit] Committed: \"{}\", \"{}\"",
            newVisitRef.Path, newWaitingUserRef.Path);

        return Ok(new CreateVisitSuccessResponse
        {
            UserId = userSnapshot.Id,
            VisitId = newVisitId,
            Created = true
        });
    }

    // [HttpPatch("{id}/finish")]
    // public async Task<ActionResult> Finish()
    // { }

    private static string GenerateRoomName()
    {
        return Guid.NewGuid().ToString("N");
    }
}