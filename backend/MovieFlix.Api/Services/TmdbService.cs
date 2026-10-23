using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MovieFlix.Api.Configuration;
using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Exceptions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace MovieFlix.Api.Services
{
    public class TmdbService : ITmdbService
    {
        private readonly HttpClient _httpClient;

        public TmdbService(HttpClient httpClient, IOptions<TmdbSettings> options)
        {
            _httpClient = httpClient;

            var settings = options.Value;

            _httpClient.BaseAddress = new Uri(settings.BaseUrl);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.AccessToken);
        }

        public async Task<TmdbMovieSearchResponse> SearchMoviesAsync(
            string query,
            int page,
            CancellationToken cancellationToken = default)
        {
            var encodedQuery = Uri.EscapeDataString(query);

            var url = $"search/movie?query={encodedQuery}&page={page}";

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if(!response.IsSuccessStatusCode)
                {
                    throw new TmdbServiceException();
                }

                var result = await response.Content
                    .ReadFromJsonAsync<TmdbMovieSearchResponse>(
                    cancellationToken: cancellationToken);

                return result ?? throw new TmdbServiceException();

            } catch (HttpRequestException ex)
            {
                throw new TmdbServiceException(ex);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient timed out, rather than the caller cancelling.
                throw new TmdbServiceException();
            }
            catch (TimeoutRejectedException)
            {
                // The resilience pipeline's attempt or total timeout fired.
                throw new TmdbServiceException();
            }
            catch (BrokenCircuitException)
            {
                // TMDB has been failing consistently; the circuit is open.
                throw new TmdbServiceException();
            }
        }


        public async Task<TmdbMovieDetailsResponse> GetMovieDetailsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            if (tmdbId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tmdbId));
            }

            var url = $"movie/{tmdbId}";

            try
            {
                using var response = await _httpClient.GetAsync(
                    url,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new TmdbServiceException();
                }

                var result = await response.Content
                    .ReadFromJsonAsync<TmdbMovieDetailsResponse>(
                        cancellationToken: cancellationToken);

                return result ?? throw new TmdbServiceException();
            }
            catch (HttpRequestException ex)
            {
                throw new TmdbServiceException(ex);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TmdbServiceException();
            }
            catch (TimeoutRejectedException)
            {
                // The resilience pipeline's attempt or total timeout fired.
                throw new TmdbServiceException();
            }
            catch (BrokenCircuitException)
            {
                // TMDB has been failing consistently; the circuit is open.
                throw new TmdbServiceException();
            }
        }
    }
}
