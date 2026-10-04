using Microsoft.Extensions.Logging;

using System;
using System.Net;
using System.Net.Sockets;

namespace ISRORCert.Network
{
    public class AsyncServer : AsyncBase
    {
        public AsyncServer(ILogger<AsyncServer> logger) : base(logger)
        {
        }

        public void Accept(string host, int port, int outstanding, IAsyncInterface @interface)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            if (!IPAddress.TryParse(host, out var address))
            {
                IPHostEntry host_entry = Dns.GetHostEntry(host);
                address = host_entry.AddressList[0];
            }
            socket.Bind(new IPEndPoint(address, port));

            socket.Listen(outstanding);

            for (int x = 0; x < outstanding; ++x)
            {
                AsyncToken token = new AsyncToken { Socket = socket, Interface = @interface };

                SocketAsyncEventArgs acceptEvtArgs = new SocketAsyncEventArgs();
                acceptEvtArgs.UserToken = token;
                acceptEvtArgs.Completed += NetworkOnAccept;
                ProcessAccept(acceptEvtArgs);
            }
        }

        private void DispatchAccept(object? param)
        {
            SocketAsyncEventArgs e = (SocketAsyncEventArgs)param!;

            NetworkOnAccept(null, e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            AsyncToken token = (AsyncToken)e.UserToken!;

            e.AcceptSocket = null;

            try
            {
                if (!token.Socket.AcceptAsync(e))
                {
                    ThreadPool.QueueUserWorkItem(DispatchAccept, e);
                }
            }
            catch (ObjectDisposedException)
            {
                // The listener was closed (shutdown), stop accepting.
            }
        }

        private void NetworkOnAccept(object? sender, SocketAsyncEventArgs e)
        {
            AsyncToken token = (AsyncToken)e.UserToken!;

            Socket? socket = e.AcceptSocket;

            if (e.SocketError != SocketError.Success)
                Logger.LogDebug("Accept failed: {SocketError}", e.SocketError);

            ProcessAccept(e); // Start the next accept asap.

            if (socket == null || !socket.Connected)
            {
                return; // Nothing to handle, the error (if any) was logged above
            }

            AsyncState state = new AsyncState(this, socket, AsyncOperation.Accept, token.Interface); // Now handle the current connection.

            bool result = false;
            try
            {
                result = state.Context.Interface.OnConnect(state.Context);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "OnConnect failed for {EndPoint}", state.EndPoint);
            }

            if (!result)
            {
                try
                {
                    state.Context.Interface.OnError(state.Context); // Ensure the user can cleanup anything before the object dies
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "OnError failed for {EndPoint}", state.EndPoint);
                }

                state.Cleanup(); // Cleanup the socket

                return;
            }

            // Store the state before the first read so a fast disconnect can't remove it before it was added.
            AddState(state);

            try
            {
                state.Read(); // Begin receiving data on the socket
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to start reading from {EndPoint}", state.EndPoint);
                state.Cleanup(); // Cleanup the object
                RemoveState(state);
            }
        }
    }
}
