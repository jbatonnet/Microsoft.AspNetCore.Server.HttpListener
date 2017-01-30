using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.HttpListener
{
    using HttpListener = System.Net.HttpListener;

    internal class HttpListenerServer : IServer, IDisposable
    {
        public IFeatureCollection Features { get; } = new Http.Features.FeatureCollection();

        private IHttpApplication<object> application;
        private ServerAddressesFeature serverAddresses = new ServerAddressesFeature();

        private HttpListener httpListener;
        private List<Task> listenerTasks;
        private CancellationTokenSource listenerCancellationToken = new CancellationTokenSource();

        public HttpListenerServer(ILoggerFactory loggerFactory)
        {
            Features.Set<IServerAddressesFeature>(serverAddresses);

            int threadCount = Environment.ProcessorCount - 1;
            listenerTasks = new List<Task>(threadCount == 0 ? 1 : threadCount);
        }

        public void Start<TContext>(IHttpApplication<TContext> application)
        {
            // Save application
            this.application = new HttpApplicationWrapper<TContext>(application);

            // Setup web server
            httpListener = new HttpListener();

            httpListener.Prefixes.Remove("http://*/");
            foreach (string address in serverAddresses.Addresses)
                httpListener.Prefixes.Add(address + "/");

            // Start listener
            httpListener.Start();

            while (listenerTasks.Count < listenerTasks.Capacity)
            {
                Task listenerTask = HandleContexts();
                listenerTasks.Add(listenerTask);
            }
        }

        private async Task HandleContexts()
        {
            while (httpListener.IsListening && !listenerCancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context = await httpListener.GetContextAsync();

                try
                {
                    FeatureContext featureContext = new FeatureContext(context);

                    object applicationContext = application.CreateContext(featureContext.Features);

                    await application.ProcessRequestAsync(applicationContext);

                    await featureContext.OnStart();
                    
                    application.DisposeContext(applicationContext, null);

                    await featureContext.OnCompleted();
                }
                catch (Exception e)
                {
                    context.Response.StatusCode = 500;

                    using (StreamWriter writer = new StreamWriter(context.Response.OutputStream))
                        writer.Write(e.ToString());
                }
                finally
                {
                    context.Response.Close();
                }
            }
        }

        public void Dispose()
        {
            listenerCancellationToken.Cancel();

            foreach (Task task in listenerTasks)
                task.Wait();
            listenerTasks.Clear();

            httpListener.Close();
        }
    }
}