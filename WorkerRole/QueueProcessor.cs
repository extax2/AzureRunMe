using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using Microsoft.WindowsAzure;
using System.IO;

namespace WorkerRole
{
    class QueueProcessor
    {
        private volatile bool stop = false;

        public string InboundQueue { get; private set; }

        public string OutboundQueue { get; private set; }

        public string InboundBlobContainer { get; private set; }

        public string OutboundBlobContainer { get; private set; }

        public string Command { get; private set; }

        public int CommandTimeout { get; private set; }

        public string WorkingDirectory { get; private set; }

        public string EnvironmentVariables { get; private set; }

        public CloudDrive CloudDrive { get; private set; }


        public QueueProcessor(string inboundQueue, string outboundQueue, int commandTimeout, string workingDirectory, string environmentVariables, CloudDrive cloudDrive, string inboundBlobContainer, string outboundBlobContainer, string command)
        {
            this.InboundQueue = inboundQueue;
            this.OutboundQueue = outboundQueue;
            this.CommandTimeout = commandTimeout;
            this.WorkingDirectory = workingDirectory;
            this.EnvironmentVariables = environmentVariables;
            this.CloudDrive = cloudDrive;
            this.InboundBlobContainer = inboundBlobContainer;
            this.OutboundBlobContainer = outboundBlobContainer;
            this.Command = command;
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
                    var message = inboundQueue.GetMessage(TimeSpan.FromSeconds(this.CommandTimeout));

                    if (null == message)
                    {
                        Tracer.WriteLine("No tasks - wait", "Information");
                        System.Threading.Thread.Sleep(30 * 1000);
                        continue;
                    }

                    ProcessMessage(inboundQueue, outboundQueue, message);
                }
             
                catch (Exception ex)
                {
                    Tracer.WriteLine(ex, "Error");
                }
            }
        }

        private void ProcessMessage(CloudQueue inboundQueue, CloudQueue outboundQueue, CloudQueueMessage message)
        {
            if (null == message) throw new ArgumentNullException("message");
            if (null == inboundQueue) throw new ArgumentNullException("inboundQueue");
            if (null == outboundQueue) throw new ArgumentNullException("outboundQueue");

            ProcessingTask task = ProcessingTask.FromString(message.AsString);
            if (null == task)
            {
                throw new NullReferenceException("Task is null");
            }

            if (!string.IsNullOrWhiteSpace(task.InputFilename))
            {
                DownloadBlob(this.InboundBlobContainer, task.InputFilename, this.WorkingDirectory);
            }

            Tracer.WriteLine(string.Format("Starting task '{0}'", task), "Information");

            task.ProcessingTime = DateTime.UtcNow;
            RunMe.Run(this.WorkingDirectory, this.EnvironmentVariables, this.Command, this.CloudDrive, task.InputFilename);
            task.ProcessingDuration = (int)(DateTime.UtcNow - task.ProcessingTime).TotalSeconds;

            if (!string.IsNullOrWhiteSpace(task.OutputFilename))
            {
                UploadBlob(this.InboundBlobContainer, task.InputFilename, this.WorkingDirectory);
            }

            Tracer.WriteLine(string.Format("Finished task '{0}'", task), "Information");

            outboundQueue.AddMessage(new CloudQueueMessage(task.ToString()));
            inboundQueue.DeleteMessage(message);

            Tracer.WriteLine(string.Format("Clearing up task {0}", task), "Information");
            if (!string.IsNullOrWhiteSpace(task.InputFilename))
            {
                this.DeleteFile(task.InputFilename);
            }

            if (!string.IsNullOrWhiteSpace(task.OutputFilename))
            {
                this.DeleteFile(task.OutputFilename);
            }

            Tracer.WriteLine(string.Format("Task complete '{0}'", task), "Information");
        }

        private static CloudStorageAccount GetStorageAccount()
        {
            return CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"));
        }


        private static CloudQueue CreateQueue(string queueName)
        {
            if (string.IsNullOrEmpty(queueName)) throw new ArgumentException("A queue name can't be null or empty", queueName);
            if (queueName.Length < 3 || queueName.Length > 63) throw new ArgumentException("A queue name must be from 3 to 63 characters long - \"", queueName + "\"");
            
            // upper case characters and spaces are not allows in queue names.
            queueName = queueName.Replace(" ", "").ToLower();
            var queue = GetStorageAccount().CreateCloudQueueClient().GetQueueReference(queueName);
            queue.CreateIfNotExist();
            
            return queue;
        }


        private void DownloadBlob(string containerName, string filename, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentNullException("containerName");
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException("filename");
            if (string.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentNullException("workingDirectory");

            CloudBlobClient blobClient = GetStorageAccount().CreateCloudBlobClient();

            blobClient.RetryPolicy = RetryPolicies.Retry(100, TimeSpan.FromSeconds(1));
            blobClient.Timeout = TimeSpan.FromSeconds(600);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(filename);

            Tracer.WriteLine(string.Format("Downloading {0} to {1}", blob.Uri, workingDirectory), "Information");

            blob.DownloadToFile(Path.Combine(this.WorkingDirectory, filename));

            Tracer.WriteLine(string.Format("Downloaded {1}", blob.Uri), "Information");
        }

        private void DeleteFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException("filename");

            try
            {
                File.Delete(Path.Combine(this.WorkingDirectory, filename));
            }
            catch (FileNotFoundException)
            {
            
            }
        }

        private void UploadBlob(string containerName, string filename, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(containerName)) throw new ArgumentNullException("containerName");
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException("filename");
            if (string.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentNullException("workingDirectory");

            CloudBlobClient blobClient = GetStorageAccount().CreateCloudBlobClient();

            blobClient.RetryPolicy = RetryPolicies.Retry(100, TimeSpan.FromSeconds(1));
            blobClient.Timeout = TimeSpan.FromSeconds(600);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            container.CreateIfNotExist();
            
            CloudBlockBlob blob = container.GetBlockBlobReference(filename);

            Tracer.WriteLine(string.Format("Uploading {0}", blob.Uri), "Information");

            blob.UploadFile(Path.Combine(this.WorkingDirectory, filename));

            Tracer.WriteLine(string.Format("Upload {0}", blob.Uri), "Information");
        }

    }
}
