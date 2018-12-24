using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleAppTest.Redis
{
    public class PooledSimpleRedisClient
    {
        private readonly RedisClient[] _simpleRedisClients;
        private int PoolSize { get; }
        private int Db { get; }
        public string Host { get; }
        public int Port { get; }
        public string Password { get; set; }
        public int? PoolTimeout { get; set; }
        public int RecheckPoolAfterMs { get; } = 100;

        public PooledSimpleRedisClient(string host, int port = 6379, int db = 0, int poolSize = 5)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            PoolSize = poolSize;
            Db = db;
            _simpleRedisClients = new RedisClient[PoolSize];
        }

        public void Set(string key, string value)
        {
            var client = GetSimpleRedisClient();
            client.Set(key, Encoding.UTF8.GetBytes(value));
            client.Active = false;
        }

        public bool Set(string key, byte[] value)
        {
            var client = GetSimpleRedisClient();
            client.Set(key, value);
            client.Active = false;
            return true;
        }

//        public void SetAll(IDictionary<string, byte[]> dict)
//        {
//            var client = GetSimpleRedisClient();
//            client.MSet(dict.Keys, dict.Values);
//            client.Active = false;
//        }

        public bool Ping()
        {
            return GetSimpleRedisClient().Ping();
        }

        public bool Remove(string key)
        {
            var client = GetSimpleRedisClient();
            var succeed = client.Del(key);
            client.Active = false;
            return true;
        }

        public byte[] Get(string key)
        {
            var client = GetSimpleRedisClient();
            var value = client.Get(key);
            client.Active = false;
            return value;
        }

        public string GetString(string key)
        {
            var client = GetSimpleRedisClient();
            var value = Encoding.UTF8.GetString(Get(key));
            client.Active = false;
            return value;
        }

        private RedisClient GetSimpleRedisClient()
        {
            lock (_simpleRedisClients)
            {
                RedisClient inActiveClient;
                while ((inActiveClient = GetInActiveSimpleRedisClient()) == null)
                {
                    if (PoolTimeout.HasValue)
                    {
                        // wait for a connection, cry out if made to wait too long
                        if (!Monitor.Wait(_simpleRedisClients, PoolTimeout.Value))
                            throw new TimeoutException("Pool timeout error.");
                    }
                    else
                        Monitor.Wait(_simpleRedisClients, RecheckPoolAfterMs);
                }

                inActiveClient.Active = true;
                return inActiveClient;
            }
        }

        private RedisClient GetInActiveSimpleRedisClient()
        {
            for (var i = 0; i < _simpleRedisClients.Length; i++)
            {
                if (_simpleRedisClients[i] != null && !_simpleRedisClients[i].Active && !_simpleRedisClients[i].HadExceptions)
                    return _simpleRedisClients[i];

                if (_simpleRedisClients[i] == null || _simpleRedisClients[i].HadExceptions)
                {
                    if (_simpleRedisClients[i] != null)
                        _simpleRedisClients[i].Dispose();
                    var client = new RedisClient(Host, Port, Password, Db);

                    _simpleRedisClients[i] = client;
                    return client;
                }
            }

            return null;
        }
    }

    public class RedisClient
    {
        public const long DefaultDb = 0;
        public const int DefaultPort = 6379;
        public const string DefaultHost = "localhost";
        public const int DefaultIdleTimeOutSecs = 240; //default on redis is 300

        internal const int Success = 1;
        internal const int OneGb = 1073741824;
        private readonly byte[] endData = new[] {(byte) '\r', (byte) '\n'};

        private int clientPort;
        private string lastCommand;
        private SocketException lastSocketException;
        public bool HadExceptions { get; protected set; }

        protected Socket socket;
        protected BufferedStream Bstream;

        private Dictionary<string, string> info;

        /// <summary>
        /// Used to manage connection pooling
        /// </summary>
        internal bool Active { get; set; }

        internal long LastConnectedAtTimestamp;

        public long Id { get; set; }

        public string Host { get; private set; }
        public int Port { get; private set; }

        /// <summary>
        /// Gets or sets object key prefix.
        /// </summary>
        public string NamespacePrefix { get; set; }

        public int ConnectTimeout { get; set; }
        public int RetryTimeout { get; set; }
        public int RetryCount { get; set; }
        public int SendTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public string Password { get; set; }
        public int IdleTimeOutSecs { get; set; }

        public RedisClient(string host)
            : this(host, 6379)
        {
        }

        public RedisClient(string host, int port)
            : this(host, port, null)
        {
        }

        public RedisClient(string host, int port, string password = null, long db = DefaultDb)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            Host = host;
            Port = port;
            SendTimeout = -1;
            ReceiveTimeout = -1;
            Password = password;
            Db = db;
            IdleTimeOutSecs = DefaultIdleTimeOutSecs;
        }

        long db;

        public long Db
        {
            get { return db; }

            set { db = value; }
        }

        public bool Ping()
        {
            return SendExpectCode(Commands.Ping) == "PONG";
        }

        public void Quit()
        {
            SendCommand(Commands.Quit);
        }

        public void Set(string key, byte[] value)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            value = value ?? new byte[0];

            if (value.Length > OneGb)
                throw new ArgumentException("value exceeds 1G", "value");

            SendExpectSuccess(Commands.Set, key.ToUtf8Bytes(), value);
        }

        public void MSet(byte[][] keys, byte[][] values)
        {
            var keysAndValues = MergeCommandWithKeysAndValues(Commands.MSet, keys, values);

            SendExpectSuccess(keysAndValues);
        }

        public byte[] Get(string key)
        {
            return GetBytes(key);
        }

        public byte[] GetBytes(string key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectData(Commands.Get, key.ToUtf8Bytes());
        }

        public long Exists(string key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLong(Commands.Exists, key.ToUtf8Bytes());
        }

        public long Del(string key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            return SendExpectLong(Commands.Del, key.ToUtf8Bytes());
        }

        public long Del(params string[] keys)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");

            var cmdWithArgs = MergeCommandWithArgs(Commands.Del, keys);
            return SendExpectLong(cmdWithArgs);
        }

        private void Connect()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout = SendTimeout,
                ReceiveTimeout = ReceiveTimeout
            };
            try
            {
                if (ConnectTimeout == 0)
                {
                    socket.Connect(Host, Port);
                }
                else
                {
                    var connectResult = socket.BeginConnect(Host, Port, null, null);
                    connectResult.AsyncWaitHandle.WaitOne(ConnectTimeout, true);
                }

                if (!socket.Connected)
                {
                    socket.Close();
                    socket = null;
                    HadExceptions = true;
                    return;
                }

                Bstream = new BufferedStream(new NetworkStream(socket), 16 * 1024);

                if (Password != null)
                    SendExpectSuccess(Commands.Auth, Password.ToUtf8Bytes());

                if (db != 0)
                    SendExpectSuccess(Commands.Select, db.ToUtf8Bytes());

                var ipEndpoint = socket.LocalEndPoint as IPEndPoint;
                clientPort = ipEndpoint != null ? ipEndpoint.Port : -1;
                lastCommand = null;
                lastSocketException = null;
                LastConnectedAtTimestamp = Stopwatch.GetTimestamp();
            }
            catch (SocketException ex)
            {
                if (socket != null)
                    socket.Close();
                socket = null;

                HadExceptions = true;
                var throwEx = new Exception("could not connect to redis Instance at " + Host + ":" + Port, ex);
                Log(throwEx.Message, ex);
                throw throwEx;
            }
        }

        internal bool IsDisposed { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RedisClient()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //dispose un managed resources
                DisposeConnection();
            }
        }

        internal void DisposeConnection()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            if (socket == null) return;

            try
            {
                Quit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when trying to Quit(), {ex}");
            }
            finally
            {
                SafeConnectionClose();
            }
        }

        private bool Reconnect()
        {
            var previousDb = db;

            SafeConnectionClose();
            Connect(); //sets db to 0

            if (previousDb != DefaultDb) Db = previousDb;

            return socket != null;
        }

        private void SafeConnectionClose()
        {
            try
            {
                // workaround for a .net bug: http://support.microsoft.com/kb/821625
                if (Bstream != null)
                    Bstream.Close();
            }
            catch
            {
            }

            try
            {
                if (socket != null)
                    socket.Close();
            }
            catch
            {
            }

            Bstream = null;
            socket = null;
        }

        protected string ReadLine()
        {
            var sb = new StringBuilder();

            int c;
            while ((c = Bstream.ReadByte()) != -1)
            {
                if (c == '\r')
                    continue;
                if (c == '\n')
                    break;
                sb.Append((char) c);
            }

            return sb.ToString();
        }

        public bool IsSocketConnected()
        {
            var part1 = socket.Poll(1000, SelectMode.SelectRead);
            var part2 = (socket.Available == 0);
            return !(part1 & part2);
        }

        private bool AssertConnectedSocket()
        {
            if (LastConnectedAtTimestamp > 0)
            {
                var now = Stopwatch.GetTimestamp();
                var elapsedSecs = (now - LastConnectedAtTimestamp) / Stopwatch.Frequency;

                if (socket == null || (elapsedSecs > IdleTimeOutSecs && !socket.IsConnected()))
                {
                    return Reconnect();
                }

                LastConnectedAtTimestamp = now;
            }

            if (socket == null)
            {
                Connect();
            }

            var isConnected = socket != null;

            return isConnected;
        }

        private bool HandleSocketException(SocketException ex)
        {
            HadExceptions = true;
            Console.WriteLine($"SocketException: {ex}");

            lastSocketException = ex;

            // timeout?
            socket.Close();
            socket = null;

            return false;
        }

        private RedisResponseException CreateResponseError(string error)
        {
            HadExceptions = true;
            var throwEx = new RedisResponseException(
                string.Format("{0}, sPort: {1}, LastCommand: {2}",
                    error, clientPort, lastCommand));
            Log(throwEx.Message);
            throw throwEx;
        }

        private RedisException CreateConnectionError()
        {
            HadExceptions = true;
            var throwEx = new RedisException(
                string.Format("Unable to Connect: sPort: {0}",
                    clientPort), lastSocketException);
            Log(throwEx.Message);
            throw throwEx;
        }

        private static byte[] GetCmdBytes(char cmdPrefix, int noOfLines)
        {
            var strLines = noOfLines.ToString();
            var strLinesLength = strLines.Length;

            var cmdBytes = new byte[1 + strLinesLength + 2];
            cmdBytes[0] = (byte) cmdPrefix;

            for (var i = 0; i < strLinesLength; i++)
                cmdBytes[i + 1] = (byte) strLines[i];

            cmdBytes[1 + strLinesLength] = 0x0D; // \r
            cmdBytes[2 + strLinesLength] = 0x0A; // \n

            return cmdBytes;
        }

        /// <summary>
        /// Command to set multuple binary safe arguments
        /// </summary>
        /// <param name="cmdWithBinaryArgs"></param>
        /// <returns></returns>
        protected bool SendCommand(params byte[][] cmdWithBinaryArgs)
        {
            if (!AssertConnectedSocket()) return false;

            try
            {
                CmdLog(cmdWithBinaryArgs);

                //Total command lines count
                WriteAllToSendBuffer(cmdWithBinaryArgs);

                FlushSendBuffer();
            }
            catch (SocketException ex)
            {
                cmdBuffer.Clear();
                return HandleSocketException(ex);
            }

            return true;
        }

        public void WriteAllToSendBuffer(params byte[][] cmdWithBinaryArgs)
        {
            WriteToSendBuffer(GetCmdBytes('*', cmdWithBinaryArgs.Length));

            foreach (var safeBinaryValue in cmdWithBinaryArgs)
            {
                WriteToSendBuffer(GetCmdBytes('$', safeBinaryValue.Length));
                WriteToSendBuffer(safeBinaryValue);
                WriteToSendBuffer(endData);
            }
        }

        readonly IList<ArraySegment<byte>> cmdBuffer = new List<ArraySegment<byte>>();
        byte[] currentBuffer = BufferPool.GetBuffer();
        int currentBufferIndex;

        public void WriteToSendBuffer(byte[] cmdBytes)
        {
            if (CouldAddToCurrentBuffer(cmdBytes)) return;

            PushCurrentBuffer();

            if (CouldAddToCurrentBuffer(cmdBytes)) return;

            var bytesCopied = 0;
            while (bytesCopied < cmdBytes.Length)
            {
                var copyOfBytes = BufferPool.GetBuffer();
                var bytesToCopy = Math.Min(cmdBytes.Length - bytesCopied, copyOfBytes.Length);
                Buffer.BlockCopy(cmdBytes, bytesCopied, copyOfBytes, 0, bytesToCopy);
                cmdBuffer.Add(new ArraySegment<byte>(copyOfBytes, 0, bytesToCopy));
                bytesCopied += bytesToCopy;
            }
        }

        private bool CouldAddToCurrentBuffer(byte[] cmdBytes)
        {
            if (cmdBytes.Length + currentBufferIndex < BufferPool.BufferLength)
            {
                Buffer.BlockCopy(cmdBytes, 0, currentBuffer, currentBufferIndex, cmdBytes.Length);
                currentBufferIndex += cmdBytes.Length;
                return true;
            }

            return false;
        }

        private void PushCurrentBuffer()
        {
            cmdBuffer.Add(new ArraySegment<byte>(currentBuffer, 0, currentBufferIndex));
            currentBuffer = BufferPool.GetBuffer();
            currentBufferIndex = 0;
        }

        public void FlushSendBuffer()
        {
            if (currentBufferIndex > 0)
                PushCurrentBuffer();

            //Sendling IList<ArraySegment> Throws 'Message to Large' SocketException in Mono
            foreach (var segment in cmdBuffer)
            {
                var buffer = segment.Array;
                socket.Send(buffer, segment.Offset, segment.Count, SocketFlags.None);
            }

            ResetSendBuffer();
        }

        /// <summary>
        /// reset buffer index in send buffer
        /// </summary>
        public void ResetSendBuffer()
        {
            currentBufferIndex = 0;
            for (int i = cmdBuffer.Count - 1; i >= 0; i--)
            {
                var buffer = cmdBuffer[i].Array;
                BufferPool.ReleaseBufferToPool(ref buffer);
                cmdBuffer.RemoveAt(i);
            }
        }

        private int SafeReadByte()
        {
            return Bstream.ReadByte();
        }

        protected void SendExpectSuccess(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            ExpectSuccess();
        }

        protected long SendExpectLong(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ReadLong();
        }

        protected byte[] SendExpectData(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ReadData();
        }

        protected string SendExpectString(params byte[][] cmdWithBinaryArgs)
        {
            var bytes = SendExpectData(cmdWithBinaryArgs);
            return bytes.FromUtf8Bytes();
        }

        protected double SendExpectDouble(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ReadDouble();
        }

        public double ReadDouble()
        {
            var bytes = ReadData();
            return (bytes == null) ? double.NaN : ParseDouble(bytes);
        }

        public static double ParseDouble(byte[] doubleBytes)
        {
            var doubleString = Encoding.UTF8.GetString(doubleBytes);

            double d;
            double.TryParse(doubleString, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat, out d);

            return d;
        }

        protected string SendExpectCode(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ExpectCode();
        }

        protected byte[][] SendExpectMultiData(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ReadMultiData();
        }

        protected object[] SendExpectDeeplyNestedMultiData(params byte[][] cmdWithBinaryArgs)
        {
            if (!SendCommand(cmdWithBinaryArgs))
                throw CreateConnectionError();

            return ReadDeeplyNestedMultiData();
        }

        [Conditional("DEBUG")]
        protected void Log(string fmt, params object[] args)
        {
            //Console.WriteLine("{0}", string.Format(fmt, args).Trim());
        }

        [Conditional("DEBUG")]
        protected void CmdLog(byte[][] args)
        {
//            var sb = new StringBuilder();
//            foreach (var arg in args)
//            {
//                if (sb.Length > 0)
//                    sb.Append(" ");
//
//                sb.Append(arg.FromUtf8Bytes());
//            }
//
//            this.lastCommand = sb.ToString();
//            if (this.lastCommand.Length > 100)
//            {
//                this.lastCommand = this.lastCommand.Substring(0, 100) + "...";
//            }
//
//            Console.WriteLine("S: " + this.lastCommand);
        }

        protected void ExpectSuccess()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();

            Log((char) c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") && s.Length >= 4 ? s.Substring(4) : s);
        }

        private void ExpectWord(string word)
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();

            Log((char) c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            if (s != word)
                throw CreateResponseError(string.Format("Expected '{0}' got '{1}'", word, s));
        }

        private string ExpectCode()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();

            Log((char) c + s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            return s;
        }

        internal void ExpectOk()
        {
            ExpectWord("OK");
        }

        internal void ExpectQueued()
        {
            ExpectWord("QUEUED");
        }

        public long ReadInt()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();

            Log("R: {0}", s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            if (c == ':' || c == '$') //really strange why ZRANK needs the '$' here
            {
                int i;
                if (int.TryParse(s, out i))
                    return i;
            }

            throw CreateResponseError("Unknown reply on integer response: " + c + s);
        }

        public long ReadLong()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();

            Log("R: {0}", s);

            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

            if (c == ':' || c == '$') //really strange why ZRANK needs the '$' here
            {
                long i;
                if (long.TryParse(s, out i))
                    return i;
            }

            throw CreateResponseError("Unknown reply on integer response: " + c + s);
        }

        private byte[] ReadData()
        {
            var r = ReadLine();
            return ParseSingleLine(r);
        }

        private byte[] ParseSingleLine(string r)
        {
            Log("R: {0}", r);
            if (r.Length == 0)
                throw CreateResponseError("Zero length respose");

            char c = r[0];
            if (c == '-')
                throw CreateResponseError(r.StartsWith("-ERR") ? r.Substring(5) : r.Substring(1));

            if (c == '$')
            {
                if (r == "$-1")
                    return null;
                int count;

                if (Int32.TryParse(r.Substring(1), out count))
                {
                    var retbuf = new byte[count];

                    var offset = 0;
                    while (count > 0)
                    {
                        var readCount = Bstream.Read(retbuf, offset, count);
                        if (readCount <= 0)
                            throw CreateResponseError("Unexpected end of Stream");

                        offset += readCount;
                        count -= readCount;
                    }

                    if (Bstream.ReadByte() != '\r' || Bstream.ReadByte() != '\n')
                        throw CreateResponseError("Invalid termination");

                    return retbuf;
                }

                throw CreateResponseError("Invalid length");
            }

            if (c == ':')
            {
                //match the return value
                return r.Substring(1).ToUtf8Bytes();
            }

            throw CreateResponseError("Unexpected reply: " + r);
        }

        private byte[][] ReadMultiData()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();
            Log("R: {0}", s);

            switch (c)
            {
                // Some commands like BRPOPLPUSH may return Bulk Reply instead of Multi-bulk
                case '$':
                    var t = new byte[2][];
                    t[1] = ParseSingleLine(string.Concat(char.ToString((char) c), s));
                    return t;

                case '-':
                    throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

                case '*':
                    int count;
                    if (int.TryParse(s, out count))
                    {
                        if (count == -1)
                        {
                            //redis is in an invalid state
                            return new byte[0][];
                        }

                        var result = new byte[count][];

                        for (int i = 0; i < count; i++)
                            result[i] = ReadData();

                        return result;
                    }

                    break;
            }

            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }

        private object[] ReadDeeplyNestedMultiData()
        {
            return (object[]) ReadDeeplyNestedMultiDataItem();
        }

        private object ReadDeeplyNestedMultiDataItem()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();
            Log("R: {0}", s);

            switch (c)
            {
                case '$':
                    return ParseSingleLine(string.Concat(char.ToString((char) c), s));

                case '-':
                    throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);

                case '*':
                    int count;
                    if (int.TryParse(s, out count))
                    {
                        var array = new object[count];
                        for (int i = 0; i < count; i++)
                        {
                            array[i] = ReadDeeplyNestedMultiDataItem();
                        }

                        return array;
                    }

                    break;

                default:
                    return s;
            }

            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }

        internal int ReadMultiDataResultCount()
        {
            int c = SafeReadByte();
            if (c == -1)
                throw CreateResponseError("No more data");

            var s = ReadLine();
            Log("R: {0}", s);
            if (c == '-')
                throw CreateResponseError(s.StartsWith("ERR") ? s.Substring(4) : s);
            if (c == '*')
            {
                int count;
                if (int.TryParse(s, out count))
                {
                    return count;
                }
            }

            throw CreateResponseError("Unknown reply on multi-request: " + c + s);
        }

        private static void AssertListIdAndValue(string listId, byte[] value)
        {
            if (listId == null)
                throw new ArgumentNullException("listId");
            if (value == null)
                throw new ArgumentNullException("value");
        }

        private static byte[][] MergeCommandWithKeysAndValues(byte[] cmd, byte[][] keys, byte[][] values)
        {
            var firstParams = new[] {cmd};
            return MergeCommandWithKeysAndValues(firstParams, keys, values);
        }

        private static byte[][] MergeCommandWithKeysAndValues(byte[] cmd, byte[] firstArg, byte[][] keys, byte[][] values)
        {
            var firstParams = new[] {cmd, firstArg};
            return MergeCommandWithKeysAndValues(firstParams, keys, values);
        }

        private static byte[][] MergeCommandWithKeysAndValues(byte[][] firstParams,
            byte[][] keys, byte[][] values)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentNullException("keys");
            if (values == null || values.Length == 0)
                throw new ArgumentNullException("values");
            if (keys.Length != values.Length)
                throw new ArgumentException("The number of values must be equal to the number of keys");

            var keyValueStartIndex = (firstParams != null) ? firstParams.Length : 0;

            var keysAndValuesLength = keys.Length * 2 + keyValueStartIndex;
            var keysAndValues = new byte[keysAndValuesLength][];

            for (var i = 0; i < keyValueStartIndex; i++)
            {
                keysAndValues[i] = firstParams[i];
            }

            var j = 0;
            for (var i = keyValueStartIndex; i < keysAndValuesLength; i += 2)
            {
                keysAndValues[i] = keys[j];
                keysAndValues[i + 1] = values[j];
                j++;
            }

            return keysAndValues;
        }

        private static byte[][] MergeCommandWithArgs(byte[] cmd, params string[] args)
        {
            var byteArgs = args.ToMultiByteArray();
            return MergeCommandWithArgs(cmd, byteArgs);
        }

        private static byte[][] MergeCommandWithArgs(byte[] cmd, params byte[][] args)
        {
            var mergedBytes = new byte[1 + args.Length][];
            mergedBytes[0] = cmd;
            for (var i = 0; i < args.Length; i++)
            {
                mergedBytes[i + 1] = args[i];
            }

            return mergedBytes;
        }

        private static byte[][] MergeCommandWithArgs(byte[] cmd, byte[] firstArg, params byte[][] args)
        {
            var mergedBytes = new byte[2 + args.Length][];
            mergedBytes[0] = cmd;
            mergedBytes[1] = firstArg;
            for (var i = 0; i < args.Length; i++)
            {
                mergedBytes[i + 2] = args[i];
            }

            return mergedBytes;
        }

        protected byte[][] ConvertToBytes(string[] keys)
        {
            var keyBytes = new byte[keys.Length][];
            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                keyBytes[i] = key != null ? key.ToUtf8Bytes() : new byte[0];
            }

            return keyBytes;
        }

        protected byte[][] MergeAndConvertToBytes(string[] keys, string[] args)
        {
            if (keys == null)
                keys = new string[0];
            if (args == null)
                args = new string[0];

            var keysLength = keys.Length;
            var merged = new string[keysLength + args.Length];
            for (var i = 0; i < merged.Length; i++)
            {
                merged[i] = i < keysLength ? keys[i] : args[i - keysLength];
            }

            return ConvertToBytes(merged);
        }
    }

    public class RedisException
        : Exception
    {
        public RedisException(string message)
            : base(message)
        {
        }

        public RedisException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal static class RedisExtensions
    {
        public static byte[][] ToMultiByteArray(this string[] args)
        {
            var byteArgs = new byte[args.Length][];
            for (var i = 0; i < args.Length; ++i)
                byteArgs[i] = args[i].ToUtf8Bytes();
            return byteArgs;
        }

        public static bool IsConnected(this Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        public static string FromUtf8Bytes(this byte[] bytes)
        {
            return bytes == null
                ? null
                : Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        public static byte[] ToUtf8Bytes(this string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        public static byte[] ToUtf8Bytes(this int intVal)
        {
            return FastToUtf8Bytes(intVal.ToString());
        }

        public static byte[] ToUtf8Bytes(this long longVal)
        {
            return FastToUtf8Bytes(longVal.ToString());
        }

        public static byte[] ToUtf8Bytes(this ulong ulongVal)
        {
            return FastToUtf8Bytes(ulongVal.ToString());
        }

        /// <summary>
        /// Skip the encoding process for 'safe strings' 
        /// </summary>
        /// <param name="strVal"></param>
        /// <returns></returns>
        private static byte[] FastToUtf8Bytes(string strVal)
        {
            var bytes = new byte[strVal.Length];
            for (var i = 0; i < strVal.Length; i++)
                bytes[i] = (byte) strVal[i];

            return bytes;
        }
    }

    public static class Commands
    {
        public readonly static byte[] Quit = "QUIT".ToUtf8Bytes();
        public readonly static byte[] Auth = "AUTH".ToUtf8Bytes();
        public readonly static byte[] Exists = "EXISTS".ToUtf8Bytes();
        public readonly static byte[] Del = "DEL".ToUtf8Bytes();
        public readonly static byte[] Type = "TYPE".ToUtf8Bytes();
        public readonly static byte[] Keys = "KEYS".ToUtf8Bytes();
        public readonly static byte[] RandomKey = "RANDOMKEY".ToUtf8Bytes();
        public readonly static byte[] Rename = "RENAME".ToUtf8Bytes();
        public readonly static byte[] RenameNx = "RENAMENX".ToUtf8Bytes();
        public readonly static byte[] PExpire = "PEXPIRE".ToUtf8Bytes();
        public readonly static byte[] PExpireAt = "PEXPIREAT".ToUtf8Bytes();
        public readonly static byte[] DbSize = "DBSIZE".ToUtf8Bytes();
        public readonly static byte[] Expire = "EXPIRE".ToUtf8Bytes();
        public readonly static byte[] ExpireAt = "EXPIREAT".ToUtf8Bytes();
        public readonly static byte[] Ttl = "TTL".ToUtf8Bytes();
        public readonly static byte[] PTtl = "PTTL".ToUtf8Bytes();
        public readonly static byte[] Select = "SELECT".ToUtf8Bytes();
        public readonly static byte[] FlushDb = "FLUSHDB".ToUtf8Bytes();
        public readonly static byte[] FlushAll = "FLUSHALL".ToUtf8Bytes();
        public readonly static byte[] Ping = "PING".ToUtf8Bytes();
        public readonly static byte[] Echo = "ECHO".ToUtf8Bytes();

        public readonly static byte[] Save = "SAVE".ToUtf8Bytes();
        public readonly static byte[] BgSave = "BGSAVE".ToUtf8Bytes();
        public readonly static byte[] LastSave = "LASTSAVE".ToUtf8Bytes();
        public readonly static byte[] Shutdown = "SHUTDOWN".ToUtf8Bytes();
        public readonly static byte[] BgRewriteAof = "BGREWRITEAOF".ToUtf8Bytes();

        public readonly static byte[] Info = "INFO".ToUtf8Bytes();
        public readonly static byte[] SlaveOf = "SLAVEOF".ToUtf8Bytes();
        public readonly static byte[] No = "NO".ToUtf8Bytes();
        public readonly static byte[] One = "ONE".ToUtf8Bytes();
        public readonly static byte[] ResetStat = "RESETSTAT".ToUtf8Bytes();
        public readonly static byte[] Time = "TIME".ToUtf8Bytes();
        public readonly static byte[] Segfault = "SEGFAULT".ToUtf8Bytes();
        public readonly static byte[] Dump = "DUMP".ToUtf8Bytes();
        public readonly static byte[] Restore = "RESTORE".ToUtf8Bytes();
        public readonly static byte[] Migrate = "MIGRATE".ToUtf8Bytes();
        public readonly static byte[] Move = "MOVE".ToUtf8Bytes();
        public readonly static byte[] Object = "OBJECT".ToUtf8Bytes();
        public readonly static byte[] IdleTime = "IDLETIME".ToUtf8Bytes();
        public readonly static byte[] Monitor = "MONITOR".ToUtf8Bytes(); //missing
        public readonly static byte[] Debug = "DEBUG".ToUtf8Bytes(); //missing
        public readonly static byte[] Config = "CONFIG".ToUtf8Bytes(); //missing
        public readonly static byte[] Client = "CLIENT".ToUtf8Bytes();
        public readonly static byte[] List = "LIST".ToUtf8Bytes();
        public readonly static byte[] Kill = "KILL".ToUtf8Bytes();
        public readonly static byte[] SetName = "SETNAME".ToUtf8Bytes();

        public readonly static byte[] GetName = "GETNAME".ToUtf8Bytes();
        //public readonly static byte[] Get = "GET".ToUtf8Bytes();
        //public readonly static byte[] Set = "SET".ToUtf8Bytes();

        public readonly static byte[] StrLen = "STRLEN".ToUtf8Bytes();
        public readonly static byte[] Set = "SET".ToUtf8Bytes();
        public readonly static byte[] Get = "GET".ToUtf8Bytes();
        public readonly static byte[] GetSet = "GETSET".ToUtf8Bytes();
        public readonly static byte[] MGet = "MGET".ToUtf8Bytes();
        public readonly static byte[] SetNx = "SETNX".ToUtf8Bytes();
        public readonly static byte[] SetEx = "SETEX".ToUtf8Bytes();
        public readonly static byte[] Persist = "PERSIST".ToUtf8Bytes();
        public readonly static byte[] PSetEx = "PSETEX".ToUtf8Bytes();
        public readonly static byte[] MSet = "MSET".ToUtf8Bytes();
        public readonly static byte[] MSetNx = "MSETNX".ToUtf8Bytes();
        public readonly static byte[] Incr = "INCR".ToUtf8Bytes();
        public readonly static byte[] IncrBy = "INCRBY".ToUtf8Bytes();
        public readonly static byte[] IncrByFloat = "INCRBYFLOAT".ToUtf8Bytes();
        public readonly static byte[] Decr = "DECR".ToUtf8Bytes();
        public readonly static byte[] DecrBy = "DECRBY".ToUtf8Bytes();
        public readonly static byte[] Append = "APPEND".ToUtf8Bytes();
        public readonly static byte[] Substr = "SUBSTR".ToUtf8Bytes();
        public readonly static byte[] GetRange = "GETRANGE".ToUtf8Bytes();
        public readonly static byte[] SetRange = "SETRANGE".ToUtf8Bytes();
        public readonly static byte[] GetBit = "GETBIT".ToUtf8Bytes();
        public readonly static byte[] SetBit = "SETBIT".ToUtf8Bytes();
        public readonly static byte[] BitCount = "BITCOUNT".ToUtf8Bytes();

        public readonly static byte[] Load = "LOAD".ToUtf8Bytes();

        //public readonly static byte[] Exists = "EXISTS".ToUtf8Bytes();
    }

    public class RedisResponseException
        : RedisException
    {
        public RedisResponseException(string message)
            : base(message)
        {
        }

        public RedisResponseException(string message, string code) : base(message)
        {
            Code = code;
        }

        public string Code { get; private set; }
    }


    internal class BufferPool
    {
        internal static void Flush()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                Interlocked.Exchange(ref pool[i], null); // and drop the old value on the floor
            }
        }

        private BufferPool()
        {
        }

        const int PoolSize = 1000; //1.45MB
        internal const int BufferLength = 1450; //MTU size - some headers
        private static readonly object[] pool = new object[PoolSize];

        internal static byte[] GetBuffer()
        {
            object tmp;
            for (int i = 0; i < pool.Length; i++)
            {
                if ((tmp = Interlocked.Exchange(ref pool[i], null)) != null)
                    return (byte[]) tmp;
            }

            return new byte[BufferLength];
        }

        internal static void ResizeAndFlushLeft(ref byte[] buffer, int toFitAtLeastBytes, int copyFromIndex, int copyBytes)
        {
            Debug.Assert(buffer != null);
            Debug.Assert(toFitAtLeastBytes > buffer.Length);
            Debug.Assert(copyFromIndex >= 0);
            Debug.Assert(copyBytes >= 0);

            // try doubling, else match
            int newLength = buffer.Length * 2;
            if (newLength < toFitAtLeastBytes) newLength = toFitAtLeastBytes;

            var newBuffer = new byte[newLength];
            if (copyBytes > 0)
            {
                Buffer.BlockCopy(buffer, copyFromIndex, newBuffer, 0, copyBytes);
            }

            if (buffer.Length == BufferLength)
            {
                ReleaseBufferToPool(ref buffer);
            }

            buffer = newBuffer;
        }

        internal static void ReleaseBufferToPool(ref byte[] buffer)
        {
            if (buffer == null) return;
            if (buffer.Length == BufferLength)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    if (Interlocked.CompareExchange(ref pool[i], buffer, null) == null)
                    {
                        break; // found a null; swapped it in
                    }
                }
            }

            // if no space, just drop it on the floor
            buffer = null;
        }
    }
}