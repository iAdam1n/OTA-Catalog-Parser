﻿/*
 * Copyright (c) 2017 Dialexio
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify,
 * merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
 * CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
using Claunia.PropertyList;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Octothorpe
{
	public class Parser
	{
		private static bool DeviceIsWatch = false,
			showBeta = false,
			ModelNeedsChecking = false,
			wikiMarkup = false;
		private static Dictionary<string, uint> BuildNumberRowspan = new Dictionary<string, uint>(),
			DateRowspan = new Dictionary<string, uint>(),
			MarketingVersionRowspan = new Dictionary<string, uint>(),
			OSVersionRowspan = new Dictionary<string, uint>();
		private static Dictionary<string, Dictionary<string, uint>> FileRowspan = new Dictionary<string, Dictionary<string, uint>>(),// url, <PrereqOS, count> 
			PrereqBuildRowspan = new Dictionary<string, Dictionary<string, uint>>(), // DeclaredBuild, <PrereqBuild, count>
			PrereqOSRowspan = new Dictionary<string, Dictionary<string, uint>>(); // DeclaredBuild, <PrereqOS, count>
		private static readonly List<OTAPackage> Packages = new List<OTAPackage>();
		private static string device, model, plist;
		private static Version max, minimum;

		public string Device
		{
			set { device = value; }
		}

		public Version Maximum
		{
			set { max = value; }
		}

		public Version Minimum
		{
			set { minimum = value; }
		}

		public string Model
		{
			set
			{
				model = value;
				ModelNeedsChecking = Regex.IsMatch(model, @"iPhone8,(1|2|4)");
			}
		}

		public string Plist
		{
			get { return plist; }
			set { plist = value; }
		}

		public bool ShowBeta
		{
			set { showBeta = value; }
		}

		public bool WikiMarkup
		{
			set { wikiMarkup = value; }
		}

		public string ParsePlist()
		{
			ErrorCheck();

			AddEntries();
			SortEntries();

			if (wikiMarkup)
			{
				CountRowspan();
				return OutputWikiMarkup();
			}

			else
				return OutputHumanFormat();
		}

		private static void AddEntries()
		{
			bool matched;
			NSDictionary root;
			OTAPackage package;

			// Load the PLIST.
			if (plist.StartsWith("http://mesu.apple.com/assets/"))
			{
				WebClient Fido = new WebClient();
				root = (NSDictionary)PropertyListParser.Parse(Fido.DownloadData(plist));
				Fido.Dispose();
			}

			else if (plist.Contains("://"))
				throw new ArgumentException("notmesu");

			else
				root = (NSDictionary)PropertyListParser.Parse(plist);

			// Look at every item in the NSArray named "Assets."
			foreach (NSObject entry in ((NSArray)root.ObjectForKey("Assets")).GetArray())
			{
				matched = false;
				package = new OTAPackage((NSDictionary)entry); // Feed the info into a custom object so we can easily pull info and sort.
				
				// Beta check.
				if (showBeta == false && package.ActualReleaseType > 0)
					continue;

				// For wikiMarkup markup: If a beta has two entries
				// (one for betas, one for non-betas), don't count it twice.
				if (wikiMarkup &&
					package.ReleaseType != "Public" &&
					package.BetaNumber > 0 &&
					package.DocumentationID != "iOS7Seed6")
						continue;

				// Device check.
				matched = package.SupportedDevices.Contains(device);

				// Model check, if needed.
				if (matched && ModelNeedsChecking)
				{
					matched = false; // Skipping unless we can verify we want it.

					// Make sure "SupportedDeviceModels" exists before checking it.
					if (package.SupportedDeviceModels.Count > 0)
						matched = (package.SupportedDeviceModels.Contains(model));
				}

				// If it's still a match, check the OS version.
				// If the OS version doesn't fit what we're
				// searching for, continue to the next entry.
				if (matched)
				{
					if (max != null && max.CompareTo(new Version(package.MarketingVersion)) < 0)
						continue;
					if (minimum != null && minimum.CompareTo(new Version(package.MarketingVersion)) > 0)
						continue;

					// It survived the checks!
					Packages.Add(package);
				}
			}
		}

		private static void Cleanup()
		{
			BuildNumberRowspan.Clear();
			DateRowspan.Clear();
			FileRowspan.Clear();
			MarketingVersionRowspan.Clear();
			OSVersionRowspan.Clear();
			Packages.Clear();
			PrereqBuildRowspan.Clear();
			PrereqOSRowspan.Clear();
		}

		private static void CountRowspan()
		{
			Dictionary<string, uint> fileNestedCount, prereqBuildNestedCount, prereqOSNestedCount;

			// Count the rowspans for wikiMarkup markup.
			foreach (OTAPackage entry in Packages)
			{
				fileNestedCount = new Dictionary<string, uint>();
				prereqBuildNestedCount = new Dictionary<string, uint>();
				prereqOSNestedCount = new Dictionary<string, uint>();

				// Build
				// Increment the count if it exists.
				// If not, add the first tally.
				if (BuildNumberRowspan.ContainsKey(entry.DeclaredBuild))
					BuildNumberRowspan[entry.DeclaredBuild]++;

				else
					BuildNumberRowspan.Add(entry.DeclaredBuild, 1);


				// Date (Count ActualBuild() and not Date() because x.0 GM and x.1 beta can technically be pushed at the same time.)
				// Increment the count if it exists.
				// If not, add the first tally.
				if (DateRowspan.ContainsKey(entry.ActualBuild))
					DateRowspan[entry.ActualBuild]++;

				else
					DateRowspan.Add(entry.ActualBuild, 1);


				// File URL
				// Load nested HashMap into a temporary variable, if it exists.
				if (FileRowspan.ContainsKey(entry.URL))
					fileNestedCount = FileRowspan[entry.URL];

				// Increment the count if it exists.
				// If not, add the first tally.
				if (fileNestedCount.ContainsKey(entry.PrerequisiteVer))
					fileNestedCount[entry.PrerequisiteVer]++;

				else
					fileNestedCount.Add(entry.PrerequisiteVer, 1);

				FileRowspan[entry.URL] = fileNestedCount;


				// Marketing version
				// Increment the count if it exists.
				// If not, add the first tally.
				if (MarketingVersionRowspan.ContainsKey(entry.MarketingVersion))
					MarketingVersionRowspan[entry.MarketingVersion]++;

				else
					MarketingVersionRowspan.Add(entry.MarketingVersion, 1);


				// OS version
				// Increment the count if it exists.
				// If not, add the first tally.
				if (OSVersionRowspan.ContainsKey(entry.OSVersion))
					OSVersionRowspan[entry.OSVersion]++;

				else
					OSVersionRowspan.Add(entry.OSVersion, 1);


				// Prerequisite OS version
				if (PrereqOSRowspan.ContainsKey(entry.DeclaredBuild))
					prereqOSNestedCount = PrereqOSRowspan[entry.DeclaredBuild];

				// Increment the count if it exists.
				// If not, add the first tally.
				if (prereqOSNestedCount.ContainsKey(entry.PrerequisiteVer))
					prereqOSNestedCount[entry.PrerequisiteVer]++;

				else
					prereqOSNestedCount.Add(entry.PrerequisiteVer, 1);

				PrereqOSRowspan[entry.DeclaredBuild] = prereqOSNestedCount;


				// Prerequisite Build version
				if (PrereqBuildRowspan.ContainsKey(entry.DeclaredBuild))
					prereqBuildNestedCount = PrereqBuildRowspan[entry.DeclaredBuild];

				// Increment the count if it exists.
				// If not, add the first tally.
				if (prereqBuildNestedCount.ContainsKey(entry.PrerequisiteBuild))
					prereqBuildNestedCount[entry.PrerequisiteBuild]++;

				else
					prereqBuildNestedCount.Add(entry.PrerequisiteBuild, 1);

				PrereqBuildRowspan[entry.DeclaredBuild] = prereqBuildNestedCount;
			}

			fileNestedCount = null;
			prereqBuildNestedCount = null;
			prereqOSNestedCount = null;
		}

		private static void ErrorCheck()
		{
			// Device check.
			if (device == null || Regex.IsMatch(device, @"(AppleTV|iPad|iPhone|iPod|Watch)(\d)?\d,\d") == false)
				throw new ArgumentException("device");

			DeviceIsWatch = Regex.IsMatch(device, @"Watch\d,\d");
			ModelNeedsChecking = Regex.IsMatch(device, "iPhone8,(1|2|4)");

			// Model check.
			if (ModelNeedsChecking && (model == null || Regex.IsMatch(model, @"[JKMNP]\d((\d)?){2}[A-Za-z]?AP") == false))
				throw new ArgumentException("model");
		}

		private static string OutputHumanFormat()
		{
			StringBuilder Output = new StringBuilder();
			string osName;

			// So we don't add on to a previous run.
			Output.Length = 0;

			foreach (OTAPackage package in Packages)
			{
				if (DeviceIsWatch)
					osName = "watchOS ";

				else if (device.StartsWith("AppleTV"))
					osName = (Regex.Match(device, @"AppleTV(2,1|3,1|3,2)").Success) ? "Apple TV software " : "tvOS ";
				
				else
					osName = "iOS ";

				// Output OS version and build.
				Output.Append(osName + package.MarketingVersion);

				// Give it a beta label (if it is one).
				if (package.ActualReleaseType > 0) {
					switch (package.ActualReleaseType) {
						case 1:
							Output.Append(" Public Beta");
							break;
						case 2:
							Output.Append(" beta");
							break;
						case 3:
							Output.Append(" Carrier Beta");
							break;
						case 4:
							Output.Append(" Internal");
							break;
					}

					// Don't print a 1 if this is the first beta.
					if (package.BetaNumber > 1)
						Output.Append(" " + package.BetaNumber);
				}

				Output.AppendLine(" (Build " + package.ActualBuild + ')');
				Output.AppendLine("Listed as: " + package.OSVersion + " (Build " + package.DeclaredBuild + ')');
				Output.AppendLine("Reported Release Type: " + package.ReleaseType);

				// Print prerequisites if there are any.
				if (package.IsUniversal)
					Output.AppendLine("Requires: Not specified");

				else
					Output.AppendLine(string.Format("Requires: {0} (Build {1})", package.PrerequisiteVer, package.PrerequisiteBuild));

				// Date as extracted from the url.
				Output.AppendLine(string.Format("Timestamp: {0}/{1}/{2}", package.Date('y'), package.Date('m'), package.Date('d')));

				// Compatibility Version.
				Output.AppendLine("Compatibility Version: " + package.CompatibilityVersion);

				// Print out the url and file Size.
				Output.AppendLine("URL: " + package.URL);
				Output.AppendLine("File size: " + package.Size + Environment.NewLine);
			}

			Cleanup();

			return Output.ToString();
		}

		private static string OutputWikiMarkup()
		{
			Match name;
			string fileName, NewTableCell = "| ";
			StringBuilder Output = new StringBuilder();

			// So we don't add on to a previous run.
			Output.Length = 0;

			foreach (OTAPackage package in Packages)
			{
				// Obtain the file name.
				fileName = string.Empty;
				name = Regex.Match(package.URL, @"[0-9a-f]{40}\.zip");

				if (name.Success)
					fileName = name.ToString();

				// Let us begin!
				Output.AppendLine("|-");

				// Marketing Version for Apple Watch (1st generation)
				if (Regex.Match(device, @"Watch1,\d").Success && MarketingVersionRowspan.ContainsKey(package.MarketingVersion)) {
					Output.Append(NewTableCell);

					// Only give rowspan if there is more than one row with the OS version.
					if (MarketingVersionRowspan[package.MarketingVersion] > 1)
						Output.Append("rowspan=\"" + MarketingVersionRowspan[package.MarketingVersion] + "\" | ");

					Output.Append(package.MarketingVersion);

					// Give it a beta label (if it is one).
					if (package.ActualReleaseType > 0)
					{
						switch (package.ActualReleaseType)
						{
							case 1:
								Output.Append(" Public Beta");
								break;
							case 2:
							case 3:
								Output.Append(" beta");
								break;
							case 4:
								Output.Append(" Internal");
								break;
						}

						// Don't print a 1 if this is the first beta.
						if (package.BetaNumber > 1)
							Output.Append(" " + package.BetaNumber);
					}

					Output.AppendLine();

					//Remove the count since we're done with it.
					MarketingVersionRowspan.Remove(package.MarketingVersion);
				}

				// Output OS version.
				if (OSVersionRowspan.ContainsKey(package.OSVersion))
				{
					Output.Append(NewTableCell);

					// Create a filler for Marketing Version, if this is a 32-bit Apple TV.
					if (Regex.Match(device, "AppleTV(2,1|3,1|3,2)").Success && OSVersionRowspan[package.OSVersion] > 1)
						Output.AppendLine("| rowspan=\"" + OSVersionRowspan[package.OSVersion] + "\" | [MARKETING VERSION]");

					// Creating the rowspan attribute, provided:
					// - there is more than one entry for the version
					// - this isn't a universal Apple Watch entry
					if (OSVersionRowspan[package.OSVersion] > 1)
					{
						if (DeviceIsWatch == false || package.IsUniversal == false)
							Output.Append("rowspan=\"" + OSVersionRowspan[package.OSVersion] + "\" | ");
					}

					Output.Append(package.OSVersion);

					// Give it a beta label (if it is one).
					if (package.ActualReleaseType > 0)
					{
						switch (package.ActualReleaseType)
						{
							case 1:
								Output.Append(" Public Beta");
								break;
							case 2:
							case 3:
								Output.Append(" beta");
								break;
							case 4:
								Output.Append(" Internal");
								break;
						}

						// Don't print a 1 if this is the first beta.
						if (package.BetaNumber > 1)
							Output.Append(package.BetaNumber);
					}

					Output.AppendLine();

					//Remove the count when we're done with it.
					if (DeviceIsWatch == false || package.IsUniversal == false)
						OSVersionRowspan.Remove(package.OSVersion);

					else
						OSVersionRowspan[package.OSVersion]--;
				}

				// Output build number.
				if (BuildNumberRowspan.ContainsKey(package.DeclaredBuild))
				{
					Output.Append(NewTableCell);

					// Only give rowspan if there is more than one row with the OS version.
					// Count DeclaredBuild() instead of ActualBuild() so the entry pointing betas to the final build is treated separately.
					if (BuildNumberRowspan[package.DeclaredBuild] > 1)
						Output.Append("rowspan=\"" + BuildNumberRowspan[package.DeclaredBuild] + "\" | ");

					//Remove the count since we're done with it.
					BuildNumberRowspan.Remove(package.DeclaredBuild);

					Output.Append(package.ActualBuild);

					// Do we have a false build number? If so, add a footnote reference.
					if (package.IsHonestBuild == false)
						Output.Append("<ref name=\"fakefive\" />");

					Output.AppendLine();
				}

				// Printing prerequisite version
				if (PrereqOSRowspan.ContainsKey(package.DeclaredBuild) && PrereqOSRowspan[package.DeclaredBuild].ContainsKey(package.PrerequisiteVer))
				{
					Output.Append(NewTableCell);

					// Is there more than one of this prerequisite version tallied?
					if (PrereqOSRowspan[package.DeclaredBuild][package.PrerequisiteVer] > 1)
					{
						Output.Append("rowspan=\"" + PrereqOSRowspan[package.DeclaredBuild][package.PrerequisiteVer] + "\" ");
						PrereqOSRowspan[package.DeclaredBuild].Remove(package.PrerequisiteVer);

						if (package.IsUniversal == false)
							Output.Append(NewTableCell);
					}

					// Print out the cell text
					if (package.IsUniversal)
						Output.AppendLine("colspan=\"2\" {{n/a}}");

					else
					{
						// If this is a GM, print the link to Golden Master.
						if (package.PrerequisiteVer.Contains(" GM"))
							Output.AppendLine(package.PrerequisiteVer.Replace("GM", "[[Golden Master|GM]]"));

						// Very quick check if prerequisite is a beta. This is not bulletproof.
						else if (Regex.Match(package.PrerequisiteBuild, OTAPackage.REGEX_BETA).Success && package.PrerequisiteVer.Contains("beta") == false)
							Output.AppendLine(package.PrerequisiteVer + " beta #");

						else
							Output.AppendLine(package.PrerequisiteVer);
					}
				}

				// Printing prerequisite build
				if (package.IsUniversal == false
					&& PrereqBuildRowspan.ContainsKey(package.DeclaredBuild)
					&& PrereqBuildRowspan[package.DeclaredBuild].ContainsKey(package.PrerequisiteBuild))
				{
					Output.Append(NewTableCell);

					// Is there more than one of this prerequisite build tallied?
					// Also do not use rowspan if the prerequisite build is a beta.
					if (PrereqBuildRowspan[package.DeclaredBuild][package.PrerequisiteBuild] > 1)
					{
						Output.Append("rowspan=\"" + PrereqBuildRowspan[package.DeclaredBuild][package.PrerequisiteBuild] + "\" | ");
						PrereqBuildRowspan[package.DeclaredBuild].Remove(package.PrerequisiteBuild);
					}

					Output.AppendLine(package.PrerequisiteBuild);
				}

				if (package.CompatibilityVersion > 0)
					Output.AppendLine(NewTableCell + package.CompatibilityVersion);

				// Date as extracted from the url. Using the same rowspan count as build.
				// (3.1.1 had two builds released on different dates for iPod touch 3G.)
				if (DateRowspan.ContainsKey(package.ActualBuild))
				{
					Output.Append(NewTableCell);

					// Only give rowspan if there is more than one row with the OS version.
					if (DateRowspan[package.ActualBuild] > 1)
					{
						Output.Append("rowspan=\"" + DateRowspan[package.ActualBuild] + "\" | ");
						DateRowspan.Remove(package.ActualBuild); //Remove the count since we already used it.
					}

					Output.AppendLine("{{date|" + package.Date('y') + '|' + package.Date('m') + '|' + package.Date('d') + "}}");
				}

				// Release Type.
				if (DeviceIsWatch == false)
				{
					switch (package.ActualReleaseType)
					{
						case 1:
						case 2:
							Output.AppendLine("| Beta");
							break;
						case 3:
							Output.AppendLine("| Carrier");
							break;
						case 4:
							Output.AppendLine("| Internal");
							break;
						default:
							if (package.ReleaseType != "Public")
								Output.AppendLine("| Beta");
	
							else
								Output.AppendLine("| {{n/a}}");
							break;
					}
				}

				if (FileRowspan.ContainsKey(package.URL) && FileRowspan[package.URL].ContainsKey(package.PrerequisiteVer))
				{
					Output.Append(NewTableCell);

					// Is there more than one of this prerequisite version tallied?
					// Also do not use rowspan if the prerequisite build is a beta.
					if (FileRowspan[package.URL][package.PrerequisiteVer] > 1)
						Output.Append("rowspan=\"" + FileRowspan[package.URL][package.PrerequisiteVer] + "\" | ");

					Output.Append('[' + package.URL + ' ' + fileName + ']' + Environment.NewLine + NewTableCell);

					//Print file Size.
					// Only give rowspan if there is more than one row with the OS version.
					if (FileRowspan[package.URL][package.PrerequisiteVer] > 1)
						Output.Append("rowspan=\"" + FileRowspan[package.URL][package.PrerequisiteVer] + "\" | ");

					Output.AppendLine(package.Size);

					//Remove the count since we're done with it.
					FileRowspan[package.URL].Remove(package.PrerequisiteVer);
				}
			}

			Cleanup();

			return Output.ToString();
		}

		private static void SortEntries()
		{
			Packages.Sort
			(
				delegate(OTAPackage one, OTAPackage two)
				{
					return one.SortingString.CompareTo(two.SortingString);
				}
			);
		}
	}
}
