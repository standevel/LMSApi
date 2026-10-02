using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LMS.Api.Contracts;
using LMS.Api.Data;
using LMS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Api.Endpoints.Cafeteria;

// ─── GET Available Menu Items ──────────────────────────────────────────────────

public sealed class GetAvailableMenuItemsEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<MenuItemDto>>
{
    public override void Configure()
    {
        Get("cafeteria/menu/GetAvailableMenuItems");
        AllowAnonymous();
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var vendorId = Query<string?>("vendorId", isRequired: false)
                       ?? User.FindFirst("vendorId")?.Value;

        try
        {
            await SeedDefaultsIfEmpty(db, ct);
        }
        catch
        {
            // Ignore seeding errors in production when table already has concurrent writes
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.CafeteriaMenuItems
            .Where(m => m.IsAvailable);

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Where(m => m.VendorId == vendorId);
        }

        var items = await query.OrderBy(m => m.FeedingTimeId).ThenBy(m => m.Name).ToListAsync(ct);
        await SendSuccessAsync(items.Select(m => MapDto(m)).ToList(), ct);
    }

    private static MenuItemDto MapDto(CafeteriaMenuItem m) => new(
        m.Id, m.VendorId, m.Name, m.Description, m.ImageUrl,
        m.Price, m.FeedingTimeId, m.FeedingTimeName,
        m.IsAvailable, m.AvailableDate, m.CreatedAt, m.UpdatedAt,
        m.Allergens, m.DietaryFlags, m.Calories);

    internal static async Task SeedDefaultsIfEmpty(LmsDbContext db, CancellationToken ct)
    {
        var hasAny = await db.CafeteriaMenuItems.AnyAsync(ct);
        if (hasAny) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var defaults = new[]
        {
            new CafeteriaMenuItem { VendorId = "1", Name = "Jollof Rice & Fried Plantain", FeedingTimeId = 1, FeedingTimeName = "Breakfast", Price = 2500m, IsAvailable = true, AvailableDate = today, DietaryFlags = "Halal, Vegetarian", Allergens = "None", Calories = 550 },
            new CafeteriaMenuItem { VendorId = "1", Name = "Stewed Beans & Fresh Bread",    FeedingTimeId = 2, FeedingTimeName = "Lunch",    Price = 3500m, IsAvailable = true, AvailableDate = today, DietaryFlags = "Vegetarian", Allergens = "Gluten", Calories = 620 },
            new CafeteriaMenuItem { VendorId = "1", Name = "Vegetable Soup & Pounded Yam",  FeedingTimeId = 3, FeedingTimeName = "Dinner",   Price = 4500m, IsAvailable = true, AvailableDate = today, DietaryFlags = "Halal, Gluten-Free", Allergens = "Fish", Calories = 750 }
        };

        db.CafeteriaMenuItems.AddRange(defaults);
        await db.SaveChangesAsync(ct);
    }
}

public sealed class GetAvailableMenuItemsFallbackEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<MenuItemDto>>
{
    public override void Configure()
    {
        // Alternate route the Angular client falls back to.
        Get("cafeteria/menuitems/GetAvailableMenuItems");
        AllowAnonymous();
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var vendorId = Query<string?>("vendorId", isRequired: false)
                       ?? User.FindFirst("vendorId")?.Value;

        try
        {
            await GetAvailableMenuItemsEndpoint.SeedDefaultsIfEmpty(db, ct);
        }
        catch
        {
            // Ignore concurrent seeding collision
        }

        var query = db.CafeteriaMenuItems
            .Where(m => m.IsAvailable);

        if (!string.IsNullOrWhiteSpace(vendorId))
        {
            query = query.Where(m => m.VendorId == vendorId);
        }

        var items = await query.OrderBy(m => m.FeedingTimeId).ThenBy(m => m.Name).ToListAsync(ct);

        await SendSuccessAsync(items.Select(m => new MenuItemDto(
            m.Id, m.VendorId, m.Name, m.Description, m.ImageUrl,
            m.Price, m.FeedingTimeId, m.FeedingTimeName,
            m.IsAvailable, m.AvailableDate, m.CreatedAt, m.UpdatedAt,
            m.Allergens, m.DietaryFlags, m.Calories)).ToList(), ct);
    }
}

// ─── GET Single Menu Item ─────────────────────────────────────────────────────

public sealed class GetMenuItemByIdEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<MenuItemDto?>
{
    public override void Configure()
    {
        Get("cafeteria/menu/GetMenuItemById/{id}");
        AllowAnonymous();
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idStr = Route<string>("id");
        if (!Guid.TryParse(idStr, out var id))
        {
            await SendFailureAsync(400, "Invalid menu item ID.", "INVALID_ID", "Menu item ID must be a valid GUID.", ct);
            return;
        }

        var item = await db.CafeteriaMenuItems.FindAsync([id], ct);
        if (item is null)
        {
            await SendFailureAsync(404, "Menu item not found.", "NOT_FOUND", "Menu item not found.", ct);
            return;
        }

        await SendSuccessAsync(new MenuItemDto(
            item.Id, item.VendorId, item.Name, item.Description, item.ImageUrl,
            item.Price, item.FeedingTimeId, item.FeedingTimeName,
            item.IsAvailable, item.AvailableDate, item.CreatedAt, item.UpdatedAt,
            item.Allergens, item.DietaryFlags, item.Calories), ct);
    }
}

// ─── GET Feeding Times ────────────────────────────────────────────────────────

public sealed class GetFeedingTimesEndpoint : ApiEndpointWithoutRequest<List<FeedingTimeDto>>
{
    public override void Configure()
    {
        Get("cafeteria/menu/GetFeedingTimes");
        AllowAnonymous();
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var times = new[]
        {
            new FeedingTimeDto(1, "Breakfast", "07:00", "10:00"),
            new FeedingTimeDto(2, "Lunch",     "12:00", "15:30"),
            new FeedingTimeDto(3, "Dinner",    "18:00", "21:00")
        };

        await SendSuccessAsync(times.ToList(), ct);
    }
}

// ─── POST Save Favorite Meal ──────────────────────────────────────────────────

public sealed class SaveFavoriteMealEndpoint(LmsDbContext db)
    : ApiEndpoint<SaveFavoriteMealRequest, bool>
{
    public override void Configure()
    {
        Post("cafeteria/menu/SaveFavoriteMeal");
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(SaveFavoriteMealRequest req, CancellationToken ct)
    {
        var username = User.Identity?.Name
                       ?? User.FindFirst("name")?.Value
                       ?? User.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            await SendFailureAsync(401, "Authenticated user required.", "UNAUTHORIZED", "Login is required.", ct);
            return;
        }

        var menuItemId = string.IsNullOrWhiteSpace(req.MenuItemId) ? (req.MenuItemId ?? string.Empty) : req.MenuItemId;
        if (!Guid.TryParse(menuItemId, out var menuItemGuid))
        {
            // Frontend may send arbitrary ids; accept by name lookup instead.
            var existing = await db.CafeteriaMenuItems
                .Where(m => m.VendorId == req.VendorId)
                .Where(m => string.Equals(m.Name, req.MenuItemId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                await SendSuccessAsync(false, ct);
                return;
            }
            menuItemGuid = existing.Id;
        }

        var existingFav = await db.CafeteriaMenuItemFavorites
            .FirstOrDefaultAsync(f => f.Username == username && f.MenuItemId == menuItemGuid, ct);

        if (existingFav is null)
        {
            db.CafeteriaMenuItemFavorites.Add(new CafeteriaMenuItemFavorite
            {
                Username = username,
                MenuItemId = menuItemGuid
            });
        }
        else if (existingFav.DeletedAt.HasValue)
        {
            existingFav.DeletedAt = null;
        }

        await db.SaveChangesAsync(ct);
        await SendSuccessAsync(true, ct);
    }
}

public sealed record SaveFavoriteMealRequest(string? MenuItemId, string? VendorId);

// ─── GET Favorite Meals ────────────────────────────────────────────────────────

public sealed class GetFavoriteMealsEndpoint(LmsDbContext db)
    : ApiEndpointWithoutRequest<List<MenuItemDto>>
{
    public override void Configure()
    {
        Get("cafeteria/menu/GetFavoriteMeals");
        Tags("CafeteriaMenu");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var username = User.Identity?.Name
                       ?? User.FindFirst("name")?.Value
                       ?? User.FindFirst("preferred_username")?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            await SendSuccessAsync(new List<MenuItemDto>(), ct);
            return;
        }

        var items = await db.CafeteriaMenuItemFavorites
            .Where(f => f.Username == username && f.DeletedAt == null)
            .Select(f => f.MenuItem)
            .ToListAsync(ct);

        await SendSuccessAsync(items.Select(m => new MenuItemDto(
            m.Id, m.VendorId, m.Name, m.Description, m.ImageUrl,
            m.Price, m.FeedingTimeId, m.FeedingTimeName,
            m.IsAvailable, m.AvailableDate, m.CreatedAt, m.UpdatedAt,
            m.Allergens, m.DietaryFlags, m.Calories)).ToList(), ct);
    }
}
