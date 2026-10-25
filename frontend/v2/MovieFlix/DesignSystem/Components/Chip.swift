import SwiftUI

struct MFChoiceChip: View {
    let title: String
    var systemImage: String?
    var isSelected = false
    let action: () -> Void

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                if let systemImage {
                    Image(systemName: systemImage)
                        .font(.system(size: 12, weight: .semibold))
                }
                Text(title)
                    .font(MFTypography.label)
                    .lineLimit(1)
            }
            .foregroundStyle(isSelected ? MFColor.textPrimary : MFColor.textSecondary)
            .padding(.horizontal, MFSpace.sm)
            .frame(minHeight: 38)
            .background {
                Capsule(style: .continuous)
                    .fill(isSelected ? MFColor.violet.opacity(0.20) : MFColor.surface)
            }
            .overlay {
                Capsule(style: .continuous)
                    .strokeBorder(
                        isSelected ? MFColor.violet.opacity(0.70) : MFColor.border,
                        lineWidth: 1
                    )
            }
            .contentShape(Capsule(style: .continuous))
        }
        .buttonStyle(.plain)
        .scaleEffect(1)
        .animation(reduceMotion ? nil : MFMotion.quick, value: isSelected)
        .accessibilityLabel(title)
        .accessibilityAddTraits(isSelected ? .isSelected : [])
    }
}

struct MFTagPill: View {
    let title: String
    var systemImage: String?

    var body: some View {
        HStack(spacing: 5) {
            if let systemImage {
                Image(systemName: systemImage)
                    .font(.system(size: 10, weight: .semibold))
            }
            Text(title)
                .font(MFTypography.mono)
                .lineLimit(1)
        }
        .foregroundStyle(MFColor.textSecondary)
        .padding(.horizontal, MFSpace.xs)
        .padding(.vertical, 5)
        .fixedSize(horizontal: true, vertical: false)
        .background {
            Capsule(style: .continuous)
                .fill(MFColor.surfaceElevated.opacity(0.88))
        }
        .overlay {
            Capsule(style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 0.5)
        }
    }
}
