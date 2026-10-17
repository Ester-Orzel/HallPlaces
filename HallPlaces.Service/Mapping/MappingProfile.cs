using AutoMapper;
using HallPlaces.Core.DTOs;
using HallPlaces.Core.Entities;

namespace HallPlaces.Service.Mapping;

// All the Entity <-> DTO conversions in one place.
// AutoMapper maps properties with the same name automatically;
// here we only describe what is NOT automatic.
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Category, CategoryDto>();
        CreateMap<CategoryRequest, Category>();

        CreateMap<Hall, HallDto>();
        CreateMap<HallRequest, Hall>();

        // HallName is filled automatically from Hall.Name ("flattening").
        CreateMap<Event, EventDto>();
        // Only title/description/date/hall are copied. Seats and categories are built by the Service.
        CreateMap<EventRequest, Event>();

        // Enum -> string (Available / Booked) is automatic.
        CreateMap<Seat, SeatDto>();

        CreateMap<Booking, BookingDto>()
            .ForMember(d => d.EventId, o => o.MapFrom(b => b.Seat.EventId))
            .ForMember(d => d.EventTitle, o => o.MapFrom(b => b.Seat.Event.Title))
            .ForMember(d => d.EventStartsAt, o => o.MapFrom(b => b.Seat.Event.StartsAt))
            .ForMember(d => d.RowNumber, o => o.MapFrom(b => b.Seat.RowNumber))
            .ForMember(d => d.SeatNumber, o => o.MapFrom(b => b.Seat.SeatNumber))
            .ForMember(d => d.Price, o => o.MapFrom(b => b.Seat.Price))
            .ForMember(d => d.UserFullName, o => o.MapFrom(b => b.User.FullName));
    }
}
