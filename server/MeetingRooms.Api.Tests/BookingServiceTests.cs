using MeetingRooms.Api.Models;
using MeetingRooms.Api.Services;

namespace MeetingRooms.Api.Tests;

public class BookingServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 24);

    private static BookingService NewService() => new(new RoomCatalog(RoomCatalog.DefaultRooms));

    private static DateTime At(int hour, int minute = 0) => Day.ToDateTime(new TimeOnly(hour, minute));

    private static CreateBookingRequest Request(DateTime start, DateTime end, int roomId = 1,
        string? title = "Standup", string? bookedBy = "Dana")
        => new(roomId, title, start, end, bookedBy);

    // ---------- happy path ----------

    [Fact]
    public void Create_ValidRequest_IsCreatedAndTrimmed()
    {
        var service = NewService();

        var result = service.Create(Request(At(9), At(10), title: "  Standup  ", bookedBy: " Dana "));

        Assert.Equal(CreateBookingStatus.Created, result.Status);
        Assert.Equal("Standup", result.Booking!.Title);
        Assert.Equal("Dana", result.Booking.BookedBy);
    }

    [Fact]
    public void GetForDay_ReturnsOnlyThatRoomAndDay_SortedByStart()
    {
        var service = NewService();
        service.Create(Request(At(14), At(15), title: "Afternoon"));
        service.Create(Request(At(8), At(9), title: "Morning"));
        service.Create(Request(At(11), At(12), title: "Noon"));
        service.Create(Request(At(8), At(9), roomId: 2, title: "Other room"));
        service.Create(Request(At(8).AddDays(1), At(9).AddDays(1), title: "Next day"));

        var titles = service.GetForDay(1, Day).Select(b => b.Title).ToList();

        Assert.Equal(new[] { "Morning", "Noon", "Afternoon" }, titles);
    }

    // ---------- overlap rule ----------

    [Fact]
    public void Create_TouchingBookings_DoNotOverlap()
    {
        var service = NewService();
        service.Create(Request(At(9), At(10)));

        var after = service.Create(Request(At(10), At(11)));   // starts exactly when the first ends
        var before = service.Create(Request(At(8), At(9)));    // ends exactly when the first starts

        Assert.Equal(CreateBookingStatus.Created, after.Status);
        Assert.Equal(CreateBookingStatus.Created, before.Status);
    }

    [Theory]
    [InlineData(9, 30, 10, 30)]   // starts inside
    [InlineData(8, 30, 9, 30)]    // ends inside
    [InlineData(9, 15, 9, 45)]    // fully inside
    [InlineData(8, 0, 11, 0)]     // fully covers
    [InlineData(9, 0, 10, 0)]     // identical
    public void Create_OverlappingBooking_InSameRoom_IsConflict(int sh, int sm, int eh, int em)
    {
        var service = NewService();
        service.Create(Request(At(9), At(10), title: "Existing"));

        var result = service.Create(Request(At(sh, sm), At(eh, em)));

        Assert.Equal(CreateBookingStatus.Conflict, result.Status);
        Assert.Equal("Existing", result.ConflictsWith!.Title);
    }

    [Fact]
    public void Create_SameTime_InDifferentRoom_IsAllowed()
    {
        var service = NewService();
        service.Create(Request(At(9), At(10), roomId: 1));

        var result = service.Create(Request(At(9), At(10), roomId: 2));

        Assert.Equal(CreateBookingStatus.Created, result.Status);
    }

    [Fact]
    public void Cancel_FreesTheSlot_AndSecondCancelReturnsFalse()
    {
        var service = NewService();
        var id = service.Create(Request(At(9), At(10))).Booking!.Id;

        Assert.True(service.Cancel(id));
        Assert.False(service.Cancel(id));
        Assert.Equal(CreateBookingStatus.Created, service.Create(Request(At(9), At(10))).Status);
    }

    // ---------- validation ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingTitleOrBookedBy_IsInvalid(string? value)
    {
        var service = NewService();

        var noTitle = service.Create(Request(At(9), At(10), title: value));
        var noBooker = service.Create(Request(At(9), At(10), bookedBy: value));

        Assert.Equal(CreateBookingStatus.Invalid, noTitle.Status);
        Assert.True(noTitle.Errors!.ContainsKey("title"));
        Assert.Equal(CreateBookingStatus.Invalid, noBooker.Status);
        Assert.True(noBooker.Errors!.ContainsKey("bookedBy"));
    }

    [Theory]
    [InlineData(10, 9)]    // end before start
    [InlineData(10, 10)]   // zero length
    public void Create_EndNotAfterStart_IsInvalid(int startHour, int endHour)
    {
        var result = NewService().Create(Request(At(startHour), At(endHour)));

        Assert.Equal(CreateBookingStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("end"));
    }

    [Fact]
    public void Create_EndingOnAnotherDay_IsInvalid()
    {
        var result = NewService().Create(Request(At(23), At(1).AddDays(1)));

        Assert.Equal(CreateBookingStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("end"));
    }

    [Fact]
    public void Create_UnknownRoom_IsInvalid()
    {
        var result = NewService().Create(Request(At(9), At(10), roomId: 99));

        Assert.Equal(CreateBookingStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("roomId"));
    }

    [Fact]
    public void Create_MissingTimes_IsInvalid()
    {
        var result = NewService().Create(new CreateBookingRequest(1, "Standup", null, null, "Dana"));

        Assert.Equal(CreateBookingStatus.Invalid, result.Status);
        Assert.True(result.Errors!.ContainsKey("start"));
        Assert.True(result.Errors.ContainsKey("end"));
    }

    // ---------- concurrency ----------

    [Fact]
    public void Create_ManyConcurrentRequestsForSameSlot_OnlyOneSucceeds()
    {
        var service = NewService();

        var results = Enumerable.Range(0, 50)
            .AsParallel()
            .Select(_ => service.Create(Request(At(9), At(10))).Status)
            .ToList();

        Assert.Equal(1, results.Count(s => s == CreateBookingStatus.Created));
        Assert.Single(service.GetForDay(1, Day));
    }
}
