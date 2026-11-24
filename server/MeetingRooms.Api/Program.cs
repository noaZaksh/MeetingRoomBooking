using MeetingRooms.Api.Endpoints;
using MeetingRooms.Api.Services;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// In-memory data: singletons live for the lifetime of the process.
builder.Services.AddSingleton(new RoomCatalog(RoomCatalog.DefaultRooms));
builder.Services.AddSingleton<BookingService>();

builder.Services.AddProblemDetails();

// Make malformed requests (bad JSON, wrong types) throw so we can return a clear 400 below,
// instead of an empty 400 response.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isBadRequest = error is BadHttpRequestException;

    context.Response.StatusCode = isBadRequest ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    await Results.Problem(
        statusCode: context.Response.StatusCode,
        title: isBadRequest ? "Malformed request." : "Unexpected server error.",
        detail: isBadRequest
            ? "The request body could not be read. Check that it is valid JSON and that dates look like 2026-11-24T09:30:00."
            : "Something went wrong on the server. Please try again.")
        .ExecuteAsync(context);
}));

app.MapBookingEndpoints();

app.Run();

// Lets a test project reference the entry point (e.g. WebApplicationFactory<Program>).
public partial class Program;
