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
    public static class VideoCapture
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Info_t
        {
            public Int32 iGivenBufferSize1 { get; set; }   //a6[0] = 0 //ref: "C Buffer Size is too small. needed %d bytes but given %d bytes [%d:%s]" needs to be = iGivenBufferSize2
            public Int32 iGivenBufferSize2 { get; set; }   //a6[1] = 4 //ref: "C Buffer Size is too small. needed %d bytes but given %d bytes [%d:%s]" needs to be = iGivenBufferSize1
            public Int32 iWidth { get; set; }        //a6[2] = 8 //ref: IceWater "caputre_param.ret_width"
            public Int32 iHeight { get; set; }        //a6[3] = 12  //ref: IceWater "caputre_param.ret_height"
            public IntPtr pImageY { get; set; }      //a6[4] // = 16 dest of memcopy copys v31 in adress with sizeof(needed buffer size(i think)) into this
            public IntPtr pImageUV { get; set; }      //a6[5] // = 20 use this! dest of memcopy copys v223 in adress with sizeof(needed buffer size) into this
            public Int32 iRetColorFormat { get; set; }       //a6[6] // = 24  //ref: IceWater "color format is"(YUV420 = 0, YUV422 = 1, YUV444 = 2 , None = 3, Everything else = Error)
            public Int32 unknown2 { get; set; }       //a6[7] // = 28 
            public Int32 capture3DMode { get; set; }       // = 32  //ref: "Capture 3D Mode is DRM_SDP_3D_2D [%d:%s]" (DRM_SDP_3D_2D = 0, DRM_SDP_3D_FRAMEPACKING = 1, DRM_SDP_3D_FRAMESEQ = 2, DRM_SDP_3D_TOPBOTTOM = 3, DRM_SDP_3D_SIDEBYSIDE = 4)
                                                           //unk3 a6[15] // = 60
        }
        //int (__fastcall *)(_DWORD, _DWORD, _DWORD)
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen")] //record with ui
        unsafe private static extern int CaptureScreen(int w, int h, ref Info_t pInfo);
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_video_only")] // without ui
        private static extern int CaptureScreenVideo(int w, int h, ref Info_t pInfo);
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_video_only_crop")] // cropped
        private static extern int CaptureScreenCrop(int w, int h, ref Info_t pInfo, int iCapture3DMode, int cropW, int cropH);
        //Main Func Errors:
        //-1 "Input Pram is Wrong"
        //-1,-2,-3,-5...negative numbers without 4 "Failed scaler_capture"

        //Sub Func Errors
        //-2 Error: capture type %s, plane %s video only %d
        //-1 req size less or equal that crop size or non video for videoonly found
        //-1 Home Screen & yt crop size, capture lock, video info related
        //-4 Netflix/ Widevine Drm Stuff

        static bool isRunning = true;
        unsafe public static void DoCapture()
        {
            //These lines need to stay here somehow - they arent used but when i delete them the service breaks ??? weird tizen stuff...
            var width = 480;//480 960
            var height = 270;//270 540

            var yBufferSize = width * height * 2;

            var uvBufferSizeYUV420 = (width / 2) * (height / 2); 
            var uvBufferSizeYUV422 = (width / 2) * height;
            var uvBufferSizeYUV444 = width * height;

            int NV12ySize = Globals.Instance.Width * Globals.Instance.Height;
            int NV12uvSize = (Globals.Instance.Width * Globals.Instance.Height) / 2; // UV-Plane ishalf as big as Y-Plane in NV12

            Info_t info = new Info_t();
            info.iGivenBufferSize1 = NV12ySize;
            info.iGivenBufferSize2 = NV12uvSize;
            //info.iWidth = width;
            //info.iHeight = height;
            info.pImageY = Marshal.AllocHGlobal(NV12ySize);
            info.pImageUV = Marshal.AllocHGlobal(NV12uvSize);
            //info.iRetColorFormat = 0;
            //info.capture3DMode = 0;

            //int result = CaptureScreenCrop(width, height, ref info,0, width+10, height+10);
            int result = CaptureScreen(Globals.Instance.Width, Globals.Instance.Height, ref info);
            if (result < 0 && isRunning) //only send Notification once
            {
                switch (result)
                {
                    case -4:
                        Debug.WriteLine("CaptureScreen Result: -4 [Netflix/ Widevine Drm Error]");
                        Notification notification4 = new Notification
                        {
                            Title = "HyperTizen",
                            Content = "Capture Error: Seems like you are watching DRM protected content",
                            Count = 1
                        };
                        NotificationManager.Post(notification4);
                        break;
                    case -1:
                        Debug.WriteLine("CaptureScreen Result: -1 [Input Pram is wrong / req size less or equal that crop size / non video for videoonly found]");
                        Notification notification1 = new Notification
                        {
                            Title = "HyperTizen",
                            Content = "Capture Error: Input Pram seems wrong",
                            Count = 1
                        };
                        NotificationManager.Post(notification1);
                        break;
                    case -2:
                        Debug.WriteLine("CaptureScreen Result: -2 [capture type %s, plane %s video only %d / Failed scaler_capture]");
                        Notification notification2 = new Notification
                        {
                            Title = "HyperTizen",
                            Content = "Capture Error: Failed scaler",
                            Count = 1
                        };
                        NotificationManager.Post(notification2);
                        break;
                    default:
                        Notification notificationN = new Notification
                        {
                            Title = "HyperTizen",
                            Content = "Capture Error: New Error occured. Please report ID:"+ result,
                            Count = 1
                        };
                        NotificationManager.Post(notificationN);
                        break;
                }
                isRunning = false;

                return;
            }

            isRunning = true;


            byte[] managedArrayY = new byte[NV12ySize];
            Marshal.Copy(info.pImageY, managedArrayY, 0, NV12ySize);

            byte[] managedArrayUV = new byte[NV12uvSize];
            Marshal.Copy(info.pImageUV, managedArrayUV, 0, NV12uvSize);

            bool hasAllZeroes1 = managedArrayY.All(singleByte => singleByte == 0);
            bool hasAllZeroes2 = managedArrayUV.All(singleByte => singleByte == 0);
            if (hasAllZeroes1 && hasAllZeroes2)
                throw new Exception("Sanity check Error");

            /*
            var stringByte1 = Convert.ToBase64String(managedArray1);
            var stringByte2 = Convert.ToBase64String(managedArray2);
            Debug.WriteLine(stringByte1);
            Debug.WriteLine(stringByte2);
            */
            //(managedArrayY, managedArrayUV) = GenerateDummyYUVColor(Globals.Instance.Width, Globals.Instance.Height);

            Networking.SendImage(managedArrayY, managedArrayUV, Globals.Instance.Width, Globals.Instance.Height);


        }

        public static (byte[] yData, byte[] uvData) GenerateDummyYUVRandom(int width, int height)
        {
            int ySize = width * height;
            int uvSize = (width * height) / 2; // UV-Plane ist halb so groß wie Y-Plane in NV12

            byte[] yData = new byte[ySize];  // Y-Plane
            byte[] uvData = new byte[uvSize]; // Interleaved UV-Plane (VU VU VU ...)

            Random rnd = new Random();
            rnd.NextBytes(yData);  // Zufallswerte für Y (Helligkeit)
            rnd.NextBytes(uvData); // Zufallswerte für UV (Farbinformation)

            return (yData, uvData);
        }

        public static (byte[] yData, byte[] uvData) GenerateDummyYUVColor(int width, int height)
        {
            int ySize = width * height;
            int uvSize = (width * height) / 2; // UV-Plane ist halb so groß wie Y-Plane in NV12

            byte[] yData = new byte[ySize];  // Y-Plane
            byte[] uvData = new byte[uvSize]; // Interleaved UV-Plane (VU VU VU ...)

            // Set Y (luminance) to a value that represents brightness
            for (int i = 0; i < ySize; i++)
            {
                yData[i] = 128; // You can adjust this value for darker or brighter green
            }

            // Set U and V values for green
            for (int i = 0; i < uvSize; i += 2)
            {
                uvData[i] = 128;   // U component (no red)
                uvData[i + 1] = 255; // V component (max green)
            }

            return (yData, uvData);
        }

    }

}