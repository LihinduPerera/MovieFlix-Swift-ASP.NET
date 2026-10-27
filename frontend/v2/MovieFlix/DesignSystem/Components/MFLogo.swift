import SwiftUI

struct MFLogo: View {
    var size: CGFloat = 22
    var showsMark = false

    var body: some View {
        HStack(spacing: size * 0.28) {
            if showsMark {
                Image(systemName: "sparkles.tv.fill")
                    .font(.system(size: size * 1.05, weight: .bold))
                    .foregroundStyle(MFColor.brandGradient)
                    .accessibilityHidden(true)
            }

            Text("MOVIEFLIX")
                .font(.system(size: size, weight: .black, design: .default))
                .tracking(size * 0.11)
                .foregroundStyle(MFColor.brandGradient)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("MovieFlix")
    }
}

struct MFProgressBar: View {
    let value: Double
    var tint: Color = MFColor.violet
    var height: CGFloat = 4

    var body: some View {
        GeometryReader { proxy in
            ZStack(alignment: .leading) {
                Capsule(style: .continuous)
                    .fill(MFColor.textPrimary.opacity(0.18))

                Capsule(style: .continuous)
                    .fill(tint)
                    .frame(width: proxy.size.width * clampedValue)
            }
        }
        .frame(height: height)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Watch progress")
        .accessibilityValue("\(Int((clampedValue * 100).rounded())) percent")
    }

    private var clampedValue: Double {
        min(max(value, 0), 1)
    }
}

struct MFAvatar: View {
    let initials: String
    var size: CGFloat = MFSize.avatar

    var body: some View {
        ZStack {
            Circle()
                .fill(MFColor.primaryButtonGradient)

            Text(initials)
                .font(.system(size: size * 0.36, weight: .bold))
                .foregroundStyle(.white)
        }
        .frame(width: size, height: size)
        .overlay {
            Circle()
                .strokeBorder(.white.opacity(0.18), lineWidth: 1)
        }
        .accessibilityHidden(true)
    }
}
