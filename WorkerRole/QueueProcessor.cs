using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;

namespace WorkerRole
{
    class QueueProcessor
    {
        private volatile bool stop = false;

        public string InboundQueue { get; private set; }

        public string OutboundQueue { get; private set; }

        public int MessageTimeout { get; private set; }

        public string WorkingDirectory { get; private set; }

        public string EnvironmentVariables { get; private set; }

        public CloudDrive CloudDrive { get; private set; }

        public QueueProcessor(string inboundQueue, string outboundQueue, int MessageTimeout, string workingDirectory, string environmentVariables, CloudDrive cloudDrive)
        {
            this.InboundQueue = inboundQueue;
            this.OutboundQueue = outboundQueue;
            this.MessageTimeout = MessageTimeout;
            this.WorkingDirectory = workingDirectory;
            this.EnvironmentVariables = environmentVariables;
            this.CloudDrive = cloudDrive;
        }

        public void Start()
        {
            this.stop = false;
            Action action = new Action(() => this.Process());
            action.BeginInvoke(null, null);
        }

        public void Stop()
        {
            this.stop = true;
        }


        /// <summary>
        /// Runs an infinite loop, dequeuing messages from an inbound queue, runs them, and then adds the message to the outbound queue
        /// </summary>
        private void Process()
        {
            CloudQueue inboundQueue = null;
            CloudQueue outboundQueue = null;
            try
            {
                inboundQueue = CreateQueue(this.InboundQueue);
                outboundQueue = CreateQueue(this.OutboundQueue);
            }
            catch (Exception ex)
            {
                Tracer.WriteLine(ex, "Error");
                return;
            }

            while (!stop)
            {
                try
                {
                    Tracer.WriteLine("Inspecting Queues", "Information");

                    // dequeue
                    var message = inboundQueue.GetMessage(TimeSpan.FromSeconds(this.MessageTimeout));

                    if (null == message)
                    {
                        Tracer.WriteLine("Nothing to process - wait", "Information");
                        System.Threading.Thread.Sleep(30 * 1000);
                        continue;
                    }

                    var command = message.AsString;

                    // process
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        Tracer.WriteLine(string.Format("Starting command '{0}'", command), "Information");

                        RunMe.Run(this.WorkingDirectory, this.EnvironmentVariables, command, this.CloudDrive);

                        Tracer.WriteLine(string.Format("Finished command '{0}'", command), "Information");
                    }

                    // enqueue
                    Tracer.WriteLine(string.Format("Signalling completion '{0}'", command), "Information");

                    outboundQueue.AddMessage(new CloudQueueMessage(command));
                    inboundQueue.DeleteMessage(message);

                    Tracer.WriteLine(string.Format("Complete '{0}'", command), "Information");
                }
                catch (Exception ex)
                {
                    Tracer.WriteLine(ex, "Error");
                }
            }
        }

        public CloudQueue CreateQueue(string queueName)
        {
            if (string.IsNullOrEmpty(queueName)) throw new ArgumentException("A queue name can't be null or empty", queueName);
            if (queueName.Length < 3 || queueName.Length > 63) throw new ArgumentException("A queue name must be from 3 to 63 characters long - \"", queueName + "\"");
            
            // upper case characters and spaces are not allows in queue names.
            queueName = queueName.Replace(" ", "").ToLower();
            var queue = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString")).CreateCloudQueueClient().GetQueueReference(queueName);
            queue.CreateIfNotExist();
            
            return queue;
        }


    }
}
