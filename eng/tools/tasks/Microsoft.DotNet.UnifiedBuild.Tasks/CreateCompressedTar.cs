// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.UnifiedBuild.Tasks
{
    public class CreateCompressedTar : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string SourceDir { get; set; }

        [Required]
        public string DestinationFile { get; set; }

        [Required]
        public string CompressionType { get; set; }

        [Required]
        public string Format { get; set; }

        public override bool Execute()
        {
            try
            {
                if (File.Exists(DestinationFile))
                {
                    File.Delete(DestinationFile);
                }

                if (!Directory.Exists(SourceDir))
                {
                    Log.LogError($"Source directory does not exist: '{SourceDir}'");
                    return false;
                }

                if (!Enum.TryParse<TarEntryFormat>(Format, true, out TarEntryFormat targetFormat))
                {
                    Log.LogError($"Invalid Tar Format flag: '{Format}'. Valid options are Gnu, Pax, Ustar, or V7.");
                    return false;
                }

                string algorithm = CompressionType.Trim().ToLowerInvariant();
                if (algorithm is not ("gz" or "zstd"))
                {
                    Log.LogError($"Unsupported compression type: '{CompressionType}'. Valid choices are 'gz' or 'zstd'.");
                    return false;
                }

                using FileStream fs = new(DestinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan);

                if (algorithm == "gz")
                {
                    using GZipStream gzip = new(fs, CompressionLevel.Optimal);
                    TarFile.CreateFromDirectory(SourceDir, gzip, includeBaseDirectory: false, format: targetFormat);
                }
                else if (algorithm == "zstd")
                {
                    using ZstandardStream zstd = new(fs, CompressionLevel.Optimal);
                    TarFile.CreateFromDirectory(SourceDir, zstd, includeBaseDirectory: false, format: targetFormat);
                }

                Log.LogMessage(MessageImportance.High, $"Successfully generated a .{algorithm} tar archive format [{targetFormat}] at: {DestinationFile}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to compile archive output: {ex.Message}");
                return false;
            }

            return !Log.HasLoggedErrors;
        }
    }
}
