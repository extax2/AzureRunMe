using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WorkerRole
{

    /// <summary>
    /// A class used to signal the start of the batch job, and confirm completion
    /// </summary>
    public class ProcessingTask
    {

        /// <summary>
        /// The URI for the input file to process. Optionally set by the task initiator, if an input file is being passed in to process
        /// </summary>
        public string InputFilename { get; set; }

        /// <summary>
        /// If the process creates a file, set the name here, this is the name which will be uploaded to blob storage.
        /// </summary>
        public string OutputFilename { get; set; }

        /// <summary>
        /// The time it took to process the task (in seconds)
        /// </summary>
        public int ProcessingDuration { get; set; }

        /// <summary>
        /// The time the processing started
        /// </summary>
        public DateTime ProcessingTime { get; set; }

    }
}
