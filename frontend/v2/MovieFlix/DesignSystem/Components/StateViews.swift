import SwiftUI

struct MFEmptyStateView: View {
    let systemImage: String
    let title: String
    let message: String
    var primaryTitle: String?
    var primaryAction: (() -> Void)?
    var secondaryTitle: String?
    var secondaryAction: (() -> Void)?

    var body: some View {
        VStack(spacing: MFSpace.lg) {
            ZStack {
                Circle()
                    .fill(MFColor.subtleGradient)
                    .frame(width: 92, height: 92)
                    .overlay {
                        Circle()
                            .strokeBorder(MFColor.borderStrong, lineWidth: 1)
                    }

                Image(systemName: systemImage)
                    .font(.system(size: 34, weight: .medium))
                    .foregroundStyle(MFColor.brandGradient)
            }
            .accessibilityHidden(true)

            VStack(spacing: MFSpace.xs) {
                Text(title)
                    .mfText(.title)
                    .multilineTextAlignment(.center)

                Text(message)
                    .mfText(.body)
                    .foregroundStyle(MFColor.textSecondary)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if primaryTitle != nil || secondaryTitle != nil {
                VStack(spacing: MFSpace.xs) {
                    if let primaryTitle, let primaryAction {
                        MFButton(title: primaryTitle, role: .primary, isFullWidth: true, action: primaryAction)
                    }
                    if let secondaryTitle, let secondaryAction {
                        MFButton(title: secondaryTitle, role: .secondary, isFullWidth: true, action: secondaryAction)
                    }
                }
                .padding(.top, MFSpace.xxs)
            }
        }
        .frame(maxWidth: 420)
        .padding(MFSpace.xl)
        .frame(maxWidth: .infinity)
    }
}

struct MFErrorStateView: View {
    let error: APIError
    var retry: (() -> Void)?
    var signIn: (() -> Void)?

    var body: some View {
        VStack(spacing: MFSpace.lg) {
            ZStack {
                Circle()
                    .fill(MFColor.danger.opacity(0.12))
                    .frame(width: 92, height: 92)
                    .overlay {
                        Circle()
                            .strokeBorder(MFColor.danger.opacity(0.30), lineWidth: 1)
                    }

                Image(systemName: error.systemImage)
                    .font(.system(size: 34, weight: .medium))
                    .foregroundStyle(MFColor.danger)
            }
            .accessibilityHidden(true)

            VStack(spacing: MFSpace.xs) {
                Text(error.title)
                    .mfText(.title)
                    .multilineTextAlignment(.center)

                Text(error.message)
                    .mfText(.body)
                    .foregroundStyle(MFColor.textSecondary)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)

                if let suggestion = error.recoverySuggestion {
                    Text(suggestion)
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textTertiary)
                        .multilineTextAlignment(.center)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }

            VStack(spacing: MFSpace.xs) {
                if let signIn, error.needsAuthentication {
                    MFButton(
                        title: "Sign In",
                        systemImage: "person.crop.circle",
                        role: .primary,
                        isFullWidth: true,
                        action: signIn
                    )
                } else if let retry, error.isRetryable {
                    MFButton(
                        title: "Try Again",
                        systemImage: "arrow.clockwise",
                        role: .primary,
                        isFullWidth: true,
                        action: retry
                    )
                }
            }
            .padding(.top, MFSpace.xxs)
        }
        .frame(maxWidth: 440)
        .padding(MFSpace.xl)
        .frame(maxWidth: .infinity)
        .accessibilityElement(children: .contain)
    }
}
