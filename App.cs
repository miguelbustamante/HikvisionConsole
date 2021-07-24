using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HikvisionGetUsers
{
    
    class App
    {
        private UserInfo m_userInfo = new UserInfo();
        bool m_bInitSDK = false;
        private Int32 m_lUserID = -1;
        private Int32 m_lUserID2 = -1;
        private Int32 m_lUserID3 = -1;

        public const int iMaxFingerPrint = 10000;
        public int m_doorNum = 1;
        private uint iLastErr = 0;
        private string str;
        private Int32 m_lRealHandle = -1;
        public int m_iDeviceIndex = 0;
        public string DeviceIP = null;

        public const int iMaxCardNum = 1000;
        public CHCNetSDK.NET_DVR_FINGER_PRINT_CFG_V50 m_struFingerPrintOne = new CHCNetSDK.NET_DVR_FINGER_PRINT_CFG_V50();
        public CHCNetSDK.NET_DVR_FINGER_PRINT_INFO_CTRL_BYCARD_V50 m_struDelFingerPrint = new CHCNetSDK.NET_DVR_FINGER_PRINT_INFO_CTRL_BYCARD_V50();
        public CHCNetSDK.NET_DVR_FINGER_PRINT_CFG_V50[] m_struRecordCardCfg = new CHCNetSDK.NET_DVR_FINGER_PRINT_CFG_V50[iMaxCardNum];
        public int m_lSetFingerPrintCfgHandle = -1;
        public int m_lGetFingerPrintCfgHandle = -1;
        public int m_iSendIndex = -1;
        public int m_lDelFingerPrintCfHandle = -1;
        public int m_lCapFingerPrintCfHandle = -1;
        public delegate bool ConfigureFingerCallBack(bool bGet);
        public ConfigureFingerCallBack g_fConfigFingerCallBack = null;
        public CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo;
        public CHCNetSDK.NET_DVR_IPPARACFG_V40 m_struIpParaCfgV40;
        public CHCNetSDK.NET_DVR_STREAM_MODE m_struStreamMode;
        public CHCNetSDK.NET_DVR_IPCHANINFO m_struChanInfo;
        public CHCNetSDK.NET_DVR_IPCHANINFO_V40 m_struChanInfoV40;
        private CHCNetSDK.NET_DVR_ACS_EVENT_COND m_struAcsEventCond = new CHCNetSDK.NET_DVR_ACS_EVENT_COND();
        private int m_lLogNum = 0;
        private int m_lGetAcsEvent = 0;
        private string CsTemp = null;

        private string MinorType = null;
        private string MajorType = null;


        public delegate void MyDebugInfo(string str);
        public int lRemoteHandle = -1;
        public int iTotalAcsEvent = 0;

        CHCNetSDK.NET_DVR_CARD_CFG_V50[] m_struCardInfo2 = new CHCNetSDK.NET_DVR_CARD_CFG_V50[iMaxCardNum];
        private CHCNetSDK.RemoteConfigCallback g_fGetGatewayCardCallback = null;
        private int m_lGetCardCfgHandle = -1;

        List<CHCNetSDK.NET_DVR_CARD_CFG_V50> cardInfos = new List<CHCNetSDK.NET_DVR_CARD_CFG_V50>();

        private readonly ILogger _logger;
        public App(ILogger<App> logger)
        {
            _logger = logger;
        }

        internal async Task Run(string[] args) {
            var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json").Build();
            var configInfo = config.GetSection("Configuration").Get<Configuration>(); 
            bool m_bInitSDK = CHCNetSDK.NET_DVR_Init();
            CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo = new CHCNetSDK.NET_DVR_DEVICEINFO_V30();
            CHCNetSDK.NET_DVR_USER_LOGIN_INFO userData = new CHCNetSDK.NET_DVR_USER_LOGIN_INFO();
            userData.sUserName = configInfo.username;
            userData.sPassword = configInfo.password;
            userData.sDeviceAddress = configInfo.ip;
            userData.wPort = (ushort)configInfo.port;
            int userId;
            if (m_bInitSDK)
            {
                _logger.LogInformation("Dispositivo inicializado...");
                userId = Conectar(configInfo.ip, configInfo.port, configInfo.username, configInfo.password, ref DeviceInfo);
                userId = Conectar(configInfo.ip, configInfo.port, configInfo.username, configInfo.password, ref DeviceInfo);
                if (userId >= 0)
                {
                    
                    _logger.LogInformation($"Conexión exitosa, respuesta de conexión {userId}, serial de dispositivo: {Encoding.UTF8.GetString(DeviceInfo.sSerialNumber)}");
                    //obtUs(userId, 0, userData);
                    obtenerReporteRRHH(userId, userData);

                }
                else
                {
                    _logger.LogError($"Fallo la conexión al dispositivo error: {CHCNetSDK.NET_DVR_GetLastError()}");
                }
            }
            else {
                _logger.LogError($"Fallo la inicialización del dispositivo error: {CHCNetSDK.NET_DVR_GetLastError()}");
            }
        }

        public int Conectar(string ip, short port, string username, string password, ref CHCNetSDK.NET_DVR_DEVICEINFO_V30 deviceInfo) {

            return CHCNetSDK.NET_DVR_Login_V30(ip, port, username, password, ref deviceInfo);
        }

        private void obtUs(int ml, ushort controllerId, CHCNetSDK.NET_DVR_USER_LOGIN_INFO userData)
        {
            _logger.LogInformation("Iniciando la obtención de usuarios...");
            if (-1 != m_lGetCardCfgHandle)
            {
                if (CHCNetSDK.NET_DVR_StopRemoteConfig(m_lGetCardCfgHandle))
                {
                    m_lGetCardCfgHandle = -1;
                }
            }

            CHCNetSDK.NET_DVR_CARD_CFG_COND struCond = new CHCNetSDK.NET_DVR_CARD_CFG_COND();
            struCond.dwSize = (uint)Marshal.SizeOf(struCond);
            //ID DEL DISPOSITIVO
            struCond.wLocalControllerID = controllerId;
            struCond.dwCardNum = 0xffffffff;
            struCond.byCheckCardNo = 0;

            int dwSize = Marshal.SizeOf(struCond);
            int dwSize2 = Marshal.SizeOf(userData);
            IntPtr ptrStruCond = Marshal.AllocHGlobal(dwSize);
            IntPtr pUserData = Marshal.AllocHGlobal(dwSize2);
            Marshal.StructureToPtr(struCond, ptrStruCond, false);
            Marshal.StructureToPtr(userData, pUserData, false);

            _logger.LogInformation($"User information: {userData.sUserName}");

            g_fGetGatewayCardCallback = new CHCNetSDK.RemoteConfigCallback(ProcessGetGatewayCardCallback3);
            m_lGetCardCfgHandle = CHCNetSDK.NET_DVR_StartRemoteConfig(m_lUserID, CHCNetSDK.NET_DVR_GET_CARD_CFG_V50, ptrStruCond, dwSize, g_fGetGatewayCardCallback, pUserData);
            if (m_lGetCardCfgHandle == -1)
            {
                _logger.LogError($"NET_DVR_GET_CARD_CFG_V50 fallo, error: {CHCNetSDK.NET_DVR_GetLastError()}");
                Marshal.FreeHGlobal(ptrStruCond);
                return;
            }
            else
            {
                string fileNameNuevas = "\\Usuarios_" + string.Format("{0:ddMMyyyyHmmss}.json", DateTime.Now);

                string path = Directory.GetCurrentDirectory();
                string fullPathNuevas = path + fileNameNuevas;

                using (StreamWriter file = File.CreateText(fullPathNuevas))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, cardInfos);
                }
                _logger.LogInformation("Finalizo el evento.");
            }
            Marshal.FreeHGlobal(ptrStruCond);

        }

        private void obtenerReporteRRHH(int ml, CHCNetSDK.NET_DVR_USER_LOGIN_INFO userData)
        {
            m_struAcsEventCond.dwSize = (uint)Marshal.SizeOf(m_struAcsEventCond);

            MajorType = "Event";
            m_struAcsEventCond.dwMajor = GetAcsEventType.ReturnMajorTypeValue(ref MajorType);

            MinorType = "All";
            m_struAcsEventCond.dwMinor = GetAcsEventType.ReturnMinorTypeValue(ref MinorType);

            m_struAcsEventCond.struStartTime.dwYear = 2021;
            m_struAcsEventCond.struStartTime.dwMonth = 5;
            m_struAcsEventCond.struStartTime.dwDay = 1;
            m_struAcsEventCond.struStartTime.dwHour = 0;
            m_struAcsEventCond.struStartTime.dwMinute = 0;
            m_struAcsEventCond.struStartTime.dwSecond = 0;

            m_struAcsEventCond.struEndTime.dwYear = 2021;
            m_struAcsEventCond.struEndTime.dwMonth = 7;
            m_struAcsEventCond.struEndTime.dwDay = 1;
            m_struAcsEventCond.struEndTime.dwHour = 0;
            m_struAcsEventCond.struEndTime.dwMinute = 0;
            m_struAcsEventCond.struEndTime.dwSecond = 0;

            if (!StrToByteArray(ref m_struAcsEventCond.byCardNo, ""))
            {
                return;
            }

            if (!StrToByteArray(ref m_struAcsEventCond.byName, ""))
            {
                return;
            }

            
            m_struAcsEventCond.wInductiveEventType = GetAcsEventType.ReturnInductiveEventTypeValue("all");

            _logger.LogInformation($"wInductiveEventType: {m_struAcsEventCond.wInductiveEventType}");

            if (!StrToByteArray(ref m_struAcsEventCond.byEmployeeNo, ""))
            {
                return;
            }

            m_lLogNum = 0;
            uint dwSize = (uint)Marshal.SizeOf(m_struAcsEventCond);
            IntPtr ptrCond = Marshal.AllocHGlobal((int)dwSize);

            int dwSize2 = Marshal.SizeOf(userData);
            IntPtr pUserData = Marshal.AllocHGlobal(dwSize2);

            Marshal.StructureToPtr(m_struAcsEventCond, ptrCond, false);
            Marshal.StructureToPtr(userData, pUserData, false);

            CHCNetSDK.RemoteConfigCallback g_fGetAcsEventCallback = new CHCNetSDK.RemoteConfigCallback(ProcessGetAcsEventCallback);
            m_lGetAcsEvent = CHCNetSDK.NET_DVR_StartRemoteConfig(ml, CHCNetSDK.NET_DVR_GET_ACS_EVENT, ptrCond, (int)dwSize, g_fGetAcsEventCallback, pUserData);


            while (true) {
                int result = CHCNetSDK.NET_DVR_GetNextRemoteConfig(m_lGetAcsEvent, ptrCond, dwSize);
                if ( result == 1002)
                {
                    CHCNetSDK.NET_DVR_StopRemoteConfig(m_lGetAcsEvent);
                    break;
                }
                else {
                    if (result == 1001)
                    {
                        _logger.LogWarning("Se debe realizar una espera....");
                        break;
                    }
                    else {
                        if (result == 1003) {
                            _logger.LogError($"Fallo la operación: {CHCNetSDK.NET_DVR_GetLastError()}");
                        }
                    }
                }
            }
            

            _logger.LogInformation($"Respuesta de lectura de callback: {m_lGetAcsEvent}");
            if (-1 == m_lGetAcsEvent)
            {
                //g_formList.AddLog(m_iDeviceIndex, AcsDemoPublic.OPERATION_FAIL_T, "NET_DVR_GET_ACS_EVENT");
                _logger.LogError($"Fallo la operación: {CHCNetSDK.NET_DVR_GetLastError()}");
                Marshal.FreeHGlobal(ptrCond);
                Marshal.FreeHGlobal(pUserData);
            }
            else
            {
                //g_formList.AddLog(m_iDeviceIndex, AcsDemoPublic.OPERATION_SUCC_T, "NET_DVR_GET_ACS_EVENT");
                _logger.LogInformation("Finalizo el evento de obtener reporte...");
                Marshal.FreeHGlobal(ptrCond);
                Marshal.FreeHGlobal(pUserData);
            }
        }

        private void ProcessGetAcsEventCallback(uint dwType, IntPtr lpBuffer, uint dwBufLen, IntPtr pUserData)
        {

            _logger.LogInformation("Ingresando al procesamiento de callback...");
            if (dwType == (uint)CHCNetSDK.NET_SDK_CALLBACK_TYPE.NET_SDK_CALLBACK_TYPE_DATA)
            {
                _logger.LogInformation("Obteniendo accesos...");
                CHCNetSDK.NET_DVR_ACS_EVENT_CFG lpAcsEventCfg = new CHCNetSDK.NET_DVR_ACS_EVENT_CFG();
                lpAcsEventCfg = (CHCNetSDK.NET_DVR_ACS_EVENT_CFG)Marshal.PtrToStructure(lpBuffer, typeof(CHCNetSDK.NET_DVR_ACS_EVENT_CFG));
                IntPtr ptrAcsEventCfg = Marshal.AllocHGlobal(Marshal.SizeOf(lpAcsEventCfg));
                Marshal.StructureToPtr(lpAcsEventCfg, ptrAcsEventCfg, true);

                CHCNetSDK.NET_DVR_ACS_EVENT_CFG struEventCfg = new CHCNetSDK.NET_DVR_ACS_EVENT_CFG();
                struEventCfg = (CHCNetSDK.NET_DVR_ACS_EVENT_CFG)Marshal.PtrToStructure(ptrAcsEventCfg, typeof(CHCNetSDK.NET_DVR_ACS_EVENT_CFG));
                Marshal.FreeHGlobal(ptrAcsEventCfg);

                _logger.LogInformation($"Numero empleado: {Encoding.UTF8.GetString(struEventCfg.struAcsEventInfo.byEmployeeNo)}");
            }
            else if (dwType == (uint)CHCNetSDK.NET_SDK_CALLBACK_TYPE.NET_SDK_CALLBACK_TYPE_STATUS)
            {
                int dwStatus = Marshal.ReadInt32(lpBuffer);
                if (dwStatus == (uint)CHCNetSDK.NET_SDK_CALLBACK_STATUS_NORMAL.NET_SDK_CALLBACK_STATUS_SUCCESS)
                {
                    _logger.LogInformation($"Finalizando la lectura");
                }
                else if (dwStatus == (uint)CHCNetSDK.NET_SDK_CALLBACK_STATUS_NORMAL.NET_SDK_CALLBACK_STATUS_FAILED)
                {
                    _logger.LogError("Existio un error en la lectura de accesos...");
                    //g_formList.AddLog(m_iDeviceIndex, AcsDemoPublic.OPERATION_FAIL_T, "NET_DVR_GET_ACS_EVENT failed");
                }
            }
        }

        private Boolean StrToByteArray(ref byte[] destination, string data)
        {
            if (data != "")
            {
                byte[] source = System.Text.Encoding.Default.GetBytes(data);
                if (source.Length > destination.Length)
                {
                    _logger.LogInformation("The length of num is exceeding");
                    return false;
                }
                else
                {
                    for (int i = 0; i < source.Length; ++i)
                    {
                        destination[i] = source[i];
                    }
                    return true;
                }
            }
            return true;
        }
        private void ProcessGetGatewayCardCallback3(uint dwType, IntPtr lpBuffer, uint dwBufLen, IntPtr pUserData)
        {
            if (pUserData == null)
            {
                return;
            }

            if (dwType == (uint)CHCNetSDK.NET_SDK_CALLBACK_TYPE.NET_SDK_CALLBACK_TYPE_DATA)
            {
                CHCNetSDK.NET_DVR_CARD_CFG_V50 struCardCfg = new CHCNetSDK.NET_DVR_CARD_CFG_V50();
                struCardCfg = (CHCNetSDK.NET_DVR_CARD_CFG_V50)Marshal.PtrToStructure(lpBuffer, typeof(CHCNetSDK.NET_DVR_CARD_CFG_V50));
                string strCardNo = System.Text.Encoding.UTF8.GetString(struCardCfg.byCardNo);
                IntPtr pCardInfo = Marshal.AllocHGlobal(Marshal.SizeOf(struCardCfg));
                Marshal.StructureToPtr(struCardCfg, pCardInfo, true);
                //CHCNetSDK.PostMessage(pUserData, 1003, (int)pCardInfo, 0);

                _logger.LogInformation("Agregando información.");
                cardInfos.Add(struCardCfg);


            }
            else
            {
                if (dwType == (uint)CHCNetSDK.NET_SDK_CALLBACK_TYPE.NET_SDK_CALLBACK_TYPE_STATUS)
                {
                    uint dwStatus = (uint)Marshal.ReadInt32(lpBuffer);
                    if (dwStatus == (uint)CHCNetSDK.NET_SDK_CALLBACK_STATUS_NORMAL.NET_SDK_CALLBACK_STATUS_SUCCESS)
                    {
                        _logger.LogInformation("NET_DVR_GET_CARD_CFG_V50 finalizo");
                        CHCNetSDK.PostMessage(pUserData, 1002, 0, 0);
                    }
                    else if (dwStatus == (uint)CHCNetSDK.NET_SDK_CALLBACK_STATUS_NORMAL.NET_SDK_CALLBACK_STATUS_FAILED)
                    {
                        uint dwErrorCode = (uint)Marshal.ReadInt32(lpBuffer + 1);
                        string cardNumber = Marshal.PtrToStringAnsi(lpBuffer + 2);
                        _logger.LogError($"NET_DVR_GET_CARD_CFG_V50 fallo, ErrorCode:{dwErrorCode},CardNo:{cardNumber}");
                        CHCNetSDK.PostMessage(pUserData, 1002, 0, 0);
                    }
                }
            }
            return;
        }
    }
}
