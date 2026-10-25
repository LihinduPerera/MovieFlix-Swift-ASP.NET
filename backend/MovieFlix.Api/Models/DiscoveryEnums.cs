namespace MovieFlix.Api.Models;

/// <summary>Search scope for <c>GET /api/v1/search</c>.</summary>
public enum SearchType
{
    Multi,
    Movie,
    Tv
}

/// <summary>Media filter used by trending, where "all" is allowed.</summary>
public enum MediaTypeFilter
{
    All,
    Movie,
    Tv
}

/// <summary>TMDB trending window.</summary>
public enum TrendingWindow
{
    Day,
    Week
}

/// <summary>The supported TMDB feed lists.</summary>
public enum MediaFeed
{
    Popular,
    TopRated,
    NowPlaying,
    Upcoming,
    OnTheAir,
    AiringToday
}
