using System.Net;
using System.Net.Http.Json;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Media;
using MovieFlix.Api.DTOs.Ratings;
using MovieFlix.Api.DTOs.Reviews;

namespace MovieFlix.Api.Tests;

public sealed class RatingsIntegrationTests : IntegrationTestBase
{
    public RatingsIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Rating_CreateUpdateAndRemove_RoundTrips()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8201, "Rated Film", "/rated.jpg");

        var create = await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/8201",
            new { rating = 4 });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await ReadAsync<MediaRatingResponse>(create);
        Assert.Equal(4, created.Rating);
        Assert.Equal("Rated Film", created.Media.Title);

        var update = await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/8201",
            new { rating = 5 });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<MediaRatingResponse>(update);
        Assert.Equal(5, updated.Rating);

        var mine = await ReadAsync<MediaRatingResponse>(
            await client.GetAsync("/api/v1/me/ratings/movie/8201"));
        Assert.Equal(5, mine.Rating);

        var remove = await client.DeleteAsync(
            "/api/v1/me/ratings/movie/8201");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        var missing = await client.GetAsync(
            "/api/v1/me/ratings/movie/8201");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var removeAgain = await client.DeleteAsync(
            "/api/v1/me/ratings/movie/8201");
        Assert.Equal(HttpStatusCode.NoContent, removeAgain.StatusCode);
    }

    [Fact]
    public async Task CommunityRating_AggregatesAndExposesUserRating()
    {
        var first = await CreateAuthenticatedClientAsync();
        using var firstClient = first.Client;
        var second = await CreateAuthenticatedClientAsync();
        using var secondClient = second.Client;

        Factory.Tmdb.AddMovie(8202, "Community Film", "/community.jpg");

        Assert.Equal(
            HttpStatusCode.OK,
            (await firstClient.PutAsJsonAsync(
                "/api/v1/me/ratings/movie/8202",
                new { rating = 5 })).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await secondClient.PutAsJsonAsync(
                "/api/v1/me/ratings/movie/8202",
                new { rating = 3 })).StatusCode);

        using var anonymous = Factory.CreateClient();
        var publicResponse = await anonymous.GetAsync(
            "/api/v1/media/movie/8202/ratings");

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var community = await ReadAsync<CommunityRatingResponse>(publicResponse);
        Assert.Equal(4.0, community.AverageRating);
        Assert.Equal(2, community.RatingCount);
        Assert.Null(community.UserRating);

        var authenticated = await ReadAsync<CommunityRatingResponse>(
            await firstClient.GetAsync("/api/v1/media/movie/8202/ratings"));

        Assert.Equal(5, authenticated.UserRating);
        Assert.Equal(2, authenticated.RatingCount);
    }

    [Fact]
    public async Task Rating_ValidatesRangeAndTitleExistence()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var tooLow = await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/550",
            new { rating = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, tooLow.StatusCode);
        AssertValidationError(await ReadProblemAsync(tooLow), "Rating");

        var tooHigh = await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/550",
            new { rating = 6 });

        Assert.Equal(HttpStatusCode.BadRequest, tooHigh.StatusCode);

        var unknownTitle = await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/8299",
            new { rating = 4 });

        Assert.Equal(HttpStatusCode.NotFound, unknownTitle.StatusCode);

        var unknownCommunity = await client.GetAsync(
            "/api/v1/media/movie/8299/ratings");

        Assert.Equal(HttpStatusCode.NotFound, unknownCommunity.StatusCode);
    }

    [Fact]
    public async Task State_IncludesRatingAndReview()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8203, "Stateful Film", "/stateful.jpg");

        await client.PutAsJsonAsync(
            "/api/v1/me/ratings/movie/8203",
            new { rating = 4 });

        var reviewResponse = await client.PostAsJsonAsync(
            "/api/v1/media/movie/8203/reviews",
            new { content = "A stateful review." });

        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);

        var state = await ReadAsync<MediaStateResponse>(
            await client.GetAsync("/api/v1/media/movie/8203/state"));

        Assert.Equal(4, state.UserRating);
        Assert.True(state.HasReview);
        Assert.NotNull(state.ReviewId);
    }
}

public sealed class ReviewsIntegrationTests : IntegrationTestBase
{
    public ReviewsIntegrationTests(MovieFlixApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Review_CanBeCreatedListedAndViewedAnonymously()
    {
        var auth = await CreateAuthenticatedClientAsync(
            displayName: "Review Author");
        using var client = auth.Client;
        Factory.Tmdb.AddMovie(8301, "Reviewed Film", "/reviewed.jpg");

        var create = await client.PostAsJsonAsync(
            "/api/v1/media/movie/8301/reviews",
            new { content = "  A great film.  " });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await ReadAsync<ReviewResponse>(create);
        Assert.Equal(
            $"/api/v1/reviews/{created.Id}",
            create.Headers.Location?.OriginalString);

        Assert.Equal("A great film.", created.Content);
        Assert.Equal("Review Author", created.AuthorDisplayName);
        Assert.True(created.IsMine);
        Assert.Equal("Reviewed Film", created.Media.Title);
        Assert.True(created.CreatedAt > DateTime.MinValue);

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/media/movie/8301/reviews",
            new { content = "Second review." });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await ReadProblemAsync(duplicate);

        using var anonymous = Factory.CreateClient();
        var publicResponse = await anonymous.GetAsync(
            "/api/v1/media/movie/8301/reviews");

        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var publicPage = await ReadAsync<PagedResponse<ReviewResponse>>(
            publicResponse);
        Assert.Equal(1, publicPage.TotalCount);
        Assert.False(publicPage.Items[0].IsMine);

        var mine = await ReadAsync<PagedResponse<ReviewResponse>>(
            await client.GetAsync("/api/v1/me/reviews"));
        Assert.Equal(1, mine.TotalCount);
        Assert.True(mine.Items[0].IsMine);
    }

    [Fact]
    public async Task Review_UpdateAndDelete_EnforceOwnership()
    {
        var owner = await CreateAuthenticatedClientAsync();
        using var ownerClient = owner.Client;
        var other = await CreateAuthenticatedClientAsync();
        using var otherClient = other.Client;

        Factory.Tmdb.AddMovie(8302, "Owned Review Film", "/owned.jpg");

        var create = await ownerClient.PostAsJsonAsync(
            "/api/v1/media/movie/8302/reviews",
            new { content = "Original." });

        var created = await ReadAsync<ReviewResponse>(create);

        var forbiddenUpdate = await otherClient.PutAsJsonAsync(
            $"/api/v1/reviews/{created.Id}",
            new { content = "Hijacked." });

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);

        var update = await ownerClient.PutAsJsonAsync(
            $"/api/v1/reviews/{created.Id}",
            new { content = "Updated." });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<ReviewResponse>(update);
        Assert.Equal("Updated.", updated.Content);

        var forbiddenDelete = await otherClient.DeleteAsync(
            $"/api/v1/reviews/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        var delete = await ownerClient.DeleteAsync(
            $"/api/v1/reviews/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var missing = await ownerClient.DeleteAsync(
            $"/api/v1/reviews/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Review_ValidationAndMissingTitle()
    {
        var auth = await CreateAuthenticatedClientAsync();
        using var client = auth.Client;

        var empty = await client.PostAsJsonAsync(
            "/api/v1/media/movie/550/reviews",
            new { content = "" });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        AssertValidationError(await ReadProblemAsync(empty), "Content");

        var missingTitle = await client.PostAsJsonAsync(
            "/api/v1/media/movie/8399/reviews",
            new { content = "No such film." });

        Assert.Equal(HttpStatusCode.NotFound, missingTitle.StatusCode);

        var missingReviews = await client.GetAsync(
            "/api/v1/media/movie/8399/reviews");
        Assert.Equal(HttpStatusCode.NotFound, missingReviews.StatusCode);

        var invalidId = await client.PutAsJsonAsync(
            "/api/v1/reviews/999999",
            new { content = "Nothing." });
        Assert.Equal(HttpStatusCode.NotFound, invalidId.StatusCode);
    }

    [Fact]
    public async Task ReviewEndpoints_RequireAuthentication()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me/reviews");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
