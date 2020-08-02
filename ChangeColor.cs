using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ChangeColor : MonoBehaviour
{
    static Socket listener;
    private CancellationTokenSource source;
    public ManualResetEvent allDone;
    public Renderer objectRenderer;
    private Color matColor;

    public static readonly int PORT = 1755;
    public static readonly int WAITTIME = 1;


    ChangeColor()
    {
        source = new CancellationTokenSource();
        allDone = new ManualResetEvent(false);
    }

    // Start is called before the first frame update
    async void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        await Task.Run(() => ListenEvents(source.Token));   
    }

    // Update is called once per frame
    void Update()
    {
        objectRenderer.material.color = matColor;
    }

    private void ListenEvents(CancellationToken token)
    {

        
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = ipHostInfo.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PORT);

         
        listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

         
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(10);

             
            while (!token.IsCancellationRequested)
            {
                allDone.Reset();

                print("Waiting for a connection... host :" + ipAddress.MapToIPv4().ToString() + " port : " + PORT);
                listener.BeginAccept(new AsyncCallback(AcceptCallback),listener);

                while(!token.IsCancellationRequested)
                {
                    if (allDone.WaitOne(WAITTIME))
                    {
                        break;
                    }
                }
      
            }

        }
        catch (Exception e)
        {
            print(e.ToString());
        }
    }

    void AcceptCallback(IAsyncResult ar)
    {  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);
 
        allDone.Set();
  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
    }

    void ReadCallback(IAsyncResult ar)
    {
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        int read = handler.EndReceive(ar);
  
        if (read > 0)
        {
            state.colorCode.Append(Encoding.ASCII.GetString(state.buffer, 0, read));
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }
        else
        {
            if (state.colorCode.Length > 1)
            { 
                string content = state.colorCode.ToString();
                print($"Read {content.Length} bytes from socket.\n Data : {content}");
                SetColors(content);
            }
            handler.Close();
        }
    }

    //Set color to the Material
    private void SetColors (string data) 
    {
        string[] colors = data.Split(',');
        matColor = new Color()
        {
            r = float.Parse(colors[0]) / 255.0f,
            g = float.Parse(colors[1]) / 255.0f,
            b = float.Parse(colors[2]) / 255.0f,
            a = float.Parse(colors[3]) / 255.0f
        };

    }

    private void OnDestroy()
    {
        source.Cancel();
    }

    public class StateObject
    {
        public Socket workSocket = null;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
        public StringBuilder colorCode = new StringBuilder();
    }
}
