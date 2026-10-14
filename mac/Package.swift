// swift-tools-version: 6.0
import PackageDescription
import Foundation

// These extra Testing flags are a Command-Line-Tools-only workaround.
// CLT ships Swift Testing outside the SDK framework search path.
// Use a joined -I flag: SwiftPM forwards it to its generated test runner.
// A test-target-only -F flag compiles tests but silently skips their execution.
// Also pass -F for the Foundation cross-import overlay used by database/date tests.
let cltFrameworks = "/Library/Developer/CommandLineTools/Library/Developer/Frameworks"
let needsTestingFrameworkPath = FileManager.default.fileExists(atPath: "\(cltFrameworks)/Testing.framework")
let testSwiftSettings: [SwiftSetting] = needsTestingFrameworkPath
    ? [.unsafeFlags(["-I\(cltFrameworks)/Testing.framework/Modules", "-F", cltFrameworks])] : []
let testLinkerSettings: [LinkerSetting] = needsTestingFrameworkPath
    ? [.linkedFramework("Testing"), .unsafeFlags(["-F", cltFrameworks, "-Xlinker", "-rpath", "-Xlinker", cltFrameworks,
                     "-Xlinker", "-rpath", "-Xlinker", "\(cltFrameworks)/../usr/lib"])] : []

let package = Package(
    name: "ToastFish",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "ToastFishCore", targets: ["ToastFishCore"]),
        .executable(name: "ToastFish", targets: ["ToastFish"]),
    ],
    targets: [
        .target(name: "ToastFishCore"),
        .executableTarget(name: "ToastFish", dependencies: ["ToastFishCore"]),
        .testTarget(
            name: "ToastFishCoreTests", dependencies: ["ToastFishCore"],
            swiftSettings: testSwiftSettings, linkerSettings: testLinkerSettings
        ),
    ]
)
