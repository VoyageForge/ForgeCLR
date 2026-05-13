using UnityEngine;

namespace VoyageForge.ForgeCLR.Editor
{
    [CreateAssetMenu(fileName = "ForgeCLRServer", menuName = "VoyageForge/ForgeCLRServerSO")]
    public class ForgeCLRServerSO : ScriptableObject
    {
        public VoyageForgeFileServer Server;

        /// <summary>
        /// 默认存储资源路径
        /// </summary>
        public const string DefaultAssetPath = "Assets/ForgeCLR/Editor/ForgeCLRServer.asset";

        public static ForgeCLRServerSO LoadOrCreate()
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ForgeCLRServerSO>(DefaultAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ForgeCLRServerSO>();
                UnityEditor.AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
            }
            return asset;
        }
    }
}