// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "AthenaNative",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "AthenaNative", targets: ["AthenaNative"]),
        .library(name: "AthenaNativeCore", targets: ["AthenaNativeCore"])
    ],
    targets: [
        .target(
            name: "AthenaNativeCore",
            path: "Sources/AthenaNativeCore"
        ),
        .executableTarget(
            name: "AthenaNative",
            dependencies: ["AthenaNativeCore"],
            path: "Sources/AthenaNative"
        ),
        .testTarget(
            name: "AthenaNativeCoreTests",
            dependencies: ["AthenaNativeCore"],
            path: "Tests/AthenaNativeCoreTests"
        )
    ]
)
