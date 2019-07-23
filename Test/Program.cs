﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Watson;
using WatsonMesh.Fuck;

namespace TestNetCore
{
    class Program
    {
        static string _Ip;
        static int _Port;
        static MeshSettings _Settings;
        static Peer _Self;
        static WatsonMeshNetwork _Mesh;

        static bool _RunForever = true;

        static void Main(string[] args)
        {
            _Ip = InputString("Listener IP:", "127.0.0.1", false);
            _Port = InputInteger("Listener Port:", 8000, true, false);

            _Settings = new MeshSettings();
            _Settings.AcceptInvalidCertificates = true;
            _Settings.AutomaticReconnect = true; 
            _Settings.MutuallyAuthenticate = false;
            _Settings.PresharedKey = null;
            _Settings.ReadDataStream = false;
            _Settings.ReadStreamBufferSize = 65536;
            _Settings.ReconnectIntervalMs = 1000;

            _Self = new Peer(_Ip, _Port);

            _Mesh = new WatsonMeshNetwork(_Settings, _Self);
            _Mesh.PeerConnected = PeerConnected;
            _Mesh.PeerDisconnected = PeerDisconnected;
            _Mesh.AsyncMessageReceived = AsyncMessageReceived;
            _Mesh.SyncMessageReceived = SyncMessageReceived;
            _Mesh.AsyncStreamReceived = AsyncStreamReceived;
            _Mesh.SyncStreamReceived = SyncStreamReceived;
	        _Mesh.PeerConnectRequest = PeerConnectRequest;

			_Mesh.Start();

            while (_RunForever)
            {
                string userInput = InputString("WatsonMesh [? for help] >", null, false);

                List<Peer> peers;

                switch (userInput)
                {
                    case "?":
                        Menu();
                        break;

                    case "q":
                    case "quit":
                        _RunForever = false;
                        break;

                    case "c":
                    case "cls":
                        Console.Clear();
                        break;

                    case "list":
                        peers = _Mesh.GetPeers();
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Configured peers: " + peers.Count);
                            foreach (Peer curr in peers) Console.WriteLine("  " + curr.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        break;

                    case "failed":
                        peers = _Mesh.GetDisconnectedPeers();
                        if (peers != null && peers.Count > 0)
                        {
                            Console.WriteLine("Failed peers: " + peers.Count);
                            foreach (Peer currPeer in peers) Console.WriteLine("  " + currPeer.ToString());
                        }
                        else
                        {
                            Console.WriteLine("None");
                        }
                        break;

                    case "sendasync":
                        SendAsync();
                        break;

                    case "sendsync":
                        SendSync();
                        break;
                         
                    case "bcast":
                        Broadcast(); 
                        break;

                    case "add":
	                    Add();
                        break;

                    case "del":
                        _Mesh.Remove(
                            new Peer(
                                InputString("Peer IP:", "127.0.0.1", false),
                                InputInteger("Peer port:", 8000, true, false),
                                false));
                        break;

                    case "health":
                        Console.WriteLine("Healthy: " + _Mesh.IsHealthy());
                        break;

                    case "nodehealth":
                        Console.WriteLine(
                            _Mesh.IsHealthy(
                                InputString("Peer IP:", "127.0.0.1", false),
                                InputInteger("Peer port:", 8000, true, false)));
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?           help, this menu");
            Console.WriteLine("  cls         clear the screen");
            Console.WriteLine("  q           quit the application");
            Console.WriteLine("  list        list all peers");
            Console.WriteLine("  failed      list failed peers");
            Console.WriteLine("  add         add a peer");
            Console.WriteLine("  del         delete a peer");
            Console.WriteLine("  sendasync   send a message to a peer asynchronously");
            Console.WriteLine("  sendsync    send a message to a peer and await a response"); 
            Console.WriteLine("  bcast       send a message to all peers");
            Console.WriteLine("  health      display if the mesh is healthy");
            Console.WriteLine("  nodehealth  display if a connection to a peer is healthy");
        }

	    static void Add()
	    {
		    var ipAddress = InputString("Peer IP:", "127.0.0.1", false);
		    var port = InputInteger("Peer port:", 8000, true, false);
		    var peer = new Peer(ipAddress, port, false);
			_Mesh.Add(peer);
	    }

        static void SendAsync()
        { 
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);
             
            if (_Mesh.SendAsync(
                InputString("Peer IP", "127.0.0.1", false),
                InputInteger("Peer port:", 8000, true, false), 
                inputBytes.Length,
                inputStream))
            {
                Console.WriteLine("Success"); 
            }
            else
            {
                Console.WriteLine("Failed");
            }
        }

	    static void SendConnectRequest(Peer peer)
	    {
		    var currNode = JsonConvert.SerializeObject(new ConnectRequest {Ip = _Ip, Port = _Port});

			byte[] inputBytes = Encoding.UTF8.GetBytes(currNode);
			MemoryStream inputStream = new MemoryStream(inputBytes);
		    inputStream.Seek(0, SeekOrigin.Begin);

		    if (_Mesh.SendAsync(peer.Ip, peer.Port, inputBytes.Length, inputStream))
		    {
			    Console.WriteLine("Success");
		    }
		    else
		    {
			    Console.WriteLine("Failed");
		    }
	    }


		static void SendSync()
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);

            long responseLength = 0;
            Stream responseStream = null;
             
            if (_Mesh.SendSync(
                InputString("Peer IP", "127.0.0.1", false),
                InputInteger("Peer port:", 8000, true, false),
                InputInteger("Timeout ms:", 15000, true, false),
                inputBytes.Length, 
                inputStream, 
                out responseLength,
                out responseStream))
            {
                Console.WriteLine("Success: " + responseLength + " bytes");
                if (responseLength > 0)
                {
                    if (responseStream != null)
                    {
                        if (responseStream.CanRead)
                        {
                            Console.WriteLine("Response: " + Encoding.UTF8.GetString(ReadStream(responseLength, responseStream)));
                        }
                        else
                        {
                            Console.WriteLine("Cannot read from response stream");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Response stream is null");
                    }
                }
                else
                {
                    Console.WriteLine("(null)");
                }
            }
            else
            {
                Console.WriteLine("Failed");
            } 
        }

        static void Broadcast()
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(InputString("Data:", "some data", false));
            MemoryStream inputStream = new MemoryStream(inputBytes);
            inputStream.Seek(0, SeekOrigin.Begin);

            if (_Mesh.Broadcast(inputBytes.Length, inputStream))
            {
                Console.WriteLine("Success");
            }
            else
            {
                Console.WriteLine("Failed");
            } 
        }

        static bool PeerConnected(Peer peer)
        {
            Console.WriteLine("Peer " + peer.ToString() + " connected");
	        SendConnectRequest(peer);
			return true;
        }

        static bool PeerDisconnected(Peer peer)
        {
            Console.WriteLine("Peer " + peer.ToString() + " disconnected");
            return true;
        }

        static bool AsyncMessageReceived(Peer peer, byte[] data)
        {
            Console.WriteLine("Async message received from " + peer.ToString() + ": " + Encoding.UTF8.GetString(data));
            return true;
        }

        static SyncResponse SyncMessageReceived(Peer peer, byte[] data)
        {
            Console.WriteLine("Sync message received from " + peer.ToString() + ": " + Encoding.UTF8.GetString(data));
            Console.WriteLine("");
            Console.WriteLine("Press ENTER and THEN type your response!");
            string resp = InputString("Response:", "This is a response", false);
            return new SyncResponse(Encoding.UTF8.GetBytes(resp));
        }

        static bool AsyncStreamReceived(Peer peer, long contentLength, Stream stream)
        {
            Console.WriteLine("Async stream received from " + peer.ToString() + ": " + contentLength + " bytes in stream");
            byte[] data = ReadStream(contentLength, stream);
            Console.WriteLine(Encoding.UTF8.GetString(data)); 
            return true;
        }

        static SyncResponse SyncStreamReceived(Peer peer, long contentLength, Stream stream)
        {
            Console.WriteLine("Sync stream received from " + peer.ToString() + ": " + contentLength + " bytes in stream");
            byte[] data = ReadStream(contentLength, stream);
            Console.WriteLine(Encoding.UTF8.GetString(data));
            Console.WriteLine("");
            Console.WriteLine("Press ENTER and THEN type your response!"); 
            string resp = InputString("Response:", "This is a response", false);
            byte[] respData = Encoding.UTF8.GetBytes(resp);
            MemoryStream ms = new MemoryStream(respData);
            ms.Seek(0, SeekOrigin.Begin);
            return new SyncResponse(respData.Length, ms);
        }

	    static bool PeerConnectRequest(ConnectRequest connectRequest)
	    {
		    if (!_Mesh.GetPeers().Any(x => x.Ip == connectRequest.Ip && x.Port == connectRequest.Port))
		    {
			    _Mesh.Add(new Peer(connectRequest.Ip, connectRequest.Port));
		    }

			return true;
	    }

        static bool InputBoolean(string question, bool yesDefault)
        {
            Console.Write(question);

            if (yesDefault) Console.Write(" [Y/n]? ");
            else Console.Write(" [y/N]? ");

            string userInput = Console.ReadLine();

            if (String.IsNullOrEmpty(userInput))
            {
                if (yesDefault) return true;
                return false;
            }

            userInput = userInput.ToLower();

            if (yesDefault)
            {
                if (
                    (String.Compare(userInput, "n") == 0)
                    || (String.Compare(userInput, "no") == 0)
                   )
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (
                    (String.Compare(userInput, "y") == 0)
                    || (String.Compare(userInput, "yes") == 0)
                   )
                {
                    return true;
                }

                return false;
            }
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }

        static int InputInteger(string question, int defaultAnswer, bool positiveOnly, bool allowZero)
        {
            while (true)
            {
                Console.Write(question);
                Console.Write(" [" + defaultAnswer + "] ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    return defaultAnswer;
                }

                int ret = 0;
                if (!Int32.TryParse(userInput, out ret))
                {
                    Console.WriteLine("Please enter a valid integer.");
                    continue;
                }

                if (ret == 0)
                {
                    if (allowZero)
                    {
                        return 0;
                    }
                }

                if (ret < 0)
                {
                    if (positiveOnly)
                    {
                        Console.WriteLine("Please enter a value greater than zero.");
                        continue;
                    }
                }

                return ret;
            }
        }

        static byte[] ReadStream(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            long bytesRemaining = contentLength;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    if (bytesRead == buffer.Length)
                    {
                        ret = AppendBytes(ret, buffer);
                    }
                    else
                    {
                        byte[] temp = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                        ret = AppendBytes(ret, temp);
                    }
                    bytesRemaining -= bytesRead;
                }
            }

            return ret;
        }

        static byte[] ReadStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            int bytesRead = 0;
            byte[] buffer = new byte[65536];
            byte[] ret = null;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (bytesRead == buffer.Length)
                {
                    ret = AppendBytes(ret, buffer);
                }
                else
                {
                    byte[] temp = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, temp, 0, bytesRead);
                    ret = AppendBytes(ret, buffer);
                }
            }

            return ret;
        }

        static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            byte[] ret;

            if (head == null || head.Length == 0)
            {
                if (tail == null || tail.Length == 0) return null;

                ret = new byte[tail.Length];
                Buffer.BlockCopy(tail, 0, ret, 0, tail.Length);
                return ret;
            }
            else
            {
                if (tail == null || tail.Length == 0) return head;

                ret = new byte[head.Length + tail.Length];
                Buffer.BlockCopy(head, 0, ret, 0, head.Length);
                Buffer.BlockCopy(tail, 0, ret, head.Length, tail.Length);
                return ret;
            }
        } 
    }
}
