import SwiftUI

nonisolated enum RatingBadgeStyle: Sendable {
    case overlay
    case plain
    case prominent
}

struct RatingBadge: View {
    let value: Double
    var style: RatingBadgeStyle = .overlay
    var label: String? = nil

    var body: some View {
        HStack(spacing: 3) {
            Image(systemName: "star.fill")
                .font(.system(size: style == .prominent ? 12 : 9, weight: .bold))
                .foregroundStyle(hasRating ? MFColor.rating : MFColor.textTertiary)

            Text(displayValue)
                .font(style == .prominent ? MFTypography.label : MFTypography.mono)
                .monospacedDigit()
                .foregroundStyle(hasRating ? MFColor.textPrimary : MFColor.textSecondary)
        }
        .padding(.horizontal, style == .prominent ? MFSpace.sm : 6)
        .padding(.vertical, style == .prominent ? 6 : 4)
        .background {
            if style != .plain {
                Capsule(style: .continuous)
                    .fill(.black.opacity(style == .prominent ? 0.40 : 0.58))
                    .overlay {
                        Capsule(style: .continuous)
                            .strokeBorder(.white.opacity(0.12), lineWidth: 0.5)
                    }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label ?? accessibilityValue)
    }

    private var hasRating: Bool { value > 0 }

    private var displayValue: String {
        guard hasRating else { return "NR" }
        return value.formatted(.number.precision(.fractionLength(1)))
    }

    private var accessibilityValue: String {
        guard hasRating else { return "Not rated" }
        return "Rated \(displayValue) out of 10"
    }
}
