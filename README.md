# OTA Catalog Parser
This program lets you view an OTA update catalog for [audioOS](https://mesu.apple.com/assets/audio/com_apple_MobileAsset_SoftwareUpdate/com_apple_MobileAsset_SoftwareUpdate.xml), [iOS](https://mesu.apple.com/assets/com_apple_MobileAsset_SoftwareUpdate/com_apple_MobileAsset_SoftwareUpdate.xml), [tvOS](https://mesu.apple.com/assets/tv/com_apple_MobileAsset_SoftwareUpdate/com_apple_MobileAsset_SoftwareUpdate.xml), and [watchOS](https://mesu.apple.com/assets/watch/com_apple_MobileAsset_SoftwareUpdate/com_apple_MobileAsset_SoftwareUpdate.xml) in a more pleasant format. It can also output the information in a format suitable for entry on [The Apple Wiki](https://theapplewiki.com/wiki/OTA_Updates).

It is also capable of making/receiving requests to Apple's [Pallas server](https://gdmf.apple.com/v2/assets), which also provides OTA updates.

## Program Requirements
The Mac version requires macOS v10.9 (Mavericks) or newer. It is built with Xamarin.Mac; no additional downloads are required.

The Windows version requires .NET Framework 4.7.2 or newer. (Technically, the program will compile for .NET Framework 4.6.1, but will require significantly more DLLs to be distributed alongside it.)

The command-line version is cross-platform and requires either [Mono](http://www.mono-project.com/) or .NET Framework 4.7.2 or newer to be installed. (Technically, the program will compile for .NET Framework 4.6.1, but will require significantly more DLLs to be distributed alongside it.)

([Link to .NET Framework 4.8 for Windows 7 and newer.](https://support.microsoft.com/kb/4503548))

## Command-Line Arguments (CLI version only)
If no arguments are specified, the program will return the following information about the arguments that it uses.

### Required Arguments
* `-d <device>` specifies what device you're looking for. This argument is looking for a value like "iPad2,3" or "iPod7,1." (If you do not know what value to use, you may refer to the identifiers listed on [The Apple Wiki](https://theapplewiki.com/wiki/Models).)
* `-f <file>` specifies the location of the OTA update catalog. This may be either an XML file saved on your computer, or a mesu.apple.com URL.
* `-m <model>` specifies what device you're looking for. This argument is looking for a value like "N71AP" or "N66mAP." (If you do not know what value to use, you may refer to the internal names listed on [The Apple Wiki](https://theapplewiki.com/wiki/Models).)
* `-pb <OS build>` specifies the build that you are starting a query of Pallas requests from. Both this and `-pv` are required for querying Pallas requests only.
* `-pv <OS version>` specifies the version that you are starting a query of Pallas requests from. Both this and `-pb` are required for querying Pallas requests only.

### Optional Arguments
* `-b` specifies that you would like to see beta releases. By default, this program does not display beta releases.
* `-max <OS version>` specifies the _highest_ version of iOS you're looking for. This argument is looking for a value like "4.3" or "8.0.1."
* `-min <OS version>` _(renamed from_ `-o` _as of version 0.3)_ specifies the _lowest_ version of iOS you're looking for. This argument is looking for a value like "4.3" or "8.0.1."
* `-t` adds the table headers, if `-w` is specified.
* `-w` allows you to see the results formatted more appropriately for [The Apple Wiki](https://theapplewiki.com/wiki/OTA_Updates). Manual editing may still be required (e.g. for "marketing version"), but this will make the burden more bearable.

## Source Code Information
When opening the solution in Visual Studio, one project will be incompatible, depending on your platform.

* Visual Studio for Windows may not open Octothorpe.Mac because it is a Xamarin.Mac project.
* Xamarin Studio/Visual Studio for Mac will not open Octothorpe.WPF because WPF is exclusive to Windows.

## Broken Output
Apple may make changes to their property lists that break this program. Apple's ability to do so is limited since they need to ensure compatibility with older firmwares, but nevertheless remains a possibility.

### BuildInfo.plist
Broken output most commonly happens with watchOS updates, especially with beta updates. The parser needs to know what is and isn't a beta in order to format it correctly, but Apple hasn't made that easy in the past.

This program utilizes a PLIST file, named "BuildInfo.plist," to override the information that Apple provides. Its format is simple:

* There is a dictionary for each OS branch (e.g. audioOS, iOS…).
* In each OS branch's dictionary are more dictionaries, named by the version number.
* In each version number's dictionary, there is another dictionary for each build number available.
* In each build number's dictionary, you may specify the following keys:
 * "Beta" (what number beta it is; if it is not a beta, the value should be 0)
 * "Suffix" (if something should follow, e.g. "watchOS 3.1 beta 2 **Pre-release**")
 * "Date" (the release date for the software, as an integer)
 * "Devices" (if the entry should only be applied to certain devices)

Before reporting issues with the program's output, please make sure BuildInfo.plist contains information for the latest OS updates. For the Mac version, this file can be found at `OTA Parser.app/Contents/MonoBundle/BuildInfo.plist`.

## Licensing Information
This program is distributed under the MIT License.

This program utilizes the [Json.NET](https://www.newtonsoft.com/json), [Jwt.Net](https://github.com/jwt-dotnet/jwt), [plist-cil](https://github.com/claunia/plist-cil), and [RestSharp](https://restsharp.dev) libraries. Please refer to LICENSE.md for more details regarding the licenses for third-party libraries.
