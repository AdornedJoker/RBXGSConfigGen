using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace RBXGSConfigGen
{
    class Program
    {
        static readonly string currentDirectory = Environment.CurrentDirectory;

        class Unit
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            [JsonPropertyName("target_path")]
            public string TargetPath { get; set; } = "";

            [JsonPropertyName("base_path")]
            public string BasePath { get; set; } = "";
        }

        class ObjDiffConfig
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; } = "";

            [JsonPropertyName("build_base")]
            public bool BuildBase { get; set; } = false;

            [JsonPropertyName("units")]
            public List<Unit> Units { get; set; } = new();
        }
        
        static bool DeLink(string directoryPath)
        {
            string fileName;
            string arch;

            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    arch = "x86_64";
                    break;
                default:
                    arch = "arm64";
                    break;
            }

            if (OperatingSystem.IsWindows())
            {
                fileName = "delink-windows-" + arch + ".exe";
            }
            else if (OperatingSystem.IsMacOS())
            {
                fileName = "delink-macos-" + arch;
            }
            else
            {
                fileName = "delink-linux-" + arch;
            }

            Console.WriteLine(fileName);

            DirectoryInfo di = new DirectoryInfo(currentDirectory);
            List<string> currentFiles = GetFileNames(di);

            if (!currentFiles.Contains(fileName))
            {
                Console.WriteLine(currentDirectory);
                Console.WriteLine("Delink program not found! Attempting to download it.");
                
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"https://github.com/HaydnTrigg/delink/releases/download/v0.17.0/{fileName}");

                using HttpClient client = new();
                using var response = client.Send(request);

                response.EnsureSuccessStatusCode();

                using Stream downloadStream = response.Content.ReadAsStream();
                using FileStream fileStream = new FileStream($"{currentDirectory}\\{fileName}", FileMode.Create, FileAccess.Write);
                
                downloadStream.CopyTo(fileStream);

                Console.WriteLine($"{fileName} was downloaded sucessfully!");
            }

            using Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = $"pe-split --pdb \"{directoryPath}\\WebService.pdb\" --outdir objs \"{directoryPath}\\WebService.dll\"";

            process.Start();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return true;

            return false;
        }

        static List<string> GetFileNames(DirectoryInfo di)
        {
            FileInfo[] directoryFiles;
            directoryFiles = di.GetFiles();
            
            List<string> fileNames = [];
            foreach (FileInfo file in directoryFiles)
            {
                fileNames.Add(file.Name);
            }

            return fileNames;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("RBXGS ObjDiff Config Generator.");
            Console.WriteLine("Please input the directory of WebService.");

            string? directoryPath = Console.ReadLine();
            DirectoryInfo di = new DirectoryInfo(directoryPath!);
            if (!di.Exists)
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            List<string> fileNames = GetFileNames(di);

            if (!fileNames.Contains("WebService.dll") || !fileNames.Contains("WebService.pdb"))
            {
                Console.WriteLine("This directory does not include WebService.dll or WebService.pdb!");
                return;
            } 

            Console.WriteLine("Found WebService.dll and WebService.pdb!");

            if (!DeLink(directoryPath!))
            {
                Console.WriteLine("Delinking PDB failed!");
                return;
            }

            DirectoryInfo currentDi = new DirectoryInfo($"{currentDirectory}\\Client\\");

            if (!currentDi.Exists)
            {
                Console.WriteLine("RBXGSDecomp Project Files not found.");
                return;
            }

            DirectoryInfo[] directoryFiles = currentDi.GetDirectories();
            FileInfo[] objs = new DirectoryInfo($"{currentDirectory}\\objs\\").GetFiles();

            ObjDiffConfig objDiffConfig = new()
            {
                Schema = "https://raw.githubusercontent.com/encounter/objdiff/main/config.schema.json"
            };

            foreach (DirectoryInfo dir in directoryFiles)
            {
                DirectoryInfo[] inDirFiles = dir.GetDirectories();
                foreach (DirectoryInfo directory in inDirFiles)
                {
                    if (directory.Name == "obj")
                    {
                        DirectoryInfo[] inDirFiles2 = directory.GetDirectories();
                        foreach (DirectoryInfo directory2 in inDirFiles2)
                        {
                            if (directory2.Name == "ReleaseAssert" || directory2.Name == "Release")
                            {
                                FileInfo[] files = directory2.GetFiles();

                                foreach (FileInfo file in files)
                                {
                                    foreach (FileInfo deLinkedObj in objs)
                                    {
                                        string fileName = file.Name;
                                        string pattern = $@"(?:^|_){Regex.Escape(fileName)}$";

                                        if (Regex.IsMatch(deLinkedObj.Name, pattern))
                                        {
                                            Unit unit = new()
                                            {
                                                Name = fileName,
                                                TargetPath = deLinkedObj.FullName,
                                                BasePath = file.FullName
                                            };

                                            objDiffConfig.Units.Add(unit);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            string jsonString = JsonSerializer.Serialize(objDiffConfig);
            File.WriteAllText("objdiff.json", jsonString);

            Console.WriteLine("All done, enjoy!");
        }
    }
}