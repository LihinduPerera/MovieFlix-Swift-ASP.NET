using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Tests;

/// <summary>
/// Focused unit tests for the TMDB status-code translation that the
/// integration fake cannot exercise (it bypasses the real HTTP client).
/// </summary>
public sealed class TmdbServiceTests
{
    [Fact]
    public async Task TooManyRequests_UsesRetryAfterDelta()
    {
        using var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(
                HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter =
                new RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return response;
        });

        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<TmdbRateLimitedException>(
            () => service.GetDetailsAsync(MediaType.Movie, 550, "en-US"));

        Assert.Equal(42, exception.RetryAfterSeconds);
    }

    [Fact]
    public async Task TooManyRequests_UsesRetryAfterDate()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        using var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(
                HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter =
                new RetryConditionHeaderValue(now.AddSeconds(15));
            return response;
        });

        var service = CreateService(handler, new FixedTimeProvider(now));

        var exception = await Assert.ThrowsAsync<TmdbRateLimitedException>(
            () => service.GetDetailsAsync(MediaType.Movie, 550, "en-US"));

        Assert.Equal(15, exception.RetryAfterSeconds);
    }

    [Fact]
    public async Task ServerError_MapsToBadGateway()
    {
        using var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<TmdbServiceException>(
            () => service.GetDetailsAsync(MediaType.Movie, 550, "en-US"));
    }

    [Fact]
    public async Task Timeout_MapsToUnavailable()
    {
        using var handler = new StubHandler(_ => throw new TaskCanceledException());

        var service = CreateService(handler);

        await Assert.ThrowsAsync<TmdbUnavailableException>(
            () => service.GetDetailsAsync(MediaType.Movie, 550, "en-US"));
    }

    [Fact]
    public async Task NotFound_MapsToNotFound()
    {
        using var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = CreateService(handler);

        await Assert.ThrowsAsync<TmdbNotFoundException>(
            () => service.GetDetailsAsync(MediaType.Movie, 550, "en-US"));
    }

    private static TmdbService CreateService(
        HttpMessageHandler handler,
        TimeProvider? timeProvider = null)
    {
        return new TmdbService(
            new HttpClient(handler),
            Options.Create(new TmdbSettings
            {
                BaseUrl = "https://tmdb.test/",
                AccessToken = "test-token"
            }),
            timeProvider ?? TimeProvider.System);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
