using System;
using System.IO;
using System.Security.Authentication;
using System.Threading.Tasks;
using WatsonTcp;

namespace Watson
{
    /// <summary>
    /// Watson mesh networking client.
    /// </summary>
    internal class MeshClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug = false;

        /// <summary>
        /// The peer object.
        /// </summary>
        public Peer Peer { get; private set; }

        /// <summary>
        /// Function to call when authentication is requested.
        /// </summary>
        public Func<string> AuthenticationRequested = null;

        /// <summary>
        /// Function to call when authentication succeeded.
        /// </summary>
        public Func<bool> AuthenticationSucceeded = null;

        /// <summary>
        /// Function to call when authentication failed.
        /// </summary>
        public Func<bool> AuthenticationFailure = null;

        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<Peer, bool> ServerConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<Peer, bool> ServerDisconnected = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// </summary>
        public Func<Peer, byte[], bool> MessageReceived = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<Peer, long, Stream, bool> StreamReceived = null;
          
        /// <summary>
        /// Check if the local client is connected to the remote server.
        /// </summary>
        /// <returns>True if connected.</returns>
        public bool Connected
        {
            get
            {
                if (_tcpClient == null) return false;
                return _tcpClient.Connected;
            }
        }

        #endregion

        #region Private-Members

        private bool _disposed = false; 
        private readonly MeshSettings _settings; 
        private WatsonTcpClient _tcpClient;  

        #endregion

        #region Constructors-and-Factories
        
        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="peer">Peer.</param>
        public MeshClient(MeshSettings settings, Peer peer)
        {
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Establish TCP (with or without SSL) connection to the peer server.
        /// </summary>
        public void Start()
        {
            if (Peer.Ssl)
            {
                _tcpClient = new WatsonTcpClient(
                    Peer.Ip,
                    Peer.Port,
                    Peer.PfxCertificateFile,
                    Peer.PfxCertificatePassword);
            }
            else
            {
                _tcpClient = new WatsonTcpClient(
                    Peer.Ip,
                    Peer.Port);
            }

            _tcpClient.AcceptInvalidCertificates = _settings.AcceptInvalidCertificates; 
            _tcpClient.MutuallyAuthenticate = _settings.MutuallyAuthenticate;
            _tcpClient.ReadDataStream = _settings.ReadDataStream;
            _tcpClient.ReadStreamBufferSize = _settings.ReadStreamBufferSize;

            _tcpClient.AuthenticationRequested = MeshClientAuthenticationRequested;
            _tcpClient.AuthenticationSucceeded = MeshClientAuthenticationSucceeded;
            _tcpClient.AuthenticationFailure = MeshClientAuthenticationFailure;
            _tcpClient.ServerConnected = MeshClientServerConnected;
            _tcpClient.ServerDisconnected = MeshClientServerDisconnected;
            _tcpClient.StreamReceived = MeshClientStreamReceived;
            _tcpClient.MessageReceived = MeshClientMessageReceived;

            try
            {
                _tcpClient.Start();
            }
            catch (Exception)
            {
                Task.Run(() => MeshClientServerDisconnected());
            }
        }

        /// <summary>
        /// Send data to the remote server.
        /// </summary>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
             
            try
            {
                return await _tcpClient.SendAsync(data);
            }
            catch (Exception)
            {  
                return false;
            }
        }

        /// <summary>
        /// Send data to the remote server.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data to send.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
             
            try
            { 
                stream.Seek(0, SeekOrigin.Begin);
                return await _tcpClient.SendAsync(contentLength, stream);
            }
            catch (Exception)
            { 
                return false;
            }
        }

        #endregion

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        { 
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_tcpClient != null) _tcpClient.Dispose();
            }

            _disposed = true;
        }
         
        private string MeshClientAuthenticationRequested()
        {
            if (AuthenticationRequested != null) return AuthenticationRequested();
            if (!string.IsNullOrEmpty(_settings.PresharedKey)) return _settings.PresharedKey;
            else throw new AuthenticationException("Cannot authenticate using supplied preshared key to peer " + Peer.ToString());
        }

        private bool MeshClientAuthenticationSucceeded()
        {
            if (AuthenticationSucceeded != null) return AuthenticationSucceeded();
            return true;
        }

        private bool MeshClientAuthenticationFailure()
        {
            if (AuthenticationFailure != null) return AuthenticationFailure();
            return true;
        }

        private bool MeshClientServerConnected()
        {
            if (ServerConnected != null)
            {
                return ServerConnected(Peer);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientServerDisconnected()
        { 
            Task.Run(() => ReconnectToServer());
            if (ServerDisconnected != null)
            {
                return ServerDisconnected(Peer);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientMessageReceived(byte[] data)
        {
            if (MessageReceived != null)
            {
                return MessageReceived(Peer, data);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientStreamReceived(long contentLength, Stream stream)
        {
            if (StreamReceived != null)
            {
                return StreamReceived(Peer, contentLength, stream);
            }
            else
            {
                return true;
            }
        }

        private void ReconnectToServer()
        {
            if (!_settings.AutomaticReconnect)
	            return;

            while (true)
            { 
                try
                {
                    Task.Delay(_settings.ReconnectIntervalMs).Wait();
                    Start(); 
                    break;
                }
                catch (Exception)
                { 
                }
            }
        }

        #endregion
    }
}
