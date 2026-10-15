using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.DTOs.Common;
using MovieFlix.Api.DTOs.Favorites;
using MovieFlix.Api.DTOs.Movies;
using MovieFlix.Api.Exceptions;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Services;

public class MovieService : IMovieService
{
    private readonly MovieFlixDbContext _context;

    public MovieService(MovieFlixDbContext context)
    {
        _context = context;
    }

    private static MovieResponse MapToResponse(Movie movie)
    {
        return new MovieResponse
        {
            Id = movie.Id,
            TmdbId = movie.TmdbId,
            Title = movie.Title,
            Overview = movie.Overview
        };
    }

    public async Task<PagedResponse<MovieResponse>> GetMoviesAsync(MovieQueryRequest request)
    {
        //This is called deferred execution. IQueryable
        var query = _context.Movies
            .AsNoTracking();

        // 1.Filter
        if(!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(movie => EF.Functions.Like(movie.Title, $"%{search}%"));
        }

        // 2.Sort
        query = request.SortBy.ToLower() switch
        {
            "title" => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(movie => movie.Title)
                : query.OrderBy(movie => movie.Title),

            "tmdbid" => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(movie => movie.TmdbId)
                : query.OrderBy(movie => movie.TmdbId),

            _ => request.SortOrder.ToLower() == "desc"
                ? query.OrderByDescending(movie => movie.Id)
                : query.OrderBy(movie => movie.Id)
        };

        // 3.Count
        var totalCount = await query.CountAsync();

        // 4.Pagination
        var movies = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // 5.Mapping
        var items = movies
            .Select(MapToResponse)
            .ToList();

        // 6.Calculate total pages
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResponse<MovieResponse>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<MovieResponse?> GetMovieByIdAsync(int id)
    {
        var movie = await _context.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(movie => movie.Id == id);

        if(movie is null)
        {
            return null;
        }

        return MapToResponse(movie);
    }

    public async Task<MovieResponse> CreateMovieAsync(CreateMovieRequest request)
    {
        var existingMovie = await _context.Movies
            .AsNoTracking()
            .FirstOrDefaultAsync(movie => movie.TmdbId == request.TmdbId);

        if(existingMovie is not null)
        {
            throw new MovieAlreadyExistsException(request.TmdbId);
        }

        var movie = new Movie
        {
            TmdbId = request.TmdbId,
            Title = request.Title,
            Overview = request.Overview
        };

        _context.Movies.Add(movie);

        await _context.SaveChangesAsync();

        return MapToResponse(movie);
    }

    public async Task<MovieResponse?> UpdateMovieAsync(int id, UpdateMovieRequest request)
    {
        var movie = await _context.Movies
            .FirstOrDefaultAsync(movie => movie.Id == id);

        if(movie is null)
        {
            return null;
        }

        movie.Title = request.Title;
        movie.Overview = request.Overview;

        await _context.SaveChangesAsync();

        return MapToResponse(movie);
    }

    public async Task<bool> DeleteMovieAsync(int id)
    {
        var movie = await _context.Movies
            .FirstOrDefaultAsync(movie => movie.Id == id);

        if(movie is null)
        {
            return false;
        }

        _context.Movies.Remove(movie);

        await _context.SaveChangesAsync();

        return true;
    }
}