/*This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
Mats 'Afr0' Vederhus. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using GonzoNet.Encryption;

namespace GonzoNet
{
    public delegate void NetworkErrorDelegate(SocketException Exception);
    public delegate void ReceivedPacketDelegate(PacketStream Packet);
    public delegate void OnConnectedDelegate(LoginArgsContainer LoginArgs);

    public class NetworkClient
    {
        protected Listener m_Listener;
        private Socket m_Sock;
        private string m_IP;
        private int m_Port;

        private bool m_Connected = false;

        //Buffer for storing packets that were not fully read.
        private PacketStream m_TempPacket;

        //The number of bytes to be sent. See Send()
        private int m_NumBytesToSend = 0;
        private byte[] m_RecvBuf;

        private EncryptionMode m_EMode;
        private Encryptor m_ClientEncryptor;

        public Encryptor ClientEncryptor
        {
            get
            {
                if (m_ClientEncryptor == null)
                {
                    switch(m_EMode)
                    {
                        case EncryptionMode.AESCrypto:
                            m_ClientEncryptor = new AESEncryptor("");
                            return m_ClientEncryptor;
                        default: //Should never end up here, so doesn't really matter what we put...
                            m_ClientEncryptor = new AESEncryptor("");
                            return m_ClientEncryptor;
                    }
                }

                return m_ClientEncryptor;
            }

            set { m_ClientEncryptor = value; }
        }

        protected LoginArgsContainer m_LoginArgs;

        public event NetworkErrorDelegate OnNetworkError;
        public event ReceivedPacketDelegate OnReceivedData;
        public event OnConnectedDelegate OnConnected;

        public NetworkClient(string IP, int Port, EncryptionMode EMode, bool KeepAlive)
        {
            m_Sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			if(KeepAlive)
				m_Sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            m_IP = IP;
            m_Port = Port;

            m_EMode = EMode;

            m_RecvBuf = new byte[11024];
        }

        /// <summary>
        /// Initializes a client that listens for data.
        /// </summary>
        /// <param name="ClientSocket">The client's socket.</param>
        /// <param name="Server">The Listener instance calling this constructor.</param>
        /// <param name="ReceivePulse">Should this client receive a pulse at a regular interval?</param>
        public NetworkClient(Socket ClientSocket, Listener Server, EncryptionMode EMode)
        {
            m_Sock = ClientSocket;
            m_Listener = Server;
            m_RecvBuf = new byte[11024];

            m_EMode = EMode;

            m_Sock.BeginReceive(m_RecvBuf, 0, m_RecvBuf.Length, SocketFlags.None,
                new AsyncCallback(ReceiveCallback), m_Sock);
        }

        /// <summary>
        /// Connects to the login server.
        /// </summary>
        /// <param name="LoginArgs">Arguments used for login. Can be null.</param>
        public void Connect(LoginArgsContainer LoginArgs)
        {
            m_LoginArgs = LoginArgs;

            if (LoginArgs != null)
            {
                m_ClientEncryptor = LoginArgs.Enc;
                m_ClientEncryptor.Username = LoginArgs.Username;
            }
            //Making sure that the client is not already connecting to the loginserver.
            if (!m_Sock.Connected)
            {
                m_Sock.BeginConnect(IPAddress.Parse(m_IP), m_Port, new AsyncCallback(ConnectCallback), m_Sock);
            }
        }

        public void Send(byte[] Data)
        {
            try
            {
                m_NumBytesToSend = Data.Length;
                m_Sock.BeginSend(Data, 0, Data.Length, SocketFlags.None, new AsyncCallback(OnSend), m_Sock);
            }
            catch (SocketException)
            {
                //TODO: Reconnect?
                this.Disconnect();
            }
        }

        /// <summary>
        /// Sends an encrypted packet to the server.
        /// Automatically appends the length of the packet after the ID, as 
        /// the encrypted data can be smaller or longer than that of the
        /// unencrypted data.
        /// </summary>
        /// <param name="PacketID">The ID of the packet (will remain unencrypted).</param>
        /// <param name="Data">The data that will be encrypted.</param>
        public void SendEncrypted(byte PacketID, byte[] Data)
        {
			byte[] EncryptedData;

			lock (m_ClientEncryptor)
			{
				m_NumBytesToSend = Data.Length;
				EncryptedData = m_ClientEncryptor.FinalizePacket(PacketID, Data);
			}

			try
			{
				m_Sock.BeginSend(EncryptedData, 0, EncryptedData.Length, SocketFlags.None,
					new AsyncCallback(OnSend), m_Sock);
			}
			catch(SocketException)
			{
				Disconnect();
			}
        }

        /*public void On(PacketType PType, ReceivedPacketDelegate PacketDelegate)
        {

        }*/

        protected virtual void OnSend(IAsyncResult AR)
        {
            Socket ClientSock = (Socket)AR.AsyncState;
            int NumBytesSent = ClientSock.EndSend(AR);
        }

        private void BeginReceive(/*object State*/)
        {
            //if (m_Connected)
            //{
                m_Sock.BeginReceive(m_RecvBuf, 0, m_RecvBuf.Length, SocketFlags.None,
                    new AsyncCallback(ReceiveCallback), m_Sock);
            //}
        }

        private void ConnectCallback(IAsyncResult AR)
        {
            try
            {
                Socket Sock = (Socket)AR.AsyncState;
                Sock.EndConnect(AR);

                m_Connected = true;
                BeginReceive();

                if(OnConnected != null)
                    OnConnected(m_LoginArgs);
            }
            catch (SocketException E)
            {
                //Hopefully all classes inheriting from NetworkedUIElement will subscribe to this...
                if (OnNetworkError != null)
                    OnNetworkError(E);
            }
        }

        private void OnPacket(ProcessedPacket packet, PacketHandler handler)
        {
            if (OnReceivedData != null)
            {
                OnReceivedData(packet);
            }

            handler.Handler(this, packet);
        }

        protected virtual void ReceiveCallback(IAsyncResult AR)
        {
            try
            {
                Socket Sock = (Socket)AR.AsyncState;
                int NumBytesRead = Sock.EndReceive(AR);

                /** Cant do anything with this! **/
                if (NumBytesRead == 0) { return; }

                byte[] TmpBuf = new byte[NumBytesRead];
                Buffer.BlockCopy(m_RecvBuf, 0, TmpBuf, 0, NumBytesRead);

                //The packet is given an ID of 0x00 because its ID is currently unknown.
                PacketStream TempPacket = new PacketStream(0x00, (ushort)NumBytesRead, TmpBuf);
                byte ID = TempPacket.PeekByte(0);
                Logger.Log("Received packet: " + ID, LogLevel.info);

                ushort PacketLength = 0;
                var handler = FindPacketHandler(ID);

                if (handler != null)
                {
                    PacketLength = handler.Length;
                    Logger.Log("Found matching PacketID!\r\n\r\n", LogLevel.info);

                    if (NumBytesRead == PacketLength)
                    {
                        Logger.Log("Got packet - exact length!\r\n\r\n", LogLevel.info);
                        m_RecvBuf = new byte[11024];

                        OnPacket(new ProcessedPacket(ID, handler.Encrypted, handler.VariableLength, PacketLength,
                            m_ClientEncryptor, TempPacket.ToArray()), handler);
                    }
                    else if (NumBytesRead < PacketLength)
                    {
                        m_TempPacket = new PacketStream(ID, PacketLength);
                        byte[] TmpBuffer = new byte[NumBytesRead];

                        //Store the number of bytes that were read in the temporary buffer.
                        Logger.Log("Got data, but not a full packet - stored " +
                            NumBytesRead.ToString() + "bytes!\r\n\r\n", LogLevel.info);
                        Buffer.BlockCopy(m_RecvBuf, 0, TmpBuffer, 0, NumBytesRead);
                        m_TempPacket.WriteBytes(TmpBuffer);

                        //And reset the buffers!
                        m_RecvBuf = new byte[11024];
                        TmpBuffer = null;
                    }
                    else if (PacketLength == 0)
                    {
                        Logger.Log("Received variable length packet!\r\n", LogLevel.info);

                        if (NumBytesRead > (int)PacketHeaders.UNENCRYPTED) //Header is 3 bytes.
                        {
                            PacketLength = TempPacket.PeekUShort(1);

                            if (NumBytesRead == PacketLength)
                            {
                                Logger.Log("Received exact number of bytes for packet!\r\n",
                                    LogLevel.info);

                                m_RecvBuf = new byte[11024];
                                m_TempPacket = null;

                                OnPacket(new ProcessedPacket(ID, handler.Encrypted, handler.VariableLength, PacketLength,
                                    m_ClientEncryptor, TempPacket.ToArray()), handler);
                            }
                            else if (NumBytesRead < PacketLength)
                            {
                                Logger.Log("Didn't receive entire packet - stored: " + PacketLength + " bytes!\r\n",
                                    LogLevel.info);

                                TempPacket.SetLength(PacketLength);
                                m_TempPacket = TempPacket;
                                m_RecvBuf = new byte[11024];
                            }
                            else if (NumBytesRead > PacketLength)
                            {
                                Logger.Log("Received more bytes than needed for packet. Excess: " +
                                    (NumBytesRead - PacketLength) + "\r\n", LogLevel.info);

                                byte[] TmpBuffer = new byte[NumBytesRead - PacketLength];
                                Buffer.BlockCopy(TempPacket.ToArray(), 0, TmpBuffer, 0, TmpBuffer.Length);
                                m_TempPacket = new PacketStream(TmpBuffer[0], (ushort)(NumBytesRead - PacketLength),
                                    TmpBuffer);

                                byte[] PacketBuffer = new byte[PacketLength];
                                Buffer.BlockCopy(TempPacket.ToArray(), 0, PacketBuffer, 0, PacketBuffer.Length);

                                m_RecvBuf = new byte[11024];

                                OnPacket(new ProcessedPacket(ID, handler.Encrypted, handler.VariableLength, PacketLength,
                                    m_ClientEncryptor, PacketBuffer), handler);
                            }
                        }
                    }
                }
                else
                {
                    if (m_TempPacket != null)
                    {
                        if (m_TempPacket.Length < m_TempPacket.BufferLength)
                        {
                            //Received the exact number of bytes needed to complete the stored packet.
                            if ((m_TempPacket.BufferLength + NumBytesRead) == m_TempPacket.Length)
                            {
                                byte[] TmpBuffer = new byte[NumBytesRead];
                                Buffer.BlockCopy(m_RecvBuf, 0, TmpBuffer, 0, NumBytesRead);

                                m_RecvBuf = new byte[11024];
                                TmpBuffer = null;
                            }
                            //Received more than the number of bytes needed to complete the packet!
                            else if ((m_TempPacket.BufferLength + NumBytesRead) > m_TempPacket.Length)
                            {
                                ushort Target = (ushort)((m_TempPacket.BufferLength + NumBytesRead) - m_TempPacket.Length);
                                byte[] TmpBuffer = new byte[Target];

                                Buffer.BlockCopy(m_RecvBuf, 0, TmpBuffer, 0, Target);
                                m_TempPacket.WriteBytes(TmpBuffer);

                                //Now we have a full packet, so call the received event!
                                OnPacket(new ProcessedPacket(m_TempPacket.PacketID, handler.Encrypted,
                                    handler.VariableLength, (ushort)m_TempPacket.Length, m_ClientEncryptor, m_TempPacket.ToArray()), handler);

                                //Copy the remaining bytes in the receiving buffer.
                                TmpBuffer = new byte[NumBytesRead - Target];
                                Buffer.BlockCopy(m_RecvBuf, Target, TmpBuffer, 0, (NumBytesRead - Target));

                                //Give the temporary packet an ID of 0x00 since we don't know its ID yet.
                                TempPacket = new PacketStream(0x00, (ushort)(NumBytesRead - Target), TmpBuffer);
                                ID = TempPacket.PeekByte(0);
                                handler = FindPacketHandler(ID);

                                //This SHOULD be an existing ID, but let's sanity-check it...
                                if (handler != null)
                                {
                                    m_TempPacket = new PacketStream(ID, handler.Length, TempPacket.ToArray());

                                    //Congratulations, you just received another packet!
                                    if (m_TempPacket.Length == m_TempPacket.BufferLength)
                                    {
                                        OnPacket(new ProcessedPacket(m_TempPacket.PacketID, handler.Encrypted,
                                            handler.VariableLength, (ushort)m_TempPacket.Length, m_ClientEncryptor,
                                            m_TempPacket.ToArray()), handler);

                                        //No more data to store on this read, so reset everything...
                                        m_TempPacket = null;
                                        TmpBuffer = null;
                                        m_RecvBuf = new byte[11024];
                                    }
                                }
                                else
                                {
                                    //Houston, we have a problem (this should never occur)!
                                    this.Disconnect();
                                }
                            }
                        }
                    }
                }

                m_Sock.BeginReceive(m_RecvBuf, 0, m_RecvBuf.Length, SocketFlags.None,
                    new AsyncCallback(ReceiveCallback), m_Sock);
            }
            catch (SocketException)
            {
                Disconnect();
            }
        }

        /// <summary>
        /// This socket's remote IP. Will return null if the socket is not connected remotely.
        /// </summary>
        public string RemoteIP
        {
            get
            {
                IPEndPoint RemoteEP = (IPEndPoint)m_Sock.RemoteEndPoint;

                if (RemoteEP != null)
                    return RemoteEP.Address.ToString();
                else
                    return null;
            }
        }

		/// <summary>
		/// This socket's remote port. Will return 0 if the socket is not connected remotely.
		/// </summary>
		public int RemotePort
		{
			get
			{
				IPEndPoint RemoteEP = (IPEndPoint)m_Sock.RemoteEndPoint;

                if (RemoteEP != null)
                    return RemoteEP.Port;
                else
                    return 0;
			}
		}

        /// <summary>
        /// Disconnects this NetworkClient instance and stops
        /// all sending and receiving of data.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                m_Sock.Shutdown(SocketShutdown.Both);
                m_Sock.Disconnect(true);

                if(m_Listener != null)
                    m_Listener.RemoveClient(this);
            }
            catch
            {
            }
        }

        private PacketHandler FindPacketHandler(byte ID)
        {
            PacketHandler Handler = PacketHandlers.Get(ID);

            if (Handler != null)
                return Handler;
            else return null;
        }
    }
}
