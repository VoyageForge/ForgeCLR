using UnityEngine;
using YooAsset;

namespace VoyageForge.ForgeCLR.Runtime
{
    public class FsmCreateDownloader : IStateNode
    {
        private StateMachine _machine;

        void IStateNode.OnCreate(StateMachine machine)
        {
            _machine = machine;
        }

        void IStateNode.OnEnter()
        {
            LauncherStatus.Instance.Log("[ForgeCLR] 创建资源下载器");
            CreateDownloader();
        }

        void IStateNode.OnUpdate()
        {
        }

        void IStateNode.OnExit()
        {
        }

        private void CreateDownloader()
        {
            var packageName = (string)_machine.GetBlackboardValue("PackageName");
            var package = YooAssets.GetPackage(packageName);
            var downloader = package.CreateResourceDownloader(10, 3);

            _machine.SetBlackboardValue("Downloader", downloader);

            if (downloader.TotalDownloadCount == 0)
            {
                LauncherStatus.Instance.Log("[ForgeCLR] 没有需要下载的资源文件");
                _machine.ChangeState<FsmLoadAotMetadata>();
                return;
            }

            LauncherStatus.Instance.Log($"[ForgeCLR] Found update files {downloader.TotalDownloadCount}:{downloader.TotalDownloadBytes}");
            _machine.ChangeState<FsmDownloadPackageFiles>();
        }
    }
}
