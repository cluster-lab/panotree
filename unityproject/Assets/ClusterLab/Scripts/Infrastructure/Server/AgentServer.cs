using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ClusterLab.Infrastructure.Agent;
using ClusterLab.Infrastructure.Server.ViewModel;
using UnityEngine;

namespace ClusterLab.Infrastructure.Server
{
    /// <summary>
    /// IAgentDriver を制御するための、REST APIサーバー
    /// 基本的にUnityに非依存。 (JSONUtilityを除く)
    /// </summary>
    public partial class AgentServer
    {
        public int SemaphoreTimeoutMillis { get; set;  } = 3000;
        readonly Semaphore semaphore = new(initialCount: 1, maximumCount: 1);

        static readonly VersionInfo VERSION = new (1, 1, 0, 0);
        HttpListener listener;
        Thread listenerThread;

        readonly string domain;
        readonly int port;

        readonly IAgentDriver agentDriver;
        List<AgentServerHandler> handlers;

        public AgentServer(IAgentDriver agentDriver, string domain = "localhost", int port = 8080)
        {
            this.domain = domain;
            this.port = port;
            this.agentDriver = agentDriver;

            SetUpHandlers();
        }

        IEnumerable<string> GetLocalIPs()
        {
            // Get a list of all network interfaces (usually one per network card, dialup, and VPN connection)
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Select(ni => ni.GetIPProperties())
                .SelectMany(ip => ip.UnicastAddresses)
                .Where(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork && // We're only interested in IPv4 addresses for now
                    !IPAddress.IsLoopback(address.Address)
                ) // Ignore loopback addresses (e.g., 127.0.0.1)
                .Select(address => address.Address.ToString());
        }

        public void StartServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://" + domain + ":" + port + "/");
            var localIPs = GetLocalIPs().ToList();
            localIPs.ForEach(ip => listener.Prefixes.Add("http://" + ip + ":" + port + "/"));
            localIPs.ForEach(ip => Debug.Log(ip));
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Start();

            listenerThread = new Thread(StartListener);
            listenerThread.Start();
            Debug.Log("Agent Server Started");
        }

        public void StopServer()
        {
            listener.Stop();
            listenerThread.Join();
        }

        void StartListener()
        {
            while (listener.IsListening)
            {
                var result = listener.BeginGetContext(ListenerCallback, listener);
                result.AsyncWaitHandle.WaitOne();
            }
        }

        void ListenerCallback(IAsyncResult result)
        {
            if (!listener.IsListening) return;
            var context = listener.EndGetContext(result);
            // Debug.Log("Method: " + context.Request.HttpMethod);
            // Debug.Log("LocalUrl: " + context.Request.Url.LocalPath);

            try
            {
                ProcessRequest(context);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                ReturnInternalError(context.Response, e);
            }
        }

        void ProcessRequest(HttpListenerContext context)
        {
            foreach (var handler in handlers)
            {
                if (handler.Accept(context))
                {
                    handler.Action(context);
                    break;
                }
            }
        }

        void HandlePostReturnEmpty<T>(HttpListenerContext context, Action<T> handler)
        {
            var request = ParseJson<T>(context);
            handler(request);
            context.Response.StatusCode = 200;
            context.Response.Close();
        }



        /// <summary>
        /// 過去バージョンとの互換性のために存在
        /// 過去バージョンではバウンディングボックスの各値をstring interpolationで丸めているために、そのままでは誤差が出る
        /// 過去バージョンとの比較をしないなら必要ない
        /// </summary>
        /// <returns></returns>
        private string ConvertToCompatFloat(float value)
        {
            return value.ToString();
        }



        private T ParseJson<T>(HttpListenerContext context)
        {
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                var json = reader.ReadToEnd();
                return JsonUtility.FromJson<T>(json);
            }
        }

        private void ReturnInternalError(HttpListenerResponse response, Exception cause)
        {
            Console.Error.WriteLine(cause);
            response.StatusCode = (int) HttpStatusCode.InternalServerError;
            response.ContentType = "text/plain";
            try
            {
                using (var writer = new StreamWriter(response.OutputStream, Encoding.UTF8))
                    writer.Write(cause.ToString());
                response.Close();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                Console.Error.WriteLine(e);
                response.Abort();
            }
        }
    }

    class AgentServerHandler
    {
        public readonly HttpMethod Method;
        public readonly string PathPattern;
        public readonly Action<HttpListenerContext> Action;

        public AgentServerHandler(HttpMethod method, string pathPattern, Action<HttpListenerContext> action)
        {
            Method = method;
            PathPattern = pathPattern;
            Action = action;
        }

        public bool Accept(HttpListenerContext context)
        {
            return context.MatchesMethod(Method) && context.MatchesPath(PathPattern);;
        }
    }

    public static class HttpListenerContextExtensions
    {
        public static bool MatchesMethod(this HttpListenerContext context, HttpMethod method)
        {
            return string.Equals(method.Method, context.Request.HttpMethod, StringComparison.CurrentCultureIgnoreCase);
        }

        public static bool MatchesPath(this HttpListenerContext context, string expectedRegexPattern)
        {
            return Regex.IsMatch(context.Request.Url.LocalPath, expectedRegexPattern);
        }
    }
}
