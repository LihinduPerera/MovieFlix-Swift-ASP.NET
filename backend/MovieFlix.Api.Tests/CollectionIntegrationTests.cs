using System.Net;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Collections;
using MovieFlix.Api.DTOs.Common;

namespace MovieFlix.Api.Tests;

public sealed class CollectionIntegrationTests : IntegrationTestBase
{
    public CollectionIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Collection_Lifecycle_AndPublicDetail()
    {
        var auth = await CreateAuthenticatedClientAsync(
            displayName: "Collector");
        using var client = auth.Client;

        var create = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "  Best Films  ", description = "  My picks.  " });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await ReadAsync<CollectionSummaryResponse>(create);
        Assert.Equal("Best Films", created.Name);
        Assert.Equal("My picks.", created.Description);
        Assert.Equal(0, created.ItemCount);
        Assert.Empty(created.CoverPosterPaths);
        Assert.Equal(
            $"/api/v1/collections/{created.Id}",
            create.Headers.Location?.OriginalString);

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "best films", description = "" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await ReadProblemAsync(duplicate);

        using var anonymous = Factory.CreateClient();
        var publicResponse = await anonymous.GetAsync(
            $"/api/v1/collections/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicDetail = await ReadAsync<CollectionDetailResponse>(publicResponse);
        Assert.Equal("Collector", publicDetail.OwnerDisplayName);
        Assert.False(publicDetail.IsOwner);

        var ownerResponse = await ReadAsync<CollectionDetailResponse>(
            await client.GetAsync($"/api/v1/collections/{created.Id}"));
        Assert.True(ownerResponse.IsOwner);
    }

    [Fact]
    public async Task CollectionItems_AreIdempotent_AndCoversAreCapped()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var create = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "Cover Collection", description = "" });
        var collection = await ReadAsync<CollectionSummaryResponse>(create);

        for (var index = 0; index < 5; index++)
        {
            var tmdbId = 8501 + index;
            Factory.Tmdb.AddMovie(
                tmdbId,
                $"Cover Film {index}",
                $"/cover-{index}.jpg");

            var add = await client.PutAsync(
                $"/api/v1/me/collections/{collection.Id}/items/movie/{tmdbId}",
                null);

            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        var duplicate = await client.PutAsync(
            $"/api/v1/me/collections/{collection.Id}/items/movie/8501",
            null);

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateDetail = await ReadAsync<CollectionDetailResponse>(duplicate);
        Assert.Equal(5, duplicateDetail.ItemCount);

        var detail = await ReadAsync<CollectionDetailResponse>(
            await client.GetAsync($"/api/v1/collections/{collection.Id}"));

        Assert.Equal(5, detail.Items.Count);
        Assert.Equal(5, detail.ItemCount);
        Assert.Equal(4, detail.CoverPosterPaths.Count);
        Assert.All(detail.CoverPosterPaths, path => Assert.StartsWith("/cover-", path));
        Assert.All(detail.Items, item => Assert.NotNull(item.Media.PosterPath));

        var mine = await ReadAsync<PagedResponse<CollectionSummaryResponse>>(
            await client.GetAsync("/api/v1/me/collections"));

        var summary = Assert.Single(mine.Items);
        Assert.Equal(5, summary.ItemCount);
        Assert.Equal(4, summary.CoverPosterPaths.Count);

        var remove = await client.DeleteAsync(
            $"/api/v1/me/collections/{collection.Id}/items/movie/8501");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var removeAgain = await client.DeleteAsync(
            $"/api/v1/me/collections/{collection.Id}/items/movie/8501");
        Assert.Equal(HttpStatusCode.NoContent, removeAgain.StatusCode);

        var afterRemoval = await ReadAsync<CollectionDetailResponse>(
            await client.GetAsync($"/api/v1/collections/{collection.Id}"));
        Assert.Equal(4, afterRemoval.ItemCount);
    }

    [Fact]
    public async Task CollectionWrites_EnforceOwnership()
    {
        var owner = await CreateAuthenticatedClientAsync();
        using var ownerClient = owner.Client;
        var other = await CreateAuthenticatedClientAsync();
        using var otherClient = other.Client;

        var create = await ownerClient.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "Owner Collection", description = "" });
        var collection = await ReadAsync<CollectionSummaryResponse>(create);

        var otherList = await ReadAsync<PagedResponse<CollectionSummaryResponse>>(
            await otherClient.GetAsync("/api/v1/me/collections"));
        Assert.Empty(otherList.Items);

        var forbiddenUpdate = await otherClient.PutAsJsonAsync(
            $"/api/v1/me/collections/{collection.Id}",
            new { name = "Hijacked", description = "" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);

        var forbiddenAdd = await otherClient.PutAsync(
            $"/api/v1/me/collections/{collection.Id}/items/movie/550",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAdd.StatusCode);

        var forbiddenDelete = await otherClient.DeleteAsync(
            $"/api/v1/me/collections/{collection.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        var update = await ownerClient.PutAsJsonAsync(
            $"/api/v1/me/collections/{collection.Id}",
            new { name = "Renamed", description = "Updated" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<CollectionSummaryResponse>(update);
        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Updated", updated.Description);

        var delete = await ownerClient.DeleteAsync(
            $"/api/v1/me/collections/{collection.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await ownerClient.GetAsync(
            $"/api/v1/collections/{collection.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Collection_ValidatesInputAndMediaExistence()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var invalidCreate = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "", description = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        var problem = await ReadProblemAsync(invalidCreate);
        AssertValidationError(problem, "Name");
        AssertValidationError(problem, "Description");

        var create = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "Valid Name", description = "" });
        var collection = await ReadAsync<CollectionSummaryResponse>(create);

        var missingMedia = await client.PutAsync(
            $"/api/v1/me/collections/{collection.Id}/items/movie/8599",
            null);

        Assert.Equal(HttpStatusCode.NotFound, missingMedia.StatusCode);
    }

    [Fact]
    public async Task CollectionEndpoints_RequireAuthentication()
    {
        using var client = Factory.CreateClient();

        var create = await client.PostAsJsonAsync(
            "/api/v1/me/collections",
            new { name = "No auth", description = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }
}
