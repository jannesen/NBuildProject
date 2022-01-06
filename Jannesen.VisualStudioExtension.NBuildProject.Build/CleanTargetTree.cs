using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Jannesen.VisualStudioExtension.NBuildProject.Build.Library;

namespace Jannesen.VisualStudioExtension.NBuildProject.Build
{
    public class CleanTargetTree: BaseTask
    {
        [Required]
        public                  string              TargetDirectory             { get; set;  }

        protected   override    bool                Run()
        {
            string      directory = FullFileName(TargetDirectory);

            if (!Directory.Exists(directory)) {
                return true;
            }

            Log.LogMessage(MessageImportance.High, "Cleanup tree: " + directory);
            for (var i = 0; i < 3 ; i++) {
                if (_cleanupDirectory(directory, false)) {
                    return true;
                }
                Log.LogWarning("Cleanup failed. Retry...");
                Thread.Sleep(1000);
            }

            return _cleanupDirectory(directory, true);
        }

        private                 bool                _cleanupDirectory(string directory, bool logError)
        {
            List<FileSystemInfo>    entrys;

            try {
                entrys = new List<FileSystemInfo>((new DirectoryInfo(directory)).EnumerateFileSystemInfos());
            }
            catch(DirectoryNotFoundException) {
                return true;
            }

            bool    rtn = true;

            foreach (FileSystemInfo entry in entrys) {
                if ((entry.Attributes & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System)) == FileAttributes.Directory) {
                    string  path = directory + "\\" + entry.Name;

                    if (_cleanupDirectory(path, logError)) {
                        try {
                            Directory.Delete(path);
                        }
                        catch(Exception err) {
                            if (logError) {
                                Log.LogError("Can't delete directory '" + path + "': " + err.Message);
                            }
                            rtn = false;
                        }
                    }
                    else {
                        rtn = false;
                    }
                }
            }

            foreach (FileSystemInfo entry in entrys) {
                if ((entry.Attributes & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System)) == (FileAttributes)0) {
                    string  path = directory + "\\" + entry.Name;

                    try {
                        File.Delete(path);
                    }
                    catch(Exception err) {
                        if (logError) {
                            Log.LogError("Can't delete file '" + path + "': " + err.Message);
                        }
                        rtn = false;
                    }
                }
            }

            return rtn;
        }
    }
}
