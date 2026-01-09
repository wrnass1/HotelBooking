namespace HotelBooking.Auth;

public static class Permissions
{
    public const string HotelsRead = "Hotels:Read";
    public const string HotelsCreate = "Hotels:Create";
    public const string HotelsUpdate = "Hotels:Update";
    public const string HotelsDelete = "Hotels:Delete";

    public const string RoomsRead = "Rooms:Read";
    public const string RoomsCreate = "Rooms:Create";
    public const string RoomsUpdate = "Rooms:Update";
    public const string RoomsDelete = "Rooms:Delete";

    public const string BookingsRead = "Bookings:Read";
    public const string BookingsCreate = "Bookings:Create";
    public const string BookingsUpdate = "Bookings:Update";
    public const string BookingsDelete = "Bookings:Delete";

    public static Dictionary<string, string[]> RolePermissions => new()
    {
        { "Admin", new[] { HotelsRead, HotelsCreate, HotelsUpdate, HotelsDelete, RoomsRead, RoomsCreate, RoomsUpdate, RoomsDelete, BookingsRead, BookingsCreate, BookingsUpdate, BookingsDelete } },
        { "Manager", new[] { HotelsRead, HotelsCreate, HotelsUpdate, RoomsRead, RoomsCreate, RoomsUpdate, BookingsRead, BookingsCreate, BookingsUpdate } },
        { "User", new[] { HotelsRead, RoomsRead, BookingsRead, BookingsCreate } }
    };
}
