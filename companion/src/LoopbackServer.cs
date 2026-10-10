using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;

namespace TartarusIO.Companion
{
    public sealed class LoopbackServer : IDisposable
    {
        public const int Port = 47651;
        private readonly string _webRoot;
        private readonly Func<CompanionStatus> _getStatus;
        private readonly Func<string> _getConfiguration;
        private readonly Action<string> _saveConfiguration;
        private readonly Func<ApplicationCatalog> _getApplications;
        private readonly Func<string, ApplicationBrowseResult> _browseApplication;
        private readonly Func<string, ApiResult> _activateProfile;
        private readonly Func<bool, ApiResult> _setRemapping;
        private readonly Func<ApiResult> _configureHidHide;
        private readonly JavaScriptSerializer _serializer;
        private TcpListener _listener;
        private Thread _listenerThread;
        private volatile bool _running;

        public LoopbackServer(
            string webRoot,
            Func<CompanionStatus> getStatus,
            Func<string> getConfiguration,
            Action<string> saveConfiguration,
            Func<ApplicationCatalog> getApplications,
            Func<string, ApplicationBrowseResult> browseApplication,
            Func<string, ApiResult> activateProfile,
            Func<bool, ApiResult> setRemapping,
            Func<ApiResult> configureHidHide)
        {
            _webRoot = Path.GetFullPath(webRoot);
            _getStatus = getStatus;
            _getConfiguration = getConfiguration;
            _saveConfiguration = saveConfiguration;
            _getApplications = getApplications;
            _browseApplication = browseApplication;
            _activateProfile = activateProfile;
            _setRemapping = setRemapping;
            _configureHidHide = configureHidHide;
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = Int32.MaxValue;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
            _running = true;
            _listenerThread = new Thread(Listen);
            _listenerThread.IsBackground = true;
            _listenerThread.Name = "Tartarus IO Loopback Server";
            _listenerThread.Start();
            Logger.Info("Loopback server started on http://127.0.0.1:" + Port + "/");
        }

        public void Dispose()
        {
            _running = false;
            if (_listener != null)
            {
                _listener.Stop();
            }
        }

        private void Listen()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch (SocketException)
                {
                    if (_running)
                    {
                        Logger.Info("The loopback server stopped accepting connections.");
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;
                    var request = ReadRequest(stream);
                    if (request == null)
                    {
                        return;
                    }
                    if (!IsTrustedRequest(request))
                    {
                        WriteJson(stream, 403, new ApiResult { ok = false, message = "The request origin is not allowed." });
                        return;
                    }
                    Route(request, stream);
                }
                catch (Exception exception)
                {
                    Logger.Error("A loopback request failed.", exception);
                    TryWriteError(stream, 500, "The companion could not process the request.");
                }
            }
        }

        private void Route(HttpRequestData request, Stream stream)
        {
            if (request.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                RouteApi(request, stream);
                return;
            }

            if (!String.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(stream, 405, new ApiResult { ok = false, message = "Method not allowed." });
                return;
            }

            ServeStaticFile(request.Path, stream);
        }

        private void RouteApi(HttpRequestData request, Stream stream)
        {
            if (request.Method != "GET" && !HasJsonContentType(request))
            {
                WriteJson(stream, 400, new ApiResult { ok = false, message = "API writes require application/json." });
                return;
            }

            if (request.Method == "GET" && request.Path == "/api/status")
            {
                WriteJson(stream, 200, _getStatus());
                return;
            }

            if (request.Path == "/api/config")
            {
                if (request.Method == "GET")
                {
                    WriteRawJson(stream, 200, _getConfiguration());
                    return;
                }

                if (request.Method == "PUT")
                {
                    try
                    {
                        _saveConfiguration(request.Body);
                        WriteJson(stream, 200, new ApiResult { ok = true, message = "Configuration saved." });
                    }
                    catch (InvalidDataException exception)
                    {
                        WriteJson(stream, 400, new ApiResult { ok = false, message = exception.Message });
                    }
                    return;
                }
            }

            if (request.Method == "GET" && request.Path == "/api/applications")
            {
                WriteJson(stream, 200, _getApplications());
                return;
            }

            if (request.Method == "POST" && request.Path == "/api/applications/browse")
            {
                ApplicationBrowseRequest body;
                try
                {
                    body = _serializer.Deserialize<ApplicationBrowseRequest>(request.Body);
                }
                catch
                {
                    body = null;
                }

                var result = _browseApplication(body == null ? null : body.initialDirectory);
                WriteJson(stream, 200, result);
                return;
            }

            if (request.Method == "POST" && request.Path == "/api/profiles/activate")
            {
                ProfileActivationRequest body;
                try
                {
                    body = _serializer.Deserialize<ProfileActivationRequest>(request.Body);
                }
                catch
                {
                    body = null;
                }

                if (body == null || String.IsNullOrWhiteSpace(body.profileId))
                {
                    WriteJson(stream, 400, new ApiResult { ok = false, message = "A profile id is required." });
                    return;
                }

                var result = _activateProfile(body.profileId);
                WriteJson(stream, result.ok ? 200 : 409, result);
                return;
            }

            if (request.Method == "POST" && request.Path == "/api/remapping")
            {
                RemappingRequest body;
                try
                {
                    body = _serializer.Deserialize<RemappingRequest>(request.Body);
                }
                catch
                {
                    body = null;
                }

                if (body == null)
                {
                    WriteJson(stream, 400, new ApiResult { ok = false, message = "A remapping state is required." });
                    return;
                }

                var result = _setRemapping(body.enabled);
                WriteJson(stream, result.ok ? 200 : 409, result);
                return;
            }

            if (request.Method == "POST" && request.Path == "/api/hidhide/configure")
            {
                var result = _configureHidHide();
                WriteJson(stream, result.ok ? 200 : 409, result);
                return;
            }

            WriteJson(stream, 404, new ApiResult { ok = false, message = "API route not found." });
        }

        private void ServeStaticFile(string requestPath, Stream stream)
        {
            var relativePath = requestPath == "/" ? "index.html" : HttpUtility.UrlDecode(requestPath.TrimStart('/'));
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(_webRoot, relativePath));
            var rootPrefix = _webRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            {
                WriteText(stream, 404, "text/plain; charset=utf-8", "Not found.");
                return;
            }

            var contentType = GetContentType(candidate);
            var content = File.ReadAllBytes(candidate);
            WriteResponse(stream, 200, contentType, content);
        }

        private HttpRequestData ReadRequest(Stream stream)
        {
            var headerBytes = new List<byte>();
            var matched = 0;
            while (headerBytes.Count < 65536)
            {
                var value = stream.ReadByte();
                if (value < 0)
                {
                    return null;
                }

                headerBytes.Add((byte)value);
                if ((matched == 0 || matched == 2) && value == '\r')
                {
                    matched += 1;
                }
                else if ((matched == 1 || matched == 3) && value == '\n')
                {
                    matched += 1;
                    if (matched == 4)
                    {
                        break;
                    }
                }
                else
                {
                    matched = value == '\r' ? 1 : 0;
                }
            }

            if (matched != 4)
            {
                throw new InvalidDataException("HTTP headers were too large.");
            }

            var headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2)
            {
                throw new InvalidDataException("The HTTP request line is invalid.");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 1; index < lines.Length; index += 1)
            {
                var separator = lines[index].IndexOf(':');
                if (separator > 0)
                {
                    headers[lines[index].Substring(0, separator).Trim()] =
                        lines[index].Substring(separator + 1).Trim();
                }
            }

            var contentLength = 0;
            string contentLengthValue;
            if (headers.TryGetValue("Content-Length", out contentLengthValue))
            {
                Int32.TryParse(contentLengthValue, out contentLength);
            }
            if (contentLength < 0 || contentLength > 8 * 1024 * 1024)
            {
                throw new InvalidDataException("The HTTP request body is too large.");
            }

            var bodyBytes = new byte[contentLength];
            var offset = 0;
            while (offset < contentLength)
            {
                var read = stream.Read(bodyBytes, offset, contentLength - offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException("The HTTP request body ended unexpectedly.");
                }
                offset += read;
            }

            var target = requestLine[1];
            var queryIndex = target.IndexOf('?');
            if (queryIndex >= 0)
            {
                target = target.Substring(0, queryIndex);
            }

            return new HttpRequestData
            {
                Method = requestLine[0].ToUpperInvariant(),
                Path = target,
                Headers = headers,
                Body = Encoding.UTF8.GetString(bodyBytes)
            };
        }

        private void WriteJson(Stream stream, int statusCode, object value)
        {
            WriteRawJson(stream, statusCode, _serializer.Serialize(value));
        }

        private static void WriteRawJson(Stream stream, int statusCode, string json)
        {
            WriteText(stream, statusCode, "application/json; charset=utf-8", json);
        }

        private static void WriteText(Stream stream, int statusCode, string contentType, string content)
        {
            WriteResponse(stream, statusCode, contentType, Encoding.UTF8.GetBytes(content));
        }

        private static void WriteResponse(Stream stream, int statusCode, string contentType, byte[] content)
        {
            var reason = GetReasonPhrase(statusCode);
            var headers = String.Format(
                "HTTP/1.1 {0} {1}\r\nContent-Type: {2}\r\nContent-Length: {3}\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; frame-ancestors 'none'\r\n\r\n",
                statusCode,
                reason,
                contentType,
                content.Length);
            var headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(content, 0, content.Length);
            stream.Flush();
        }

        private static void TryWriteError(Stream stream, int statusCode, string message)
        {
            try
            {
                WriteText(stream, statusCode, "text/plain; charset=utf-8", message);
            }
            catch
            {
                // The client may already have closed the connection.
            }
        }

        private static string GetReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 200: return "OK";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 409: return "Conflict";
                default: return "Internal Server Error";
            }
        }

        private static string GetContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".js": return "application/javascript; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".png": return "image/png";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".md": return "text/markdown; charset=utf-8";
                default: return "application/octet-stream";
            }
        }

        private static bool IsTrustedRequest(HttpRequestData request)
        {
            string host;
            if (!request.Headers.TryGetValue("Host", out host)
                || (!String.Equals(host, "127.0.0.1:" + Port, StringComparison.OrdinalIgnoreCase)
                    && !String.Equals(host, "localhost:" + Port, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string fetchSite;
            if (request.Headers.TryGetValue("Sec-Fetch-Site", out fetchSite)
                && !String.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase)
                && !String.Equals(fetchSite, "none", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string origin;
            if (request.Headers.TryGetValue("Origin", out origin)
                && !String.Equals(origin, "http://127.0.0.1:" + Port, StringComparison.OrdinalIgnoreCase)
                && !String.Equals(origin, "http://localhost:" + Port, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool HasJsonContentType(HttpRequestData request)
        {
            string contentType;
            return request.Headers.TryGetValue("Content-Type", out contentType)
                   && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class HttpRequestData
        {
            public string Method { get; set; }
            public string Path { get; set; }
            public Dictionary<string, string> Headers { get; set; }
            public string Body { get; set; }
        }
    }
}
