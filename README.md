# The Unreal Engine Tool

UET (Unreal Engine Tool) makes building and testing Unreal Engine projects and plugins easy. It can distribute builds with BuildGraph, remotely execute automation tests on device and across multiple machines, handles fault tolerance and automatic retries and generally makes the whole experience of automating Unreal Engine a lot more pleasant.

UET is the successor to the [Unreal Engine Scripts](https://src.redpoint.games/redpointgames/unreal-engine-scripts) which are now deprecated. UET is almost entirely compatible with the existing `BuildConfig.json` files you were using with the Unreal Engine Scripts, except that the `Type` property is now mandatory (it was previously optional and defaulted to "Plugin"; you must now set it to "Plugin" when building a plugin).

UET can now also perform simple builds of projects and plugins without a `BuildConfig.json` file. Simply run `uet build` for a project or `uet build -e <engine version>` for a plugin, and it will build your project or plugin with BuildGraph.

## Download

You can download the latest version of UET from the links below.

- [Latest Windows (x64)](https://src.redpoint.games/api/v4/projects/242/jobs/artifacts/main/raw/UET/uet/bin/Release/net7.0/win-x64/publish/uet.exe?job=Build%2C%20Test%2C%20Package%20and%20Push)
- [Latest macOS (ARM64)](https://src.redpoint.games/api/v4/projects/242/jobs/artifacts/main/raw/UET/uet/bin/Release/net7.0/osx.11.0-arm64/publish/uet?job=Build%2C%20Test%2C%20Package%20and%20Push)

Once you have UET downloaded, you can install it into your PATH by running `uet upgrade`. You can also use this command later on to update to the latest version.

UET will respect the `UETVersion` property in `BuildConfig.json` files. When you run `uet upgrade`, this property will be updated/set if a `BuildConfig.json` file exists. If the property is present, UET will automatically download the target version and re-execute the command under that version. This ensures that all of your team members and build servers are using the same version of UET to build your project, without you having to manually sync `uet.exe`.

## How to use it

This tool is still a work-in-progress, so better documentation is still in the works. In the meantime, the [documentation for Unreal Engine Scripts](https://src.redpoint.games/redpointgames/unreal-engine-scripts/-/wikis/home) is pretty good if you want to set up a `BuildConfig.json` for your project or plugin.

The big difference between the Unreal Engine Scripts and UET is how you invoke it. Previously you'd add the build scripts to your repository as a submodule, and invoke them with `Build.ps1` and `Generate.ps1`. Now you use `uet build` for both cases, and you don't need to add anything as a submodule of your Git repository. Once you get `uet` onto your PATH somewhere (by downloading it and running `uet upgrade`), it will be available in every command prompt and can be automatically updated and version sync'd with your project.

To get help on the build command, run `uet build --help`.

To locally inside a folder that has `BuildConfig.json` set up, run `uet build -d <distribution> -e <engine>`.

To export the build for GitLab, run `uet build -d <distribution> -e <engine> -x gitlab --executor-output-file .export.gitlab-ci.yml --windows-shared-storage-path <network share> --mac-shared-storage-path <network mount>`.

You can also add the options `--test`, `--deploy` and `--strict-includes` to vary how the build is executed. If you're building a plain project or plugin (without `BuildConfig.json`), you can also add `--shipping` which will build the project or plugin for Shipping instead of Development.

## License

UET is licensed under the MIT license.