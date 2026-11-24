using MeetingRooms.Api.Models;

namespace MeetingRooms.Api.Services;

public enum CreateBookingStatus { Created, Invalid, Conflict }

public sealed record CreateBookingResult(
    CreateBookingStatus Status,
    Booking? Booking = null,
    Dictionary<string, string[]>? Errors = null,
    Booking? ConflictsWith = null);

/// <summary>
/// Business rules + in-memory storage for bookings.
/// Registered as a singleton; a lock makes "check for overlap, then insert" atomic,
/// so two concurrent requests cannot book the same slot.
/// </summary>
public sealed class BookingService
{
    public const int MaxTextLength = 100;

    private readonly RoomCatalog _rooms;
    private readonly List<Booking> _bookings = [];
    private readonly object _lock = new();
    private int _nextId = 1;

    public BookingService(RoomCatalog rooms)
    {
        _rooms = rooms;
    }

    /// <summary>Bookings of one room on one day, sorted by start time.</summary>
    public IReadOnlyList<Booking> GetForDay(int roomId, DateOnly date)
    {
        lock (_lock)
        {
            return _bookings
                .Where(b => b.RoomId == roomId && DateOnly.FromDateTime(b.Start) == date)
                .OrderBy(b => b.Start)
                .ToList();
        }
    }

    public CreateBookingResult Create(CreateBookingRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            return new(CreateBookingStatus.Invalid, Errors: errors);

        // After validation these are known to be present.
        var roomId = request.RoomId!.Value;
        var start = request.Start!.Value;
        var end = request.End!.Value;

        lock (_lock)
        {
            var conflict = _bookings.FirstOrDefault(b => b.RoomId == roomId && Overlaps(b.Start, b.End, start, end));
            if (conflict is not null)
                return new(CreateBookingStatus.Conflict, ConflictsWith: conflict);

            var booking = new Booking(_nextId++, roomId, request.Title!.Trim(), start, end, request.BookedBy!.Trim());
            _bookings.Add(booking);
            return new(CreateBookingStatus.Created, Booking: booking);
        }
    }

    /// <returns>true if the booking existed and was removed.</returns>
    public bool Cancel(int id)
    {
        lock (_lock)
        {
            return _bookings.RemoveAll(b => b.Id == id) > 0;
        }
    }

    /// <summary>
    /// Half-open intervals [start, end): touching intervals (10:00 end / 10:00 start) do not overlap.
    /// </summary>
    public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
        => aStart < bEnd && bStart < aEnd;

    /// <summary>Field-level validation. Returns field name -> messages (empty when valid).</summary>
    public Dictionary<string, string[]> Validate(CreateBookingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.RoomId is null)
            errors["roomId"] = ["Room is required."];
        else if (!_rooms.Exists(request.RoomId.Value))
            errors["roomId"] = [$"Room {request.RoomId} does not exist."];

        CheckText(request.Title, "title", "Title", errors);
        CheckText(request.BookedBy, "bookedBy", "Booked by", errors);

        if (request.Start is null)
            errors["start"] = ["Start time is required."];
        if (request.End is null)
            errors["end"] = ["End time is required."];

        if (request.Start is { } start && request.End is { } end)
        {
            if (end <= start)
                errors["end"] = ["End time must be after start time."];
            else if (start.Date != end.Date)
                errors["end"] = ["A booking must start and end on the same day."];
        }

        return errors;
    }

    private static void CheckText(string? value, string key, string label, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[key] = [$"{label} is required."];
        else if (value.Trim().Length > MaxTextLength)
            errors[key] = [$"{label} must be at most {MaxTextLength} characters."];
    }
}
