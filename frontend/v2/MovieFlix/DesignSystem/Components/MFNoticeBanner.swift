import SwiftUI

/// A compact, dismissible inline notice used for partial failures that should
/// not replace the content the user is already looking at.
struct MFNoticeBanner: View {
    let message: String
    var systemImage = "wifi.exclamationmark"
    var tint = MFColor.warning
    var onDismiss: (() -> Void)?

    var body: some View {
        HStack(spacing: MFSpace.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(tint)
                .accessibilityHidden(true)

            Text(message)
                .mfText(.caption)
                .foregroundStyle(MFColor.textSecondary)
                .fixedSize(horizontal: false, vertical: true)

            Spacer(minLength: MFSpace.xs)

            if let onDismiss {
                Button(action: onDismiss) {
                    Image(systemName: "xmark")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(MFColor.textTertiary)
                        .frame(width: 30, height: 30)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Dismiss")
            }
        }
        .padding(.leading, MFSpace.sm)
        .padding(.trailing, onDismiss == nil ? MFSpace.sm : MFSpace.xxs)
        .padding(.vertical, MFSpace.xxs)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
    }
}
