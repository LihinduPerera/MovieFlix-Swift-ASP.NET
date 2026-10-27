import Observation
import SwiftUI

@MainActor
@Observable
final class AppContainer {
    let configuration: AppConfiguration
    let apiClient: APIClient
    let imagePipeline: ImagePipeline
    let images: TMDBImageURLBuilder
    let mediaRepository: any MediaRepositoryProtocol
    let libraryRepository: any LibraryRepositoryProtocol
    let searchHistoryRepository: any SearchHistoryRepositoryProtocol
    let session: SessionStore
    let navigation: AppNavigation
    let toast: ToastCenter

    init(configuration: AppConfiguration) {
        self.configuration = configuration

        let apiSession = AppNetworking.makeSession(
            configuration: configuration,
            cacheName: "MovieFlixAPI"
        )
        let imageSession = AppNetworking.makeSession(
            configuration: configuration,
            cacheName: "MovieFlixImages"
        )

        let tokenStore: any TokenStoring & AccessTokenProviding
        if configuration.environment == .preview {
            tokenStore = InMemoryTokenStore()
        } else {
            tokenStore = KeychainTokenStore()
        }

        let clientConfiguration = APIClientConfiguration(
            baseURL: configuration.apiBaseURL,
            userAgent: configuration.userAgent
        )

        // Auth calls must not participate in 401 recovery; refresh itself is
        // public and would otherwise recurse.
        let authClient = APIClient(
            configuration: clientConfiguration,
            session: apiSession,
            tokenProvider: tokenStore
        )
        let authRepository = AuthRepository(client: authClient)

        let session = SessionStore(
            repository: authRepository,
            tokenStore: tokenStore
        )
        self.session = session

        let mediaClient = APIClient(
            configuration: clientConfiguration,
            session: apiSession,
            tokenProvider: tokenStore,
            unauthorizedRecovery: { [session] in
                await session.recoverUnauthorized()
            }
        )
        self.apiClient = mediaClient

        self.imagePipeline = ImagePipeline(session: imageSession)
        self.images = TMDBImageURLBuilder(baseURL: configuration.imageBaseURL)
        self.mediaRepository = MediaRepository(client: mediaClient)
        self.libraryRepository = LibraryRepository(client: mediaClient)
        self.searchHistoryRepository = SearchHistoryRepository(client: mediaClient)
        self.navigation = AppNavigation()
        self.toast = ToastCenter()
    }

    static func live() throws -> AppContainer {
        AppContainer(configuration: try AppConfiguration.live())
    }

    func posterURL(for media: MediaSummary, size: TMDBImageSize = .poster) -> URL? {
        images.url(for: media.posterPath, size: size)
    }

    func backdropURL(for media: MediaSummary, size: TMDBImageSize = .backdrop) -> URL? {
        images.url(for: media.backdropPath, size: size)
    }

    func profileURL(for path: String?, size: TMDBImageSize = .profile) -> URL? {
        images.url(for: path, size: size)
    }

    func stillURL(for path: String?, size: TMDBImageSize = .still) -> URL? {
        images.url(for: path, size: size)
    }
}
