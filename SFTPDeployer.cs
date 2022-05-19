using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace HTMLBuilder
{
    public static class SFTPDeployer
    {
        private static async Task<IEnumerable<SftpFile>> ListRemoteDirectoryAsync(SftpClient client, string directory)
        {
            return await Task.Factory.FromAsync((c, o) => client.BeginListDirectory(directory, c, o), client.EndListDirectory, null);
        }

        private static void OnListComplete(Task<IEnumerable<SftpFile>> result, object? _, string distRoot, ref ConcurrentStack<SftpFile> toExplore, ref ConcurrentDictionary<string, SftpFile> files, ref SynchronizedCollection<Task<IEnumerable<SftpFile>>> tasks, string[] ignore)
        {
            foreach (var listing in result.Result.Where((l) => l.Name != "." && l.Name != ".." && !ignore.Any(i => Regex.IsMatch(l.FullName.RemoveFirst(distRoot), i))))
            {
                if (listing.IsDirectory)
                {
                    toExplore.Push(listing);
                }
                else if (listing.IsRegularFile)
                {
                    bool wasAdded = files.TryAdd(listing.FullName.RemoveFirst(distRoot + "/"), listing);
                    if (!wasAdded)
                    {
                        throw new Exception();
                    }
                }
            }
            tasks.Remove(result);
        }

        class RemoteExploreResult
        {
            public ConcurrentDictionary<string, SftpFile> Files;
            public List<SftpFile> Folders;

            public RemoteExploreResult(ConcurrentDictionary<string, SftpFile> files, List<SftpFile> folders)
            {
                Files = files;
                Folders = folders;
            }
        }

        private static async Task<RemoteExploreResult> ExploreRemoteRecursively(SftpClient client, string startDirectory, string[] ignore)
        {
            ConcurrentStack<SftpFile> toExplore = new();
            ConcurrentDictionary<string, SftpFile> files = new();
            List<SftpFile> directories = new();
            SynchronizedCollection<Task<IEnumerable<SftpFile>>> tasks = new();

            if (!client.Exists(startDirectory))
            {
                Console.WriteLine($"Remote directory {startDirectory} does not exist. Creating...");
                try
                {
                    client.CreateDirectory(startDirectory);
                }
                catch (Exception e)
                {
                    throw new Exception($"Exception while creating '{startDirectory}': {e.Message}");
                }
                return new(files, directories);
            }

            toExplore.Push(client.Get(startDirectory));

            while (!toExplore.IsEmpty || tasks.Count > 0)
            {
                if (toExplore.IsEmpty)
                {
                    await Task.WhenAny(new List<Task<IEnumerable<SftpFile>>>(tasks));
                    if (toExplore.IsEmpty) continue;
                }
                bool wasPopped = toExplore.TryPop(out SftpFile? current);
                if (!wasPopped) throw new Exception();

                directories.Add(current!);

                Console.WriteLine($"Requesting listing for '${current!.FullName}'");
                var task = ListRemoteDirectoryAsync(client, current.FullName);
                _ = task.ContinueWith((Task<IEnumerable<SftpFile>> t, object? s) => 
                    OnListComplete(t, s, startDirectory, ref toExplore, ref files, ref tasks, ignore), null, TaskContinuationOptions.ExecuteSynchronously);
                tasks.Add(task);
            }

            return new(files, directories);
        }

        private static LinkedList<string> ExploreLocalRecursively(string startDirectory, string[] ignore)
        {
            Stack<string> toExplore = new();
            LinkedList<string> files = new();
            toExplore.Push(startDirectory);

            Console.WriteLine("Discovering local files...");

            while (toExplore.Count > 0)
            {
                string current = toExplore.Pop();
                foreach (string file in Directory.GetFiles(current).Select(f => f.Replace('\\', '/')))
                {
                    if (ignore.Any(i => Regex.IsMatch(file.RemoveFirst(startDirectory), i)))
                    {
                        Console.WriteLine($"\tIgnoring local file '{file}'");
                        continue;
                    }
                    files.AddLast(file);
                }
                foreach (string directory in Directory.GetDirectories(current))
                {
                    if (ignore.Any(i => Regex.IsMatch(directory.RemoveFirst(startDirectory), i)))
                    {
                        Console.WriteLine($"\tIgnoring local directory '{directory}'");
                        continue;
                    }
                    toExplore.Push(directory);
                }
            }

            Console.WriteLine("Local discovery succeeded.");

            return files;
        }

        private static void CreateDirectoryRecursive(SftpClient client, string directory)
        {
            string[] paths = directory.Split('/');
            StringBuilder current = new(directory.Length);

            foreach (string path in paths)
            {
                current.Append("/" + path);
                string currentString = current.ToString();
                if (!client.Exists(currentString))
                {
                    try
                    {
                        client.CreateDirectory(currentString);
                    }
                    catch (SshException)
                    {
                        continue;
                    }
                }
            }
        }

        private static void UploadFiles(ref ConcurrentQueue<string> fileQueue, SftpClient client, string targetDirectory, string sourcePath)
        {
            while (fileQueue.TryDequeue(out string? file))
            {
                string dirName = Path.GetDirectoryName(Path.Combine(targetDirectory, file.RemoveFirst(sourcePath + "\\")))!.Replace('\\', '/');
                if (!client.Exists(dirName))
                {
                    Console.WriteLine($"Creating '{dirName}'");
                    CreateDirectoryRecursive(client, dirName);
                }

                using (var stream = System.IO.File.OpenRead(file))
                {
                    string localSource = Path.GetFullPath(file);
                    string remoteTarget = Path.Combine(targetDirectory, file.RemoveFirst(sourcePath + "\\")).Replace('\\', '/');
                    Console.WriteLine($"Starting upload:\n\t{localSource}\n\t{remoteTarget}");
                    client.UploadFile(stream, remoteTarget);
                }
            }
        }

        private static async Task UploadFilesAsync(SftpClient client, IEnumerable<string> files, string sourcePath, string targetDirectory, int threads)
        {
            ConcurrentQueue<string> fileQueue = new(files);
            List<Task> uploads = new(threads);

            for (int i = 0; i < threads; i++)
            {
                uploads.Add(Task.Run(() =>
                {
                    UploadFiles(ref fileQueue, client, targetDirectory, sourcePath);
                }));
            }
            await Task.WhenAll(uploads);
        }

        private static void FilterFiles(ref LinkedList<string> localFiles, ref Dictionary<string, SftpFile> remoteFiles, string localSourcePath)
        {
            List<string> toRemove = new();
            foreach (string localFile in localFiles)
            {
                string sourceLocalFile = localFile.RemoveFirst(localSourcePath + "/");
                if (remoteFiles.TryGetValue(sourceLocalFile, out SftpFile? fileInfo))
                {
                    FileInfo localFileInfo = new(localFile);
                    bool isSizeDifferent = fileInfo.Attributes.Size != localFileInfo.Length;
                    bool isNewer = fileInfo.Attributes.LastWriteTime < localFileInfo.LastWriteTime;

                    if (isSizeDifferent && isNewer)
                    {
                        Console.WriteLine($"Overwrite: {localFile}");
                    }
                    else
                    {
                        Console.WriteLine($"Skip:      {localFile}");
                        toRemove.Add(localFile);
                    }
                    remoteFiles.Remove(sourceLocalFile);
                }
            }
            
            foreach (string remove in toRemove)
            {
                localFiles.Remove(remove);
            }
            Console.WriteLine($"Skipped {toRemove.Count} files.");
        }

        public static void Deploy(Arguments.Argument[] args)
        {
            string distPath = Config.Read(null, "ftp.DistPath")[0];
            if (string.IsNullOrEmpty(distPath)) throw new ArgumentException("ftp.DistPath is empty");
            if (!Directory.Exists(distPath)) throw new ArgumentException($"ftp.DistPath points to a directory that does not exist '{Path.GetFullPath(distPath)}'.");
            string remotePath = Config.Read(null, "ftp.RemoteTargetPath")[0];
            if (string.IsNullOrEmpty(remotePath)) throw new ArgumentException("ftp.RemoteTargetPath is empty");
            string address = Config.Read(null, "ftp.Address")[0];
            if (string.IsNullOrEmpty(address)) throw new ArgumentException("ftp.Address is empty");
            string user = Config.Read(null, "ftp.UserName")[0];
            if (string.IsNullOrEmpty(user)) throw new ArgumentException("ftp.UserName is empty");
            string password = Config.Read(null, "ftp.Password")[0];
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("ftp.Password is empty");

            bool isDryRun = args.Length > 0 && args[0].IsOption && (args[0].Value == "dry" || args[0].Value == "d");

            string[] remoteIgnore = Config.Read(null, "ftp.RemoteIgnore")[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            string[] localIgnore = Config.Read(null, "ftp.DistIgnore")[0].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            LinkedList<string> localFiles = ExploreLocalRecursively(distPath, localIgnore);

            using (var client = new SftpClient(address, user, password))
            {
                Console.WriteLine($"Attempting connection to {address}...");
                try
                {
                    client.Connect();
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e.Message);
                }
                Console.WriteLine("Successfully connected.");

                var remoteExploreTask = ExploreRemoteRecursively(client, remotePath, remoteIgnore);
                remoteExploreTask.Wait();
                var remoteExplore = remoteExploreTask.Result;
                Dictionary<string, SftpFile> remoteFiles = new(remoteExplore.Files);
                List<SftpFile> remoteDirectories = remoteExplore.Folders;

                FilterFiles(ref localFiles, ref remoteFiles, distPath);
                if (!isDryRun)
                {
                    UploadFilesAsync(client, localFiles, distPath, remotePath, 4).Wait();
                    Console.WriteLine($"All uploads completed."); 
                }
                Console.WriteLine($"Cleaning {remoteFiles.Count} remaining files...");
                
                foreach ((string path, SftpFile file) in remoteFiles)
                {
                    Console.WriteLine($"\t{(isDryRun ? "(dry) " : "")}Deleting {file.FullName}");
                    if (!isDryRun)
                    {
                        file.Delete(); 
                    }
                }

                Console.WriteLine($"Cleaning empty folders...");

                for (int i = remoteDirectories.Count - 1; i >= 0; i--)
                {
                    SftpFile dir = remoteDirectories[i];
                    if (dir.Name == "." || dir.Name == ".." || client.ListDirectory(dir.FullName).Count(f => f.Name != "." && f.Name != "..") != 0)
                    {
                        continue;
                    }
                    Console.WriteLine($"\t{(isDryRun ? "(dry) " : "")}Deleting {dir.FullName}");
                    if (!isDryRun)
                    {
                        dir.Delete();
                    }
                }

                Console.WriteLine($"Deploy completed.");

                client.Disconnect();
            }
        }
    }
}
