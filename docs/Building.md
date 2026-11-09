# Building from source

Use Visual Studio with the .NET desktop build tools and .NET 10 SDK. Supply your own SPT 4.1.6 installation with Anvil WebOverlay 1.12.0 or newer. Game and third-party binaries are not redistributed here.

Build Release with Visual Studio MSBuild, passing `/p:SptRoot="X:\your\SPT"` and `/restore`. Use the solution at the repository root. Release builds do not install into the game. The download ZIP contains the matching runtime files and custom asset bundles.
