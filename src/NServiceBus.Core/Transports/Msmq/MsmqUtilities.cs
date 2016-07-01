namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Messaging;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Xml;
    using DeliveryConstraints;
    using Logging;
    using Performance.TimeToBeReceived;
    using Transports;
    using Transports.Msmq;

    class MsmqUtilities
    {
        static MsmqAddress GetIndependentAddressForQueue(MessageQueue q)
        {
            var arr = q.FormatName.Split('\\');
            var queueName = arr[arr.Length - 1];

            var directPrefixIndex = arr[0].IndexOf(DIRECTPREFIX);
            if (directPrefixIndex >= 0)
            {
                return new MsmqAddress(queueName, arr[0].Substring(directPrefixIndex + DIRECTPREFIX.Length));
            }

            var tcpPrefixIndex = arr[0].IndexOf(DIRECTPREFIX_TCP);
            if (tcpPrefixIndex >= 0)
            {
                return new MsmqAddress(queueName, arr[0].Substring(tcpPrefixIndex + DIRECTPREFIX_TCP.Length));
            }

            try
            {
                // the pessimistic approach failed, try the optimistic approach
                arr = q.QueueName.Split('\\');
                queueName = arr[arr.Length - 1];
                return new MsmqAddress(queueName, q.MachineName);
            }
            catch
            {
                throw new Exception($"Could not translate format name to independent name: {q.FormatName}");
            }
        }

        public static Dictionary<string, string> ExtractHeaders(Message msmqMessage)
        {
            var headers = DeserializeMessageHeaders(msmqMessage);

            //note: we can drop this line when we no longer support interop btw v3 + v4
            if (msmqMessage.ResponseQueue != null)
            {
                headers[Headers.ReplyToAddress] = GetIndependentAddressForQueue(msmqMessage.ResponseQueue).ToString();
            }

            if (Enum.IsDefined(typeof(MessageIntentEnum), msmqMessage.AppSpecific))
            {
                headers[Headers.MessageIntent] = ((MessageIntentEnum) msmqMessage.AppSpecific).ToString();
            }

            headers[Headers.CorrelationId] = GetCorrelationId(msmqMessage, headers);

            return headers;
        }

        static string GetCorrelationId(Message message, Dictionary<string, string> headers)
        {
            string correlationId;

            if (headers.TryGetValue(Headers.CorrelationId, out correlationId))
            {
                return correlationId;
            }

            if (message.CorrelationId == "00000000-0000-0000-0000-000000000000\\0")
            {
                return null;
            }

            //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end that the sender added to make it compatible
            //The replace can be removed in v5 since only v3 messages will need this
            return message.CorrelationId.Replace("\\0", string.Empty);
        }

        public static Dictionary<string, string> DeserializeMessageHeaders(Message m)
        {
            var bytes = m.Extension;
            var result = new Dictionary<string, string>();

            if (bytes.Length == 0)
            {
                return result;
            }

            //This is to make us compatible with v3 messages that are affected by this bug:
            //http://stackoverflow.com/questions/3779690/xml-serialization-appending-the-0-backslash-0-or-null-character
            var extension = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            object o;
            using (var stream = new StringReader(extension))
            {
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    CheckCharacters = false
                }))
                {
                    o = headerSerializer.Deserialize(reader);
                }
            }

            foreach (var pair in (List<HeaderInfo>) o)
            {
                if (pair.Key != null)
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            return result;
        }

        public static Dictionary<string, string> DeserializeMessageHeadersFast(Message m)
        {
            var bytes = m.Extension;
            var result = new Dictionary<string, string>();

            var byteCount = bytes.Length;

            if (byteCount == 0)
            {
                return result;
            }

            var pos = 0;

            // <xml
            if (TryMoveNext(bytes, HeaderBytes.Open, ref pos) == false)
            {
                return result;
            }
            pos += 1;

            // <ArrayOfHeaderInfo
            if (TryMoveNext(bytes, HeaderBytes.Open, ref pos) == false)
            {
                return result;
            }
            pos += 1;

            // loop over <HeaderInfo>, moving to the starting tag
            while (TryMoveNext(bytes, HeaderBytes.Open, ref pos))
            {
                if (TryConsumeTag(bytes, ref pos, HeaderBytes.HeaderInfo) == false)
                {
                    // this is not a header info
                    break;
                }
                // inside <HeaderInfo>...
                //                    ^

                ConsumeTag(bytes, ref pos, HeaderBytes.HeaderKey);
                // <Key>...
                //      ^
                var keyStart = pos;
                MoveNext(bytes, HeaderBytes.Open, ref pos);

                var key = GetKey(new ArraySegment<byte>(bytes, keyStart, pos - keyStart));

                ConsumeTag(bytes, ref pos, HeaderBytes.HeaderKeyEnd);
                // </Key>...
                //       ^

                MoveNext(bytes, HeaderBytes.Open, ref pos);

                if (TryConsumeTag(bytes, ref pos, HeaderBytes.HeaderValue))
                {
                    // <Value>...
                    //        ^

                    var valueStart = pos;
                    MoveNext(bytes, HeaderBytes.Open, ref pos);

                    var value = GetValue(new ArraySegment<byte>(bytes, valueStart, pos - valueStart));
                    result.Add(key, value);

                    TryConsumeTag(bytes, ref pos, HeaderBytes.HeaderValueEnd);
                    // </Value>...
                    //         ^
                }
                else
                {
                    if (TryConsumeTag(bytes, ref pos, HeaderBytes.HeaderValueEmpty))
                    {
                        // <Value />...
                        //          ^
                        result.Add(key, "");
                    }
                    else
                    {
                        throw new SerializationException();
                    }
                }
                ConsumeTag(bytes, ref pos, HeaderBytes.HeaderInfoEnd);
                // </HeaderInfo>...
                //              ^
            }

            return result;
        }

        static string GetKey(ArraySegment<byte> s)
        {
            // TODO: memoize and return "internalized" string for this
            return Encoding.UTF8.GetString(s.Array, s.Offset, s.Count);
        }

        static string GetValue(ArraySegment<byte> s)
        {
            return Encoding.UTF8.GetString(s.Array, s.Offset, s.Count);
        }

        /// <summary>
        /// Consumes a tag moving the <paramref name="pos" /> rigth after the tag.
        /// </summary>
        static void ConsumeTag(byte[] bytes, ref int pos, ArraySegment<byte> tag)
        {
            if (TryConsumeTag(bytes, ref pos, tag) == false)
            {
                throw new SerializationException();
            }
        }

        static bool TryConsumeTag(byte[] bytes, ref int position, ArraySegment<byte> tag)
        {
            var pos = position;
            MoveNext(bytes, HeaderBytes.Open, ref pos);
            var start = pos + 1;
            MoveNext(bytes, HeaderBytes.Close, ref pos);
            var end = pos - 1;


            var headerInfo = new ArraySegment<byte>(bytes, start, end - start + 1);
            if (UnsafeCompare(headerInfo, tag))
            {
                pos += 1;
                position = pos;
                return true;
            }

            return false;
        }

        static bool TryMoveNext(byte[] bytes, byte b, ref int pos)
        {
            if (pos < 0)
            {
                return false;
            }
            pos = Array.IndexOf(bytes, b, pos);
            return true;
        }

        static void MoveNext(byte[] bytes, byte b, ref int pos)
        {
            if (TryMoveNext(bytes, b, ref pos) == false)
            {
                throw new SerializationException();
            }
        }

        static unsafe bool UnsafeCompare(ArraySegment<byte> a1, ArraySegment<byte> a2)
        {
            if (a1.Count != a2.Count)
                return false;
            fixed(byte* p1 = a1.Array, p2 = a2.Array)
            {
                var x1 = p1 + a1.Offset;
                var x2 = p2 + a2.Offset;
                var l = a1.Count;
                for (var i = 0; i < l/8; i++, x1 += 8, x2 += 8)
                    if (*((long*) x1) != *((long*) x2)) return false;
                if ((l & 4) != 0)
                {
                    if (*((int*) x1) != *((int*) x2)) return false;
                    x1 += 4;
                    x2 += 4;
                }
                if ((l & 2) != 0)
                {
                    if (*((short*) x1) != *((short*) x2)) return false;
                    x1 += 2;
                    x2 += 2;
                }
                if ((l & 1) != 0) if (*x1 != *x2) return false;
                return true;
            }
        }

        public static Message Convert(OutgoingMessage message, IEnumerable<DeliveryConstraint> deliveryConstraints)
        {
            var result = new Message();

            if (message.Body != null)
            {
                result.BodyStream = new MemoryStream(message.Body);
            }


            AssignMsmqNativeCorrelationId(message, result);
            result.Recoverable = !deliveryConstraints.Any(c => c is NonDurableDelivery);

            DiscardIfNotReceivedBefore timeToBeReceived;

            if (deliveryConstraints.TryGet(out timeToBeReceived) && timeToBeReceived.MaxTime < MessageQueue.InfiniteTimeout)
            {
                result.TimeToBeReceived = timeToBeReceived.MaxTime;
            }

            var addCorrIdHeader = !message.Headers.ContainsKey("CorrId");

            using (var stream = new MemoryStream())
            {
                var headers = message.Headers.Select(pair => new HeaderInfo
                {
                    Key = pair.Key,
                    Value = pair.Value
                }).ToList();

                if (addCorrIdHeader)
                {
                    headers.Add(new HeaderInfo
                    {
                        Key = "CorrId",
                        Value = result.CorrelationId
                    });
                }

                headerSerializer.Serialize(stream, headers);
                result.Extension = stream.ToArray();
            }

            var messageIntent = default(MessageIntentEnum);

            string messageIntentString;

            if (message.Headers.TryGetValue(Headers.MessageIntent, out messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }

            result.AppSpecific = (int) messageIntent;


            return result;
        }

        static void AssignMsmqNativeCorrelationId(OutgoingMessage message, Message result)
        {
            string correlationIdHeader;

            if (!message.Headers.TryGetValue(Headers.CorrelationId, out correlationIdHeader))
            {
                return;
            }

            if (string.IsNullOrEmpty(correlationIdHeader))
            {
                return;
            }

            Guid correlationId;

            if (Guid.TryParse(correlationIdHeader, out correlationId))
            {
                //msmq required the id's to be in the {guid}\{incrementing number} format so we need to fake a \0 at the end to make it compatible
                result.CorrelationId = $"{correlationIdHeader}\\0";
                return;
            }

            try
            {
                if (correlationIdHeader.Contains("\\"))
                {
                    var parts = correlationIdHeader.Split('\\');

                    int number;

                    if (parts.Length == 2 && Guid.TryParse(parts.First(), out correlationId) &&
                        int.TryParse(parts[1], out number))
                    {
                        result.CorrelationId = correlationIdHeader;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to assign a native correlation id for message: {message.MessageId}", ex);
            }
        }

        public static bool TryOpenQueue(MsmqAddress msmqAddress, out MessageQueue messageQueue)
        {
            messageQueue = null;

            var queuePath = msmqAddress.PathWithoutPrefix;

            Logger.Debug($"Checking if queue exists: {queuePath}.");

            if (msmqAddress.IsRemote)
            {
                Logger.Debug("Queue is on remote machine.");
                Logger.Debug("If this does not succeed (like if the remote machine is disconnected), processing will continue.");
            }

            var path = msmqAddress.PathWithoutPrefix;
            try
            {
                if (MessageQueue.Exists(path))
                {
                    messageQueue = new MessageQueue(path);

                    Logger.DebugFormat("Verified that the queue: [{0}] existed", queuePath);

                    return true;
                }
            }
            catch (MessageQueueException)
            {
                // Can happen because of an invalid queue path or trying to access a remote private queue.
                // Either way, this results in a failed attempt, therefore returning false.

                return false;
            }

            return false;
        }

        public static bool TryCreateQueue(MsmqAddress msmqAddress, string account, bool transactional, out MessageQueue messageQueue)
        {
            messageQueue = null;

            var queuePath = msmqAddress.PathWithoutPrefix;
            var created = false;

            try
            {
                messageQueue = MessageQueue.Create(queuePath, transactional);

                Logger.DebugFormat($"Created queue, path: [{queuePath}], identity: [{account}], transactional: [{transactional}]");

                created = true;
            }
            catch (MessageQueueException ex)
            {
                var logError = !(msmqAddress.IsRemote && (ex.MessageQueueErrorCode == MessageQueueErrorCode.IllegalQueuePathName));

                if (ex.MessageQueueErrorCode == MessageQueueErrorCode.QueueExists)
                {
                    //Solve the race condition problem when multiple endpoints try to create same queue (e.g. error queue).
                    logError = false;
                }

                if (logError)
                {
                    Logger.Error($"Could not create queue {msmqAddress}. Processing will still continue.", ex);
                }
            }

            return created;
        }

        const string DIRECTPREFIX = "DIRECT=OS:";
        const string DIRECTPREFIX_TCP = "DIRECT=TCP:";
        internal const string PRIVATE = "\\private$\\";

        static System.Xml.Serialization.XmlSerializer headerSerializer = new System.Xml.Serialization.XmlSerializer(typeof(List<HeaderInfo>));
        static ILog Logger = LogManager.GetLogger<MsmqUtilities>();

        class HeaderBytes
        {
            public static byte Open = Encoding.UTF8.GetBytes("<").Single();
            public static byte Close = Encoding.UTF8.GetBytes(">").Single();
            public static readonly ArraySegment<byte> HeaderInfo = new ArraySegment<byte>(Encoding.UTF8.GetBytes("HeaderInfo"));
            public static readonly ArraySegment<byte> HeaderInfoEnd = new ArraySegment<byte>(Encoding.UTF8.GetBytes("/HeaderInfo"));
            public static readonly ArraySegment<byte> HeaderKey = new ArraySegment<byte>(Encoding.UTF8.GetBytes("Key"));
            public static readonly ArraySegment<byte> HeaderKeyEnd = new ArraySegment<byte>(Encoding.UTF8.GetBytes("/Key"));
            public static readonly ArraySegment<byte> HeaderValue = new ArraySegment<byte>(Encoding.UTF8.GetBytes("Value"));
            public static readonly ArraySegment<byte> HeaderValueEnd = new ArraySegment<byte>(Encoding.UTF8.GetBytes("/Value"));
            public static readonly ArraySegment<byte> HeaderValueEmpty = new ArraySegment<byte>(Encoding.UTF8.GetBytes("Value /"));
        }
    }
}