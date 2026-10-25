import SwiftUI

struct MFSectionHeader: View {
    let title: String
    var subtitle: String?
    var actionTitle: String? = "See All"
    var action: (() -> Void)?

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: MFSpace.sm) {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .mfText(.sectionTitle)

                if let subtitle {
                    Text(subtitle)
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textSecondary)
                }
            }

            Spacer(minLength: MFSpace.sm)

            if let action, let actionTitle {
                Button(action: action) {
                    HStack(spacing: 2) {
                        Text(actionTitle)
                            .font(MFTypography.label)
                        Image(systemName: "chevron.right")
                            .font(.system(size: 11, weight: .bold))
                    }
                    .foregroundStyle(MFColor.textSecondary)
                    .frame(minHeight: MFSize.cardMinHeight)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("\(actionTitle) \(title)")
            }
        }
    }
}
