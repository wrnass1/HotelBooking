using FluentAssertions;
using HotelBooking.Data;
using HotelBooking.Models.Entities;
using HotelBooking.Models.DTO;
using HotelBooking.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelBooking.Tests.Repositories;

public class HotelRepositoryTests : IDisposable
{
    private readonly HotelBookingDbContext _context;
    private readonly HotelBookingReadDbContext _readContext;
    private readonly HotelRepository _repository;

    public HotelRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<HotelBookingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HotelBookingDbContext(options);
        var readOptions = new DbContextOptionsBuilder<HotelBookingReadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _readContext = new HotelBookingReadDbContext(readOptions);
        _repository = new HotelRepository(_context, _readContext);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateHotel()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };

        // Act
        var result = await _repository.CreateAsync(hotel);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Test Hotel");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnHotel_WhenExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.GetByIdAsync(hotel.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Hotel");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateHotel()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);
        hotel.Name = "Updated Hotel";

        // Act
        var result = await _repository.UpdateAsync(hotel);

        // Assert
        result.Name.Should().Be("Updated Hotel");
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnTrue_WhenHotelExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.DeleteAsync(hotel.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _repository.GetByIdAsync(hotel.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenHotelNotExists()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenHotelExists()
    {
        // Arrange
        var hotel = new Hotel
        {
            Name = "Test Hotel",
            Address = "Test Address",
            City = "Test City",
            Country = "Test Country",
            StarRating = 5
        };
        await _repository.CreateAsync(hotel);

        // Act
        var result = await _repository.ExistsAsync(hotel.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenHotelNotExists()
    {
        // Act
        var result = await _repository.ExistsAsync(999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReadReplica_WhileWritesAndPointReadsUsePrimary()
    {
        // Distinct stores reproduce a standby that has not replayed a new hotel yet.
        var hotel = await _repository.CreateAsync(new Hotel
        {
            Name = "Primary only", Address = "Address", City = "City",
            Country = "Country", StarRating = 3
        });

        var catalog = await _repository.GetPagedAsync(new HotelQueryDto());
        catalog.Total.Should().Be(0);
        catalog.Items.Should().BeEmpty();
        (await _repository.GetByIdAsync(hotel.Id)).Should().NotBeNull();
        (await _repository.ExistsAsync(hotel.Id)).Should().BeTrue();

        hotel.Name = "Updated on primary";
        await _repository.UpdateAsync(hotel);
        (await _repository.GetByIdAsync(hotel.Id))!.Name.Should().Be("Updated on primary");
        (await _repository.GetPagedAsync(new HotelQueryDto())).Total.Should().Be(0);
        (await _repository.DeleteAsync(hotel.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ReadContext_ShouldRejectSaveChanges()
    {
        Action save = () => _readContext.SaveChanges();
        save.Should().Throw<InvalidOperationException>();
        Func<Task> saveAsync = () => _readContext.SaveChangesAsync();
        await saveAsync.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _readContext.Dispose();
        _context.Dispose();
    }
}
