﻿// <copyright file="FtpServer.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zhaobang.FtpServer.Authenticate;
using Zhaobang.FtpServer.Connections;
using Zhaobang.FtpServer.File;
using Zhaobang.FtpServer.Trace;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// The class to run an FTP server
    /// </summary>
    public sealed class FtpServer
    {
        private IDataConnectionFactory dataConnFactory;
        private IAuthenticator authenticator;
        private IFileProviderFactory fileProviderFactory;

        private IPEndPoint endPoint;
        private TcpListener tcpListener;

        private FtpTracer tracer = new FtpTracer();

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class
        /// with <see cref="SimpleFileProviderFactory"/>, <see cref="LocalDataConnectionFactory"/>,
        /// and <see cref="AnonymousAuthenticator"/>.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21</param>
        /// <param name="baseDirectory">The directory to provide files</param>
        public FtpServer(IPEndPoint endPoint, string baseDirectory)
        {
            this.endPoint = endPoint;
            tcpListener = new TcpListener(endPoint);

            fileProviderFactory = new SimpleFileProviderFactory(baseDirectory);
            dataConnFactory = new LocalDataConnectionFactory();
            authenticator = new AnonymousAuthenticator();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FtpServer"/> class.
        /// The server uses custom file, data connection, and authentication provider.
        /// </summary>
        /// <param name="endPoint">The local end point to listen, usually 0.0.0.0:21</param>
        /// <param name="fileProviderFactory">The <see cref="IFileProviderFactory"/> to use</param>
        /// <param name="dataConnFactory">The <see cref="IDataConnectionFactory"/> to use</param>
        /// <param name="authenticator">The <see cref="IAuthenticator"/> to use</param>
        public FtpServer(
            IPEndPoint endPoint,
            IFileProviderFactory fileProviderFactory,
            IDataConnectionFactory dataConnFactory,
            IAuthenticator authenticator)
        {
            this.endPoint = endPoint;
            tcpListener = new TcpListener(endPoint);

            this.fileProviderFactory = fileProviderFactory;
            this.dataConnFactory = dataConnFactory;
            this.authenticator = authenticator;

            tracer.CommandInvoked += Tracer_CommandInvoked;
            tracer.ReplyInvoked += Tracer_ReplyInvoked;
        }

        private void Tracer_ReplyInvoked(string replyCode, IPEndPoint remoteAddress)
        {
            System.Diagnostics.Debug.WriteLine($"{remoteAddress}, reply, {replyCode}");
        }

        private void Tracer_CommandInvoked(string command, IPEndPoint remoteAddress)
        {
            System.Diagnostics.Debug.WriteLine($"{remoteAddress}, command, {command}");
        }

        /// <summary>
        /// Gets the manager that provides <see cref="IDataConnectionFactory"/> for each user
        /// </summary>
        internal IDataConnectionFactory DataConnector { get => dataConnFactory; }

        /// <summary>
        /// Gets the manager that authenticates user
        /// </summary>
        internal IAuthenticator Authenticator { get => authenticator; }

        /// <summary>
        /// Gets the manager that provides <see cref="IFileProviderFactory"/> for each user
        /// </summary>
        internal IFileProviderFactory FileManager { get => fileProviderFactory; }

        /// <summary>
        /// Gets the instance of <see cref="FtpTracer"/> to trace FTP commands and replies
        /// </summary>
        public FtpTracer Tracer => tracer;

        /// <summary>
        /// The certificate for FTP over TLS. Keep it null to disable FTP over TLS.
        /// </summary>
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Start the FTP server
        /// </summary>
        /// <param name="cancellationToken">Token to stop the FTP server</param>
        /// <returns>The task that waits until the server stops</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                tcpListener.Start();
                cancellationToken.Register(() => tcpListener.Stop());
                while (true)
                {
                    TcpClient tcpClient;
                    try
                    {
                        tcpClient = await tcpListener.AcceptTcpClientAsync().WithCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    try
                    {
                        ControlConnection handler = new ControlConnection(this, tcpClient);
                        var result = handler.RunAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                        tcpClient.Dispose();
                    }
                }
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}
