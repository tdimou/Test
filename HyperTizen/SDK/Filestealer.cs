using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HyperTizen.Helper;

namespace HyperTizen.SDK
{
    //Credits to Leonardo Rodrigues for this way to download Tizen Operating System files to USB
    //Tested only on Tizen 8 yet
    public static class Filestealer
    {
        private static void ScanDirectory(string dir, [NotNull] Action<string, byte[]> action)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);

                        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            action(file + ".symlink", null);

                            continue;
                        }

                        action(file, File.ReadAllBytes(file));
                    }
                    catch
                    {
                        action(file + ".blocked", null);
                    }
                }

                foreach (string subDir in Directory.EnumerateDirectories(dir))
                {
                    DirectoryInfo fileInfo = new DirectoryInfo(subDir);

                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    ScanDirectory(subDir, action);
                }
            }
            catch { }
        }
        public static void CopyToUsb()
        {
            _ = Task.Run(() =>
            {

                try
                {
                    ScanDirectory("/usr/bin", (file, bytes) =>
                        {
                            try
                            {
                                Log.Write(eLogType.Debug,$"- Downloading: {file}");

                                string fileRelative = file.TrimStart(Path.DirectorySeparatorChar);
                                string fileTarget = Path.Combine(
                                    "/opt/media/USBDriveA1",
                                    fileRelative
                                );

                                string fileTargetDir = Path.GetDirectoryName(fileTarget);

                                if (!Directory.Exists(fileTargetDir))
                                {
                                    _ = Directory.CreateDirectory(fileTargetDir);
                                }

                                File.WriteAllBytes(fileTarget, bytes);
                            }
                            catch (Exception ex)
                            {
                                Log.Write(eLogType.Debug,ex.ToString());
                            }
                        }
                    );

                    Log.Write(eLogType.Debug, "Scan finished!");
                }
                catch (Exception ex)
                {
                    Log.Write(eLogType.Debug, ex.ToString());
                }
            });
        }
    }
}
