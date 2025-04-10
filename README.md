# The Unreal Engine Tool

UET (Unreal Engine Tool) makes building and testing Unreal Engine projects and plugins easy. It can distribute builds with BuildGraph, remotely execute automation tests on device and across multiple machines, handles fault tolerance and automatic retries and generally makes the whole experience of automating Unreal Engine a lot more pleasant.

UET is the successor to the [Unreal Engine Scripts](https://src.redpoint.games/redpointgames/unreal-engine-scripts) which are now deprecated.

UET can perform simple builds of projects and plugins without a `BuildConfig.json` file. Simply run `uet build` for a project or `uet build -e <engine version>` for a plugin, and it will build your project or plugin with BuildGraph.

## Download

You can download the latest version of UET from the links below.

- [Latest Windows (x64)](https://github.com/RedpointGames/uet/releases/latest/download/uet.exe)
- [Latest macOS (ARM64)](https://github.com/RedpointGames/uet/releases/latest/download/uet)
- [Latest Linux (x64)](https://github.com/RedpointGames/uet/releases/latest/download/uet.linux)

Once you have UET downloaded, you can install it into your PATH by running `uet upgrade`. You can also use this command later on to update to the latest version.

UET will respect the `UETVersion` property in `BuildConfig.json` files. When you run `uet upgrade`, this property will be updated/set if a `BuildConfig.json` file exists. If the property is present, UET will automatically download the target version and re-execute the command under that version. This ensures that all of your team members and build servers are using the same version of UET to build your project, without you having to manually sync `uet.exe`.

## How to use it

UET can be used to build Unreal Engine projects and plugins. It supports a simplified mode where you just build the project or plugin directly, and an advanced mode where you set up a `BuildConfig.json` file that has the full set of options available.

### Building a .uproject or .uplugin

The easiest way to use UET is by using `uet build` to build a `.uproject` or `.uplugin` file. For projects, all you need to do is navigate to the directory the project is in and run:

```
uet build
```

The target engine will be detected from the `.uproject` file. For plugins, you also need to specify the engine:

```
uet build -e 5.5
```

By default, only the host platform is built. You can build additional platforms by using the `--platform` option:

```
uet build --platform Android --platform Linux ...
```

If you want to build for Shipping instead of Development, use `--shipping`:

```
uet build --shipping
```

These options can be combined. For more information run `uet build --help`.

Typically for testing out UET this is enough, but if you want to test or deploy your code, you'll need to set up a `BuildConfig.json` file.

### Building a BuildConfig.json file

This tool is still a work-in-progress, so better documentation is still in the works. In the meantime, the [documentation for Unreal Engine Scripts](https://src.redpoint.games/redpointgames/unreal-engine-scripts/-/wikis/home) is pretty good if you want to set up a `BuildConfig.json` for your project or plugin.

To build locally inside a folder that has `BuildConfig.json` set up, run `uet build -d <distribution> -e <engine>`.

To export the build for GitLab, run `uet build -d <distribution> -e <engine> -x gitlab --executor-output-file .export.gitlab-ci.yml --windows-shared-storage-path <network share> --mac-shared-storage-path <network mount>`.

You can also add the options `--test`, `--deploy` and `--strict-includes` to vary how the build is executed. 

## Migrating from Unreal Engine Scripts

The big difference between the Unreal Engine Scripts and UET is how you invoke it. Previously you'd add the build scripts to your repository as a submodule, and invoke them with `Build.ps1` and `Generate.ps1`. Now you use `uet build` for both cases, and you don't need to add anything as a submodule of your Git repository. Once you get `uet` onto your PATH somewhere (by downloading it and running `uet upgrade`), it will be available in every command prompt and can be automatically updated and version sync'd with your project.

UET is almost entirely compatible with the existing `BuildConfig.json` files you were using with the Unreal Engine Scripts, except that the `Type` property is now mandatory (it was previously optional and defaulted to "Plugin"; you must now set it to "Plugin" when building a plugin).

## License

UET is licensed under the MIT license.