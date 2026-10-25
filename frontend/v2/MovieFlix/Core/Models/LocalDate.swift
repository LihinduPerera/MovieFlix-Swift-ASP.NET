import Foundation

/// A calendar date without a time component, serialized by the API as
/// `yyyy-MM-dd`. Keeping this distinct from `Date` prevents timezone drift and
/// makes the wire format impossible to confuse with UTC timestamps.
nonisolated struct LocalDate: Hashable, Comparable, Sendable, Codable, CustomStringConvertible {
    let year: Int
    let month: Int
    let day: Int

    init?(year: Int, month: Int, day: Int) {
        var components = DateComponents()
        components.calendar = Self.calendar
        components.timeZone = Self.timeZone
        components.year = year
        components.month = month
        components.day = day

        guard let date = Self.calendar.date(from: components) else { return nil }
        let roundTrip = Self.calendar.dateComponents([.year, .month, .day], from: date)
        guard roundTrip.year == year, roundTrip.month == month, roundTrip.day == day else {
            return nil
        }

        self.year = year
        self.month = month
        self.day = day
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        let raw = try container.decode(String.self)
        guard let value = Self.parse(raw) else {
            throw DecodingError.dataCorruptedError(
                in: container,
                debugDescription: "Expected a calendar date in yyyy-MM-dd format, received '\(raw)'."
            )
        }
        self = value
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode(iso8601)
    }

    static func parse(_ raw: String) -> LocalDate? {
        let parts = raw.split(separator: "-", omittingEmptySubsequences: false)
        guard parts.count == 3,
              parts[0].count == 4,
              parts[1].count == 2,
              parts[2].count == 2,
              let year = Int(parts[0]),
              let month = Int(parts[1]),
              let day = Int(parts[2])
        else {
            return nil
        }
        return LocalDate(year: year, month: month, day: day)
    }

    var iso8601: String {
        String(format: "%04d-%02d-%02d", year, month, day)
    }

    /// Midnight UTC for the represented calendar date. Use only for display
    /// formatting, never for scheduling or arithmetic across timezones.
    var date: Date? {
        var components = DateComponents()
        components.calendar = Self.calendar
        components.timeZone = Self.timeZone
        components.year = year
        components.month = month
        components.day = day
        return Self.calendar.date(from: components)
    }

    func formatted(style: Date.FormatStyle = .dateTime.year().month(.abbreviated).day()) -> String {
        date?.formatted(style) ?? iso8601
    }

    var description: String { iso8601 }

    static func < (lhs: LocalDate, rhs: LocalDate) -> Bool {
        (lhs.year, lhs.month, lhs.day) < (rhs.year, rhs.month, rhs.day)
    }

    private static let timeZone = TimeZone(secondsFromGMT: 0)!
    private static let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        calendar.locale = Locale(identifier: "en_US_POSIX")
        return calendar
    }()
}

/// Parses the UTC ISO-8601 timestamps returned by ASP.NET Core. Fractional
/// seconds are accepted but not required.
nonisolated enum APIDateParser {
    static func date(from raw: String) -> Date? {
        let withFractional = ISO8601DateFormatter()
        withFractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = withFractional.date(from: raw) {
            return date
        }

        let withoutFractional = ISO8601DateFormatter()
        withoutFractional.formatOptions = [.withInternetDateTime]
        return withoutFractional.date(from: raw)
    }
}
