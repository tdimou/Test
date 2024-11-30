using Newtonsoft.Json;
using System.Threading.Tasks;
using Tizen.Applications;
using HyperTizen.WebSocket.DataTypes;

namespace HyperTizen.WebSocket
{
    internal class HyperionClient
    {
        WebSocketClient client;
        public HyperionClient() {
            if (!Preference.Contains("rpcServer")) return;
            client = new WebSocketClient(Preference.Get<string>("rpcServer"));
            Task.Run(() => client.client.Connect());
            Task.Run(() => Start());
        }

        public void UpdateURI(string uri)
        {
            if (client?.client != null)
            {
                client.client.OnClose += null;
                client.client.OnError += null;
                Task.Run(() => client.client.Close());
                client.client = null;
            }

            client = new WebSocketClient(uri);
            Task.Run(() => client.client.Connect());
        }

        public void Start(bool shouldStart = false)
        {
            if (client != null && Capturer.GetCondition()) {

                while (App.Configuration.Enabled || shouldStart)
                {
                    Color[] colors = Capturer.GetColors();
                    string image = Capturer.ToImage(colors);
                    
                    if (client?.client?.ReadyState == WebSocketSharp.WebSocketState.Open)
                    {
                        ImageCommand imgCmd = new ImageCommand(image);
                        client.client.Send(JsonConvert.SerializeObject(imgCmd));
                    }

                    if (App.Configuration.Enabled && shouldStart) shouldStart = false;
                    else if (!App.Configuration.Enabled && shouldStart) App.Configuration.Enabled = true;
                }
            }
        }

        public void Stop()
        {
            if (App.Configuration.Enabled) App.Configuration.Enabled = false;
            Color[] colors = Capturer.GetColors();
            string image = Capturer.ToImage(colors);

            if (client?.client?.ReadyState == WebSocketSharp.WebSocketState.Open)
            {
                ImageCommand imgCmd = new ImageCommand(image);
                client.client.Send(JsonConvert.SerializeObject(imgCmd));
            }

        }
    }
}
