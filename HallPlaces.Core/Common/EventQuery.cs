using HallPlaces.Core.Enums;

namespace HallPlaces.Core.Common;

// Filtering, sorting and paging options for the events list.
// Filled from the query string: /api/events?page=2&search=...&categoryId=3&sortBy=Title
public class EventQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool UpcomingOnly { get; set; } = true;
    public EventSortBy SortBy { get; set; } = EventSortBy.Date;
    public bool Descending { get; set; }
}
