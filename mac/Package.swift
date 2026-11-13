// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "PadPilot",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "PadPilot",
            path: "Sources/PadPilot",
            linkerSettings: [
                .linkedFramework("GameController"),
                .linkedFramework("CoreHaptics"),
                .linkedFramework("AppKit"),
            ]
        )
    ]
)
