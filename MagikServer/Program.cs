using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 为远程计算机提供 MAGIK Web 服务。
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var options = new StartOptions();
            foreach (var ba in Configurations.BaseAddresses) options.Urls.Add(ba);
            // Start OWIN host 
            using (WebApp.Start<Startup>(options))
            {
                /*
                var client = new HttpClient();
                var response = client.GetAsync("http://localhost:9000/magik/v1/paths?expr=[123,456]").Result;
                Console.WriteLine(response);
                Console.WriteLine(response.Content.ReadAsStringAsync().Result);
                */
                Console.WriteLine("请按回车键以结束服务。");
                Console.ReadLine();
            }
        }

    }
}
