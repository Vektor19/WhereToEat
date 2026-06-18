using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace WhereToEat.AdminApi.Integration;

/// <summary>
/// Admin CRUD round-trips over the script-created schema via the EF database-first store (Step 10).
/// Each test seeds rows with Dapper (the SQL schema-of-record), edits them through the admin API (EF),
/// and reads them back with Dapper — proving EF and the scripts share one schema. Covers: edit a menu
/// item (price/weight/dish + admin provenance), set DoNotParse, set DoNotUpdate (upsert), the
/// real-photo permission gate (invariant #8), and the unknown-row 404 path.
/// </summary>
[Collection(AdminApiTestGroup.Name)]
public sealed class AdminCrudTests
{
    private readonly AdminApiFixture _fixture;

    public AdminCrudTests(AdminApiFixture fixture) => _fixture = fixture;

    private HttpClient AdminClient()
    {
        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.Admin());
        return client;
    }

    [Fact]
    public async Task EditMenuItem_UpdatesPriceWeightDish_AndMarksAdminSourced()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Фаст-фуд");
        var dishA = await seeder.AddDishAsync(categoryId, "Піца Маргарита");
        var dishB = await seeder.AddDishAsync(categoryId, "Піца Пепероні");
        var restaurantId = await seeder.AddRestaurantAsync("Pizza Edit", "вул. Едит, 1");
        var menuItemId = await seeder.AddMenuItemAsync(restaurantId, dishA, 150m, "UAH", "400 г", source: 0);

        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/menu-items/{menuItemId}", UriKind.Relative),
            new { dishId = dishB, priceAmount = 199.50m, priceCurrency = "UAH", weight = "450 г" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var row = await seeder.GetMenuItemAsync(menuItemId);
        row.Should().NotBeNull();
        row!.DishId.Should().Be(dishB);
        row.PriceAmount.Should().Be(199.50m);
        row.Weight.Should().Be("450 г");
        row.Source.Should().Be(1); // Admin provenance (invariant #3).
    }

    [Fact]
    public async Task EditMenuItem_UnknownItem_Returns404()
    {
        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/menu-items/{Guid.NewGuid()}", UriKind.Relative),
            new { dishId = Guid.NewGuid(), priceAmount = 1m, priceCurrency = "UAH", weight = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetMenuItemDoNotParse_PersistsTheFlag()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Перші страви");
        var dishId = await seeder.AddDishAsync(categoryId, "Борщ");
        var restaurantId = await seeder.AddRestaurantAsync("Borsch DoNotParse", "вул. Перша, 2");
        var menuItemId = await seeder.AddMenuItemAsync(restaurantId, dishId, 90m, "UAH");

        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/menu-items/{menuItemId}/do-not-parse", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var row = await seeder.GetMenuItemAsync(menuItemId);
        row!.DoNotParse.Should().BeTrue();
    }

    [Fact]
    public async Task SetRestaurantDoNotUpdate_UpsertsTheProtectionRow()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var restaurantId = await seeder.AddRestaurantAsync("DoNotUpdate Diner", "вул. Друга, 3");

        // No protection row yet — the upsert creates one.
        (await seeder.GetDoNotUpdateAsync(restaurantId)).Should().BeNull();

        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/restaurants/{restaurantId}/do-not-update", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await seeder.GetDoNotUpdateAsync(restaurantId)).Should().BeTrue();

        // Toggling again updates the same row (still a single upsert).
        await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/restaurants/{restaurantId}/do-not-update", UriKind.Relative),
            new { value = false });
        (await seeder.GetDoNotUpdateAsync(restaurantId)).Should().BeFalse();
    }

    [Fact]
    public async Task ManageRealPhoto_FlipsThePermissionGate_ForRealPhotos()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Десерти");
        var dishId = await seeder.AddDishAsync(categoryId, "Тірамісу");
        var realPhotoId = await seeder.AddPhotoAsync(dishId, isGeneric: false, permissionGranted: false, "https://x/real.jpg");

        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/photos/{realPhotoId}/permission", UriKind.Relative),
            new { value = true });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await seeder.GetPhotoPermissionAsync(realPhotoId)).Should().BeTrue();
    }

    [Fact]
    public async Task ManageRealPhoto_RejectsAGenericPhoto()
    {
        var seeder = new CatalogSeeder(_fixture.ConnectionString);
        var categoryId = await seeder.AddCategoryAsync("Напої");
        var dishId = await seeder.AddDishAsync(categoryId, "Кава");
        var genericPhotoId = await seeder.AddPhotoAsync(dishId, isGeneric: true, permissionGranted: false, "https://x/generic.jpg");

        var response = await AdminClient().PutAsJsonAsync(
            new Uri($"/admin/photos/{genericPhotoId}/permission", UriKind.Relative),
            new { value = true });

        // Our own generic content is never permission-gated (invariant #8) -> a 400 validation failure.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await seeder.GetPhotoPermissionAsync(genericPhotoId)).Should().BeFalse();
    }
}
