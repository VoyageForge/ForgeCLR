using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoyageForge.Depot.Runtime.Utilities;

namespace VoyageForge.ForgeCLR.Runtime
{
    public class LauncherStatus : MonoSingleton<LauncherStatus>
    {
        /// <summary>
        /// 状态文本，用于显示启动日志。
        /// </summary>
        [SerializeField] private Text _statusText;

        /// <summary>
        /// 滚动区域，用于显示启动日志。
        /// </summary>
        [SerializeField] private ScrollRect _scrollRect;

       

        public void Log(string message)
        {
            if (_statusText == null || _statusText == null) return;

            _statusText.text += message + "\n";
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}