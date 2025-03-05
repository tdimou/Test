using Newtonsoft.Json;
using System.Threading.Tasks;
using Tizen.Applications;
using System.Net.WebSockets;
using System.Text;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using SkiaSharp;
using Tizen.NUI;
using System.Diagnostics;
using System.Linq;
using Tizen.Uix.Tts;
using System.Net.Sockets;
using Tizen.Applications.RPCPort;
using System.IO;
using Tizen.Messaging.Messages;
using System.Linq.Expressions;
using Tizen.Applications.Notifications;

namespace HyperTizen
{

    internal class HyperionClient
    {
        public HyperionClient()
        {
            Task.Run(() => Start());
        }

        public async Task Start()
        {

            Globals.Instance.SetConfig();
            while (Globals.Instance.Enabled)
            {
                if(Networking.client != null && Networking.client.Connected)
                    VideoCapture.DoCapture();
                else
                    Networking.SendRegister();
            }
                
        }

        public async Task Stop()
        {

        }
    }
}