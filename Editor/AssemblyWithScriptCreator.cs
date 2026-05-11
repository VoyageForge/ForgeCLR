using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
/// 在指定文件夹下创建程序集并可选创建 Mono 模板脚本
/// </summary>
public class AssemblyWithScriptCreator : EditorWindow
{
    private string targetFolder = "Assets";
    private string assemblyName = "NewAssembly";

    [MenuItem("Tools/Create Assembly with Script...")]
    public static void ShowWindow()
    {
        // 获取当前选中的文件夹路径（如果有的话）
        string selectedPath = GetSelectedFolderPath();
        var window = GetWindow<AssemblyWithScriptCreator>("Assembly Creator");
        window.targetFolder = selectedPath ?? "Assets";
        window.assemblyName = "NewAssembly";
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("Create Assembly Definition & Placeholder Script", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // 目标文件夹输入
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Folder", GUILayout.Width(100));
        targetFolder = EditorGUILayout.TextField(targetFolder);
        if (GUILayout.Button("Select", GUILayout.Width(60)))
        {
            string selected = GetSelectedFolderPath();
            if (!string.IsNullOrEmpty(selected))
                targetFolder = selected;
        }
        EditorGUILayout.EndHorizontal();

        // 程序集名称输入
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Assembly Name", GUILayout.Width(100));
        assemblyName = EditorGUILayout.TextField(assemblyName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // 输入验证与提示
        bool isValid = !string.IsNullOrWhiteSpace(assemblyName) && targetFolder.StartsWith("Assets/");
        if (!isValid)
        {
            EditorGUILayout.HelpBox("Folder must be under 'Assets/' and Assembly Name cannot be empty.", MessageType.Warning);
        }

        EditorGUI.BeginDisabledGroup(!isValid);
        if (GUILayout.Button("Create", GUILayout.Height(30)))
        {
            CreateAssemblyWithScript(targetFolder, assemblyName);
            Close();
        }
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>
    /// 核心创建逻辑：文件夹 → .asmdef → 占位脚本
    /// </summary>
    public static void CreateAssemblyWithScript(string folderPath, string assemblyName)
    {
        // ---------- 1. 确保文件夹存在 ----------
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Directory.CreateDirectory(folderPath);          // 创建物理目录
            AssetDatabase.Refresh();                        // Unity 识别新目录
            Debug.Log($"Created folder: {folderPath}");
        }
        else
        {
            Debug.Log($"Folder already exists: {folderPath}");
        }

        // ---------- 2. 创建程序集定义（.asmdef） ----------
        string asmdefPath = Path.Combine(folderPath, assemblyName + ".asmdef");
        if (!File.Exists(asmdefPath))
        {
            string json = GenerateAsmdefJson(assemblyName);
            File.WriteAllText(asmdefPath, json);
            AssetDatabase.Refresh();
            Debug.Log($"Assembly Definition created: {asmdefPath}");
        }
        else
        {
            Debug.Log($"Assembly Definition already exists, skipped: {asmdefPath}");
        }

        // ---------- 3. 创建占位脚本（仅当文件夹内无 .cs 文件时） ----------
        string fullFolderPath = Path.GetFullPath(folderPath);     // 转为绝对路径用于 IO 检查
        if (Directory.Exists(fullFolderPath))
        {
            string[] csFiles = Directory.GetFiles(fullFolderPath, "*.cs", SearchOption.TopDirectoryOnly);
            if (csFiles.Length == 0)
            {
                // 生成一个合法的类名（移除特殊字符，确保不以数字开头）
                string className = SanitizeClassName(assemblyName);
                string scriptPath = Path.Combine(folderPath, className + ".cs");
                string scriptContent = GenerateMonoScriptTemplate(className);
                File.WriteAllText(scriptPath, scriptContent);
                AssetDatabase.Refresh();
                Debug.Log($"Placeholder script created: {scriptPath}");
            }
            else
            {
                Debug.Log("Folder already contains C# scripts, placeholder script skipped.");
            }
        }
        else
        {
            Debug.LogError($"Folder path is invalid: {fullFolderPath}");
        }

        AssetDatabase.Refresh();
    }

    // ---------- 辅助方法 ----------

    private static string GetSelectedFolderPath()
    {
        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
                return path;
            // 如果是文件，则返回其所在文件夹
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string folder = Path.GetDirectoryName(path);
                if (AssetDatabase.IsValidFolder(folder))
                    return folder;
            }
        }
        return null;
    }

    private static string GenerateAsmdefJson(string name)
    {
        // 使用匿名类序列化，字段与 Unity 默认 .asmdef 完全一致
        var asmdef = new
        {
            name = name,
            rootNamespace = "",
            references = new string[] { },
            includePlatforms = new string[] { },
            excludePlatforms = new string[] { },
            allowUnsafeCode = false,
            overrideReferences = false,
            precompiledReferences = new string[] { },
            autoReferenced = true,
            defineConstraints = new string[] { },
            versionDefines = new object[] { },
            noEngineReferences = false
        };
        return JsonUtility.ToJson(asmdef, true);
    }

    private static string GenerateMonoScriptTemplate(string className)
    {
        return $@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    private void Start()
    {{
        
    }}

    private void Update()
    {{
        
    }}
}}";
    }

    private static string SanitizeClassName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "NewBehaviour";

        // 移除不符合 C# 标识符的字符，只保留字母、数字、下划线
        string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        // 如果以数字开头，添加前缀
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        // 若结果为空，使用默认名称
        return string.IsNullOrEmpty(sanitized) ? "NewBehaviour" : sanitized;
    }
}

}