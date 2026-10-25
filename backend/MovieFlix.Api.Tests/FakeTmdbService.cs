using MovieFlix.Api.DTOs.Tmdb;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;
using MovieFlix.Api.Services;

namespace MovieFlix.Api.Tests;

/// <summary>
/// Deterministic in-memory TMDB replacement. Tests never touch the network and
/// can assert which language/region values reached the provider.
/// </summary>
public sealed class FakeTmdbService : ITmdbService
{
    public const int FightClubId = 550;
    public const int InceptionId = 27205;
    public const int GameOfThronesId = 1399;
    public const int BreakingBadId = 1396;

    private readonly object _gate = new();
    private readonly Dictionary<int, TmdbMediaDetailsResponse> _movies = [];
    private readonly Dictionary<int, TmdbMediaDetailsResponse> _shows = [];
    private readonly Dictionary<(int ShowId, int Season), TmdbSeasonDetailsResponse> _seasons = [];

    public FakeTmdbService()
    {
        SeedDefaults();
    }

    public int DetailsCallCount { get; private set; }

    public int SearchCallCount { get; private set; }

    public int SeasonCallCount { get; private set; }

    public string? LastLanguage { get; private set; }

    public string? LastRegion { get; private set; }

    public string? LastSearchQuery { get; private set; }

    public SearchType? LastSearchType { get; private set; }

    public MediaTypeFilter? LastTrendingType { get; private set; }

    public TrendingWindow? LastTrendingWindow { get; private set; }

    public MediaFeed? LastFeed { get; private set; }

    public int? LastDiscoverGenreId { get; private set; }

    public int? LastDiscoverYear { get; private set; }

    public double? LastDiscoverMinRating { get; private set; }

    public string? LastDiscoverSortBy { get; private set; }

    /// <summary>When set, the matching details request throws a not-found error.</summary>
    public (MediaType MediaType, int Id)? FailDetails { get; set; }

    /// <summary>When true, the next details request simulates provider downtime.</summary>
    public bool ThrowUnavailableOnDetails { get; set; }

    /// <summary>When true, the next details request simulates TMDB rate limiting.</summary>
    public bool ThrowRateLimitedOnDetails { get; set; }

    /// <summary>When true, the next details request simulates a TMDB server error.</summary>
    public bool ThrowBadGatewayOnDetails { get; set; }

    public void AddMovie(
        int id,
        string title,
        string posterPath = "/poster.jpg",
        string releaseDate = "2020-01-01")
    {
        lock (_gate)
        {
            _movies[id] = new TmdbMediaDetailsResponse
            {
                Id = id,
                Title = title,
                OriginalTitle = title,
                Overview = $"{title} overview.",
                PosterPath = posterPath,
                BackdropPath = $"{posterPath}-backdrop.jpg",
                ReleaseDate = releaseDate,
                VoteAverage = 7.5,
                VoteCount = 100,
                Popularity = 10,
                OriginalLanguage = "en",
                Status = "Released",
                Runtime = 120,
                Tagline = $"{title} tagline",
                Genres = [new TmdbGenre { Id = 18, Name = "Drama" }]
            };
        }
    }

    public TmdbMediaDetailsResponse GetMovieDetails(int id)
    {
        lock (_gate)
        {
            return _movies[id];
        }
    }

    public TmdbMediaDetailsResponse GetShowDetails(int id)
    {
        lock (_gate)
        {
            return _shows[id];
        }
    }

    public Task<TmdbSearchResponse> SearchAsync(
        string query,
        SearchType type,
        int page,
        string language,
        string? region,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            SearchCallCount++;
            LastSearchQuery = query;
            LastSearchType = type;
            LastLanguage = language;
            LastRegion = region;
        }

        if (query.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(EmptyPage(page));
        }

        var results = new List<TmdbSearchResult>();

        if (type is SearchType.Multi or SearchType.Movie)
        {
            results.Add(ToResult(GetMovieDetails(FightClubId), "movie"));
        }

        if (type is SearchType.Multi or SearchType.Tv)
        {
            results.Add(ToResult(GetShowDetails(GameOfThronesId), "tv"));
        }

        if (type == SearchType.Multi)
        {
            results.Add(new TmdbSearchResult
            {
                Id = 999,
                MediaType = "person",
                Name = "A Person",
                Popularity = 1
            });
        }

        return Task.FromResult(new TmdbSearchResponse
        {
            Page = page,
            Results = results,
            TotalPages = 1,
            TotalResults = results.Count
        });
    }

    public Task<TmdbMediaDetailsResponse> GetDetailsAsync(
        MediaType mediaType,
        int tmdbId,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            DetailsCallCount++;
            LastLanguage = language;
        }

        if (ThrowUnavailableOnDetails)
        {
            throw new TmdbUnavailableException();
        }

        if (ThrowRateLimitedOnDetails)
        {
            throw new TmdbRateLimitedException(37);
        }

        if (ThrowBadGatewayOnDetails)
        {
            throw new TmdbServiceException();
        }

        if (FailDetails == (mediaType, tmdbId))
        {
            throw new TmdbNotFoundException();
        }

        var source = mediaType == MediaType.Movie ? _movies : _shows;

        lock (_gate)
        {
            if (source.TryGetValue(tmdbId, out var details))
            {
                return Task.FromResult(details);
            }
        }

        throw new TmdbNotFoundException();
    }

    public Task<TmdbSearchResponse> GetTrendingAsync(
        MediaTypeFilter type,
        TrendingWindow window,
        int page,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            LastTrendingType = type;
            LastTrendingWindow = window;
            LastLanguage = language;
        }

        var results = new List<TmdbSearchResult>();

        if (type is MediaTypeFilter.All or MediaTypeFilter.Movie)
        {
            results.Add(ToResult(GetMovieDetails(FightClubId), "movie"));
        }

        if (type is MediaTypeFilter.All or MediaTypeFilter.Tv)
        {
            results.Add(ToResult(GetShowDetails(GameOfThronesId), "tv"));
        }

        return Task.FromResult(new TmdbSearchResponse
        {
            Page = page,
            Results = results,
            TotalPages = 1,
            TotalResults = results.Count
        });
    }

    public Task<TmdbSearchResponse> GetFeedAsync(
        MediaType mediaType,
        MediaFeed feed,
        int page,
        string language,
        string? region,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            LastFeed = feed;
            LastLanguage = language;
            LastRegion = region;
        }

        var result = mediaType == MediaType.Movie
            ? ToResult(GetMovieDetails(27205), "movie")
            : ToResult(GetShowDetails(GameOfThronesId), "tv");

        var results = new List<TmdbSearchResult> { result };

        return Task.FromResult(new TmdbSearchResponse
        {
            Page = page,
            Results = results,
            TotalPages = 1,
            TotalResults = results.Count
        });
    }

    public Task<TmdbSeasonDetailsResponse> GetSeasonAsync(
        int tvId,
        int seasonNumber,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            SeasonCallCount++;
            LastLanguage = language;

            if (!_shows.ContainsKey(tvId))
            {
                throw new TmdbNotFoundException();
            }

            if (_seasons.TryGetValue((tvId, seasonNumber), out var details))
            {
                return Task.FromResult(details);
            }
        }

        return Task.FromResult(new TmdbSeasonDetailsResponse
        {
            Id = tvId * 1000 + seasonNumber,
            Name = $"Season {seasonNumber}",
            AirDate = "2012-01-01",
            SeasonNumber = seasonNumber,
            Episodes = []
        });
    }

    public Task<List<TmdbGenre>> GetGenresAsync(
        MediaType mediaType,
        string language,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            LastLanguage = language;
        }

        List<TmdbGenre> genres = mediaType == MediaType.Movie
            ? [new TmdbGenre { Id = 18, Name = "Drama" }, new TmdbGenre { Id = 35, Name = "Comedy" }]
            : [new TmdbGenre { Id = 10765, Name = "Sci-Fi & Fantasy" }, new TmdbGenre { Id = 18, Name = "Drama" }];

        return Task.FromResult(genres);
    }

    public Task<TmdbSearchResponse> GetDiscoverAsync(
        MediaType mediaType,
        int? genreId,
        int? year,
        double? minRating,
        string? sortBy,
        int page,
        string language,
        string? region,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            LastDiscoverGenreId = genreId;
            LastDiscoverYear = year;
            LastDiscoverMinRating = minRating;
            LastDiscoverSortBy = sortBy;
            LastLanguage = language;
            LastRegion = region;
        }

        var result = mediaType == MediaType.Movie
            ? ToResult(GetMovieDetails(InceptionId), "movie")
            : ToResult(GetShowDetails(BreakingBadId), "tv");

        var results = new List<TmdbSearchResult> { result };

        return Task.FromResult(new TmdbSearchResponse
        {
            Page = page,
            Results = results,
            TotalPages = 1,
            TotalResults = results.Count
        });
    }

    private void SeedDefaults()
    {
        _movies[FightClubId] = new TmdbMediaDetailsResponse
        {
            Id = FightClubId,
            Title = "Fight Club",
            OriginalTitle = "Fight Club",
            Overview = "An insomniac office worker forms an underground fight club.",
            PosterPath = "/fight-club.jpg",
            BackdropPath = "/fight-club-backdrop.jpg",
            ReleaseDate = "1999-10-15",
            VoteAverage = 8.4,
            VoteCount = 26000,
            Popularity = 60,
            OriginalLanguage = "en",
            Status = "Released",
            Runtime = 139,
            Tagline = "Mischief. Mayhem. Soap.",
            Genres =
            [
                new TmdbGenre { Id = 18, Name = "Drama" },
                new TmdbGenre { Id = 53, Name = "Thriller" }
            ],
            Credits = new TmdbCredits
            {
                Cast =
                [
                    new TmdbCastMember
                    {
                        Id = 819,
                        Name = "Edward Norton",
                        Character = "The Narrator",
                        Order = 0
                    },
                    new TmdbCastMember
                    {
                        Id = 287,
                        Name = "Brad Pitt",
                        Character = "Tyler Durden",
                        Order = 1
                    }
                ],
                Crew =
                [
                    new TmdbCrewMember
                    {
                        Id = 7467,
                        Name = "David Fincher",
                        Job = "Director",
                        Department = "Directing"
                    }
                ]
            },
            Videos = new TmdbVideos
            {
                Results =
                [
                    new TmdbVideo
                    {
                        Id = "video-1",
                        Key = "BdJKm16Co6M",
                        Name = "Fight Club trailer",
                        Site = "YouTube",
                        Type = "Trailer",
                        Official = true,
                        Language = "en"
                    }
                ]
            },
            Similar = new TmdbSearchResponse
            {
                Results =
                [
                    ToResult(_movies.TryGetValue(InceptionId, out var inception)
                        ? inception
                        : new TmdbMediaDetailsResponse
                        {
                            Id = InceptionId,
                            Title = "Inception",
                            Name = "Inception"
                        }, "movie")
                ],
                TotalResults = 1,
                TotalPages = 1
            }
        };

        _movies[InceptionId] = new TmdbMediaDetailsResponse
        {
            Id = InceptionId,
            Title = "Inception",
            OriginalTitle = "Inception",
            Overview = "A thief enters dreams to steal secrets.",
            PosterPath = "/inception.jpg",
            BackdropPath = "/inception-backdrop.jpg",
            ReleaseDate = "2010-07-16",
            VoteAverage = 8.3,
            VoteCount = 34000,
            Popularity = 80,
            OriginalLanguage = "en",
            Status = "Released",
            Runtime = 148,
            Genres =
            [
                new TmdbGenre { Id = 28, Name = "Action" },
                new TmdbGenre { Id = 878, Name = "Science Fiction" }
            ]
        };

        _shows[GameOfThronesId] = new TmdbMediaDetailsResponse
        {
            Id = GameOfThronesId,
            Name = "Game of Thrones",
            OriginalName = "Game of Thrones",
            Overview = "Noble families vie for the Iron Throne.",
            PosterPath = "/got.jpg",
            BackdropPath = "/got-backdrop.jpg",
            FirstAirDate = "2011-04-17",
            LastAirDate = "2019-05-19",
            VoteAverage = 8.4,
            VoteCount = 22000,
            Popularity = 300,
            OriginalLanguage = "en",
            Status = "Ended",
            NumberOfSeasons = 2,
            NumberOfEpisodes = 4,
            Tagline = "Winter is coming.",
            Genres =
            [
                new TmdbGenre { Id = 18, Name = "Drama" },
                new TmdbGenre { Id = 10765, Name = "Sci-Fi & Fantasy" }
            ],
            Seasons =
            [
                new TmdbSeasonSummary
                {
                    Id = 1,
                    Name = "Specials",
                    AirDate = "2010-01-01",
                    SeasonNumber = 0,
                    EpisodeCount = 1
                },
                new TmdbSeasonSummary
                {
                    Id = 2,
                    Name = "Season 1",
                    AirDate = "2011-04-17",
                    SeasonNumber = 1,
                    EpisodeCount = 2
                },
                new TmdbSeasonSummary
                {
                    Id = 3,
                    Name = "Season 2",
                    AirDate = "2012-04-01",
                    SeasonNumber = 2,
                    EpisodeCount = 2
                }
            ],
            Credits = new TmdbCredits
            {
                Cast =
                [
                    new TmdbCastMember
                    {
                        Id = 22970,
                        Name = "Peter Dinklage",
                        Character = "Tyrion Lannister",
                        Order = 0
                    }
                ]
            },
            Similar = new TmdbSearchResponse
            {
                Results =
                [
                    // Cannot call ToResult(_shows[GameOfThronesId]) here: this
                    // initializer is still assigning that dictionary slot.
                    new TmdbSearchResult
                    {
                        Id = GameOfThronesId,
                        MediaType = "tv",
                        Name = "Game of Thrones",
                        OriginalName = "Game of Thrones",
                        Overview = "Noble families vie for the Iron Throne.",
                        PosterPath = "/got.jpg",
                        BackdropPath = "/got-backdrop.jpg",
                        FirstAirDate = "2011-04-17",
                        VoteAverage = 8.4,
                        VoteCount = 22000,
                        Popularity = 300,
                        OriginalLanguage = "en"
                    },
                    ToResult(_movies[FightClubId], "movie")
                ],
                TotalResults = 2,
                TotalPages = 1
            }
        };

        _shows[BreakingBadId] = new TmdbMediaDetailsResponse
        {
            Id = BreakingBadId,
            Name = "Breaking Bad",
            OriginalName = "Breaking Bad",
            Overview = "A chemistry teacher turns to crime.",
            PosterPath = "/breaking-bad.jpg",
            BackdropPath = "/breaking-bad-backdrop.jpg",
            FirstAirDate = "2008-01-20",
            LastAirDate = "2013-09-29",
            VoteAverage = 8.9,
            VoteCount = 25000,
            Popularity = 200,
            OriginalLanguage = "en",
            Status = "Ended",
            NumberOfSeasons = 1,
            NumberOfEpisodes = 2,
            Genres =
            [
                new TmdbGenre { Id = 18, Name = "Drama" },
                new TmdbGenre { Id = 80, Name = "Crime" }
            ],
            Seasons =
            [
                new TmdbSeasonSummary
                {
                    Id = 10,
                    Name = "Season 1",
                    AirDate = "2008-01-20",
                    SeasonNumber = 1,
                    EpisodeCount = 2
                }
            ]
        };

        _seasons[(GameOfThronesId, 0)] = new TmdbSeasonDetailsResponse
        {
            Id = 100,
            Name = "Specials",
            AirDate = "2010-01-01",
            SeasonNumber = 0,
            Episodes =
            [
                new TmdbEpisode
                {
                    Id = 9000,
                    Name = "Special",
                    AirDate = "2010-01-01",
                    SeasonNumber = 0,
                    EpisodeNumber = 1,
                    Runtime = 30
                }
            ]
        };

        _seasons[(GameOfThronesId, 1)] = new TmdbSeasonDetailsResponse
        {
            Id = 101,
            Name = "Season 1",
            AirDate = "2011-04-17",
            SeasonNumber = 1,
            Episodes =
            [
                new TmdbEpisode
                {
                    Id = 9001,
                    Name = "Winter Is Coming",
                    Overview = "Ned Stark learns of a threat.",
                    StillPath = "/got-s1e1.jpg",
                    AirDate = "2011-04-17",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    Runtime = 60,
                    VoteAverage = 8.0,
                    VoteCount = 100
                },
                new TmdbEpisode
                {
                    Id = 9002,
                    Name = "The Kingsroad",
                    StillPath = "/got-s1e2.jpg",
                    AirDate = "2011-04-24",
                    SeasonNumber = 1,
                    EpisodeNumber = 2,
                    Runtime = 60,
                    VoteAverage = 8.1,
                    VoteCount = 110
                }
            ]
        };

        _seasons[(GameOfThronesId, 2)] = new TmdbSeasonDetailsResponse
        {
            Id = 102,
            Name = "Season 2",
            AirDate = "2012-04-01",
            SeasonNumber = 2,
            Episodes =
            [
                new TmdbEpisode
                {
                    Id = 9003,
                    Name = "The North Remembers",
                    AirDate = "2012-04-01",
                    SeasonNumber = 2,
                    EpisodeNumber = 1,
                    Runtime = 60,
                    VoteAverage = 8.2,
                    VoteCount = 120
                },
                new TmdbEpisode
                {
                    Id = 9004,
                    Name = "The Night Lands",
                    AirDate = "2012-04-08",
                    SeasonNumber = 2,
                    EpisodeNumber = 2,
                    Runtime = 60,
                    VoteAverage = 8.0,
                    VoteCount = 90
                }
            ]
        };

        _seasons[(BreakingBadId, 1)] = new TmdbSeasonDetailsResponse
        {
            Id = 200,
            Name = "Season 1",
            AirDate = "2008-01-20",
            SeasonNumber = 1,
            Episodes =
            [
                new TmdbEpisode
                {
                    Id = 8001,
                    Name = "Pilot",
                    AirDate = "2008-01-20",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    Runtime = 58
                },
                new TmdbEpisode
                {
                    Id = 8002,
                    Name = "Cat's in the Bag...",
                    AirDate = "2008-01-27",
                    SeasonNumber = 1,
                    EpisodeNumber = 2,
                    Runtime = 48
                }
            ]
        };
    }

    private static TmdbSearchResult ToResult(
        TmdbMediaDetailsResponse details,
        string mediaType)
    {
        return new TmdbSearchResult
        {
            Id = details.Id,
            MediaType = mediaType,
            Title = details.Title,
            Name = details.Name,
            OriginalTitle = details.OriginalTitle,
            OriginalName = details.OriginalName,
            Overview = details.Overview,
            PosterPath = details.PosterPath,
            BackdropPath = details.BackdropPath,
            ReleaseDate = details.ReleaseDate,
            FirstAirDate = details.FirstAirDate,
            VoteAverage = details.VoteAverage,
            VoteCount = details.VoteCount,
            Popularity = details.Popularity,
            OriginalLanguage = details.OriginalLanguage,
            Adult = details.Adult
        };
    }

    private static TmdbSearchResponse EmptyPage(int page)
    {
        return new TmdbSearchResponse
        {
            Page = page,
            Results = [],
            TotalPages = 0,
            TotalResults = 0
        };
    }
}
