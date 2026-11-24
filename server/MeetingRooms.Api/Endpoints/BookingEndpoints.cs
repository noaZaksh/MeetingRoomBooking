using MeetingRooms.Api.Models;
using MeetingRooms.Api.Services;

namespace MeetingRooms.Api.Endpoints;

/// <summary>
/// HTTP layer only: maps requests to BookingService and results to status codes.
/// All errors are returned as ProblemDetails (RFC 7807): { title, detail, status, errors? }.
/// </summary>
public static class BookingEndpoints
{
    public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/rooms", (RoomCatalog rooms) => rooms.GetAll());

        api.MapGet("/bookings", GetBookings);
        api.MapPost("/bookings", CreateBooking);
        api.MapDelete("/bookings/{id:int}", CancelBooking);
    }

    // GET /api/bookings?roomId=1&date=2026-11-24
    private static IResult GetBookings(int? roomId, string? date, RoomCatalog rooms, BookingService bookings)
    {
        var errors = new Dictionary<string, string[]>();
        if (roomId is null)
            errors["roomId"] = ["Query parameter 'roomId' is required."];

        DateOnly day = default;
        if (string.IsNullOrWhiteSpace(date))
            errors["date"] = ["Query parameter 'date' is required."];
        else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out day))
            errors["date"] = ["Query parameter 'date' must be in the format yyyy-MM-dd."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors, title: "Invalid request.");

        if (!rooms.Exists(roomId!.Value))
            return RoomNotFound(roomId.Value);

        return Results.Ok(bookings.GetForDay(roomId.Value, day));
    }

    // POST /api/bookings
    private static IResult CreateBooking(CreateBookingRequest request, BookingService bookings)
    {
        var result = bookings.Create(request);

        return result.Status switch
        {
            CreateBookingStatus.Created =>
                Results.Created($"/api/bookings/{result.Booking!.Id}", result.Booking),

            CreateBookingStatus.Invalid =>
                Results.ValidationProblem(result.Errors!, title: "The booking is not valid."),

            CreateBookingStatus.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The room is already booked at this time.",
                detail: $"Overlaps with \"{result.ConflictsWith!.Title}\" " +
                        $"({result.ConflictsWith.Start:HH:mm}-{result.ConflictsWith.End:HH:mm}, " +
                        $"booked by {result.ConflictsWith.BookedBy})."),

            _ => throw new InvalidOperationException($"Unhandled status {result.Status}"),
        };
    }

    // DELETE /api/bookings/5
    private static IResult CancelBooking(int id, BookingService bookings)
        => bookings.Cancel(id)
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status404NotFound,
                title: "Booking not found.",
                detail: $"No booking with id {id}. It may have been cancelled already.");

    private static IResult RoomNotFound(int roomId)
        => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Room not found.",
            detail: $"No room with id {roomId}.");
}
