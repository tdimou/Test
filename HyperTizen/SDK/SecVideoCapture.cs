using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static HyperTizen.SDK.SecVideoCapture;

namespace HyperTizen.SDK
{
    public static unsafe class SecVideoCaptureT7 //for Tizen 7 and below
    {
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_unlock")]
        unsafe public static extern int CaptureScreenUnlock();
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen")] //record with ui
        unsafe public static extern int CaptureScreen(int w, int h, ref Info_t pInfo);
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_video_only")] // without ui
        unsafe public static extern int CaptureScreenVideo(int w, int h, ref Info_t pInfo);
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_video_only_crop")] // cropped
        unsafe public static extern int CaptureScreenCrop(int w, int h, ref Info_t pInfo, int iCapture3DMode, int cropW, int cropH);
        [DllImport("/usr/lib/libsec-video-capture.so.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "secvideo_api_capture_screen_no_lock_no_copy")] // unknown
        unsafe public static extern int CaptureScreenNoLocknoCopy(int w, int h, ref Info_t pInfo);
        //Main Func Errors:
        //-1 "Input Pram is Wrong"
        //-1,-2,-3,-5...negative numbers without 4 "Failed scaler_capture"

        //Sub Func Errors
        //-2 Error: capture type %s, plane %s video only %d
        //-1 req size less or equal that crop size or non video for videoonly found
        //-1 Home Screen & yt crop size, capture lock, video info related
        //-4 Netflix/ Widevine Drm Stuff

    }

    public static unsafe class SecVideoCaptureT8 //for Tizen 8 and above
    {

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public unsafe delegate int CaptureScreenDelegate(IntPtr @this, int w, int h, ref Info_t pInfo);
        public unsafe struct IVideoCapture
        {
            public IntPtr* vtable;
        }

        private static IVideoCapture* instance;
        private static CaptureScreenDelegate captureScreen;

        // Muss importiert sein, wenn getInstance exportiert wird
        [DllImport("/usr/lib/libvideo-capture.so.0.1.0", CallingConvention = CallingConvention.Cdecl, EntryPoint = "getInstance")]
        private static extern IVideoCapture* GetInstance();

        public static void Init()
        {
            instance = GetInstance();

            if (instance == null)
                Helper.Log.Write(Helper.eLogType.Error, "IVideoCapture instance is null");

            const int CaptureScreenVTableIndex = 3;//

            IntPtr fp = instance->vtable[CaptureScreenVTableIndex];
            captureScreen = (CaptureScreenDelegate)Marshal.GetDelegateForFunctionPointer(fp, typeof(CaptureScreenDelegate));
        }

        public static int CaptureScreen(int w, int h, ref Info_t pInfo)
        {
            if (captureScreen == null)
                Helper.Log.Write(Helper.eLogType.Error, "SecVideoCaptureNew not initialized");

            return captureScreen((IntPtr)instance, w, h, ref pInfo);
        }

    }

    public static class SecVideoCapture
    {

        public static unsafe int CaptureScreen(int w, int h, ref Info_t pInfo)
        {
            if (SystemInfo.TizenVersionMajor >= 8)
            {
                // Init should only be done once
                SecVideoCaptureT8.Init();
                return SecVideoCaptureT8.CaptureScreen(w, h, ref pInfo);
            }
            else
            {
                return SecVideoCaptureT7.CaptureScreen(w, h, ref pInfo);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        unsafe public struct Info_t
        {
            unsafe public Int32 iGivenBufferSize1 { get; set; }   //a6[0] = 0 //ref: "C Buffer Size is too small. needed %d bytes but given %d bytes [%d:%s]" needs to be = iGivenBufferSize2
            unsafe public Int32 iGivenBufferSize2 { get; set; }   //a6[1] = 4 //ref: "C Buffer Size is too small. needed %d bytes but given %d bytes [%d:%s]" needs to be = iGivenBufferSize1
            unsafe public Int32 iWidth { get; set; }        //a6[2] = 8 //ref: IceWater "caputre_param.ret_width"
            unsafe public Int32 iHeight { get; set; }        //a6[3] = 12  //ref: IceWater "caputre_param.ret_height"
            unsafe public IntPtr pImageY { get; set; }      //a6[4] // = 16 dest of memcopy copys v31 in adress with sizeof(needed buffer size(i think)) into this
            unsafe public IntPtr pImageUV { get; set; }      //a6[5] // = 20 use this! dest of memcopy copys v223 in adress with sizeof(needed buffer size) into this
            unsafe public Int32 iRetColorFormat { get; set; }       //a6[6] // = 24  //ref: IceWater "color format is"(YUV420 = 0, YUV422 = 1, YUV444 = 2 , None = 3, Everything else = Error)
            unsafe public Int32 unknown2 { get; set; }       //a6[7] // = 28 
            unsafe public Int32 capture3DMode { get; set; }       // = 32  //ref: "Capture 3D Mode is DRM_SDP_3D_2D [%d:%s]" (DRM_SDP_3D_2D = 0, DRM_SDP_3D_FRAMEPACKING = 1, DRM_SDP_3D_FRAMESEQ = 2, DRM_SDP_3D_TOPBOTTOM = 3, DRM_SDP_3D_SIDEBYSIDE = 4)
                                                                  //unk3 a6[15] // = 60
        }

    }
}
