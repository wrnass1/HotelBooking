using AutoMapper;
using HotelBooking.Models.DTO;
using HotelBooking.Models.Entities;

namespace HotelBooking.Services;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Hotel mappings
        CreateMap<Hotel, HotelDto>();
        CreateMap<CreateHotelDto, Hotel>();
        CreateMap<UpdateHotelDto, Hotel>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Room mappings
        CreateMap<Room, RoomDto>()
            .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Hotel.Name));
        CreateMap<CreateRoomDto, Room>();
        CreateMap<UpdateRoomDto, Room>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Booking mappings
        CreateMap<Booking, BookingDto>()
            .ForMember(dest => dest.RoomNumber, opt => opt.MapFrom(src => src.Room.RoomNumber))
            .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Room.Hotel.Name));
        CreateMap<CreateBookingDto, Booking>();
        CreateMap<UpdateBookingDto, Booking>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Facility mappings
        CreateMap<Facility, FacilityDto>();
        CreateMap<CreateFacilityDto, Facility>();
        CreateMap<UpdateFacilityDto, Facility>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Amenity mappings
        CreateMap<Amenity, AmenityDto>();
        CreateMap<CreateAmenityDto, Amenity>();
        CreateMap<UpdateAmenityDto, Amenity>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // ApiKey mappings
        CreateMap<ApiKey, ApiKeyDto>();
        CreateMap<CreateApiKeyDto, ApiKey>();

        // User mappings
        CreateMap<User, UserDto>();
    }
}
