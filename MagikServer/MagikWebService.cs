using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 为 MAGIK Web API 提供 Windows 服务承载。
    /// </summary>
    partial class MagikWebService : ServiceBase
    {

        public const string ThisServiceName = "MagikWebService";

        private IDisposable currentWebApp = null;
        private static bool WarmedUp = false;
        private Task warmUpTask = null;

        public MagikWebService()
        {
            InitializeComponent();
            Debug.Assert(ServiceName == ThisServiceName);
            this.Disposed += MagikWebService_Disposed;
        }

        private void DiscardCurrentWebApp()
        {
            currentWebApp?.Dispose();
            currentWebApp = null;
        }

        protected override void OnStart(string[] args)
        {
            DiscardCurrentWebApp();
            EventLog.WriteEntry(Configurations.DumpConfigurations(), EventLogEntryType.Information);
            if (!WarmedUp)
            {
                // 开始预热。
                WarmedUp = true;
                warmUpTask = Analyzer.WarmUpAsync(AnalyzerFactory.CreateSearchClient());
                warmUpTask = warmUpTask.ContinueWith(t => warmUpTask = null);
            }
            currentWebApp = Program.StartWebApp();
        }

        protected override void OnStop()
        {
            DiscardCurrentWebApp();
        }

        private void MagikWebService_Disposed(object sender, EventArgs e)
        {
            DiscardCurrentWebApp();
        }
    }

    /// <summary>
    /// 为 MAGIK Web API 提供安装程序。
    /// </summary>
    [RunInstaller(true)]
    public class MagikWebServiceInstaller : Installer
    {
        public MagikWebServiceInstaller()
        {
            Installers.Add(new ServiceProcessInstaller {Account = ServiceAccount.LocalSystem});
            Installers.Add(new ServiceInstaller
            {
                ServiceName = MagikWebService.ThisServiceName,
                Description = "BOP 2016 Contestant Web API Service (MAGIK)",
                StartType = ServiceStartMode.Automatic,
                DelayedAutoStart = false,
            });
        }
    }
}
