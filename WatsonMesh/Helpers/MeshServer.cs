using System;
using System.IO;
using WatsonTcp;

namespace Watson
{
    /// <summary>
    /// Watson mesh networking server.
    /// </summary>
    internal class MeshServer
    {
        #region Public-Members
         
        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<string, bool> ClientConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<string, bool> ClientDisconnected = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// </summary>
        public Func<string, byte[], bool> MessageReceived = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<string, long, Stream, bool> StreamReceived = null;
         
        #endregion

        #region Private-Members
         
        private readonly MeshSettings _settings;
        private Peer _self;
        private WatsonTcpServer _tcpServer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="self">Node details for the local node.</param>
        public MeshServer(MeshSettings settings, Peer self)
        {
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _self = self ?? throw new ArgumentNullException(nameof(self));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the Watson mesh server.
        /// </summary>
        public void Start()
        {
	        _tcpServer = _self.Ssl 
		        ? new WatsonTcpServer(_self.Ip, _self.Port, _self.PfxCertificateFile, _self.PfxCertificatePassword)
				: new WatsonTcpServer(_self.Ip, _self.Port);

            _tcpServer.AcceptInvalidCertificates = _settings.AcceptInvalidCertificates; 
            _tcpServer.MutuallyAuthenticate = _settings.MutuallyAuthenticate;
            _tcpServer.PresharedKey = _settings.PresharedKey;
            _tcpServer.ReadDataStream = _settings.ReadDataStream;
            _tcpServer.ReadStreamBufferSize = _settings.ReadStreamBufferSize;

            _tcpServer.ClientConnected = MeshServerClientConnected;
            _tcpServer.ClientDisconnected = MeshServerClientDisconnected;
            _tcpServer.MessageReceived = MeshServerMessageReceived;
            _tcpServer.StreamReceived = MeshServerStreamReceived;

            _tcpServer.Start();
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="ipPort">IP address and port of the remoteclient, of the form IP:port.</param>
        public void DisconnectClient(string ipPort)
        {
            Console.WriteLine("DisconnectClient " + ipPort);

            if (string.IsNullOrEmpty(ipPort))
	            throw new ArgumentNullException(nameof(ipPort));

            _tcpServer.DisconnectClient(ipPort);
        }

        #endregion

        #region Private-Methods

        private bool MeshServerClientConnected(string ipPort)
        { 
            if (ClientConnected != null)
            { 
                return ClientConnected(ipPort);
            }
            else
            { 
                return true;
            }
        }

        private bool MeshServerClientDisconnected(string ipPort)
        { 
            if (ClientDisconnected != null)
            { 
                return ClientDisconnected(ipPort);
            }
            else
            { 
                return true;
            }
        }

        private bool MeshServerMessageReceived(string ipPort, byte[] data)
        { 
            if (MessageReceived != null)
            { 
                return MessageReceived(ipPort, data);
            }
            else
            { 
                return true;
            }
        }

        private bool MeshServerStreamReceived(string ipPort, long contentLength, Stream stream)
        { 
            if (StreamReceived != null)
            { 
                return StreamReceived(ipPort, contentLength, stream);
            }
            else
            { 
                return true;
            }
        }

        #endregion
    }
}
