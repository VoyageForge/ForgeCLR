using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    public class FsmDownloadPackageFiles : IStateNode
    {
        private StateMachine _machine;
        private PatchOperation _owner;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
            _owner = machine.Owner as PatchOperation;
        }

        void IStateNode.OnEnter()
        {
            Debug.Log("[ForgeCLR] 开始下载资源文件");
            BeginDownload().Forget();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private async UniTaskVoid BeginDownload()
        {
            var downloader = (ResourceDownloaderOperation)_machine.GetBlackboardValue("Downloader");

            downloader.DownloadErrorCallback = data =>
            {
                Debug.LogError($"[ForgeCLR] Download failed: {data.FileName} {data.ErrorInfo}");
            };

            downloader.DownloadUpdateCallback = data =>
            {
                Debug.Log($"[ForgeCLR] Download progress: {data.CurrentDownloadCount}/{data.TotalDownloadCount}");
            };

            downloader.BeginDownload();
            await downloader.ToUniTask();

            if (downloader.Status != EOperationStatus.Succeed)
            {
                _owner.SetError(downloader.Error);
                return;
            }

            _machine.ChangeState<FsmDownloadPackageOver>();
        }
    }
}
