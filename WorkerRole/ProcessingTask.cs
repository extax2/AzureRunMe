#region Copyright (c) 2010 - 2011 Active Web Solutions Ltd
//
// (C) Copyright 2010 - 2011 Active Web Solutions Ltd
//      All rights reserved.
//
// This software is provided "as is" without warranty of any kind,
// express or implied, including but not limited to warranties as to
// quality and fitness for a particular purpose. Active Web Solutions Ltd
// does not support the Software, nor does it warrant that the Software
// will meet your requirements or that the operation of the Software will
// be uninterrupted or error free or that any defects will be
// corrected. Nothing in this statement is intended to limit or exclude
// any liability for personal injury or death caused by the negligence of
// Active Web Solutions Ltd, its employees, contractors or agents.
//
#endregion

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
