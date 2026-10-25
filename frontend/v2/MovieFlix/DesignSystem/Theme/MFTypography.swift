import SwiftUI

nonisolated enum MFTypography {
    static let display = Font.system(.largeTitle, design: .default, weight: .black)
    static let heroTitle = Font.system(.largeTitle, design: .default, weight: .bold)
    static let title = Font.system(.title2, design: .default, weight: .bold)
    static let sectionTitle = Font.system(.title3, design: .default, weight: .bold)
    static let headline = Font.system(.headline, design: .default, weight: .semibold)
    static let body = Font.system(.body, design: .default, weight: .regular)
    static let callout = Font.system(.callout, design: .default, weight: .regular)
    static let caption = Font.system(.caption, design: .default, weight: .regular)
    static let overline = Font.system(.caption2, design: .default, weight: .semibold)
    static let label = Font.system(.subheadline, design: .default, weight: .semibold)
    static let mono = Font.system(.caption, design: .monospaced, weight: .semibold)
}

nonisolated enum MFTextRole: Sendable, Equatable {
    case display
    case heroTitle
    case title
    case sectionTitle
    case headline
    case body
    case callout
    case caption
    case overline
    case label
    case mono

    var font: Font {
        switch self {
        case .display: MFTypography.display
        case .heroTitle: MFTypography.heroTitle
        case .title: MFTypography.title
        case .sectionTitle: MFTypography.sectionTitle
        case .headline: MFTypography.headline
        case .body: MFTypography.body
        case .callout: MFTypography.callout
        case .caption: MFTypography.caption
        case .overline: MFTypography.overline
        case .label: MFTypography.label
        case .mono: MFTypography.mono
        }
    }

    var tracking: CGFloat {
        switch self {
        case .display: -1.8
        case .heroTitle: -1.2
        case .title: -0.6
        case .sectionTitle: -0.3
        case .headline: -0.2
        case .body: -0.1
        case .callout, .caption: 0
        case .overline: 1.1
        case .label: -0.1
        case .mono: 0.4
        }
    }

    var lineSpacing: CGFloat {
        switch self {
        case .display, .heroTitle, .title: 2
        case .body, .callout: 3
        default: 0
        }
    }
}

nonisolated private struct MFTextModifier: ViewModifier {
    let role: MFTextRole

    func body(content: Content) -> some View {
        content
            .font(role.font)
            .tracking(role.tracking)
            .lineSpacing(role.lineSpacing)
            .foregroundStyle(MFColor.textPrimary)
            .textCase(role == .overline ? .uppercase : nil)
    }
}

extension View {
    func mfText(_ role: MFTextRole) -> some View {
        modifier(MFTextModifier(role: role))
    }
}
