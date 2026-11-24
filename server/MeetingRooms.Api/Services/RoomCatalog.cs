using MeetingRooms.Api.Models;

namespace MeetingRooms.Api.Services;

/// <summary>Fixed list of rooms. Rooms are not editable in this exercise.</summary>
public sealed class RoomCatalog
{
    private readonly IReadOnlyList<Room> _rooms;

    public RoomCatalog(IEnumerable<Room> rooms)
    {
        _rooms = rooms.ToList();
    }

    public static readonly IReadOnlyList<Room> DefaultRooms =
    [
        new(1, "Room A (4 people)"),
        new(2, "Room B (8 people)"),
        new(3, "Board Room (16 people)"),
    ];

    public IReadOnlyList<Room> GetAll() => _rooms;

    public bool Exists(int roomId) => _rooms.Any(r => r.Id == roomId);
}
