using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using HomeGroup.API.Data;
using HomeGroup.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeGroup.API.Services;

public class GoogleCalendarSyncService(AppDbContext db, IConfiguration config)
{
    public async Task<int> SyncAsync()
    {
        var calendarId = config["Google:CalendarId"]
            ?? throw new InvalidOperationException("Google:CalendarId is not configured");

        var serviceAccountJson = config["Google:ServiceAccountJson"]
            ?? throw new InvalidOperationException("Google:ServiceAccountJson is not configured");

        var credential = GoogleCredential
            .FromJson(serviceAccountJson)
            .CreateScoped(CalendarService.Scope.CalendarReadonly);

        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "HomeGroup CRM",
        });

        var allEvents = new List<Event>();
        string? pageToken = null;
        do
        {
            var listRequest = service.Events.List(calendarId);
            listRequest.TimeMinDateTimeOffset = DateTimeOffset.UtcNow.AddMonths(-1);
            listRequest.TimeMaxDateTimeOffset = DateTimeOffset.UtcNow.AddMonths(6);
            listRequest.SingleEvents = true;
            listRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            listRequest.MaxResults = 2500;
            if (pageToken != null) listRequest.PageToken = pageToken;

            var response = await listRequest.ExecuteAsync();
            if (response.Items != null) allEvents.AddRange(response.Items);
            pageToken = response.NextPageToken;
        }
        while (pageToken != null);

        var existing = await db.CalendarEvents
            .Where(e => e.Type == CalendarEventType.Google)
            .ToListAsync();

        var existingByGoogleId = existing
            .Where(e => e.GoogleEventId != null)
            .ToDictionary(e => e.GoogleEventId!);

        var seenIds = new HashSet<string>();
        var syncedCount = 0;

        foreach (var ge in allEvents)
        {
            if (string.IsNullOrEmpty(ge.Id)) continue;
            seenIds.Add(ge.Id);

            var (date, startTime, endTime) = ParseEventTimes(ge);
            if (!date.HasValue) continue;

            if (existingByGoogleId.TryGetValue(ge.Id, out var existingEvt))
            {
                existingEvt.Title = ge.Summary ?? "Без назви";
                existingEvt.Description = ge.Description;
                existingEvt.Location = ge.Location;
                existingEvt.Date = date;
                existingEvt.StartTime = startTime;
                existingEvt.EndTime = endTime;
                existingEvt.IsRecurring = false;
            }
            else
            {
                db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = ge.Summary ?? "Без назви",
                    Description = ge.Description,
                    Location = ge.Location,
                    Type = CalendarEventType.Google,
                    IsRecurring = false,
                    Date = date,
                    StartTime = startTime,
                    EndTime = endTime,
                    GoogleEventId = ge.Id,
                });
            }
            syncedCount++;
        }

        foreach (var evt in existing)
        {
            if (evt.GoogleEventId != null && !seenIds.Contains(evt.GoogleEventId))
                db.CalendarEvents.Remove(evt);
        }

        await db.SaveChangesAsync();
        return syncedCount;
    }

    private static (DateOnly? date, TimeOnly? startTime, TimeOnly? endTime) ParseEventTimes(Event ge)
    {
        DateOnly? date = null;
        TimeOnly? startTime = null;
        TimeOnly? endTime = null;

        if (!string.IsNullOrEmpty(ge.Start?.DateTimeRaw))
        {
            if (DateTimeOffset.TryParse(ge.Start.DateTimeRaw, out var dto))
            {
                date = DateOnly.FromDateTime(dto.DateTime);
                startTime = TimeOnly.FromDateTime(dto.DateTime);
            }
        }
        else if (!string.IsNullOrEmpty(ge.Start?.Date))
        {
            date = DateOnly.Parse(ge.Start.Date);
        }

        if (!string.IsNullOrEmpty(ge.End?.DateTimeRaw))
        {
            if (DateTimeOffset.TryParse(ge.End.DateTimeRaw, out var dto))
                endTime = TimeOnly.FromDateTime(dto.DateTime);
        }

        return (date, startTime, endTime);
    }
}
