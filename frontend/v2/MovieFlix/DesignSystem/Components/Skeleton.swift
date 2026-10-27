import SwiftUI

struct SkeletonShimmer: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var phase: CGFloat = -1

    var body: some View {
        GeometryReader { proxy in
            if reduceMotion {
                Color.clear
            } else {
                LinearGradient(
                    colors: [
                        .clear,
                        MFColor.skeletonHighlight.opacity(0.85),
                        .clear
                    ],
                    startPoint: .leading,
                    endPoint: .trailing
                )
                .frame(width: proxy.size.width * 0.7)
                .offset(x: phase * proxy.size.width * 1.7)
                .onAppear {
                    phase = -1
                    withAnimation(.linear(duration: 1.35).repeatForever(autoreverses: false)) {
                        phase = 1
                    }
                }
            }
        }
        .allowsHitTesting(false)
    }
}

struct SkeletonBlock: View {
    var cornerRadius: CGFloat = MFRadius.sm

    var body: some View {
        RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
            .fill(MFColor.skeletonBase)
            .overlay {
                SkeletonShimmer()
                    .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
            }
            .accessibilityHidden(true)
    }
}

struct PosterSkeleton: View {
    var width: CGFloat = MFSize.posterWidth

    var body: some View {
        VStack(alignment: .leading, spacing: MFSpace.xs) {
            SkeletonBlock(cornerRadius: MFRadius.poster)
                .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)
                .frame(width: width)

            SkeletonBlock(cornerRadius: MFRadius.xs)
                .frame(width: width * 0.82, height: 12)

            SkeletonBlock(cornerRadius: MFRadius.xs)
                .frame(width: width * 0.52, height: 10)
        }
        .frame(width: width, alignment: .leading)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading title")
    }
}

struct MediaRailSkeleton: View {
    var cardCount = 4

    var body: some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            SkeletonBlock(cornerRadius: MFRadius.xs)
                .frame(width: 148, height: 18)
                .padding(.horizontal, MFSpace.lg)

            ScrollView(.horizontal) {
                HStack(alignment: .top, spacing: MFSize.railSpacing) {
                    ForEach(0..<cardCount, id: \.self) { _ in
                        PosterSkeleton()
                    }
                }
                .padding(.horizontal, MFSpace.lg)
            }
            .scrollDisabled(true)
            .scrollIndicators(.hidden)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading titles")
    }
}

struct HeroSkeleton: View {
    var body: some View {
        ZStack(alignment: .bottomLeading) {
            MFColor.surfaceSunken

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                SkeletonBlock(cornerRadius: MFRadius.xs)
                    .frame(width: 90, height: 12)

                SkeletonBlock(cornerRadius: MFRadius.sm)
                    .frame(width: 260, height: 42)

                SkeletonBlock(cornerRadius: MFRadius.xs)
                    .frame(width: 190, height: 14)

                SkeletonBlock(cornerRadius: MFRadius.xs)
                    .frame(maxWidth: 420)
                    .frame(height: 14)

                SkeletonBlock(cornerRadius: MFRadius.xs)
                    .frame(maxWidth: 340)
                    .frame(height: 14)

                HStack(spacing: MFSpace.sm) {
                    SkeletonBlock(cornerRadius: MFRadius.md)
                        .frame(width: 156, height: MFSize.buttonHeight)
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                }
                .padding(.top, MFSpace.xs)
            }
            .padding(MFSpace.lg)
            .padding(.bottom, MFSpace.xl)
        }
        .frame(height: MFSize.heroHeightPhone)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading featured title")
    }
}
