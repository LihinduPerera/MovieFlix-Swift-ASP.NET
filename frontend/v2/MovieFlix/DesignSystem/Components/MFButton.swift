import SwiftUI

nonisolated enum MFButtonRole: Sendable {
    case primary
    case secondary
    case ghost
    case destructive

    var foregroundColor: Color {
        switch self {
        case .primary: .white
        case .secondary, .ghost: MFColor.textPrimary
        case .destructive: MFColor.danger
        }
    }
}

struct MFButton: View {
    let title: String
    var systemImage: String?
    var role: MFButtonRole = .primary
    var isLoading = false
    var isFullWidth = false
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: MFSpace.xs) {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 15, weight: .semibold))
                }
                Text(title)
                    .font(MFTypography.label)
                    .lineLimit(1)
            }
            .opacity(isLoading ? 0 : 1)
            .overlay {
                if isLoading {
                    ProgressView()
                        .controlSize(.small)
                        .tint(role.foregroundColor)
                }
            }
            .frame(maxWidth: isFullWidth ? .infinity : nil)
        }
        .buttonStyle(MFButtonStyle(role: role))
        .allowsHitTesting(!isLoading)
        .accessibilityLabel(title)
        .accessibilityAddTraits(isLoading ? .updatesFrequently : [])
    }
}

private struct MFButtonStyle: ButtonStyle {
    let role: MFButtonRole

    func makeBody(configuration: Configuration) -> some View {
        MFButtonStyleBody(configuration: configuration, role: role)
    }
}

private struct MFButtonStyleBody: View {
    let configuration: ButtonStyleConfiguration
    let role: MFButtonRole
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        configuration.label
            .foregroundStyle(role.foregroundColor)
            .padding(.horizontal, MFSpace.lg)
            .frame(minHeight: MFSize.buttonHeight)
            .background {
                background
            }
            .overlay {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .strokeBorder(borderColor, lineWidth: 1)
            }
            .clipShape(RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous))
            .scaleEffect(configuration.isPressed && !reduceMotion ? 0.975 : 1)
            .opacity(isEnabled ? 1 : 0.42)
            .animation(reduceMotion ? nil : MFMotion.press, value: configuration.isPressed)
    }

    @ViewBuilder
    private var background: some View {
        switch role {
        case .primary:
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.primaryButtonGradient)
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .fill(
                            LinearGradient(
                                colors: [.white.opacity(0.20), .clear],
                                startPoint: .top,
                                endPoint: .center
                            )
                        )
                        .blendMode(.softLight)
                }
                .mfShadow(MFShadow.Shadow(color: MFColor.violet.opacity(0.28), radius: 16, x: 0, y: 8))
        case .secondary:
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(.ultraThinMaterial)
        case .ghost:
            Color.clear
        case .destructive:
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.danger.opacity(0.14))
        }
    }

    private var borderColor: Color {
        switch role {
        case .primary: .white.opacity(0.16)
        case .secondary: MFColor.borderStrong
        case .ghost: MFColor.border
        case .destructive: MFColor.danger.opacity(0.45)
        }
    }
}

// MARK: - Icon button

nonisolated enum MFIconButtonStyleKind: Sendable {
    case plain
    case tinted
    case glass
}

struct MFIconButton: View {
    let systemImage: String
    let label: String
    var style: MFIconButtonStyleKind = .glass
    var isSelected = false
    var tint: Color = MFColor.violet
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: systemImage)
                .font(.system(size: 17, weight: .semibold))
                .symbolVariant(isSelected ? .fill : .none)
                .frame(width: MFSize.iconButton, height: MFSize.iconButton)
        }
        .buttonStyle(MFIconButtonStyle(style: style, isSelected: isSelected, tint: tint))
        .accessibilityLabel(label)
        .accessibilityValue(isSelected ? "Selected" : "")
    }
}

private struct MFIconButtonStyle: ButtonStyle {
    let style: MFIconButtonStyleKind
    let isSelected: Bool
    let tint: Color

    func makeBody(configuration: Configuration) -> some View {
        MFIconButtonStyleBody(
            configuration: configuration,
            style: style,
            isSelected: isSelected,
            tint: tint
        )
    }
}

private struct MFIconButtonStyleBody: View {
    let configuration: ButtonStyleConfiguration
    let style: MFIconButtonStyleKind
    let isSelected: Bool
    let tint: Color
    @Environment(\.isEnabled) private var isEnabled
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        configuration.label
            .foregroundStyle(isSelected ? tint : MFColor.textPrimary)
            .background {
                switch style {
                case .plain:
                    Color.clear
                case .tinted:
                    Circle().fill(isSelected ? tint.opacity(0.20) : MFColor.surface.opacity(0.68))
                case .glass:
                    Circle().fill(.ultraThinMaterial)
                }
            }
            .overlay {
                Circle()
                    .strokeBorder(
                        isSelected ? tint.opacity(0.55) : MFColor.border,
                        lineWidth: 1
                    )
            }
            .clipShape(Circle())
            .scaleEffect(configuration.isPressed && !reduceMotion ? 0.94 : 1)
            .opacity(isEnabled ? 1 : 0.42)
            .animation(reduceMotion ? nil : MFMotion.press, value: configuration.isPressed)
    }
}
