import SwiftUI

struct ProfileView: View {
    let container: AppContainer

    @State private var showingAuth = false
    @State private var confirmingSignOut = false

    private var session: SessionStore { container.session }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: MFSpace.xxl) {
                topBar

                switch session.phase {
                case .restoring:
                    restoringContent
                case .signedOut:
                    signedOutContent
                case let .signedIn(user):
                    signedInContent(user)
                }
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.top, MFSpace.xs)
            .padding(.bottom, MFSpace.xxxl)
        }
        .scrollIndicators(.hidden)
        .background(MFColor.canvas)
        .sheet(isPresented: $showingAuth) {
            AuthFlowView(container: container)
        }
        .confirmationDialog(
            "Sign out of MovieFlix?",
            isPresented: $confirmingSignOut,
            titleVisibility: .visible
        ) {
            Button("Sign Out", role: .destructive) {
                Task { await session.signOut() }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Your favorites, ratings, and progress stay safe on your account.")
        }
    }

    // MARK: Chrome

    private var topBar: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text("Profile")
                    .mfText(.title)
                    .accessibilityAddTraits(.isHeader)

                Text(session.isSignedIn ? "Your account" : "Guest")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
            }

            Spacer(minLength: MFSpace.sm)

            if session.isSignedIn {
                Button {
                    Task { await session.refreshProfile() }
                } label: {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 15, weight: .semibold))
                        .frame(width: 40, height: 40)
                        .background(Circle().fill(MFColor.surface))
                        .overlay(Circle().strokeBorder(MFColor.border, lineWidth: 1))
                        .rotationEffect(.degrees(session.isRefreshingProfile ? 180 : 0))
                        .animation(
                            session.isRefreshingProfile
                                ? .linear(duration: 0.8).repeatForever(autoreverses: false)
                                : .default,
                            value: session.isRefreshingProfile
                        )
                }
                .buttonStyle(.plain)
                .disabled(session.isRefreshingProfile)
                .accessibilityLabel("Refresh profile")
            }
        }
    }

    // MARK: Restoring

    private var restoringContent: some View {
        HStack(spacing: MFSpace.sm) {
            ProgressView()
                .controlSize(.small)

            Text("Restoring your session…")
                .mfText(.callout)
                .foregroundStyle(MFColor.textSecondary)
        }
        .padding(MFSpace.lg)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                .fill(MFColor.surface)
        }
        .accessibilityElement(children: .combine)
    }

    // MARK: Signed out

    private var signedOutContent: some View {
        VStack(alignment: .leading, spacing: MFSpace.xl) {
            VStack(alignment: .leading, spacing: MFSpace.lg) {
                ZStack {
                    Circle()
                        .fill(MFColor.subtleGradient)
                        .frame(width: 88, height: 88)
                        .overlay {
                            Circle().strokeBorder(MFColor.borderStrong, lineWidth: 1)
                        }

                    Image(systemName: "person.crop.circle.badge.plus")
                        .font(.system(size: 34, weight: .medium))
                        .foregroundStyle(MFColor.brandGradient)
                }
                .accessibilityHidden(true)

                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    Text("Make MovieFlix yours")
                        .mfText(.title)
                        .accessibilityAddTraits(.isHeader)

                    Text("Sign in to keep your library, ratings, reviews, and episode progress in sync across every device.")
                        .mfText(.body)
                        .foregroundStyle(MFColor.textSecondary)
                        .fixedSize(horizontal: false, vertical: true)
                }

                VStack(alignment: .leading, spacing: MFSpace.sm) {
                    benefitRow("heart.fill", "Favorites and watchlists that follow you")
                    benefitRow("star.fill", "Ratings and reviews from the community")
                    benefitRow("play.circle.fill", "Episode progress and continue watching")
                }
            }
            .padding(MFSpace.lg)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background {
                RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                    .fill(MFColor.surface)
                    .overlay {
                        RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                            .fill(MFColor.subtleGradient)
                    }
            }
            .overlay {
                RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                    .strokeBorder(MFColor.border, lineWidth: 1)
            }

            VStack(spacing: MFSpace.sm) {
                MFButton(
                    title: "Sign In",
                    systemImage: "arrow.right",
                    role: .primary,
                    isFullWidth: true
                ) {
                    showingAuth = true
                }

                Text("You can keep browsing movies and TV without an account.")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity)
            }
        }
    }

    private func benefitRow(_ systemImage: String, _ title: String) -> some View {
        HStack(spacing: MFSpace.sm) {
            Image(systemName: systemImage)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(MFColor.violet)
                .frame(width: 20)
                .accessibilityHidden(true)

            Text(title)
                .mfText(.callout)
                .foregroundStyle(MFColor.textSecondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    // MARK: Signed in

    private func signedInContent(_ user: UserProfile) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.xl) {
            accountCard(user)
            accountDetails(user)

            MFButton(
                title: session.isSigningOut ? "Signing Out…" : "Sign Out",
                systemImage: "rectangle.portrait.and.arrow.right",
                role: .destructive,
                isLoading: session.isSigningOut,
                isFullWidth: true
            ) {
                confirmingSignOut = true
            }
            .disabled(session.isSigningOut)
        }
    }

    private func accountCard(_ user: UserProfile) -> some View {
        HStack(alignment: .center, spacing: MFSpace.md) {
            MFAvatar(initials: user.initials, size: 72)
                .overlay {
                    Circle()
                        .strokeBorder(MFColor.violet.opacity(0.35), lineWidth: 2)
                        .padding(-3)
                }

            VStack(alignment: .leading, spacing: MFSpace.xxs) {
                Text(user.displayName)
                    .mfText(.headline)
                    .lineLimit(2)
                    .accessibilityAddTraits(.isHeader)

                Text(user.email)
                    .mfText(.callout)
                    .foregroundStyle(MFColor.textSecondary)
                    .lineLimit(1)
                    .truncationMode(.middle)

                HStack(spacing: MFSpace.xs) {
                    ForEach(user.roles, id: \.self) { role in
                        MFTagPill(
                            title: role,
                            systemImage: role == "Admin" ? "shield.lefthalf.filled" : "person"
                        )
                    }
                }
                .padding(.top, 2)
            }

            Spacer(minLength: 0)
        }
        .padding(MFSpace.lg)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                .fill(MFColor.surface)
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                        .fill(MFColor.subtleGradient)
                }
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
    }

    private func accountDetails(_ user: UserProfile) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            Text("Account")
                .mfText(.sectionTitle)
                .accessibilityAddTraits(.isHeader)

            MFGlassPanel(cornerRadius: MFRadius.lg) {
                VStack(spacing: 0) {
                    detailRow("Member since", user.memberSince)
                    Divider().overlay(MFColor.separator)
                    detailRow("Email", user.email)
                    Divider().overlay(MFColor.separator)

                    HStack(spacing: MFSpace.xs) {
                        Circle()
                            .fill(MFColor.success)
                            .frame(width: 7, height: 7)

                        Text("Session refreshes automatically")
                            .mfText(.callout)
                            .foregroundStyle(MFColor.textSecondary)

                        Spacer(minLength: 0)
                    }
                    .padding(.vertical, MFSpace.xs)
                }
            }
        }
    }

    private func detailRow(_ label: String, _ value: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: MFSpace.md) {
            Text(label)
                .mfText(.callout)
                .foregroundStyle(MFColor.textTertiary)

            Spacer(minLength: MFSpace.sm)

            Text(value)
                .mfText(.callout)
                .multilineTextAlignment(.trailing)
                .lineLimit(1)
                .truncationMode(.middle)
        }
        .padding(.vertical, MFSpace.xs)
    }
}

#Preview {
    let configuration = AppConfiguration(
        apiBaseURL: URL(string: "https://10.35.255.69:7014")!,
        imageBaseURL: URL(string: "https://image.tmdb.org/t/p")!,
        environment: .preview,
        appVersion: "1.0",
        buildNumber: "1",
        userAgent: "MovieFlix Preview"
    )
    ProfileView(container: AppContainer(configuration: configuration))
}
