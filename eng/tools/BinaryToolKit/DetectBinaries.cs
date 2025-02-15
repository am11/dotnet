// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Text.RegularExpressions;

namespace BinaryToolKit;

public static class DetectBinaries
{
    private const string Utf16Marker = "UTF-16";
    private const int ChunkSize = 4096;
    private static readonly Regex GitCleanRegex = new Regex(@"Would (remove|skip)( repository)? (.*)");

    public static async Task<List<string>> ExecuteAsync(string targetDirectory)
    {
        Log.LogInformation($"Detecting binaries in '{targetDirectory}'...");

        var matcher = new Matcher(StringComparison.Ordinal);
        matcher.AddInclude("**/*");
        matcher.AddExcludePatterns(await GetIgnoredPatternsAsync(targetDirectory));

        IEnumerable<string> matchingFiles = matcher.GetResultsInFullPath(targetDirectory);

        var tasks = matchingFiles
            .Select(async file =>
            {
                return await IsBinaryAsync(file) ? file.Substring(targetDirectory.Length + 1) : null;
            });

        var binaryFiles = (await Task.WhenAll(tasks)).OfType<string>().ToList();

        Log.LogInformation($"Finished binary detection.");

        return binaryFiles;
    }

    private static async Task<List<string>> GetIgnoredPatternsAsync(string targetDirectory)
    {
        string gitDirectory = Path.Combine(targetDirectory, ".git");
        bool isGitRepo = Directory.Exists(gitDirectory);

        try 
        {
            if (!isGitRepo)
            {
                // Configure a fake git repo to use so that we can run git clean -ndx
                await ExecuteProcessAsync("git", $"-C {targetDirectory} init -q");
            }

            await ExecuteProcessAsync("git", $"-C {targetDirectory} config --global safe.directory {targetDirectory}");

            string output = await ExecuteProcessAsync("git", $"-C {targetDirectory} clean -ndx");

            List<string> ignoredPaths = output.Split(Environment.NewLine)
                .Select(line => GitCleanRegex.Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups[3].Value)
                .ToList();

            if (isGitRepo)
            {
                ignoredPaths.Add(".git");
            }

            return ignoredPaths;
        }
        finally
        {
            // Ensure .git directory is deleted if it wasn't originally a git repo
            if (!isGitRepo && Directory.Exists(gitDirectory))
            {
                Directory.Delete(gitDirectory, true);
            }
        }
    }

    private static async Task<bool> IsBinaryAsync(string filePath)
    {
        // Using the GNU diff heuristic to determine if a file is binary or not.
        // For more details, refer to the GNU diff manual: 
        // https://www.gnu.org/software/diffutils/manual/html_node/Binary.html

        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader br = new BinaryReader(fs))
        {
            byte[] buffer = new byte[ChunkSize];
            int bytesRead = br.Read(buffer, 0, ChunkSize);
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    // Need to check that the file is not UTF-16 encoded
                    // because heuristic can return false positives
                    return await IsNotUTF16Async(filePath);
                }
            }
        }
        return false;
    }

    private static async Task<bool> IsNotUTF16Async(string file)
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            string output = await ExecuteProcessAsync("file",  $"\"{file}\"");
            output = output.Split(":")[1].Trim();

            if (output.Contains(Utf16Marker))
            {
                return false;
            }
        }
        return true;
    }

    private static async Task<string> ExecuteProcessAsync(string executable, string arguments)
    {
        ProcessStartInfo psi = new ()
        {
            FileName = executable,
            Arguments = arguments,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var proc = Process.Start(psi)!;

        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        if (!string.IsNullOrEmpty(error))
        {
            Log.LogError(error);
        }

        return output;
    }
}