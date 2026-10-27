import SwiftUI

/// A compact two-or-more option switch used inside section headers. Unlike
/// `MFChoiceChip`, it reads as a secondary control and never dominates the row.
struct MFCompactSegmentedControl<Option: Hashable>: View {
    let options: [Option]
    let title: (Option) -> String
    let selection: Option
    let onSelect: (Option) -> Void

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        HStack(spacing: 2) {
            ForEach(options, id: \.self) { option in
                let isSelected = option == selection
                Button {
                    onSelect(option)
                } label: {
                    Text(title(option))
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundStyle(
                            isSelected ? MFColor.textPrimary : MFColor.textSecondary
                        )
                        .padding(.horizontal, 10)
                        .frame(minHeight: 28)
                        .background {
                            Capsule(style: .continuous)
                                .fill(isSelected ? MFColor.surfaceElevated : .clear)
                        }
                        .contentShape(Capsule(style: .continuous))
                }
                .buttonStyle(.plain)
                .accessibilityLabel(title(option))
                .accessibilityAddTraits(isSelected ? .isSelected : [])
            }
        }
        .padding(2)
        .background {
            Capsule(style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            Capsule(style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
        .animation(reduceMotion ? nil : MFMotion.quick, value: selection)
    }
}
