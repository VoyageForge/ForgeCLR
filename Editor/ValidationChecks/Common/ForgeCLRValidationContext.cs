using YooAsset.Editor;

namespace VoyageForge.ForgeCLR.Editor
{
    /// <summary>
    /// 检测上下文，缓存共享状态避免重复查询。
    /// </summary>
    public class ForgeCLRValidationContext
    {
        public ForgeCLRSettings Settings { get; }

        private AssetBundleCollectorSetting _collectorSetting;
        private bool _collectorSettingResolved;

        public AssetBundleCollectorSetting CollectorSetting
        {
            get
            {
                if (!_collectorSettingResolved)
                {
                    ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetCollectorSetting(out _collectorSetting);
                    _collectorSettingResolved = true;
                }

                return _collectorSetting;
            }
        }

        private bool? _hasYooAssetSettings;

        public bool HasYooAssetSettings
        {
            get
            {
                _hasYooAssetSettings ??= ForgeCLRRuntimeSettingsEditorUtility.TryGetYooAssetSettings(out _);
                return _hasYooAssetSettings.Value;
            }
        }

        public bool StrictMode { get; }

        public ForgeCLRValidationContext(ForgeCLRSettings settings, bool strictMode)
        {
            Settings = settings;
            StrictMode = strictMode;
        }

        public ForgeCLRValidationContext(ForgeCLRSettings settings)
            : this(settings, settings.StreamingAssetsStrictMode)
        {
        }
    }
}
