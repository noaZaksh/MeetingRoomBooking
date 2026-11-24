namespace MeetingRooms.Api.Models;

public sealed record Room(int Id, string Name);

public sealed record Booking(int Id, int RoomId, string Title, DateTime Start, DateTime End, string BookedBy);

/// <summary>
/// Body of POST /api/bookings. Fields are nullable so that a missing field reaches
/// our validation (with a clear message) instead of silently becoming a default value.
/// Start/End are local wall-clock times (no time zone), e.g. "2026-11-24T09:30:00".
/// </summary>
public sealed record CreateBookingRequest(int? RoomId, string? Title, DateTime? Start, DateTime? End, string? BookedBy);
