using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class UDPManager : MonoBehaviour
{
    private UdpClient udpClient;
    private IPEndPoint pythonEndPoint;
    private bool udpEnabled = true;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        
#if UNITY_WEBGL && !UNITY_EDITOR
        udpEnabled = false;
        Debug.Log("UDP disabled for WebGL build");
#else
        try
        {
            pythonEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12345);
            udpClient = new UdpClient();
            udpEnabled = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("UDP initialization failed: " + e.Message);
            udpEnabled = false;
        }
#endif
    }

    public void SendMarker(string marker)
    {
        if (!udpEnabled) return;
        
        byte[] data = Encoding.UTF8.GetBytes(marker);
        udpClient.Send(data, data.Length, pythonEndPoint);
        Debug.Log("Sent: " + marker);
    }

    void OnDestroy()
    {
        if (udpClient != null) udpClient.Close();
    }
}