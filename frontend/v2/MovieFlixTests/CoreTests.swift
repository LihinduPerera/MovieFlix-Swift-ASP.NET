import Foundation
import Testing

@testable import MovieFlix

@Suite("LocalDate")
struct LocalDateTests {
    @Test("Parses valid calendar dates")
    func parsesValidDates() throws {
        let date = try #require(LocalDate.parse("2024-02-29"))
        #expect(date.year == 2024)
        #expect(date.month == 2)
        #expect(date.day == 29)
        #expect(date.iso8601 == "2024-02-29")
    }

    @Test("Rejects impossible dates", arguments: [
        "2023-02-29",
        "2024-13-01",
        "2024-04-31",
        "2024-1-01",
        "not-a-date"
    ])
    func rejectsImpossibleDates(_ raw: String) {
        #expect(LocalDate.parse(raw) == nil)
    }

    @Test("Sorts chronologically")
    func sortsChronologically() throws {
        let earlier = try #require(LocalDate.parse("1999-12-31"))
        let later = try #require(LocalDate.parse("2000-01-01"))
        #expect(earlier < later)
    }
}

@Suite("Media identity")
struct MediaIdentityTests {
    @Test("Routes use the public type and TMDB id")
    func routePath() {
        let id = MediaID(mediaType: .tv, tmdbId: 1396)
        #expect(id.id == "tv-1396")
        #expect(id.routePath == "tv/1396")
    }
}

@Suite("API error mapping")
struct APIErrorMappingTests {
    @Test("Maps validation problems")
    func mapsValidation() throws {
        let json = """
        {
          "title": "One or more validation errors occurred.",
          "status": 400,
          "errors": { "email": ["The Email field is not a valid e-mail address."] }
        }
        """
        let data = Data(json.utf8)
        let response = try #require(HTTPURLResponse(
            url: URL(string: "https://example.com")!,
            statusCode: 400,
            httpVersion: nil,
            headerFields: nil
        ))

        let error = APIClient.error(status: 400, data: data, response: response)
        guard case let .validation(problem) = error else {
            Issue.record("Expected a validation error, received \(error)")
            return
        }
        #expect(problem.fieldErrors["email"]?.count == 1)
    }

    @Test("Parses Retry-After seconds")
    func parsesRetryAfterSeconds() {
        #expect(APIClient.retryAfter(from: "12") == 12)
        #expect(APIClient.retryAfter(from: " 3 ") == 3)
        #expect(APIClient.retryAfter(from: "soon") == nil)
    }
}

@Suite("Endpoint URLs")
struct EndpointTests {
    @Test("Builds detail URLs with query items")
    func buildsDetailURL() throws {
        let base = try #require(URL(string: "https://example.com"))
        let url = try APIEndpoint.details(mediaType: .tv, tmdbId: 1396, language: "en-US")
            .url(relativeTo: base)

        #expect(url.path == "/api/v1/media/tv/1396")
        #expect(url.query?.contains("language=en-US") == true)
    }

    @Test("Drops empty query values")
    func dropsEmptyQueryValues() {
        let items = APIEndpoint.query([
            ("page", "1"),
            ("language", nil),
            ("region", "")
        ])
        #expect(items.count == 1)
        #expect(items.first?.name == "page")
    }
}
