using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tizen.Applications.Notifications;

namespace HyperTizen.Helper
{
    public enum eLogType
    {
        Debug,
        Info,
        Warning,
        Error,
        Performance
    }
    public static class Log
    {
        public static void Write(eLogType type,string message)
        {
            switch (type)
            {
                case eLogType.Debug:
                    Debug.WriteLine(message);
                    break;
                case eLogType.Info:
                    //Debug.WriteLine(message);
                    break;
                case eLogType.Warning:
                    Debug.WriteLine(message);
                    break;
                case eLogType.Error:
                    Debug.WriteLine(message);
                    {
                        Notification notification = new Notification
                        {
                            Title = "HyperTizen Error!",
                            Content = message,
                            Count = 1
                        };
                        NotificationManager.Post(notification);
                    }
                    break;
                case eLogType.Performance:
                    //Debug.WriteLine(message);
                    break;
            }

        }
    }
}
