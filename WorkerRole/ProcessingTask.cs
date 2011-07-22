using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

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

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            XmlSerializer serializer = new XmlSerializer(typeof(ProcessingTask));
            using (XmlWriter writer = XmlWriter.Create(sb))
            {
                serializer.Serialize(writer, this);
            }
            return sb.ToString();
        }

        public static ProcessingTask FromString(string value)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ProcessingTask));
            using (System.IO.StringReader stream = new System.IO.StringReader(value))
            {
                return serializer.Deserialize(stream) as ProcessingTask;
            }
        }

    }
}
