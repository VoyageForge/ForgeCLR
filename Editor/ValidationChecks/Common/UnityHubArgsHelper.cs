using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 通过修改 Unity Hub 的 projectsInfo.json 来管理项目启动参数。
    /// Unity Hub 将每个项目的启动配置存储在 %AppData%/UnityHub/projectsInfo.json 中，
    /// 本助手类提供对该配置文件的读写操作，支持检测和添加 CLI 启动参数。
    /// 修改前会自动备份配置文件到项目的 FCLR_Backup 目录。
    /// </summary>
    internal static class UnityHubArgsHelper
    {
        private const string HubRelativePath = "UnityHub/projectsInfo.json";

        /// <summary>
        /// 获取 Unity Hub 配置文件的完整路径。
        /// 文件位于系统 AppData 目录下的 UnityHub/projectsInfo.json。
        /// </summary>
        public static string GetHubFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, HubRelativePath);
        }

        /// <summary>
        /// 检查指定项目是否已在 Unity Hub 中配置了某个启动参数。
        /// </summary>
        /// <param name="projectPath">项目的完整本地路径。</param>
        /// <param name="argument">需要检测的 CLI 启动参数（如 -force-gles）。</param>
        /// <returns>若参数已存在则返回 true，否则返回 false。</returns>
        public static bool HasArgument(string projectPath, string argument)
        {
            if (!TryGetProjectEntry(projectPath, out var settings))
                return false;

            return ArgAlreadyExists(settings.cliArgs ?? "", argument);
        }

        /// <summary>
        /// 为指定项目添加 CLI 启动参数，如果参数已存在则跳过。
        /// 操作前会自动备份原配置文件到项目根目录的 FCLR_Backup 文件夹。
        /// 如果 Hub 配置中不存在该项目条目则自动创建。
        /// </summary>
        /// <param name="projectPath">项目的完整本地路径。</param>
        /// <param name="argument">需要添加的 CLI 启动参数。</param>
        /// <returns>操作成功返回 true，失败返回 false。</returns>
        public static bool AddArgumentIfMissing(string projectPath, string argument)
        {
            string hubFile = GetHubFilePath();
            if (!File.Exists(hubFile))
            {
                Debug.LogError($"[ForgeCLR] Hub 配置文件不存在: {hubFile}");
                return false;
            }

            string backupDir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "FCLR_Backup");
            Directory.CreateDirectory(backupDir);
            string backupFile = Path.Combine(backupDir, $"projectsInfo_backup_{DateTime.Now:yyyy-MM-dd_HHmmss}.json");
            File.Copy(hubFile, backupFile, true);
            Debug.Log($"[ForgeCLR] Hub 配置已备份至: {backupFile}");

            string json = File.ReadAllText(hubFile);
            var projects = JsonConvert.DeserializeObject<Dictionary<string, ProjectSettings>>(json);
            if (projects == null)
            {
                Debug.LogError("[ForgeCLR] Hub 配置文件解析失败");
                return false;
            }

            string normalizedPath = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!projects.TryGetValue(normalizedPath, out var settings))
            {
                Debug.Log($"[ForgeCLR] Hub 配置中未找到当前项目，自动创建条目: {normalizedPath}");
                settings = new ProjectSettings();
                projects[normalizedPath] = settings;
            }

            string currentArgs = (settings.cliArgs ?? "").Trim();
            if (ArgAlreadyExists(currentArgs, argument))
            {
                Debug.Log($"[ForgeCLR] 参数 '{argument}' 已存在，无需重复添加。");
                return true;
            }

            settings.cliArgs = string.IsNullOrEmpty(currentArgs)
                ? argument
                : currentArgs + " " + argument;

            projects[normalizedPath] = settings;
            string updatedJson = JsonConvert.SerializeObject(projects, Formatting.Indented);
            File.WriteAllText(hubFile, updatedJson);

            Debug.Log($"[ForgeCLR] 成功添加参数 '{argument}' 到项目: {normalizedPath}");
            return true;
        }

        /// <summary>
        /// 尝试从 Unity Hub 配置文件中读取指定项目的设置条目。
        /// </summary>
        /// <param name="projectPath">项目的完整本地路径。</param>
        /// <param name="settings">输出参数，若找到则返回对应的项目配置。</param>
        /// <returns>找到项目条目返回 true，否则返回 false。</returns>
        private static bool TryGetProjectEntry(string projectPath, out ProjectSettings settings)
        {
            settings = null;
            string hubFile = GetHubFilePath();
            if (!File.Exists(hubFile))
                return false;

            string json = File.ReadAllText(hubFile);
            var projects = JsonConvert.DeserializeObject<Dictionary<string, ProjectSettings>>(json);
            if (projects == null)
                return false;

            string normalizedPath = Path.GetFullPath(projectPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return projects.TryGetValue(normalizedPath, out settings);
        }

        /// <summary>
        /// 检查命令行参数字符串中是否已包含目标参数（不区分大小写）。
        /// </summary>
        /// <param name="args">当前已配置的命令行参数字符串。</param>
        /// <param name="targetArg">需要检测的目标参数。</param>
        /// <returns>若目标参数已存在则返回 true。</returns>
        private static bool ArgAlreadyExists(string args, string targetArg)
        {
            if (string.IsNullOrEmpty(args))
                return false;

            foreach (var part in args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Equals(targetArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Unity Hub 项目设置数据模型，对应 projectsInfo.json 中每个项目的配置结构。
    /// </summary>
    [Serializable]
    internal class ProjectSettings
    {
        /// <summary>
        /// CLI 启动参数字符串，多个参数以空格分隔。
        /// </summary>
        public string cliArgs;
    }
}
