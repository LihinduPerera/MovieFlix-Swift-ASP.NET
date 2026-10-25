import Foundation

nonisolated enum JSONCoding {
    static func makeDecoder() -> JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .useDefaultKeys
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let raw = try container.decode(String.self)
            guard let date = APIDateParser.date(from: raw) else {
                throw DecodingError.dataCorruptedError(
                    in: container,
                    debugDescription: "Expected an ISO-8601 timestamp, received '\(raw)'."
                )
            }
            return date
        }
        return decoder
    }

    static func makeEncoder() -> JSONEncoder {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .useDefaultKeys
        encoder.dateEncodingStrategy = .custom { date, encoder in
            var container = encoder.singleValueContainer()
            let formatter = ISO8601DateFormatter()
            formatter.formatOptions = [.withInternetDateTime]
            try container.encode(formatter.string(from: date))
        }
        encoder.outputFormatting = [.withoutEscapingSlashes]
        return encoder
    }
}

/// Type-erased request body. Endpoints can carry heterogeneous `Encodable`
/// payloads while remaining `Sendable`.
nonisolated struct AnyEncodable: Encodable, Sendable {
    private let encodeClosure: @Sendable (Encoder) throws -> Void

    init<T: Encodable & Sendable>(_ value: T) {
        encodeClosure = { encoder in
            try value.encode(to: encoder)
        }
    }

    func encode(to encoder: any Encoder) throws {
        try encodeClosure(encoder)
    }
}

nonisolated struct PagedResponse<Item: Decodable & Sendable>: Decodable, Sendable {
    let items: [Item]
    let page: Int
    let pageSize: Int
    let totalCount: Int
    let totalPages: Int

    var hasMore: Bool { page < totalPages }
    var nextPage: Int? { hasMore ? page + 1 : nil }
    var isEmpty: Bool { items.isEmpty }
}

extension PagedResponse: Equatable where Item: Equatable {}
extension PagedResponse: Hashable where Item: Hashable {}

nonisolated struct EmptyResponse: Decodable, Sendable {
    init() {}
    init(from decoder: any Decoder) throws {}
}

nonisolated enum HTTPMethod: String, Sendable {
    case get = "GET"
    case post = "POST"
    case put = "PUT"
    case patch = "PATCH"
    case delete = "DELETE"
}
