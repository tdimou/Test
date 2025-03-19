using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tizen.Applications;

namespace HyperTizen
{
    public sealed class Globals
    {
        private static readonly Globals instance = new Globals();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static Globals()
        {
        }

        private Globals()
        {
        }

        public static Globals Instance
        {
            get
            {
                return instance;
            }
        }
        public void SetConfig()
        {
            ServerIp = "192.168.69.200";//Preference.Contains("rpcServer") ? Preference.Get<string>("rpcServer") : null;  
            ServerPort = 19400;
            Enabled = true;//bool.Parse(Preference.Get<string>("enabled"));
            Width = 3840/8;
            Height = 2160/8;
        }

        public string ServerIp; //IP of hyperhdr server
        public int ServerPort; //Port of hyperhdr server
        public int Width; //Capture Width
        public int Height; //Capture Height
        public bool Enabled; //Is the service enabled
        public uint SessionId; //Used for detection if some Threads are executed beacuse of the last Session -> if so ignore these
    }
}
